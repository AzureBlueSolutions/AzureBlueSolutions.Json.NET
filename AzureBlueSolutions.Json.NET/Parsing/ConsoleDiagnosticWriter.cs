namespace AzureBlueSolutions.Json.NET;

/// <summary>
/// Writes diagnostics to the console with optional colorization.
/// Non-color mode now mirrors the exact logic/spacing of the color mode.
/// </summary>
public static class ConsoleDiagnosticWriter
{
    public static void WriteErrors(IEnumerable<JsonParseError>? errors, DiagnosticColorOptions? options = null, int indentSpaces = 2)
    {
        if (errors is null) return;
        foreach (var error in errors) Write(error, options, indentSpaces);
    }

    public static void Write(JsonParseError error, DiagnosticColorOptions? options = null, int indentSpaces = 2)
    {
        options ??= DiagnosticColorOptions.Default;

        string indent = new string(' ', Math.Max(0, indentSpaces));
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
                WriteColored(options, options.LocationColor, $"(Line {error.LineNumber}, Position {error.LinePosition})");
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

    private static void WriteCaretLine(DiagnosticColorOptions options, string caretLine, ConsoleColor spaceColor, ConsoleColor caretColor)
    {
        // Character-wise emission so tabs/spaces are preserved exactly.
        // If color is off, we still keep identical logic—just no color swaps.
        var previous = Console.ForegroundColor;

        if (!options.EnableColor)
        {
            // Non-color: still iterate; identical spacing behavior
            foreach (char c in caretLine)
            {
                Console.Write(c);
            }

            return;
        }

        foreach (char c in caretLine)
        {
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
        }

        Console.ForegroundColor = previous;
    }

    private static (string? textLine, string? caretLine) SplitSnippet(string snippet)
    {
        int idx = snippet.IndexOf('\n');
        if (idx < 0) return (snippet, null);
        string textLine = snippet.Substring(0, idx);
        string caretLine = snippet[(idx + 1)..];
        return (textLine, caretLine);
    }
}