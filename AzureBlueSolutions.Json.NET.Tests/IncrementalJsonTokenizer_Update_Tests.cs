using System.Linq;
using AzureBlueSolutions.Json.NET;

namespace AzureBlueSolutions.Json.NET.Tests;

public sealed class IncrementalJsonTokenizer_Update_Tests
{
    [Fact]
    public void Update_Replaces_String_In_Window_And_Merges_Spans()
    {
        const string oldText = """
                               {
                                 "name": "x",
                                 "arr": [1,2]
                               }
                               """;
        var parse = JsonParser.ParseSafe(oldText, Profiles.Tolerant());
        var oldSpans = parse.TokenSpans;
        var i = oldText.IndexOf("\"x\"", StringComparison.Ordinal);
        var change = new TextChange(i + 1, i + 2, "xyz");
        var updated = IncrementalJsonTokenizer.Update(oldText, oldSpans, new[] { change }, 64, default);
        Assert.Contains("\"xyz\"", updated.Text);
        Assert.True(updated.TokenSpans.Count > 0);
        Assert.True(updated.WindowStartOffset <= i && updated.WindowEndOffset >= i + 3);
    }

    [Fact]
    public void Update_Insert_Number_Produces_Number_Token_In_Window()
    {
        const string oldText = """{"n":0}""";
        var parse = JsonParser.ParseSafe(oldText, Profiles.Tolerant());
        var oldSpans = parse.TokenSpans;
        var insertAt = oldText.IndexOf('}', StringComparison.Ordinal);
        var change = new TextChange(insertAt, insertAt, ",\"m\":123");
        var updated = IncrementalJsonTokenizer.Update(oldText, oldSpans, new[] { change }, 32, default);
        Assert.Contains("\"m\":123", updated.Text);
        var kinds = updated.TokenSpans.Select(t => t.Kind).ToArray();
        Assert.Contains(JsonLexemeKind.Number, kinds);
    }
}