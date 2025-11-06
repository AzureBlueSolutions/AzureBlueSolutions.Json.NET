namespace AzureBlueSolutions.Json.NET;

/// <summary>
/// A single parse diagnostic (error, warning, or informational note).
/// </summary>
/// <remarks>
/// Codes are typically resolved from <see cref="ErrorKey"/> via
/// <see cref="DefaultErrorCodes.Resolve(ErrorKey)"/>, but may be customized
/// by supplying a delegate in <c>ParseOptions.ResolveErrorCode</c>.
/// Line and column values are 1‑based to match Newtonsoft.Json conventions,
/// while <see cref="Range"/> uses zero‑based LSP coordinates.
/// </remarks>
public sealed class JsonParseError
{
    /// <summary>
    /// Overrideable code string (e.g., <c>"E002"</c>).
    /// </summary>
    public string Code { get; init; } = string.Empty;

    /// <summary>
    /// Severity of the diagnostic.
    /// </summary>
    public ErrorSeverity Severity { get; init; } = ErrorSeverity.Error;

    /// <summary>
    /// Human‑readable message.
    /// </summary>
    public string Message { get; init; } = string.Empty;

    /// <summary>
    /// 1‑based line number if available.
    /// </summary>
    public int? LineNumber { get; init; }

    /// <summary>
    /// 1‑based column position if available.
    /// </summary>
    public int? LinePosition { get; init; }

    /// <summary>
    /// JSON path if available.
    /// </summary>
    public string? Path { get; init; }

    /// <summary>
    /// Stage label that produced the diagnostic (e.g., <c>"Initial"</c>, <c>"Sanitized"</c>, <c>"Aggressive"</c>).
    /// </summary>
    public string Stage { get; init; } = "Initial";

    /// <summary>
    /// A short context snippet with a caret line pointing at the position (when available).
    /// </summary>
    public string? Snippet { get; init; }

    /// <summary>
    /// Zero‑based range for LSP squiggles if available.
    /// </summary>
    public TextRange? Range { get; init; }

    /// <summary>
    /// Returns a concise, single‑line representation of the diagnostic,
    /// optionally followed by a newline and the snippet text.
    /// </summary>
    public override string ToString()
    {
        var where = LineNumber.HasValue && LinePosition.HasValue
            ? $"(Line {LineNumber}, Position {LinePosition})"
            : string.Empty;

        return $"{Stage} [{Severity}] {Code}: {Message} {where} Path='{Path}'"
             + (string.IsNullOrWhiteSpace(Snippet) ? string.Empty : $"\n{Snippet}");
    }
}