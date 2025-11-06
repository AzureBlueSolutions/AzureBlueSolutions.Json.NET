# AzureBlueSolutions.Json.NET

A high-performance JSON parsing and sanitization library built on Newtonsoft.Json, with extra features for LSP-friendly token spans and recovery.

## What is it?

AzureBlueSolutions.Json.NET is a resilient JSON parsing solution that goes far beyond standard JSON parsers. It provides:

- **Safe parsing with normalization and fallback recovery.** The library automatically detects and repairs common JSON formatting issues like trailing commas, missing closing brackets, unterminated strings, and JavaScript-style comments. When strict parsing fails, it progressively applies sanitization passes—from conservative fixes to aggressive recovery—maximizing the chance of extracting valid data from malformed input. This multi-stage approach means you can work with real-world JSON that doesn't always follow the spec perfectly, without manual preprocessing.

- **Optional token spans and JSON path mapping for editor integrations.** Every parsed document can generate detailed metadata about token locations, types, and boundaries in the source text. This enables features like syntax highlighting, intelligent autocomplete, go-to-definition, and error underlining in code editors and LSP servers. The library tracks exactly where each property name, value, bracket, and comma appears in the original document, making it trivial to build rich editing experiences.

- **Async sanitization for malformed JSON.** All parsing and recovery operations support cancellation tokens and can run asynchronously, with cooperative yielding to keep UI threads responsive during long operations. This makes the library suitable for processing large or complex documents in interactive applications without blocking the user interface.

## Quick Start
```csharp
using AzureBlueSolutions.Json.NET;

var json = @"{
  // trailing comma
  ""name"": ""Demo"",
}
";
var result = JsonParser.ParseSafe(json);
if (result.Success) Console.WriteLine(result.Root);
```

## Full Documentation
https://jsonnet.azurebluesolutions.net