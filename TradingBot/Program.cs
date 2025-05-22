using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Globalization;

namespace BybitPerpetualsTradingBot
{
    internal sealed class TradingBot
    {
        static async Task Main()
        {
            CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;

            ServiceCollection serviceCollection = new();
            serviceCollection.AddServices();
            ServiceProvider serviceProvider = serviceCollection.BuildServiceProvider();

            TradingBotService tradingBotService = serviceProvider.GetRequiredService<TradingBotService>();
            ILogger<TradingBot> logger = serviceProvider.GetRequiredService<ILogger<TradingBot>>();

            bool initialized = false;

            while (true)
            {
                try
                {
                    if (!initialized)
                    {
                        initialized = await tradingBotService.InitializeActiveTradingPairs();
                        continue;
                    }

                    await tradingBotService.ExecuteTasksConcurrently(
                    [
                        tradingBotService.PlaceInitialPosition,
                        tradingBotService.PlaceOrAmendTakeProfitOrder,
                        tradingBotService.PlaceScalingOrders
                    ]);
                }
                catch (Exception ex)
                {
                    Helpers.LogAndPrint(logger, LogLevel.Error, "Error occurred in trading loop: {0}", ex.Message);
                }
            }
        }
    }
}
