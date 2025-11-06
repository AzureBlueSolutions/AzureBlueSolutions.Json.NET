namespace AzureBlueSolutions.Json.NET
{
    /// <summary>
    /// Provides incremental operations for applying text edits to JSON documents
    /// and updating tokens and path ranges with minimal recomputation.
    /// </summary>
    public static class JsonIncremental
    {
        /// <summary>
        /// Applies a set of text changes, performs windowed re-tokenization,
        /// and incrementally updates the JSON path-to-range map.
        /// </summary>
        /// <param name="oldText">
        /// The original document text prior to applying <paramref name="changes"/>.
        /// </param>
        /// <param name="oldTokens">
        /// The existing token spans corresponding to <paramref name="oldText"/>.
        /// </param>
        /// <param name="oldPathRanges">
        /// The existing map from JSON paths to their source ranges for <paramref name="oldText"/>.
        /// </param>
        /// <param name="changes">
        /// The list of edits to apply (zero-based offsets, end exclusive).
        /// </param>
        /// <param name="contextRadius">
        /// The number of characters to extend before and after the edited region to stabilize tokenization.
        /// </param>
        /// <param name="cancellationToken">
        /// A token that can be used to cancel the operation.
        /// </param>
        /// <returns>
        /// A tuple containing:
        /// <br/>
        /// <c>text</c> — the updated full document text;<br/>
        /// <c>tokens</c> — the updated list of token spans;<br/>
        /// <c>pathRanges</c> — the incrementally updated JSON path-to-range map.
        /// </returns>
        public static (string text, IReadOnlyList<JsonTokenSpan> tokens, IReadOnlyDictionary<string, JsonPathRange> pathRanges)
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
        /// Applies text edits, re-tokenizes a minimal window, and then reparses the
        /// entire document to rebuild the path map. This is safer but heavier than
        /// incremental path map updates.
        /// </summary>
        /// <param name="oldText">
        /// The original document text prior to applying <paramref name="changes"/>.
        /// </param>
        /// <param name="oldTokens">
        /// The existing token spans corresponding to <paramref name="oldText"/>.
        /// </param>
        /// <param name="changes">
        /// The list of edits to apply (zero-based offsets, end exclusive).
        /// </param>
        /// <param name="options">
        /// Optional parse options; if not provided, a tolerant profile is used.
        /// </param>
        /// <param name="contextRadius">
        /// The number of characters to extend before and after the edited region to stabilize tokenization.
        /// </param>
        /// <param name="cancellationToken">
        /// A token that can be used to cancel the operation.
        /// </param>
        /// <returns>
        /// A tuple containing:
        /// <br/>
        /// <c>text</c> — the updated full document text;<br/>
        /// <c>tokens</c> — the updated list of token spans;<br/>
        /// <c>pathRanges</c> — the rebuilt JSON path-to-range map;<br/>
        /// <c>parse</c> — the full parse result produced after reparsing.
        /// </returns>
        public static (string text, IReadOnlyList<JsonTokenSpan> tokens, IReadOnlyDictionary<string, JsonPathRange> pathRanges, JsonParseResult parse)
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
}
