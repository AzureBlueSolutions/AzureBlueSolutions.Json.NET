namespace AzureBlueSolutions.Json.NET;

/// <summary>
/// Keys used to resolve overrideable error codes.
/// </summary>
/// <remarks>
/// These keys map to short code strings via <see cref="DefaultErrorCodes.Resolve(ErrorKey)"/>.
/// Consumers can plug a custom resolver into <c>ParseOptions.ResolveErrorCode</c>
/// to replace or extend the mapping.
/// </remarks>
public enum ErrorKey
{
    /// <summary>
    /// Input text was <c>null</c>.
    /// </summary>
    NullInput,

    /// <summary>
    /// No JSON content was found (empty or whitespace only).
    /// </summary>
    NoContent,

    /// <summary>
    /// Invalid token encountered by the parser.
    /// </summary>
    InvalidToken,

    /// <summary>
    /// Duplicate property name detected when duplicate keys are configured as an error.
    /// </summary>
    DuplicateKey,

    /// <summary>
    /// One or more comments were removed during sanitization.
    /// </summary>
    CommentsRemoved,

    /// <summary>
    /// One or more trailing commas were removed during sanitization.
    /// </summary>
    TrailingCommasRemoved,

    /// <summary>
    /// One or more control characters were removed during sanitization.
    /// </summary>
    ControlCharsRemoved,

    /// <summary>
    /// A UTF‑8 BOM was removed.
    /// </summary>
    BomRemoved,

    /// <summary>
    /// Line endings were normalized (e.g., CRLF -> LF).
    /// </summary>
    LineEndingsNormalized,

    /// <summary>
    /// Unhandled exception occurred during parsing.
    /// </summary>
    Exception,

    /// <summary>
    /// The document exceeded the configured maximum length.
    /// </summary>
    SizeLimitExceeded,

    /// <summary>
    /// The document exceeded the configured maximum nesting depth.
    /// </summary>
    DepthLimitExceeded,

    /// <summary>
    /// One or more unterminated strings were closed by the sanitizer.
    /// </summary>
    UnterminatedStringsClosed,

    /// <summary>
    /// Inserted one or more missing commas between adjacent values/properties.
    /// </summary>
    MissingCommasInserted,

    /// <summary>
    /// Inserted one or more missing closers (']' or '}').
    /// </summary>
    ClosersInserted
}