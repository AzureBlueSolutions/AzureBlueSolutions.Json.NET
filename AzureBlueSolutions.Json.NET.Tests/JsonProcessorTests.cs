namespace AzureBlueSolutions.Json.NET.Tests;

public sealed class JsonProcessorTests
{
    [Fact]
    public void Both_CorrectnessFirst_Prefers_Strict_When_Valid()
    {
        const string json = """{ "a": 1 }""";
        var options = new ProcessingOptions { Mode = ParserMode.Both, Priority = ParsePriority.CorrectnessFirst };
        var result = JsonProcessor.Parse(json, options);
        Assert.True(result.SelectedIsStrict);
        Assert.Equal(ParserMode.Both, result.ModeUsed);
    }

    [Fact]
    public void Both_RecoveryFirst_Prefers_Tolerant_When_Valid()
    {
        const string json = """{ "a": 1 }""";
        var options = new ProcessingOptions { Mode = ParserMode.Both, Priority = ParsePriority.RecoveryFirst };
        var result = JsonProcessor.Parse(json, options);
        Assert.False(result.SelectedIsStrict);
        Assert.Equal(ParserMode.Both, result.ModeUsed);
    }
}