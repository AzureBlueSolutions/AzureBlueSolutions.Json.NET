namespace AzureBlueSolutions.Json.NET;

/// <summary>
/// Console color options for diagnostic output.
/// </summary>
public sealed record DiagnosticColorOptions
{
    public bool EnableColor { get; init; } = true;

    public ConsoleColor StageColor { get; init; } = ConsoleColor.DarkGray;
    public ConsoleColor SeverityInfoColor { get; init; } = ConsoleColor.Cyan;
    public ConsoleColor SeverityWarningColor { get; init; } = ConsoleColor.Yellow;
    public ConsoleColor SeverityErrorColor { get; init; } = ConsoleColor.Red;

    public ConsoleColor CodeColor { get; init; } = ConsoleColor.Blue;
    public ConsoleColor MessageColor { get; init; } = ConsoleColor.White;
    public ConsoleColor PathColor { get; init; } = ConsoleColor.Magenta;
    public ConsoleColor LocationColor { get; init; } = ConsoleColor.Gray;

    public ConsoleColor SnippetTextColor { get; init; } = ConsoleColor.Gray;
    public ConsoleColor CaretColor { get; init; } = ConsoleColor.Red;

    public static DiagnosticColorOptions Default => new DiagnosticColorOptions();
}