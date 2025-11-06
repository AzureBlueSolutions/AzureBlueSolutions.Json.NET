namespace AzureBlueSolutions.Json.NET.Tests;

public sealed class TokenizerInvariantsTests
{
    [Fact]
    public void TokenSpans_Are_Monotonic_By_Offset()
    {
        var json = """
                   {
                     "a": 1, // c
                     "b": [2,3],
                     "c": { "d": "x" }
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

        var offsets = result.TokenSpans.Select(t => t.Range.Start.Offset).ToArray();
        for (var i = 1; i < offsets.Length; i++) Assert.True(offsets[i] >= offsets[i - 1]);
    }

    [Fact]
    public void TryFindToken_Fallback_Finds_Token_Inside_Range()
    {
        var json = """
                   { "x": "value" }
                   """;

        var options = new ParseOptions
        {
            ProduceTokenSpans = true,
            ProducePathMap = true
        };

        var result = JsonParser.ParseSafe(json, options);
        Assert.True(result.Success);

        Assert.True(result.PathRanges.ContainsKey("x"));
        var px = result.PathRanges["x"];
        Assert.NotNull(px.Name);
        Assert.NotNull(px.Value);
    }

    [Fact]
    public void Failed_Parse_Produces_No_PathRanges()
    {
        const string json = "{ \"a\": 1,, }";

        var options = new ParseOptions
        {
            ProduceTokenSpans = true,
            ProducePathMap = true,
            EnableSanitizationFallback = false
        };

        var result = JsonParser.ParseSafe(json, options);
        Assert.False(result.Success);
        Assert.Empty(result.PathRanges);
    }
}