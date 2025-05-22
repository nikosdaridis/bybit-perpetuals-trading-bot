using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System.Globalization;
using System.Text.RegularExpressions;

namespace BybitPerpetualsTradingBot
{
    internal static partial class Helpers
    {
        // Matches route parameters in curly braces
        [GeneratedRegex(@"\{[^{}]+\}", RegexOptions.Compiled)]
        private static partial Regex RouteParameterRegex();

        /// <summary>
        /// Loads data from file or backups current file and creates a default file
        /// </summary>
        internal static T LoadFileData<T>(string filePath, ILogger? logger = null) where T : new()
        {
            T defaultModel = new();

            try
            {
                string jsonContent = File.ReadAllText(filePath);

                if (string.IsNullOrEmpty(jsonContent))
                {
                    jsonContent = JsonConvert.SerializeObject(defaultModel, Formatting.Indented);
                    File.WriteAllText(filePath, jsonContent);
                    return defaultModel;
                }

                return JsonConvert.DeserializeObject<T>(jsonContent) ?? defaultModel;
            }
            catch (Exception ex)
            {
                LogAndPrint(logger, LogLevel.Error, "Invalid Json - Error reading file '{0}': {1}", filePath, ex.Message);

                if (File.Exists(filePath))
                {
                    try
                    {
                        string backupFilePath = Path.ChangeExtension(filePath, $".invalid.{DateTime.Now:MMddHHmmss}.json");

                        File.Move(filePath, backupFilePath);
                        LogAndPrint(logger, LogLevel.Warning, "Existing file backed up as {0}", backupFilePath);
                    }
                    catch (Exception backupEx)
                    {
                        LogAndPrint(logger, LogLevel.Error, "Failed to back up the existing file: {0}", backupEx.Message);
                    }
                }

                string defaultJson = JsonConvert.SerializeObject(defaultModel, Formatting.Indented);
                File.WriteAllText(filePath, defaultJson);
                return defaultModel;
            }
        }

        /// <summary>
        /// Builds URI from base URI, replacing route parameters with specified values
        /// </summary>
        public static string BuildUri(string baseUri, params string[] routeParameters)
        {
            if (routeParameters.Length == 0)
                return baseUri;

            uint paramIndex = 0;

            return RouteParameterRegex().Replace(baseUri, match =>
                paramIndex < routeParameters.Length ? routeParameters[paramIndex++] ?? match.Value : match.Value);
        }

        /// <summary>
        /// Converts string to decimal with decimal point, invariant culture and normalizes value
        /// </summary>
        public static bool TryParseDecimal(string? input, out decimal result)
        {
            bool success = decimal.TryParse(input, NumberStyles.Number, CultureInfo.InvariantCulture, out result);

            if (success)
                result = result.Normalize();

            return success;
        }

        /// <summary>
        /// Normalizes decimal
        /// </summary>
        public static decimal Normalize(this decimal value) =>
            value / 1.000000000000000000000000000000000m;

        /// <summary>
        /// Logs and prints message
        /// </summary>
        public static void LogAndPrint(ILogger? logger, LogLevel logLevel, string errorMessage, params object?[] parameters)
        {
            string formattedMessage = string.Format(errorMessage, parameters);

            logger?.Log(logLevel, formattedMessage);

            Console.ForegroundColor = logLevel switch
            {
                LogLevel.Critical => ConsoleColor.DarkMagenta,
                LogLevel.Error => ConsoleColor.DarkRed,
                LogLevel.Warning => ConsoleColor.DarkYellow,
                LogLevel.Information => ConsoleColor.DarkCyan,
                LogLevel.Debug => ConsoleColor.DarkGreen,
                LogLevel.Trace => ConsoleColor.DarkGray,
                _ => ConsoleColor.White
            };

            Console.WriteLine(formattedMessage);
        }
    }
}
