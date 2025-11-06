namespace AzureBlueSolutions.Json.NET;

public static class DefaultErrorCodes
{
    public static string Resolve(ErrorKey key) => key switch
    {
        ErrorKey.NullInput => "E000",
        ErrorKey.NoContent => "E001",
        ErrorKey.InvalidToken => "E002",
        ErrorKey.DuplicateKey => "E003",
        ErrorKey.SizeLimitExceeded => "E008",
        ErrorKey.DepthLimitExceeded => "E009",

        ErrorKey.CommentsRemoved => "W100",
        ErrorKey.TrailingCommasRemoved => "W101",
        ErrorKey.ControlCharsRemoved => "W102",
        ErrorKey.UnterminatedStringsClosed => "W103",

        ErrorKey.BomRemoved => "I200",
        ErrorKey.LineEndingsNormalized => "I201",

        ErrorKey.MissingCommasInserted => "R100",
        ErrorKey.ClosersInserted => "R101",

        ErrorKey.Exception => "E999",
        _ => "E000"
    };
}