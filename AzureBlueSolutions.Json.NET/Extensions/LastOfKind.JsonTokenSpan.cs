namespace AzureBlueSolutions.Json.NET;

/// <summary>
///     Reverse-selection helpers for <see cref="JsonTokenSpan" /> sequences.
/// </summary>
public static class JsonTokenSpanLastOfKindExtensions
{
    /// <summary>
    ///     Returns the last token of the specified <see cref="JsonLexemeKind" /> from a sequence, or <c>null</c> when none
    ///     exists.
    /// </summary>
    /// <param name="tokens">The sequence of tokens to search.</param>
    /// <param name="kind">The token kind to match.</param>
    /// <returns>
    ///     The last token of the requested kind, or <c>null</c> if the sequence contains no such token.
    /// </returns>
    public static JsonTokenSpan? LastOfKind(this IEnumerable<JsonTokenSpan> tokens, JsonLexemeKind kind)
    {
        return tokens.LastOrDefault(t => t.Kind == kind);
    }
}