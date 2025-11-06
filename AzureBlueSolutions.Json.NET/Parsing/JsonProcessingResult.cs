namespace AzureBlueSolutions.Json.NET;

/// <summary>
/// Represents the combined outcome when parsing in multiple modes
/// (Strict, Tolerant, or Both) and the policy‑selected result.
/// </summary>
/// <remarks>
/// When <c>Both</c> is used, <see cref="SelectedResult"/> is chosen according
/// to the orchestration policy (e.g., correctness‑first or recovery‑first),
/// and <see cref="SelectedIsStrict"/> indicates which result was selected.
/// </remarks>
public sealed record JsonProcessingResult
{
    /// <summary>
    /// The parser mode that was actually executed for this run
    /// (e.g., <c>Strict</c>, <c>Tolerant</c>, or <c>Both</c>).
    /// </summary>
    public ParserMode ModeUsed { get; init; }

    /// <summary>
    /// The priority policy used when <see cref="ModeUsed"/> is <c>Both</c>
    /// (e.g., correctness‑first or recovery‑first).
    /// </summary>
    public ParsePriority PriorityUsed { get; init; }

    /// <summary>
    /// The result produced by running the strict parser, when applicable;
    /// <c>null</c> when strict parsing was not executed.
    /// </summary>
    public JsonParseResult? StrictResult { get; init; }

    /// <summary>
    /// The result produced by running the tolerant parser, when applicable;
    /// <c>null</c> when tolerant parsing was not executed.
    /// </summary>
    public JsonParseResult? TolerantResult { get; init; }

    /// <summary>
    /// The result selected according to <see cref="PriorityUsed"/> and the
    /// availability/success of <see cref="StrictResult"/> and <see cref="TolerantResult"/>.
    /// </summary>
    public JsonParseResult SelectedResult { get; init; } = new();

    /// <summary>
    /// Indicates whether <see cref="SelectedResult"/> is the strict result (<c>true</c>)
    /// or the tolerant result (<c>false</c>).
    /// </summary>
    public bool SelectedIsStrict { get; init; }
}