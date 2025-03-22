using Newtonsoft.Json;

namespace TradingBotConfigurator
{
    internal class BaseHttpClient(HttpClient httpClient)
    {
        /// <summary>
        /// GET request and returns deserialized response
        /// </summary>
        public async Task<TResponse?> GetAsync<TResponse>(string uri)
        {
            HttpRequestMessage request = new(HttpMethod.Get, uri);

            return await SendAsync<TResponse>(request);
        }

        /// <summary>
        /// Sends HTTP request and handles response and deserialization
        /// </summary>
        protected async Task<TResponse?> SendAsync<TResponse>(HttpRequestMessage request)
        {
            using HttpResponseMessage response = await httpClient.SendAsync(request);

            if (!response.IsSuccessStatusCode)
                return default;

            string content = await response.Content.ReadAsStringAsync();

            try
            {
                TResponse? deserializedResponse = JsonConvert.DeserializeObject<TResponse?>(content);

                return deserializedResponse ?? default;
            }
            catch
            {
                return default;
            }
        }
    }
}
