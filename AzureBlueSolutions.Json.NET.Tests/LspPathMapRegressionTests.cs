namespace AzureBlueSolutions.Json.NET.Tests;

public sealed class LspPathMapRegressionTests
{
    [Fact]
    public void JValue_DoesNotOverwrite_JProperty_NameRange()
    {
        const string json = """
                            {
                              "title": "X",
                              "nested": { "title": "Y" }
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
        var title = result.PathRanges["title"];
        Assert.NotNull(title.Name);
        Assert.NotNull(title.Value);

        Assert.True(result.PathRanges.ContainsKey("nested.title"));
        var nestedTitle = result.PathRanges["nested.title"];
        Assert.NotNull(nestedTitle.Name);
        Assert.NotNull(nestedTitle.Value);
    }

    [Fact]
    public void Property_With_Comments_Around_Colon_Has_Name_And_Value()
    {
        string json = """
        {
          /*a*/ "name" /*b*/ : /*c*/ "ReCrafter" /*d*/
        }
        """;

        var options = new ParseOptions
        {
            ProduceTokenSpans = true,
            ProducePathMap = true
        };

        var result = JsonParser.ParseSafe(json, options);
        Assert.True(result.Success);

        Assert.True(result.PathRanges.ContainsKey("name"));
        var name = result.PathRanges["name"];
        Assert.NotNull(name.Name);
        Assert.NotNull(name.Value);
    }

    [Fact]
    public void Multiline_And_Escaped_Strings_Are_Mapped()
    {
        string json = """
        {
          "multi": "line1\nline2",
          "quoted": "a \"quoted\" value"
        }
        """;

        var options = new ParseOptions
        {
            ProduceTokenSpans = true,
            ProducePathMap = true
        };

        var result = JsonParser.ParseSafe(json, options);
        Assert.True(result.Success);

        var multi = result.PathRanges["multi"];
        Assert.NotNull(multi.Name);
        Assert.NotNull(multi.Value);

        var quoted = result.PathRanges["quoted"];
        Assert.NotNull(quoted.Name);
        Assert.NotNull(quoted.Value);
    }
}