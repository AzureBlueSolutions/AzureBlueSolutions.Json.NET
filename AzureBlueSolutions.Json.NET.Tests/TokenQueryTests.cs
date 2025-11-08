namespace AzureBlueSolutions.Json.NET.Tests;

/// <summary>
///     End-to-end tests for TokenQuery over real token streams produced by the library.
/// </summary>
public sealed class TokenQueryTests
{
    /// <summary>
    ///     PreviousSignificant should return the last non-comment token whose end offset is <= the given offset.
    /// </summary>
    [Fact]
    public void PreviousSignificant_Returns_LastNonCommentToken()
    {
        var json = "{\n  // c1\n  \"a\": 1,\n  /* c2 */ \"b\": 2\n}";
        var tokens = Lex(json, true);

        var cursorOffset = json.IndexOf("\"b\"", StringComparison.Ordinal);
        var previous = TokenQuery.PreviousSignificant(tokens, cursorOffset);

        Assert.True(previous.HasValue);
        Assert.Equal(JsonLexemeKind.Comma, previous!.Value.token.Kind);

        var offsetJustBeforeComma = previous.Value.token.Range.Start.Offset;
        var previousBeforeComma = TokenQuery.PreviousSignificant(tokens, offsetJustBeforeComma);
        Assert.True(previousBeforeComma.HasValue);
        Assert.Equal(JsonLexemeKind.Number, previousBeforeComma!.Value.token.Kind);
    }

    /// <summary>
    ///     NextSignificant should return the first non-comment token whose start offset is >= the given offset.
    /// </summary>
    [Fact]
    public void NextSignificant_Skips_Comments_And_Whitespace()
    {
        var json = "{\n  // leading\n  \"a\": 1,\n  /* block */   \"b\": 2\n}";
        var tokens = Lex(json, true);

        var offsetAtNewline = json.IndexOf('\n') + 1;
        var next = TokenQuery.NextSignificant(tokens, offsetAtNewline);

        Assert.True(next.HasValue);
        Assert.Equal(JsonLexemeKind.String, next!.Value.token.Kind);
    }

    /// <summary>
    ///     TokenEndingAt should find a token whose end equals the offset; TokenCovering should find a token that spans the
    ///     offset.
    /// </summary>
    [Fact]
    public void TokenEndingAt_And_Covering_Work_As_Expected()
    {
        var json = "{\"name\": \"Chase\", \"n\": 42}";
        var tokens = Lex(json);

        var nameString = "\"name\"";
        var nameEndOffset = json.IndexOf(nameString, StringComparison.Ordinal) + nameString.Length;
        var ending = TokenQuery.TokenEndingAt(tokens, nameEndOffset);

        Assert.True(ending.HasValue);
        Assert.Equal(JsonLexemeKind.String, ending!.Value.token.Kind);

        var chaseOffsetInside = json.IndexOf("Chase", StringComparison.Ordinal) + 2;
        var covering = TokenQuery.TokenCovering(tokens, chaseOffsetInside);

        Assert.True(covering.HasValue);
        Assert.Equal(JsonLexemeKind.String, covering!.Value.token.Kind);
    }

    /// <summary>
    ///     LooksLikePropertyName should identify a String token followed by optional comments and a Colon.
    /// </summary>
    [Fact]
    public void LooksLikePropertyName_Detects_Property()
    {
        var json = "{ /* pre */ \"prop\" /* mid */ : 123 }";
        var tokens = Lex(json, true);

        var stringIndex = IndexOfFirst(tokens, JsonLexemeKind.String);
        Assert.True(stringIndex >= 0);
        Assert.True(TokenQuery.LooksLikePropertyName(tokens, stringIndex));
    }

    /// <summary>
    ///     IsValueTerminator must return true for string/number/true/false/null/']'/'}' kinds.
    /// </summary>
    [Fact]
    public void IsValueTerminator_Matches_Correct_Kinds()
    {
        Assert.True(TokenQuery.IsValueTerminator(JsonLexemeKind.String));
        Assert.True(TokenQuery.IsValueTerminator(JsonLexemeKind.Number));
        Assert.True(TokenQuery.IsValueTerminator(JsonLexemeKind.True));
        Assert.True(TokenQuery.IsValueTerminator(JsonLexemeKind.False));
        Assert.True(TokenQuery.IsValueTerminator(JsonLexemeKind.Null));
        Assert.True(TokenQuery.IsValueTerminator(JsonLexemeKind.RightBracket));
        Assert.True(TokenQuery.IsValueTerminator(JsonLexemeKind.RightBrace));

        Assert.False(TokenQuery.IsValueTerminator(JsonLexemeKind.Comma));
        Assert.False(TokenQuery.IsValueTerminator(JsonLexemeKind.Colon));
        Assert.False(TokenQuery.IsValueTerminator(JsonLexemeKind.LeftBracket));
        Assert.False(TokenQuery.IsValueTerminator(JsonLexemeKind.LeftBrace));
        Assert.False(TokenQuery.IsValueTerminator(JsonLexemeKind.Comment));
    }

    /// <summary>
    ///     HasCommaBetween should detect a comma before a newline between two offsets.
    /// </summary>
    [Fact]
    public void HasCommaBetween_Detects_Comma_Before_Newline()
    {
        var json = "{\n  \"a\": 1,\n  \"b\": 2\n}";
        var startOne = json.IndexOf("1", StringComparison.Ordinal);
        var startOffset = startOne + 1;
        var endOffset = json.IndexOf('\n', startOne) + 1;

        Assert.True(TokenQuery.HasCommaBetween(json, startOffset, endOffset));

        var jsonNoComma = "{\n  \"a\": 1\n  \"b\": 2\n}";
        var startOneNo = jsonNoComma.IndexOf("1", StringComparison.Ordinal);
        var startOffsetNo = startOneNo + 1;
        var endOffsetNo = jsonNoComma.IndexOf('\n', startOneNo) + 1;
        Assert.False(TokenQuery.HasCommaBetween(jsonNoComma, startOffsetNo, endOffsetNo));
    }

    /// <summary>
    ///     Creates token spans via JsonParser with ProduceTokenSpans enabled.
    /// </summary>
    private static IReadOnlyList<JsonTokenSpan> Lex(string json, bool allowComments = false)
    {
        var options = new ParseOptions
        {
            AllowComments = allowComments,
            CollectLineInfo = false,
            ProduceTokenSpans = true,
            ProducePathMap = false,
            NormalizeLineEndings = true,
            IncludeSanitizationDiagnostics = false,
            EnableSanitizationFallback = false
        };
        var result = JsonParser.ParseSafe(json, options);
        return result.TokenSpans;
    }

    /// <summary>
    ///     Finds the first index of a token with a given kind.
    /// </summary>
    private static int IndexOfFirst(IReadOnlyList<JsonTokenSpan> tokens, JsonLexemeKind kind)
    {
        for (var i = 0; i < tokens.Count; i++)
            if (tokens[i].Kind == kind)
                return i;
        return -1;
    }
}