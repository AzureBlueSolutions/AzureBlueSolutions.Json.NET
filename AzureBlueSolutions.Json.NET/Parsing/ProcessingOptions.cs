namespace AzureBlueSolutions.Json.NET;

/// <summary>
///     High-level configuration for orchestrating strict, tolerant, or dual-mode parsing.
/// </summary>
public sealed record ProcessingOptions
{
    /// <summary>
    ///     Determines which parsing mode to run: strict, tolerant, or both.
    /// </summary>
    public ParserMode Mode { get; init; } = ParserMode.Both;

    /// <summary>
    ///     Chooses which result to prefer when both modes are run.
    /// </summary>
    public ParsePriority Priority { get; init; } = ParsePriority.CorrectnessFirst;

    /// <summary>
    ///     Parse options used when running in strict mode.
    /// </summary>
    public ParseOptions Strict { get; init; } = Profiles.Strict();

    /// <summary>
    ///     Parse options used when running in tolerant mode.
    /// </summary>
    public ParseOptions Tolerant { get; init; } = Profiles.Tolerant();
}