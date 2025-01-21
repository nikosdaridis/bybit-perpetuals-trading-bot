using BybitPerpetualsTradingBot.Models;
using BybitPerpetualsTradingBot.Models.API;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace BybitPerpetualsTradingBot
{
    internal sealed class TradingBot(BaseHttpClient baseHttpClient, ILogger<TradingBot> logger)
    {
        private readonly BaseHttpClient _baseHttpClient = baseHttpClient;
        private readonly ILogger<TradingBot> _logger = logger;
        private Settings? _settings;

        static async Task Main()
        {
            ServiceCollection serviceCollection = new();
            ConfigureServices.AddServices(serviceCollection);
            ServiceProvider serviceProvider = serviceCollection.BuildServiceProvider();

            TradingBot tradingBot = serviceProvider.GetRequiredService<TradingBot>();
            tradingBot._settings = TradingBotHelper.LoadSettings();
            TradingBotHelper.InitializeDependencies(tradingBot._settings, tradingBot._baseHttpClient);

            if (string.IsNullOrEmpty(tradingBot?._settings?.APIKey) || string.IsNullOrEmpty(tradingBot?._settings?.APISecret))
            {
                Console.WriteLine("API key or secret not found in settings file");
                tradingBot?._logger.LogError("API key or secret not found in settings file");
                return;
            }

            //Set leverage
            ApiResponse<object>? responseSetLeverage = await TradingBotHelper.SetLeverage(ApiParameters.Category.Linear, "BTCUSDT", "10", "10");
            Console.WriteLine(responseSetLeverage?.RetCode);
            Console.WriteLine(responseSetLeverage?.RetMsg);

            if (responseSetLeverage?.RetCode != 0 || responseSetLeverage.RetCode != 110043)
            {
                tradingBot._logger.LogError("Error setting margin mode and leverage: {RetMsg}", responseSetLeverage?.RetMsg);
            }

            //Get position info
            ApiResponse<object>? responseGetPositionInfo = await TradingBotHelper.GetPositionInfo(ApiParameters.Category.Linear, "BTCUSDT");
            Console.WriteLine(responseGetPositionInfo?.RetCode);
            Console.WriteLine(responseGetPositionInfo?.RetMsg);

            if (responseGetPositionInfo?.RetCode != 0)
            {
                tradingBot._logger.LogError("Error getting position info: {RetMsg}", responseGetPositionInfo?.RetMsg);
            }

            //Place order
            //Batch Place Order
            //Amend order
            //Cancel all orders

            Console.ReadLine();
        }
    }
}
