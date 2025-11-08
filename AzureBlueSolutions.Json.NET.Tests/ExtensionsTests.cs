using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using OmniSharp.Extensions.LanguageServer.Protocol;

namespace AzureBlueSolutions.Json.NET.Tests;

public sealed class ExtensionsTests
{
    [Fact]
    public void TextRange_ToRange_MapsStartAndEnd()
    {
        var start = new TextPosition(2, 4, 10);
        var end = new TextPosition(3, 1, 20);
        var r = new TextRange(start, end);

        var lsp = r.ToRange();

        Assert.Equal(start.Line, lsp.Start.Line);
        Assert.Equal(start.Column, lsp.Start.Character);
        Assert.Equal(end.Line, lsp.End.Line);
        Assert.Equal(end.Column, lsp.End.Character);
    }

    [Fact]
    public void TextEdit_ToTextEdit_CopiesRangeAndText()
    {
        var start = new TextPosition(0, 5, 5);
        var end = new TextPosition(0, 8, 8);
        var r = new TextRange(start, end);
        var edit = new TextEdit(r, "abc");

        var lspEdit = edit.ToTextEdit();

        Assert.Equal(start.Line, lspEdit.Range.Start.Line);
        Assert.Equal(start.Column, lspEdit.Range.Start.Character);
        Assert.Equal(end.Line, lspEdit.Range.End.Line);
        Assert.Equal(end.Column, lspEdit.Range.End.Character);
        Assert.Equal("abc", lspEdit.NewText);
    }

    [Fact]
    public void TextRange_ToLocation_BindsUriAndRange()
    {
        var start = new TextPosition(1, 2, 12);
        var end = new TextPosition(1, 6, 16);
        var r = new TextRange(start, end);
        var uri = DocumentUri.From("file:///c:/temp/sample.json");

        var loc = r.ToLocation(uri);

        Assert.Equal(uri, loc.Uri);
        Assert.Equal(r.ToRange(), loc.Range);
    }

    [Fact]
    public void Enumerable_ToTextEdits_ConvertsBatch()
    {
        var r1 = new TextRange(new TextPosition(0, 0, 0), new TextPosition(0, 1, 1));
        var r2 = new TextRange(new TextPosition(1, 2, 7), new TextPosition(1, 4, 9));
        var edits = new[]
        {
            new TextEdit(r1, "X"),
            new TextEdit(r2, "YY")
        };

        var lsp = edits.ToTextEdits();

        Assert.NotNull(lsp);
        Assert.Equal(2, lsp!.Length);
        Assert.Equal(edits[0].Range.ToRange(), lsp[0].Range);
        Assert.Equal("X", lsp[0].NewText);
        Assert.Equal(edits[1].Range.ToRange(), lsp[1].Range);
        Assert.Equal("YY", lsp[1].NewText);
    }

    [Fact]
    public void TextPosition_ToOneBased_IncrementsBoth()
    {
        var p = new TextPosition(0, 0, 0);
        var (l1, c1) = p.ToOneBased();
        Assert.Equal(1, l1);
        Assert.Equal(1, c1);

        p = new TextPosition(10, 25, 0);
        (l1, c1) = p.ToOneBased();
        Assert.Equal(11, l1);
        Assert.Equal(26, c1);
    }

    [Fact]
    public void Tuple_ToZeroBasedPosition_ClampAndDecrement()
    {
        var p = (1, 1).ToZeroBasedPosition();
        Assert.Equal(0, p.Line);
        Assert.Equal(0, p.Column);

        p = (5, 9).ToZeroBasedPosition();
        Assert.Equal(4, p.Line);
        Assert.Equal(8, p.Column);

        p = (0, 0).ToZeroBasedPosition();
        Assert.Equal(0, p.Line);
        Assert.Equal(0, p.Column);
    }

    [Fact]
    public void TextLineIndex_ToOffset_MapsLineAndColumn()
    {
        var text = "abc\ndef\nxyz";
        var index = new TextLineIndex(text);

        var off0 = index.ToOffset(0, 2);
        Assert.Equal(2, off0);

        var off1 = index.ToOffset(1, 1);
        // Line 0: "abc\n" -> length 4, so start of line 1 is 4; +1 column = 5
        Assert.Equal(5, off1);

        var off2 = index.ToOffset(2, 0);
        // Line 2 starts at "abc\n" (4) + "def\n" (4) = 8
        Assert.Equal(8, off2);
    }

    [Fact]
    public void JsonTokenSpan_ToTextRange_ReturnsRange()
    {
        var start = new TextPosition(0, 0, 0);
        var end = new TextPosition(0, 3, 3);
        var r = new TextRange(start, end);
        var t = new JsonTokenSpan(JsonLexemeKind.String, r);

        var back = t.ToTextRange();
        Assert.Same(r, back);
    }

    [Fact]
    public void JsonTokenSpan_ToOffsets_ReturnsStartAndEndOffsets()
    {
        var start = new TextPosition(0, 0, 10);
        var end = new TextPosition(0, 2, 12);
        var r = new TextRange(start, end);
        var t = new JsonTokenSpan(JsonLexemeKind.Number, r);

        var (s, e) = t.ToOffsets();
        Assert.Equal(10, s);
        Assert.Equal(12, e);
    }

    [Fact]
    public void TokenSequence_FirstAndLastOfKind_FindCorrectItems()
    {
        var t1 = new JsonTokenSpan(JsonLexemeKind.LeftBrace,
            new TextRange(new TextPosition(0, 0, 0), new TextPosition(0, 1, 1)));
        var t2 = new JsonTokenSpan(JsonLexemeKind.String,
            new TextRange(new TextPosition(0, 1, 1), new TextPosition(0, 4, 4)));
        var t3 = new JsonTokenSpan(JsonLexemeKind.Colon,
            new TextRange(new TextPosition(0, 4, 4), new TextPosition(0, 5, 5)));
        var t4 = new JsonTokenSpan(JsonLexemeKind.String,
            new TextRange(new TextPosition(0, 6, 6), new TextPosition(0, 9, 9)));
        var list = new List<JsonTokenSpan> { t1, t2, t3, t4 };

        var firstString = list.FirstOfKind(JsonLexemeKind.String);
        var lastString = list.LastOfKind(JsonLexemeKind.String);

        Assert.Same(t2, firstString);
        Assert.Same(t4, lastString);
        Assert.Null(list.FirstOfKind(JsonLexemeKind.RightBracket));
        Assert.Null(list.LastOfKind(JsonLexemeKind.RightBracket));
    }

    [Fact]
    public void JToken_GetLineInfo_ReturnsOneBasedTuple_WhenAvailable()
    {
        var json = "{\n  \"a\": 1,\n  \"b\": 2\n}";
        var load = new JsonLoadSettings
        {
            LineInfoHandling = LineInfoHandling.Load,
            CommentHandling = CommentHandling.Ignore
        };

        using var sr = new StringReader(json);
        using var reader = new JsonTextReader(sr);
        var token = JToken.ReadFrom(reader, load);

        // Select "b" value token; navigate JToken tree
        var bValue = token["b"];
        Assert.NotNull(bValue);

        var info = bValue.GetLineInfo();
        Assert.NotNull(info);

        var (line1, pos1) = info.Value;

        // "b" is on line 3 (1-based) with some position >= 3 (approx), depending on spacing
        Assert.True(line1 >= 3);
        Assert.True(pos1 >= 3);
    }
}