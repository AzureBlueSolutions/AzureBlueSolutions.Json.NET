using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace AzureBlueSolutions.Json.NET;

/// <summary>
/// Line info helper.
/// </summary>
public static class JTokenLineInfoExtensions
{
    public static (int line, int position)? GetLineInfo(this JToken token)
    {
        if (token is IJsonLineInfo lineInfo && lineInfo.HasLineInfo())
        {
            return (lineInfo.LineNumber, lineInfo.LinePosition);
        }
        return null;
    }
}