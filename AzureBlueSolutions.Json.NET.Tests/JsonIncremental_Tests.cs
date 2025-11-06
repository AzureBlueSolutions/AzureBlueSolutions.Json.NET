namespace AzureBlueSolutions.Json.NET.Tests;

public sealed class JsonIncremental_Tests
{
    [Fact]
    public void ApplyChanges_Updates_Text_And_Tokens_And_Shifts_Map()
    {
        const string oldText = """{ "items": [1] }""";
        var parse = JsonParser.ParseSafe(oldText, Profiles.Tolerant());
        var changePoint = oldText.IndexOf(']', StringComparison.Ordinal);
        var change = new TextChange(changePoint, changePoint, ", 2");
        var (text, tokens, map) =
            JsonIncremental.ApplyChanges(oldText, parse.TokenSpans, parse.PathRanges, new[] { change }, 32);
        Assert.Contains("[1, 2]", text);
        Assert.True(tokens.Count > 0);
        Assert.True(map.ContainsKey("items"));
    }

    [Fact]
    public void ApplyChangesWithReparse_Rebuilds_PathMap_For_New_Elements()
    {
        const string oldText = """{ "items": [1] }""";
        var parse = JsonParser.ParseSafe(oldText, Profiles.Tolerant());
        var changePoint = oldText.IndexOf(']', StringComparison.Ordinal);
        var change = new TextChange(changePoint, changePoint, ", 2");
        var (text, tokens, map, reparsed) = JsonIncremental.ApplyChangesWithReparse(oldText, parse.TokenSpans,
            new[] { change }, Profiles.Tolerant(), 32);
        Assert.Contains("[1, 2]", text);
        Assert.True(tokens.Count > 0);
        Assert.True(reparsed.Success);
        Assert.True(map.ContainsKey("items[1]"));
    }
}