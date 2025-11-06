namespace AzureBlueSolutions.Json.NET;

/// <summary>
/// Console color options for diagnostic output.
/// </summary>
/// <remarks>
/// When <see cref="EnableColor"/> is <c>false</c>, all text is written using the console's
/// current foreground color and no color changes are applied.
/// </remarks>
public sealed record DiagnosticColorOptions
{
    /// <summary>
    /// Enables colorized output. When <c>false</c>, values are written without changing
    /// <see cref="Console.ForegroundColor"/>.
    /// </summary>
    public bool EnableColor { get; init; } = true;

    /// <summary>
    /// Color for the diagnostic stage label (e.g., <c>Initial</c>, <c>Sanitized</c>, <c>Aggressive</c>).
    /// </summary>
    public ConsoleColor StageColor { get; init; } = ConsoleColor.DarkGray;

    /// <summary>
    /// Color for informational severity labels.
    /// </summary>
    public ConsoleColor SeverityInfoColor { get; init; } = ConsoleColor.Cyan;

    /// <summary>
    /// Color for warning severity labels.
    /// </summary>
    public ConsoleColor SeverityWarningColor { get; init; } = ConsoleColor.Yellow;

    /// <summary>
    /// Color for error severity labels.
    /// </summary>
    public ConsoleColor SeverityErrorColor { get; init; } = ConsoleColor.Red;

    /// <summary>
    /// Color for diagnostic codes (e.g., <c>E002</c>, <c>W101</c>).
    /// </summary>
    public ConsoleColor CodeColor { get; init; } = ConsoleColor.Blue;

    /// <summary>
    /// Color for the main diagnostic message text.
    /// </summary>
    public ConsoleColor MessageColor { get; init; } = ConsoleColor.White;

    /// <summary>
    /// Color for the JSON path segment (when present).
    /// </summary>
    public ConsoleColor PathColor { get; init; } = ConsoleColor.Magenta;

    /// <summary>
    /// Color for the source location tuple (line and position).
    /// </summary>
    public ConsoleColor LocationColor { get; init; } = ConsoleColor.Gray;

    /// <summary>
    /// Color for the first line of a snippet (the text line).
    /// </summary>
    public ConsoleColor SnippetTextColor { get; init; } = ConsoleColor.Gray;

    /// <summary>
    /// Color for caret characters (<c>^</c>) in the second snippet line.
    /// </summary>
    public ConsoleColor CaretColor { get; init; } = ConsoleColor.Red;

    /// <summary>
    /// Provides a convenient default instance with sensible colors and <see cref="EnableColor"/> = <c>true</c>.
    /// </summary>
    public static DiagnosticColorOptions Default => new();
}