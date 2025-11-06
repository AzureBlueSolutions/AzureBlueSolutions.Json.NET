namespace AzureBlueSolutions.Json.NET
{
    /// <summary>
    /// Shifts existing path ranges after text edits and invalidates overlaps.
    /// </summary>
    public static class PathRangeIncrementalUpdater
    {
        /// <summary>
        /// Returns a new map where entries that intersect any change are removed,
        /// and all positions strictly after edits are shifted for line/column/offset.
        /// </summary>
        /// <param name="oldMap">
        /// The existing JSON path-to-range map for the original text.
        /// </param>
        /// <param name="changes">
        /// The list of edits to apply (zero-based offsets, end exclusive).
        /// </param>
        /// <param name="oldText">
        /// The original text before edits.
        /// </param>
        /// <param name="newText">
        /// The updated text after edits.
        /// </param>
        /// <returns>
        /// A new map with intersecting entries removed and surviving ranges shifted to match <paramref name="newText"/>.
        /// </returns>
        public static IReadOnlyDictionary<string, JsonPathRange> Update(
            IReadOnlyDictionary<string, JsonPathRange> oldMap,
            IReadOnlyList<TextChange> changes,
            string oldText,
            string newText)
        {
            if (oldMap.Count == 0 || changes.Count == 0) return oldMap;

            var ordered = changes.OrderBy(c => c.StartOffset).ToArray();
            var deltas = ComputeAppliedDeltas(ordered, oldText);

            var updated = new Dictionary<string, JsonPathRange>(oldMap.Count, StringComparer.Ordinal);

            foreach (var kv in oldMap)
            {
                var name = kv.Value.Name;
                var val = kv.Value.Value;

                if ((name is not null && IntersectsAny(name, ordered)) ||
                    (val is not null && IntersectsAny(val, ordered)))
                    continue;

                var newName = name is null ? null : ApplyDeltas(name, deltas);
                var newVal = val is null ? null : ApplyDeltas(val, deltas);

                updated[kv.Key] = kv.Value with { Name = newName, Value = newVal };
            }

            return updated;
        }

        /// <summary>
        /// Determines whether a range intersects any of the specified edits.
        /// </summary>
        /// <param name="range">The range to test.</param>
        /// <param name="edits">The edits to test against.</param>
        /// <returns><c>true</c> if the range intersects at least one edit; otherwise, <c>false</c>.</returns>
        private static bool IntersectsAny(TextRange range, IReadOnlyList<TextChange> edits)
        {
            return (from edit in edits
                    let startOffset = edit.StartOffset
                    let endOffset = edit.EndOffset
                    where range.Start.Offset < endOffset &&
                          range.End.Offset > startOffset
                    select startOffset).Any();
        }

        /// <summary>
        /// Computes applied deltas (offset, line, and tail-column) in the coordinates after previous edits.
        /// </summary>
        /// <param name="ordered">Edits ordered by start offset.</param>
        /// <param name="oldText">The original text prior to all edits.</param>
        /// <returns>
        /// A list of <c>Delta</c> entries describing how positions shift after each applied edit.
        /// </returns>
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

        /// <summary>
        /// Applies a sequence of deltas to shift a range forward in response to prior edits.
        /// </summary>
        /// <param name="r">The original range.</param>
        /// <param name="deltas">The applied deltas in new-text coordinates.</param>
        /// <returns>The shifted range.</returns>
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

        /// <summary>
        /// Shifts a single position by the specified delta.
        /// </summary>
        /// <param name="p">The original position.</param>
        /// <param name="d">The delta to apply.</param>
        /// <returns>The shifted position.</returns>
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

        /// <summary>
        /// Counts the number of LF characters in the given string.
        /// </summary>
        /// <param name="s">The string to scan.</param>
        /// <returns>The count of <c>'\n'</c> characters.</returns>
        private static int CountNewlines(string s)
        {
            return string.IsNullOrEmpty(s) ?
                0 :
                s.Count(ch => ch == '\n');
        }

        /// <summary>
        /// Computes the length of the trailing segment after the last newline.
        /// </summary>
        /// <param name="s">The string to inspect.</param>
        /// <returns>The number of characters after the last <c>'\n'</c>; zero if empty.</returns>
        private static int TailColumns(string s)
        {
            if (string.IsNullOrEmpty(s)) return 0;
            var i = s.LastIndexOf('\n');
            return i < 0 ? s.Length : s.Length - i - 1;
        }

        /// <summary>
        /// Describes how positions shift due to a single applied edit, expressed in coordinates after earlier edits.
        /// </summary>
        /// <param name="Start">Start offset (inclusive) of the inserted region in new text coordinates.</param>
        /// <param name="End">End offset (exclusive) of the inserted region in new text coordinates.</param>
        /// <param name="LengthDelta">Net change in absolute offset.</param>
        /// <param name="LineDelta">Net change in line count.</param>
        /// <param name="TailColumnDelta">Net change in column position on the trailing line.</param>
        private readonly record struct Delta(int Start, int End, int LengthDelta, int LineDelta, int TailColumnDelta);
    }
}