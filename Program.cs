using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace CryptoFuturesTradingBot
{
    internal sealed class TradingBot(BaseHttpClient baseHttpClient, ILogger<TradingBot> logger)
    {
        private readonly BaseHttpClient _baseHttpClient = baseHttpClient;
        private readonly ILogger<TradingBot> _logger = logger;
        private Settings? _settings;

        static async Task Main()
        {
            ServiceCollection serviceCollection = new();
            ConfigureServices.AddServices(serviceCollection, TradingBotHelper.LoadSettings());
            ServiceProvider serviceProvider = serviceCollection.BuildServiceProvider();

            TradingBot tradingBot = serviceProvider.GetRequiredService<TradingBot>();
            tradingBot._settings = TradingBotHelper.LoadSettings();

            if (tradingBot?._settings is null || string.IsNullOrEmpty(tradingBot?._settings?.APIKey) || string.IsNullOrEmpty(tradingBot?._settings?.APISecret))
            {
                Console.WriteLine(tradingBot?._settings is null
                    ? "Settings file not found or invalid"
                    : "API key or secret not found in settings file"
                );
                tradingBot?._logger.LogError(tradingBot?._settings is null
                    ? "Settings file not found or invalid"
                    : "API key or secret not found in settings file"
                );

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
            string signature = TradingBotHelper.GenerateSignature(tradingBot._settings, timestap, parameters);
            string jsonPayload = JsonConvert.SerializeObject(parameters);

            object? response = await tradingBot._baseHttpClient.PostAsync<object>(
                TradingBotHelper.BuildUri(tradingBot._settings.Endpoint, "order", "create"),
                jsonPayload,
                tradingBot._settings.APIKey,
                timestap,
                signature,
                tradingBot._settings.RecvWindow
            );

            Console.WriteLine(response);
            tradingBot._logger.LogInformation("Test");

            Console.ReadLine();
        }
    }
}
