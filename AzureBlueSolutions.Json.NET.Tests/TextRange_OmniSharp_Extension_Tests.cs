using OmniSharp.Extensions.LanguageServer.Protocol.Models;

namespace AzureBlueSolutions.Json.NET.Tests;

public sealed class TextRange_OmniSharp_Extension_Tests
{
    [Fact]
    public void ToRange_Maps_ZeroBased_Range_To_Lsp_Range()
    {
        var r = new TextRange(
            new TextPosition(3, 2, 10),
            new TextPosition(3, 5, 13));
        var lsp = r.ToRange();
        Assert.Equal(new Position(3, 2), lsp.Start);
        Assert.Equal(new Position(3, 5), lsp.End);
    }
}