namespace AzureBlueSolutions.Json.NET;

/// <summary>
///     Offset pair retrieval for <see cref="JsonTokenSpan" />.
/// </summary>
public static class JsonTokenSpanToOffsetsExtensions
{
    /// <summary>
    ///     Returns the absolute start and end offsets for a token span's range.
    /// </summary>
    /// <param name="t">The token span whose offsets should be returned.</param>
    /// <returns>
    ///     A tuple (start, end) representing <see cref="TextRange.Start" /> and <see cref="TextRange.End" />.
    /// </returns>
    public static (int start, int end) ToOffsets(this JsonTokenSpan t)
    {
        return (t.Range.Start.Offset, t.Range.End.Offset);
    }
}