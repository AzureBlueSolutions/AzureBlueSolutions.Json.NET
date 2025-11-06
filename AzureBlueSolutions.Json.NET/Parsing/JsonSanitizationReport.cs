namespace AzureBlueSolutions.Json.NET;

/// <summary>
/// Concrete sanitization report attached to JsonParseResult.Report.
/// </summary>
public sealed record JsonSanitizationReport : SanitizationReport
{
    /// <summary>
    /// Which pass produced this report: "Initial", "Sanitized", or "Aggressive".
    /// </summary>
    public string Stage { get; init; } = "Initial";
}