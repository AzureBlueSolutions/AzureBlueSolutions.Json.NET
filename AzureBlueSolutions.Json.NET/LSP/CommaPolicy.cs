namespace AzureBlueSolutions.Json.NET;

/// <summary>
///     Low-level comma editing helpers that produce precise <see cref="TextEdit" />s using token spans only (no external
///     offset/line math).
/// </summary>
public static class CommaPolicy
{
    /// <summary>
    ///     On newline: if the previous token terminates a value and the next token looks like a property name,
    ///     insert a comma right after the previous token (unless a comma already exists between).
    /// </summary>
    public static TextEdit? TryInsertCommaBeforeNewline(string text, IReadOnlyList<JsonTokenSpan> tokens,
        int cursorOffset)
    {
        var previous = TokenQuery.PreviousSignificant(tokens, cursorOffset);
        if (previous is null) return null;

        var (prevTok, _) = previous.Value;
        if (!TokenQuery.IsValueTerminator(prevTok.Kind)) return null;

        var next = TokenQuery.NextSignificant(tokens, cursorOffset);
        if (next is null) return null;

        var (_, nextIndex) = next.Value;
        if (!TokenQuery.LooksLikePropertyName(tokens, nextIndex)) return null;

        if (TokenQuery.HasCommaBetween(text, prevTok.Range.End.Offset, cursorOffset)) return null;

        var pos = prevTok.Range.End;
        var insertAt = new TextRange(pos, pos);
        return new TextEdit(insertAt, ",");
    }

    /// <summary>
    ///     On '}' or ']': if there is a trailing comma immediately before the closer, remove it.
    /// </summary>
    public static TextEdit? TryRemoveCommaBeforeCloser(string text, IReadOnlyList<JsonTokenSpan> tokens,
        int cursorOffset)
    {
        if (string.IsNullOrEmpty(text)) return null;

        // Text-first: tolerant caret handling and exact closer/preceding-comma search in raw text.
        var i = Math.Min(Math.Max(0, cursorOffset - 1), Math.Max(0, text.Length - 1));
        while (i >= 0 && (text[i] == ' ' || text[i] == '\t' || text[i] == '\r' || text[i] == '\n'))
            i--;

        if (i >= 0 && (text[i] == '}' || text[i] == ']'))
        {
            var j = i - 1;
            while (j >= 0 && (text[j] == ' ' || text[j] == '\t' || text[j] == '\r' || text[j] == '\n'))
                j--;

            if (j >= 1 && text[j - 1] == '/' && text[j] == '/')
            {
                j -= 2;
                while (j >= 0 && text[j] != '\n' && text[j] != '\r') j--;
                while (j >= 0 && (text[j] == ' ' || text[j] == '\t' || text[j] == '\r' || text[j] == '\n')) j--;
            }
            else if (j >= 1 && text[j - 1] == '*' && text[j] == '/')
            {
                j -= 2;
                while (j >= 1 && !(text[j - 1] == '/' && text[j] == '*')) j--;
                if (j >= 1) j -= 2;
                while (j >= 0 && (text[j] == ' ' || text[j] == '\t' || text[j] == '\r' || text[j] == '\n')) j--;
            }

            if (j >= 0 && text[j] == ',')
            {
                var commaStart = j;
                var commaEnd = j + 1;

                var closerTok =
                    TokenQuery.TokenCovering(tokens, Math.Max(0, i)) ??
                    TokenQuery.TokenEndingAt(tokens, Math.Min(text.Length, i + 1));

                if (closerTok is { } ct)
                {
                    var range = ct.token.Range.WithOffsets(commaStart, commaEnd);
                    return new TextEdit(range, string.Empty);
                }

                var startPos = new TextPosition(0, 0, commaStart);
                var endPos = new TextPosition(0, 0, commaEnd);
                return new TextEdit(new TextRange(startPos, endPos), string.Empty);
            }
        }

        // Fallback: your original token-centric logic.
        return TryRemoveCommaBeforeCloser_TokenFallback(text, tokens, cursorOffset);
    }

    private static TextEdit? TryRemoveCommaBeforeCloser_TokenFallback(string text, IReadOnlyList<JsonTokenSpan> tokens,
        int cursorOffset)
    {
        if (tokens.Count == 0) return null;

        var closer =
            TokenQuery.TokenEndingAt(tokens, cursorOffset)
            ?? TokenQuery.TokenCovering(tokens, Math.Max(0, cursorOffset - 1));

        if (closer is null)
        {
            var bestStart = -1;
            var bestIndex = -1;
            for (var k = 0; k < tokens.Count; k++)
            {
                var t = tokens[k];
                if (t.Kind is JsonLexemeKind.RightBrace or JsonLexemeKind.RightBracket)
                    if (t.Range.Start.Offset <= cursorOffset && t.Range.Start.Offset > bestStart)
                    {
                        bestStart = t.Range.Start.Offset;
                        bestIndex = k;
                    }
            }

            if (bestIndex >= 0) closer = (tokens[bestIndex], bestIndex);
        }

        if (closer is null) return null;

        var (closerTok, closerIndex) = closer.Value;
        if (closerTok.Kind is not (JsonLexemeKind.RightBrace or JsonLexemeKind.RightBracket)) return null;

        var j = closerIndex - 1;
        while (j >= 0 && tokens[j].Kind == JsonLexemeKind.Comment) j--;

        if (j >= 0 && tokens[j].Kind == JsonLexemeKind.Comma)
        {
            var prevTok = tokens[j];
            return new TextEdit(prevTok.Range, string.Empty);
        }

        var searchEnd = closerTok.Range.Start.Offset;
        var iChar = Math.Min(searchEnd - 1, Math.Max(0, text.Length - 1));
        while (iChar >= 0 && (text[iChar] == ' ' || text[iChar] == '\t' || text[iChar] == '\r' || text[iChar] == '\n'))
            iChar--;

        if (iChar >= 0 && text[iChar] == ',')
        {
            var range = closerTok.Range.WithOffsets(iChar, iChar + 1);
            return new TextEdit(range, string.Empty);
        }

        return null;
    }
}