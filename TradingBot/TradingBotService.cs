using BybitPerpetualsTradingBot.Models;
using BybitPerpetualsTradingBot.Models.API;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using static BybitPerpetualsTradingBot.Models.API.ApiParameters;
using static BybitPerpetualsTradingBot.Models.API.GetInstrumentsInfoResult;
using static BybitPerpetualsTradingBot.Models.API.GetOpenAndClosedOrdersResult;
using static BybitPerpetualsTradingBot.Models.API.GetPositionInfoResult;
using static BybitPerpetualsTradingBot.Models.PairsConfiguration;

namespace BybitPerpetualsTradingBot
{
    internal class TradingBotService
    {
        private readonly ApiService _apiService;
        private readonly ILogger<TradingBotService> _logger;

        internal static readonly string settingsFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "settings.json");
        internal static readonly string pairsFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "pairs.json");
        internal static Settings _settings = new();
        internal static PairsConfiguration _pairsConfiguration = new();

        private Dictionary<string, InstrumentList> _instrumentsInfo = [];
        private readonly Dictionary<string, ActiveTradingPair> _activeTradingPairs = [];

        public TradingBotService(ApiService apiService, ILogger<TradingBotService> logger)
        {
            _apiService = apiService;
            _logger = logger;

            _settings = Helpers.LoadFileData<Settings>(settingsFilePath, _logger);
            _pairsConfiguration = Helpers.LoadFileData<PairsConfiguration>(pairsFilePath, _logger);
        }

        /// <summary>
        /// Executes trading tasks concurrently for pairs
        /// </summary>
        internal async Task ExecuteTasksConcurrently(List<Func<string, ActiveTradingPair, Task<bool>>> taskFuncs)
        {
            SemaphoreSlim semaphore = new(_settings.RunningTasks, _settings.RunningTasks);
            Stopwatch stopwatch = Stopwatch.StartNew();

            foreach (Func<string, ActiveTradingPair, Task<bool>> taskFunc in taskFuncs)
            {
                List<Task<bool>> tasks = [.. _activeTradingPairs.Select(async pair =>
                {
                    await semaphore.WaitAsync();

                    try
                    {
                        return await taskFunc(pair.Key, pair.Value);
                    }
                    catch (Exception ex)
                    {
                        Helpers.LogAndPrint(_logger, LogLevel.Error, "({0}) Exception in task execution for {1}: {2}", taskFunc.Method.Name, pair.Key, ex.Message);
                        return false;
                    }
                    finally
                    {
                        semaphore.Release();
                    }
                })];

                await Task.WhenAll(tasks);
            }

            stopwatch.Stop();
            Console.ForegroundColor = ConsoleColor.DarkGreen;
            Console.WriteLine($"Loop done {stopwatch.Elapsed.TotalSeconds:F1} sec");
        }

        /// <summary>
        /// Initializes active trading pairs and validates pairs configuration data with API data
        /// </summary>
        internal async Task<bool> InitializeActiveTradingPairs()
        {
            Console.ForegroundColor = ConsoleColor.DarkYellow;
            Console.WriteLine("Initializing active trading pairs and validating configuration data");

            ApiResponse<GetInstrumentsInfoResult, object>? responseInstrumentsInfo = await _apiService.GetInstrumentsInfo(Category.Linear);
            if (responseInstrumentsInfo?.RetCode != 0)
            {
                Helpers.LogAndPrint(_logger, LogLevel.Error, "Error getting instruments info: {0}", responseInstrumentsInfo?.RetMsg);
                return false;
            }

            _instrumentsInfo = responseInstrumentsInfo?.Result?.List?.ToDictionary(List => List.Symbol ?? string.Empty, List => List) ?? [];

            if (_instrumentsInfo.Count == 0)
            {
                Helpers.LogAndPrint(_logger, LogLevel.Error, "No instruments info found");
                return false;
            }

            if (string.IsNullOrEmpty(_settings.APIKey))
            {
                Helpers.LogAndPrint(_logger, LogLevel.Error, "API key is not set in settings file");
                return false;
            }

            if (string.IsNullOrEmpty(_settings.APISecret))
            {
                Helpers.LogAndPrint(_logger, LogLevel.Error, "API secret is not set in settings file");
                return false;
            }

            foreach (string pair in _pairsConfiguration.ActiveTradingPairs)
            {
                if (!_instrumentsInfo.TryGetValue(pair, out InstrumentList? instrumentList))
                {
                    Helpers.LogAndPrint(_logger, LogLevel.Error, "{0}: not found in instruments info", pair);
                    return false;
                }

                if (instrumentList is null)
                {
                    Helpers.LogAndPrint(_logger, LogLevel.Error, "{0}: instrument list is null", pair);
                    return false;
                }

                if (!_pairsConfiguration.PairsConfigurations.TryGetValue(pair, out PairConfiguration? pairConfiguration))
                {
                    Helpers.LogAndPrint(_logger, LogLevel.Error, "{0}: not found in pairs configuration", pair);
                    return false;
                }

                if (pairConfiguration is null)
                {
                    Helpers.LogAndPrint(_logger, LogLevel.Error, "{0}: configuration is null", pair);
                    return false;
                }

                if (pairConfiguration.Side is null || (pairConfiguration.Side != Side.Buy && pairConfiguration.Side != Side.Sell))
                {
                    Helpers.LogAndPrint(_logger, LogLevel.Error, "{0}: side {1} is invalid", pair, pairConfiguration.Side);
                    return false;
                }

                if (pairConfiguration.Leverage is null || pairConfiguration.Leverage <= 0)
                {
                    Helpers.LogAndPrint(_logger, LogLevel.Error, "{0}: leverage {1} is invalid", pair, pairConfiguration.Leverage);
                    return false;
                }

                if (Helpers.TryParseDecimal(instrumentList.LeverageFilter?.MinLeverage, out decimal minLeverage) &&
                    Helpers.TryParseDecimal(instrumentList.LeverageFilter?.MaxLeverage, out decimal maxLeverage) &&
                    Helpers.TryParseDecimal(instrumentList.LeverageFilter?.LeverageStep, out decimal leverageStep))
                {
                    if (pairConfiguration.Leverage < minLeverage || pairConfiguration.Leverage > maxLeverage || (pairConfiguration.Leverage - minLeverage) % leverageStep != 0)
                    {
                        Helpers.LogAndPrint(_logger, LogLevel.Error, "{0}: leverage {1} is invalid", pair, pairConfiguration.Leverage);
                        return false;
                    }
                }
                else
                {
                    Helpers.LogAndPrint(_logger, LogLevel.Error, "{0}: leverage filter data is invalid", pair);
                    return false;
                }

                if (pairConfiguration.InitialMargin is null || pairConfiguration.InitialMargin <= 0)
                {
                    Helpers.LogAndPrint(_logger, LogLevel.Error, "{0}: initial margin {1} is invalid", pair, pairConfiguration.InitialMargin);
                    return false;
                }

                if (pairConfiguration.InitialPriceTickSizeOffset is null || pairConfiguration.InitialPriceTickSizeOffset < 0)
                {
                    Helpers.LogAndPrint(_logger, LogLevel.Error, "{0}: initial price tick size offset {1} is invalid", pair, pairConfiguration.InitialPriceTickSizeOffset);
                    return false;
                }

                if (pairConfiguration.InitialPriceTickSizeThreshold is null || pairConfiguration.InitialPriceTickSizeThreshold <= 0 ||
                    pairConfiguration.InitialPriceTickSizeThreshold <= pairConfiguration.InitialPriceTickSizeOffset)
                {
                    Helpers.LogAndPrint(_logger, LogLevel.Error, "{0}: initial price tick size threshold {1} is invalid", pair, pairConfiguration.InitialPriceTickSizeThreshold);
                    return false;
                }

                if (pairConfiguration.TakeProfitPercentage is null || pairConfiguration.TakeProfitPercentage <= 0)
                {
                    Helpers.LogAndPrint(_logger, LogLevel.Error, "{0}: take profit percentage {1} is invalid", pair, pairConfiguration.TakeProfitPercentage);
                    return false;
                }

                if (pairConfiguration.NumberOfScalingLevels is null || pairConfiguration.NumberOfScalingLevels < 0 || pairConfiguration.NumberOfScalingLevels > 99)
                {
                    Helpers.LogAndPrint(_logger, LogLevel.Error, "{0}: number of scaling levels {1} is invalid", pair, pairConfiguration.NumberOfScalingLevels);
                    return false;
                }

                if (pairConfiguration.InitialScalingUnrealizedPnL is null || pairConfiguration.InitialScalingUnrealizedPnL <= 0)
                {
                    Helpers.LogAndPrint(_logger, LogLevel.Error, "{0}: initial scaling unrealised PnL percentage {1} is invalid", pair, pairConfiguration.InitialScalingUnrealizedPnL);
                    return false;
                }

                if (pairConfiguration.InitialScalingQuantityMultiplier is null || pairConfiguration.InitialScalingQuantityMultiplier <= 0)
                {
                    Helpers.LogAndPrint(_logger, LogLevel.Error, "A{0}: initial scaling quantity multiplier {1} is invalid", pair, pairConfiguration.InitialScalingQuantityMultiplier);
                    return false;
                }

                if (pairConfiguration.MaxScalingUnrealizedPnL is null || pairConfiguration.MaxScalingUnrealizedPnL < pairConfiguration.InitialScalingUnrealizedPnL)
                {
                    Helpers.LogAndPrint(_logger, LogLevel.Error, "{0}: max scaling unrealised PnL percentage {1} is invalid", pair, pairConfiguration.MaxScalingUnrealizedPnL);
                    return false;
                }

                if (pairConfiguration.ScalingUnrealisedPnlMultiplier is null || pairConfiguration.ScalingUnrealisedPnlMultiplier <= 0)
                {
                    Helpers.LogAndPrint(_logger, LogLevel.Error, "{0}: scaling unrealised PnL multiplier {1} is invalid", pair, pairConfiguration.ScalingUnrealisedPnlMultiplier);
                    return false;
                }

                if (pairConfiguration.ScalingQuantityMultiplier is null || pairConfiguration.ScalingQuantityMultiplier <= 0)
                {
                    Helpers.LogAndPrint(_logger, LogLevel.Error, "{0}: scaling quantity multiplier {1} is invalid", pair, pairConfiguration.ScalingQuantityMultiplier);
                    return false;
                }

                _activeTradingPairs.TryAdd(pair, new()
                {
                    Configuration = pairConfiguration
                });

                ApiResponse<GetPositionInfoResult, object>? responsePositionInfo = await _apiService.GetPositionInfo(Category.Linear, pair);
                if (responsePositionInfo?.RetCode != 0)
                {
                    Helpers.LogAndPrint(_logger, LogLevel.Error, "{0}: Error getting position info, {1}", pair, responsePositionInfo?.RetMsg);
                    return false;
                }

                ApiResponse<GetTickersResult, object>? responseTickers = await _apiService.GetTickers(Category.Linear, pair);
                if (responseTickers?.RetCode != 0)
                {
                    Helpers.LogAndPrint(_logger, LogLevel.Error, "{0}: Error getting tickers, {1}", pair, responseTickers?.RetMsg);
                    return false;
                }
            }

            Console.ForegroundColor = ConsoleColor.DarkGreen;
            Console.WriteLine("Active trading pairs initialized successfully");

            return true;
        }

        /// <summary>
        /// Places initial position for active trading pair
        /// </summary>
        internal async Task<bool> PlaceInitialPosition(string Symbol, ActiveTradingPair ActiveTradingPair)
        {
            ApiResponse<GetPositionInfoResult, object>? responsePositionInfo = await _apiService.GetPositionInfo(Category.Linear, Symbol);
            if (responsePositionInfo?.RetCode != 0)
            {
                Helpers.LogAndPrint(_logger, LogLevel.Error, "{0}: Error getting position info, {1}", Symbol, responsePositionInfo?.RetMsg);
                return false;
            }
            GetPositionInfoList position = responsePositionInfo?.Result?.List?.FirstOrDefault() ?? new();

            if (!Helpers.TryParseDecimal(position.Size, out decimal size) || size != 0)
                return false;

            ApiResponse<GetTickersResult, object>? responseTickers = await _apiService.GetTickers(Category.Linear, Symbol);
            if (responseTickers?.RetCode != 0)
            {
                Helpers.LogAndPrint(_logger, LogLevel.Error, "{0}: Error getting tickers, {1}", Symbol, responseTickers?.RetMsg);
                return false;
            }

            ApiResponse<GetOpenAndClosedOrdersResult, object>? responseOpenOrders = await _apiService.GetOpenAndClosedOrders(Category.Linear, Symbol, OpenOnly.True);
            if (responseOpenOrders?.RetCode != 0)
            {
                Helpers.LogAndPrint(_logger, LogLevel.Error, "{0}: Error getting open orders, {1}", Symbol, responseOpenOrders?.RetMsg);
                return false;
            }

            if (responseOpenOrders.Result?.List?.Any(order => order.OrderStatus == OrderStatus.PartiallyFilled) == true)
                return false;

            // Check if initial order price is within threshold of last price
            if (responseOpenOrders.Result?.List?.Count == 1 && responseOpenOrders.Result.List.Any(order =>
            {
                if (Helpers.TryParseDecimal(order.Price, out decimal orderPrice) &&
                    Helpers.TryParseDecimal(responseTickers.Result?.List?.FirstOrDefault()?.LastPrice, out decimal lastPrice) &&
                    Helpers.TryParseDecimal(_instrumentsInfo[Symbol].PriceFilter?.TickSize, out decimal tickSize))
                {
                    bool isWithinThreshold = Math.Abs(orderPrice - lastPrice) < tickSize * ActiveTradingPair.Configuration.InitialPriceTickSizeThreshold;

                    return isWithinThreshold;
                }

                return false;
            }) == true)
                return false;

            ApiResponse<CancelAllOrdersResult, object>? responseCancelAll = await _apiService.CancelAllOrders(Category.Linear, Symbol);
            if (responseCancelAll?.RetCode != 0)
            {
                Helpers.LogAndPrint(_logger, LogLevel.Error, "{0}: Error cancelling all orders, {1}", Symbol, responseCancelAll?.RetMsg);
                return false;
            }

            Helpers.LogAndPrint(_logger, LogLevel.Information, "{0}: All orders cancelled", Symbol);

            if (!Helpers.TryParseDecimal(position.Leverage, out decimal leverage))
            {
                Helpers.LogAndPrint(_logger, LogLevel.Error, "{0}: Error parsing leverage", Symbol);
                return false;
            }

            if (leverage != ActiveTradingPair.Configuration.Leverage)
            {
                ApiResponse<object, object>? responseSetLeverage = await _apiService.SetLeverage(Category.Linear, Symbol, ActiveTradingPair.Configuration.Leverage.ToString()!, ActiveTradingPair.Configuration.Leverage.ToString()!);
                if (responseSetLeverage?.RetCode != 0 && responseSetLeverage?.RetCode != 110043) // 110043: leverage is the same
                {
                    Helpers.LogAndPrint(_logger, LogLevel.Error, "{0}: Error setting leverage for, {1}", Symbol, responseSetLeverage?.RetMsg);
                    return false;
                }
            }

            // Calculate order price based on bid or ask price offset by initial order tick size
            decimal orderPrice;
            (string? priceString, int priceModifier) = ActiveTradingPair.Configuration.Side switch
            {
                Side.Buy => (responseTickers.Result?.List?.FirstOrDefault()?.Bid1Price, -(ActiveTradingPair.Configuration.InitialPriceTickSizeOffset ?? 2)),
                Side.Sell => (responseTickers.Result?.List?.FirstOrDefault()?.Ask1Price, ActiveTradingPair.Configuration.InitialPriceTickSizeOffset ?? 2),
                _ => throw new InvalidOperationException($"Unsupported trading side: {ActiveTradingPair.Configuration.Side}")
            };

            if (!Helpers.TryParseDecimal(priceString, out decimal price) ||
                !Helpers.TryParseDecimal(_instrumentsInfo[Symbol].PriceFilter?.TickSize, out decimal tickSize))
            {
                Helpers.LogAndPrint(_logger, LogLevel.Error, "{0}: Error parsing price or tick size", Symbol);
                return false;
            }
            orderPrice = price + (priceModifier * tickSize);

            // Get open position again before placing order
            responsePositionInfo = await _apiService.GetPositionInfo(Category.Linear, Symbol);
            if (responsePositionInfo?.RetCode != 0)
            {
                Helpers.LogAndPrint(_logger, LogLevel.Error, "{0}: Error getting position info for, {1}", Symbol, responsePositionInfo?.RetMsg);
                return false;
            }
            position = responsePositionInfo?.Result?.List?.FirstOrDefault() ?? new();

            // Check if position size is more than 0 to prevent placing duplicate initial order
            if (!Helpers.TryParseDecimal(position.Size, out decimal latestSize) || latestSize > 0)
                return false;

            // Calculate quantity based on leverage, initial margin and qty step and check if it is more than min order qty and min notional value
            decimal adjustedQuantity = 0;
            if (Helpers.TryParseDecimal(_instrumentsInfo[Symbol].LotSizeFilter?.MinNotionalValue, out decimal minNotionalValue) &&
                Helpers.TryParseDecimal(_instrumentsInfo[Symbol].LotSizeFilter?.QtyStep, out decimal qtyStep) &&
                Helpers.TryParseDecimal(_instrumentsInfo[Symbol].LotSizeFilter?.MinOrderQty, out decimal minOrderQty) &&
                Helpers.TryParseDecimal(responseTickers.Result?.List?.FirstOrDefault()?.LastPrice, out decimal lastPrice))
            {
                decimal rawQuantity = (ActiveTradingPair.Configuration.InitialMargin.GetValueOrDefault() * ActiveTradingPair.Configuration.Leverage.GetValueOrDefault()) / lastPrice;
                adjustedQuantity = Math.Floor(rawQuantity / qtyStep) * qtyStep;

                if (adjustedQuantity < minOrderQty)
                {
                    Helpers.LogAndPrint(_logger, LogLevel.Error, "{0}: adjusted quantity {1} is below MinOrderQty {2}. Raw quantity: {3}", Symbol, adjustedQuantity, minOrderQty, rawQuantity);
                    return false;
                }

                decimal notionalValue = adjustedQuantity * lastPrice;

                if (notionalValue < minNotionalValue)
                {
                    Helpers.LogAndPrint(_logger, LogLevel.Error, "{0}: calculated quantity {1} * leverage {2} * latest price {3} results in notional value {4}, which is less than the min notional value {5}", Symbol, adjustedQuantity, ActiveTradingPair.Configuration.Leverage, lastPrice, notionalValue, minNotionalValue);
                    return false;
                }
            }

            ApiResponse<OrderResult, object>? responsePlaceOrder = await _apiService.PlaceOrder(
                Category.Linear,
                Symbol,
                ActiveTradingPair.Configuration.Side,
                OrderType.Limit,
                adjustedQuantity.ToString(),
                orderPrice.ToString(),
                TimeInForce.PostOnly);
            if (responsePlaceOrder?.RetCode != 0)
            {
                Helpers.LogAndPrint(_logger, LogLevel.Error, "{0}: Error placing initial order, {1}", Symbol, responsePlaceOrder?.RetMsg);
                return false;
            }

            Helpers.LogAndPrint(_logger, LogLevel.Information, "{0}: Initial order placed, price {1} quantity {2}", Symbol, orderPrice, adjustedQuantity);

            ActiveTradingPair.CalculatedInitialQuantity = adjustedQuantity;
            ActiveTradingPair.ScalingLevels = [];
            ActiveTradingPair.ScalingLevelsToBePlaced = [];

            return true;
        }

        /// <summary>
        /// Places or amends take profit orders for active trading pair
        /// </summary>
        internal async Task<bool> PlaceOrAmendTakeProfitOrder(string Symbol, ActiveTradingPair ActiveTradingPair)
        {
            ApiResponse<GetPositionInfoResult, object>? responsePositionInfo = await _apiService.GetPositionInfo(Category.Linear, Symbol);
            if (responsePositionInfo?.RetCode != 0)
            {
                Helpers.LogAndPrint(_logger, LogLevel.Error, "{0}: Error getting position info, {1}", Symbol, responsePositionInfo?.RetMsg);
                return false;
            }
            GetPositionInfoList position = responsePositionInfo?.Result?.List?.FirstOrDefault() ?? new();

            if (!Helpers.TryParseDecimal(position.Size, out decimal size) || size <= 0)
                return false;

            // Check if there is open reduce only order
            ApiResponse<GetOpenAndClosedOrdersResult, object>? responseOpenOrders = await _apiService.GetOpenAndClosedOrders(Category.Linear, Symbol, OpenOnly.True);
            if (responseOpenOrders?.RetCode != 0)
            {
                Helpers.LogAndPrint(_logger, LogLevel.Error, "{0}: Error getting open orders, {1}", Symbol, responseOpenOrders?.RetMsg);
                return false;
            }

            if (!Helpers.TryParseDecimal(_instrumentsInfo[Symbol].PriceFilter?.TickSize, out decimal priceTickSize) || priceTickSize <= 0)
            {
                Helpers.LogAndPrint(_logger, LogLevel.Error, "{0}: Invalid quantity step or price tick size", Symbol);
                return false;
            }

            if (!Helpers.TryParseDecimal(position.AvgPrice, out decimal averagePrice) || averagePrice <= 0)
            {
                Helpers.LogAndPrint(_logger, LogLevel.Error, "{0}: Error parsing average price", Symbol);
                return false;
            }

            string takeProfitSide = ActiveTradingPair.Configuration.Side == Side.Buy ? Side.Sell : Side.Buy;
            decimal takeProfitFactor = ActiveTradingPair.Configuration.TakeProfitPercentage!.Value / (100 * ActiveTradingPair.Configuration.Leverage!.Value);
            decimal takeProfitPrice;

            if (ActiveTradingPair.Configuration.Side == Side.Buy)
                takeProfitPrice = averagePrice * (1 + takeProfitFactor);
            else
                takeProfitPrice = averagePrice * (1 - takeProfitFactor);

            takeProfitPrice = (Math.Round(takeProfitPrice / priceTickSize) * priceTickSize).Normalize();
            GetOpenAndClosedOrdersDetails? takeProfitOrder = responseOpenOrders.Result?.List?.FirstOrDefault(order => order.ReduceOnly.GetValueOrDefault());

            // Check if take profit price or quantity is different from take profit order price or position quantity to amend order with new price and quantity
            if (takeProfitOrder is not null)
            {
                if (takeProfitOrder?.Price == takeProfitPrice.ToString() && takeProfitOrder.Quantity == position.Size)
                    return false;
                else
                {
                    ApiResponse<OrderResult, object>? responseAmendOrder = await _apiService.AmendOrder(Category.Linear, Symbol, takeProfitOrder?.OrderId ?? string.Empty, position.Size!, takeProfitPrice.ToString());
                    if (responseAmendOrder?.RetCode != 0)
                    {
                        Helpers.LogAndPrint(_logger, LogLevel.Error, "{0}: Error amending take profit order, {1}", Symbol, responseAmendOrder?.RetMsg);
                        return false;
                    }

                    Helpers.LogAndPrint(_logger, LogLevel.Information, "{0}: Take profit order amended, price {1} quantity {2}", Symbol, takeProfitPrice, position.Size);

                    return true;
                }
            }

            // Check position info again before placing take profit order
            responsePositionInfo = await _apiService.GetPositionInfo(Category.Linear, Symbol);
            if (responsePositionInfo?.RetCode != 0)
            {
                Helpers.LogAndPrint(_logger, LogLevel.Error, "{0}: Error getting position info, {1}", Symbol, responsePositionInfo?.RetMsg);
                return false;
            }
            position = responsePositionInfo?.Result?.List?.FirstOrDefault() ?? new();

            if (!Helpers.TryParseDecimal(position.Size, out decimal latestSize) || latestSize <= 0)
                return false;

            ApiResponse<OrderResult, object>? responsePlaceOrder = await _apiService.PlaceOrder(
                Category.Linear,
                Symbol,
                takeProfitSide,
                OrderType.Limit,
                position.Size!,
                takeProfitPrice.ToString(),
                TimeInForce.PostOnly,
                true);

            if (responsePlaceOrder?.RetCode != 0)
            {
                Helpers.LogAndPrint(_logger, LogLevel.Error, "{0}: Error placing take profit order, {1}", Symbol, responsePlaceOrder?.RetMsg);
                return false;
            }

            Helpers.LogAndPrint(_logger, LogLevel.Information, "{0}: Take profit order placed, price {1} quantity {2}", Symbol, takeProfitPrice, position.Size);

            return true;
        }

        /// <summary>
        /// Places scaling orders for active trading pair in batches of 10 max
        /// </summary>
        internal async Task<bool> PlaceScalingOrders(string Symbol, ActiveTradingPair ActiveTradingPair)
        {
            ApiResponse<GetPositionInfoResult, object>? responsePositionInfo = await _apiService.GetPositionInfo(Category.Linear, Symbol);
            if (responsePositionInfo?.RetCode != 0)
            {
                Helpers.LogAndPrint(_logger, LogLevel.Error, "{0}: Error getting position info, {1}", Symbol, responsePositionInfo?.RetMsg);
                return false;
            }
            GetPositionInfoList position = responsePositionInfo?.Result?.List?.FirstOrDefault() ?? new();

            if (!Helpers.TryParseDecimal(position.Size, out decimal size) || size <= 0)
                return false;

            if (ActiveTradingPair.ScalingLevels.Count > 0 && ActiveTradingPair.ScalingLevelsToBePlaced.Count <= 0)
                return false;

            if (ActiveTradingPair.ScalingLevelsToBePlaced.Count <= 0)
            {
                ApiResponse<GetOpenAndClosedOrdersResult, object>? responseOpenOrders = await _apiService.GetOpenAndClosedOrders(Category.Linear, Symbol, OpenOnly.True);
                if (responseOpenOrders?.RetCode != 0)
                {
                    Helpers.LogAndPrint(_logger, LogLevel.Error, "{0}: Error getting open orders, {1}", Symbol, responseOpenOrders?.RetMsg);
                    return false;
                }

                if (responseOpenOrders.Result?.List?.Any(order => order.Side == ActiveTradingPair.Configuration.Side) == true)
                    return false;

                responsePositionInfo = await _apiService.GetPositionInfo(Category.Linear, Symbol);
                if (responsePositionInfo?.RetCode != 0)
                {
                    Helpers.LogAndPrint(_logger, LogLevel.Error, "{0}: Error getting position info, {1}", Symbol, responsePositionInfo?.RetMsg);
                    return false;
                }

                if (!Helpers.TryParseDecimal(responsePositionInfo.Result?.List?.FirstOrDefault()?.Size, out decimal positionSize) || positionSize > ActiveTradingPair.CalculatedInitialQuantity)
                    return false;
            }

            if (ActiveTradingPair.ScalingLevels.Count <= 0)
            {
                if (!await CalculateScalingLevels(Symbol, ActiveTradingPair))
                {
                    Helpers.LogAndPrint(_logger, LogLevel.Error, "{0}: Error calculating scaling levels", Symbol);
                    return false;
                }
            }

            // Batch place orders in groups of 10 max
            while (ActiveTradingPair.ScalingLevelsToBePlaced.Count > 0)
            {
                List<ScalingLevel> scalingLevelsBatch = [.. ActiveTradingPair.ScalingLevelsToBePlaced.Take(10)];

                ApiResponse<BatchOrderResult, BatchOrderRetExtInfo>? responseBatchOrder = await _apiService.BatchPlaceOrder(
                    new()
                    {
                        Category = Category.Linear,
                        Request = [.. scalingLevelsBatch.Select(scalingLevel =>
                            new BatchOrderRequest()
                            {
                                Symbol = Symbol,
                                Side = ActiveTradingPair.Configuration.Side,
                                OrderType = OrderType.Limit,
                                Qty = scalingLevel.Quantity.ToString(),
                                Price = scalingLevel.Price.ToString(),
                                TimeInForce = TimeInForce.PostOnly
                            })]
                    });

                if (responseBatchOrder?.RetCode != 0)
                {
                    Helpers.LogAndPrint(_logger, LogLevel.Error, "{0}: Error placing scaling orders for, {1}", Symbol, responseBatchOrder?.RetMsg);
                    return false;
                }

                ApiResponse<GetOpenAndClosedOrdersResult, object>? responseOpenOrders = await _apiService.GetOpenAndClosedOrders(Category.Linear, Symbol, OpenOnly.True);
                if (responseOpenOrders?.RetCode != 0)
                {
                    Helpers.LogAndPrint(_logger, LogLevel.Error, "{0}: Error getting open orders, {1}", Symbol, responseOpenOrders?.RetMsg);
                    return false;
                }

                List<GetOpenAndClosedOrdersDetails> openOrders = responseOpenOrders.Result?.List ?? [];

                // Remove successfully placed orders from scaling levels to be placed
                ActiveTradingPair.ScalingLevelsToBePlaced.RemoveAll(scalingLevel =>
                    openOrders.Any(order =>
                        order.Side == ActiveTradingPair.Configuration.Side &&
                        Helpers.TryParseDecimal(order.Price, out decimal orderPrice) &&
                        Helpers.TryParseDecimal(order.Quantity, out decimal orderQuantity) &&
                        scalingLevel.Price == orderPrice &&
                        scalingLevel.Quantity == orderQuantity));

                // Check if any order placement failed
                for (int i = 0; i < responseBatchOrder.RetExtInfo?.List?.Count; i++)
                {
                    ScalingLevel scalingLevel = scalingLevelsBatch[i];
                    if (responseBatchOrder.RetExtInfo.List[i].Code != 0)
                    {
                        Helpers.LogAndPrint(_logger, LogLevel.Error, "{0}: Error placing scaling order at price {1} and quantity {2}, {3}", Symbol, scalingLevel.Price, scalingLevel.Quantity, responseBatchOrder.RetExtInfo.List[i].Msg);
                        return false;
                    }
                }
            }

            Helpers.LogAndPrint(_logger, LogLevel.Information, "{0}: Scaling orders placed", Symbol);

            return true;
        }

        /// <summary>
        /// Calculates scaling levels for active trading pair based on configuration
        /// </summary>
        private async Task<bool> CalculateScalingLevels(string symbol, ActiveTradingPair activeTradingPair)
        {
            List<ScalingLevel> scalingLevels = [];

            ApiResponse<GetPositionInfoResult, object>? responsePositionInfo = await _apiService.GetPositionInfo(Category.Linear, symbol);
            if (responsePositionInfo?.RetCode != 0)
            {
                Helpers.LogAndPrint(_logger, LogLevel.Error, "{0}: Error getting position info, {1}", symbol, responsePositionInfo?.RetMsg);
                return false;
            }
            GetPositionInfoList position = responsePositionInfo?.Result?.List?.FirstOrDefault() ?? new();

            if (!Helpers.TryParseDecimal(position.Size, out decimal currentQty) || currentQty <= 0 ||
                !Helpers.TryParseDecimal(position.AvgPrice, out decimal currentAvgPrice) || currentAvgPrice <= 0)
            {
                Helpers.LogAndPrint(_logger, LogLevel.Error, "{0}: Invalid current quantity or average price", symbol);
                return false;
            }

            if (!Helpers.TryParseDecimal(_instrumentsInfo[symbol].LotSizeFilter?.QtyStep, out decimal qtyStep) || qtyStep <= 0 ||
            !Helpers.TryParseDecimal(_instrumentsInfo[symbol].PriceFilter?.TickSize, out decimal priceTickSize) || priceTickSize <= 0)
            {
                Helpers.LogAndPrint(_logger, LogLevel.Error, "{0}: Invalid quantity step or price tick size", symbol);
                return false;
            }

            decimal pnlFactor = 1m;
            decimal levelQty = currentQty * activeTradingPair.Configuration.InitialScalingQuantityMultiplier ?? 1;

            for (int i = 0; i < activeTradingPair.Configuration.NumberOfScalingLevels; i++)
            {
                decimal pnlPercentage = Math.Min(
                    activeTradingPair.Configuration.InitialScalingUnrealizedPnL.GetValueOrDefault() * pnlFactor,
                    activeTradingPair.Configuration.MaxScalingUnrealizedPnL.GetValueOrDefault());

                decimal takeProfitFactor = pnlPercentage / (100m * activeTradingPair.Configuration.Leverage.GetValueOrDefault());

                decimal levelPrice = position.Side == Side.Buy
                    ? currentAvgPrice * (1m - takeProfitFactor)
                    : currentAvgPrice * (1m + takeProfitFactor);

                levelQty *= activeTradingPair.Configuration.ScalingQuantityMultiplier.GetValueOrDefault();
                decimal roundedPrice = Math.Round(levelPrice / priceTickSize) * priceTickSize;
                decimal roundedQty = Math.Round(levelQty / qtyStep) * qtyStep;

                scalingLevels.Add(new() { Price = roundedPrice, Quantity = roundedQty });

                decimal totalQty = currentQty + roundedQty;
                currentAvgPrice = (currentAvgPrice * currentQty + roundedPrice * roundedQty) / totalQty;
                currentQty = totalQty;

                pnlFactor *= activeTradingPair.Configuration.ScalingUnrealisedPnlMultiplier.GetValueOrDefault();
            }

            activeTradingPair.ScalingLevels = [.. scalingLevels];
            activeTradingPair.ScalingLevelsToBePlaced = [.. scalingLevels];
            return true;
        }
    }
}
