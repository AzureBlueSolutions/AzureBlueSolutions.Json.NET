using AzureBlueSolutions.Json.NET;

internal class Program
{
    private static void Main()
    {
        var jsonObject = "{ \"a\": 1, }";
        var jsonArray = "[1, 2, ]";

        Console.WriteLine("=== TOKEN DUMPS ===");
        DumpTokens("OBJECT", jsonObject);
        DumpTokens("ARRAY", jsonArray);

        Console.WriteLine();
        Console.WriteLine("=== POLICY CHECKS (TryRemoveCommaBeforeCloser) ===");
        RunPolicyProbe("OBJECT", jsonObject);
        RunPolicyProbe("ARRAY", jsonArray);

        Console.WriteLine();
        Console.WriteLine("Done.");
    }

    private static void DumpTokens(string caption, string json)
    {
        var opts = new ParseOptions
        {
            AllowComments = false,
            CollectLineInfo = false,
            ProduceTokenSpans = true,
            ProducePathMap = false,
            NormalizeLineEndings = true,
            IncludeSanitizationDiagnostics = false,
            EnableSanitizationFallback = false
        };

        var parsed = JsonParser.ParseSafe(json, opts);

        Console.WriteLine($"--- {caption} ---");
        Console.WriteLine(json);
        Console.WriteLine($"Length: {json.Length}");
        foreach (var t in parsed.TokenSpans)
            Console.WriteLine($"{t.Kind,-12}  start={t.Range.Start.Offset,3}  end={t.Range.End.Offset,3}");
        Console.WriteLine();
    }

    private static void RunPolicyProbe(string caption, string json)
    {
        var tokens = Lex(json);
        var closer = FindFirst(tokens, JsonLexemeKind.RightBrace)
                     ?? FindFirst(tokens, JsonLexemeKind.RightBracket);

        Console.WriteLine($"--- {caption} ---");
        Console.WriteLine(json);

        if (closer is null)
        {
            Console.WriteLine("No closer token found; cannot probe TryRemoveCommaBeforeCloser.");
            Console.WriteLine();
            return;
        }

        var caretAfterCloser = closer.Range.End.Offset;
        var edit = CommaPolicy.TryRemoveCommaBeforeCloser(json, tokens, caretAfterCloser);

        if (edit is null)
        {
            Console.WriteLine("Edit: <null> (no trailing comma detected or closer not found)");
            Console.WriteLine();
            return;
        }

        Console.WriteLine("Edit:");
        Console.WriteLine($"  Range.Start.Offset = {edit.Range.Start.Offset}");
        Console.WriteLine($"  Range.End.Offset   = {edit.Range.End.Offset}");
        Console.WriteLine($"  NewText            = \"{edit.NewText}\"");

        var start = Math.Clamp(edit.Range.Start.Offset, 0, json.Length);
        var end = Math.Clamp(edit.Range.End.Offset, start, json.Length);
        var slice = json.Substring(start, end - start);
        Console.WriteLine($"  Removed Slice      = \"{slice}\"");

        var before = json.Substring(0, start);
        var after = json.Substring(end);
        var result = before + edit.NewText + after;
        Console.WriteLine("  Resulting Text     = " + result);
        Console.WriteLine();
    }

    private static IReadOnlyList<JsonTokenSpan> Lex(string json)
    {
        var options = new ParseOptions
        {
            AllowComments = false,
            CollectLineInfo = false,
            ProduceTokenSpans = true,
            ProducePathMap = false,
            NormalizeLineEndings = true,
            IncludeSanitizationDiagnostics = false,
            EnableSanitizationFallback = false
        };
        var result = JsonParser.ParseSafe(json, options);
        return result.TokenSpans;
    }

    private static JsonTokenSpan? FindFirst(IReadOnlyList<JsonTokenSpan> tokens, JsonLexemeKind kind)
    {
        foreach (var t in tokens)
            if (t.Kind == kind)
                return t;
        return null;
    }
}