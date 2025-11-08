namespace AzureBlueSolutions.Json.NET.Tests;

public sealed class ProfilesTests
{
    [Fact]
    public void Strict_Disables_Leniencies()
    {
        var p = Profiles.Strict();
        Assert.False(p.AllowComments);
        Assert.False(p.EnableSanitizationFallback);
        Assert.False(p.EnableAggressiveRecovery);
        Assert.False(p.AllowTrailingCommas);
        Assert.False(p.RemoveControlCharacters);
        Assert.False(p.ReturnSanitizedText);
        Assert.False(p.IncludeSanitizationDiagnostics);
    }

    [Fact]
    public void Tolerant_Enables_Recovery_Features()
    {
        var p = Profiles.Tolerant();
        Assert.True(p.AllowComments);
        Assert.True(p.EnableSanitizationFallback);
        Assert.True(p.EnableAggressiveRecovery);
        Assert.True(p.AllowTrailingCommas);
        Assert.True(p.RemoveControlCharacters);
        Assert.True(p.ReturnSanitizedText);
        Assert.True(p.IncludeSanitizationDiagnostics);
        Assert.True(p.FixUnterminatedStrings);
        Assert.True(p.RecoverMissingCommas);
        Assert.True(p.RecoverMissingClosers);
    }
}