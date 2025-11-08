using Newtonsoft.Json.Linq;

namespace AzureBlueSolutions.Json.NET;

/// <summary>
///     Result of a JSON parse attempt.
/// </summary>
/// <remarks>
///     This record aggregates the parsed <see cref="JToken" /> (when successful),
///     any diagnostics collected during parsing/sanitization, and optional
///     LSP-friendly artifacts such as token spans and path ranges.
/// </remarks>
public sealed record JsonParseResult
{
    /// <summary>
    ///     Indicates whether parsing produced a non-<c>null</c> <see cref="Root" />.
    /// </summary>
    public bool Success => Root is not null;

    /// <summary>
    ///     The parsed JSON root token produced by Newtonsoft.Json (<see cref="JToken" />),
    ///     or <c>null</c> when parsing did not succeed.
    /// </summary>
    public JToken? Root { get; init; }

    /// <summary>
    ///     The list of diagnostics (errors, warnings, info) emitted across all stages
    ///     (e.g., Initial, Sanitized, Aggressive, Validation).
    /// </summary>
    public IReadOnlyList<JsonParseError> Errors { get; init; } = [];

    /// <summary>
    ///     The sanitized text that was used for successful parsing when a sanitization
    ///     pass modified the input. <c>null</c> when no sanitization was applied or
    ///     when parsing did not succeed.
    /// </summary>
    public string? SanitizedText { get; init; }

    /// <summary>
    ///     Token spans suitable for syntax highlighting and LSP diagnostics.
    ///     Line and column values are zero-based; ranges are end-exclusive.
    /// </summary>
    public IReadOnlyList<JsonTokenSpan> TokenSpans { get; init; } = [];

    /// <summary>
    ///     A map from JSON paths (e.g., <c>root.items[0].name</c>) to their source
    ///     ranges for the property name and value, enabling fast navigation.
    /// </summary>
    public IReadOnlyDictionary<string, JsonPathRange> PathRanges { get; init; } =
        new Dictionary<string, JsonPathRange>();

    /// <summary>
    ///     Optional sanitization report that summarizes what changes were applied
    ///     (e.g., comments removed, trailing commas removed, missing closers inserted).
    /// </summary>
    public SanitizationReport? Report { get; init; }
}

/// <summary>
///     Base report describing the changes performed by a sanitization pass.
/// </summary>
/// <remarks>
///     Concrete instances are exposed via <see cref="JsonSanitizationReport" /> on
///     <see cref="JsonParseResult.Report" />.
/// </remarks>
public abstract record SanitizationReport
{
    /// <summary>
    ///     Whether the sanitizer modified the input text.
    /// </summary>
    public bool Changed { get; init; }

    /// <summary>
    ///     Number of line comments removed (<c>// ...</c>).
    /// </summary>
    public int LineCommentsRemoved { get; init; }

    /// <summary>
    ///     Number of block comments removed (<c>/* ... */</c>).
    /// </summary>
    public int BlockCommentsRemoved { get; init; }

    /// <summary>
    ///     Number of trailing commas removed.
    /// </summary>
    public int TrailingCommasRemoved { get; init; }

    /// <summary>
    ///     Number of control characters removed.
    /// </summary>
    public int ControlCharsRemoved { get; init; }

    /// <summary>
    ///     Whether a UTF-8 BOM was stripped from the beginning of the text.
    /// </summary>
    public bool BomRemoved { get; init; }

    /// <summary>
    ///     Whether CR/CRLF line endings were normalized to LF.
    /// </summary>
    public bool LineEndingsNormalized { get; init; }

    /// <summary>
    ///     Number of unterminated strings that were closed by the sanitizer.
    /// </summary>
    public int UnterminatedStringsClosed { get; init; }

    /// <summary>
    ///     Number of missing commas that were inserted (recovery).
    /// </summary>
    public int MissingCommasInserted { get; init; }

    /// <summary>
    ///     Number of missing closers (<c>']'</c> or <c>'}'</c>) that were inserted (recovery).
    /// </summary>
    public int ClosersInserted { get; init; }
}