using BybitPerpetualsTradingBot.Models;
using BybitPerpetualsTradingBot.Models.API;
using Newtonsoft.Json;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace BybitPerpetualsTradingBot
{
    internal static partial class TradingBotHelper
    {
        private static BaseHttpClient _baseHttpClient;
        private static Settings _settings;

        // Matches route parameters in curly braces
        [GeneratedRegex(@"\{[^{}]+\}", RegexOptions.Compiled)]
        private static partial Regex RouteParameterRegex();

        private readonly static string _settingsFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "settings.json");

        /// <summary>
        /// Initializes TradingBotHelper with Dependencies
        /// </summary>
        internal static void InitializeDependencies(Settings settings, BaseHttpClient baseHttpClient)
        {
            _baseHttpClient = baseHttpClient;
            _settings = settings;
        }

        /// <summary>
        /// Sets leverage for category and symbol
        /// </summary>
        internal static async Task<ApiResponse<object>?> SetLeverage(string category, string symbol, string buyLeverage, string sellLeverage)
        {
            string uri = BuildUri(_settings.Endpoint, "position", "set-leverage");

            Dictionary<string, object> parameters = new()
            {
                {"category",category},
                {"symbol", symbol},
                {"buyLeverage", buyLeverage},
                {"sellLeverage", sellLeverage}
            };

            string timestap = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString();
            string signature = GenerateSignature(_settings, timestap, parameters);
            string jsonPayload = JsonConvert.SerializeObject(parameters);

            return await _baseHttpClient.PostAsync<ApiResponse<object>?>(uri, jsonPayload, _settings.APIKey, timestap, signature, _settings.RecvWindow);
        }

        /// <summary>
        /// Gets position info for category and symbol
        /// </summary>
        internal static async Task<ApiResponse<object>?> GetPositionInfo(string category, string symbol)
        {
            string uri = BuildUri(_settings.Endpoint, "position", $"list?category={category}&symbol={symbol}");

            string timestap = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString();
            string signature = GenerateSignature(_settings, timestap, $"category={category}&symbol={symbol}");

            return await _baseHttpClient.GetAsync<ApiResponse<object>?>(uri, _settings.APIKey, timestap, signature, _settings.RecvWindow);
        }

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
        /// Generates signature for GET with query
        /// </summary>
        private static string GenerateSignature(Settings settings, string timestap, string query) =>
            GenerateSignatureBase(settings, string.Concat(timestap, settings.APIKey, settings.RecvWindow, query));

        /// <summary>
        /// Generates signature for POST with parameters
        /// </summary>
        private static string GenerateSignature(Settings settings, string timestap, IDictionary<string, object> parameters) =>
            GenerateSignatureBase(settings, string.Concat(timestap, settings.APIKey, settings.RecvWindow, JsonConvert.SerializeObject(parameters)));

        /// <summary>
        /// Generates signature with raw data
        /// </summary>
        private static string GenerateSignatureBase(Settings settings, string rawData)
        {
            using HMACSHA256 hmac = new(Encoding.UTF8.GetBytes(settings.APISecret));
            byte[] signature = hmac.ComputeHash(Encoding.UTF8.GetBytes(rawData));

            return Convert.ToHexStringLower(signature);
        }
    }
}
