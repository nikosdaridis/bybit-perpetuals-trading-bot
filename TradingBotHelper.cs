using Newtonsoft.Json;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace CryptoFuturesTradingBot
{
    internal static partial class TradingBotHelper
    {
        // Matches route parameters in curly braces
        [GeneratedRegex(@"\{[^{}]+\}", RegexOptions.Compiled)]
        private static partial Regex RouteParameterRegex();

        private readonly static string _settingsFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "settings.json");

        /// <summary>
        /// Loads settings from the settings file and verifies all properties are present
        /// </summary>
        internal static Settings LoadSettings()
        {
            try
            {
                return VerifySettings(File.ReadAllText(_settingsFilePath));
            }
            catch
            {
                return VerifySettings();
            }

            // Verifies all properties are present and of the correct type in the settings file
            static Settings VerifySettings(string jsonContent = "")
            {
                bool updateFile = false;

                Dictionary<string, object?> settingsMap = string.IsNullOrEmpty(jsonContent)
                    ? []
                    : JsonConvert.DeserializeObject<Dictionary<string, object?>>(jsonContent) ?? [];

                foreach (PropertyInfo property in typeof(Settings).GetProperties())
                {
                    if (!settingsMap.TryGetValue(property.Name, out object? value) ||
                        value == null ||
                        !property.PropertyType.IsAssignableFrom(value?.GetType()))
                    {
                        settingsMap[property.Name] = property.GetValue(new Settings());
                        updateFile = true;
                    }
                }

                if (updateFile)
                {
                    string updatedJson = JsonConvert.SerializeObject(settingsMap, Formatting.Indented);
                    File.WriteAllText(_settingsFilePath, updatedJson);

                    return JsonConvert.DeserializeObject<Settings>(updatedJson) ?? new();
                }
                else
                {
                    return JsonConvert.DeserializeObject<Settings>(jsonContent) ?? new();
                }
            }
        }

        /// <summary>
        /// Builds URI from base URI, replacing route parameters with specified values
        /// </summary>
        internal static string BuildUri(string baseUri, params string[] routeParameters)
        {
            if (routeParameters.Length == 0)
                return baseUri;

            int paramIndex = 0;

            return RouteParameterRegex().Replace(baseUri, match =>
                paramIndex < routeParameters.Length ? routeParameters[paramIndex++] ?? match.Value : match.Value);
        }

        /// <summary>
        /// Generates signature for Post request
        /// </summary>
        internal static string GenerateSignature(Settings settings, string timestap, IDictionary<string, object> parameters)
        {
            string rawData = string.Concat(timestap, settings.APIKey, settings.RecvWindow, JsonConvert.SerializeObject(parameters));
            using HMACSHA256 hmac = new(Encoding.UTF8.GetBytes(settings.APISecret));
            byte[] signature = hmac.ComputeHash(Encoding.UTF8.GetBytes(rawData));

            return Convert.ToHexStringLower(signature);
        }
    }
}
