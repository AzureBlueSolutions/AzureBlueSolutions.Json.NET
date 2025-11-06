namespace AzureBlueSolutions.Json.NET.Tests;

public sealed class ParserSecurityTests
{
    [Fact]
    public void SizeLimitExceeded_Returns_E008_And_NoRoot()
    {
        var input = new string('x', 10_000);
        var options = new ParseOptions
        {
            MaxDocumentLength = 1024,
            ProduceTokenSpans = true
        };

        var result = JsonParser.ParseSafe(input, options);

        Assert.False(result.Success);
        var error = Assert.Single(result.Errors);
        Assert.Equal(DefaultErrorCodes.Resolve(ErrorKey.SizeLimitExceeded), error.Code);
        Assert.Null(result.Root);
        Assert.True(result.TokenSpans.Count >= 0);
    }

    [Fact]
    public void DepthLimitExceeded_Returns_E009()
    {
        var deep = new string('[', 100) + "0" + new string(']', 100);
        var options = new ParseOptions
        {
            MaxDepth = 8,
            MaxDocumentLength = 1_000_000
        };

        var result = JsonParser.ParseSafe(deep, options);

        Assert.False(result.Success);
        Assert.Contains(result.Errors, e => e.Code == DefaultErrorCodes.Resolve(ErrorKey.DepthLimitExceeded));
    }
}