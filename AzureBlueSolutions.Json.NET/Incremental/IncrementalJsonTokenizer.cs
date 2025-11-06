namespace AzureBlueSolutions.Json.NET;

/// <summary>
/// Result of an incremental token update.
/// </summary>
public sealed record IncrementalTokenUpdateResult(
    string Text,
    IReadOnlyList<JsonTokenSpan> TokenSpans,
    int WindowStartOffset,
    int WindowEndOffset);

/// <summary>
/// Performs windowed re-tokenization for edited regions and splices results with previous spans.
/// </summary>
public static class IncrementalJsonTokenizer
{
    /// <summary>
    /// Applies one or more text changes, re-tokenizes a minimal window, and returns updated text and spans.
    /// </summary>
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
            var all = new JsonTokenizer(newTextEmpty, cancellationToken).Tokenize();
            return new IncrementalTokenUpdateResult(newTextEmpty, all, 0, newTextEmpty.Length);
        }

        var ordered = changes?.OrderBy(c => c.StartOffset).ToArray() ?? Array.Empty<TextChange>();
        var newText = ApplyChanges(oldText, ordered);

        if (ordered.Length == 0)
        {
            return new IncrementalTokenUpdateResult(oldText, oldSpans, 0, oldText.Length);
        }

        var minStart = Math.Clamp(ordered.Min(c => c.StartOffset), 0, oldText.Length);
        var maxEndOld = Math.Clamp(ordered.Max(c => c.EndOffset), 0, oldText.Length);
        var totalDelta = ordered.Sum(c => c.LengthDelta);

        var windowStart = Math.Max(0, minStart - contextRadius);
        var windowEndNew = Math.Min(newText.Length, maxEndOld + totalDelta + contextRadius);

        var newLineIndex = new TextLineIndex(newText);
        int seedLine = 0, seedCol = 0;
        if (windowStart > 0)
        {
            // Translate windowStart to zero-based line/column in the new text.
            // We do that by walking the line index.
            var tmp = OffsetToLineCol(newText, windowStart);
            seedLine = tmp.line;
            seedCol = tmp.col;
        }

        var windowTokens = TokenizeWindow(newText, windowStart, windowEndNew, seedLine, seedCol, cancellationToken);

        var before = oldSpans.Where(t => t.Range.End.Offset <= windowStart).ToList();
        var after = oldSpans.Where(t => t.Range.Start.Offset >= windowEndNew).ToList();

        // Splice: [before] + [windowTokens] + [after shifted by totalDelta where needed around the window]
        // After-tokens might need shifting if the applied changes altered offsets between old and new text.
        var afterShift = ShiftOffsets(after, ComputeAppliedDeltas(ordered, oldText, newText));

        var merged = new List<JsonTokenSpan>(before.Count + windowTokens.Count + afterShift.Count);
        merged.AddRange(before);
        merged.AddRange(windowTokens);
        merged.AddRange(afterShift);

        return new IncrementalTokenUpdateResult(newText, merged, windowStart, windowEndNew);
    }

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
            cur = before + (c.NewText ?? string.Empty) + after;
            shift += (c.NewText?.Length ?? 0) - (e - s);
        }

        return cur;
    }

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
            var added = c.NewText ?? string.Empty;

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

    private static int CountNewlines(string s)
    {
        if (string.IsNullOrEmpty(s)) return 0;
        var n = 0;
        foreach (var ch in s)
            if (ch == '\n')
                n++;
        return n;
    }

    private static int TailColumnLength(string s)
    {
        if (string.IsNullOrEmpty(s)) return 0;
        var i = s.LastIndexOf('\n');
        return i < 0 ? s.Length : s.Length - i - 1;
    }

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
                // If token intersects the changed slice, keep it; it will be replaced by window tokens anyway.
            }

            result.Add(new JsonTokenSpan(t.Kind, new TextRange(shiftedStart, shiftedEnd)));
        }

        return result;
    }

    private static TextPosition ShiftPosition(TextPosition p,
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
                    newCol = p.Column; // column becomes position within last line after newline; keep as-is here
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

        // Inlined the logic from the 'Emit' local function and simplified column calculation
        void EmitToken(JsonLexemeKind kind, int start, int end)
        {
            var currentLine = line;
            var currentCol = col - (index - end); // col before consuming this token

            var sl = currentLine;
            var sc = currentCol - (end - start); // start column is current col minus token length
            var el = sl;
            var ec = sc + (end - start);

            // Re-calculate line/col for multi-line tokens
            for (int i = start; i < end; i++)
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

            var startPos = new TextPosition(sl, sc, start);
            var endPos = new TextPosition(el, ec, end);
            tokens.Add(new JsonTokenSpan(kind, new TextRange(startPos, endPos)));
        }

        while (index < windowEnd) // Fixed: changed 'InRange' to 'index < windowEnd'
        {
            cancellationToken.ThrowIfCancellationRequested();
            var c = fullText[index];

            if (char.IsWhiteSpace(c))
            {
                if (c == '\r')
                {
                    if (index + 1 < fullText.Length && fullText[index + 1] == '\n') index++;
                    line++;
                    col = 0;
                    index++;
                }
                else if (c == '\n')
                {
                    line++;
                    col = 0;
                    index++;
                }
                else
                {
                    col++;
                    index++;
                }

                continue;
            }

            if (c == '/')
            {
                if (index + 1 < fullText.Length)
                {
                    if (fullText[index + 1] == '/')
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

                    if (fullText[index + 1] == '*')
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
                                index++;
                            }
                            else
                            {
                                col++;
                                index++;
                            }
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
                            else col++;

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
                            if (c == '-' || c == '+')
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
                            break;
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
    }

    private static bool Starts(string text, int start, string s)
    {
        if (start + s.Length > text.Length) return false;
        for (int i = 0; i < s.Length; i++)
            if (text[start + i] != s[i])
                return false;
        return true;
    }
}