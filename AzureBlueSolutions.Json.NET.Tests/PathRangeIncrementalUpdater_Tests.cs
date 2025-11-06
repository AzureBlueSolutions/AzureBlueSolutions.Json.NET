namespace AzureBlueSolutions.Json.NET.Tests;

public sealed class PathRangeIncrementalUpdater_Tests
{
    [Fact]
    public void Update_Shifts_Ranges_When_Text_Is_Prepended()
    {
        const string oldText = """
                               {
                                 "name": "a",
                                 "age": 1
                               }
                               """;
        var parse = JsonParser.ParseSafe(oldText, Profiles.Tolerant());
        var oldMap = parse.PathRanges;
        var change = new TextChange(0, 0, "\n");
        var newMap = PathRangeIncrementalUpdater.Update(oldMap, new[] { change }, oldText, "\n" + oldText);
        var name = newMap["name"];
        Assert.NotNull(name.Name);
        Assert.True(name.Name!.Start.Line > oldMap["name"].Name!.Start.Line);
        Assert.NotNull(name.Value);
    }

    [Fact]
    public void Update_Deletes_Entries_Intersecting_Change()
    {
        const string oldText = """{ "age": 1, "name": "a" }""";
        var parse = JsonParser.ParseSafe(oldText, Profiles.Tolerant());
        var oldMap = parse.PathRanges;
        var ageRange = oldMap["age"].Value!;
        var change = new TextChange(ageRange.Start.Offset, ageRange.End.Offset, "22");
        var newText = oldText[..ageRange.Start.Offset] + "22" + oldText[ageRange.End.Offset..];
        var newMap = PathRangeIncrementalUpdater.Update(oldMap, new[] { change }, oldText, newText);
        Assert.False(newMap.ContainsKey("age"));
        Assert.True(newMap.ContainsKey("name"));
    }
}