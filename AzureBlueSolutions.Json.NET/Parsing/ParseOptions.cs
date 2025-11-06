namespace AzureBlueSolutions.Json.NET;

/// <summary>
/// Options that control parsing behavior, normalization, recovery,
/// and the production of LSP‑friendly artifacts.
/// </summary>
public sealed record ParseOptions
{
    /// <summary>
    /// Converts all line endings to <c>'\n'</c> prior to parsing.
    /// </summary>
    public bool NormalizeLineEndings { get; init; } = true;

    /// <summary>
    /// Collects line and column information for diagnostics and mapping.
    /// </summary>
    public bool CollectLineInfo { get; init; } = true;

    /// <summary>
    /// Allows JavaScript‑style comments (<c>//</c>, <c>/* ... */</c>) to be present in the input.
    /// </summary>
    public bool AllowComments { get; init; }

    /// <summary>
    /// Determines how to handle duplicate property names in JSON objects.
    /// </summary>
    public DuplicateKeyStrategy DuplicatePropertyHandling { get; init; } =
        DuplicateKeyStrategy.OverwriteWithLast;

    /// <summary>
    /// Enables a sanitization fallback pass when strict parsing fails.
    /// </summary>
    public bool EnableSanitizationFallback { get; init; } = true;

    /// <summary>
    /// Enables more aggressive recovery (e.g., inserting missing closers) if the
    /// initial sanitization pass still does not yield a valid parse.
    /// </summary>
    public bool EnableAggressiveRecovery { get; init; } = true;

    /// <summary>
    /// Permits trailing commas inside arrays and objects during sanitization.
    /// </summary>
    public bool AllowTrailingCommas { get; init; } = true;

    /// <summary>
    /// Removes control characters from the input during sanitization (except LF and TAB).
    /// </summary>
    public bool RemoveControlCharacters { get; init; } = true;

    /// <summary>
    /// When <c>true</c>, returns the sanitized text used for a successful parse.
    /// </summary>
    public bool ReturnSanitizedText { get; init; } = true;

    /// <summary>
    /// When <c>true</c>, includes diagnostics describing changes performed by the sanitizer.
    /// </summary>
    public bool IncludeSanitizationDiagnostics { get; init; } = true;

    /// <summary>
    /// The number of context characters to include on each side when building error snippets.
    /// </summary>
    public int SnippetContextRadius { get; init; } = 60;

    /// <summary>
    /// Optional delegate that resolves an <see cref="ErrorKey"/> to a code string
    /// (e.g., <c>"E002"</c>). Defaults to <see cref="DefaultErrorCodes.Resolve"/>.
    /// </summary>
    public Func<ErrorKey, string>? ResolveErrorCode { get; init; }

    /// <summary>
    /// Maximum allowed nesting depth for the JSON reader (values &gt; 0 enable the cap).
    /// </summary>
    public int MaxDepth { get; init; } = 128;

    /// <summary>
    /// Maximum allowed document length (in characters). Values &gt; 0 enforce the limit.
    /// </summary>
    public int MaxDocumentLength { get; init; } = 4_000_000;

    /// <summary>
    /// Produces token span metadata for syntax highlighting and diagnostics.
    /// </summary>
    public bool ProduceTokenSpans { get; init; } = true;

    /// <summary>
    /// Produces a path map (JSONPath source ranges) for quick navigation.
    /// </summary>
    public bool ProducePathMap { get; init; } = true;

    /// <summary>
    /// Safety cap on the number of token spans to generate.
    /// </summary>
    public int TokenSpanLimit { get; init; } = 2_000_000;

    /// <summary>
    /// Attempts to close unterminated strings during sanitization.
    /// </summary>
    public bool FixUnterminatedStrings { get; init; } = true;

    /// <summary>
    /// Attempts to insert missing commas between adjacent values/properties during recovery.
    /// </summary>
    public bool RecoverMissingCommas { get; init; } = false;

    /// <summary>
    /// Attempts to insert a single missing closing bracket or brace during recovery.
    /// </summary>
    public bool RecoverMissingClosers { get; init; } = false;
}

/// <summary>
/// Strategy for handling duplicate keys in JSON objects.
/// </summary>
public enum DuplicateKeyStrategy
{
    /// <summary>
    /// Treat duplicate keys as an error; parsing fails at the duplicate.
    /// </summary>
    Error,

    /// <summary>
    /// Keep the first occurrence of the key and ignore subsequent duplicates.
    /// </summary>
    KeepFirst,

    /// <summary>
    /// Overwrite the first occurrence with the last one encountered.
    /// </summary>
    OverwriteWithLast
}
