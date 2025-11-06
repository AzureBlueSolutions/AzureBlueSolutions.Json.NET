namespace AzureBlueSolutions.Json.NET.Tests;

public sealed class LspIntegrationTests
{
    [Fact]
    public void TokenSpans_Are_Produced_And_Contain_Expected_Kinds()
    {
        const string json = """
                            {
                              "name": "ReCrafter",
                              "nums": [1,2,3],
                              "ok": true,
                              "nz": null
                            }
                            """;

        var options = new ParseOptions
        {
            ProduceTokenSpans = true,
            ProducePathMap = false
        };

        var result = JsonParser.ParseSafe(json, options);
        Assert.True(result.Success);
        Assert.True(result.TokenSpans.Count > 0);

        var kinds = result.TokenSpans.Select(t => t.Kind).ToArray();
        Assert.Contains(JsonLexemeKind.LeftBrace, kinds);
        Assert.Contains(JsonLexemeKind.RightBrace, kinds);
        Assert.Contains(JsonLexemeKind.String, kinds);
        Assert.Contains(JsonLexemeKind.Number, kinds);
        Assert.Contains(JsonLexemeKind.True, kinds);
        Assert.Contains(JsonLexemeKind.Null, kinds);
        Assert.Contains(JsonLexemeKind.LeftBracket, kinds);
        Assert.Contains(JsonLexemeKind.RightBracket, kinds);
        Assert.Contains(JsonLexemeKind.Colon, kinds);
        Assert.Contains(JsonLexemeKind.Comma, kinds);
    }

    [Fact]
    public void PathMap_Contains_Name_And_Value_Ranges_For_Properties_And_Array_Items()
    {
        const string json = """
                            {
                              "title": "X",
                              "items": [ { "id": 1 }, { "id": 2 } ]
                            }
                            """;

        var options = new ParseOptions
        {
            ProduceTokenSpans = true,
            ProducePathMap = true
        };

        var result = JsonParser.ParseSafe(json, options);
        Assert.True(result.Success);

        Assert.True(result.PathRanges.ContainsKey("title"));
        var prTitle = result.PathRanges["title"];
        Assert.NotNull(prTitle.Name);
        Assert.NotNull(prTitle.Value);

        Assert.True(result.PathRanges.ContainsKey("items[0].id"));
        Assert.True(result.PathRanges.ContainsKey("items[1].id"));
    }
}
