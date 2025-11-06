using System.Text;

namespace AzureBlueSolutions.Json.NET.Tests;

public sealed class CancellationTests
{
    [Fact]
    public async Task ParseSafe_Throws_When_Canceled_Before_Start_Async()
    {
        var json = "{\"a\":1}";
        var options = new ParseOptions
        {
            ProduceTokenSpans = true,
            ProducePathMap = true
        };

        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        await Assert.ThrowsAsync<TaskCanceledException>(async () =>
        {
            await Task.Run(() => JsonParser.ParseSafe(json, options, cts.Token), cts.Token);
        });
    }

    [Fact]
    public async Task ParseSafe_Tokenizer_Cancels_During_Long_Input_Async()
    {
        var sb = new StringBuilder();
        sb.Append("{\"nums\":[");
        for (var i = 0; i < 200_000; i++)
        {
            if (i > 0) sb.Append(',');
            sb.Append(i);
        }

        sb.Append("]}");
        var bigJson = sb.ToString();

        var options = new ParseOptions
        {
            ProduceTokenSpans = true,
            ProducePathMap = false,
            EnableSanitizationFallback = false
        };

        using var cts = new CancellationTokenSource();
        var task = Task.Run(async () =>
        {
            await Task.Yield();
            return JsonParser.ParseSafeAsync(bigJson, options, cts.Token);
        }, CancellationToken.None);
        cts.CancelAfter(15);

        await Assert.ThrowsAsync<OperationCanceledException>(async () => await task);
    }

    [Fact]
    public async Task ParseSafe_PathMap_Cancels_During_Map_Build_Async()
    {
        var objBuilder = new StringBuilder();
        objBuilder.Append("{\"root\":[");
        for (var i = 0; i < 15_000; i++)
        {
            if (i > 0) objBuilder.Append(',');
            objBuilder.Append('{');
            for (var k = 0; k < 4; k++)
            {
                if (k > 0) objBuilder.Append(',');
                objBuilder.Append('"').Append("k").Append(k).Append('"').Append(':').Append(i * 10 + k);
            }

            objBuilder.Append('}');
        }

        objBuilder.Append("]}");
        var bigValid = objBuilder.ToString();

        var options = new ParseOptions
        {
            ProduceTokenSpans = true,
            ProducePathMap = true,
            EnableSanitizationFallback = false
        };

        using var cts = new CancellationTokenSource();
        var task = Task.Run(async () =>
        {
            await Task.Yield();
            return JsonParser.ParseSafeAsync(bigValid, options, cts.Token);
        }, CancellationToken.None);
        cts.CancelAfter(25);

        await Assert.ThrowsAsync<OperationCanceledException>(async () => { await task; });
    }
}