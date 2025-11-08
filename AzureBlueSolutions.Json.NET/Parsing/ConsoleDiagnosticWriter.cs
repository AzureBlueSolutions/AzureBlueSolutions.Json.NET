namespace AzureBlueSolutions.Json.NET;

/// <summary>
///     Writes parse diagnostics to the console with optional ANSI colorization,
///     mirroring the structure used by other components in the package.
/// </summary>
public static class ConsoleDiagnosticWriter
{
    /// <summary>
    ///     Writes a sequence of diagnostics to the console.
    /// </summary>
    /// <param name="errors">
    ///     The diagnostics to write. If <c>null</c>, nothing is written.
    /// </param>
    /// <param name="options">
    ///     Console color options. When <c>null</c>, <see cref="DiagnosticColorOptions.Default" /> is used.
    /// </param>
    /// <param name="indentSpaces">
    ///     Number of leading spaces to indent each line. Must be non‑negative.
    /// </param>
    public static void WriteErrors(IEnumerable<JsonParseError>? errors, DiagnosticColorOptions? options = null,
        int indentSpaces = 2)
    {
        if (errors is null) return;
        foreach (var error in errors) Write(error, options, indentSpaces);
    }

    /// <summary>
    ///     Writes a single diagnostic to the console, including stage, severity, code,
    ///     message, optional location/path, and an optional two‑line snippet with a caret.
    /// </summary>
    /// <param name="error">
    ///     The diagnostic to write.
    /// </param>
    /// <param name="options">
    ///     Console color options. When <c>null</c>, <see cref="DiagnosticColorOptions.Default" /> is used.
    /// </param>
    /// <param name="indentSpaces">
    ///     Number of leading spaces to indent each line. Must be non‑negative.
    /// </param>
    public static void Write(JsonParseError error, DiagnosticColorOptions? options = null, int indentSpaces = 2)
    {
        options ??= DiagnosticColorOptions.Default;
        var indent = new string(' ', Math.Max(0, indentSpaces));

        var previousColor = Console.ForegroundColor;
        try
        {
            Console.Write(indent);

            // Stage
            WriteColored(options, options.StageColor, error.Stage);
            Console.Write(' ');

            // [Severity]
            Console.Write('[');
            var sevColor = error.Severity switch
            {
                ErrorSeverity.Info => options.SeverityInfoColor,
                ErrorSeverity.Warning => options.SeverityWarningColor,
                _ => options.SeverityErrorColor
            };
            WriteColored(options, sevColor, error.Severity.ToString());
            Console.Write(']');

            Console.Write(' ');

            // Code
            WriteColored(options, options.CodeColor, error.Code);
            Console.Write(": ");

            // Message
            WriteColored(options, options.MessageColor, error.Message);

            // Location
            if (error.LineNumber is not null && error.LinePosition is not null)
            {
                Console.Write(' ');
                WriteColored(options, options.LocationColor,
                    $"(Line {error.LineNumber}, Position {error.LinePosition})");
            }

            // Path
            if (!string.IsNullOrEmpty(error.Path))
            {
                Console.Write(' ');
                WriteColored(options, options.PathColor, $"Path='{error.Path}'");
            }

            Console.WriteLine();

            // Snippet (render both text and caret through the same pipeline irrespective of color)
            if (string.IsNullOrWhiteSpace(error.Snippet)) return;

            var parts = SplitSnippet(error.Snippet!);

            if (parts.textLine is not null)
            {
                Console.Write(indent);
                // Always route through colored writer for uniform codepath;
                // color is internally disabled when options.EnableColor == false
                WriteColored(options, options.SnippetTextColor, parts.textLine);
                Console.WriteLine();
            }

            if (parts.caretLine is null) return;

            Console.Write(indent);
            // Even in non-color mode, iterate per-char to preserve tabs/spaces identically
            WriteCaretLine(options, parts.caretLine, options.SnippetTextColor, options.CaretColor);
            Console.WriteLine();
        }
        finally
        {
            Console.ForegroundColor = previousColor;
        }
    }

    /// <summary>
    ///     Writes <paramref name="value" /> to the console using <paramref name="color" />
    ///     when <see cref="DiagnosticColorOptions.EnableColor" /> is <c>true</c>; otherwise writes without color.
    /// </summary>
    /// <param name="options">
    ///     Color configuration determining whether color is enabled and which colors to use.
    /// </param>
    /// <param name="color">
    ///     The foreground color to apply when color is enabled.
    /// </param>
    /// <param name="value">
    ///     The text to write.
    /// </param>
    private static void WriteColored(DiagnosticColorOptions options, ConsoleColor color, string value)
    {
        if (!options.EnableColor)
        {
            // Unified path: still a single call site, we just don't change color
            Console.Write(value);
            return;
        }

        var previous = Console.ForegroundColor;
        Console.ForegroundColor = color;
        Console.Write(value);
        Console.ForegroundColor = previous;
    }

    /// <summary>
    ///     Writes the caret line for a snippet character‑by‑character, preserving spacing and applying
    ///     <paramref name="caretColor" /> to '^' characters and <paramref name="spaceColor" /> to everything else.
    /// </summary>
    /// <param name="options">
    ///     Color configuration determining whether color is enabled and which colors to use.
    /// </param>
    /// <param name="caretLine">
    ///     The caret line to write (typically consists of spaces/tabs and one or more '^' characters).
    /// </param>
    /// <param name="spaceColor">
    ///     The color to use for non‑caret characters (e.g., spaces/tabs).
    /// </param>
    /// <param name="caretColor">
    ///     The color to use for caret characters ('^').
    /// </param>
    private static void WriteCaretLine(DiagnosticColorOptions options, string caretLine, ConsoleColor spaceColor,
        ConsoleColor caretColor)
    {
        // Character-wise emission so tabs/spaces are preserved exactly.
        // If color is off, we still keep identical logic—just no color swaps.
        var previous = Console.ForegroundColor;

        if (!options.EnableColor)
        {
            // Non-color: still iterate; identical spacing behavior
            foreach (var c in caretLine) Console.Write(c);
            return;
        }

        foreach (var c in caretLine)
            if (c == '^')
            {
                Console.ForegroundColor = caretColor;
                Console.Write('^');
                Console.ForegroundColor = spaceColor;
            }
            else
            {
                Console.ForegroundColor = spaceColor;
                Console.Write(c);
            }

        Console.ForegroundColor = previous;
    }

    /// <summary>
    ///     Splits a two‑line snippet into its content line and caret line.
    /// </summary>
    /// <param name="snippet">
    ///     The snippet text. If it does not contain a newline, it is treated as a single text line with no caret line.
    /// </param>
    /// <returns>
    ///     A tuple of <c>(textLine, caretLine)</c>. <c>caretLine</c> may be <c>null</c> when not present.
    /// </returns>
    private static (string? textLine, string? caretLine) SplitSnippet(string snippet)
    {
        var idx = snippet.IndexOf('\n');
        if (idx < 0) return (snippet, null);

        var textLine = snippet[..idx];
        var caretLine = snippet[(idx + 1)..];
        return (textLine, caretLine);
    }
}