using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using Range = OmniSharp.Extensions.LanguageServer.Protocol.Models.Range;

namespace AzureBlueSolutions.Json.NET;

/// <summary>
///     Provides extension methods for converting internal text ranges to OmniSharp LSP-compatible ranges.
/// </summary>
public static class TextRangeOmniSharpExtensions
{
    /// <summary>
    ///     Converts a <see cref="TextRange" /> (zero-based line and column) to an OmniSharp LSP <see cref="Range" />.
    /// </summary>
    /// <param name="r">
    ///     The <see cref="TextRange" /> to convert. Both start and end positions are zero-based.
    /// </param>
    /// <returns>
    ///     A <see cref="Range" /> instance representing the same span in LSP format.
    /// </returns>
    /// <remarks>
    ///     This method maps <see cref="TextRange.Start" /> and <see cref="TextRange.End" /> to LSP <see cref="Position" />
    ///     objects.
    /// </remarks>
    public static Range ToRange(this TextRange r)
    {
        return new Range(
            new Position(r.Start.Line, r.Start.Column),
            new Position(r.End.Line, r.End.Column));
    }
}