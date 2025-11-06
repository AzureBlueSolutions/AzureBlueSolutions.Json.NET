namespace AzureBlueSolutions.Json.NET;

/// <summary>
///     Ready-to-use <see cref="ParseOptions" /> presets for strict and tolerant operation.
/// </summary>
public static class Profiles
{
    /// <summary>
    ///     Strict profile: correctness-first, no sanitization or leniencies.
    /// </summary>
    /// <returns>Configured <see cref="ParseOptions" /> instance.</returns>
    public static ParseOptions Strict()
    {
        return new ParseOptions
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
    }

    /// <summary>
    ///     Tolerant profile: recovery-first with normalization and sanitization.
    /// </summary>
    /// <returns>Configured <see cref="ParseOptions" /> instance.</returns>
    public static ParseOptions Tolerant()
    {
        return new ParseOptions
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
}