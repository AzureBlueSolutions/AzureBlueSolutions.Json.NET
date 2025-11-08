namespace AzureBlueSolutions.Json.NET;

/// <summary>
///     Represents a zero-based logical position in a source text.
///     Includes line, column, and absolute offset for LSP-friendly mapping.
/// </summary>
/// <param name="Line">
///     The zero-based line number.
/// </param>
/// <param name="Column">
///     The zero-based column number within the line.
/// </param>
/// <param name="Offset">
///     The absolute offset from the start of the text.
/// </param>
public sealed record TextPosition(int Line, int Column, int Offset);

/// <summary>
///     Represents a zero-based range in a source text, where
///     <see cref="Start" /> is inclusive and <see cref="End" /> is exclusive.
///     Suitable for LSP-friendly mapping.
/// </summary>
/// <param name="Start">
///     The starting position of the range (inclusive).
/// </param>
/// <param name="End">
///     The ending position of the range (exclusive).
/// </param>
public sealed record TextRange(TextPosition Start, TextPosition End)
{
    /// <summary>
    ///     Creates a <see cref="TextRange" /> from one-based line and column values,
    ///     converting them to zero-based positions. The range defaults to one character
    ///     on the same line.
    /// </summary>
    /// <param name="line">
    ///     The one-based line number.
    /// </param>
    /// <param name="column">
    ///     The one-based column number.
    /// </param>
    /// <returns>
    ///     A <see cref="TextRange" /> covering a single character at the specified position.
    /// </returns>
    public static TextRange FromOneBased(int line, int column)
    {
        var l0 = Math.Max(0, line - 1);
        var c0 = Math.Max(0, column - 1);
        var start = new TextPosition(l0, c0, 0);
        var end = new TextPosition(l0, c0 + 1, 0);
        return new TextRange(start, end);
    }

    /// <summary>
    ///     Returns a new <see cref="TextRange" /> with updated absolute offsets,
    ///     preserving the original line and column values.
    /// </summary>
    /// <param name="startOffset">
    ///     The absolute offset for the start position.
    /// </param>
    /// <param name="endOffset">
    ///     The absolute offset for the end position.
    /// </param>
    /// <returns>
    ///     A new <see cref="TextRange" /> with adjusted offsets.
    /// </returns>
    public TextRange WithOffsets(int startOffset, int endOffset)
    {
        return new TextRange(
            Start with { Offset = startOffset },
            End with { Offset = endOffset });
    }
}

/// <summary>
///     Provides utilities for converting between (line, column) positions and absolute offsets
///     in a source text.
/// </summary>
public sealed class TextLineIndex
{
    private readonly int[] _lineStarts;
    private readonly string _text;

    /// <summary>
    ///     Initializes a new <see cref="TextLineIndex" /> for the specified text.
    /// </summary>
    /// <param name="text">
    ///     The source text to index.
    /// </param>
    public TextLineIndex(string text)
    {
        _text = text;
        _lineStarts = BuildLineStarts(_text);
    }

    /// <summary>
    ///     Computes the absolute offset for a given zero-based line and column.
    /// </summary>
    /// <param name="zeroBasedLine">
    ///     The zero-based line number.
    /// </param>
    /// <param name="zeroBasedColumn">
    ///     The zero-based column number within the line.
    /// </param>
    /// <returns>
    ///     The absolute offset corresponding to the specified position.
    /// </returns>
    public int GetOffset(int zeroBasedLine, int zeroBasedColumn)
    {
        if (zeroBasedLine < 0) zeroBasedLine = 0;
        if (zeroBasedLine >= _lineStarts.Length)
            zeroBasedLine = _lineStarts.Length - 1;

        var start = _lineStarts[zeroBasedLine];
        var end = zeroBasedLine + 1 < _lineStarts.Length
            ? _lineStarts[zeroBasedLine + 1]
            : _text.Length;

        var column = Math.Max(0, zeroBasedColumn);
        var offset = Math.Min(start + column, end);
        return offset;
    }

    /// <summary>
    ///     Builds an array of line start offsets for the specified text.
    /// </summary>
    /// <param name="text">
    ///     The text to analyze.
    /// </param>
    /// <returns>
    ///     An array of offsets where each entry marks the start of a line.
    /// </returns>
    private static int[] BuildLineStarts(string text)
    {
        var starts = new List<int>(Math.Max(4, text.Length / 24)) { 0 };

        for (var i = 0; i < text.Length; i++)
        {
            var c = text[i];
            switch (c)
            {
                case '\r':
                    if (i + 1 < text.Length && text[i + 1] == '\n') i++;
                    starts.Add(i + 1);
                    break;
                case '\n':
                    starts.Add(i + 1);
                    break;
            }
        }

        return starts.ToArray();
    }
}