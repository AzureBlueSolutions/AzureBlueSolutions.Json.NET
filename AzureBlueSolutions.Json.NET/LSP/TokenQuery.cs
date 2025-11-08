namespace AzureBlueSolutions.Json.NET;

/// <summary>
///     Token navigation and shape helpers over <see cref="JsonTokenSpan" /> lists.
/// </summary>
public static class TokenQuery
{
    /// <summary>
    ///     Returns the last non-comment token whose end offset is &lt;= <paramref name="offset" />.
    /// </summary>
    public static (JsonTokenSpan token, int index)? PreviousSignificant(IReadOnlyList<JsonTokenSpan> tokens, int offset)
    {
        for (var i = tokens.Count - 1; i >= 0; i--)
        {
            var t = tokens[i];
            if (t.Range.End.Offset > offset) continue;
            if (t.Kind == JsonLexemeKind.Comment) continue;
            return (t, i);
        }

        return null;
    }

    /// <summary>
    ///     Returns the first non-comment token whose start offset is &gt;= <paramref name="offset" />.
    /// </summary>
    public static (JsonTokenSpan token, int index)? NextSignificant(IReadOnlyList<JsonTokenSpan> tokens, int offset)
    {
        for (var i = 0; i < tokens.Count; i++)
        {
            var t = tokens[i];
            if (t.Range.Start.Offset < offset) continue;
            if (t.Kind == JsonLexemeKind.Comment) continue;
            return (t, i);
        }

        return null;
    }

    /// <summary>
    ///     Returns the token that ends exactly at <paramref name="offset" />, if any.
    /// </summary>
    public static (JsonTokenSpan token, int index)? TokenEndingAt(IReadOnlyList<JsonTokenSpan> tokens, int offset)
    {
        for (var i = 0; i < tokens.Count; i++)
        {
            var t = tokens[i];
            if (t.Range.End.Offset == offset) return (t, i);
        }

        return null;
    }

    /// <summary>
    ///     Returns the token that covers <paramref name="offset" /> (start ≤ offset &lt; end), if any.
    /// </summary>
    public static (JsonTokenSpan token, int index)? TokenCovering(IReadOnlyList<JsonTokenSpan> tokens, int offset)
    {
        for (var i = 0; i < tokens.Count; i++)
        {
            var t = tokens[i];
            if (t.Range.Start.Offset <= offset && offset < t.Range.End.Offset) return (t, i);
        }

        return null;
    }

    /// <summary>
    ///     Returns true when the token at <paramref name="tokenIndex" /> looks like a JSON property name
    ///     (a String token followed by optional Comment tokens and a Colon).
    /// </summary>
    public static bool LooksLikePropertyName(IReadOnlyList<JsonTokenSpan> tokens, int tokenIndex)
    {
        if (tokenIndex < 0 || tokenIndex >= tokens.Count) return false;
        if (tokens[tokenIndex].Kind != JsonLexemeKind.String) return false;
        var i = tokenIndex + 1;
        while (i < tokens.Count && tokens[i].Kind == JsonLexemeKind.Comment) i++;
        return i < tokens.Count && tokens[i].Kind == JsonLexemeKind.Colon;
    }

    /// <summary>
    ///     Returns true if <paramref name="kind" /> can terminate a value (string, number, true, false, null, '}' or ']').
    /// </summary>
    public static bool IsValueTerminator(JsonLexemeKind kind)
    {
        return kind is JsonLexemeKind.String
            or JsonLexemeKind.Number
            or JsonLexemeKind.True
            or JsonLexemeKind.False
            or JsonLexemeKind.Null
            or JsonLexemeKind.RightBrace
            or JsonLexemeKind.RightBracket;
    }

    /// <summary>
    ///     Returns true if a comma character appears between <paramref name="startOffset" /> and <paramref name="endOffset" />
    ///     in <paramref name="text" /> before a newline.
    /// </summary>
    public static bool HasCommaBetween(string text, int startOffset, int endOffset)
    {
        if (string.IsNullOrEmpty(text)) return false;
        if (endOffset <= startOffset) return false;

        var stop = Math.Min(endOffset, text.Length);
        for (var i = Math.Clamp(startOffset, 0, text.Length); i < stop; i++)
        {
            var ch = text[i];
            if (ch == ',') return true;
            if (ch == '\n' || ch == '\r') break;
        }

        return false;
    }
}