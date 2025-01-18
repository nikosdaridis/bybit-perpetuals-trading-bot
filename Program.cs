using Newtonsoft.Json;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;

namespace CryptoFuturesTradingBot
{
    internal sealed class TradingBot
    {
        private Settings? _settings;
        private string _settingsFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "settings.json");

        static void Main(string[] args)
        {
            TradingBot tradingBot = new();
            tradingBot.LoadSettings();

            if (string.IsNullOrEmpty(tradingBot?._settings?.APIKey) || string.IsNullOrEmpty(tradingBot?._settings?.APISecret))
            {
                Console.WriteLine("API Key or Secret is missing in settings.json");
                return;
            }

            Dictionary<string, object> parameters = new()
            {
                {"category", "linear"},
                {"symbol", "BTCUSDT"},
                {"side", "Buy"},
                {"orderType", "Limit"},
                {"qty", "0.001"},
                {"price", "50000"},
                {"timeInForce", "GTC"},
                {"positionIdx", "0"},
                {"takeProfit", "80000"}
            };

            string timestap = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString();
            string signature = GenerateSignature(tradingBot._settings, timestap, parameters);
            string jsonPayload = JsonConvert.SerializeObject(parameters);

            using HttpClient client = new();
            HttpRequestMessage request = new(HttpMethod.Post, StringUtility.BuildUri(tradingBot._settings.Endpoint, "order", "create"))
            {
                Content = new StringContent(jsonPayload, Encoding.UTF8, "application/json")
            };

            request.Headers.Add("X-BAPI-API-KEY", tradingBot._settings.APIKey);
            request.Headers.Add("X-BAPI-TIMESTAMP", timestap);
            request.Headers.Add("X-BAPI-SIGN", signature);
            request.Headers.Add("X-BAPI-RECV-WINDOW", tradingBot._settings.RecvWindow);

            HttpResponseMessage response = client.SendAsync(request).Result;
            Console.WriteLine(response.Content.ReadAsStringAsync().Result);

            Console.ReadLine();
        }

        /// <summary>
        /// Loads settings from the settings file and ensures all properties are present
        /// </summary>
        private void LoadSettings()
        {
            try
            {
                VerifySettings(File.ReadAllText(_settingsFilePath));
            }
            catch
            {
                VerifySettings();
            }

            // Verifies all properties are present and of the correct type in the settings file
            void VerifySettings(string jsonContent = "")
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

                    _settings = JsonConvert.DeserializeObject<Settings>(updatedJson) ?? new();
                }
                else
                {
                    _settings = JsonConvert.DeserializeObject<Settings>(jsonContent);
                }
            }
        }

        /// <summary>
        /// Generates signature for the Post request
        /// </summary>
        private static string GenerateSignature(Settings settings, string timestap, IDictionary<string, object> parameters)
        {
            string rawData = string.Concat(timestap, settings.APIKey, settings.RecvWindow, JsonConvert.SerializeObject(parameters));
            using HMACSHA256 hmac = new(Encoding.UTF8.GetBytes(settings.APISecret));
            byte[] signature = hmac.ComputeHash(Encoding.UTF8.GetBytes(rawData));

            return Convert.ToHexStringLower(signature);
        }
    }
}
