namespace AzureBlueSolutions.Json.NET;

/// <summary>
/// Ready-to-use ParseOptions presets for strict and tolerant operation.
/// </summary>
public static class Profiles
{
    /// <summary>
    /// Strict profile: correctness-first, no sanitization or leniencies.
    /// </summary>
    public static ParseOptions Strict() => new()
    {
        NormalizeLineEndings = true,
        CollectLineInfo = true,
        AllowComments = false,
        DuplicatePropertyHandling = DuplicateKeyStrategy.Error,
        EnableSanitizationFallback = false,
        EnableAggressiveRecovery = false,
        AllowTrailingCommas = false,
        RemoveControlCharacters = false,
        ReturnSanitizedText = false,
        IncludeSanitizationDiagnostics = false,
        SnippetContextRadius = 60,
        MaxDepth = 128,
        MaxDocumentLength = 4_000_000,
        ProduceTokenSpans = true,
        ProducePathMap = true,
        ResolveErrorCode = null,
        FixUnterminatedStrings = false,
        RecoverMissingCommas = false,
        RecoverMissingClosers = false
    };

    /// <summary>
    /// Tolerant profile: recovery-first with normalization and sanitization.
    /// </summary>
    public static ParseOptions Tolerant() => new()
    {
        NormalizeLineEndings = true,
        CollectLineInfo = true,
        AllowComments = true,
        DuplicatePropertyHandling = DuplicateKeyStrategy.OverwriteWithLast,
        EnableSanitizationFallback = true,
        EnableAggressiveRecovery = true,
        AllowTrailingCommas = true,
        RemoveControlCharacters = true,
        ReturnSanitizedText = true,
        IncludeSanitizationDiagnostics = true,
        SnippetContextRadius = 60,
        MaxDepth = 128,
        MaxDocumentLength = 4_000_000,
        ProduceTokenSpans = true,
        ProducePathMap = true,
        ResolveErrorCode = null,
        FixUnterminatedStrings = true,
        RecoverMissingCommas = true,
        RecoverMissingClosers = true
    };
}