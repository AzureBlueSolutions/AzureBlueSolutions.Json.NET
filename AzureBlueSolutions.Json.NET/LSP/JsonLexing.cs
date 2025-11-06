namespace AzureBlueSolutions.Json.NET
{
    /// <summary>
    /// Kinds of JSON lexemes recognized during tokenization and suitable for syntax highlighting.
    /// </summary>
    public enum JsonLexemeKind
    {
        /// <summary>
        /// The left brace token: <c>{</c>.
        /// </summary>
        LeftBrace,

        /// <summary>
        /// The right brace token: <c>}</c>.
        /// </summary>
        RightBrace,

        /// <summary>
        /// The left bracket token: <c>[</c>.
        /// </summary>
        LeftBracket,

        /// <summary>
        /// The right bracket token: <c>]</c>.
        /// </summary>
        RightBracket,

        /// <summary>
        /// The colon token: <c>:</c>.
        /// </summary>
        Colon,

        /// <summary>
        /// The comma token: <c>,</c>.
        /// </summary>
        Comma,

        /// <summary>
        /// A JSON string literal.
        /// </summary>
        String,

        /// <summary>
        /// A JSON number literal.
        /// </summary>
        Number,

        /// <summary>
        /// The <c>true</c> literal.
        /// </summary>
        True,

        /// <summary>
        /// The <c>false</c> literal.
        /// </summary>
        False,

        /// <summary>
        /// The <c>null</c> literal.
        /// </summary>
        Null,

        /// <summary>
        /// A line (<c>//</c>) or block (<c>/* ... */</c>) comment.
        /// </summary>
        Comment
    }

    /// <summary>
    /// Represents a single token and its source range in the document.
    /// </summary>
    /// <param name="Kind">
    /// The lexeme kind for this token.
    /// </param>
    /// <param name="Range">
    /// The zero-based source range covering the token (end exclusive).
    /// </param>
    public sealed record JsonTokenSpan(JsonLexemeKind Kind, TextRange Range);

    /// <summary>
    /// Lexes JSON text into a sequence of <see cref="JsonTokenSpan"/> items.
    /// Intended for fast, cancellation-aware tokenization to support incremental pipelines.
    /// </summary>
    internal sealed class JsonTokenizer
    {
        private readonly CancellationToken _cancellationToken;
        private readonly Stack<Container> _containers = new();
        private readonly int _maxTokens;
        private readonly string _text;
        private readonly List<JsonTokenSpan> _tokens = [];
        private int _column;
        private bool _expectingProperty;
        private int _index;
        private int _line;

        /// <summary>
        /// Initializes a new tokenizer for the specified JSON text.
        /// </summary>
        /// <param name="text">
        /// The source text to tokenize. If <c>null</c>, an empty string is used.
        /// </param>
        /// <param name="cancellationToken">
        /// A token to observe for cancellation during lexing.
        /// </param>
        /// <param name="maxTokens">
        /// A safety cap for the maximum number of tokens to produce. Minimum is 1,000.
        /// </param>
        public JsonTokenizer(string text, int maxTokens = 2_000_000, CancellationToken cancellationToken = default)
        {
            _text = text;
            _index = 0;
            _line = 0;
            _column = 0;
            _containers.Clear();
            _expectingProperty = false;
            _maxTokens = Math.Max(1_000, maxTokens);
            _cancellationToken = cancellationToken;
        }

        /// <summary>
        /// Tokenizes the entire input and returns the produced tokens.
        /// </summary>
        /// <returns>
        /// A read-only list of <see cref="JsonTokenSpan"/> instances in source order.
        /// </returns>
        public IReadOnlyList<JsonTokenSpan> Tokenize()
        {
            while (!IsEof())
            {
                _cancellationToken.ThrowIfCancellationRequested();
                if (_tokens.Count >= _maxTokens) break;

                var c = Peek();

                if (char.IsWhiteSpace(c))
                {
                    ReadWhitespace();
                    continue;
                }

                if (c == '/')
                    if (TryReadComment(out var range))
                    {
                        _tokens.Add(new JsonTokenSpan(JsonLexemeKind.Comment, range));
                        continue;
                    }

                switch (c)
                {
                    case '{':
                        EmitPunct(JsonLexemeKind.LeftBrace, 1);
                        _containers.Push(Container.Object);
                        _expectingProperty = true;
                        break;
                    case '}':
                        EmitPunct(JsonLexemeKind.RightBrace, 1);
                        if (_containers.Count > 0) _containers.Pop();
                        _expectingProperty = _containers.Count > 0 && _containers.Peek() == Container.Object;
                        break;
                    case '[':
                        EmitPunct(JsonLexemeKind.LeftBracket, 1);
                        _containers.Push(Container.Array);
                        _expectingProperty = false;
                        break;
                    case ']':
                        EmitPunct(JsonLexemeKind.RightBracket, 1);
                        if (_containers.Count > 0) _containers.Pop();
                        _expectingProperty = _containers.Count > 0 && _containers.Peek() == Container.Object;
                        break;
                    case ':':
                        EmitPunct(JsonLexemeKind.Colon, 1);
                        _expectingProperty = false;
                        break;
                    case ',':
                        EmitPunct(JsonLexemeKind.Comma, 1);
                        _expectingProperty = _containers.Count > 0 && _containers.Peek() == Container.Object;
                        break;
                    case '"':
                        var strRange = ReadJsonString();
                        _tokens.Add(new JsonTokenSpan(JsonLexemeKind.String, strRange));
                        break;
                    case 't':
                    case 'f':
                    case 'n':
                        var (kind, length, startLine, startColumn, startOffset) = ReadKeyword();
                        if (length > 0)
                        {
                            var range = MakeRange(startLine, startColumn, startOffset, _line, _column, _index);
                            _tokens.Add(new JsonTokenSpan(kind, range));
                        }
                        else
                        {
                            Advance();
                        }

                        break;
                    default:
                        if (c == '-' ||
                            c == '+' ||
                            char.IsDigit(c))
                        {
                            var num = ReadNumber();
                            if (num.length > 0)
                            {
                                var range = MakeRange(num.startLine, num.startColumn, num.startOffset, _line, _column,
                                    _index);
                                _tokens.Add(new JsonTokenSpan(JsonLexemeKind.Number, range));
                            }
                            else
                            {
                                Advance();
                            }
                        }
                        else
                        {
                            Advance();
                        }

                        break;
                }
            }

            return _tokens;
        }

        /// <summary>
        /// Emits a punctuation token of the specified kind and length at the current position.
        /// </summary>
        private void EmitPunct(JsonLexemeKind kind, int length)
        {
            int sl = _line, sc = _column, so = _index;
            for (var i = 0; i < length; i++) Advance();
            var range = MakeRange(sl, sc, so, _line, _column, _index);
            _tokens.Add(new JsonTokenSpan(kind, range));
        }

        /// <summary>
        /// Reads a JSON number literal starting at the current position.
        /// </summary>
        /// <returns>
        /// A tuple containing the consumed length and starting coordinates; <c>length</c> is zero if parsing fails.
        /// </returns>
        private (int length, int startLine, int startColumn, int startOffset) ReadNumber()
        {
            int sl = _line, sc = _column, so = _index;
            var start = _index;

            if (!IsEof() && Peek() == '-') Advance();

            var any = false;

            while (!IsEof() && char.IsDigit(Peek()))
            {
                any = true;
                Advance();
            }

            if (!IsEof() && Peek() == '.')
            {
                Advance();
                while (!IsEof() && char.IsDigit(Peek()))
                {
                    any = true;
                    Advance();
                }
            }

            if (IsEof() ||
                (Peek() != 'e' && Peek() != 'E')) return any ? (_index - start, sl, sc, so) : (0, sl, sc, so);

            Advance();
            if (!IsEof() && Peek() == '-') Advance();

            while (!IsEof() && char.IsDigit(Peek()))
            {
                any = true;
                Advance();
            }

            return any ? (_index - start, sl, sc, so) : (0, sl, sc, so);
        }

        /// <summary>
        /// Reads one of the JSON literals: <c>true</c>, <c>false</c>, or <c>null</c>.
        /// </summary>
        /// <returns>
        /// A tuple that includes the recognized <see cref="JsonLexemeKind"/> and the consumed length;
        /// <c>length</c> is zero if no keyword matches at the current position.
        /// </returns>
        private (JsonLexemeKind kind, int length, int startLine, int startColumn, int startOffset) ReadKeyword()
        {
            int sl = _line, sc = _column, so = _index;

            if (StartsWith("true"))
            {
                Advance(4);
                return (JsonLexemeKind.True, 4, sl, sc, so);
            }

            if (StartsWith("false"))
            {
                Advance(5);
                return (JsonLexemeKind.False, 5, sl, sc, so);
            }

            if (StartsWith("null"))
            {
                Advance(4);
                return (JsonLexemeKind.Null, 4, sl, sc, so);
            }

            return (JsonLexemeKind.Null, 0, sl, sc, so);
        }

        /// <summary>
        /// Reads a JSON string literal (double-quoted) starting at the current position.
        /// </summary>
        /// <returns>
        /// The source range of the string, including the closing quote if present.
        /// </returns>
        private TextRange ReadJsonString()
        {
            int sl = _line, sc = _column, so = _index;
            Advance();
            var escape = false;

            while (!IsEof())
            {
                _cancellationToken.ThrowIfCancellationRequested();
                var c = Peek();
                Advance();

                if (escape)
                    escape = false;
                else if (c == '\\')
                    escape = true;
                else if (c == '"')
                    break;
                else if (c is '\n' or '\r') break;
            }

            return MakeRange(sl, sc, so, _line, _column, _index);
        }

        /// <summary>
        /// Tries to read a line (<c>//</c>) or block (<c>/*…*/</c>) comment.
        /// </summary>
        /// <param name="range">When successful, receives the range of the comment.</param>
        /// <returns><c>true</c> if a comment was read; otherwise, <c>false</c>.</returns>
        private bool TryReadComment(out TextRange range)
        {
            int sl = _line, sc = _column, so = _index;

            if (StartsWith("//"))
            {
                Advance(2);
                while (!IsEof())
                {
                    _cancellationToken.ThrowIfCancellationRequested();
                    var c = Peek();
                    if (c is '\r' or '\n') break;
                    Advance();
                }

                range = MakeRange(sl, sc, so, _line, _column, _index);
                return true;
            }

            if (StartsWith("/*"))
            {
                Advance(2);
                while (!IsEof())
                {
                    _cancellationToken.ThrowIfCancellationRequested();
                    if (StartsWith("*/"))
                    {
                        Advance(2);
                        break;
                    }

                    Advance();
                }

                range = MakeRange(sl, sc, so, _line, _column, _index);
                return true;
            }

            range = MakeRange(sl, sc, so, _line, _column, _index);
            return false;
        }

        /// <summary>
        /// Consumes whitespace characters from the current position.
        /// </summary>
        private void ReadWhitespace()
        {
            while (!IsEof())
            {
                _cancellationToken.ThrowIfCancellationRequested();
                var c = Peek();
                if (!char.IsWhiteSpace(c)) break;
                Advance();
            }
        }

        /// <summary>
        /// Determines whether the remaining input starts with the provided string.
        /// </summary>
        private bool StartsWith(string s)
        {
            if (_index + s.Length > _text.Length) return false;
            return !s.Where((t, i) =>
                _text[_index + i] != t).Any();
        }

        /// <summary>
        /// Advances the current position by the specified number of characters,
        /// updating line and column counters.
        /// </summary>
        private void Advance(int count = 1)
        {
            for (var k = 0; k < count; k++)
            {
                _cancellationToken.ThrowIfCancellationRequested();
                if (IsEof()) return;

                var c = _text[_index++];

                switch (c)
                {
                    case '\r':
                    {
                        if (_index < _text.Length && _text[_index] == '\n') _index++;
                        _line++;
                        _column = 0;
                        break;
                    }
                    case '\n':
                        _line++;
                        _column = 0;
                        break;
                    default:
                        _column++;
                        break;
                }
            }
        }

        /// <summary>
        /// Returns the current character without consuming it.
        /// </summary>
        private char Peek()
        {
            return _text[_index];
        }

        /// <summary>
        /// Determines whether the end of input has been reached.
        /// </summary>
        private bool IsEof()
        {
            return _index >= _text.Length;
        }

        /// <summary>
        /// Creates a <see cref="TextRange"/> from explicit start and end coordinates.
        /// </summary>
        private static TextRange MakeRange(int sl, int sc, int so, int el, int ec, int eo)
        {
            return new TextRange(new TextPosition(sl, sc, so), new TextPosition(el, ec, eo));
        }

        private enum Container
        {
            Object,
            Array
        }
    }
}