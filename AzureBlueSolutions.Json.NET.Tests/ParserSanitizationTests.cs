namespace AzureBlueSolutions.Json.NET.Tests;

public sealed class ParserSanitizationTests
{
    [Fact]
    public void Comments_And_TrailingCommas_Removed_With_Diagnostics()
    {
        var json = """
                   {
                     // c
                     "a": 1, 
                     /* bc */ 
                     "b": [1,2,],
                     "c": { "d": 3, }
                   }
                   """;

        var options = new ParseOptions
        {
            AllowComments = false,
            IncludeSanitizationDiagnostics = true,
            ReturnSanitizedText = true,
            ProduceTokenSpans = true
        };

        var result = JsonParser.ParseSafe(json, options);

        Assert.True(result.Success);
        Assert.NotNull(result.SanitizedText);

        var codes = result.Errors.Select(e => e.Code).ToArray();
        Assert.Contains(DefaultErrorCodes.Resolve(ErrorKey.CommentsRemoved), codes);
        Assert.Contains(DefaultErrorCodes.Resolve(ErrorKey.TrailingCommasRemoved), codes);
    }

    [Fact]
    public void Bom_And_CRLF_Normalized_Report_Info_Diagnostics_When_Sanitizer_Runs()
    {
        var withBomAndCrLf = "\uFEFF{\r\n  // c\r\n  \"n\": 1,\r\n}\r\n";
        var options = new ParseOptions
        {
            AllowComments = false,
            IncludeSanitizationDiagnostics = true,
            ReturnSanitizedText = true
        };

        var result = JsonParser.ParseSafe(withBomAndCrLf, options);

        // Parse may succeed or require sanitization; we just assert that the info diagnostics exist.
        var codes = result.Errors.Select(e => e.Code).ToArray();
        Assert.Contains(DefaultErrorCodes.Resolve(ErrorKey.BomRemoved), codes);
        Assert.Contains(DefaultErrorCodes.Resolve(ErrorKey.LineEndingsNormalized), codes);

        Assert.NotNull(result.SanitizedText ?? result.SanitizedText); // accept from pre or post path
    }
}