namespace AzureBlueSolutions.Json.NET;

/// <summary>
/// High-level orchestration options for strict/tolerant/both
/// </summary>
public sealed record ProcessingOptions
{
    public ParserMode Mode { get; init; } = ParserMode.Both;
    public ParsePriority Priority { get; init; } = ParsePriority.CorrectnessFirst;

    public ParseOptions Strict { get; init; } = Profiles.Strict();
    public ParseOptions Tolerant { get; init; } = Profiles.Tolerant();
}