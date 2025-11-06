using Newtonsoft.Json.Linq;

namespace AzureBlueSolutions.Json.NET;

public sealed record JsonPathRange
{
    public string Path { get; init; } = string.Empty;
    public TextRange? Name { get; init; }
    public TextRange? Value { get; init; }
}

/// <summary>
/// Ranges associated with a JSON path: one for the property name, one for the value.
/// </summary>
internal static class JsonPathMapper
{
    /// <summary>
    /// Builds a map of JSON paths to token ranges, honoring cancellation.
    /// </summary>
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

            int zl = info.Value.line - 1;
            int zc = info.Value.position - 1;

            if (node is JProperty prop)
            {
                TextRange? valueRange = null;
                JsonTokenSpan? valueToken = null;

                var vinfo = prop.Value.GetLineInfo();
                if (vinfo is not null)
                {
                    int vl = vinfo.Value.line - 1;
                    int vc = vinfo.Value.position - 1;
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
                    int afterNameOffset = nameSpan.Range.End.Offset;
                    int colonIdx = FindFirstTokenIndexStartingAtOrAfter(tokens, afterNameOffset, JsonLexemeKind.Colon);
                    if (colonIdx >= 0)
                    {
                        int i = colonIdx + 1;
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
                    int nameIndex = -1;
                    for (int i = 0; i < tokens.Count; i++)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        if (ReferenceEquals(tokens[i], nameSpan)) { nameIndex = i; break; }
                    }
                    if (nameIndex >= 0)
                    {
                        int j = nameIndex + 1;
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
                    int vIndex = -1;
                    for (int i = 0; i < tokens.Count; i++)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        if (ReferenceEquals(tokens[i], valueToken)) { vIndex = i; break; }
                    }
                    if (vIndex >= 0)
                    {
                        int i = vIndex - 1;
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
                            if (i >= 0 && tokens[i].Kind == JsonLexemeKind.String)
                            {
                                nameSpan ??= tokens[i];
                            }
                        }
                        else
                        {
                            int colonIdx = -1;
                            for (int j = vIndex - 1; j >= 0; j--)
                            {
                                cancellationToken.ThrowIfCancellationRequested();
                                if (tokens[j].Kind == JsonLexemeKind.Colon) { colonIdx = j; break; }
                            }
                            if (colonIdx >= 0)
                            {
                                int nameIdx = colonIdx - 1;
                                while (nameIdx >= 0 && tokens[nameIdx].Kind == JsonLexemeKind.Comment)
                                {
                                    cancellationToken.ThrowIfCancellationRequested();
                                    nameIdx--;
                                }
                                if (nameIdx >= 0 && tokens[nameIdx].Kind == JsonLexemeKind.String)
                                {
                                    nameSpan ??= tokens[nameIdx];
                                }
                            }
                        }
                    }
                }

                if (nameSpan is null || valueRange is null) continue;
                if (!result.TryGetValue(prop.Path, out var existing))
                {
                    existing = new JsonPathRange { Path = prop.Path };
                }
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
                if (!result.TryGetValue(node.Path, out var existing))
                {
                    existing = new JsonPathRange { Path = node.Path };
                }
                result[node.Path] = existing with
                {
                    Name = existing.Name,
                    Value = vspan.Range
                };
            }
        }

        return result;
    }

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
            bool afterStart = (line > s.Line) || (line == s.Line && col >= s.Column);
            bool beforeEnd = (line < e.Line) || (line == e.Line && col < e.Column);
            if (afterStart && beforeEnd)
            {
                span = t;
                return true;
            }
        }
        span = null!;
        return false;
    }

    private static int FindFirstTokenIndexStartingAtOrAfter(
        IReadOnlyList<JsonTokenSpan> tokens,
        int startOffset,
        JsonLexemeKind kindToFind)
    {
        for (int i = 0; i < tokens.Count; i++)
        {
            var t = tokens[i];
            if (t.Range.Start.Offset >= startOffset && t.Kind == kindToFind)
                return i;
        }
        return -1;
    }
}