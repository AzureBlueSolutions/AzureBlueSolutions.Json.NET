using Newtonsoft.Json.Linq;

namespace AzureBlueSolutions.Json.NET;

/// <summary>
/// Represents the source ranges associated with a single JSON path.
/// Includes the range for the property name (when applicable) and the range for the value.
/// </summary>
public sealed record JsonPathRange
{
    /// <summary>
    /// The JSON path this entry corresponds to.
    /// </summary>
    public string Path { get; init; } = string.Empty;

    /// <summary>
    /// The source range of the property name associated with <see cref="Path"/>, if applicable.
    /// </summary>
    public TextRange? Name { get; init; }

    /// <summary>
    /// The source range of the value associated with <see cref="Path"/>, if applicable.
    /// </summary>
    public TextRange? Value { get; init; }
}

/// <summary>
/// Builds a mapping between JSON paths and their corresponding source ranges
/// (property name and value), using token information for precise locations.
/// </summary>
internal static class JsonPathMapper
{
    /// <summary>
    /// Builds a map of JSON paths to token ranges, honoring cancellation.
    /// </summary>
    /// <param name="root">
    /// The root <see cref="JToken"/> of the parsed JSON document.
    /// </param>
    /// <param name="tokens">
    /// The token stream produced by lexing the document.
    /// </param>
    /// <param name="cancellationToken">
    /// A token to observe for cancellation.
    /// </param>
    /// <returns>
    /// A dictionary mapping each JSON path to a <see cref="JsonPathRange"/> containing
    /// the name and value ranges where applicable.
    /// </returns>
    public static IReadOnlyDictionary<string, JsonPathRange> Build(
        JToken root,
        IReadOnlyList<JsonTokenSpan> tokens,
        CancellationToken cancellationToken = default)
    {
        var result = new Dictionary<string, JsonPathRange>();
        if (tokens.Count == 0) return result;

        var startIndex = BuildStartIndex(tokens);

        foreach (var node in Traverse(root, cancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested();

            var info = node.GetLineInfo();
            if (info is null) continue;

            var zl = info.Value.line - 1;
            var zc = info.Value.position - 1;

            if (node is JProperty prop)
            {
                TextRange? valueRange = null;
                JsonTokenSpan? valueToken = null;

                var vinfo = prop.Value.GetLineInfo();
                if (vinfo is not null)
                {
                    var vl = vinfo.Value.line - 1;
                    var vc = vinfo.Value.position - 1;
                    if (TryFindToken(startIndex, tokens, vl, vc, out var vspan))
                    {
                        valueToken = vspan;
                        valueRange = vspan.Range;
                    }
                }

                JsonTokenSpan? nameSpan = null;

                if (valueRange is null && TryFindToken(startIndex, tokens, zl, zc, out var possibleName)
                                       && possibleName.Kind == JsonLexemeKind.String)
                {
                    nameSpan = possibleName;

                    var afterNameOffset = nameSpan.Range.End.Offset;
                    var colonIdx = FindFirstTokenIndexStartingAtOrAfter(tokens, afterNameOffset, JsonLexemeKind.Colon);
                    if (colonIdx >= 0)
                    {
                        var i = colonIdx + 1;
                        while (i < tokens.Count && tokens[i].Kind == JsonLexemeKind.Comment)
                        {
                            cancellationToken.ThrowIfCancellationRequested();
                            i++;
                        }

                        if (i < tokens.Count)
                        {
                            valueToken = tokens[i];
                            valueRange = tokens[i].Range;
                        }
                    }
                }

                if (nameSpan is not null && valueRange is null)
                {
                    var nameIndex = -1;
                    for (var i = 0; i < tokens.Count; i++)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        if (ReferenceEquals(tokens[i], nameSpan))
                        {
                            nameIndex = i;
                            break;
                        }
                    }

                    if (nameIndex >= 0)
                    {
                        var j = nameIndex + 1;
                        while (j < tokens.Count && tokens[j].Kind != JsonLexemeKind.Colon)
                        {
                            cancellationToken.ThrowIfCancellationRequested();
                            j++;
                        }

                        if (j < tokens.Count && tokens[j].Kind == JsonLexemeKind.Colon) j++;

                        while (j < tokens.Count && tokens[j].Kind == JsonLexemeKind.Comment)
                        {
                            cancellationToken.ThrowIfCancellationRequested();
                            j++;
                        }

                        if (j < tokens.Count)
                        {
                            valueToken = tokens[j];
                            valueRange = tokens[j].Range;
                        }
                    }
                }

                if (valueToken is not null)
                {
                    var vIndex = -1;
                    for (var i = 0; i < tokens.Count; i++)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        if (ReferenceEquals(tokens[i], valueToken))
                        {
                            vIndex = i;
                            break;
                        }
                    }

                    if (vIndex >= 0)
                    {
                        var i = vIndex - 1;
                        while (i >= 0 && tokens[i].Kind == JsonLexemeKind.Comment)
                        {
                            cancellationToken.ThrowIfCancellationRequested();
                            i--;
                        }

                        if (i >= 0 && tokens[i].Kind == JsonLexemeKind.Colon)
                        {
                            i--;
                            while (i >= 0 && tokens[i].Kind == JsonLexemeKind.Comment)
                            {
                                cancellationToken.ThrowIfCancellationRequested();
                                i--;
                            }

                            if (i >= 0 && tokens[i].Kind == JsonLexemeKind.String) nameSpan ??= tokens[i];
                        }
                        else
                        {
                            var colonIdx = -1;
                            for (var j = vIndex - 1; j >= 0; j--)
                            {
                                cancellationToken.ThrowIfCancellationRequested();
                                if (tokens[j].Kind == JsonLexemeKind.Colon)
                                {
                                    colonIdx = j;
                                    break;
                                }
                            }

                            if (colonIdx >= 0)
                            {
                                var nameIdx = colonIdx - 1;
                                while (nameIdx >= 0 && tokens[nameIdx].Kind == JsonLexemeKind.Comment)
                                {
                                    cancellationToken.ThrowIfCancellationRequested();
                                    nameIdx--;
                                }

                                if (nameIdx >= 0 && tokens[nameIdx].Kind == JsonLexemeKind.String)
                                    nameSpan ??= tokens[nameIdx];
                            }
                        }
                    }
                }

                if (nameSpan is null || valueRange is null) continue;

                if (!result.TryGetValue(prop.Path, out var existing)) existing = new JsonPathRange { Path = prop.Path };

                var combined = existing with
                {
                    Name = nameSpan.Range,
                    Value = valueRange
                };

                result[prop.Path] = combined;
            }
            else if (node is JValue)
            {
                if (!TryFindToken(startIndex, tokens, zl, zc, out var vspan)) continue;

                if (!result.TryGetValue(node.Path, out var existing)) existing = new JsonPathRange { Path = node.Path };

                result[node.Path] = existing with
                {
                    Name = existing.Name,
                    Value = vspan.Range
                };
            }
        }

        return result;
    }

    /// <summary>
    /// Traverses the token tree in a non-recursive depth-first manner, yielding nodes.
    /// </summary>
    /// <param name="root">The root node to traverse.</param>
    /// <param name="cancellationToken">A token to observe for cancellation.</param>
    private static IEnumerable<JToken> Traverse(JToken root, CancellationToken cancellationToken)
    {
        var stack = new Stack<JToken>();
        stack.Push(root);

        while (stack.Count > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var current = stack.Pop();
            yield return current;

            foreach (var child in current.Children())
            {
                cancellationToken.ThrowIfCancellationRequested();
                stack.Push(child);
            }
        }
    }

    /// <summary>
    /// Builds a lookup from (line, column) to token for fast start-position searches.
    /// </summary>
    /// <param name="tokens">The token stream.</param>
    /// <returns>A dictionary keyed by start (line, column) with token values.</returns>
    private static Dictionary<(int line, int col), JsonTokenSpan> BuildStartIndex(IReadOnlyList<JsonTokenSpan> tokens)
    {
        var map = new Dictionary<(int, int), JsonTokenSpan>();

        foreach (var t in tokens)
        {
            var key = (t.Range.Start.Line, t.Range.Start.Column);
            map.TryAdd(key, t);
        }

        return map;
    }

    /// <summary>
    /// Attempts to find the token that starts at or contains the specified (line, column).
    /// </summary>
    /// <param name="index">The start-position index built by <see cref="BuildStartIndex"/>.</param>
    /// <param name="tokens">The full token stream.</param>
    /// <param name="line">Zero-based line number.</param>
    /// <param name="col">Zero-based column number.</param>
    /// <param name="span">When successful, receives the located token.</param>
    /// <returns><c>true</c> if a token is found; otherwise, <c>false</c>.</returns>
    private static bool TryFindToken(
        Dictionary<(int line, int col), JsonTokenSpan> index,
        IReadOnlyList<JsonTokenSpan> tokens,
        int line,
        int col,
        out JsonTokenSpan span)
    {
        if (index.TryGetValue((line, col), out span!))
            return true;

        foreach (var t in tokens)
        {
            var s = t.Range.Start;
            var e = t.Range.End;

            var afterStart = line > s.Line ||
                             (line == s.Line && col >= s.Column);

            var beforeEnd = line < e.Line ||
                            (line == e.Line && col < e.Column);

            if (afterStart && beforeEnd)
            {
                span = t;
                return true;
            }
        }

        span = null!;
        return false;
    }

    /// <summary>
    /// Finds the first token index whose start offset is at or after <paramref name="startOffset"/>
    /// and whose kind matches <paramref name="kindToFind"/>.
    /// </summary>
    /// <param name="tokens">The token stream.</param>
    /// <param name="startOffset">The minimum start offset (inclusive).</param>
    /// <param name="kindToFind">The token kind to locate.</param>
    /// <returns>The zero-based token index, or <c>-1</c> if not found.</returns>
    private static int FindFirstTokenIndexStartingAtOrAfter(
        IReadOnlyList<JsonTokenSpan> tokens,
        int startOffset,
        JsonLexemeKind kindToFind)
    {
        for (var i = 0; i < tokens.Count; i++)
        {
            var t = tokens[i];
            if (t.Range.Start.Offset >= startOffset && t.Kind == kindToFind)
                return i;
        }
        return -1;
    }
}