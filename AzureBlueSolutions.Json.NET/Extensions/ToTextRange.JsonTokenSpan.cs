namespace AzureBlueSolutions.Json.NET;

/// <summary>
///     Source range retrieval for <see cref="JsonTokenSpan" />.
/// </summary>
public static class JsonTokenSpanToTextRangeExtensions
{
    /// <summary>
    ///     Returns the source <see cref="TextRange" /> covered by a token span.
    /// </summary>
    /// <param name="t">The token span whose range should be returned.</param>
    /// <returns>
    ///     The <see cref="TextRange" /> for <paramref name="t" />.
    /// </returns>
    /// <remarks>
    ///     This method exists primarily for call-site readability when working with fluent code.
    /// </remarks>
    public static TextRange ToTextRange(this JsonTokenSpan t)
    {
        return t.Range;
    }
}