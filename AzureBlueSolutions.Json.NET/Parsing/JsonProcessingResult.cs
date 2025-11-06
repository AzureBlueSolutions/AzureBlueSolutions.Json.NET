namespace AzureBlueSolutions.Json.NET;

/// <summary>
/// Combined result when running strict, tolerant, or both.
/// </summary>
public sealed record JsonProcessingResult
{
    public ParserMode ModeUsed { get; init; }
    public ParsePriority PriorityUsed { get; init; }

    public JsonParseResult? StrictResult { get; init; }
    public JsonParseResult? TolerantResult { get; init; }

    public JsonParseResult SelectedResult { get; init; } = new JsonParseResult();
    public bool SelectedIsStrict { get; init; }
}