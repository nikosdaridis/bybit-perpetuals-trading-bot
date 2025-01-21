using BybitPerpetualsTradingBot.Models.API;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace BybitPerpetualsTradingBot
{
    internal sealed class TradingBot(BaseHttpClient baseHttpClient, ILogger<TradingBot> logger)
    {
        private readonly BaseHttpClient _baseHttpClient = baseHttpClient;
        private readonly ILogger<TradingBot> _logger = logger;

        static async Task Main()
        {
            ServiceCollection serviceCollection = new();
            ConfigureServices.AddServices(serviceCollection);
            ServiceProvider serviceProvider = serviceCollection.BuildServiceProvider();

            TradingBot tradingBot = serviceProvider.GetRequiredService<TradingBot>();
            TradingBotHelper.InitializeDependencies(tradingBot._baseHttpClient);

            //Get Instruments Info
            ApiResponse<GetInstrumentsInfoResult>? responseInstrumentsInfo = await TradingBotHelper.GetInstrumentsInfo(ApiParams.Category.Linear);
            Console.WriteLine($"InstrumentsInfo - Code: {responseInstrumentsInfo?.RetCode}, Message {responseInstrumentsInfo?.RetMsg}");
            if (responseInstrumentsInfo?.RetCode != 0)
                tradingBot._logger.LogError("Error getting instruments info: {RetMsg}", responseInstrumentsInfo?.RetMsg);

            Dictionary<string, GetInstrumentsInfoResult.InstrumentList>? responseInstrumentsInfoDictionary = responseInstrumentsInfo?.Result?.List?.ToDictionary(List => List.Symbol ?? "", List => List);
            Console.WriteLine(responseInstrumentsInfoDictionary?["BTCUSDT"]?.LotSizeFilter?.MinOrderQty);

            //Get position info
            ApiResponse<GetPositionInfoResult>? responseGetPositionInfo = await TradingBotHelper.GetPositionInfo(ApiParams.Category.Linear, "BTCUSDT");
            Console.WriteLine($"PositionInfo - Code: {responseGetPositionInfo?.RetCode}, Message {responseGetPositionInfo?.RetMsg}");
            if (responseGetPositionInfo?.RetCode != 0)
                tradingBot._logger.LogError("Error getting position info: {RetMsg}", responseGetPositionInfo?.RetMsg);

            //Set leverage
            ApiResponse<object>? responseSetLeverage = await TradingBotHelper.SetLeverage(ApiParams.Category.Linear, "BTCUSDT", "100", "100");
            Console.WriteLine($"SetLeverage - Code: {responseSetLeverage?.RetCode}, Message {responseSetLeverage?.RetMsg}");
            if (responseSetLeverage?.RetCode != 0 && responseSetLeverage?.RetCode != 110043)
                tradingBot._logger.LogError("Error setting leverage: {RetMsg}", responseSetLeverage?.RetMsg);

            //Place order
            ApiResponse<PlaceOrderResult>? responsePlaceOrder = await TradingBotHelper.PlaceOrder(ApiParams.Category.Linear, "BTCUSDT", ApiParams.Side.Buy, ApiParams.OrderType.Limit, "0.001", "50000");
            Console.WriteLine($"PlaceOrder - Code: {responsePlaceOrder?.RetCode}, Message {responsePlaceOrder?.RetMsg}");
            if (responsePlaceOrder?.RetCode != 0)
                tradingBot._logger.LogError("Error placing order: {RetMsg}", responsePlaceOrder?.RetMsg);

            //Batch Place Order

            //Amend order

            //Cancel all orders

            Console.ReadLine();
        }
    }
}
