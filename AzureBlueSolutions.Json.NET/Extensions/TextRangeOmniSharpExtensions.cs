using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using Range = OmniSharp.Extensions.LanguageServer.Protocol.Models.Range;

namespace AzureBlueSolutions.Json.NET;

/// <summary>
/// Converts TextRange (zero-based) to OmniSharp LSP Range.
/// </summary>
public static class TextRangeOmniSharpExtensions
{
    public static Range ToRange(this TextRange r)
        => new Range(
            new Position(r.Start.Line, r.Start.Column),
            new Position(r.End.Line, r.End.Column));
}