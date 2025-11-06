namespace AzureBlueSolutions.Json.NET;

/// <summary>
/// Orchestrates strict/tolerant parsing with a clear selection policy.
/// </summary>
public static class JsonProcessor
{
    /// <summary>
    /// Parses using the chosen mode and priority.
    /// </summary>
    public static JsonProcessingResult Parse(string text, ProcessingOptions? options = null, CancellationToken cancellationToken = default)
    {
        options ??= new ProcessingOptions();

        return options.Mode switch
        {
            ParserMode.Strict => RunStrict(text, options, cancellationToken),
            ParserMode.Tolerant => RunTolerant(text, options, cancellationToken),
            _ => RunBoth(text, options, cancellationToken)
        };
    }

    /// <summary>
    /// Async version of <see cref="Parse"/>.
    /// </summary>
    public static Task<JsonProcessingResult> ParseAsync(string text, ProcessingOptions? options = null, CancellationToken cancellationToken = default)
        => ParseCoreAsync(text, options, cancellationToken);

    private static JsonProcessingResult RunStrict(string text, ProcessingOptions options, CancellationToken cancellationToken)
    {
        var strict = JsonParser.ParseSafe(text, options.Strict, cancellationToken);

        return new JsonProcessingResult
        {
            ModeUsed = ParserMode.Strict,
            PriorityUsed = options.Priority,
            StrictResult = strict,
            SelectedResult = strict,
            SelectedIsStrict = true
        };
    }

    private static JsonProcessingResult RunTolerant(string text, ProcessingOptions options, CancellationToken cancellationToken)
    {
        var tolerant = JsonParser.ParseSafe(text, options.Tolerant, cancellationToken);

        return new JsonProcessingResult
        {
            ModeUsed = ParserMode.Tolerant,
            PriorityUsed = options.Priority,
            TolerantResult = tolerant,
            SelectedResult = tolerant,
            SelectedIsStrict = false
        };
    }

    private static JsonProcessingResult RunBoth(string text, ProcessingOptions options, CancellationToken cancellationToken)
    {
        if (options.Priority == ParsePriority.CorrectnessFirst)
        {
            var strict = JsonParser.ParseSafe(text, options.Strict, cancellationToken);

            if (strict.Success && !HasHardValidationFailures(strict))
            {
                return new JsonProcessingResult
                {
                    ModeUsed = ParserMode.Both,
                    PriorityUsed = ParsePriority.CorrectnessFirst,
                    StrictResult = strict,
                    SelectedResult = strict,
                    SelectedIsStrict = true
                };
            }

            var tolerant = JsonParser.ParseSafe(text, options.Tolerant, cancellationToken);

            return new JsonProcessingResult
            {
                ModeUsed = ParserMode.Both,
                PriorityUsed = ParsePriority.CorrectnessFirst,
                StrictResult = strict,
                TolerantResult = tolerant,
                SelectedResult = tolerant,
                SelectedIsStrict = false
            };
        }
        else
        {
            var tolerant = JsonParser.ParseSafe(text, options.Tolerant, cancellationToken);

            if (tolerant.Success && !HasHardValidationFailures(tolerant))
            {
                return new JsonProcessingResult
                {
                    ModeUsed = ParserMode.Both,
                    PriorityUsed = ParsePriority.RecoveryFirst,
                    TolerantResult = tolerant,
                    SelectedResult = tolerant,
                    SelectedIsStrict = false
                };
            }

            var strict = JsonParser.ParseSafe(text, options.Strict, cancellationToken);

            return new JsonProcessingResult
            {
                ModeUsed = ParserMode.Both,
                PriorityUsed = ParsePriority.RecoveryFirst,
                StrictResult = strict,
                TolerantResult = tolerant,
                SelectedResult = strict,
                SelectedIsStrict = true
            };
        }
    }

    private static async Task<JsonProcessingResult> ParseCoreAsync(string text, ProcessingOptions? opts, CancellationToken cancellationToken)
    {
        var options = opts ?? new ProcessingOptions();

        if (options.Mode == ParserMode.Strict)
        {
            var strict = await JsonParser.ParseSafeAsync(text, options.Strict, cancellationToken);
            return new JsonProcessingResult
            {
                ModeUsed = ParserMode.Strict,
                PriorityUsed = options.Priority,
                StrictResult = strict,
                SelectedResult = strict,
                SelectedIsStrict = true
            };
        }

        if (options.Mode == ParserMode.Tolerant)
        {
            var tolerant = await JsonParser.ParseSafeAsync(text, options.Tolerant, cancellationToken);
            return new JsonProcessingResult
            {
                ModeUsed = ParserMode.Tolerant,
                PriorityUsed = options.Priority,
                TolerantResult = tolerant,
                SelectedResult = tolerant,
                SelectedIsStrict = false
            };
        }

        if (options.Priority == ParsePriority.CorrectnessFirst)
        {
            var strict = await JsonParser.ParseSafeAsync(text, options.Strict, cancellationToken);

            if (strict.Success && !HasHardValidationFailures(strict))
            {
                return new JsonProcessingResult
                {
                    ModeUsed = ParserMode.Both,
                    PriorityUsed = ParsePriority.CorrectnessFirst,
                    StrictResult = strict,
                    SelectedResult = strict,
                    SelectedIsStrict = true
                };
            }

            var tolerant = await JsonParser.ParseSafeAsync(text, options.Tolerant, cancellationToken);

            return new JsonProcessingResult
            {
                ModeUsed = ParserMode.Both,
                PriorityUsed = ParsePriority.CorrectnessFirst,
                StrictResult = strict,
                TolerantResult = tolerant,
                SelectedResult = tolerant,
                SelectedIsStrict = false
            };
        }
        else
        {
            var tolerant = await JsonParser.ParseSafeAsync(text, options.Tolerant, cancellationToken);

            if (tolerant.Success && !HasHardValidationFailures(tolerant))
            {
                return new JsonProcessingResult
                {
                    ModeUsed = ParserMode.Both,
                    PriorityUsed = ParsePriority.RecoveryFirst,
                    TolerantResult = tolerant,
                    SelectedResult = tolerant,
                    SelectedIsStrict = false
                };
            }

            var strict = await JsonParser.ParseSafeAsync(text, options.Strict, cancellationToken);

            return new JsonProcessingResult
            {
                ModeUsed = ParserMode.Both,
                PriorityUsed = ParsePriority.RecoveryFirst,
                StrictResult = strict,
                TolerantResult = tolerant,
                SelectedResult = strict,
                SelectedIsStrict = true
            };
        }
    }

    private static bool HasHardValidationFailures(JsonParseResult result)
    {
        if (!result.Success) return true;
        foreach (var e in result.Errors)
        {
            if (e is { Severity: ErrorSeverity.Error } && string.Equals(e.Stage, "Validation", StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }
}