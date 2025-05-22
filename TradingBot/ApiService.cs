using BybitPerpetualsTradingBot.Models;
using BybitPerpetualsTradingBot.Models.API;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using static BybitPerpetualsTradingBot.Models.API.ApiParameters;

namespace BybitPerpetualsTradingBot
{
    internal class ApiService(BaseHttpClient baseHttpClient)
    {
        private readonly JsonSerializerSettings _jsonSerializerSettings = new()
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
        /// Gets instruments info for category and optional symbol and limit
        /// </summary>
        public async Task<ApiResponse<GetInstrumentsInfoResult, object>?> GetInstrumentsInfo(string category, string symbol = "", int limit = 1000)
        {
            Dictionary<string, string> queryParams = new()
            {
                {nameof(category), category},
                {nameof(limit), limit.ToString()}
            };

            if (!string.IsNullOrEmpty(symbol))
                queryParams[nameof(symbol)] = symbol.ToUpper();

            string query = string.Join("&", queryParams.Select(kvp => $"{kvp.Key}={kvp.Value}"));

            string uri = Helpers.BuildUri(TradingBotService._settings.Endpoint, EndpointProduct.Market, string.Concat(EndpointModule.InstrumentsInfo, '?', query));
            (string timestamp, string signature) = GenerateSignature(TradingBotService._settings, query);

            return await baseHttpClient.GetAsync<ApiResponse<GetInstrumentsInfoResult, object>?>(uri, TradingBotService._settings.APIKey, timestamp, signature, TradingBotService._settings.RecvWindow);
        }

        /// <summary>
        /// Gets tickers for category and symbol
        /// </summary>
        public async Task<ApiResponse<GetTickersResult, object>?> GetTickers(string category, string symbol)
        {
            Dictionary<string, string> queryParams = new()
            {
                {nameof(category), category},
                {nameof(symbol), symbol.ToUpper()}
            };
            string query = string.Join("&", queryParams.Select(kvp => $"{kvp.Key}={kvp.Value}"));

            string uri = Helpers.BuildUri(TradingBotService._settings.Endpoint, EndpointProduct.Market, string.Concat(EndpointModule.Tickers, '?', query));
            (string timestamp, string signature) = GenerateSignature(TradingBotService._settings, query);

            return await baseHttpClient.GetAsync<ApiResponse<GetTickersResult, object>?>(uri, TradingBotService._settings.APIKey, timestamp, signature, TradingBotService._settings.RecvWindow);
        }

        /// <summary>
        /// Gets position info for category and symbol
        /// </summary>
        public async Task<ApiResponse<GetPositionInfoResult, object>?> GetPositionInfo(string category, string symbol)
        {
            Dictionary<string, string> queryParams = new()
            {
                {nameof(category), category},
                {nameof(symbol), symbol.ToUpper()}
            };
            string query = string.Join("&", queryParams.Select(kvp => $"{kvp.Key}={kvp.Value}"));

            string uri = Helpers.BuildUri(TradingBotService._settings.Endpoint, EndpointProduct.Position, string.Concat(EndpointModule.List, '?', query));
            (string timestamp, string signature) = GenerateSignature(TradingBotService._settings, query);

            return await baseHttpClient.GetAsync<ApiResponse<GetPositionInfoResult, object>?>(uri, TradingBotService._settings.APIKey, timestamp, signature, TradingBotService._settings.RecvWindow);
        }

        /// <summary>
        /// Gets open and closed orders for category, symbol and openOnly with optional limit
        /// </summary>
        public async Task<ApiResponse<GetOpenAndClosedOrdersResult, object>?> GetOpenAndClosedOrders(string category, string symbol, int openOnly, int limit = 50)
        {
            Dictionary<string, string> queryParams = new()
            {
                {nameof(category), category},
                {nameof(symbol), symbol.ToUpper()},
                {nameof(openOnly), openOnly.ToString()},
                {nameof(limit), limit.ToString()}
            };
            string query = string.Join("&", queryParams.Select(kvp => $"{kvp.Key}={kvp.Value}"));

            string uri = Helpers.BuildUri(TradingBotService._settings.Endpoint, EndpointProduct.Order, string.Concat(EndpointModule.RealTime, '?', query));
            (string timestamp, string signature) = GenerateSignature(TradingBotService._settings, query);

            return await baseHttpClient.GetAsync<ApiResponse<GetOpenAndClosedOrdersResult, object>?>(uri, TradingBotService._settings.APIKey, timestamp, signature, TradingBotService._settings.RecvWindow);
        }

        /// <summary>
        /// Sets leverage for category and symbol
        /// </summary>
        public async Task<ApiResponse<object, object>?> SetLeverage(string category, string symbol, string buyLeverage, string sellLeverage)
        {
            string uri = Helpers.BuildUri(TradingBotService._settings.Endpoint, EndpointProduct.Position, EndpointModule.SetLeverage);

            Dictionary<string, object> parameters = new()
            {
                {nameof(category), category},
                {nameof(symbol), symbol.ToUpper()},
                {nameof(buyLeverage), buyLeverage},
                {nameof(sellLeverage), sellLeverage}
            };

            string jsonPayload = JsonConvert.SerializeObject(parameters, _jsonSerializerSettings);
            (string timestamp, string signature) = GenerateSignature(TradingBotService._settings, jsonPayload);

            return await baseHttpClient.PostAsync<ApiResponse<object, object>?>(uri, jsonPayload, TradingBotService._settings.APIKey, timestamp, signature, TradingBotService._settings.RecvWindow);
        }

        /// <summary>
        /// Places order for category, symbol, side, order type and quantity with optional price, timeInforce and reduceOnly
        /// </summary>
        public async Task<ApiResponse<OrderResult, object>?> PlaceOrder(string category, string symbol, string side, string orderType, string qty, string price = "0", string timeInForce = "PostOnly", bool reduceOnly = false)
        {
            string uri = Helpers.BuildUri(TradingBotService._settings.Endpoint, EndpointProduct.Order, EndpointModule.Create);

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

            string jsonPayload = JsonConvert.SerializeObject(parameters, _jsonSerializerSettings);
            (string timestamp, string signature) = GenerateSignature(TradingBotService._settings, jsonPayload);

            return await baseHttpClient.PostAsync<ApiResponse<OrderResult, object>?>(uri, jsonPayload, TradingBotService._settings.APIKey, timestamp, signature, TradingBotService._settings.RecvWindow);
        }

        /// <summary>
        /// Batch places orders
        /// </summary>
        public async Task<ApiResponse<BatchOrderResult, BatchOrderRetExtInfo>?> BatchPlaceOrder(ApiRequest<BatchOrderRequest> request)
        {
            string uri = Helpers.BuildUri(TradingBotService._settings.Endpoint, EndpointProduct.Order, EndpointModule.CreateBatch);

            Dictionary<string, object> parameters = [];

            foreach (PropertyInfo property in request.GetType().GetProperties())
            {
                object? value = property.GetValue(request);

                if (value is not null && (value is not string stringValue || !string.IsNullOrEmpty(stringValue)))
                    parameters[property.Name.ToLower()] = value;
            }

            string jsonPayload = JsonConvert.SerializeObject(parameters, _jsonSerializerSettings);
            (string timestamp, string signature) = GenerateSignature(TradingBotService._settings, jsonPayload);

            return await baseHttpClient.PostAsync<ApiResponse<BatchOrderResult, BatchOrderRetExtInfo>?>(uri, jsonPayload, TradingBotService._settings.APIKey, timestamp, signature, TradingBotService._settings.RecvWindow);
        }

        /// <summary>
        /// Amends order for category, symbol, orderId, qty and price
        /// </summary>
        public async Task<ApiResponse<OrderResult, object>?> AmendOrder(string category, string symbol, string orderId, string qty, string price)
        {
            string uri = Helpers.BuildUri(TradingBotService._settings.Endpoint, EndpointProduct.Order, EndpointModule.Amend);

            Dictionary<string, object> parameters = new()
            {
                {nameof(category), category},
                {nameof(symbol), symbol.ToUpper()},
                {nameof(orderId), orderId},
                {nameof(qty), qty},
                {nameof(price), price}
            };

            string jsonPayload = JsonConvert.SerializeObject(parameters, _jsonSerializerSettings);
            (string timestamp, string signature) = GenerateSignature(TradingBotService._settings, jsonPayload);

            return await baseHttpClient.PostAsync<ApiResponse<OrderResult, object>?>(uri, jsonPayload, TradingBotService._settings.APIKey, timestamp, signature, TradingBotService._settings.RecvWindow);
        }

        /// <summary>
        /// Cancels all orders for category and symbol
        /// </summary>
        public async Task<ApiResponse<CancelAllOrdersResult, object>?> CancelAllOrders(string category, string symbol)
        {
            string uri = Helpers.BuildUri(TradingBotService._settings.Endpoint, EndpointProduct.Order, EndpointModule.CancelAll);

            Dictionary<string, object> parameters = new()
            {
                {nameof(category), category},
                {nameof(symbol), symbol.ToUpper()}
            };

            string jsonPayload = JsonConvert.SerializeObject(parameters, _jsonSerializerSettings);
            (string timestamp, string signature) = GenerateSignature(TradingBotService._settings, jsonPayload);

            return await baseHttpClient.PostAsync<ApiResponse<CancelAllOrdersResult, object>?>(uri, jsonPayload, TradingBotService._settings.APIKey, timestamp, signature, TradingBotService._settings.RecvWindow);
        }

        /// <summary>
        /// Generates signature with data
        /// </summary>
        private static (string Timestamp, string Signature) GenerateSignature(Settings settings, string data)
        {
            string timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString();
            string rawData = string.Concat(timestamp, settings.APIKey, settings.RecvWindow, data);
            using HMACSHA256 hmac = new(Encoding.UTF8.GetBytes(settings.APISecret));
            byte[] signature = hmac.ComputeHash(Encoding.UTF8.GetBytes(rawData));

            return (timestamp, Convert.ToHexStringLower(signature));
        }
    }
}
