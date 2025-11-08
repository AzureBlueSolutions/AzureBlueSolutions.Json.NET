using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace AzureBlueSolutions.Json.NET;

/// <summary>
///     Line-info retrieval for <see cref="JToken" /> using <see cref="IJsonLineInfo" />.
/// </summary>
public static class JTokenGetLineInfoExtensions
{
    /// <summary>
    ///     Gets one-based line/position information from a <see cref="JToken" /> when available.
    /// </summary>
    /// <param name="token">The token to inspect for line information.</param>
    /// <returns>
    ///     A tuple (line, position) where both values are one-based, or <c>null</c> when the token
    ///     does not provide meaningful line info.
    /// </returns>
    /// <remarks>
    ///     This method checks whether <paramref name="token" /> implements <see cref="IJsonLineInfo" /> and
    ///     has line information (<see cref="IJsonLineInfo.HasLineInfo()" />). If so, the values returned by
    ///     <see cref="IJsonLineInfo.LineNumber" /> and <see cref="IJsonLineInfo.LinePosition" /> are propagated.
    /// </remarks>
    public static (int line, int position)? GetLineInfo(this JToken token)
    {
        if (token is IJsonLineInfo lineInfo && lineInfo.HasLineInfo())
            return (lineInfo.LineNumber, lineInfo.LinePosition);
        return null;
    }
}