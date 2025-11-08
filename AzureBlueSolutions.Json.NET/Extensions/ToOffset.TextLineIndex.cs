namespace AzureBlueSolutions.Json.NET;

/// <summary>
///     Offset conversion using <see cref="TextLineIndex" />.
/// </summary>
public static class TextLineIndexToOffsetExtensions
{
    /// <summary>
    ///     Converts a zero-based (line, column) pair to an absolute offset using <see cref="TextLineIndex" />.
    /// </summary>
    /// <param name="index">The line index built from a specific document text.</param>
    /// <param name="zeroBasedLine">The zero-based line number.</param>
    /// <param name="zeroBasedColumn">The zero-based column value within the line.</param>
    /// <returns>
    ///     The absolute character offset corresponding to (<paramref name="zeroBasedLine" />,
    ///     <paramref name="zeroBasedColumn" />).
    /// </returns>
    /// <remarks>
    ///     This is a thin convenience wrapper over <see cref="TextLineIndex.GetOffset(int, int)" />.
    /// </remarks>
    public static int ToOffset(this TextLineIndex index, int zeroBasedLine, int zeroBasedColumn)
    {
        return index.GetOffset(zeroBasedLine, zeroBasedColumn);
    }
}