namespace AzureBlueSolutions.Json.NET;

/// <summary>
///     Orchestrates strict and tolerant parsing modes with a clear, configurable
///     selection policy (e.g., correctness‑first or recovery‑first).
/// </summary>
public static class JsonProcessor
{
    /// <summary>
    ///     Parses using the chosen mode and priority.
    /// </summary>
    /// <param name="text">The JSON text to parse.</param>
    /// <param name="options">
    ///     Processing options that specify the mode, priority, and the strict/tolerant
    ///     <see cref="ParseOptions" /> to use. If <c>null</c>, defaults are applied.
    /// </param>
    /// <param name="cancellationToken">A token to observe for cancellation.</param>
    /// <returns>
    ///     A <see cref="JsonProcessingResult" /> containing individual mode results (when run)
    ///     and the policy‑selected <see cref="JsonProcessingResult.SelectedResult" />.
    /// </returns>
    public static JsonProcessingResult Parse(
        string text,
        ProcessingOptions? options = null,
        CancellationToken cancellationToken = default)
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
    ///     Asynchronous counterpart of <see cref="Parse" /> that returns a task.
    /// </summary>
    /// <param name="text">The JSON text to parse.</param>
    /// <param name="options">Processing options; if <c>null</c>, defaults are applied.</param>
    /// <param name="cancellationToken">A token to observe for cancellation.</param>
    /// <returns>A task that resolves to a <see cref="JsonProcessingResult" />.</returns>
    public static Task<JsonProcessingResult> ParseAsync(
        string text,
        ProcessingOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        return ParseCoreAsync(text, options, cancellationToken);
    }

    /// <summary>
    ///     Runs the strict parser and selects it as the final result.
    /// </summary>
    private static JsonProcessingResult RunStrict(
        string text,
        ProcessingOptions options,
        CancellationToken cancellationToken)
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

    /// <summary>
    ///     Runs the tolerant parser and selects it as the final result.
    /// </summary>
    private static JsonProcessingResult RunTolerant(
        string text,
        ProcessingOptions options,
        CancellationToken cancellationToken)
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

    /// <summary>
    ///     Runs both strict and tolerant parsers and selects a result according to the priority policy.
    /// </summary>
    private static JsonProcessingResult RunBoth(
        string text,
        ProcessingOptions options,
        CancellationToken cancellationToken)
    {
        if (options.Priority == ParsePriority.CorrectnessFirst)
        {
            var strict = JsonParser.ParseSafe(text, options.Strict, cancellationToken);
            if (strict.Success && !HasHardValidationFailures(strict))
                return new JsonProcessingResult
                {
                    ModeUsed = ParserMode.Both,
                    PriorityUsed = ParsePriority.CorrectnessFirst,
                    StrictResult = strict,
                    SelectedResult = strict,
                    SelectedIsStrict = true
                };

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
                return new JsonProcessingResult
                {
                    ModeUsed = ParserMode.Both,
                    PriorityUsed = ParsePriority.RecoveryFirst,
                    TolerantResult = tolerant,
                    SelectedResult = tolerant,
                    SelectedIsStrict = false
                };

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

    /// <summary>
    ///     Core async implementation that mirrors <see cref="Parse" /> behavior.
    /// </summary>
    private static async Task<JsonProcessingResult> ParseCoreAsync(
        string text,
        ProcessingOptions? opts,
        CancellationToken cancellationToken)
    {
        var options = opts ?? new ProcessingOptions();
        switch (options.Mode)
        {
            case ParserMode.Strict:
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
            case ParserMode.Tolerant:
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
        }

        if (options.Priority == ParsePriority.CorrectnessFirst)
        {
            var strict = await JsonParser.ParseSafeAsync(text, options.Strict, cancellationToken);
            if (strict.Success && !HasHardValidationFailures(strict))
                return new JsonProcessingResult
                {
                    ModeUsed = ParserMode.Both,
                    PriorityUsed = ParsePriority.CorrectnessFirst,
                    StrictResult = strict,
                    SelectedResult = strict,
                    SelectedIsStrict = true
                };

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
                return new JsonProcessingResult
                {
                    ModeUsed = ParserMode.Both,
                    PriorityUsed = ParsePriority.RecoveryFirst,
                    TolerantResult = tolerant,
                    SelectedResult = tolerant,
                    SelectedIsStrict = false
                };

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

    /// <summary>
    ///     Returns <c>true</c> when the result has hard validation failures (or no root).
    /// </summary>
    private static bool HasHardValidationFailures(JsonParseResult result)
    {
        if (!result.Success) return true;

        foreach (var e in result.Errors)
            if (e is { Severity: ErrorSeverity.Error } &&
                string.Equals(e.Stage, "Validation", StringComparison.OrdinalIgnoreCase))
                return true;

        return false;
    }
}