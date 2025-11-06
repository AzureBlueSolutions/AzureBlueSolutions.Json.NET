namespace AzureBlueSolutions.Json.NET;

/// <summary>
/// A single parse diagnostic (error or warning).
/// </summary>
public sealed class JsonParseError
{
    /// <summary>
    /// Overrideable code string (e.g., "E002").
    /// </summary>
    public string Code { get; init; } = string.Empty;

    /// <summary>
    /// Severity of the diagnostic.
    /// </summary>
    public ErrorSeverity Severity { get; init; } = ErrorSeverity.Error;

    /// <summary>
    /// Human-readable message.
    /// </summary>
    public string Message { get; init; } = string.Empty;

    /// <summary>
    /// 1-based line number if available.
    /// </summary>
    public int? LineNumber { get; init; }

    /// <summary>
    /// 1-based column position if available.
    /// </summary>
    public int? LinePosition { get; init; }

    /// <summary>
    /// JSON path if available.
    /// </summary>
    public string? Path { get; init; }

    /// <summary>
    /// Stage label (Initial, Sanitized, Aggressive).
    /// </summary>
    public string Stage { get; init; } = "Initial";

    /// <summary>
    /// A short context snippet with a caret pointing to the position.
    /// </summary>
    public string? Snippet { get; init; }

    /// <summary>
    /// Zero-based range for LSP squiggles if available.
    /// </summary>
    public TextRange? Range { get; init; }

    public override string ToString()
    {
        var where = (LineNumber.HasValue && LinePosition.HasValue)
            ? $"(Line {LineNumber}, Position {LinePosition})"
            : string.Empty;

        return $"{Stage} [{Severity}] {Code}: {Message} {where} Path='{Path}'"
               + (string.IsNullOrWhiteSpace(Snippet) ? string.Empty : $"\n{Snippet}");
    }
}