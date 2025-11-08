namespace AzureBlueSolutions.Json.NET;

/// <summary>
///     LSP conversion for <see cref="TextEdit" /> →
///     <see cref="OmniSharp.Extensions.LanguageServer.Protocol.Models.TextEdit" />.
/// </summary>
public static class TextEditToLspTextEditExtensions
{
    /// <summary>
    ///     Converts an internal <see cref="TextEdit" /> to an LSP-compatible
    ///     <see cref="OmniSharp.Extensions.LanguageServer.Protocol.Models.TextEdit" />.
    /// </summary>
    /// <param name="e">
    ///     The internal edit to convert. Its <see cref="TextEdit.Range" /> must reference zero-based coordinates.
    /// </param>
    /// <returns>
    ///     A new LSP <see cref="OmniSharp.Extensions.LanguageServer.Protocol.Models.TextEdit" /> with the same replacement
    ///     text
    ///     and the corresponding LSP <see cref="OmniSharp.Extensions.LanguageServer.Protocol.Models.Range" />.
    /// </returns>
    /// <remarks>
    ///     The method delegates range conversion to
    ///     <see cref="TextRangeToRangeExtensions.ToRange(AzureBlueSolutions.Json.NET.TextRange)" />
    ///     and copies <see cref="TextEdit.NewText" /> verbatim.
    /// </remarks>
    public static OmniSharp.Extensions.LanguageServer.Protocol.Models.TextEdit ToTextEdit(this TextEdit e)
    {
        return new OmniSharp.Extensions.LanguageServer.Protocol.Models.TextEdit
        {
            Range = e.Range.ToRange(),
            NewText = e.NewText
        };
    }
}