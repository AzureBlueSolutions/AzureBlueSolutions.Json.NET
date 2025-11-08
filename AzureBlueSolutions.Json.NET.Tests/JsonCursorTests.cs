using Newtonsoft.Json.Linq;

namespace AzureBlueSolutions.Json.NET.Tests;

public sealed class JsonCursorTests
{
    [Fact]
    public void Set_Replaces_Value_Using_ValueRange()
    {
        const string json = """{ "title": "X" }""";
        var result = JsonParser.ParseSafe(json, Profiles.Tolerant());
        var cursor = JsonCursor.FromPath(result, "title");
        Assert.NotNull(cursor);
        var edit = cursor!.Set(json, JValue.CreateString("Y"));
        Assert.NotNull(edit);
        var newText = Replace(json, edit.Range, edit.NewText);
        Assert.Contains("\"Y\"", newText);
    }

    [Fact]
    public void InsertProperty_Adds_Property_To_Object_With_Indent()
    {
        const string json = """
                            {
                              "obj": { }
                            }
                            """;
        var result = JsonParser.ParseSafe(json, Profiles.Tolerant());
        var cursor = JsonCursor.FromPath(result, "obj");
        Assert.NotNull(cursor);
        var edit = cursor!.InsertProperty(json, "added", JToken.FromObject(123), true, "  ");
        Assert.NotNull(edit);
        var newText = Replace(json, edit!.Range, edit!.NewText);
        Assert.Contains("\"added\": 123", newText);
    }

    [Fact]
    public void InsertArrayItem_Appends_And_Inserts_At_Index()
    {
        const string json = """{ "arr": [1] }""";
        var result = JsonParser.ParseSafe(json, Profiles.Tolerant());
        var arrCursor = JsonCursor.FromPath(result, "arr");
        Assert.NotNull(arrCursor);

        var append = arrCursor!.InsertArrayItem(json, JToken.FromObject(2));
        Assert.NotNull(append);
        var afterAppend = Replace(json, append!.Range, append!.NewText);
        Assert.Contains("[1, 2]", afterAppend);

        var reparsedAfterAppend = JsonParser.ParseSafe(afterAppend, Profiles.Tolerant());
        var idxCursor = JsonCursor.FromPath(reparsedAfterAppend, "arr");
        var insertAt0 = idxCursor!.InsertArrayItem(afterAppend, JToken.FromObject(0), 0);
        Assert.NotNull(insertAt0);
        var afterInsert = Replace(afterAppend, insertAt0!.Range, insertAt0!.NewText);

        Assert.Contains("[1, 0, 2]", afterInsert);

        var reparsed = JsonParser.ParseSafe(afterInsert, Profiles.Tolerant());
        Assert.True(reparsed.Success);
        var a0 = (int?)reparsed.Root!.SelectToken("arr[0]");
        var a1 = (int?)reparsed.Root!.SelectToken("arr[1]");
        var a2 = (int?)reparsed.Root!.SelectToken("arr[2]");
        Assert.Equal(1, a0);
        Assert.Equal(0, a1);
        Assert.Equal(2, a2);
    }

    private static string Replace(string text, TextRange range, string newText)
    {
        var s = range.Start.Offset;
        var e = range.End.Offset;
        if (s < 0 || e < s || e > text.Length) return text;
        return text[..s] + newText + text[e..];
    }
}