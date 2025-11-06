namespace AzureBlueSolutions.Json.NET.Tests;

public sealed class Extensions_LineInfo_Tests
{
    [Fact]
    public void GetLineInfo_Returns_Line_And_Column_For_Tokens()
    {
        const string json = """
                            {
                              "a": {
                                "b": 1
                              }
                            }
                            """;
        var options = new ParseOptions { ProduceTokenSpans = true, ProducePathMap = true, CollectLineInfo = true };
        var result = JsonParser.ParseSafe(json, options);
        Assert.True(result.Success);
        var token = result.Root!.SelectToken("a.b");
        Assert.NotNull(token);
        var info = token!.GetLineInfo();
        Assert.NotNull(info);
        Assert.True(info!.Value.line > 0);
        Assert.True(info!.Value.position > 0);
    }
}