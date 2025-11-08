namespace AzureBlueSolutions.Json.NET.Tests;

public sealed class ConsoleDiagnosticWriter_Tests
{
    [Fact]
    public void Write_Produces_Same_Text_With_And_Without_Color()
    {
        var err = new JsonParseError
        {
            Code = DefaultErrorCodes.Resolve(ErrorKey.InvalidToken),
            Severity = ErrorSeverity.Error,
            Message = "Unexpected token.",
            LineNumber = 1,
            LinePosition = 5,
            Stage = "Initial",
            Snippet = "abcd\na^^^"
        };

        var writer1 = new StringWriter();
        var writer2 = new StringWriter();
        var saved = Console.Out;

        try
        {
            Console.SetOut(writer1);
            ConsoleDiagnosticWriter.Write(err, new DiagnosticColorOptions { EnableColor = false }, 0);
            Console.Out.Flush();

            Console.SetOut(writer2);
            ConsoleDiagnosticWriter.Write(err, new DiagnosticColorOptions { EnableColor = true }, 0);
            Console.Out.Flush();
        }
        finally
        {
            Console.SetOut(saved);
        }

        var a = writer1.ToString().Replace("\r\n", "\n");
        var b = writer2.ToString().Replace("\r\n", "\n");
        Assert.Equal(a, b);
        Assert.Contains("[Error]", a);
        Assert.Contains(DefaultErrorCodes.Resolve(ErrorKey.InvalidToken), a);
        Assert.Contains("Unexpected token.", a);
        Assert.Contains("^", a);
    }
}