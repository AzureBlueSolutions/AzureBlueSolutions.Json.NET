namespace AzureBlueSolutions.Json.NET
{
    /// <summary>
    /// Result of an incremental token update.
    /// </summary>
    /// <param name="Text">
    /// The updated full document text after applying all changes.
    /// </param>
    /// <param name="TokenSpans">
    /// The updated list of token spans for the document.
    /// </param>
    /// <param name="WindowStartOffset">
    /// The zero-based start offset (inclusive) of the retokenized window in the updated text.
    /// </param>
    /// <param name="WindowEndOffset">
    /// The zero-based end offset (exclusive) of the retokenized window in the updated text.
    /// </param>
    public sealed record IncrementalTokenUpdateResult(
        string Text,
        IReadOnlyList<JsonTokenSpan> TokenSpans,
        int WindowStartOffset,
        int WindowEndOffset);

    /// <summary>
    /// Performs windowed re-tokenization around edited regions and splices the result
    /// with existing tokens to minimize work after small text edits.
    /// </summary>
    public static class IncrementalJsonTokenizer
    {
        /// <summary>
        /// Applies one or more text changes, re-tokenizes a minimal window, and returns updated text and tokens.
        /// </summary>
        /// <param name="oldText">The original document text.</param>
        /// <param name="oldSpans">The existing tokens for <paramref name="oldText"/>.</param>
        /// <param name="changes">A list of text edits (zero-based, end-exclusive).</param>
        /// <param name="contextRadius">
        /// Extra characters to include before and after the edit range to stabilize tokenization.
        /// </param>
        /// <param name="cancellationToken">A token to observe for cancellation.</param>
        /// <returns>
        /// An <see cref="IncrementalTokenUpdateResult"/> containing updated text, merged tokens,
        /// and the retokenized window bounds.
        /// </returns>
        /// <remarks>
        /// When no changes are supplied, the method returns the original text and spans.
        /// </remarks>
        public static IncrementalTokenUpdateResult Update(
            string oldText,
            IReadOnlyList<JsonTokenSpan> oldSpans,
            IReadOnlyList<TextChange> changes,
            int contextRadius = 256,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(oldText))
            {
                var newTextEmpty = ApplyChanges(oldText, changes);
                var all = new JsonTokenizer(newTextEmpty, default ,cancellationToken).Tokenize();
                return new IncrementalTokenUpdateResult(newTextEmpty, all, 0, newTextEmpty.Length);
            }

            var ordered = changes.OrderBy(c => c.StartOffset).ToArray();
            var newText = ApplyChanges(oldText, ordered);
            if (ordered.Length == 0) return new IncrementalTokenUpdateResult(oldText, oldSpans, 0, oldText.Length);

            var minStart = Math.Clamp(ordered.Min(c => c.StartOffset), 0, oldText.Length);
            var maxEndOld = Math.Clamp(ordered.Max(c => c.EndOffset), 0, oldText.Length);
            var totalDelta = ordered.Sum(c => c.LengthDelta);

            var windowStart = Math.Max(0, minStart - contextRadius);
            var windowEndNew = Math.Min(newText.Length, maxEndOld + totalDelta + contextRadius);

            int seedLine = 0, seedCol = 0;
            if (windowStart > 0)
            {
                var tmp = OffsetToLineCol(newText, windowStart);
                seedLine = tmp.line;
                seedCol = tmp.col;
            }

            var windowTokens = TokenizeWindow(newText, windowStart, windowEndNew, seedLine, seedCol, cancellationToken);

            var before = oldSpans.Where(t => t.Range.End.Offset <= windowStart).ToList();
            var after = oldSpans.Where(t => t.Range.Start.Offset >= windowEndNew).ToList();

            var afterShift = ShiftOffsets(after, ComputeAppliedDeltas(ordered, oldText, newText));

            var merged = new List<JsonTokenSpan>(before.Count + windowTokens.Count + afterShift.Count);
            merged.AddRange(before);
            merged.AddRange(windowTokens);
            merged.AddRange(afterShift);

            return new IncrementalTokenUpdateResult(newText, merged, windowStart, windowEndNew);
        }

        /// <summary>
        /// Applies a sequence of ordered edits to the specified text.
        /// </summary>
        /// <param name="text">The source text to mutate.</param>
        /// <param name="ordered">Edits ordered by <see cref="TextChange.StartOffset"/>.</param>
        /// <returns>The text after all edits are applied.</returns>
        private static string ApplyChanges(string text, IReadOnlyList<TextChange> ordered)
        {
            if (ordered.Count == 0) return text;

            var cur = text;
            var shift = 0;

            foreach (var c in ordered)
            {
                var s = Math.Clamp(c.StartOffset + shift, 0, cur.Length);
                var e = Math.Clamp(c.EndOffset + shift, s, cur.Length);

                var before = s > 0 ? cur[..s] : string.Empty;
                var after = e < cur.Length ? cur[e..] : string.Empty;

                cur = before + (c.NewText) + after;
                shift += (c.NewText?.Length ?? 0) - (e - s);
            }

            return cur;
        }

        /// <summary>
        /// Computes offset and line/column deltas introduced by the applied edits, in the coordinates of the new text.
        /// </summary>
        /// <param name="ordered">Edits ordered by start offset.</param>
        /// <param name="oldText">The original text before edits.</param>
        /// <param name="newText">The updated text after edits.</param>
        /// <returns>
        /// A list of tuples describing, for each edit, the affected span in new coordinates and its offset/line/column deltas.
        /// </returns>
        private static IReadOnlyList<(int start, int end, int delta, int newLinesDelta, int tailDelta)>
            ComputeAppliedDeltas(
                IReadOnlyList<TextChange> ordered,
                string oldText,
                string newText)
        {
            var result = new List<(int, int, int, int, int)>(ordered.Count);
            var runningShift = 0;

            foreach (var c in ordered)
            {
                var startOld = Math.Clamp(c.StartOffset, 0, oldText.Length);
                var endOld = Math.Clamp(c.EndOffset, 0, oldText.Length);

                var removed = oldText.Substring(startOld, Math.Max(0, endOld - startOld));
                var added = c.NewText;

                var removedNl = CountNewlines(removed);
                var addedNl = CountNewlines(added);
                var newLinesDelta = addedNl - removedNl;

                var removedTail = TailColumnLength(removed);
                var addedTail = TailColumnLength(added);
                var tailDelta = addedTail - removedTail;

                var delta = (added?.Length ?? 0) - (endOld - startOld);

                var startNew = startOld + runningShift;
                var endNew = startNew + (added?.Length ?? 0);

                result.Add((startNew, endNew, delta, newLinesDelta, tailDelta));
                runningShift += delta;
            }

            return result;
        }

        /// <summary>
        /// Counts LF characters in a string.
        /// </summary>
        /// <param name="s">The string to scan.</param>
        private static int CountNewlines(string s)
        {
            return string.IsNullOrEmpty(s) ?
                0 :
                s.Count(ch => ch == '\n');
        }

        /// <summary>
        /// Computes the number of characters after the last newline (the tail column length).
        /// </summary>
        /// <param name="s">The string to inspect.</param>
        /// <returns>The length of the trailing segment on the last line.</returns>
        private static int TailColumnLength(string s)
        {
            if (string.IsNullOrEmpty(s)) return 0;
            var i = s.LastIndexOf('\n');
            return i < 0 ? s.Length : s.Length - i - 1;
        }

        /// <summary>
        /// Shifts token positions by applying a set of computed deltas, for tokens strictly after edited slices.
        /// </summary>
        /// <param name="tokens">Tokens to shift.</param>
        /// <param name="deltas">Applied edit deltas in new coordinates.</param>
        /// <returns>A new list with shifted token ranges.</returns>
        private static List<JsonTokenSpan> ShiftOffsets(
            List<JsonTokenSpan> tokens,
            IReadOnlyList<(int start, int end, int delta, int newLinesDelta, int tailDelta)> deltas)
        {
            if (tokens.Count == 0 || deltas.Count == 0) return tokens;

            var result = new List<JsonTokenSpan>(tokens.Count);

            foreach (var t in tokens)
            {
                var s = t.Range.Start;
                var e = t.Range.End;

                var shiftedStart = s;
                var shiftedEnd = e;

                foreach (var d in deltas)
                {
                    // Shift positions strictly after the edited slice.
                    if (shiftedStart.Offset >= d.end)
                    {
                        shiftedStart = ShiftPosition(shiftedStart, d);
                        shiftedEnd = ShiftPosition(shiftedEnd, d);
                    }
                    // Tokens intersecting the changed slice are expected to be replaced by the window tokens.
                }

                result.Add(t with { Range = new TextRange(shiftedStart, shiftedEnd) });
            }

            return result;
        }

        /// <summary>
        /// Applies a single delta to a position if it lies after the edited slice end.
        /// </summary>
        /// <param name="p">The original position.</param>
        /// <param name="d">The applied delta tuple.</param>
        /// <returns>The shifted position.</returns>
        private static TextPosition ShiftPosition(
            TextPosition p,
            (int start, int end, int delta, int newLinesDelta, int tailDelta) d)
        {
            if (p.Offset < d.end) return p;

            var newOffset = p.Offset + d.delta;
            var newLine = p.Line;
            var newCol = p.Column;

            if (d.newLinesDelta != 0)
            {
                newLine += d.newLinesDelta;

                if (d.newLinesDelta > 0)
                {
                    if (newLine >= 0 && newCol >= 0 && d.tailDelta != 0)
                    {
                        // Column becomes the position within the last line; keep as-is here.
                        newCol = p.Column;
                    }
                }
            }
            else
            {
                newCol += d.tailDelta;
                if (newCol < 0) newCol = 0;
            }

            return new TextPosition(newLine, newCol, newOffset);
        }

        /// <summary>
        /// Converts an absolute offset to zero-based (line, column).
        /// </summary>
        /// <param name="text">The text to index.</param>
        /// <param name="offset">The absolute offset to convert.</param>
        /// <returns>A tuple of zero-based line and column.</returns>
        private static (int line, int col) OffsetToLineCol(string text, int offset)
        {
            offset = Math.Clamp(offset, 0, text.Length);
            var line = 0;
            var col = 0;
            var i = 0;

            while (i < offset)
            {
                if (text[i] == '\n')
                {
                    line++;
                    col = 0;
                }
                else
                {
                    col++;
                }
                i++;
            }

            return (line, col);
        }

        /// <summary>
        /// Tokenizes a slice of the document between <paramref name="windowStart"/> and <paramref name="windowEnd"/>.
        /// </summary>
        /// <param name="fullText">The full document text.</param>
        /// <param name="windowStart">Zero-based start offset (inclusive) of the window.</param>
        /// <param name="windowEnd">Zero-based end offset (exclusive) of the window.</param>
        /// <param name="seedLine">The starting zero-based line for the window.</param>
        /// <param name="seedCol">The starting zero-based column for the window.</param>
        /// <param name="cancellationToken">A token to observe for cancellation.</param>
        /// <returns>A list of tokens produced within the window.</returns>
        private static List<JsonTokenSpan> TokenizeWindow(
            string fullText,
            int windowStart,
            int windowEnd,
            int seedLine,
            int seedCol,
            CancellationToken cancellationToken)
        {
            var tokens = new List<JsonTokenSpan>(Math.Max(128, (windowEnd - windowStart) / 4));
            var index = windowStart;
            var line = seedLine;
            var col = seedCol;

            while (index < windowEnd)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var c = fullText[index];

                if (char.IsWhiteSpace(c))
                {
                    switch (c)
                    {
                        case '\r':
                        {
                            if (index + 1 < fullText.Length && fullText[index + 1] == '\n') index++;
                            line++;
                            col = 0;
                            break;
                        }
                        case '\n':
                            line++;
                            col = 0;
                            break;
                        default:
                            col++;
                            break;
                    }

                    index++;
                    continue;
                }

                if (c == '/')
                {
                    if (index + 1 < fullText.Length)
                    {
                        switch (fullText[index + 1])
                        {
                            case '/':
                            {
                                var start = index;
                                index += 2;
                                col += 2;
                                while (index < fullText.Length && fullText[index] != '\n')
                                {
                                    index++;
                                    col++;
                                }
                                EmitToken(JsonLexemeKind.Comment, start, index);
                                continue;
                            }
                            case '*':
                            {
                                var start = index;
                                index += 2;
                                col += 2;
                                while (index < fullText.Length - 1 && !(fullText[index] == '*' && fullText[index + 1] == '/'))
                                {
                                    if (fullText[index] == '\n')
                                    {
                                        line++;
                                        col = 0;
                                    }
                                    else
                                    {
                                        col++;
                                    }

                                    index++;
                                }
                                if (index < fullText.Length)
                                {
                                    index += 2;
                                    col += 2;
                                }
                                EmitToken(JsonLexemeKind.Comment, start, Math.Min(index, fullText.Length));
                                continue;
                            }
                        }
                    }
                }

                switch (c)
                {
                    case '{':
                        EmitToken(JsonLexemeKind.LeftBrace, index, ++index);
                        col++;
                        break;
                    case '}':
                        EmitToken(JsonLexemeKind.RightBrace, index, ++index);
                        col++;
                        break;
                    case '[':
                        EmitToken(JsonLexemeKind.LeftBracket, index, ++index);
                        col++;
                        break;
                    case ']':
                        EmitToken(JsonLexemeKind.RightBracket, index, ++index);
                        col++;
                        break;
                    case ':':
                        EmitToken(JsonLexemeKind.Colon, index, ++index);
                        col++;
                        break;
                    case ',':
                        EmitToken(JsonLexemeKind.Comma, index, ++index);
                        col++;
                        break;
                    case '"':
                        {
                            var start = index;
                            index++;
                            col++;
                            var escape = false;
                            while (index < fullText.Length)
                            {
                                var ch = fullText[index];
                                index++;
                                if (ch == '\n')
                                {
                                    line++;
                                    col = 0;
                                }
                                else
                                {
                                    col++;
                                }

                                if (escape)
                                {
                                    escape = false;
                                    continue;
                                }
                                if (ch == '\\')
                                {
                                    escape = true;
                                    continue;
                                }
                                if (ch == '"') break;
                            }
                            EmitToken(JsonLexemeKind.String, start, index);
                            break;
                        }
                    default:
                        {
                            if (c == '-' || c == '+' || char.IsDigit(c))
                            {
                                var start = index;

                                if (c is '-' or '+')
                                {
                                    index++;
                                    col++;
                                }

                                while (index < fullText.Length && char.IsDigit(fullText[index]))
                                {
                                    index++;
                                    col++;
                                }

                                if (index < fullText.Length && fullText[index] == '.')
                                {
                                    index++;
                                    col++;
                                    while (index < fullText.Length && char.IsDigit(fullText[index]))
                                    {
                                        index++;
                                        col++;
                                    }
                                }

                                if (index < fullText.Length && (fullText[index] == 'e' || fullText[index] == 'E'))
                                {
                                    index++;
                                    col++;
                                    if (index < fullText.Length && (fullText[index] == '-' || fullText[index] == '+'))
                                    {
                                        index++;
                                        col++;
                                    }
                                    while (index < fullText.Length && char.IsDigit(fullText[index]))
                                    {
                                        index++;
                                        col++;
                                    }
                                }

                                EmitToken(JsonLexemeKind.Number, start, index);
                            }
                            else
                            {
                                var start = index;

                                if (Starts(fullText, index, "true"))
                                {
                                    index += 4;
                                    col += 4;
                                    EmitToken(JsonLexemeKind.True, start, index);
                                    break;
                                }
                                if (Starts(fullText, index, "false"))
                                {
                                    index += 5;
                                    col += 5;
                                    EmitToken(JsonLexemeKind.False, start, index);
                                    break;
                                }
                                if (Starts(fullText, index, "null"))
                                {
                                    index += 4;
                                    col += 4;
                                    EmitToken(JsonLexemeKind.Null, start, index);
                                    break;
                                }

                                index++;
                                col++;
                            }
                            break;
                        }
                }
            }

            return tokens;

            void EmitToken(JsonLexemeKind kind, int start, int end)
            {
                var currentCol = col - (index - end);
                var sc = currentCol - (end - start);
                var el = line;
                var ec = sc + (end - start);

                for (var i = start; i < end; i++)
                {
                    if (fullText[i] == '\n')
                    {
                        el++;
                        ec = 0;
                    }
                    else
                    {
                        ec++;
                    }
                }

                var startPos = new TextPosition(line, sc, start);
                var endPos = new TextPosition(el, ec, end);
                tokens.Add(new JsonTokenSpan(kind, new TextRange(startPos, endPos)));
            }
        }

        /// <summary>
        /// Determines whether the specified substring begins at a given index.
        /// </summary>
        /// <param name="text">The source text.</param>
        /// <param name="start">The starting index.</param>
        /// <param name="s">The substring to match.</param>
        /// <returns><c>true</c> if the substring matches; otherwise, <c>false</c>.</returns>
        private static bool Starts(string text, int start, string s)
        {
            if (start + s.Length > text.Length) return false;
            return !s.Where((t, i) =>
                text[start + i] != t).Any();
        }
    }
}
