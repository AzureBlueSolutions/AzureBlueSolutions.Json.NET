using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using Range = OmniSharp.Extensions.LanguageServer.Protocol.Models.Range;

namespace AzureBlueSolutions.Json.NET;

/// <summary>
///     LSP conversion for <see cref="TextRange" /> → <see cref="Range" />.
/// </summary>
public static class TextRangeToRangeExtensions
{
    /// <summary>
    ///     Converts a zero-based <see cref="TextRange" /> to an LSP <see cref="Range" />.
    /// </summary>
    /// <param name="range">
    ///     The zero-based text range to convert. Both <see cref="TextRange.Start" /> and
    ///     <see cref="TextRange.End" /> are expected to use zero-based line and column values.
    /// </param>
    /// <returns>
    ///     An LSP <see cref="Range" /> that represents the same start and end coordinates as <paramref name="range" />.
    /// </returns>
    /// <remarks>
    ///     This conversion is a direct mapping:
    ///     <list type="bullet">
    ///         <item>
    ///             <description><see cref="Range.Start" /> is created from <see cref="TextRange.Start" />.</description>
    ///         </item>
    ///         <item>
    ///             <description><see cref="Range.End" />   is created from <see cref="TextRange.End" />.</description>
    ///         </item>
    ///     </list>
    /// </remarks>
    public static Range ToRange(this TextRange range)
    {
        return new Range(
            new Position(range.Start.Line, range.Start.Column),
            new Position(range.End.Line, range.End.Column));
    }
}