namespace AzureBlueSolutions.Json.NET;

/// <summary>
/// Zero-based logical position in a source text.
/// </summary>
public sealed record TextPosition(int Line, int Column, int Offset);

/// <summary>
/// Zero-based range (start inclusive, end exclusive) for LSP-friendly mapping.
/// </summary>
public sealed record TextRange(TextPosition Start, TextPosition End)
{
    public static TextRange FromOneBased(int line, int column)
    {
        // Convert to zero-based; length defaults to 1 character on the same line.
        int l0 = Math.Max(0, line - 1);
        int c0 = Math.Max(0, column - 1);
        var start = new TextPosition(l0, c0, 0);
        var end = new TextPosition(l0, c0 + 1, 0);
        return new TextRange(start, end);
    }

    public TextRange WithOffsets(int startOffset, int endOffset)
        => new(new TextPosition(Start.Line, Start.Column, startOffset),
               new TextPosition(End.Line, End.Column, endOffset));
}

/// <summary>
/// Helper for converting between (line, column) and absolute offsets.
/// </summary>
public sealed class TextLineIndex
{
    private readonly string _text;
    private readonly int[] _lineStarts;

    public TextLineIndex(string text)
    {
        _text = text;
        _lineStarts = BuildLineStarts(_text);
    }

    public int GetOffset(int zeroBasedLine, int zeroBasedColumn)
    {
        if (zeroBasedLine < 0) zeroBasedLine = 0;
        if (zeroBasedLine >= _lineStarts.Length)
            zeroBasedLine = _lineStarts.Length - 1;

        int start = _lineStarts[zeroBasedLine];
        int end = (zeroBasedLine + 1 < _lineStarts.Length)
            ? _lineStarts[zeroBasedLine + 1]
            : _text.Length;

        int column = Math.Max(0, zeroBasedColumn);
        int offset = Math.Min(start + column, end);
        return offset;
    }

    private static int[] BuildLineStarts(string text)
    {
        var starts = new System.Collections.Generic.List<int>(Math.Max(4, text.Length / 24));
        starts.Add(0);

        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];
            switch (c)
            {
                case '\r':
                    {
                        if (i + 1 < text.Length && text[i + 1] == '\n') i++;
                        starts.Add(i + 1);
                        break;
                    }
                case '\n':
                    starts.Add(i + 1);
                    break;
            }
        }

        return starts.ToArray();
    }
}