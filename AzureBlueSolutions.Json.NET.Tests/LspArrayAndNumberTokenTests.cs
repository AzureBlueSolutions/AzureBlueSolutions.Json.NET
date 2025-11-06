namespace AzureBlueSolutions.Json.NET.Tests;

public sealed class LspArrayAndNumberTokenTests
{
    [Fact]
    public void Arrays_Of_Objects_And_Arrays_Map_Value_Ranges()
    {
        string json = """
        {
          "items": [
            { "id": 1 },
            [ true, null, 2.5, 1e3 ]
          ]
        }
        """;

        var options = new ParseOptions
        {
            ProduceTokenSpans = true,
            ProducePathMap = true
        };

        var result = JsonParser.ParseSafe(json, options);
        Assert.True(result.Success);

        Assert.True(result.PathRanges.ContainsKey("items[0].id"));
        var id = result.PathRanges["items[0].id"];
        Assert.NotNull(id.Value);

        Assert.True(result.PathRanges.ContainsKey("items[1][0]"));
        Assert.NotNull(result.PathRanges["items[1][0]"].Value);

        Assert.True(result.PathRanges.ContainsKey("items[1][1]"));
        Assert.NotNull(result.PathRanges["items[1][1]"].Value);

        Assert.True(result.PathRanges.ContainsKey("items[1][2]"));
        Assert.NotNull(result.PathRanges["items[1][2]"].Value);

        Assert.True(result.PathRanges.ContainsKey("items[1][3]"));
        Assert.NotNull(result.PathRanges["items[1][3]"].Value);
    }

    [Fact]
    public void Number_Formats_Tokenized_And_Mapped()
    {
        string json = """
        {
          "n1": 0,
          "n2": -1,
          "n3": 0.25,
          "n4": 1e10
        }
        """;

        var options = new ParseOptions
        {
            ProduceTokenSpans = true,
            ProducePathMap = true
        };

        var result = JsonParser.ParseSafe(json, options);
        Assert.True(result.Success);

        Assert.NotNull(result.PathRanges["n1"].Value);
        Assert.NotNull(result.PathRanges["n2"].Value);
        Assert.NotNull(result.PathRanges["n3"].Value);
        Assert.NotNull(result.PathRanges["n4"].Value);

        var kinds = result.TokenSpans.Select(t => t.Kind).ToArray();
        Assert.Contains(JsonLexemeKind.Number, kinds);
    }

    [Fact]
    public void Booleans_And_Null_Tokenized_And_Mapped()
    {
        string json = """
        { "t": true, "f": false, "z": null }
        """;

        var options = new ParseOptions
        {
            ProduceTokenSpans = true,
            ProducePathMap = true
        };

        var result = JsonParser.ParseSafe(json, options);
        Assert.True(result.Success);

        Assert.NotNull(result.PathRanges["t"].Value);
        Assert.NotNull(result.PathRanges["f"].Value);
        Assert.NotNull(result.PathRanges["z"].Value);

        var kinds = result.TokenSpans.Select(t => t.Kind).ToArray();
        Assert.Contains(JsonLexemeKind.True, kinds);
        Assert.Contains(JsonLexemeKind.False, kinds);
        Assert.Contains(JsonLexemeKind.Null, kinds);
    }
}
