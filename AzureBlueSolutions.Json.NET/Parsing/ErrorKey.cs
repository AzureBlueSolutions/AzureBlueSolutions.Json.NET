namespace AzureBlueSolutions.Json.NET;

/// <summary>
/// Keys used to resolve overrideable error codes.
/// </summary>
public enum ErrorKey
{
    NullInput,
    NoContent,
    InvalidToken,
    DuplicateKey,
    CommentsRemoved,
    TrailingCommasRemoved,
    ControlCharsRemoved,
    BomRemoved,
    LineEndingsNormalized,
    Exception,
    SizeLimitExceeded,
    DepthLimitExceeded,
    UnterminatedStringsClosed,

    /// <summary>Inserted one or more missing commas between adjacent values/properties.</summary>
    MissingCommasInserted,

    /// <summary>Inserted one or more missing closers (']' or '}').</summary>
    ClosersInserted
}