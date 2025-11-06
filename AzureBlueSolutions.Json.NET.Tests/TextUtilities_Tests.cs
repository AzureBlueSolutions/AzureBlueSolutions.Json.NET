namespace AzureBlueSolutions.Json.NET.Tests;

public sealed class TextUtilities_Tests
{
    [Fact]
    public void TextRange_FromOneBased_Converts_To_ZeroBased()
    {
        var r = TextRange.FromOneBased(2, 5);
        Assert.Equal(1, r.Start.Line);
        Assert.Equal(4, r.Start.Column);
        Assert.Equal(0, r.Start.Offset);
        Assert.Equal(1, r.End.Line);
        Assert.Equal(5, r.End.Column);
        Assert.Equal(0, r.End.Offset);
    }

    [Fact]
    public void TextRange_WithOffsets_Sets_Offsets()
    {
        var r = TextRange.FromOneBased(1, 1).WithOffsets(10, 12);
        Assert.Equal(10, r.Start.Offset);
        Assert.Equal(12, r.End.Offset);
    }

    [Fact]
    public void TextLineIndex_Maps_Line_And_Column_To_Offsets()
    {
        const string text = "alpha\nbeta\r\ngamma\n";
        var index = new TextLineIndex(text);
        var off00 = index.GetOffset(0, 0);
        var off04 = index.GetOffset(0, 4);
        var off10 = index.GetOffset(1, 0);
        var off112 = index.GetOffset(1, 12);
        Assert.Equal(0, off00);
        Assert.Equal(4, off04);
        Assert.Equal(6, off10); // after "alpha\n"
        Assert.True(off112 >= off10); // clamped within line length
    }
}