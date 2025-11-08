namespace AzureBlueSolutions.Json.NET;

/// <summary>
///     One-based conversion for <see cref="TextPosition" />.
/// </summary>
public static class TextPositionToOneBasedExtensions
{
    /// <summary>
    ///     Converts a zero-based <see cref="TextPosition" /> to one-based line and column values.
    /// </summary>
    /// <param name="p">The zero-based position to convert.</param>
    /// <returns>
    ///     A tuple (line1, col1) where both values are one-based (i.e., +1 applied to the zero-based input).
    /// </returns>
    /// <remarks>
    ///     Newtonsoft.Json line info (<see cref="Newtonsoft.Json.IJsonLineInfo" />) is traditionally one-based.
    ///     Use this helper when you need to present positions in that convention.
    /// </remarks>
    public static (int line1, int col1) ToOneBased(this TextPosition p)
    {
        return (p.Line + 1, p.Column + 1);
    }
}