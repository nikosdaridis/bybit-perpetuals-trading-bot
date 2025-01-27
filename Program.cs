using BybitPerpetualsTradingBot.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Globalization;

namespace BybitPerpetualsTradingBot
{
    internal sealed class TradingBot(ILogger<TradingBot> logger)
    {
        private readonly ILogger<TradingBot> _logger = logger;
        private readonly SemaphoreSlim semaphoreSlim = new(1, 1);
        private TradingBotState _state = new();

        static async Task Main()
        {
            CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;

            ServiceCollection serviceCollection = new();
            ConfigureServices.AddServices(serviceCollection);
            ServiceProvider serviceProvider = serviceCollection.BuildServiceProvider();

            TradingBot tradingBot = serviceProvider.GetRequiredService<TradingBot>();
            TradingBotHelper.InitializeDependencies(serviceProvider.GetRequiredService<BaseHttpClient>(), tradingBot._logger);

            while (true)
            {
                try
                {
                    await tradingBot.semaphoreSlim.WaitAsync();

                    if (!tradingBot._state.InitializedActiveTradingPairs)
                    {
                        tradingBot._state.InitializedActiveTradingPairs = await TradingBotHelper.InitializeActiveTradingPairs();
                        continue;
                    }

                    await TradingBotHelper.PlaceInitialPositions();
                    await TradingBotHelper.PlaceTakeProfitOrders();
                    await TradingBotHelper.PlaceScalingOrders();
                }
                catch (Exception ex)
                {
                    tradingBot._logger.LogError(ex, "Error occurred in trading loop");
                    Console.WriteLine($"Error occurred in trading loop: {ex.Message}");
                }
                finally
                {
                    tradingBot.semaphoreSlim.Release();
                }
            }
        }
    }
}
