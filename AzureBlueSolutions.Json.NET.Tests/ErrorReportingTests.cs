namespace AzureBlueSolutions.Json.NET.Tests;

public sealed class ErrorReportingTests
{
    [Fact]
    public void DuplicateKey_With_Error_Strategy_Reports_E003_And_Has_Path_And_Range()
    {
        const string dup = """{ "a": 1, "a": 2 }""";
        var options = new ParseOptions
        {
            DuplicatePropertyHandling = DuplicateKeyStrategy.Error,
            ProduceTokenSpans = true,
            ProducePathMap = true
        };

        var result = JsonParser.ParseSafe(dup, options);

        Assert.False(result.Success);

        var dupErr = result.Errors.FirstOrDefault(e => e.Code == DefaultErrorCodes.Resolve(ErrorKey.DuplicateKey));
        Assert.NotNull(dupErr);
        Assert.NotNull(dupErr.Path);
        Assert.NotNull(dupErr.Range);
    }

    [Fact]
    public void InvalidToken_Reports_E002_With_Snippet_And_Range()
    {
        const string bad = """{ "a": 1,, "b": 2 }""";
        var options = new ParseOptions
        {
            ProduceTokenSpans = true
        };

        var result = JsonParser.ParseSafe(bad, options);

        Assert.False(result.Success);

        var e = result.Errors.FirstOrDefault(x => x.Code == DefaultErrorCodes.Resolve(ErrorKey.InvalidToken));
        Assert.NotNull(e);
        Assert.False(string.IsNullOrWhiteSpace(e.Snippet));
        Assert.NotNull(e.Range);
    }
}