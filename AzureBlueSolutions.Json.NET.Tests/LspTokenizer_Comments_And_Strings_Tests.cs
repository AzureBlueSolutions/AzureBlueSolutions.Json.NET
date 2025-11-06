using System.Linq;
using AzureBlueSolutions.Json.NET;

namespace AzureBlueSolutions.Json.NET.Tests;

public sealed class LspTokenizer_Comments_And_Strings_Tests
{
    [Fact]
    public void Tokenizer_Emits_Comment_Tokens_For_Line_And_Block()
    {
        const string json = """
        {
          // line
          "a": 1, /* block */ "b": 2
        }
        """;
        var result = JsonParser.ParseSafe(json, Profiles.Tolerant());
        var kinds = result.TokenSpans.Select(t => t.Kind).ToArray();
        Assert.Contains(JsonLexemeKind.Comment, kinds);
    }

    [Fact]
    public void Tokenizer_Handles_Escaped_Quotes_And_Newlines_In_Strings()
    {
        const string json = """{ "s": "a \"b\" \n c" }""";
        var result = JsonParser.ParseSafe(json, Profiles.Tolerant());
        Assert.True(result.Success);
        var kinds = result.TokenSpans.Select(t => t.Kind).ToArray();
        Assert.Contains(JsonLexemeKind.String, kinds);
    }
}