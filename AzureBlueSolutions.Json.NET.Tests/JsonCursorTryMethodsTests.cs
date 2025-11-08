using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace AzureBlueSolutions.Json.NET.Tests;

/// <summary>
///     Tests for the 'Try*' helper methods exposed by JsonCursor.
///     Ensures we parse with edit-friendly options (PathMap + TokenSpans + LineInfo)
///     so JsonCursor can compute precise text edits for deletions.
/// </summary>
public sealed class JsonCursorTryMethodsTests
{
    private static ParseOptions EditFriendly()
    {
        return new ParseOptions
        {
            ProduceTokenSpans = true,
            ProducePathMap = true,
            CollectLineInfo = true
        };
    }

    /// <summary>
    ///     TryFromPath returns true for existing paths and false for missing paths.
    /// </summary>
    [Fact]
    public void TryFromPath_Succeeds_And_Fails()
    {
        const string json = """{ "title": "X", "arr": [1,2], "obj": { "a": 1 } }""";
        var result = JsonParser.ParseSafe(json, EditFriendly());
        Assert.True(JsonCursor.TryFromPath(result, "title", out var title));
        Assert.NotNull(title);
        Assert.False(JsonCursor.TryFromPath(result, "missing", out _));
    }

    /// <summary>
    ///     TrySet succeeds when ValueRange is known; fails when path map was not produced.
    /// </summary>
    [Fact]
    public void TrySet_Succeeds_When_ValueRange_Present_And_Fails_Without_PathMap()
    {
        const string json = """{ "title": "X" }""";

        var withMap = JsonParser.ParseSafe(json, EditFriendly());
        Assert.True(JsonCursor.TryFromPath(withMap, "title", out var cursorWithMap));
        Assert.True(cursorWithMap!.TrySet(json, JValue.CreateString("Y"), out var edit1));
        var after1 = ApplyEdit(json, edit1!);
        Assert.Contains(@"""Y""", after1);

        var withoutMap = JsonParser.ParseSafe(json,
            new ParseOptions { ProduceTokenSpans = true, ProducePathMap = false, CollectLineInfo = true });
        Assert.True(JsonCursor.TryFromPath(withoutMap, "title", out var cursorNoMap));
        Assert.False(cursorNoMap!.TrySet(json, JValue.CreateString("Z"), out _));
    }

    /// <summary>
    ///     TryInsertProperty inserts into an object cursor and also when the cursor is a property whose value is an object.
    /// </summary>
    [Fact]
    public void TryInsertProperty_Succeeds_On_Object_And_PropertyValuedObject()
    {
        const string json = """
                            {
                              "o": { }
                            }
                            """;
        var result = JsonParser.ParseSafe(json, EditFriendly());
        Assert.True(JsonCursor.TryFromPath(result, "o", out var objCursor));

        Assert.True(objCursor!.TryInsertProperty(json, "added1", JToken.FromObject(123), out var e1));
        var t1 = ApplyEdit(json, e1!);
        Assert.Contains(@"""added1"": 123", t1);

        var reparsed = JsonParser.ParseSafe(t1, EditFriendly());
        Assert.True(JsonCursor.TryFromPath(reparsed, "o", out var propCursorOnObj));
        Assert.True(propCursorOnObj!.TryInsertProperty(t1, "added2", JToken.FromObject(true), true, "  ", out var e2));
        var t2 = ApplyEdit(t1, e2!);
        Assert.Contains(@"""added2"": true", t2);
    }

    /// <summary>
    ///     TryInsertProperty fails when the cursor is not an object or property-with-object.
    /// </summary>
    [Fact]
    public void TryInsertProperty_Fails_On_NonObject()
    {
        const string json = """{ "title": "X" }""";
        var result = JsonParser.ParseSafe(json, EditFriendly());
        Assert.True(JsonCursor.TryFromPath(result, "title", out var titleCursor));
        Assert.False(titleCursor!.TryInsertProperty(json, "n", JToken.FromObject(1), out _));
    }

    /// <summary>
    ///     TryInsertArrayItem appends and inserts at an index.
    /// </summary>
    [Fact]
    public void TryInsertArrayItem_Succeeds_Append_And_Index()
    {
        const string json = """{ "arr": [1] }""";
        var result = JsonParser.ParseSafe(json, EditFriendly());
        Assert.True(JsonCursor.TryFromPath(result, "arr", out var arrCursor));

        Assert.True(arrCursor!.TryInsertArrayItem(json, JToken.FromObject(2), out var append));
        var afterAppend = ApplyEdit(json, append!);
        Assert.Contains("[1, 2]", afterAppend);

        var reparsed = JsonParser.ParseSafe(afterAppend, EditFriendly());
        Assert.True(JsonCursor.TryFromPath(reparsed, "arr", out var arrAgain));

        Assert.True(arrAgain!.TryInsertArrayItem(afterAppend, JToken.FromObject(0), 0, true, out var at0));
        var afterInsert = ApplyEdit(afterAppend, at0!);
        Assert.Contains("[1, 0, 2]", afterInsert);

        var check = JsonParser.ParseSafe(afterInsert, Profiles.Tolerant());
        Assert.Equal(1, (int?)check.Root!.SelectToken("arr[0]"));
        Assert.Equal(0, (int?)check.Root!.SelectToken("arr[1]"));
        Assert.Equal(2, (int?)check.Root!.SelectToken("arr[2]"));
    }

    /// <summary>
    ///     TryInsertArrayItem fails when the cursor is not an array or property-with-array.
    /// </summary>
    [Fact]
    public void TryInsertArrayItem_Fails_On_NonArray()
    {
        const string json = """{ "title": "X" }""";
        var result = JsonParser.ParseSafe(json, EditFriendly());
        Assert.True(JsonCursor.TryFromPath(result, "title", out var titleCursor));
        Assert.False(titleCursor!.TryInsertArrayItem(json, JToken.FromObject(2), out _));
        Assert.False(titleCursor!.TryInsertArrayItem(json, JToken.FromObject(2), 0, true, out var _2));
    }

    /// <summary>
    ///     TryRemoveProperty on a property cursor deletes the property and normalizes commas/whitespace.
    /// </summary>
    [Fact]
    public void TryRemoveProperty_On_PropertyCursor_Removes_Properly()
    {
        const string json = """{ "a": 1, "b": 2, "c": 3 }""";
        var parsed = JsonParser.ParseSafe(json, EditFriendly());
        Assert.True(JsonCursor.TryFromPath(parsed, "b", out var propCursor));

        Assert.True(propCursor!.TryRemoveProperty(json, out var edit));
        var after = ApplyEdit(json, edit!);
        var reparsed = JsonParser.ParseSafe(after, Profiles.Tolerant());
        Assert.True(reparsed.Success);
        Assert.Equal(1, (int?)reparsed.Root!.SelectToken("a"));
        Assert.Null(reparsed.Root!.SelectToken("b"));
        Assert.Equal(3, (int?)reparsed.Root!.SelectToken("c"));
    }

    /// <summary>
    ///     TryRemoveProperty by name from an object cursor removes the named property.
    /// </summary>
    [Fact]
    public void TryRemoveProperty_ByName_From_Object()
    {
        const string json = """{ "obj": { "x": 1, "y": 2, "z": 3 } }""";
        var parsed = JsonParser.ParseSafe(json, EditFriendly());
        Assert.True(JsonCursor.TryFromPath(parsed, "obj", out var objCursor));

        Assert.True(objCursor!.TryRemoveProperty(json, "y", out var edit));
        var after = ApplyEdit(json, edit!);
        var reparsed = JsonParser.ParseSafe(after, Profiles.Tolerant());
        Assert.Null(reparsed.Root!.SelectToken("obj.y"));
        Assert.Equal(1, (int?)reparsed.Root!.SelectToken("obj.x"));
        Assert.Equal(3, (int?)reparsed.Root!.SelectToken("obj.z"));
    }

    /// <summary>
    ///     TryRemoveArrayItem removes first, middle, and last elements with proper comma handling.
    /// </summary>
    [Fact]
    public void TryRemoveArrayItem_Removes_Elements_Cleanly()
    {
        const string json = """{ "arr": [0, 1, 2, 3] }""";
        var parsed = JsonParser.ParseSafe(json, EditFriendly());
        Assert.True(JsonCursor.TryFromPath(parsed, "arr", out var arrCursor));

        Assert.True(arrCursor!.TryRemoveArrayItem(json, 0, out var e0));
        var t0 = ApplyEdit(json, e0!); // => [1,2,3]

        var p1 = JsonParser.ParseSafe(t0, EditFriendly());
        Assert.True(JsonCursor.TryFromPath(p1, "arr", out var arr1));
        Assert.True(arr1!.TryRemoveArrayItem(t0, 1, out var eMid));
        var t1 = ApplyEdit(t0, eMid!); // remove '2' => [1,3]

        var p2 = JsonParser.ParseSafe(t1, EditFriendly());
        Assert.True(JsonCursor.TryFromPath(p2, "arr", out var arr2));
        Assert.True(arr2!.TryRemoveArrayItem(t1, 1, out var eLast));
        var t2 = ApplyEdit(t1, eLast!); // remove last => [1]

        var final = JsonParser.ParseSafe(t2, Profiles.Tolerant());
        Assert.Equal(1, (int?)final.Root!.SelectToken("arr[0]"));
        Assert.Null(final.Root!.SelectToken("arr[1]"));
    }

    /// <summary>
    ///     TryRemoveSelf removes a property node and also works for an array item node.
    /// </summary>
    [Fact]
    public void TryRemoveSelf_Removes_Property_And_ArrayItem()
    {
        const string json = """{ "p": 1, "arr": [10, 20, 30] }""";
        var parsed = JsonParser.ParseSafe(json, EditFriendly());

        Assert.True(JsonCursor.TryFromPath(parsed, "p", out var propCursor));
        Assert.True(propCursor!.TryRemoveSelf(json, out var eProp));
        var afterProp = ApplyEdit(json, eProp!);
        var checkProp = JsonParser.ParseSafe(afterProp, Profiles.Tolerant());
        Assert.Null(checkProp.Root!.SelectToken("p"));

        var reparsed = JsonParser.ParseSafe(afterProp, EditFriendly());
        Assert.True(JsonCursor.TryFromPath(reparsed, "arr[1]", out var itemCursor));
        Assert.True(itemCursor!.TryRemoveSelf(afterProp, out var eItem));
        var afterItem = ApplyEdit(afterProp, eItem!);
        var checkItem = JsonParser.ParseSafe(afterItem, Profiles.Tolerant());
        Assert.Equal(10, (int?)checkItem.Root!.SelectToken("arr[0]"));
        Assert.Equal(30, (int?)checkItem.Root!.SelectToken("arr[1]"));
    }

    /// <summary>
    ///     TrySetObject replaces the value at the cursor with an empty object.
    /// </summary>
    [Fact]
    public void TrySetObject_Replaces_Value_With_Empty_Object()
    {
        const string json = """{ "title": "X" }""";
        var parsed = JsonParser.ParseSafe(json, EditFriendly());
        Assert.True(JsonCursor.TryFromPath(parsed, "title", out var cursor));
        Assert.True(cursor!.TrySetObject(json, out var edit));
        var after = ApplyEdit(json, edit!);
        var check = JsonParser.ParseSafe(after, Profiles.Tolerant());
        Assert.Equal("{}", check.Root!.SelectToken("title")!.ToString(Formatting.None));
    }

    /// <summary>
    ///     TryInsertObjectProperty inserts a property whose value is {}.
    /// </summary>
    [Fact]
    public void TryInsertObjectProperty_Inserts_Empty_Object_Property()
    {
        const string json = """
                            {
                              "root": { }
                            }
                            """;
        var parsed = JsonParser.ParseSafe(json, EditFriendly());
        Assert.True(JsonCursor.TryFromPath(parsed, "root", out var rootCursor));
        Assert.True(rootCursor!.TryInsertObjectProperty(json, "child", true, "  ", out var edit));
        var after = ApplyEdit(json, edit!);
        var check = JsonParser.ParseSafe(after, Profiles.Tolerant());
        Assert.Equal("{}", check.Root!.SelectToken("root.child")!.ToString(Formatting.None));
    }

    /// <summary>
    ///     TryInsertObjectArrayItem appends {} into an array (also works at index via InsertArrayItem overloads).
    /// </summary>
    [Fact]
    public void TryInsertObjectArrayItem_Appends_Empty_Object()
    {
        const string json = """{ "items": [] }""";
        var parsed = JsonParser.ParseSafe(json, EditFriendly());
        Assert.True(JsonCursor.TryFromPath(parsed, "items", out var items));

        Assert.True(items!.TryInsertObjectArrayItem(json, null, true, out var edit));
        var after = ApplyEdit(json, edit!);
        var check = JsonParser.ParseSafe(after, Profiles.Tolerant());
        Assert.Equal("{}", check.Root!.SelectToken("items[0]")!.ToString(Formatting.None));
    }

    /// <summary>
    ///     TryRemoveAt removes a node by path by creating a cursor internally.
    /// </summary>
    [Fact]
    public void TryRemoveAt_Removes_By_Path()
    {
        const string json = """{ "root": { "a": 1, "b": 2 } }""";
        var parsed = JsonParser.ParseSafe(json, EditFriendly());
        Assert.True(JsonCursor.TryRemoveAt(json, parsed, "root.a", out var edit));
        var after = ApplyEdit(json, edit!);
        var check = JsonParser.ParseSafe(after, Profiles.Tolerant());
        Assert.Null(check.Root!.SelectToken("root.a"));
        Assert.Equal(2, (int?)check.Root!.SelectToken("root.b"));
    }

    private static string ApplyEdit(string text, TextEdit edit)
    {
        var s = edit.Range.Start.Offset;
        var e = edit.Range.End.Offset;
        if (s < 0 || e < s || e > text.Length) return text;
        return text[..s] + edit.NewText + text[e..];
    }
}