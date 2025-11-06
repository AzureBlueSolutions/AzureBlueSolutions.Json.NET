using System.Text;

namespace AzureBlueSolutions.Json.NET;

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
    internal sealed class Result
    {
        public string Text { get; init; } = string.Empty;
        public bool Changed { get; init; }
        public int LineCommentsRemoved { get; init; }
        public int BlockCommentsRemoved { get; init; }
        public int TrailingCommasRemoved { get; init; }
        public int ControlCharsRemoved { get; init; }
        public bool BomRemoved { get; init; }
        public bool LineEndingsNormalized { get; init; }
        public int UnterminatedStringsClosed { get; init; }
        public int MissingCommasInserted { get; init; }
        public int ClosersInserted { get; init; }
    }

    private readonly bool _removeComments = removeComments;
    private readonly bool _removeTrailingCommas = removeTrailingCommas;
    private readonly bool _removeControlChars = removeControlChars;
    private readonly bool _normalizeLineEndings = normalizeLineEndings;
    private readonly bool _fixUnterminatedStrings = fixUnterminatedStrings;
    private readonly bool _recoverMissingCommas = recoverMissingCommas;
    private readonly bool _recoverMissingClosers = recoverMissingClosers;
    private readonly CancellationToken _cancellationToken = cancellationToken;

    private enum Container
    { Object, Array }

    public Result Sanitize(string text)
    {
        if (string.IsNullOrEmpty(text))
            return new Result { Text = string.Empty, Changed = false };

        _cancellationToken.ThrowIfCancellationRequested();

        bool changed = false;
        bool bomRemoved = false;
        bool lineEndingsNormalized = false;

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
        bool inString = false;
        bool escape = false;
        bool inLineComment = false;
        bool inBlockComment = false;
        var containers = new Stack<Container>();
        bool expectingProperty = false;
        bool isPropertyNameString = false;
        int stringContentStart = -1;
        int lastNonWhitespaceInString = -1;

        int lineCommentsRemoved = 0;
        int blockCommentsRemoved = 0;
        int trailingCommasRemoved = 0;
        int controlCharsRemoved = 0;
        int unterminatedStringsClosed = 0;
        int missingCommasInserted = 0;
        int closersInserted = 0;

        for (int i = 0; i < text.Length; i++)
        {
            _cancellationToken.ThrowIfCancellationRequested();

            char c = text[i];
            char next = i + 1 < text.Length ? text[i + 1] : '\0';

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
                    sb.Append('"');
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
                    sb.Append('"');
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
                else if (c == '"')
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
                int k = sb.Length - 1;
                while (k >= 0 && (sb[k] == ' ' || sb[k] == '\n' || sb[k] == '\t')) k--;
                if (k >= 0 && sb[k] == ',')
                {
                    sb[k] = ' ';
                    changed = true;
                    trailingCommasRemoved++;
                }
            }

            if (c == '"')
            {
                sb.Append(c);
                inString = true;
                escape = false;
                isPropertyNameString = expectingProperty && containers.Count > 0 && containers.Peek() == Container.Object;
                stringContentStart = sb.Length;
                lastNonWhitespaceInString = stringContentStart - 1;
                continue;
            }

            if ((c == '\n') && (containers.Count > 0))
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
                        if (containers.Peek() == Container.Object && expectingProperty == false && nextNonWs == '"')
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
            sb.Append('"');
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

    public async Task<Result> SanitizeAsync(string text)
    {
        if (string.IsNullOrEmpty(text))
            return new Result { Text = string.Empty, Changed = false };

        _cancellationToken.ThrowIfCancellationRequested();

        bool changed = false;
        bool bomRemoved = false;
        bool lineEndingsNormalized = false;

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
        bool inString = false;
        bool escape = false;
        bool inLineComment = false;
        bool inBlockComment = false;
        var containers = new Stack<Container>();
        bool expectingProperty = false;
        bool isPropertyNameString = false;
        int stringContentStart = -1;
        int lastNonWhitespaceInString = -1;

        int lineCommentsRemoved = 0;
        int blockCommentsRemoved = 0;
        int trailingCommasRemoved = 0;
        int controlCharsRemoved = 0;
        int unterminatedStringsClosed = 0;
        int missingCommasInserted = 0;
        int closersInserted = 0;

        int yieldEvery = Math.Max(1024, cooperativeYieldEvery);

        for (int i = 0; i < text.Length; i++)
        {
            if ((i & (yieldEvery - 1)) == 0)
            {
                _cancellationToken.ThrowIfCancellationRequested();
                await Task.Yield();
            }

            char c = text[i];
            char next = i + 1 < text.Length ? text[i + 1] : '\0';

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
                    sb.Append('"');
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
                    sb.Append('"');
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
                else if (c == '"')
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
                int k = sb.Length - 1;
                while (k >= 0 && (sb[k] == ' ' || sb[k] == '\n' || sb[k] == '\t')) k--;
                if (k >= 0 && sb[k] == ',')
                {
                    sb[k] = ' ';
                    changed = true;
                    trailingCommasRemoved++;
                }
            }

            if (c == '"')
            {
                sb.Append(c);
                inString = true;
                escape = false;
                isPropertyNameString = expectingProperty && containers.Count > 0 && containers.Peek() == Container.Object;
                stringContentStart = sb.Length;
                lastNonWhitespaceInString = stringContentStart - 1;
                continue;
            }

            if ((c == '\n') && (containers.Count > 0))
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
                        if (containers.Peek() == Container.Object && expectingProperty == false && nextNonWs == '"')
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
            sb.Append('"');
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

    private static char PeekNextNonWhitespace(string text, int startIndex)
    {
        for (int j = startIndex; j < text.Length; j++)
        {
            char cj = text[j];
            if (cj == ' ' || cj == '\t' || cj == '\r' || cj == '\n')
                continue;
            return cj;
        }
        return '\0';
    }

    private static bool IsValueStarter(char c)
        => c == '"' || c == '{' || c == '[' || c == 't' || c == 'f' || c == 'n' || c == '-' || char.IsDigit(c);
}