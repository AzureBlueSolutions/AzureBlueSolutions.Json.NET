namespace AzureBlueSolutions.Json.NET;

/// <summary>
/// Kinds of JSON lexemes recognized for highlighting.
/// </summary>
public enum JsonLexemeKind
{
    LeftBrace,
    RightBrace,
    LeftBracket,
    RightBracket,
    Colon,
    Comma,
    String,
    Number,
    True,
    False,
    Null,
    Comment
}

/// <summary>
/// A single token with its range in the source.
/// </summary>
public sealed record JsonTokenSpan(JsonLexemeKind Kind, TextRange Range);

internal sealed class JsonTokenizer
{
    private readonly string _text;
    private readonly CancellationToken _cancellationToken;
    private readonly int _maxTokens;
    private int _index;
    private int _line;
    private int _column;
    private readonly List<JsonTokenSpan> _tokens = [];

    private enum Container
    {
        Object,
        Array
    }

    private readonly Stack<Container> _containers = new();
    private bool _expectingProperty = false;

    /// <summary>
    /// Initializes a new tokenizer for the given text.
    /// </summary>
    public JsonTokenizer(string text, CancellationToken cancellationToken = default, int maxTokens = 2_000_000)
    {
        _text = text ?? string.Empty;
        _cancellationToken = cancellationToken;
        _index = 0;
        _line = 0;
        _column = 0;
        _containers.Clear();
        _expectingProperty = false;
        _maxTokens = Math.Max(1_000, maxTokens);
    }

    /// <summary>
    /// Tokenizes the entire input, honoring cancellation.
    /// </summary>
    public IReadOnlyList<JsonTokenSpan> Tokenize()
    {
        while (!IsEof())
        {
            _cancellationToken.ThrowIfCancellationRequested();

            if (_tokens.Count >= _maxTokens) break;

            char c = Peek();
            if (char.IsWhiteSpace(c))
            {
                ReadWhitespace();
                continue;
            }
            if (c == '/')
            {
                if (TryReadComment(out var range))
                {
                    _tokens.Add(new JsonTokenSpan(JsonLexemeKind.Comment, range));
                    continue;
                }
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
                    var kw = ReadKeyword();
                    if (kw.length > 0)
                    {
                        var kind = kw.kind;
                        var range = MakeRange(kw.startLine, kw.startColumn, kw.startOffset, _line, _column, _index);
                        _tokens.Add(new JsonTokenSpan(kind, range));
                    }
                    else
                    {
                        Advance();
                    }
                    break;

                default:
                    if (c == '-' || c == '+' || char.IsDigit(c))
                    {
                        var num = ReadNumber();
                        if (num.length > 0)
                        {
                            var range = MakeRange(num.startLine, num.startColumn, num.startOffset, _line, _column, _index);
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

    private void EmitPunct(JsonLexemeKind kind, int length)
    {
        int sl = _line, sc = _column, so = _index;
        for (int i = 0; i < length; i++) Advance();
        var range = MakeRange(sl, sc, so, _line, _column, _index);
        _tokens.Add(new JsonTokenSpan(kind, range));
    }

    private (int length, int startLine, int startColumn, int startOffset) ReadNumber()
    {
        int sl = _line, sc = _column, so = _index;
        int start = _index;
        if (!IsEof() && Peek() == '-') Advance();
        bool any = false;
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

        if (IsEof() || (Peek() != 'e' && Peek() != 'E')) return any ? (_index - start, sl, sc, so) : (0, sl, sc, so);
        Advance();
        if (!IsEof() && Peek() == '-') Advance();
        while (!IsEof() && char.IsDigit(Peek()))
        {
            any = true;
            Advance();
        }
        return any ? (_index - start, sl, sc, so) : (0, sl, sc, so);
    }

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

    private TextRange ReadJsonString()
    {
        int sl = _line, sc = _column, so = _index;
        Advance();
        bool escape = false;
        while (!IsEof())
        {
            _cancellationToken.ThrowIfCancellationRequested();

            char c = Peek();
            Advance();
            if (escape)
            {
                escape = false;
            }
            else if (c == '\\')
            {
                escape = true;
            }
            else if (c == '"')
            {
                break;
            }
            else if (c == '\n' || c == '\r')
            {
                break;
            }
        }
        return MakeRange(sl, sc, so, _line, _column, _index);
    }

    private bool TryReadComment(out TextRange range)
    {
        int sl = _line, sc = _column, so = _index;

        if (StartsWith("//"))
        {
            Advance(2);
            while (!IsEof())
            {
                _cancellationToken.ThrowIfCancellationRequested();

                char c = Peek();
                if (c == '\r' || c == '\n') break;
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

    private void ReadWhitespace()
    {
        while (!IsEof())
        {
            _cancellationToken.ThrowIfCancellationRequested();

            char c = Peek();
            if (!char.IsWhiteSpace(c)) break;
            Advance();
        }
    }

    private bool StartsWith(string s)
    {
        if (_index + s.Length > _text.Length) return false;
        for (int i = 0; i < s.Length; i++)
        {
            if (_text[_index + i] != s[i]) return false;
        }
        return true;
    }

    private void Advance(int count = 1)
    {
        for (int k = 0; k < count; k++)
        {
            _cancellationToken.ThrowIfCancellationRequested();

            if (IsEof()) return;
            char c = _text[_index++];
            if (c == '\r')
            {
                if (_index < _text.Length && _text[_index] == '\n')
                {
                    _index++;
                }
                _line++;
                _column = 0;
            }
            else if (c == '\n')
            {
                _line++;
                _column = 0;
            }
            else
            {
                _column++;
            }
        }
    }

    private char Peek() => _text[_index];

    private bool IsEof() => _index >= _text.Length;

    private static TextRange MakeRange(int sl, int sc, int so, int el, int ec, int eo)
        => new(new TextPosition(sl, sc, so), new TextPosition(el, ec, eo));
}