namespace AzureBlueSolutions.Json.NET;

/// <summary>
///     Specifies which parsing strategy to use.
/// </summary>
public enum ParserMode
{
    /// <summary>
    ///     Use strict parsing only.
    /// </summary>
    Strict,

    /// <summary>
    ///     Use tolerant parsing only.
    /// </summary>
    Tolerant,

    /// <summary>
    ///     Run both strict and tolerant parsing.
    /// </summary>
    Both
}

/// <summary>
///     Determines which result to prioritize when both modes are used.
/// </summary>
public enum ParsePriority
{
    /// <summary>
    ///     Prefer the most correct result.
    /// </summary>
    CorrectnessFirst,

    /// <summary>
    ///     Prefer the most recoverable result.
    /// </summary>
    RecoveryFirst
}