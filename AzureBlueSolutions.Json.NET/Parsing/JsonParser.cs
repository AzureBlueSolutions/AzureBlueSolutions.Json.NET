using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace AzureBlueSolutions.Json.NET;

public static class JsonParser
{
    /// <summary>
    /// Robust parse with normalization, tolerant settings, and sanitization fallback.
    /// </summary>
    public static JsonParseResult ParseSafe(string text, ParseOptions? options = null)
        => ParseSafe(text, options, default);

    /// <summary>
    /// Robust parse with normalization, tolerant settings, and sanitization fallback.
    /// </summary>
    public static JsonParseResult ParseSafe(string? text, ParseOptions? options, CancellationToken cancellationToken)
    {
        options ??= new ParseOptions();
        cancellationToken.ThrowIfCancellationRequested();

        var resolve = options.ResolveErrorCode ?? DefaultErrorCodes.Resolve;
        var errors = new List<JsonParseError>();

        if (text is null)
        {
            errors.Add(new JsonParseError
            {
                Code = resolve(ErrorKey.NullInput),
                Severity = ErrorSeverity.Error,
                Message = "Input text is null.",
                Stage = "Initial"
            });
            return new JsonParseResult { Errors = errors, Report = new JsonSanitizationReport { Stage = "Initial", Changed = false } };
        }

        if (options.MaxDocumentLength > 0 && text.Length > options.MaxDocumentLength)
        {
            errors.Add(new JsonParseError
            {
                Code = resolve(ErrorKey.SizeLimitExceeded),
                Severity = ErrorSeverity.Error,
                Message = $"Document exceeds the maximum allowed size of {options.MaxDocumentLength:N0} characters.",
                Stage = "Initial",
                Snippet = BuildSnippet(text, null, null, options.SnippetContextRadius)
            });
            var spansOversized = options.ProduceTokenSpans
                ? new JsonTokenizer(text, cancellationToken).Tokenize()
                : Array.Empty<JsonTokenSpan>();
            return new JsonParseResult
            {
                Root = null,
                Errors = errors,
                TokenSpans = spansOversized,
                Report = new JsonSanitizationReport { Stage = "Initial", Changed = false }
            };
        }

        bool preBomRemoved = false;
        bool preLineNormalized = false;

        if (options.NormalizeLineEndings && text.Length > 0)
        {
            var normalized = text.Replace("\r\n", "\n").Replace("\r", "\n");
            if (!ReferenceEquals(normalized, text) && !string.Equals(normalized, text))
            {
                text = normalized;
                preLineNormalized = true;
            }
            if (text.Length > 0 && text[0] == '\uFEFF')
            {
                text = text.AsSpan(1).ToString();
                preBomRemoved = true;
            }
            if (options.IncludeSanitizationDiagnostics)
            {
                if (preBomRemoved)
                {
                    errors.Add(new JsonParseError
                    {
                        Code = resolve(ErrorKey.BomRemoved),
                        Severity = ErrorSeverity.Info,
                        Message = "Removed UTF-8 BOM.",
                        Stage = "Initial"
                    });
                }
                if (preLineNormalized)
                {
                    errors.Add(new JsonParseError
                    {
                        Code = resolve(ErrorKey.LineEndingsNormalized),
                        Severity = ErrorSeverity.Info,
                        Message = "Normalized line endings to LF.",
                        Stage = "Initial"
                    });
                }
            }
        }

        cancellationToken.ThrowIfCancellationRequested();
        var initial = TryParseSkippingLeadingComments(text, options, "Initial", resolve, cancellationToken);

        if (initial.Root is not null)
        {
            if (options.IncludeSanitizationDiagnostics || options.ReturnSanitizedText)
            {
                var postSanitizer = new JsonSanitizer(
                    removeComments: !options.AllowComments,
                    removeTrailingCommas: options.AllowTrailingCommas,
                    removeControlChars: options.RemoveControlCharacters,
                    normalizeLineEndings: options.NormalizeLineEndings,
                    fixUnterminatedStrings: options.FixUnterminatedStrings,
                    recoverMissingCommas: options.RecoverMissingCommas,
                    recoverMissingClosers: options.RecoverMissingClosers,
                    cancellationToken: cancellationToken);

                var post = postSanitizer.Sanitize(text);

                if (post.Changed && options.IncludeSanitizationDiagnostics)
                {
                    if (post.LineCommentsRemoved + post.BlockCommentsRemoved > 0)
                    {
                        errors.Add(new JsonParseError
                        {
                            Code = resolve(ErrorKey.CommentsRemoved),
                            Severity = ErrorSeverity.Warning,
                            Message = $"Removed {post.LineCommentsRemoved} line comment(s) and {post.BlockCommentsRemoved} block comment(s).",
                            Stage = "Sanitized"
                        });
                    }
                    if (post.TrailingCommasRemoved > 0)
                    {
                        errors.Add(new JsonParseError
                        {
                            Code = resolve(ErrorKey.TrailingCommasRemoved),
                            Severity = ErrorSeverity.Warning,
                            Message = $"Removed {post.TrailingCommasRemoved} trailing comma(s).",
                            Stage = "Sanitized"
                        });
                    }
                    if (post.ControlCharsRemoved > 0)
                    {
                        errors.Add(new JsonParseError
                        {
                            Code = resolve(ErrorKey.ControlCharsRemoved),
                            Severity = ErrorSeverity.Warning,
                            Message = $"Removed {post.ControlCharsRemoved} control character(s).",
                            Stage = "Sanitized"
                        });
                    }
                    if (post.BomRemoved && !preBomRemoved)
                    {
                        errors.Add(new JsonParseError
                        {
                            Code = resolve(ErrorKey.BomRemoved),
                            Severity = ErrorSeverity.Info,
                            Message = "Removed UTF-8 BOM.",
                            Stage = "Sanitized"
                        });
                    }
                    if (post.LineEndingsNormalized && !preLineNormalized)
                    {
                        errors.Add(new JsonParseError
                        {
                            Code = resolve(ErrorKey.LineEndingsNormalized),
                            Severity = ErrorSeverity.Info,
                            Message = "Normalized line endings to LF.",
                            Stage = "Sanitized"
                        });
                    }
                    if (post.UnterminatedStringsClosed > 0)
                    {
                        errors.Add(new JsonParseError
                        {
                            Code = resolve(ErrorKey.UnterminatedStringsClosed),
                            Severity = ErrorSeverity.Warning,
                            Message = $"Closed {post.UnterminatedStringsClosed} unterminated string(s).",
                            Stage = "Sanitized"
                        });
                    }
                    if (post.MissingCommasInserted > 0)
                    {
                        errors.Add(new JsonParseError
                        {
                            Code = resolve(ErrorKey.MissingCommasInserted),
                            Severity = ErrorSeverity.Warning,
                            Message = $"Inserted {post.MissingCommasInserted} missing comma(s).",
                            Stage = "Sanitized"
                        });
                    }
                    if (post.ClosersInserted > 0)
                    {
                        errors.Add(new JsonParseError
                        {
                            Code = resolve(ErrorKey.ClosersInserted),
                            Severity = ErrorSeverity.Warning,
                            Message = $"Inserted {post.ClosersInserted} missing closer(s).",
                            Stage = "Sanitized"
                        });
                    }
                }

                return WithLspArtifacts(new JsonParseResult
                {
                    Root = initial.Root,
                    Errors = Merge(errors, initial.Errors),
                    SanitizedText = options.ReturnSanitizedText ? post.Text : null,
                    Report = new JsonSanitizationReport
                    {
                        Stage = "Sanitized",
                        Changed = post.Changed,
                        LineCommentsRemoved = post.LineCommentsRemoved,
                        BlockCommentsRemoved = post.BlockCommentsRemoved,
                        TrailingCommasRemoved = post.TrailingCommasRemoved,
                        ControlCharsRemoved = post.ControlCharsRemoved,
                        BomRemoved = post.BomRemoved,
                        LineEndingsNormalized = post.LineEndingsNormalized,
                        UnterminatedStringsClosed = post.UnterminatedStringsClosed,
                        MissingCommasInserted = post.MissingCommasInserted,
                        ClosersInserted = post.ClosersInserted
                    }
                }, options, text, cancellationToken);
            }

            return WithLspArtifacts(new JsonParseResult
            {
                Root = initial.Root,
                Errors = Merge(errors, initial.Errors),
                Report = new JsonSanitizationReport { Stage = "Initial", Changed = false }
            }, options, text, cancellationToken);
        }

        errors.AddRange(initial.Errors);
        if (!options.EnableSanitizationFallback)
        {
            return WithLspArtifacts(new JsonParseResult
            {
                Root = null,
                Errors = errors,
                Report = new JsonSanitizationReport { Stage = "Initial", Changed = false }
            }, options, text, cancellationToken);
        }

        var sanitizer = new JsonSanitizer(
            removeComments: !options.AllowComments,
            removeTrailingCommas: options.AllowTrailingCommas,
            removeControlChars: options.RemoveControlCharacters,
            normalizeLineEndings: options.NormalizeLineEndings,
            fixUnterminatedStrings: options.FixUnterminatedStrings,
            recoverMissingCommas: options.RecoverMissingCommas,
            recoverMissingClosers: options.RecoverMissingClosers,
            cancellationToken: cancellationToken);

        var sanitized = sanitizer.Sanitize(text);

        if (options.IncludeSanitizationDiagnostics)
        {
            if (sanitized.LineCommentsRemoved + sanitized.BlockCommentsRemoved > 0)
            {
                errors.Add(new JsonParseError
                {
                    Code = resolve(ErrorKey.CommentsRemoved),
                    Severity = ErrorSeverity.Warning,
                    Message = $"Removed {sanitized.LineCommentsRemoved} line comment(s) and {sanitized.BlockCommentsRemoved} block comment(s).",
                    Stage = "Sanitized"
                });
            }
            if (sanitized.TrailingCommasRemoved > 0)
            {
                errors.Add(new JsonParseError
                {
                    Code = resolve(ErrorKey.TrailingCommasRemoved),
                    Severity = ErrorSeverity.Warning,
                    Message = $"Removed {sanitized.TrailingCommasRemoved} trailing comma(s).",
                    Stage = "Sanitized"
                });
            }
            if (sanitized.ControlCharsRemoved > 0)
            {
                errors.Add(new JsonParseError
                {
                    Code = resolve(ErrorKey.ControlCharsRemoved),
                    Severity = ErrorSeverity.Warning,
                    Message = $"Removed {sanitized.ControlCharsRemoved} control character(s).",
                    Stage = "Sanitized"
                });
            }
            if (sanitized.BomRemoved && !preBomRemoved)
            {
                errors.Add(new JsonParseError
                {
                    Code = resolve(ErrorKey.BomRemoved),
                    Severity = ErrorSeverity.Info,
                    Message = "Removed UTF-8 BOM.",
                    Stage = "Sanitized"
                });
            }
            if (sanitized.LineEndingsNormalized && !preLineNormalized)
            {
                errors.Add(new JsonParseError
                {
                    Code = resolve(ErrorKey.LineEndingsNormalized),
                    Severity = ErrorSeverity.Info,
                    Message = "Normalized line endings to LF.",
                    Stage = "Sanitized"
                });
            }
            if (sanitized.UnterminatedStringsClosed > 0)
            {
                errors.Add(new JsonParseError
                {
                    Code = resolve(ErrorKey.UnterminatedStringsClosed),
                    Severity = ErrorSeverity.Warning,
                    Message = $"Closed {sanitized.UnterminatedStringsClosed} unterminated string(s).",
                    Stage = "Sanitized"
                });
            }
            if (sanitized.MissingCommasInserted > 0)
            {
                errors.Add(new JsonParseError
                {
                    Code = resolve(ErrorKey.MissingCommasInserted),
                    Severity = ErrorSeverity.Warning,
                    Message = $"Inserted {sanitized.MissingCommasInserted} missing comma(s).",
                    Stage = "Sanitized"
                });
            }
            if (sanitized.ClosersInserted > 0)
            {
                errors.Add(new JsonParseError
                {
                    Code = resolve(ErrorKey.ClosersInserted),
                    Severity = ErrorSeverity.Warning,
                    Message = $"Inserted {sanitized.ClosersInserted} missing closer(s).",
                    Stage = "Sanitized"
                });
            }
        }

        cancellationToken.ThrowIfCancellationRequested();
        var sanitizedAttempt = TryParseSkippingLeadingComments(
            sanitized.Text,
            options with { AllowComments = false },
            "Sanitized",
            resolve,
            cancellationToken);

        if (sanitizedAttempt.Root is not null)
        {
            return WithLspArtifacts(new JsonParseResult
            {
                Root = sanitizedAttempt.Root,
                Errors = Merge(errors, sanitizedAttempt.Errors),
                SanitizedText = options.ReturnSanitizedText && sanitized.Changed ? sanitized.Text : null,
                Report = new JsonSanitizationReport
                {
                    Stage = "Sanitized",
                    Changed = sanitized.Changed,
                    LineCommentsRemoved = sanitized.LineCommentsRemoved,
                    BlockCommentsRemoved = sanitized.BlockCommentsRemoved,
                    TrailingCommasRemoved = sanitized.TrailingCommasRemoved,
                    ControlCharsRemoved = sanitized.ControlCharsRemoved,
                    BomRemoved = sanitized.BomRemoved,
                    LineEndingsNormalized = sanitized.LineEndingsNormalized,
                    UnterminatedStringsClosed = sanitized.UnterminatedStringsClosed,
                    MissingCommasInserted = sanitized.MissingCommasInserted,
                    ClosersInserted = sanitized.ClosersInserted
                }
            }, options, sanitized.Text, cancellationToken);
        }

        errors.AddRange(sanitizedAttempt.Errors);
        if (!options.EnableAggressiveRecovery)
        {
            return WithLspArtifacts(new JsonParseResult
            {
                Root = null,
                Errors = errors,
                SanitizedText = options.ReturnSanitizedText && sanitized.Changed ? sanitized.Text : null,
                Report = new JsonSanitizationReport
                {
                    Stage = "Sanitized",
                    Changed = sanitized.Changed,
                    LineCommentsRemoved = sanitized.LineCommentsRemoved,
                    BlockCommentsRemoved = sanitized.BlockCommentsRemoved,
                    TrailingCommasRemoved = sanitized.TrailingCommasRemoved,
                    ControlCharsRemoved = sanitized.ControlCharsRemoved,
                    BomRemoved = sanitized.BomRemoved,
                    LineEndingsNormalized = sanitized.LineEndingsNormalized,
                    UnterminatedStringsClosed = sanitized.UnterminatedStringsClosed,
                    MissingCommasInserted = sanitized.MissingCommasInserted,
                    ClosersInserted = sanitized.ClosersInserted
                }
            }, options, sanitized.Text, cancellationToken);
        }

        var aggressiveSanitizer = new JsonSanitizer(
            removeComments: true,
            removeTrailingCommas: true,
            removeControlChars: true,
            normalizeLineEndings: true,
            fixUnterminatedStrings: true,
            recoverMissingCommas: true,
            recoverMissingClosers: true,
            cancellationToken: cancellationToken);

        var aggressive = aggressiveSanitizer.Sanitize(text);

        if (options.IncludeSanitizationDiagnostics)
        {
            if (aggressive.LineCommentsRemoved + aggressive.BlockCommentsRemoved > 0)
            {
                errors.Add(new JsonParseError
                {
                    Code = resolve(ErrorKey.CommentsRemoved),
                    Severity = ErrorSeverity.Warning,
                    Message = $"Removed {aggressive.LineCommentsRemoved} line comment(s) and {aggressive.BlockCommentsRemoved} block comment(s).",
                    Stage = "Aggressive"
                });
            }
            if (aggressive.TrailingCommasRemoved > 0)
            {
                errors.Add(new JsonParseError
                {
                    Code = resolve(ErrorKey.TrailingCommasRemoved),
                    Severity = ErrorSeverity.Warning,
                    Message = $"Removed {aggressive.TrailingCommasRemoved} trailing comma(s).",
                    Stage = "Aggressive"
                });
            }
            if (aggressive.ControlCharsRemoved > 0)
            {
                errors.Add(new JsonParseError
                {
                    Code = resolve(ErrorKey.ControlCharsRemoved),
                    Severity = ErrorSeverity.Warning,
                    Message = $"Removed {aggressive.ControlCharsRemoved} control character(s).",
                    Stage = "Aggressive"
                });
            }
            if (aggressive.BomRemoved && !preBomRemoved)
            {
                errors.Add(new JsonParseError
                {
                    Code = resolve(ErrorKey.BomRemoved),
                    Severity = ErrorSeverity.Info,
                    Message = "Removed UTF-8 BOM.",
                    Stage = "Aggressive"
                });
            }
            if (aggressive.LineEndingsNormalized && !preLineNormalized)
            {
                errors.Add(new JsonParseError
                {
                    Code = resolve(ErrorKey.LineEndingsNormalized),
                    Severity = ErrorSeverity.Info,
                    Message = "Normalized line endings to LF.",
                    Stage = "Aggressive"
                });
            }
            if (aggressive.UnterminatedStringsClosed > 0)
            {
                errors.Add(new JsonParseError
                {
                    Code = resolve(ErrorKey.UnterminatedStringsClosed),
                    Severity = ErrorSeverity.Warning,
                    Message = $"Closed {aggressive.UnterminatedStringsClosed} unterminated string(s).",
                    Stage = "Aggressive"
                });
            }
            if (aggressive.MissingCommasInserted > 0)
            {
                errors.Add(new JsonParseError
                {
                    Code = resolve(ErrorKey.MissingCommasInserted),
                    Severity = ErrorSeverity.Warning,
                    Message = $"Inserted {aggressive.MissingCommasInserted} missing comma(s).",
                    Stage = "Aggressive"
                });
            }
            if (aggressive.ClosersInserted > 0)
            {
                errors.Add(new JsonParseError
                {
                    Code = resolve(ErrorKey.ClosersInserted),
                    Severity = ErrorSeverity.Warning,
                    Message = $"Inserted {aggressive.ClosersInserted} missing closer(s).",
                    Stage = "Aggressive"
                });
            }
        }

        cancellationToken.ThrowIfCancellationRequested();
        var aggressiveAttempt = TryParseSkippingLeadingComments(
            aggressive.Text,
            options with { AllowComments = false },
            "Aggressive",
            resolve,
            cancellationToken);

        if (aggressiveAttempt.Root is not null)
        {
            return WithLspArtifacts(new JsonParseResult
            {
                Root = aggressiveAttempt.Root,
                Errors = Merge(errors, aggressiveAttempt.Errors),
                SanitizedText = options.ReturnSanitizedText ? aggressive.Text : null,
                Report = new JsonSanitizationReport
                {
                    Stage = "Aggressive",
                    Changed = aggressive.Changed,
                    LineCommentsRemoved = aggressive.LineCommentsRemoved,
                    BlockCommentsRemoved = aggressive.BlockCommentsRemoved,
                    TrailingCommasRemoved = aggressive.TrailingCommasRemoved,
                    ControlCharsRemoved = aggressive.ControlCharsRemoved,
                    BomRemoved = aggressive.BomRemoved,
                    LineEndingsNormalized = aggressive.LineEndingsNormalized,
                    UnterminatedStringsClosed = aggressive.UnterminatedStringsClosed,
                    MissingCommasInserted = aggressive.MissingCommasInserted,
                    ClosersInserted = aggressive.ClosersInserted
                }
            }, options, aggressive.Text, cancellationToken);
        }

        errors.AddRange(aggressiveAttempt.Errors);

        return WithLspArtifacts(new JsonParseResult
        {
            Root = null,
            Errors = errors,
            SanitizedText = options.ReturnSanitizedText
                ? aggressive.Text
                : (options.ReturnSanitizedText ? sanitized.Text : null),
            Report = new JsonSanitizationReport
            {
                Stage = "Aggressive",
                Changed = aggressive.Changed,
                LineCommentsRemoved = aggressive.LineCommentsRemoved,
                BlockCommentsRemoved = aggressive.BlockCommentsRemoved,
                TrailingCommasRemoved = aggressive.TrailingCommasRemoved,
                ControlCharsRemoved = aggressive.ControlCharsRemoved,
                BomRemoved = aggressive.BomRemoved,
                LineEndingsNormalized = aggressive.LineEndingsNormalized,
                UnterminatedStringsClosed = aggressive.UnterminatedStringsClosed,
                MissingCommasInserted = aggressive.MissingCommasInserted,
                ClosersInserted = aggressive.ClosersInserted
            }
        }, options, aggressive.Text, cancellationToken);
    }

    /// <summary>
    /// Asynchronously parses JSON with normalization, tolerant settings, and async sanitization fallback.
    /// </summary>
    public static Task<JsonParseResult> ParseSafeAsync(string text, ParseOptions? options = null, CancellationToken cancellationToken = default)
        => ParseSafeCoreAsync(text, options, cancellationToken);

    private static async Task<JsonParseResult> ParseSafeCoreAsync(string? text, ParseOptions? options, CancellationToken cancellationToken)
    {
        options ??= new ParseOptions();
        cancellationToken.ThrowIfCancellationRequested();

        var resolve = options.ResolveErrorCode ?? DefaultErrorCodes.Resolve;
        var errors = new List<JsonParseError>();

        if (text is null)
        {
            errors.Add(new JsonParseError
            {
                Code = resolve(ErrorKey.NullInput),
                Severity = ErrorSeverity.Error,
                Message = "Input text is null.",
                Stage = "Initial"
            });
            return new JsonParseResult { Errors = errors, Report = new JsonSanitizationReport { Stage = "Initial", Changed = false } };
        }

        if (options.MaxDocumentLength > 0 && text.Length > options.MaxDocumentLength)
        {
            errors.Add(new JsonParseError
            {
                Code = resolve(ErrorKey.SizeLimitExceeded),
                Severity = ErrorSeverity.Error,
                Message = $"Document exceeds the maximum allowed size of {options.MaxDocumentLength:N0} characters.",
                Stage = "Initial",
                Snippet = BuildSnippet(text, null, null, options.SnippetContextRadius)
            });
            var spansOversized = options.ProduceTokenSpans
                ? await Task.Run(() => new JsonTokenizer(text, cancellationToken).Tokenize(), cancellationToken)
                : Array.Empty<JsonTokenSpan>();
            return new JsonParseResult
            {
                Root = null,
                Errors = errors,
                TokenSpans = spansOversized,
                Report = new JsonSanitizationReport { Stage = "Initial", Changed = false }
            };
        }

        bool preBomRemoved = false;
        bool preLineNormalized = false;

        if (options.NormalizeLineEndings && text.Length > 0)
        {
            var normalized = text.Replace("\r\n", "\n").Replace("\r", "\n");
            if (!ReferenceEquals(normalized, text) && !string.Equals(normalized, text))
            {
                text = normalized;
                preLineNormalized = true;
            }
            if (text.Length > 0 && text[0] == '\uFEFF')
            {
                text = text.AsSpan(1).ToString();
                preBomRemoved = true;
            }
            if (options.IncludeSanitizationDiagnostics)
            {
                if (preBomRemoved)
                {
                    errors.Add(new JsonParseError
                    {
                        Code = resolve(ErrorKey.BomRemoved),
                        Severity = ErrorSeverity.Info,
                        Message = "Removed UTF-8 BOM.",
                        Stage = "Initial"
                    });
                }
                if (preLineNormalized)
                {
                    errors.Add(new JsonParseError
                    {
                        Code = resolve(ErrorKey.LineEndingsNormalized),
                        Severity = ErrorSeverity.Info,
                        Message = "Normalized line endings to LF.",
                        Stage = "Initial"
                    });
                }
            }
        }

        cancellationToken.ThrowIfCancellationRequested();
        var initial = await TryParseSkippingLeadingCommentsAsync(text, options, "Initial", resolve, cancellationToken);

        if (initial.Root is not null)
        {
            if (options.IncludeSanitizationDiagnostics || options.ReturnSanitizedText)
            {
                var postSanitizer = new JsonSanitizer(
                    removeComments: !options.AllowComments,
                    removeTrailingCommas: options.AllowTrailingCommas,
                    removeControlChars: options.RemoveControlCharacters,
                    normalizeLineEndings: options.NormalizeLineEndings,
                    fixUnterminatedStrings: options.FixUnterminatedStrings,
                    recoverMissingCommas: options.RecoverMissingCommas,
                    recoverMissingClosers: options.RecoverMissingClosers,
                    cancellationToken: cancellationToken);

                var post = await postSanitizer.SanitizeAsync(text);

                if (post.Changed && options.IncludeSanitizationDiagnostics)
                {
                    if (post.LineCommentsRemoved + post.BlockCommentsRemoved > 0)
                    {
                        errors.Add(new JsonParseError
                        {
                            Code = resolve(ErrorKey.CommentsRemoved),
                            Severity = ErrorSeverity.Warning,
                            Message = $"Removed {post.LineCommentsRemoved} line comment(s) and {post.BlockCommentsRemoved} block comment(s).",
                            Stage = "Sanitized"
                        });
                    }
                    if (post.TrailingCommasRemoved > 0)
                    {
                        errors.Add(new JsonParseError
                        {
                            Code = resolve(ErrorKey.TrailingCommasRemoved),
                            Severity = ErrorSeverity.Warning,
                            Message = $"Removed {post.TrailingCommasRemoved} trailing comma(s).",
                            Stage = "Sanitized"
                        });
                    }
                    if (post.ControlCharsRemoved > 0)
                    {
                        errors.Add(new JsonParseError
                        {
                            Code = resolve(ErrorKey.ControlCharsRemoved),
                            Severity = ErrorSeverity.Warning,
                            Message = $"Removed {post.ControlCharsRemoved} control character(s).",
                            Stage = "Sanitized"
                        });
                    }
                    if (post.BomRemoved && !preBomRemoved)
                    {
                        errors.Add(new JsonParseError
                        {
                            Code = resolve(ErrorKey.BomRemoved),
                            Severity = ErrorSeverity.Info,
                            Message = "Removed UTF-8 BOM.",
                            Stage = "Sanitized"
                        });
                    }
                    if (post.LineEndingsNormalized && !preLineNormalized)
                    {
                        errors.Add(new JsonParseError
                        {
                            Code = resolve(ErrorKey.LineEndingsNormalized),
                            Severity = ErrorSeverity.Info,
                            Message = "Normalized line endings to LF.",
                            Stage = "Sanitized"
                        });
                    }
                    if (post.UnterminatedStringsClosed > 0)
                    {
                        errors.Add(new JsonParseError
                        {
                            Code = resolve(ErrorKey.UnterminatedStringsClosed),
                            Severity = ErrorSeverity.Warning,
                            Message = $"Closed {post.UnterminatedStringsClosed} unterminated string(s).",
                            Stage = "Sanitized"
                        });
                    }
                    if (post.MissingCommasInserted > 0)
                    {
                        errors.Add(new JsonParseError
                        {
                            Code = resolve(ErrorKey.MissingCommasInserted),
                            Severity = ErrorSeverity.Warning,
                            Message = $"Inserted {post.MissingCommasInserted} missing comma(s).",
                            Stage = "Sanitized"
                        });
                    }
                    if (post.ClosersInserted > 0)
                    {
                        errors.Add(new JsonParseError
                        {
                            Code = resolve(ErrorKey.ClosersInserted),
                            Severity = ErrorSeverity.Warning,
                            Message = $"Inserted {post.ClosersInserted} missing closer(s).",
                            Stage = "Sanitized"
                        });
                    }
                }

                return await WithLspArtifactsAsync(new JsonParseResult
                {
                    Root = initial.Root,
                    Errors = Merge(errors, initial.Errors),
                    SanitizedText = options.ReturnSanitizedText ? post.Text : null,
                    Report = new JsonSanitizationReport
                    {
                        Stage = "Sanitized",
                        Changed = post.Changed,
                        LineCommentsRemoved = post.LineCommentsRemoved,
                        BlockCommentsRemoved = post.BlockCommentsRemoved,
                        TrailingCommasRemoved = post.TrailingCommasRemoved,
                        ControlCharsRemoved = post.ControlCharsRemoved,
                        BomRemoved = post.BomRemoved,
                        LineEndingsNormalized = post.LineEndingsNormalized,
                        UnterminatedStringsClosed = post.UnterminatedStringsClosed,
                        MissingCommasInserted = post.MissingCommasInserted,
                        ClosersInserted = post.ClosersInserted
                    }
                }, options, text, cancellationToken);
            }

            return await WithLspArtifactsAsync(new JsonParseResult
            {
                Root = initial.Root,
                Errors = Merge(errors, initial.Errors),
                Report = new JsonSanitizationReport { Stage = "Initial", Changed = false }
            }, options, text, cancellationToken);
        }

        errors.AddRange(initial.Errors);
        if (!options.EnableSanitizationFallback)
        {
            return await WithLspArtifactsAsync(new JsonParseResult
            {
                Root = null,
                Errors = errors,
                Report = new JsonSanitizationReport { Stage = "Initial", Changed = false }
            }, options, text, cancellationToken);
        }

        var sanitizer = new JsonSanitizer(
            removeComments: !options.AllowComments,
            removeTrailingCommas: options.AllowTrailingCommas,
            removeControlChars: options.RemoveControlCharacters,
            normalizeLineEndings: options.NormalizeLineEndings,
            fixUnterminatedStrings: options.FixUnterminatedStrings,
            recoverMissingCommas: options.RecoverMissingCommas,
            recoverMissingClosers: options.RecoverMissingClosers,
            cancellationToken: cancellationToken);

        var sanitized = await sanitizer.SanitizeAsync(text);

        if (options.IncludeSanitizationDiagnostics)
        {
            if (sanitized.LineCommentsRemoved + sanitized.BlockCommentsRemoved > 0)
            {
                errors.Add(new JsonParseError
                {
                    Code = resolve(ErrorKey.CommentsRemoved),
                    Severity = ErrorSeverity.Warning,
                    Message = $"Removed {sanitized.LineCommentsRemoved} line comment(s) and {sanitized.BlockCommentsRemoved} block comment(s).",
                    Stage = "Sanitized"
                });
            }
            if (sanitized.TrailingCommasRemoved > 0)
            {
                errors.Add(new JsonParseError
                {
                    Code = resolve(ErrorKey.TrailingCommasRemoved),
                    Severity = ErrorSeverity.Warning,
                    Message = $"Removed {sanitized.TrailingCommasRemoved} trailing comma(s).",
                    Stage = "Sanitized"
                });
            }
            if (sanitized.ControlCharsRemoved > 0)
            {
                errors.Add(new JsonParseError
                {
                    Code = resolve(ErrorKey.ControlCharsRemoved),
                    Severity = ErrorSeverity.Warning,
                    Message = $"Removed {sanitized.ControlCharsRemoved} control character(s).",
                    Stage = "Sanitized"
                });
            }
            if (sanitized.BomRemoved && !preBomRemoved)
            {
                errors.Add(new JsonParseError
                {
                    Code = resolve(ErrorKey.BomRemoved),
                    Severity = ErrorSeverity.Info,
                    Message = "Removed UTF-8 BOM.",
                    Stage = "Sanitized"
                });
            }
            if (sanitized.LineEndingsNormalized && !preLineNormalized)
            {
                errors.Add(new JsonParseError
                {
                    Code = resolve(ErrorKey.LineEndingsNormalized),
                    Severity = ErrorSeverity.Info,
                    Message = "Normalized line endings to LF.",
                    Stage = "Sanitized"
                });
            }
            if (sanitized.UnterminatedStringsClosed > 0)
            {
                errors.Add(new JsonParseError
                {
                    Code = resolve(ErrorKey.UnterminatedStringsClosed),
                    Severity = ErrorSeverity.Warning,
                    Message = $"Closed {sanitized.UnterminatedStringsClosed} unterminated string(s).",
                    Stage = "Sanitized"
                });
            }
            if (sanitized.MissingCommasInserted > 0)
            {
                errors.Add(new JsonParseError
                {
                    Code = resolve(ErrorKey.MissingCommasInserted),
                    Severity = ErrorSeverity.Warning,
                    Message = $"Inserted {sanitized.MissingCommasInserted} missing comma(s).",
                    Stage = "Sanitized"
                });
            }
            if (sanitized.ClosersInserted > 0)
            {
                errors.Add(new JsonParseError
                {
                    Code = resolve(ErrorKey.ClosersInserted),
                    Severity = ErrorSeverity.Warning,
                    Message = $"Inserted {sanitized.ClosersInserted} missing closer(s).",
                    Stage = "Sanitized"
                });
            }
        }

        cancellationToken.ThrowIfCancellationRequested();
        var sanitizedAttempt = await TryParseSkippingLeadingCommentsAsync(
            sanitized.Text,
            options with { AllowComments = false },
            "Sanitized",
            resolve,
            cancellationToken);

        if (sanitizedAttempt.Root is not null)
        {
            return await WithLspArtifactsAsync(new JsonParseResult
            {
                Root = sanitizedAttempt.Root,
                Errors = Merge(errors, sanitizedAttempt.Errors),
                SanitizedText = options.ReturnSanitizedText && sanitized.Changed ? sanitized.Text : null,
                Report = new JsonSanitizationReport
                {
                    Stage = "Sanitized",
                    Changed = sanitized.Changed,
                    LineCommentsRemoved = sanitized.LineCommentsRemoved,
                    BlockCommentsRemoved = sanitized.BlockCommentsRemoved,
                    TrailingCommasRemoved = sanitized.TrailingCommasRemoved,
                    ControlCharsRemoved = sanitized.ControlCharsRemoved,
                    BomRemoved = sanitized.BomRemoved,
                    LineEndingsNormalized = sanitized.LineEndingsNormalized,
                    UnterminatedStringsClosed = sanitized.UnterminatedStringsClosed,
                    MissingCommasInserted = sanitized.MissingCommasInserted,
                    ClosersInserted = sanitized.ClosersInserted
                }
            }, options, sanitized.Text, cancellationToken);
        }

        errors.AddRange(sanitizedAttempt.Errors);
        if (!options.EnableAggressiveRecovery)
        {
            return await WithLspArtifactsAsync(new JsonParseResult
            {
                Root = null,
                Errors = errors,
                SanitizedText = options.ReturnSanitizedText && sanitized.Changed ? sanitized.Text : null,
                Report = new JsonSanitizationReport
                {
                    Stage = "Sanitized",
                    Changed = sanitized.Changed,
                    LineCommentsRemoved = sanitized.LineCommentsRemoved,
                    BlockCommentsRemoved = sanitized.BlockCommentsRemoved,
                    TrailingCommasRemoved = sanitized.TrailingCommasRemoved,
                    ControlCharsRemoved = sanitized.ControlCharsRemoved,
                    BomRemoved = sanitized.BomRemoved,
                    LineEndingsNormalized = sanitized.LineEndingsNormalized,
                    UnterminatedStringsClosed = sanitized.UnterminatedStringsClosed,
                    MissingCommasInserted = sanitized.MissingCommasInserted,
                    ClosersInserted = sanitized.ClosersInserted
                }
            }, options, sanitized.Text, cancellationToken);
        }

        var aggressiveSanitizer = new JsonSanitizer(
            removeComments: true,
            removeTrailingCommas: true,
            removeControlChars: true,
            normalizeLineEndings: true,
            fixUnterminatedStrings: true,
            recoverMissingCommas: true,
            recoverMissingClosers: true,
            cancellationToken: cancellationToken);

        var aggressive = await aggressiveSanitizer.SanitizeAsync(text);

        if (options.IncludeSanitizationDiagnostics)
        {
            if (aggressive.LineCommentsRemoved + aggressive.BlockCommentsRemoved > 0)
            {
                errors.Add(new JsonParseError
                {
                    Code = resolve(ErrorKey.CommentsRemoved),
                    Severity = ErrorSeverity.Warning,
                    Message = $"Removed {aggressive.LineCommentsRemoved} line comment(s) and {aggressive.BlockCommentsRemoved} block comment(s).",
                    Stage = "Aggressive"
                });
            }
            if (aggressive.TrailingCommasRemoved > 0)
            {
                errors.Add(new JsonParseError
                {
                    Code = resolve(ErrorKey.TrailingCommasRemoved),
                    Severity = ErrorSeverity.Warning,
                    Message = $"Removed {aggressive.TrailingCommasRemoved} trailing comma(s).",
                    Stage = "Aggressive"
                });
            }
            if (aggressive.ControlCharsRemoved > 0)
            {
                errors.Add(new JsonParseError
                {
                    Code = resolve(ErrorKey.ControlCharsRemoved),
                    Severity = ErrorSeverity.Warning,
                    Message = $"Removed {aggressive.ControlCharsRemoved} control character(s).",
                    Stage = "Aggressive"
                });
            }
            if (aggressive.BomRemoved && !preBomRemoved)
            {
                errors.Add(new JsonParseError
                {
                    Code = resolve(ErrorKey.BomRemoved),
                    Severity = ErrorSeverity.Info,
                    Message = "Removed UTF-8 BOM.",
                    Stage = "Aggressive"
                });
            }
            if (aggressive.LineEndingsNormalized && !preLineNormalized)
            {
                errors.Add(new JsonParseError
                {
                    Code = resolve(ErrorKey.LineEndingsNormalized),
                    Severity = ErrorSeverity.Info,
                    Message = "Normalized line endings to LF.",
                    Stage = "Aggressive"
                });
            }
            if (aggressive.UnterminatedStringsClosed > 0)
            {
                errors.Add(new JsonParseError
                {
                    Code = resolve(ErrorKey.UnterminatedStringsClosed),
                    Severity = ErrorSeverity.Warning,
                    Message = $"Closed {aggressive.UnterminatedStringsClosed} unterminated string(s).",
                    Stage = "Aggressive"
                });
            }
            if (aggressive.MissingCommasInserted > 0)
            {
                errors.Add(new JsonParseError
                {
                    Code = resolve(ErrorKey.MissingCommasInserted),
                    Severity = ErrorSeverity.Warning,
                    Message = $"Inserted {aggressive.MissingCommasInserted} missing comma(s).",
                    Stage = "Aggressive"
                });
            }
            if (aggressive.ClosersInserted > 0)
            {
                errors.Add(new JsonParseError
                {
                    Code = resolve(ErrorKey.ClosersInserted),
                    Severity = ErrorSeverity.Warning,
                    Message = $"Inserted {aggressive.ClosersInserted} missing closer(s).",
                    Stage = "Aggressive"
                });
            }
        }

        cancellationToken.ThrowIfCancellationRequested();
        var aggressiveAttempt = await TryParseSkippingLeadingCommentsAsync(
            aggressive.Text,
            options with { AllowComments = false },
            "Aggressive",
            resolve,
            cancellationToken);

        if (aggressiveAttempt.Root is not null)
        {
            return await WithLspArtifactsAsync(new JsonParseResult
            {
                Root = aggressiveAttempt.Root,
                Errors = Merge(errors, aggressiveAttempt.Errors),
                SanitizedText = options.ReturnSanitizedText ? aggressive.Text : null,
                Report = new JsonSanitizationReport
                {
                    Stage = "Aggressive",
                    Changed = aggressive.Changed,
                    LineCommentsRemoved = aggressive.LineCommentsRemoved,
                    BlockCommentsRemoved = aggressive.BlockCommentsRemoved,
                    TrailingCommasRemoved = aggressive.TrailingCommasRemoved,
                    ControlCharsRemoved = aggressive.ControlCharsRemoved,
                    BomRemoved = aggressive.BomRemoved,
                    LineEndingsNormalized = aggressive.LineEndingsNormalized,
                    UnterminatedStringsClosed = aggressive.UnterminatedStringsClosed,
                    MissingCommasInserted = aggressive.MissingCommasInserted,
                    ClosersInserted = aggressive.ClosersInserted
                }
            }, options, aggressive.Text, cancellationToken);
        }

        errors.AddRange(aggressiveAttempt.Errors);

        return await WithLspArtifactsAsync(new JsonParseResult
        {
            Root = null,
            Errors = errors,
            SanitizedText = options.ReturnSanitizedText ? aggressive.Text : null,
            Report = new JsonSanitizationReport
            {
                Stage = "Aggressive",
                Changed = aggressive.Changed,
                LineCommentsRemoved = aggressive.LineCommentsRemoved,
                BlockCommentsRemoved = aggressive.BlockCommentsRemoved,
                TrailingCommasRemoved = aggressive.TrailingCommasRemoved,
                ControlCharsRemoved = aggressive.ControlCharsRemoved,
                BomRemoved = aggressive.BomRemoved,
                LineEndingsNormalized = aggressive.LineEndingsNormalized,
                UnterminatedStringsClosed = aggressive.UnterminatedStringsClosed,
                MissingCommasInserted = aggressive.MissingCommasInserted,
                ClosersInserted = aggressive.ClosersInserted
            }
        }, options, aggressive.Text, cancellationToken);
    }

    private static JsonParseResult TryParseSkippingLeadingComments(
        string text,
        ParseOptions options,
        string stage,
        Func<ErrorKey, string> resolve,
        CancellationToken cancellationToken)
    {
        var errors = new List<JsonParseError>();
        var loadSettings = new JsonLoadSettings
        {
            CommentHandling = options.AllowComments ? CommentHandling.Load : CommentHandling.Ignore,
            LineInfoHandling = options.CollectLineInfo ? LineInfoHandling.Load : LineInfoHandling.Ignore,
            DuplicatePropertyNameHandling = options.DuplicatePropertyHandling switch
            {
                DuplicateKeyStrategy.Error => DuplicatePropertyNameHandling.Error,
                DuplicateKeyStrategy.KeepFirst => DuplicatePropertyNameHandling.Ignore,
                DuplicateKeyStrategy.OverwriteWithLast => DuplicatePropertyNameHandling.Replace,
                _ => DuplicatePropertyNameHandling.Replace
            }
        };

        try
        {
            using var sr = new StringReader(text);
            using var reader = new JsonTextReader(sr);
            reader.SupportMultipleContent = true;
            reader.MaxDepth = options.MaxDepth > 0 ? options.MaxDepth : null;

            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (!reader.Read())
                {
                    return new JsonParseResult
                    {
                        Root = null,
                        Errors = new List<JsonParseError>
                    {
                        new()
                        {
                            Code = resolve(ErrorKey.NoContent),
                            Severity = ErrorSeverity.Error,
                            Message = "No JSON content found.",
                            Stage = stage,
                            Snippet = BuildSnippet(text, null, null, options.SnippetContextRadius)
                        }
                    }
                    };
                }

                if (reader.TokenType == JsonToken.Comment)
                {
                    continue;
                }

                var token = JToken.ReadFrom(reader, loadSettings);
                return new JsonParseResult
                {
                    Root = token,
                    Errors = errors
                };
            }
        }
        catch (JsonReaderException ex)
        {
            bool duplicate =
                options.DuplicatePropertyHandling == DuplicateKeyStrategy.Error &&
                (ex.Message.Contains("duplicate", StringComparison.OrdinalIgnoreCase)
                 ||
                 (ex.Message.Contains("already", StringComparison.OrdinalIgnoreCase)
                  && ex.Message.Contains("exists", StringComparison.OrdinalIgnoreCase)));

            var key =
                duplicate ? ErrorKey.DuplicateKey
                : (ex.Message.Contains("is too deep", StringComparison.OrdinalIgnoreCase)
                   || ex.Message.Contains("MaxDepth", StringComparison.OrdinalIgnoreCase))
                ? ErrorKey.DepthLimitExceeded
                : ErrorKey.InvalidToken;

            var lspRange = ex is { LineNumber: > 0, LinePosition: > 0 }
                ? TextRange.FromOneBased(ex.LineNumber, ex.LinePosition)
                : null;

            errors.Add(new JsonParseError
            {
                Code = resolve(key),
                Severity = ErrorSeverity.Error,
                Message = ex.Message,
                LineNumber = ex.LineNumber,
                LinePosition = ex.LinePosition,
                Path = ex.Path,
                Stage = stage,
                Snippet = BuildSnippet(text, ex.LineNumber, ex.LinePosition, options.SnippetContextRadius),
                Range = lspRange
            });
            return new JsonParseResult { Root = null, Errors = errors };
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            errors.Add(new JsonParseError
            {
                Code = resolve(ErrorKey.Exception),
                Severity = ErrorSeverity.Error,
                Message = ex.Message,
                Stage = stage,
                Snippet = BuildSnippet(text, null, null, options.SnippetContextRadius)
            });
            return new JsonParseResult { Root = null, Errors = errors };
        }
    }

    private static async Task<JsonParseResult> TryParseSkippingLeadingCommentsAsync(
        string text,
        ParseOptions options,
        string stage,
        Func<ErrorKey, string> resolve,
        CancellationToken cancellationToken)
    {
        var errors = new List<JsonParseError>();
        var loadSettings = new JsonLoadSettings
        {
            CommentHandling = options.AllowComments ? CommentHandling.Load : CommentHandling.Ignore,
            LineInfoHandling = options.CollectLineInfo ? LineInfoHandling.Load : LineInfoHandling.Ignore,
            DuplicatePropertyNameHandling = options.DuplicatePropertyHandling switch
            {
                DuplicateKeyStrategy.Error => DuplicatePropertyNameHandling.Error,
                DuplicateKeyStrategy.KeepFirst => DuplicatePropertyNameHandling.Ignore,
                DuplicateKeyStrategy.OverwriteWithLast => DuplicatePropertyNameHandling.Replace,
                _ => DuplicatePropertyNameHandling.Replace
            }
        };

        try
        {
            using var sr = new StringReader(text);
            await using var reader = new JsonTextReader(sr);
            reader.SupportMultipleContent = true;
            reader.MaxDepth = options.MaxDepth > 0 ? options.MaxDepth : null;

            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    return new JsonParseResult
                    {
                        Root = null,
                        Errors = new List<JsonParseError>
                    {
                        new()
                        {
                            Code = resolve(ErrorKey.NoContent),
                            Severity = ErrorSeverity.Error,
                            Message = "No JSON content found.",
                            Stage = stage,
                            Snippet = BuildSnippet(text, null, null, options.SnippetContextRadius)
                        }
                    }
                    };
                }

                if (reader.TokenType == JsonToken.Comment)
                {
                    continue;
                }

                var token = await JToken.ReadFromAsync(reader, loadSettings, cancellationToken).ConfigureAwait(false);
                return new JsonParseResult
                {
                    Root = token,
                    Errors = errors
                };
            }
        }
        catch (JsonReaderException ex)
        {
            bool duplicate =
                options.DuplicatePropertyHandling == DuplicateKeyStrategy.Error &&
                (ex.Message.Contains("duplicate", StringComparison.OrdinalIgnoreCase)
                 ||
                 (ex.Message.Contains("already", StringComparison.OrdinalIgnoreCase)
                  && ex.Message.Contains("exists", StringComparison.OrdinalIgnoreCase)));

            var key =
                duplicate ? ErrorKey.DuplicateKey
                : (ex.Message.Contains("is too deep", StringComparison.OrdinalIgnoreCase)
                   || ex.Message.Contains("MaxDepth", StringComparison.OrdinalIgnoreCase))
                ? ErrorKey.DepthLimitExceeded
                : ErrorKey.InvalidToken;

            var lspRange = ex is { LineNumber: > 0, LinePosition: > 0 }
                ? TextRange.FromOneBased(ex.LineNumber, ex.LinePosition)
                : null;

            errors.Add(new JsonParseError
            {
                Code = resolve(key),
                Severity = ErrorSeverity.Error,
                Message = ex.Message,
                LineNumber = ex.LineNumber,
                LinePosition = ex.LinePosition,
                Path = ex.Path,
                Stage = stage,
                Snippet = BuildSnippet(text, ex.LineNumber, ex.LinePosition, options.SnippetContextRadius),
                Range = lspRange
            });
            return new JsonParseResult { Root = null, Errors = errors };
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            errors.Add(new JsonParseError
            {
                Code = resolve(ErrorKey.Exception),
                Severity = ErrorSeverity.Error,
                Message = ex.Message,
                Stage = stage,
                Snippet = BuildSnippet(text, null, null, options.SnippetContextRadius)
            });
            return new JsonParseResult { Root = null, Errors = errors };
        }
    }

    private static JsonParseResult WithLspArtifacts(
        JsonParseResult baseResult,
        ParseOptions options,
        string usedText,
        CancellationToken cancellationToken)
    {
        if (options is { ProduceTokenSpans: false, ProducePathMap: false })
            return baseResult;

        var spans = options.ProduceTokenSpans
            ? new JsonTokenizer(usedText, cancellationToken, options.TokenSpanLimit).Tokenize()
            : [];

        var paths = (options.ProducePathMap && baseResult.Root is not null)
            ? JsonPathMapper.Build(baseResult.Root, spans, cancellationToken)
            : new Dictionary<string, JsonPathRange>();

        return baseResult with
        {
            TokenSpans = spans,
            PathRanges = paths
        };
    }

    private static async Task<JsonParseResult> WithLspArtifactsAsync(
        JsonParseResult baseResult,
        ParseOptions options,
        string usedText,
        CancellationToken cancellationToken)
    {
        if (options is { ProduceTokenSpans: false, ProducePathMap: false })
            return baseResult;

        var spans = options.ProduceTokenSpans
            ? await Task.Run(() => new JsonTokenizer(usedText, cancellationToken).Tokenize(), cancellationToken)
            : [];

        var paths = (options.ProducePathMap && baseResult.Root is not null)
            ? JsonPathMapper.Build(baseResult.Root, spans, cancellationToken)
            : new Dictionary<string, JsonPathRange>();

        return baseResult with
        {
            TokenSpans = spans,
            PathRanges = paths
        };
    }

    private static IReadOnlyList<JsonParseError> Merge(List<JsonParseError> a, IReadOnlyList<JsonParseError> b)
    {
        if (b.Count == 0) return a;
        a.AddRange(b);
        return a;
    }

    private static string? BuildSnippet(string source, int? lineNumber, int? linePosition, int radius)
    {
        if (string.IsNullOrEmpty(source)) return null;

        if (lineNumber is null || linePosition is null)
        {
            var preview = source.Length <= radius * 2 ? source : source[..(radius * 2)];
            return preview;
        }

        var lines = source.Split('\n');
        int lineIndex = Math.Clamp(lineNumber.Value - 1, 0, Math.Max(0, lines.Length - 1));
        var line = lines[lineIndex].Replace("\r", string.Empty);

        int caretPos = Math.Max(1, linePosition.Value);
        if (caretPos > line.Length + 1) caretPos = line.Length + 1;

        int caretIndex0 = Math.Min(Math.Max(0, caretPos - 1), line.Length);

        if (caretIndex0 < line.Length && (line[caretIndex0] == ' ' || line[caretIndex0] == '\t'))
        {
            int j = caretIndex0;
            while (j < line.Length && (line[j] == ' ' || line[j] == '\t')) j++;
            if (j < line.Length) caretIndex0 = j;
        }

        if (line.Length <= radius * 2)
        {
            var caretLine = MirrorWhitespacePrefix(line, caretIndex0) + "^";
            return line + "\n" + caretLine;
        }

        int start = Math.Max(0, caretIndex0 - radius);
        int end = Math.Min(line.Length, caretIndex0 + radius);
        var slice = line.Substring(start, end - start);
        int caretInSlice = caretIndex0 - start;
        if (caretInSlice < 0) caretInSlice = 0;
        if (caretInSlice > slice.Length) caretInSlice = slice.Length;

        var caretLineSliced = MirrorWhitespacePrefix(slice, caretInSlice) + "^";
        return slice + "\n" + caretLineSliced;

        static string MirrorWhitespacePrefix(string text, int count)
        {
            if (count <= 0) return string.Empty;
            var sb = new System.Text.StringBuilder(count);
            for (int i = 0; i < count && i < text.Length; i++)
            {
                char c = text[i];
                sb.Append(c == '\t' ? '\t' : ' ');
            }
            return sb.ToString();
        }
    }
}