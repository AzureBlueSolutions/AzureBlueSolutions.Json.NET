namespace AzureBlueSolutions.Json.NET;

/// <summary>
///     Severity assigned to parse diagnostics.
/// </summary>
public enum ErrorSeverity
{
    /// <summary>
    ///     Informational message.
    /// </summary>
    Info,

    /// <summary>
    ///     Potential issue or warning.
    /// </summary>
    Warning,

    /// <summary>
    ///     Parsing error.
    /// </summary>
    Error
}