namespace AzureBlueSolutions.Json.NET;

public enum ParserMode
{
    Strict,
    Tolerant,
    Both
}

public enum ParsePriority
{
    CorrectnessFirst,
    RecoveryFirst
}