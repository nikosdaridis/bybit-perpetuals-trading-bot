using BybitPerpetualsTradingBot.Models;
using BybitPerpetualsTradingBot.Models.API;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using static BybitPerpetualsTradingBot.Models.API.ApiParameters;

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
        private readonly static JsonSerializerSettings _jsonSerializerSettings = new()
        {
            ContractResolver = new DefaultContractResolver
            {
                NamingStrategy = new CamelCaseNamingStrategy
                {
                    ProcessDictionaryKeys = true,
                    OverrideSpecifiedNames = false
                }
            },
            NullValueHandling = NullValueHandling.Ignore
        };

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
        internal static async Task<ApiResponse<GetInstrumentsInfoResult, object>?> GetInstrumentsInfo(string category, string symbol = "", int limit = 1000)
        {
            Dictionary<string, string> queryParams = new()
            {
                {nameof(category), category},
                {nameof(limit), limit.ToString()}
            };

            if (!string.IsNullOrEmpty(symbol))
                queryParams[nameof(symbol)] = symbol.ToUpper();

            string query = string.Join("&", queryParams.Select(kvp => $"{kvp.Key}={kvp.Value}"));
            string uri = BuildUri(_settings.Endpoint, EndpointProduct.Market, string.Concat(EndpointModule.InstrumentsInfo, '?', query));

            string timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString();
            string signature = GenerateSignature(_settings, timestamp, query);

            return await _baseHttpClient.GetAsync<ApiResponse<GetInstrumentsInfoResult, object>?>(uri, _settings.APIKey, timestamp, signature, _settings.RecvWindow);
        }

        /// <summary>
        /// Gets position info for category and symbol
        /// </summary>
        internal static async Task<ApiResponse<GetPositionInfoResult, object>?> GetPositionInfo(string category, string symbol)
        {
            string query = $"{nameof(category)}={category}&{nameof(symbol)}={symbol.ToUpper()}";
            string uri = BuildUri(_settings.Endpoint, EndpointProduct.Position, string.Concat(EndpointModule.List, '?', query));

            string timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString();
            string signature = GenerateSignature(_settings, timestamp, query);

            return await _baseHttpClient.GetAsync<ApiResponse<GetPositionInfoResult, object>?>(uri, _settings.APIKey, timestamp, signature, _settings.RecvWindow);
        }

        /// <summary>
        /// Gets open and closed orders for category, symbol and openOnly with optional limit
        /// </summary>
        internal static async Task<ApiResponse<GetOpenAndClosedOrdersResult, object>?> GetOpenAndClosedOrders(string category, string symbol, int openOnly, int limit = 50)
        {
            string query = $"{nameof(category)}={category}&{nameof(symbol)}={symbol.ToUpper()}&{nameof(openOnly)}={openOnly}&{nameof(limit)}={limit}";
            string uri = BuildUri(_settings.Endpoint, EndpointProduct.Order, string.Concat(EndpointModule.RealTime, '?', query));

            string timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString();
            string signature = GenerateSignature(_settings, timestamp, query);

            return await _baseHttpClient.GetAsync<ApiResponse<GetOpenAndClosedOrdersResult, object>?>(uri, _settings.APIKey, timestamp, signature, _settings.RecvWindow);
        }

        /// <summary>
        /// Sets leverage for category and symbol
        /// </summary>
        internal static async Task<ApiResponse<object, object>?> SetLeverage(string category, string symbol, string buyLeverage, string sellLeverage)
        {
            string uri = BuildUri(_settings.Endpoint, EndpointProduct.Position, EndpointModule.SetLeverage);

            Dictionary<string, object> parameters = new()
            {
                {nameof(category), category},
                {nameof(symbol), symbol.ToUpper()},
                {nameof(buyLeverage), buyLeverage},
                {nameof(sellLeverage), sellLeverage}
            };

            string timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString();
            string jsonPayload = JsonConvert.SerializeObject(parameters, _jsonSerializerSettings);
            string signature = GenerateSignature(_settings, timestamp, jsonPayload);

            return await _baseHttpClient.PostAsync<ApiResponse<object, object>?>(uri, jsonPayload, _settings.APIKey, timestamp, signature, _settings.RecvWindow);
        }

        /// <summary>
        /// Places order for category, symbol, side, order type and quantity with optional price, timeInforce and reduceOnly
        /// </summary>
        internal static async Task<ApiResponse<OrderResult, object>?> PlaceOrder(string category, string symbol, string side, string orderType, string qty, string? price = "0", string timeInForce = "PostOnly", bool reduceOnly = false)
        {
            string uri = BuildUri(_settings.Endpoint, EndpointProduct.Order, EndpointModule.Create);

            Dictionary<string, object> parameters = new()
                {
                    {nameof(category), category},
                    {nameof(symbol), symbol.ToUpper()},
                    {nameof(side), side},
                    {nameof(orderType), orderType},
                    {nameof(qty), qty},
                    {nameof(price), price},
                    {nameof(timeInForce), timeInForce},
                    {nameof(reduceOnly), reduceOnly}
                };

            string timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString();
            string jsonPayload = JsonConvert.SerializeObject(parameters, _jsonSerializerSettings);
            string signature = GenerateSignature(_settings, timestamp, jsonPayload);

            return await _baseHttpClient.PostAsync<ApiResponse<OrderResult, object>?>(uri, jsonPayload, _settings.APIKey, timestamp, signature, _settings.RecvWindow);
        }

        /// <summary>
        /// Batch places orders
        /// </summary>
        internal static async Task<ApiResponse<BatchOrderResult, BatchOrderRetExtInfo>?> BatchPlaceOrder(ApiRequest<BatchOrderRequest> request)
        {
            string uri = BuildUri(_settings.Endpoint, EndpointProduct.Order, EndpointModule.CreateBatch);

            Dictionary<string, object> parameters = [];

            foreach (PropertyInfo property in request.GetType().GetProperties())
            {
                object? value = property.GetValue(request);

                if (value is not null && (value is not string stringValue || !string.IsNullOrEmpty(stringValue)))
                    parameters[property.Name.ToLower()] = value;
            }

            string timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString();
            string jsonPayload = JsonConvert.SerializeObject(parameters, _jsonSerializerSettings);
            string signature = GenerateSignature(_settings, timestamp, jsonPayload);

            return await _baseHttpClient.PostAsync<ApiResponse<BatchOrderResult, BatchOrderRetExtInfo>?>(uri, jsonPayload, _settings.APIKey, timestamp, signature, _settings.RecvWindow);
        }

        /// <summary>
        /// Loads settings from the settings file and verifies all properties are present and of the correct type
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
                Dictionary<string, object?> settingsMap = string.IsNullOrEmpty(jsonContent)
                    ? []
                    : JsonConvert.DeserializeObject<Dictionary<string, object?>>(jsonContent) ?? [];

                foreach (PropertyInfo property in typeof(Settings).GetProperties())
                    if (!settingsMap.TryGetValue(property.Name, out object? value) || value is null || !property.PropertyType.IsAssignableFrom(value?.GetType()))
                        settingsMap[property.Name] = property.GetValue(new Settings());

                string settingsJson = JsonConvert.SerializeObject(settingsMap, Formatting.Indented);
                File.WriteAllText(_settingsFilePath, settingsJson);

                return JsonConvert.DeserializeObject<Settings>(settingsJson) ?? new();
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
        /// Generates signature with data
        /// </summary>
        private static string GenerateSignature(Settings settings, string timestamp, string data)
        {
            string rawData = string.Concat(timestamp, settings.APIKey, settings.RecvWindow, data);
            using HMACSHA256 hmac = new(Encoding.UTF8.GetBytes(settings.APISecret));
            byte[] signature = hmac.ComputeHash(Encoding.UTF8.GetBytes(rawData));

            return Convert.ToHexStringLower(signature);
        }
    }
}
