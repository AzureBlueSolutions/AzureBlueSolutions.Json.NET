namespace AzureBlueSolutions.Json.NET;

/// <summary>
///     Concrete sanitization report attached to <see cref="JsonParseResult.Report" />.
/// </summary>
/// <remarks>
///     The <see cref="Stage" /> identifies which pass produced the report:
///     <c>"Initial"</c>, <c>"Sanitized"</c>, or <c>"Aggressive"</c>.
/// </remarks>
public sealed record JsonSanitizationReport : SanitizationReport
{
    /// <summary>
    ///     Which pass produced this report: <c>"Initial"</c>, <c>"Sanitized"</c>, or <c>"Aggressive"</c>.
    /// </summary>
    public string Stage { get; init; } = "Initial";
}