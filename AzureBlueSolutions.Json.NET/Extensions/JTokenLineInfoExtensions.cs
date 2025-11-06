using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace AzureBlueSolutions.Json.NET
{
    /// <summary>
    /// Provides extension methods for retrieving line information from JSON tokens.
    /// </summary>
    public static class JTokenLineInfoExtensions
    {
        /// <summary>
        /// Gets the line number and position for the specified <see cref="JToken"/>, if available.
        /// </summary>
        /// <param name="token">
        /// The JSON token to inspect. Must implement <see cref="IJsonLineInfo"/> to return meaningful data.
        /// </param>
        /// <returns>
        /// A tuple containing the line number and position (both 1-based) if the token has line info;
        /// otherwise, <c>null</c>.
        /// </returns>
        /// <remarks>
        /// This method checks whether the token supports <see cref="IJsonLineInfo"/> and has line information.
        /// If so, it returns the line number and position as reported by the parser.
        /// </remarks>
        public static (int line, int position)? GetLineInfo(this JToken token)
        {
            if (token is IJsonLineInfo lineInfo && lineInfo.HasLineInfo())
                return (lineInfo.LineNumber, lineInfo.LinePosition);
            return null;
        }
    }
}