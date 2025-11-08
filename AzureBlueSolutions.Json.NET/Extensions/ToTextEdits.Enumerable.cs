namespace AzureBlueSolutions.Json.NET;

/// <summary>
///     Batch conversion for <see cref="IEnumerable{T}" /> of <see cref="TextEdit" /> → LSP <see cref="TextEdit" />[].
/// </summary>
public static class TextEditsToLspTextEditsExtensions
{
    /// <summary>
    ///     Converts a sequence of internal <see cref="TextEdit" /> items into an array of LSP <see cref="TextEdit" />.
    /// </summary>
    /// <param name="edits">The sequence of edits to convert. May be <c>null</c>.</param>
    /// <returns>
    ///     An array of LSP <see cref="TextEdit" /> created from <paramref name="edits" />. When
    ///     <paramref name="edits" /> is <c>null</c>, this method returns <c>null</c>.
    /// </returns>
    /// <remarks>
    ///     If you prefer a non-null return, coalesce at the call site:
    ///     <code>var array = edits.ToTextEdits() ?? Array.Empty&lt;TextEdit&gt;();</code>
    /// </remarks>
    public static OmniSharp.Extensions.LanguageServer.Protocol.Models.TextEdit[]?
        ToTextEdits(this IEnumerable<TextEdit> edits)
    {
        return edits?.Select(e => e.ToTextEdit()).ToArray();
    }
}