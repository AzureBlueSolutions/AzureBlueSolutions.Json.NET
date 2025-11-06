namespace AzureBlueSolutions.Json.NET;

/// <summary>
/// Options to control parsing behavior.
/// </summary>
public sealed record ParseOptions
{
    public bool NormalizeLineEndings { get; init; } = true;
    public bool CollectLineInfo { get; init; } = true;
    public bool AllowComments { get; init; }
    public DuplicateKeyStrategy DuplicatePropertyHandling { get; init; } = DuplicateKeyStrategy.OverwriteWithLast;
    public bool EnableSanitizationFallback { get; init; } = true;
    public bool EnableAggressiveRecovery { get; init; } = true;
    public bool AllowTrailingCommas { get; init; } = true;
    public bool RemoveControlCharacters { get; init; } = true;
    public bool ReturnSanitizedText { get; init; } = true;
    public bool IncludeSanitizationDiagnostics { get; init; } = true;
    public int SnippetContextRadius { get; init; } = 60;
    public Func<ErrorKey, string>? ResolveErrorCode { get; init; }
    public int MaxDepth { get; init; } = 128;
    public int MaxDocumentLength { get; init; } = 4_000_000;
    public bool ProduceTokenSpans { get; init; } = true;
    public bool ProducePathMap { get; init; } = true;
    public int TokenSpanLimit { get; init; } = 2_000_000;

    /// <summary>Close unterminated strings during sanitization (e.g., missing closing quote in a property name).</summary>
    public bool FixUnterminatedStrings { get; init; } = true;

    /// <summary>Insert a missing comma between adjacent values/properties when a newline boundary indicates it.</summary>
    public bool RecoverMissingCommas { get; init; } = false;

    /// <summary>Insert a single missing ']' or '}' when EOF or a following closer on the next line indicates one short.</summary>
    public bool RecoverMissingClosers { get; init; } = false;
}

public enum DuplicateKeyStrategy
{
    Error,
    KeepFirst,
    OverwriteWithLast
}