using Newtonsoft.Json.Linq;

namespace AzureBlueSolutions.Json.NET;

/// <summary>
/// Result of a parse attempt.
/// </summary>
public sealed record JsonParseResult
{
    public bool Success => Root is not null;
    public JToken? Root { get; init; }
    public IReadOnlyList<JsonParseError> Errors { get; init; } = [];

    /// <summary>
    /// The sanitized text used for successful parsing (when sanitization applied).
    /// </summary>
    public string? SanitizedText { get; init; }

    /// <summary>
    /// Token spans for syntax highlighting (zero-based lines/columns).
    /// </summary>
    public IReadOnlyList<JsonTokenSpan> TokenSpans { get; init; } = [];

    /// <summary>
    /// Map of JSON paths to name/value ranges for quick navigation.
    /// </summary>
    public IReadOnlyDictionary<string, JsonPathRange> PathRanges { get; init; } = new Dictionary<string, JsonPathRange>();
    public SanitizationReport? Report { get; init; }
}

/// <summary>
/// Base sanitization report.
/// </summary>
public abstract record SanitizationReport
{
    public bool Changed { get; init; }
    public int LineCommentsRemoved { get; init; }
    public int BlockCommentsRemoved { get; init; }
    public int TrailingCommasRemoved { get; init; }
    public int ControlCharsRemoved { get; init; }
    public bool BomRemoved { get; init; }
    public bool LineEndingsNormalized { get; init; }
    /// <summary>Number of unterminated strings closed by sanitizer.</summary>
    public int UnterminatedStringsClosed { get; init; }

    /// <summary>Number of missing commas inserted.</summary>
    public int MissingCommasInserted { get; init; }

    /// <summary>Number of missing closers (']' or '}') inserted.</summary>
    public int ClosersInserted { get; init; }
}