namespace AzureBlueSolutions.Json.NET;

/// <summary>
/// Shifts existing path ranges after text edits and invalidates overlaps.
/// </summary>
public static class PathRangeIncrementalUpdater
{
    /// <summary>
    /// Returns a new map where entries that intersect any change are removed,
    /// and all positions strictly after edits are shifted for line/column/offset.
    /// </summary>
    public static IReadOnlyDictionary<string, JsonPathRange> Update(
        IReadOnlyDictionary<string, JsonPathRange> oldMap,
        IReadOnlyList<TextChange> changes,
        string oldText,
        string newText)
    {
        if (oldMap.Count == 0 || changes == null || changes.Count == 0) return oldMap;

        var ordered = changes.OrderBy(c => c.StartOffset).ToArray();
        var deltas = ComputeAppliedDeltas(ordered, oldText);

        var updated = new Dictionary<string, JsonPathRange>(oldMap.Count, StringComparer.Ordinal);

        foreach (var kv in oldMap)
        {
            var name = kv.Value.Name;
            var val = kv.Value.Value;

            if ((name is not null && IntersectsAny(name, ordered)) ||
                (val is not null && IntersectsAny(val, ordered)))
            {
                continue;
            }

            var newName = name is null ? null : ApplyDeltas(name, deltas);
            var newVal = val is null ? null : ApplyDeltas(val, deltas);

            updated[kv.Key] = kv.Value with { Name = newName, Value = newVal };
        }

        return updated;
    }

    private static bool IntersectsAny(TextRange range, IReadOnlyList<TextChange> edits)
    {
        foreach (var c in edits)
        {
            var s = c.StartOffset;
            var e = c.EndOffset;
            if (range.Start.Offset < e && range.End.Offset > s) return true;
        }
        return false;
    }

    private static IReadOnlyList<Delta> ComputeAppliedDeltas(IReadOnlyList<TextChange> ordered, string oldText)
    {
        var result = new List<Delta>(ordered.Count);
        var shift = 0;
        foreach (var c in ordered)
        {
            var sOld = Math.Clamp(c.StartOffset, 0, oldText.Length);
            var eOld = Math.Clamp(c.EndOffset, 0, oldText.Length);
            var removed = oldText.Substring(sOld, Math.Max(0, eOld - sOld));
            var added = c.NewText ?? string.Empty;

            var removedNl = CountNewlines(removed);
            var addedNl = CountNewlines(added);
            var lineDelta = addedNl - removedNl;

            var removedTail = TailColumns(removed);
            var addedTail = TailColumns(added);
            var tailDelta = addedTail - removedTail;

            var delta = (added?.Length ?? 0) - (eOld - sOld);

            var startAfterPrev = sOld + shift;
            var endAfterPrev = startAfterPrev + (added?.Length ?? 0);

            result.Add(new Delta(startAfterPrev, endAfterPrev, delta, lineDelta, tailDelta));
            shift += delta;
        }
        return result;
    }

    private static TextRange ApplyDeltas(TextRange r, IReadOnlyList<Delta> deltas)
    {
        var a = r.Start;
        var b = r.End;

        foreach (var d in deltas)
        {
            if (a.Offset >= d.End)
                a = Shift(a, d);
            if (b.Offset >= d.End)
                b = Shift(b, d);
        }

        return new TextRange(a, b);
    }

    private static TextPosition Shift(TextPosition p, Delta d)
    {
        var newOffset = p.Offset + d.LengthDelta;
        var newLine = p.Line + d.LineDelta;
        var newCol = p.Column;

        if (d.LineDelta == 0)
        {
            newCol += d.TailColumnDelta;
            if (newCol < 0) newCol = 0;
        }

        return new TextPosition(newLine, newCol, newOffset);
    }

    private static int CountNewlines(string s)
    {
        if (string.IsNullOrEmpty(s)) return 0;
        var n = 0; foreach (var ch in s) if (ch == '\n') n++; return n;
    }

    private static int TailColumns(string s)
    {
        if (string.IsNullOrEmpty(s)) return 0;
        var i = s.LastIndexOf('\n'); return i < 0 ? s.Length : s.Length - i - 1;
    }

    private readonly record struct Delta(int Start, int End, int LengthDelta, int LineDelta, int TailColumnDelta);
}