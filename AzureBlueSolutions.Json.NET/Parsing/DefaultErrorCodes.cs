namespace AzureBlueSolutions.Json.NET;

/// <summary>
/// Provides the default mapping from <see cref="ErrorKey"/> values to short,
/// stable code strings used in diagnostics (e.g., <c>"E002"</c>, <c>"W101"</c>).
/// </summary>
/// <remarks>
/// The returned codes are grouped by prefix:
/// <br/>
/// <list type="bullet">
/// <item><description><c>E</c> — Errors (e.g., <c>E000</c> Null input, <c>E002</c> Invalid token).</description></item>
/// <item><description><c>W</c> — Warnings produced by sanitization/recovery (e.g., <c>W101</c> Trailing commas removed).</description></item>
/// <item><description><c>I</c> — Informational notes (e.g., <c>I200</c> BOM removed, <c>I201</c> Line endings normalized).</description></item>
/// <item><description><c>R</c> — Recovery actions performed (e.g., <c>R100</c> Missing commas inserted, <c>R101</c> Closers inserted).</description></item>
/// </list>
/// </remarks>
public static class DefaultErrorCodes
{
    /// <summary>
    /// Resolves the canonical code string for the specified <paramref name="key"/>.
    /// </summary>
    /// <param name="key">
    /// The <see cref="ErrorKey"/> to map to a short, human‑readable code.
    /// </param>
    /// <returns>
    /// The code string corresponding to <paramref name="key"/>. If the key is not recognized,
    /// returns <c>"E000"</c>.
    /// </returns>
    public static string Resolve(ErrorKey key)
    {
        return key switch
        {
            #region Error Codes

            ErrorKey.NullInput => "E000",
            ErrorKey.NoContent => "E001",
            ErrorKey.InvalidToken => "E002",
            ErrorKey.DuplicateKey => "E003",

            ErrorKey.SizeLimitExceeded => "E008",
            ErrorKey.DepthLimitExceeded => "E009",

            #endregion

            #region Warnings Codes

            ErrorKey.CommentsRemoved => "W100",
            ErrorKey.TrailingCommasRemoved => "W101",
            ErrorKey.ControlCharsRemoved => "W102",
            ErrorKey.UnterminatedStringsClosed => "W103",

            #endregion

            #region Info Codes

            ErrorKey.BomRemoved => "I200",
            ErrorKey.LineEndingsNormalized => "I201",

            #endregion

            #region Recovery Codes
            
            ErrorKey.MissingCommasInserted => "R100",
            ErrorKey.ClosersInserted => "R101",

            #endregion

            ErrorKey.Exception => "E999",
            _ => "E000"
        };
    }
}