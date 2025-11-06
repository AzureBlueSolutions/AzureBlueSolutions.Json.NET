namespace AzureBlueSolutions.Json.NET;

/// <summary>
/// Single textual change. Offsets are zero-based, end exclusive.
/// </summary>
public sealed record TextChange(int StartOffset, int EndOffset, string NewText)
{
    public int LengthDelta => Math.Max(0, NewText?.Length ?? 0) - Math.Max(0, EndOffset - StartOffset);
}