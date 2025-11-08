namespace AzureBlueSolutions.Json.NET.Tests;

/// <summary>
///     Tests for CommaPolicy newline insertion and trailing-comma removal behaviors.
/// </summary>
public sealed class CommaPolicyTests
{
    /// <summary>
    ///     Inserts a comma after a completed value when a new property name starts on the next line.
    /// </summary>
    [Fact]
    public void TryInsertCommaBeforeNewline_Inserts_Comma_When_Missing()
    {
        var json =
            @"{
 ""a"": 1
 ""b"": 2
}";
        var tokens = Lex(json, false);

        // Robust caret: position it at the start of the NEXT property name token ("b").
        var firstNumber = FirstToken(tokens, JsonLexemeKind.Number);
        Assert.NotNull(firstNumber);

        var nextName = NextTokenStartingAtOrAfter(tokens, JsonLexemeKind.String, firstNumber!.Range.End.Offset + 1);
        Assert.NotNull(nextName); // should be the "b" property name

        var caretOffset = nextName!.Range.Start.Offset;

        var edit = CommaPolicy.TryInsertCommaBeforeNewline(json, tokens, caretOffset);

        Assert.NotNull(edit);
        Assert.Equal(",", edit!.NewText);

        // The comma is inserted exactly at the end of the number token "1".
        Assert.Equal(firstNumber.Range.End.Offset, edit.Range.Start.Offset);
        Assert.Equal(firstNumber.Range.End.Offset, edit.Range.End.Offset);
    }

    /// <summary>
    ///     Does not insert a comma when one already exists between the value and the newline.
    /// </summary>
    [Fact]
    public void TryInsertCommaBeforeNewline_Does_Not_Insert_When_Comma_Exists()
    {
        var json =
            """
            {
            "a": 1,
            "b": 2
            }
            """;
        var tokens = Lex(json, false);

        // Place caret at the start of the next property name ("b") again.
        var firstNumber = FirstToken(tokens, JsonLexemeKind.Number);
        Assert.NotNull(firstNumber);

        var nextName = NextTokenStartingAtOrAfter(tokens, JsonLexemeKind.String, firstNumber!.Range.End.Offset + 1);
        Assert.NotNull(nextName);

        var caretOffset = nextName!.Range.Start.Offset;

        var edit = CommaPolicy.TryInsertCommaBeforeNewline(json, tokens, caretOffset);

        Assert.Null(edit);
    }

    /// <summary>
    ///     Removes a trailing comma that appears immediately before a '}' object closer.
    /// </summary>
    [Fact]
    public void TryRemoveCommaBeforeCloser_Removes_For_Object()
    {
        var json = "{ \"a\": 1, }";
        var tokens = Lex(json, false);

        // Compute caret using the actual RightBrace token end
        var rightBrace = FirstToken(tokens, JsonLexemeKind.RightBrace);
        Assert.NotNull(rightBrace);

        var caretAfterCloser = rightBrace!.Range.End.Offset;
        var edit = CommaPolicy.TryRemoveCommaBeforeCloser(json, tokens, caretAfterCloser);

        Assert.NotNull(edit);
        Assert.Equal(string.Empty, edit!.NewText);

        // Text-based assertion: the removed range must be the ',' immediately before the closer
        var expectedCommaIndex = json.LastIndexOf(',', rightBrace.Range.Start.Offset - 1);
        Assert.True(expectedCommaIndex >= 0);
        Assert.Equal(expectedCommaIndex, edit.Range.Start.Offset);
        Assert.Equal(expectedCommaIndex + 1, edit.Range.End.Offset);
    }

    /// <summary>
    ///     Removes a trailing comma that appears immediately before a ']' array closer.
    /// </summary>
    [Fact]
    public void TryRemoveCommaBeforeCloser_Removes_For_Array()
    {
        var json = "[1, 2, ]";
        var tokens = Lex(json, false);

        // Compute caret using the actual RightBracket token end
        var rightBracket = FirstToken(tokens, JsonLexemeKind.RightBracket);
        Assert.NotNull(rightBracket);

        var caretAfterCloser = rightBracket!.Range.End.Offset;
        var edit = CommaPolicy.TryRemoveCommaBeforeCloser(json, tokens, caretAfterCloser);

        Assert.NotNull(edit);
        Assert.Equal(string.Empty, edit!.NewText);

        // Text-based assertion: the removed range must be the ',' immediately before the closer
        var expectedCommaIndex = json.LastIndexOf(',', rightBracket.Range.Start.Offset - 1);
        Assert.True(expectedCommaIndex >= 0);
        Assert.Equal(expectedCommaIndex, edit.Range.Start.Offset);
        Assert.Equal(expectedCommaIndex + 1, edit.Range.End.Offset);
    }

    /// <summary>
    ///     Does not produce an edit when there is no trailing comma before the closer.
    /// </summary>
    [Fact]
    public void TryRemoveCommaBeforeCloser_No_Edit_When_No_Trailing_Comma()
    {
        var json = "{ \"a\": 1 }";
        var tokens = Lex(json, false);

        var rightBrace = FirstToken(tokens, JsonLexemeKind.RightBrace);
        Assert.NotNull(rightBrace);

        var caretAfterCloser = rightBrace!.Range.End.Offset;
        var edit = CommaPolicy.TryRemoveCommaBeforeCloser(json, tokens, caretAfterCloser);

        Assert.Null(edit);
    }

    private static IReadOnlyList<JsonTokenSpan> Lex(string json, bool allowComments)
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
    ///     Returns the first token of the specified kind; if startOffsetLessThan is provided,
    ///     returns the last token of that kind whose start is strictly less than the threshold.
    /// </summary>
    private static JsonTokenSpan? FirstToken(
        IReadOnlyList<JsonTokenSpan> tokens,
        JsonLexemeKind kind,
        int? startOffsetLessThan = null)
    {
        JsonTokenSpan? candidate = null;
        foreach (var t in tokens)
        {
            if (t.Kind != kind) continue;
            if (startOffsetLessThan is null)
                return t;
            if (t.Range.Start.Offset < startOffsetLessThan.Value)
                candidate = t;
        }

        return candidate;
    }

    /// <summary>
    ///     Returns the first token of the specified kind whose start offset is >= minStartOffset.
    /// </summary>
    private static JsonTokenSpan? NextTokenStartingAtOrAfter(
        IReadOnlyList<JsonTokenSpan> tokens,
        JsonLexemeKind kind,
        int minStartOffset)
    {
        foreach (var t in tokens)
            if (t.Kind == kind && t.Range.Start.Offset >= minStartOffset)
                return t;
        return null;
    }
}