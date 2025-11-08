using OmniSharp.Extensions.LanguageServer.Protocol;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;

namespace AzureBlueSolutions.Json.NET;

/// <summary>
///     LSP conversion for <see cref="TextRange" /> → <see cref="Location" />.
/// </summary>
public static class TextRangeToLocationExtensions
{
    /// <summary>
    ///     Converts a zero-based <see cref="TextRange" /> to an LSP <see cref="Location" /> for the specified document.
    /// </summary>
    /// <param name="r">The zero-based range to convert.</param>
    /// <param name="uri">The document URI that the resulting location should reference.</param>
    /// <returns>
    ///     An LSP <see cref="Location" /> that points to <paramref name="uri" /> and covers <paramref name="r" />.
    /// </returns>
    public static Location ToLocation(this TextRange r, DocumentUri uri)
    {
        return new Location { Uri = uri, Range = r.ToRange() };
    }
}