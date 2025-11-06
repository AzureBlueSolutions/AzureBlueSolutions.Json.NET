namespace AzureBlueSolutions.Json.NET.Sample;

public static class Program
{
    private static void Main()
    {
        Console.WriteLine("=== AzureBlueSolutions.Json.NET Program ===");
        Console.WriteLine();

        TestSizeLimit();
        TestDepthLimit();
        TestSanitizationAndLsp();
        TestDuplicateKeys();

        Console.WriteLine();
        Console.WriteLine("All demo cases finished.");
    }

    private static void TestSizeLimit()
    {
        Console.WriteLine("---- Case: Size Limit ----");
        string big = new string(' ', 256); // trivial content; we set a tiny MaxDocumentLength to trigger
        var options = new ParseOptions
        {
            MaxDocumentLength = 128, // intentionally small
            ProduceTokenSpans = true,
            ProducePathMap = false
        };

        var result = JsonParser.ParseSafe(big, options);
        ConsoleDiagnosticWriter.WriteErrors(result.Errors);

        // Show that tokenization still ran (helpful for a preview/editor)
        Console.WriteLine($"TokenSpans produced: {result.TokenSpans.Count}");
        Console.WriteLine();
    }

    private static void TestDepthLimit()
    {
        Console.WriteLine("---- Case: Depth Limit ----");
        string deep = BuildDeepArray(64); // [[[[ ... ]]]]
        var options = new ParseOptions
        {
            MaxDepth = 16, // purposely below actual depth
            MaxDocumentLength = 4_000_000
        };

        var result = JsonParser.ParseSafe(deep, options);
        ConsoleDiagnosticWriter.WriteErrors(result.Errors);

        foreach (var e in result.Errors)
        {
            if (e.Code == DefaultErrorCodes.Resolve(ErrorKey.DepthLimitExceeded))
            {
                Console.WriteLine("Depth limit correctly enforced.");
            }
        }

        Console.WriteLine();
    }

    private static void TestSanitizationAndLsp()
    {
        Console.WriteLine("---- Case: Sanitization + LSP (tokens & path map) ----");

        string json = """
                      {
                        // line comment
                        "name": "ReCrafter",
                        "version": "1.0.0",
                        "features": ["lsp", "sanitization",],
                        /* block comment */
                        "config": { "depth": 1, "enabled": true, },
                        "numbers": [1, 2, 3,]
                      }
                      """;

        var options = new ParseOptions
        {
            AllowComments = false, // make initial pass fail so sanitization kicks in
            IncludeSanitizationDiagnostics = true, // show W100/W101
            ReturnSanitizedText = true,
            ProduceTokenSpans = true,
            ProducePathMap = true,
            MaxDepth = 128,
            MaxDocumentLength = 4_000_000
        };

        var result = JsonParser.ParseSafe(json, options);

        Console.WriteLine("Errors and warnings:");
        ConsoleDiagnosticWriter.WriteErrors(result.Errors);

        if (result.Success)
        {
            Console.WriteLine("Parse succeeded after sanitization.");
        }

        if (!string.IsNullOrEmpty(result.SanitizedText))
        {
            Console.WriteLine("Sanitized text preview:");
            Console.WriteLine(result.SanitizedText);
        }

        // Token span preview
        Console.WriteLine();
        Console.WriteLine("First 20 token spans (zero-based line:column -> line:column, offsets):");
        foreach (var t in result.TokenSpans.Take(20))
        {
            var s = t.Range.Start;
            var e = t.Range.End;
            Console.WriteLine($"{t.Kind,-12}  [{s.Line}:{s.Column} ({s.Offset}) -> {e.Line}:{e.Column} ({e.Offset})]");
        }

        // Path map preview
        Console.WriteLine();
        Console.WriteLine("Path map samples:");
        ShowPath(result, "name");
        ShowPath(result, "version");
        ShowPath(result, "features[1]");
        ShowPath(result, "config.depth");
        ShowPath(result, "numbers[2]");

        Console.WriteLine();
    }

    private static void TestDuplicateKeys()
    {
        Console.WriteLine("---- Case: Duplicate Keys ----");
        string dup = """{ "a": 1, "a": 2 }""";

        var options = new ParseOptions
        {
            DuplicatePropertyHandling = DuplicateKeyStrategy.Error,
            ProduceTokenSpans = true,
            ProducePathMap = true
        };

        var result = JsonParser.ParseSafe(dup, options);

        ConsoleDiagnosticWriter.WriteErrors(result.Errors);

        foreach (var e in result.Errors)
        {
            if (e.Path is not null)
            {
                Console.WriteLine($"Error Path: {e.Path}");
            }

            if (e.Range is not null)
            {
                Console.WriteLine($"LSP Range anchor (zero-based): {e.Range.Start.Line}:{e.Range.Start.Column}");
            }
        }

        Console.WriteLine();
    }

    private static string BuildDeepArray(int depth)
    {
        // [[[ 0 ]]]
        return new string('[', depth) + "0" + new string(']', depth);
    }

    private static void ShowPath(JsonParseResult result, string path)
    {
        if (result.PathRanges.TryGetValue(path, out var pr))
        {
            var name = pr.Name is null ? "(n/a)" : $"{pr.Name.Start.Line}:{pr.Name.Start.Column}";
            var value = pr.Value is null ? "(n/a)" : $"{pr.Value.Start.Line}:{pr.Value.Start.Column}";
            Console.WriteLine($"{path,-20} name@{name}  value@{value}");
        }
        else
        {
            Console.WriteLine($"{path,-20} [no range found]");
        }
    }
}