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
        internal static void InitializeDependencies(BaseHttpClient baseHttpClient)
        {
            _baseHttpClient = baseHttpClient;
        }

        /// <summary>
        /// Gets instruments info for category and optional symbol and limit
        /// </summary>
        internal static async Task<ApiResponse<GetInstrumentsInfoResult>?> GetInstrumentsInfo(string category, string symbol = "", int limit = 1000)
        {
            Dictionary<string, string> queryParams = new()
            {
                [nameof(category)] = category,
                [nameof(limit)] = limit.ToString()
            };

            if (!string.IsNullOrEmpty(symbol))
                queryParams[nameof(symbol)] = symbol.ToUpper();

            string query = string.Join("&", queryParams.Select(kvp => $"{kvp.Key}={kvp.Value}"));
            string uri = BuildUri(_settings.Endpoint, ApiParams.EndpointProduct.Market, string.Concat(ApiParams.EndpointModule.InstrumentsInfo, '?', query));

            string timestap = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString();
            string signature = GenerateSignature(_settings, timestap, query);

            return await _baseHttpClient.GetAsync<ApiResponse<GetInstrumentsInfoResult>?>(uri, _settings.APIKey, timestap, signature, _settings.RecvWindow);
        }

        /// <summary>
        /// Gets position info for category and symbol
        /// </summary>
        internal static async Task<ApiResponse<GetPositionInfoResult>?> GetPositionInfo(string category, string symbol)
        {
            string query = $"category={category}&symbol={symbol.ToUpper()}";
            string uri = BuildUri(_settings.Endpoint, ApiParams.EndpointProduct.Position, string.Concat(ApiParams.EndpointModule.List, '?', query));

            string timestap = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString();
            string signature = GenerateSignature(_settings, timestap, query);

            return await _baseHttpClient.GetAsync<ApiResponse<GetPositionInfoResult>?>(uri, _settings.APIKey, timestap, signature, _settings.RecvWindow);
        }

        /// <summary>
        /// Sets leverage for category and symbol
        /// </summary>
        internal static async Task<ApiResponse<object>?> SetLeverage(string category, string symbol, string buyLeverage, string sellLeverage)
        {
            string uri = BuildUri(_settings.Endpoint, ApiParams.EndpointProduct.Position, ApiParams.EndpointModule.SetLeverage);

            Dictionary<string, object> parameters = new()
            {
                {nameof(category),category},
                {nameof(symbol),symbol.ToUpper()},
                {nameof(buyLeverage),buyLeverage},
                {nameof(sellLeverage),sellLeverage}
            };

            string timestap = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString();
            string signature = GenerateSignature(_settings, timestap, parameters);
            string jsonPayload = JsonConvert.SerializeObject(parameters);

            return await _baseHttpClient.PostAsync<ApiResponse<object>?>(uri, jsonPayload, _settings.APIKey, timestap, signature, _settings.RecvWindow);
        }

        /// <summary>
        /// Places order for category, symbol, side, order type and quantity with optional price, timeInforce and reduceOnly
        /// </summary>
        internal static async Task<ApiResponse<PlaceOrderResult>?> PlaceOrder(string category, string symbol, string side, string orderType, string qty, string price = "0", string timeInForce = "PostOnly", bool reduceOnly = false)
        {
            string uri = BuildUri(_settings.Endpoint, ApiParams.EndpointProduct.Order, ApiParams.EndpointModule.Create);

            Dictionary<string, object> parameters = new()
                {
                    {nameof(category),category},
                    {nameof(symbol),symbol.ToUpper()},
                    {nameof(side),side},
                    {nameof(orderType),orderType},
                    {nameof(qty),qty},
                    {nameof(price),price},
                    {nameof(timeInForce),timeInForce},
                    {nameof(reduceOnly),reduceOnly}
                };

            string timestap = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString();
            string signature = GenerateSignature(_settings, timestap, parameters);
            string jsonPayload = JsonConvert.SerializeObject(parameters);

            return await _baseHttpClient.PostAsync<ApiResponse<PlaceOrderResult>?>(uri, jsonPayload, _settings.APIKey, timestap, signature, _settings.RecvWindow);
        }

        /// <summary>
        /// Loads settings from the settings file and verifies all properties are present
        /// </summary>
        internal static Settings LoadSettings()
        {
            try
            {
                _settings = VerifySettings(File.ReadAllText(_settingsFilePath));
            }
            catch
            {
                _settings = VerifySettings();
            }

            if (string.IsNullOrEmpty(_settings?.APIKey) || string.IsNullOrEmpty(_settings?.APISecret))
                throw new Exception("API Key or Secret not found in settings.json");

            return _settings;

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
