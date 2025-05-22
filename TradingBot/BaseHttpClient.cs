using BybitPerpetualsTradingBot.Models;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System.Text;

namespace BybitPerpetualsTradingBot
{
    internal class BaseHttpClient
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<BaseHttpClient> _logger;
        private readonly RateLimiter _rateLimiter;
        private readonly Settings _settings;
        private static uint _requestCount = 0;
        private readonly Timer? _timer;
        private static readonly Lock _consoleLock = new();

        public BaseHttpClient(HttpClient httpClient, ILogger<BaseHttpClient> logger)
        {
            _httpClient = httpClient;
            _logger = logger;

            _settings = Helpers.LoadFileData<Settings>(TradingBotService.settingsFilePath, logger);
            _rateLimiter = new(_settings.APIRateLimit, TimeSpan.FromSeconds(1));
            _timer = new Timer(PrintRequestsPerSecond, null, TimeSpan.Zero, TimeSpan.FromSeconds(1));
        }

        /// <summary>
        /// GET request and returns deserialized response
        /// </summary>
        public async Task<TResponse?> GetAsync<TResponse>(string uri, string apiKey, string timestamp, string signature, string recvWindow)
        {
            HttpRequestMessage request = new(HttpMethod.Get, uri);
            AddHeaders(request, apiKey, timestamp, signature, recvWindow);

            return await SendAsync<TResponse>(request);
        }

        /// <summary>
        /// POST request and returns deserialized response
        /// </summary>
        public async Task<TResponse?> PostAsync<TResponse>(string uri, string jsonPayload, string apiKey, string timestamp, string signature, string recvWindow)
        {
            HttpRequestMessage request = new(HttpMethod.Post, uri)
            {
                Content = new StringContent(jsonPayload, Encoding.UTF8, "application/json")
            };
            AddHeaders(request, apiKey, timestamp, signature, recvWindow);

            return await SendAsync<TResponse>(request);
        }

        /// <summary>
        /// Sends HTTP request and handles response and deserialization
        /// </summary>
        protected async Task<TResponse?> SendAsync<TResponse>(HttpRequestMessage request)
        {
            await _rateLimiter.WaitAsync();

            lock (_consoleLock)
                Interlocked.Increment(ref _requestCount);

            using HttpResponseMessage response = await _httpClient.SendAsync(request);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("{RequestMethod} Request to {RequestUri} failed with status code {StatusCode}",
                      request.Method, request.RequestUri, response.StatusCode);

                return default;
            }

            string content = await response.Content.ReadAsStringAsync();

            try
            {
                TResponse? deserializedResponse = JsonConvert.DeserializeObject<TResponse?>(content);

                if (deserializedResponse is null)
                    _logger.LogWarning("Deserialization of response content returned null for {RequestMethod} request to {RequestUri}. Response content: {ResponseContent}",
                                      request.Method, request.RequestUri, content);

                return deserializedResponse ?? default;
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex, "Error deserializing response content for {RequestMethod} request to {RequestUri}. Response content: {ResponseContent}",
                                request.Method, request.RequestUri, content);

                return default;
            }
        }

        /// <summary>
        /// Adds headers to HTTP request
        /// </summary>
        private static void AddHeaders(HttpRequestMessage request, string apiKey, string timestamp, string signature, string recvWindow)
        {
            request.Headers.Add("X-BAPI-API-KEY", apiKey);
            request.Headers.Add("X-BAPI-TIMESTAMP", timestamp);
            request.Headers.Add("X-BAPI-SIGN", signature);
            request.Headers.Add("X-BAPI-RECV-WINDOW", recvWindow);
        }

        /// <summary>
        /// Prints HTTP requests per second
        /// </summary>
        private void PrintRequestsPerSecond(object? state)
        {
            lock (_consoleLock)
            {
                Console.ForegroundColor = ConsoleColor.DarkGray;
                Console.WriteLine($"HTTP/sec: {_requestCount}");
                Interlocked.Exchange(ref _requestCount, 0);
            }
        }
    }
}
