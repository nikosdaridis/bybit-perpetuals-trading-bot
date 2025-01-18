using System.Text.RegularExpressions;

namespace CryptoFuturesTradingBot
{
    internal static partial class StringUtility
    {
        // Matches route parameters in curly braces
        [GeneratedRegex(@"\{[^{}]+\}", RegexOptions.Compiled)]
        private static partial Regex RouteParameterRegex();

        /// <summary>
        /// Builds URI from base URI, replacing route parameters with specified values
        /// </summary>
        public static string BuildUri(string baseUri, params string[] routeParameters)
        {
            if (routeParameters.Length == 0)
                return baseUri;

            int paramIndex = 0;

            return RouteParameterRegex().Replace(baseUri, match =>
                paramIndex < routeParameters.Length ? routeParameters[paramIndex++] ?? match.Value : match.Value);
        }
    }
}
