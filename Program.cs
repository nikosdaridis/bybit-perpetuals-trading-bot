using BybitPerpetualsTradingBot.Models.API;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using static BybitPerpetualsTradingBot.Models.API.ApiParameters;

namespace BybitPerpetualsTradingBot
{
    internal sealed class TradingBot(ILogger<TradingBot> logger)
    {
        private readonly ILogger<TradingBot> _logger = logger;

        static async Task Main()
        {
            ServiceCollection serviceCollection = new();
            ConfigureServices.AddServices(serviceCollection);
            ServiceProvider serviceProvider = serviceCollection.BuildServiceProvider();

            TradingBot tradingBot = serviceProvider.GetRequiredService<TradingBot>();
            TradingBotHelper.InitializeDependencies(serviceProvider.GetRequiredService<BaseHttpClient>());

            //Get Instruments Info
            ApiResponse<GetInstrumentsInfoResult, object>? responseInstrumentsInfo = await TradingBotHelper.GetInstrumentsInfo(Category.Linear);
            Console.WriteLine($"InstrumentsInfo - Code: {responseInstrumentsInfo?.RetCode}, Message: {responseInstrumentsInfo?.RetMsg}");
            if (responseInstrumentsInfo?.RetCode != 0)
                tradingBot._logger.LogError("Error getting instruments info: {RetMsg}", responseInstrumentsInfo?.RetMsg);

            Dictionary<string, GetInstrumentsInfoResult.InstrumentList>? responseInstrumentsInfoDictionary = responseInstrumentsInfo?.Result?.List?.ToDictionary(List => List.Symbol ?? "", List => List);
            Console.WriteLine(responseInstrumentsInfoDictionary?["BTCUSDT"]?.LotSizeFilter?.MinOrderQty);

            //Get Position Info
            ApiResponse<GetPositionInfoResult, object>? responseGetPositionInfo = await TradingBotHelper.GetPositionInfo(Category.Linear, "BTCUSDT");
            Console.WriteLine($"PositionInfo - Code: {responseGetPositionInfo?.RetCode}, Message: {responseGetPositionInfo?.RetMsg}");
            if (responseGetPositionInfo?.RetCode != 0)
                tradingBot._logger.LogError("Error getting position info: {RetMsg}", responseGetPositionInfo?.RetMsg);

            //Get Open and Closed Orders
            ApiResponse<GetOpenAndClosedOrdersResult, object>? responseGetOpenAndClosedOrders = await TradingBotHelper.GetOpenAndClosedOrders(Category.Linear, "BTCUSDT", OpenOnly.True);
            Console.WriteLine($"OpenAndClosedOrders - Code: {responseGetOpenAndClosedOrders?.RetCode}, Message: {responseGetOpenAndClosedOrders?.RetMsg}");
            if (responseGetOpenAndClosedOrders?.RetCode != 0)
                tradingBot._logger.LogError("Error getting open and closed orders: {RetMsg}", responseGetOpenAndClosedOrders?.RetMsg);

            //Set Leverage
            ApiResponse<object, object>? responseSetLeverage = await TradingBotHelper.SetLeverage(Category.Linear, "BTCUSDT", "100", "100");
            Console.WriteLine($"SetLeverage - Code: {responseSetLeverage?.RetCode}, Message:{responseSetLeverage?.RetMsg}");
            if (responseSetLeverage?.RetCode != 0 && responseSetLeverage?.RetCode != 110043)
                tradingBot._logger.LogError("Error setting leverage: {RetMsg}", responseSetLeverage?.RetMsg);

            //Place Order
            ApiResponse<OrderResult, object>? responsePlaceOrder = await TradingBotHelper.PlaceOrder(Category.Linear, "BTCUSDT", Side.Buy, OrderType.Limit, "0.001", "40000");
            Console.WriteLine($"PlaceOrder - Code: {responsePlaceOrder?.RetCode}, Message: {responsePlaceOrder?.RetMsg}");
            if (responsePlaceOrder?.RetCode != 0)
                tradingBot._logger.LogError("Error placing order: {RetMsg}", responsePlaceOrder?.RetMsg);

            //Batch Place Order
            ApiRequest<BatchOrderRequest> batchOrderRequest = new()
            {
                Category = Category.Linear,
                Request =
                [
                    new()
                    {
                        Symbol = "BTCUSDT",
                        Side = Side.Buy,
                        OrderType = OrderType.Limit,
                        Qty = "0.001",
                        Price = "50000",
                        TimeInForce = TimeInForce.GoodTillCancel
                    },
                    new()
                    {
                        Symbol = "BTCUSDT",
                        Side = Side.Buy,
                        OrderType = OrderType.Limit,
                        Qty = "0.001",
                        Price = "60000",
                        TimeInForce = TimeInForce.FillOrKill
                    },
                    new()
                    {
                        Symbol = "BTCUSDT",
                        Side = Side.Buy,
                        OrderType = OrderType.Limit,
                        Qty = "0.001",
                        Price = "70000",
                        TimeInForce = TimeInForce.ImmediateOrCancel
                    },
                    new()
                    {
                        Symbol = "BTCUSDT",
                        Side = Side.Buy,
                        OrderType = OrderType.Limit,
                        Qty = "0.001",
                        Price = "80000",
                        TimeInForce = TimeInForce.PostOnly
                    }
                ]
            };

            ApiResponse<BatchOrderResult, BatchOrderRetExtInfo>? responseBatchPlaceOrder = await TradingBotHelper.BatchPlaceOrder(batchOrderRequest);
            Console.WriteLine($"BatchPlaceOrder - Code: {responseBatchPlaceOrder?.RetCode}, Message: {responseBatchPlaceOrder?.RetMsg}");
            if (responseBatchPlaceOrder?.RetCode != 0)
                tradingBot._logger.LogError("Error placing batch order: {RetMsg}", responseBatchPlaceOrder?.RetMsg);

            foreach (BatchOrderRetExtInfo.RetExtDetails orderRetExtDetails in responseBatchPlaceOrder?.RetExtInfo?.List ?? [])
            {
                if (orderRetExtDetails?.Code != 0)
                    tradingBot._logger.LogError("Error placing order: {RetMsg}", orderRetExtDetails?.Msg);
            }

            //Amend Order

            //Cancel All Orders

            Console.ReadLine();
        }
    }
}
