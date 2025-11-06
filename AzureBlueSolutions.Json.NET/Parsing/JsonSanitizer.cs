using System.Text;

namespace AzureBlueSolutions.Json.NET;

/// <summary>
/// Performs fast, cancellation‑aware sanitization and light recovery over JSON‑like text,
/// including comment removal, trailing comma removal, line ending normalization, control‑char
/// filtering, closing unterminated property name strings, inserting missing commas, and
/// inserting a single missing closer. Internal helper used by the parser pipeline.
/// </summary>
/// <param name="removeComments">Whether to remove line and block comments.</param>
/// <param name="removeTrailingCommas">Whether to remove trailing commas before <c>]</c> or <c>}</c>.</param>
/// <param name="removeControlChars">Whether to replace control characters (except LF, TAB) with spaces.</param>
/// <param name="normalizeLineEndings">Whether to normalize CR/CRLF to LF.</param>
/// <param name="fixUnterminatedStrings">Whether to close unterminated property name strings heuristically.</param>
/// <param name="recoverMissingCommas">Whether to insert commas between adjacent values/properties.</param>
/// <param name="recoverMissingClosers">Whether to insert a single missing <c>]</c> or <c>}</c> at EOF or newline boundaries.</param>
/// <param name="cancellationToken">A token to observe for cancellation.</param>
/// <param name="cooperativeYieldEvery">
/// For async runs, yields roughly every N characters to keep the UI responsive.
/// </param>
internal sealed class JsonSanitizer(
    bool removeComments,
    bool removeTrailingCommas,
    bool removeControlChars,
    bool normalizeLineEndings,
    bool fixUnterminatedStrings,
    bool recoverMissingCommas,
    bool recoverMissingClosers,
    CancellationToken cancellationToken = default,
    int cooperativeYieldEvery = 1 << 14)
{
    private readonly CancellationToken _cancellationToken = cancellationToken;
    private readonly bool _fixUnterminatedStrings = fixUnterminatedStrings;
    private readonly bool _normalizeLineEndings = normalizeLineEndings;
    private readonly bool _recoverMissingClosers = recoverMissingClosers;
    private readonly bool _recoverMissingCommas = recoverMissingCommas;
    private readonly bool _removeComments = removeComments;
    private readonly bool _removeControlChars = removeControlChars;
    private readonly bool _removeTrailingCommas = removeTrailingCommas;

    /// <summary>
    /// Runs the sanitizer synchronously over <paramref name="text"/> and returns
    /// the transformed text and a summary of applied changes.
    /// </summary>
    /// <param name="text">The input text to sanitize.</param>
    /// <returns>A <see cref="Result"/> describing the sanitized text and change counters.</returns>
    public Result Sanitize(string text)
    {
        if (string.IsNullOrEmpty(text))
            return new Result { Text = string.Empty, Changed = false };

        _cancellationToken.ThrowIfCancellationRequested();

        var changed = false;
        var bomRemoved = false;
        var lineEndingsNormalized = false;

        if (text.Length > 0 && text[0] == '\uFEFF')
        {
            text = text.AsSpan(1).ToString();
            changed = true;
            bomRemoved = true;
        }

        if (_normalizeLineEndings)
        {
            var normalized = text.Replace("\r\n", "\n").Replace("\r", "\n");
            if (!ReferenceEquals(normalized, text) && !string.Equals(normalized, text))
            {
                text = normalized;
                changed = true;
                lineEndingsNormalized = true;
            }
        }

        var sb = new StringBuilder(text.Length);
        var inString = false;
        var escape = false;
        var inLineComment = false;
        var inBlockComment = false;

        var containers = new Stack<Container>();
        var expectingProperty = false;
        var isPropertyNameString = false;

        var stringContentStart = -1;
        var lastNonWhitespaceInString = -1;

        var lineCommentsRemoved = 0;
        var blockCommentsRemoved = 0;
        var trailingCommasRemoved = 0;
        var controlCharsRemoved = 0;
        var unterminatedStringsClosed = 0;
        var missingCommasInserted = 0;
        var closersInserted = 0;

        for (var i = 0; i < text.Length; i++)
        {
            _cancellationToken.ThrowIfCancellationRequested();

            var c = text[i];
            var next = i + 1 < text.Length ? text[i + 1] : '\0';

            if (inLineComment)
            {
                if (c == '\n')
                {
                    sb.Append('\n');
                    inLineComment = false;
                }
                else
                {
                    sb.Append(' ');
                }

                changed = true;
                continue;
            }

            if (inBlockComment)
            {
                if (c == '\n')
                {
                    sb.Append('\n');
                }
                else if (c == '*' && next == '/')
                {
                    sb.Append(' ');
                    sb.Append(' ');
                    i++;
                    inBlockComment = false;
                }
                else
                {
                    sb.Append(' ');
                }

                changed = true;
                continue;
            }

            if (inString)
            {
                if (_fixUnterminatedStrings && isPropertyNameString && !escape && c == ':')
                {
                    if (lastNonWhitespaceInString >= stringContentStart && stringContentStart >= 0)
                        sb.Length = lastNonWhitespaceInString + 1;

                    sb.Append('\"');
                    inString = false;
                    isPropertyNameString = false;
                    changed = true;
                    unterminatedStringsClosed++;

                    sb.Append(':');
                    expectingProperty = false;
                    continue;
                }

                if (_fixUnterminatedStrings && isPropertyNameString && !escape && (c == '\r' || c == '\n'))
                {
                    if (lastNonWhitespaceInString >= stringContentStart && stringContentStart >= 0)
                        sb.Length = lastNonWhitespaceInString + 1;

                    sb.Append('\"');
                    inString = false;
                    isPropertyNameString = false;
                    changed = true;
                    unterminatedStringsClosed++;

                    sb.Append(c);
                    continue;
                }

                sb.Append(c);

                if (escape)
                {
                    escape = false;
                }
                else if (c == '\\')
                {
                    escape = true;
                }
                else if (c == '\"')
                {
                    inString = false;
                }
                else
                {
                    if (c != ' ' && c != '\t')
                        lastNonWhitespaceInString = sb.Length - 1;
                }

                continue;
            }

            if (_removeComments && c == '/' && next != '\0')
            {
                if (next == '/')
                {
                    sb.Append(' ');
                    sb.Append(' ');
                    i++;
                    inLineComment = true;
                    changed = true;
                    lineCommentsRemoved++;
                    continue;
                }

                if (next == '*')
                {
                    sb.Append(' ');
                    sb.Append(' ');
                    i++;
                    inBlockComment = true;
                    changed = true;
                    blockCommentsRemoved++;
                    continue;
                }
            }

            if (_removeControlChars && c < 0x20 && c != '\n' && c != '\t')
            {
                sb.Append(' ');
                changed = true;
                controlCharsRemoved++;
                continue;
            }

            if (c == '{')
            {
                containers.Push(Container.Object);
                expectingProperty = true;
            }
            else if (c == '}')
            {
                if (containers.Count > 0) containers.Pop();
                expectingProperty = containers.Count > 0 && containers.Peek() == Container.Object;
            }
            else if (c == '[')
            {
                containers.Push(Container.Array);
                expectingProperty = false;
            }
            else if (c == ']')
            {
                if (containers.Count > 0) containers.Pop();
                expectingProperty = containers.Count > 0 && containers.Peek() == Container.Object;
            }
            else if (c == ',')
            {
                expectingProperty = containers.Count > 0 && containers.Peek() == Container.Object;
            }
            else if (c == ':')
            {
                expectingProperty = false;
            }

            if (_removeTrailingCommas && (c == ']' || c == '}'))
            {
                var k = sb.Length - 1;
                while (k >= 0 && (sb[k] == ' ' || sb[k] == '\n' || sb[k] == '\t')) k--;
                if (k >= 0 && sb[k] == ',')
                {
                    sb[k] = ' ';
                    changed = true;
                    trailingCommasRemoved++;
                }
            }

            if (c == '\"')
            {
                sb.Append(c);
                inString = true;
                escape = false;
                isPropertyNameString =
                    expectingProperty && containers.Count > 0 && containers.Peek() == Container.Object;
                stringContentStart = sb.Length;
                lastNonWhitespaceInString = stringContentStart - 1;
                continue;
            }

            if (c == '\n' && containers.Count > 0)
            {
                var nextNonWs = PeekNextNonWhitespace(text, i + 1);

                if (_recoverMissingClosers && nextNonWs is '}' or ']')
                {
                    if (containers.Peek() == Container.Array && nextNonWs == '}')
                    {
                        sb.Append(']');
                        changed = true;
                        closersInserted++;
                        containers.Pop();
                        expectingProperty = containers.Count > 0 && containers.Peek() == Container.Object;
                    }
                    else if (containers.Peek() == Container.Object && nextNonWs == ']')
                    {
                        sb.Append('}');
                        changed = true;
                        closersInserted++;
                        containers.Pop();
                        expectingProperty = containers.Count > 0 && containers.Peek() == Container.Object;
                    }
                }

                if (_recoverMissingCommas)
                {
                    if (containers.Count > 0)
                    {
                        if (containers.Peek() == Container.Object && !expectingProperty && nextNonWs == '\"')
                        {
                            sb.Append(',');
                            changed = true;
                            missingCommasInserted++;
                            expectingProperty = true;
                        }
                        else if (containers.Peek() == Container.Array && IsValueStarter(nextNonWs))
                        {
                            sb.Append(',');
                            changed = true;
                            missingCommasInserted++;
                        }
                    }
                }
            }

            sb.Append(c);
        }

        if (_fixUnterminatedStrings && inString && isPropertyNameString)
        {
            if (lastNonWhitespaceInString >= stringContentStart && stringContentStart >= 0)
                sb.Length = lastNonWhitespaceInString + 1;

            sb.Append('\"');
            changed = true;
            unterminatedStringsClosed++;
        }

        if (_recoverMissingClosers && containers.Count > 0)
        {
            var top = containers.Pop();
            if (top == Container.Object)
            {
                sb.Append('}');
                changed = true;
                closersInserted++;
            }
            else
            {
                sb.Append(']');
                changed = true;
                closersInserted++;
            }
        }

        return new Result
        {
            Text = sb.ToString(),
            Changed = changed,
            LineCommentsRemoved = lineCommentsRemoved,
            BlockCommentsRemoved = blockCommentsRemoved,
            TrailingCommasRemoved = trailingCommasRemoved,
            ControlCharsRemoved = controlCharsRemoved,
            BomRemoved = bomRemoved,
            LineEndingsNormalized = lineEndingsNormalized,
            UnterminatedStringsClosed = unterminatedStringsClosed,
            MissingCommasInserted = missingCommasInserted,
            ClosersInserted = closersInserted
        };
    }

    /// <summary>
    /// Asynchronously runs the sanitizer over <paramref name="text"/> and returns
    /// the transformed text and a summary of applied changes.
    /// </summary>
    /// <param name="text">The input text to sanitize.</param>
    /// <returns>A task that resolves to a <see cref="Result"/> with change counters.</returns>
    public async Task<Result> SanitizeAsync(string text)
    {
        if (string.IsNullOrEmpty(text))
            return new Result { Text = string.Empty, Changed = false };

        _cancellationToken.ThrowIfCancellationRequested();

        var changed = false;
        var bomRemoved = false;
        var lineEndingsNormalized = false;

        if (text.Length > 0 && text[0] == '\uFEFF')
        {
            text = text.AsSpan(1).ToString();
            changed = true;
            bomRemoved = true;
        }

        if (_normalizeLineEndings)
        {
            var normalized = text.Replace("\r\n", "\n").Replace("\r", "\n");
            if (!ReferenceEquals(normalized, text) && !string.Equals(normalized, text))
            {
                text = normalized;
                changed = true;
                lineEndingsNormalized = true;
            }
        }

        var sb = new StringBuilder(text.Length);
        var inString = false;
        var escape = false;
        var inLineComment = false;
        var inBlockComment = false;

        var containers = new Stack<Container>();
        var expectingProperty = false;
        var isPropertyNameString = false;

        var stringContentStart = -1;
        var lastNonWhitespaceInString = -1;

        var lineCommentsRemoved = 0;
        var blockCommentsRemoved = 0;
        var trailingCommasRemoved = 0;
        var controlCharsRemoved = 0;
        var unterminatedStringsClosed = 0;
        var missingCommasInserted = 0;
        var closersInserted = 0;

        var yieldEvery = Math.Max(1024, cooperativeYieldEvery); // keep async loop responsive

        for (var i = 0; i < text.Length; i++)
        {
            if ((i & (yieldEvery - 1)) == 0)
            {
                _cancellationToken.ThrowIfCancellationRequested();
                await Task.Yield();
            }

            var c = text[i];
            var next = i + 1 < text.Length ? text[i + 1] : '\0';

            if (inLineComment)
            {
                if (c == '\n')
                {
                    sb.Append('\n');
                    inLineComment = false;
                }
                else
                {
                    sb.Append(' ');
                }

                changed = true;
                continue;
            }

            if (inBlockComment)
            {
                if (c == '\n')
                {
                    sb.Append('\n');
                }
                else if (c == '*' && next == '/')
                {
                    sb.Append(' ');
                    sb.Append(' ');
                    i++;
                    inBlockComment = false;
                }
                else
                {
                    sb.Append(' ');
                }

                changed = true;
                continue;
            }

            if (inString)
            {
                if (_fixUnterminatedStrings && isPropertyNameString && !escape && c == ':')
                {
                    if (lastNonWhitespaceInString >= stringContentStart && stringContentStart >= 0)
                        sb.Length = lastNonWhitespaceInString + 1;

                    sb.Append('\"');
                    inString = false;
                    isPropertyNameString = false;
                    changed = true;
                    unterminatedStringsClosed++;

                    sb.Append(':');
                    expectingProperty = false;
                    continue;
                }

                if (_fixUnterminatedStrings && isPropertyNameString && !escape && (c == '\r' || c == '\n'))
                {
                    if (lastNonWhitespaceInString >= stringContentStart && stringContentStart >= 0)
                        sb.Length = lastNonWhitespaceInString + 1;

                    sb.Append('\"');
                    inString = false;
                    isPropertyNameString = false;
                    changed = true;
                    unterminatedStringsClosed++;

                    sb.Append(c);
                    continue;
                }

                sb.Append(c);

                if (escape)
                {
                    escape = false;
                }
                else if (c == '\\')
                {
                    escape = true;
                }
                else if (c == '\"')
                {
                    inString = false;
                }
                else
                {
                    if (c != ' ' && c != '\t')
                        lastNonWhitespaceInString = sb.Length - 1;
                }

                continue;
            }

            if (_removeComments && c == '/' && next != '\0')
            {
                if (next == '/')
                {
                    sb.Append(' ');
                    sb.Append(' ');
                    i++;
                    inLineComment = true;
                    changed = true;
                    lineCommentsRemoved++;
                    continue;
                }

                if (next == '*')
                {
                    sb.Append(' ');
                    sb.Append(' ');
                    i++;
                    inBlockComment = true;
                    changed = true;
                    blockCommentsRemoved++;
                    continue;
                }
            }

            if (_removeControlChars && c < 0x20 && c != '\n' && c != '\t')
            {
                sb.Append(' ');
                changed = true;
                controlCharsRemoved++;
                continue;
            }

            if (c == '{')
            {
                containers.Push(Container.Object);
                expectingProperty = true;
            }
            else if (c == '}')
            {
                if (containers.Count > 0) containers.Pop();
                expectingProperty = containers.Count > 0 && containers.Peek() == Container.Object;
            }
            else if (c == '[')
            {
                containers.Push(Container.Array);
                expectingProperty = false;
            }
            else if (c == ']')
            {
                if (containers.Count > 0) containers.Pop();
                expectingProperty = containers.Count > 0 && containers.Peek() == Container.Object;
            }
            else if (c == ',')
            {
                expectingProperty = containers.Count > 0 && containers.Peek() == Container.Object;
            }
            else if (c == ':')
            {
                expectingProperty = false;
            }

            if (_removeTrailingCommas && (c == ']' || c == '}'))
            {
                var k = sb.Length - 1;
                while (k >= 0 && (sb[k] == ' ' || sb[k] == '\n' || sb[k] == '\t')) k--;
                if (k >= 0 && sb[k] == ',')
                {
                    sb[k] = ' ';
                    changed = true;
                    trailingCommasRemoved++;
                }
            }

            if (c == '\"')
            {
                sb.Append(c);
                inString = true;
                escape = false;
                isPropertyNameString =
                    expectingProperty && containers.Count > 0 && containers.Peek() == Container.Object;
                stringContentStart = sb.Length;
                lastNonWhitespaceInString = stringContentStart - 1;
                continue;
            }

            if (c == '\n' && containers.Count > 0)
            {
                var nextNonWs = PeekNextNonWhitespace(text, i + 1);

                if (_recoverMissingClosers && nextNonWs is '}' or ']')
                {
                    if (containers.Peek() == Container.Array && nextNonWs == '}')
                    {
                        sb.Append(']');
                        changed = true;
                        closersInserted++;
                        containers.Pop();
                        expectingProperty = containers.Count > 0 && containers.Peek() == Container.Object;
                    }
                    else if (containers.Peek() == Container.Object && nextNonWs == ']')
                    {
                        sb.Append('}');
                        changed = true;
                        closersInserted++;
                        containers.Pop();
                        expectingProperty = containers.Count > 0 && containers.Peek() == Container.Object;
                    }
                }

                if (_recoverMissingCommas)
                {
                    if (containers.Count > 0)
                    {
                        if (containers.Peek() == Container.Object && !expectingProperty && nextNonWs == '\"')
                        {
                            sb.Append(',');
                            changed = true;
                            missingCommasInserted++;
                            expectingProperty = true;
                        }
                        else if (containers.Peek() == Container.Array && IsValueStarter(nextNonWs))
                        {
                            sb.Append(',');
                            changed = true;
                            missingCommasInserted++;
                        }
                    }
                }
            }

            sb.Append(c);
        }

        if (_fixUnterminatedStrings && inString && isPropertyNameString)
        {
            if (lastNonWhitespaceInString >= stringContentStart && stringContentStart >= 0)
                sb.Length = lastNonWhitespaceInString + 1;

            sb.Append('\"');
            changed = true;
            unterminatedStringsClosed++;
        }

        if (_recoverMissingClosers && containers.Count > 0)
        {
            var top = containers.Pop();
            if (top == Container.Object)
            {
                sb.Append('}');
                changed = true;
                closersInserted++;
            }
            else
            {
                sb.Append(']');
                changed = true;
                closersInserted++;
            }
        }

        return new Result
        {
            Text = sb.ToString(),
            Changed = changed,
            LineCommentsRemoved = lineCommentsRemoved,
            BlockCommentsRemoved = blockCommentsRemoved,
            TrailingCommasRemoved = trailingCommasRemoved,
            ControlCharsRemoved = controlCharsRemoved,
            BomRemoved = bomRemoved,
            LineEndingsNormalized = lineEndingsNormalized,
            UnterminatedStringsClosed = unterminatedStringsClosed,
            MissingCommasInserted = missingCommasInserted,
            ClosersInserted = closersInserted
        };
    }

    /// <summary>
    /// Returns the next non‑whitespace character after <paramref name="startIndex"/>,
    /// or <c>'\0'</c> if none is found.
    /// </summary>
    private static char PeekNextNonWhitespace(string text, int startIndex)
    {
        for (var j = startIndex; j < text.Length; j++)
        {
            var cj = text[j];
            if (cj == ' ' || cj == '\t' || cj == '\r' || cj == '\n')
                continue;
            return cj;
        }
        return '\0';
    }

    /// <summary>
    /// Heuristic check whether a character can start a JSON value (string, object,
    /// array, true/false/null, number).
    /// </summary>
    private static bool IsValueStarter(char c)
    {
        return c == '\"'
               || c == '{'
               || c == '['
               || c == 't'
               || c == 'f'
               || c == 'n'
               || c == '-'
               || char.IsDigit(c);
    }

    /// <summary>
    /// Result of a sanitization pass, including the transformed text and counters
    /// describing what changes were applied.
    /// </summary>
    internal sealed class Result
    {
        /// <summary>
        /// The sanitized text produced by this pass.
        /// </summary>
        public string Text { get; init; } = string.Empty;

        /// <summary>
        /// Whether the sanitizer modified the input text.
        /// </summary>
        public bool Changed { get; init; }

        /// <summary>Number of line comments removed.</summary>
        public int LineCommentsRemoved { get; init; }

        /// <summary>Number of block comments removed.</summary>
        public int BlockCommentsRemoved { get; init; }

        /// <summary>Number of trailing commas removed.</summary>
        public int TrailingCommasRemoved { get; init; }

        /// <summary>Number of control characters removed.</summary>
        public int ControlCharsRemoved { get; init; }

        /// <summary>Whether a UTF‑8 BOM was stripped.</summary>
        public bool BomRemoved { get; init; }

        /// <summary>Whether CR/CRLF line endings were normalized to LF.</summary>
        public bool LineEndingsNormalized { get; init; }

        /// <summary>Number of unterminated strings closed.</summary>
        public int UnterminatedStringsClosed { get; init; }

        /// <summary>Number of missing commas inserted.</summary>
        public int MissingCommasInserted { get; init; }

        /// <summary>Number of missing closers inserted.</summary>
        public int ClosersInserted { get; init; }
    }

    private enum Container
    {
        Object,
        Array
    }
}
