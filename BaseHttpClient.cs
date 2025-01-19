using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System.Text;

namespace CryptoFuturesTradingBot
{
    internal class BaseHttpClient(HttpClient httpClient, ILogger<BaseHttpClient> logger)
    {
        /// <summary>
        /// GET request to specified URI and returns deserialized response
        /// </summary>
        public Task<TResponse?> GetAsync<TResponse>(string requestUri) =>
            SendAsync<TResponse?>(new HttpRequestMessage(HttpMethod.Get, requestUri));

        /// <summary>
        /// POST request to specified URI with payload and headers and returns deserialized response
        /// </summary>
        public async Task<TResponse?> PostAsync<TResponse>(string requestUri, string payload, string apiKey, string timestamp, string signature, string recvWindow = "5000")
        {
            HttpRequestMessage request = new(HttpMethod.Post, requestUri)
            {
                Content = new StringContent(payload, Encoding.UTF8, "application/json")
            };

            request.Headers.Add("X-BAPI-API-KEY", apiKey);
            request.Headers.Add("X-BAPI-TIMESTAMP", timestamp);
            request.Headers.Add("X-BAPI-SIGN", signature);
            request.Headers.Add("X-BAPI-RECV-WINDOW", recvWindow.ToString());

            return await SendAsync<TResponse>(request);
        }

        /// <summary>
        /// Sends HTTP request and handles response and deserialization
        /// </summary>
        protected async Task<TResponse?> SendAsync<TResponse>(HttpRequestMessage request)
        {
            using HttpResponseMessage response = await httpClient.SendAsync(request);

            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("{RequestMethod} Request to {RequestUri} failed with status code {StatusCode}",
                      request.Method, request.RequestUri, response.StatusCode);

                return default;
            }

            string content = await response.Content.ReadAsStringAsync();

            try
            {
                TResponse? deserializedResponse = JsonConvert.DeserializeObject<TResponse?>(content);

                if (deserializedResponse is null)
                    logger.LogWarning("Deserialization of response content returned null for {RequestMethod} request to {RequestUri}. Response content: {ResponseContent}",
                                      request.Method, request.RequestUri, content);

                return deserializedResponse ?? default;
            }
            catch (JsonException ex)
            {
                logger.LogError(ex, "Error deserializing response content for {RequestMethod} request to {RequestUri}. Response content: {ResponseContent}",
                                request.Method, request.RequestUri, content);

                return default;
            }
        }
    }
}
