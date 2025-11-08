namespace AzureBlueSolutions.Json.NET;

/// <summary>
///     Represents a single textual edit applied to a source string.
///     Offsets are zero-based; <paramref name="EndOffset" /> is end-exclusive.
/// </summary>
/// <remarks>
///     The slice defined by <c>[StartOffset, EndOffset)</c> in the original text is replaced by <see cref="NewText" />.
///     If <see cref="NewText" /> is <c>null</c>, the operation is a deletion; if it is an empty string, the slice is
///     removed.
/// </remarks>
/// <param name="StartOffset">
///     The zero-based start offset (inclusive) of the range to replace.
/// </param>
/// <param name="EndOffset">
///     The zero-based end offset (exclusive) of the range to replace. Must be greater than or equal to
///     <paramref name="StartOffset" />.
/// </param>
/// <param name="NewText">
///     The text to insert in place of the specified range. May be <c>null</c> to indicate deletion.
/// </param>
public sealed record TextChange(int StartOffset, int EndOffset, string NewText)
{
    /// <summary>
    ///     Gets the net change in document length produced by this edit,
    ///     defined as <c>max(0, NewText?.Length ?? 0) - max(0, EndOffset - StartOffset)</c>.
    /// </summary>
    /// <remarks>
    ///     Positive values indicate an insertion/expansion; negative values indicate a deletion/shrink.
    /// </remarks>
    public int LengthDelta => Math.Max(0, NewText?.Length ?? 0) - Math.Max(0, EndOffset - StartOffset);
}