namespace AzureBlueSolutions.Json.NET;

/// <summary>
/// High-level helper to apply edits and refresh tokens/path map.
/// </summary>
public static class JsonIncremental
{
    /// <summary>
    /// Applies edits, re-tokenizes a window, shifts path ranges conservatively, and returns updated artifacts.
    /// </summary>
    public static (string text, IReadOnlyList<JsonTokenSpan> tokens, IReadOnlyDictionary<string, JsonPathRange>
        pathRanges)
        ApplyChanges(
            string oldText,
            IReadOnlyList<JsonTokenSpan> oldTokens,
            IReadOnlyDictionary<string, JsonPathRange> oldPathRanges,
            IReadOnlyList<TextChange> changes,
            int contextRadius = 256,
            CancellationToken cancellationToken = default)
    {
        var updated = IncrementalJsonTokenizer.Update(oldText, oldTokens, changes, contextRadius, cancellationToken);
        var newMap = PathRangeIncrementalUpdater.Update(oldPathRanges, changes, oldText, updated.Text);
        return (updated.Text, updated.TokenSpans, newMap);
    }

    /// <summary>
    /// Applies edits, re-tokenizes a window, and fully rebuilds the path map by reparsing (safe but heavier).
    /// </summary>
    public static (string text, IReadOnlyList<JsonTokenSpan> tokens, IReadOnlyDictionary<string, JsonPathRange>
        pathRanges, JsonParseResult parse)
        ApplyChangesWithReparse(
            string oldText,
            IReadOnlyList<JsonTokenSpan> oldTokens,
            IReadOnlyList<TextChange> changes,
            ParseOptions? options = null,
            int contextRadius = 256,
            CancellationToken cancellationToken = default)
    {
        var updated = IncrementalJsonTokenizer.Update(oldText, oldTokens, changes, contextRadius, cancellationToken);
        var parse = JsonParser.ParseSafe(updated.Text, options ?? Profiles.Tolerant(), cancellationToken);
        return (updated.Text, updated.TokenSpans, parse.PathRanges, parse);
    }
}