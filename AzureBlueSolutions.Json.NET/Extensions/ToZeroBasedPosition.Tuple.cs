namespace AzureBlueSolutions.Json.NET;

/// <summary>
///     Zero-based conversion from one-based (line, column) tuple to <see cref="TextPosition" />.
/// </summary>
public static class TupleToZeroBasedTextPositionExtensions
{
    /// <summary>
    ///     Creates a zero-based <see cref="TextPosition" /> from one-based line and column values.
    /// </summary>
    /// <param name="one">
    ///     A tuple containing (line1, col1) where both components are expected to be one-based.
    /// </param>
    /// <returns>
    ///     A new zero-based <see cref="TextPosition" /> with offset initialized to <c>0</c>.
    /// </returns>
    /// <remarks>
    ///     Values are clamped to be non-negative after subtracting 1. The <see cref="TextPosition.Offset" />
    ///     is not computed here and remains <c>0</c>.
    /// </remarks>
    public static TextPosition ToZeroBasedPosition(this (int line1, int col1) one)
    {
        return new TextPosition(Math.Max(0, one.line1 - 1), Math.Max(0, one.col1 - 1), 0);
    }
}