using BybitPerpetualsTradingBot.Models;
using BybitPerpetualsTradingBot.Models.API;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using static BybitPerpetualsTradingBot.Models.API.ApiParameters;
using static BybitPerpetualsTradingBot.Models.API.GetInstrumentsInfoResult;
using static BybitPerpetualsTradingBot.Models.API.GetOpenAndClosedOrdersResult;
using static BybitPerpetualsTradingBot.Models.PairsConfiguration;

namespace BybitPerpetualsTradingBot
{
    internal static partial class TradingBotHelper
    {
        internal readonly static string settingsFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "settings.json");
        internal readonly static string pairsFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "pairs.json");

        // Matches route parameters in curly braces
        [GeneratedRegex(@"\{[^{}]+\}", RegexOptions.Compiled)]
        private static partial Regex RouteParameterRegex();

        private static BaseHttpClient _baseHttpClient;
        private static ILogger<TradingBot> _logger;
        private static Settings _settings;
        private static PairsConfiguration _pairsConfiguration;
        private static Dictionary<string, InstrumentList> _instrumentsInfo = [];
        private static Dictionary<string, ActiveTradingPair> _activeTradingPairs = [];

        private readonly static JsonSerializerSettings _jsonSerializerSettings = new()
        {
            ContractResolver = new DefaultContractResolver
            {
                NamingStrategy = new CamelCaseNamingStrategy
                {
                    ProcessDictionaryKeys = true,
                    OverrideSpecifiedNames = false
                }
            },
            NullValueHandling = NullValueHandling.Ignore
        };

        /// <summary>
        /// Initializes TradingBotHelper with Dependencies and loads settings and pairs configurations
        /// </summary>
        internal static void InitializeDependencies(BaseHttpClient baseHttpClient, ILogger<TradingBot> logger)
        {
            _baseHttpClient = baseHttpClient;
            _logger = logger;

            _settings = LoadFileData<Settings>(settingsFilePath);
            _pairsConfiguration = LoadFileData<PairsConfiguration>(pairsFilePath);
        }

        /// <summary>
        /// Loads data from file or backups current file and creates a default file
        /// </summary>
        internal static T LoadFileData<T>(string filePath) where T : new()
        {
            T defaultModel = new();

            try
            {
                string jsonContent = File.ReadAllText(filePath);

                if (string.IsNullOrEmpty(jsonContent))
                {
                    jsonContent = JsonConvert.SerializeObject(defaultModel, Formatting.Indented);
                    File.WriteAllText(filePath, jsonContent);
                    return defaultModel;
                }

                return JsonConvert.DeserializeObject<T>(jsonContent) ?? defaultModel;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error reading file '{filePath}': {ex.Message}");

                if (File.Exists(filePath))
                {
                    try
                    {
                        string timestamp = DateTime.Now.ToString("MMddHHmmss");
                        string backupFilePath = Path.ChangeExtension(filePath, $".broken.{timestamp}.json");

                        File.Move(filePath, backupFilePath);
                        Console.WriteLine($"Existing file backed up as {backupFilePath}");
                    }
                    catch (Exception backupEx)
                    {
                        Console.WriteLine($"Failed to back up the existing file: {backupEx.Message}");
                    }
                }

                string defaultJson = JsonConvert.SerializeObject(defaultModel, Formatting.Indented);
                File.WriteAllText(filePath, defaultJson);
                return defaultModel;
            }
        }

        /// <summary>
        /// Executes trading tasks concurrently
        /// </summary>
        internal static async Task ExecuteTasksConcurrently(List<Func<string, ActiveTradingPair, Task<bool>>> taskFuncs)
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
                        Stopwatch pairStopwatch = Stopwatch.StartNew();
                        bool result = await taskFunc(pair.Key, pair.Value);
                        pairStopwatch.Stop();

                        if (result)
                            LogAndPrint(LogLevel.Information, "[{TaskName}] {TradingPair} {ElapsedSeconds:F1} sec", taskFunc.Method.Name, pair.Key, pairStopwatch.Elapsed.TotalSeconds);

                        return result;
                    }
                    catch (Exception ex)
                    {
                        LogAndPrint(LogLevel.Error, "[{TaskName}] Exception in task execution for {TradingPair}: {ExceptionMessage}", taskFunc.Method.Name, pair.Key, ex.Message);
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
            double totalElapsedSeconds = stopwatch.Elapsed.TotalSeconds;

            Console.ForegroundColor = ConsoleColor.DarkGreen;
            Console.WriteLine($"Loop done {totalElapsedSeconds:F1} sec");
        }

        /// <summary>
        /// Initializes active trading pairs and validates pairs configuration data with instruments info and adds position info, open orders
        /// </summary>
        internal static async Task<bool> InitializeActiveTradingPairs()
        {
            ApiResponse<GetInstrumentsInfoResult, object>? responseInstrumentsInfo = await GetInstrumentsInfo(Category.Linear);
            if (responseInstrumentsInfo?.RetCode != 0)
            {
                LogAndPrint(LogLevel.Error, "Error getting instruments info: {0}", responseInstrumentsInfo?.RetMsg);
                return false;
            }

            _instrumentsInfo = responseInstrumentsInfo?.Result?.List?.ToDictionary(List => List.Symbol ?? string.Empty, List => List) ?? [];

            if (_instrumentsInfo.Count == 0)
            {
                LogAndPrint(LogLevel.Error, "No instruments info found");
                return false;
            }

            foreach (string pair in _pairsConfiguration.ActiveTradingPairs)
            {
                if (!_instrumentsInfo.TryGetValue(pair, out InstrumentList? instrumentList))
                {
                    LogAndPrint(LogLevel.Error, "Active trading pair {0} not found in instruments info", pair);
                    return false;
                }

                if (instrumentList is null)
                {
                    LogAndPrint(LogLevel.Error, "Active trading pair {0} instrument list is null", pair);
                    return false;
                }

                if (!_pairsConfiguration.PairsConfigurations.TryGetValue(pair, out PairConfiguration? pairConfiguration))
                {
                    LogAndPrint(LogLevel.Error, "Active trading pair {0} not found in pairs configuration", pair);
                    return false;
                }

                if (pairConfiguration is null)
                {
                    LogAndPrint(LogLevel.Error, "Active trading pair {0} configuration is null", pair);
                    return false;
                }

                // Check if side is Buy or Sell
                if (pairConfiguration.Side is not null && (pairConfiguration.Side != Side.Buy && pairConfiguration.Side != Side.Sell))
                {
                    LogAndPrint(LogLevel.Error, "Active trading pair {0} side {1} is invalid", pair, pairConfiguration.Side);
                    return false;
                }

                // Check if leverage is within expected range
                if (pairConfiguration.Leverage is not null &&
                    TryParseDecimal(instrumentList.LeverageFilter?.MinLeverage, out decimal minLeverage) &&
                    TryParseDecimal(instrumentList.LeverageFilter?.MaxLeverage, out decimal maxLeverage))
                {
                    if (pairConfiguration.Leverage < minLeverage || pairConfiguration.Leverage > maxLeverage)
                    {
                        LogAndPrint(LogLevel.Error, "Active trading pair {0} leverage {1} is not within the expected range of {2} and {3}", pair, pairConfiguration.Leverage, minLeverage, maxLeverage);
                        return false;
                    }
                }
                else
                {
                    LogAndPrint(LogLevel.Error, "Invalid leverage value for active trading pair {0}", pair);
                    return false;
                }

                // Check if leverage is correct for leverage step size
                if (pairConfiguration.Leverage is not null &&
                    TryParseDecimal(instrumentList.LeverageFilter?.LeverageStep, out decimal leverageStep))
                {
                    if ((pairConfiguration.Leverage - minLeverage) % leverageStep != 0)
                    {
                        LogAndPrint(LogLevel.Error, "Active trading pair {0} leverage {1} is not correct for leverage step size {2}", pair, pairConfiguration.Leverage, leverageStep);
                        return false;
                    }
                }

                //Check if number of steps is more than 0
                if (pairConfiguration.NumberOfScalingLevels is not null && pairConfiguration.NumberOfScalingLevels <= 0)
                {
                    LogAndPrint(LogLevel.Error, "Active trading pair {0} number of steps {1} is invalid", pair, pairConfiguration.NumberOfScalingLevels);
                    return false;
                }

                // Check if take profit percentage is more than 0
                if (pairConfiguration.TakeProfitPercentage is not null && pairConfiguration.TakeProfitPercentage <= 0)
                {
                    LogAndPrint(LogLevel.Error, "Active trading pair {0} take profit percentage {1} is invalid", pair, pairConfiguration.TakeProfitPercentage);
                    return false;
                }

                // Check if initial step unrealised PnL percentage is more than 0
                if (pairConfiguration.InitialScalingUnrealizedPnL is not null && pairConfiguration.InitialScalingUnrealizedPnL <= 0)
                {
                    LogAndPrint(LogLevel.Error, "Active trading pair {0} initial step unrealised PnL percentage {1} is invalid", pair, pairConfiguration.InitialScalingUnrealizedPnL);
                    return false;
                }

                // Check if max step unrealised PnL percentage is more than initial step unrealised PnL percentage
                if (pairConfiguration.MaxScalingUnrealizedPnL is not null && pairConfiguration.MaxScalingUnrealizedPnL < pairConfiguration.InitialScalingUnrealizedPnL)
                {
                    LogAndPrint(LogLevel.Error, "Active trading pair {0} max step unrealised PnL percentage {1} is invalid", pair, pairConfiguration.MaxScalingUnrealizedPnL);
                    return false;
                }

                // Check if step unrealised PnL multiplier is more than 0
                if (pairConfiguration.ScalingUnrealisedPnlMultiplier is not null && pairConfiguration.ScalingUnrealisedPnlMultiplier <= 0)
                {
                    LogAndPrint(LogLevel.Error, "Active trading pair {0} step unrealised PnL multiplier {1} is invalid", pair, pairConfiguration.ScalingUnrealisedPnlMultiplier);
                    return false;
                }

                // Check if step quantity multiplier is more than 0
                if (pairConfiguration.ScalingQuantityMultiplier is not null && pairConfiguration.ScalingQuantityMultiplier <= 0)
                {
                    LogAndPrint(LogLevel.Error, "Active trading pair {0} step quantity multiplier {1} is invalid", pair, pairConfiguration.ScalingQuantityMultiplier);
                    return false;
                }

                _activeTradingPairs.TryAdd(pair, new()
                {
                    Configuration = pairConfiguration
                });

                // Get position info
                ApiResponse<GetPositionInfoResult, object>? responsePositionInfo = await GetPositionInfo(Category.Linear, pair);
                if (responsePositionInfo?.RetCode != 0)
                {
                    LogAndPrint(LogLevel.Error, "Error getting position info for active trading pair {0}: {1}", pair, responsePositionInfo?.RetMsg);
                    return false;
                }
                _activeTradingPairs[pair].Position = responsePositionInfo?.Result?.List?.FirstOrDefault() ?? new();

                // Get tickers
                ApiResponse<GetTickersResult, object>? responseTickers = await GetTickers(Category.Linear, pair);
                if (responseTickers?.RetCode != 0)
                {
                    LogAndPrint(LogLevel.Error, "Error getting tickers for active trading pair {0}: {1}", pair, responseTickers?.RetMsg);
                    return false;
                }

                // Check if initial margin is set and greater than 0
                if (pairConfiguration.InitialMargin is not decimal initialMargin || initialMargin <= 0)
                {
                    LogAndPrint(LogLevel.Error, "Initial margin is not set or is zero/negative for active trading pair {0}", pair);
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Places initial positions for active trading pairs if position size is 0
        /// </summary>
        internal static async Task<bool> PlaceInitialPositions(string Symbol, ActiveTradingPair ActiveTradingPair)
        {
            // Get position info
            ApiResponse<GetPositionInfoResult, object>? responsePositionInfo = await GetPositionInfo(Category.Linear, Symbol);
            if (responsePositionInfo?.RetCode != 0)
            {
                LogAndPrint(LogLevel.Error, "Error getting position info for active trading pair {0}: {1}", Symbol, responsePositionInfo?.RetMsg);
                return false;
            }
            ActiveTradingPair.Position = responsePositionInfo?.Result?.List?.FirstOrDefault() ?? new();

            // Check if position size is 0 to place initial order
            if (!TryParseDecimal(ActiveTradingPair.Position.Size, out decimal size) || size != 0)
                return false;

            // Get tickers
            ApiResponse<GetTickersResult, object>? responseTickers = await GetTickers(Category.Linear, Symbol);
            if (responseTickers?.RetCode != 0)
            {
                LogAndPrint(LogLevel.Error, "Error getting tickers for active trading pair {0}: {1}", Symbol, responseTickers?.RetMsg);
                return false;
            }

            // Get open orders
            ApiResponse<GetOpenAndClosedOrdersResult, object>? responseOpenOrders = await GetOpenAndClosedOrders(Category.Linear, Symbol, OpenOnly.True);
            if (responseOpenOrders?.RetCode != 0)
            {
                LogAndPrint(LogLevel.Error, "Error getting open orders for active trading pair {0}: {1}", Symbol, responseOpenOrders?.RetMsg);
                return false;
            }

            // Check if there is open order with status PartiallyFilled to wait for it to be filled
            if (responseOpenOrders.Result?.List?.Any(order => order.OrderStatus == OrderStatus.PartiallyFilled) == true)
                return false;

            // Check if there is open order with price close to last price within 6 tick size to wait for it to be filled
            if (responseOpenOrders.Result?.List?.Any(order =>
            {
                if (TryParseDecimal(order.Price, out decimal orderPrice) &&
                    TryParseDecimal(responseTickers.Result?.List?.FirstOrDefault()?.LastPrice, out decimal lastPrice) &&
                    TryParseDecimal(_instrumentsInfo[Symbol].PriceFilter?.TickSize, out decimal tickSize))
                {
                    return Math.Abs(orderPrice - lastPrice) < 6 * tickSize;
                }

                return false;
            }) == true)
                return false;

            // Cancel all orders
            ApiResponse<CancelAllOrdersResult, object>? responseCancelAll = await CancelAllOrders(Category.Linear, Symbol);
            if (responseCancelAll?.RetCode != 0)
            {
                LogAndPrint(LogLevel.Error, "Error cancelling all orders for active trading pair {0}: {1}", Symbol, responseCancelAll?.RetMsg);
                return false;
            }

            if (!TryParseDecimal(ActiveTradingPair.Position.Leverage, out decimal leverage))
            {
                LogAndPrint(LogLevel.Error, "Error parsing leverage for active trading pair {0}", Symbol);
                return false;
            }

            // Set leverage
            if (leverage != ActiveTradingPair.Configuration.Leverage)
            {
                ApiResponse<object, object>? responseSetLeverage = await SetLeverage(Category.Linear, Symbol, ActiveTradingPair.Configuration.Leverage.ToString()!, ActiveTradingPair.Configuration.Leverage.ToString()!);
                if (responseSetLeverage?.RetCode != 0 && responseSetLeverage?.RetCode != 110043)
                {
                    LogAndPrint(LogLevel.Error, "Error setting leverage for active trading pair {0}: {1}", Symbol, responseSetLeverage?.RetMsg);
                    return false;
                }
            }

            // Calculate order price based on bid or ask price offset by 2 tick size
            decimal orderPrice;
            (string? priceString, int priceModifier) = ActiveTradingPair.Configuration.Side switch
            {
                Side.Buy => (responseTickers.Result?.List?.FirstOrDefault()?.Bid1Price, -2),
                Side.Sell => (responseTickers.Result?.List?.FirstOrDefault()?.Ask1Price, 2),
                _ => throw new InvalidOperationException($"Unsupported trading side: {ActiveTradingPair.Configuration.Side}")
            };

            if (!TryParseDecimal(priceString, out decimal price) ||
                !TryParseDecimal(_instrumentsInfo[Symbol].PriceFilter?.TickSize, out decimal tickSize))
            {
                LogAndPrint(LogLevel.Error, "Error parsing price or tick size for active trading pair {0}", Symbol);
                return false;
            }
            orderPrice = price + (priceModifier * tickSize);

            // Get open position again before placing order
            responsePositionInfo = await GetPositionInfo(Category.Linear, Symbol);
            if (responsePositionInfo?.RetCode != 0)
            {
                LogAndPrint(LogLevel.Error, "Error getting position info for active trading pair {0}: {1}", Symbol, responsePositionInfo?.RetMsg);
                return false;
            }
            ActiveTradingPair.Position = responsePositionInfo?.Result?.List?.FirstOrDefault() ?? new();

            // Check if position size is more than 0 to prevent placing duplicate initial order
            if (!TryParseDecimal(ActiveTradingPair.Position.Size, out decimal updatedSize) || updatedSize > 0)
                return false;

            decimal adjustedQuantity = 0;
            // Calculate and check if price and quantity is more than min notional value
            if (TryParseDecimal(_instrumentsInfo[Symbol].LotSizeFilter?.MinNotionalValue, out decimal minNotionalValue) &&
                TryParseDecimal(_instrumentsInfo[Symbol].LotSizeFilter?.QtyStep, out decimal qtyStep) &&
                TryParseDecimal(_instrumentsInfo[Symbol].LotSizeFilter?.MinOrderQty, out decimal minOrderQty) &&
                TryParseDecimal(responseTickers.Result?.List?.FirstOrDefault()?.LastPrice, out decimal lastPrice))
            {
                decimal rawQuantity = (ActiveTradingPair.Configuration.InitialMargin.GetValueOrDefault() * ActiveTradingPair.Configuration.Leverage.GetValueOrDefault()) / lastPrice;
                adjustedQuantity = Math.Floor(rawQuantity / qtyStep) * qtyStep;

                if (adjustedQuantity < minOrderQty)
                {
                    LogAndPrint(LogLevel.Error, "Active trading pair {0}: adjusted quantity {1} is below MinOrderQty {2}. Raw quantity: {3}", Symbol, adjustedQuantity, minOrderQty, rawQuantity);
                    return false;
                }

                decimal notionalValue = adjustedQuantity * lastPrice;

                if (notionalValue < minNotionalValue)
                {
                    LogAndPrint(LogLevel.Error, "Active trading pair {0}: calculated quantity {1} * leverage {2} * latest price {3} results in notional value {4}, which is less than the min notional value {5}", Symbol, adjustedQuantity, ActiveTradingPair.Configuration.Leverage, lastPrice, notionalValue, minNotionalValue);
                    return false;
                }
            }

            // Place initial order
            ApiResponse<OrderResult, object>? responsePlaceOrder = await PlaceOrder(
                Category.Linear,
                Symbol,
                ActiveTradingPair.Configuration.Side,
                OrderType.Limit,
                adjustedQuantity.ToString(),
                orderPrice.ToString(),
                TimeInForce.PostOnly);
            if (responsePlaceOrder?.RetCode != 0)
            {
                LogAndPrint(LogLevel.Error, "Error placing initial order for active trading pair {0}: {1}", Symbol, responsePlaceOrder?.RetMsg);
                return false;
            }

            ActiveTradingPair.CalculatedInitialQuantity = adjustedQuantity;
            ActiveTradingPair.ScalingLevels = [];
            ActiveTradingPair.ScalingLevelsToBePlaced = [];

            return true;
        }

        /// <summary>
        /// Places take profit orders for active trading pairs if position size is more than 0 and no open orders or amend orders with new price and quantity
        /// </summary>
        internal static async Task<bool> PlaceTakeProfitOrders(string Symbol, ActiveTradingPair ActiveTradingPair)
        {
            // Get and update position info
            ApiResponse<GetPositionInfoResult, object>? responsePositionInfo = await GetPositionInfo(Category.Linear, Symbol);
            if (responsePositionInfo?.RetCode != 0)
            {
                LogAndPrint(LogLevel.Error, "Error getting position info for active trading pair {0}: {1}", Symbol, responsePositionInfo?.RetMsg);
                return false;
            }
            ActiveTradingPair.Position = responsePositionInfo?.Result?.List?.FirstOrDefault() ?? new();

            // Check if position size is more than 0 to place reduce only order
            if (!TryParseDecimal(ActiveTradingPair.Position.Size, out decimal size) || size <= 0)
                return false;

            // Check if there is open reduce only order
            ApiResponse<GetOpenAndClosedOrdersResult, object>? responseOpenOrders = await GetOpenAndClosedOrders(Category.Linear, Symbol, OpenOnly.True);
            if (responseOpenOrders?.RetCode != 0)
            {
                LogAndPrint(LogLevel.Error, "Error getting open orders for active trading pair {0}: {1}", Symbol, responseOpenOrders?.RetMsg);
                return false;
            }

            if (!TryParseDecimal(_instrumentsInfo[Symbol].PriceFilter?.TickSize, out decimal priceTickSize) || priceTickSize <= 0)
            {
                LogAndPrint(LogLevel.Error, "Invalid quantity step or price tick size for pair {0}", Symbol);
                return false;
            }

            if (!TryParseDecimal(ActiveTradingPair.Position.AvgPrice, out decimal averagePrice) || averagePrice <= 0)
            {
                LogAndPrint(LogLevel.Error, "Error parsing average price for active trading pair {0}", Symbol);
                return false;
            }

            string takeProfitSide = ActiveTradingPair.Configuration.Side == Side.Buy ? Side.Sell : Side.Buy;
            decimal takeProfitFactor = ActiveTradingPair.Configuration.TakeProfitPercentage!.Value / (100 * ActiveTradingPair.Configuration.Leverage!.Value);
            decimal takeProfitPrice;
            if (ActiveTradingPair.Configuration.Side == Side.Buy)
                takeProfitPrice = averagePrice * (1 + takeProfitFactor);
            else
                takeProfitPrice = averagePrice * (1 - takeProfitFactor);

            takeProfitPrice = Math.Round(takeProfitPrice / priceTickSize) * priceTickSize;
            takeProfitPrice = takeProfitPrice.Normalize();

            GetOpenAndClosedOrdersDetails? takeProfitOrder = responseOpenOrders.Result?.List?.FirstOrDefault(order => order.ReduceOnly.GetValueOrDefault());

            // Check if take profit price is different from take profit order price to amend order with new price and quantity
            if (takeProfitOrder is not null)
            {
                if (takeProfitOrder?.Price == takeProfitPrice.ToString())
                    return false;
                else
                {
                    ApiResponse<OrderResult, object>? responseAmendOrder = await AmendOrder(Category.Linear, Symbol, takeProfitOrder?.OrderId ?? string.Empty, ActiveTradingPair.Position.Size!, takeProfitPrice.ToString());
                    if (responseAmendOrder?.RetCode != 0)
                    {
                        LogAndPrint(LogLevel.Error, "Error amending take profit order for active trading pair {0}: {1}", Symbol, responseAmendOrder?.RetMsg);
                        return false;
                    }

                    return true;
                }
            }

            // Get tickers
            ApiResponse<GetTickersResult, object>? responseTickers = await GetTickers(Category.Linear, Symbol);
            if (responseTickers?.RetCode != 0)
            {
                LogAndPrint(LogLevel.Error, "Error getting tickers for active trading pair {0}: {1}", Symbol, responseTickers?.RetMsg);
                return false;
            }

            if (!TryParseDecimal(responseTickers.Result?.List?.FirstOrDefault()?.LastPrice, out decimal lastPrice) || averagePrice <= 0)
            {
                LogAndPrint(LogLevel.Error, "Error parsing last price for active trading pair {0}", Symbol);
                return false;
            }

            // Check if last price with 0.5% offset is more than take profit price to place reduce only market order (fallback)
            bool lastPriceMoreThanTakeProfitPrice =
                ActiveTradingPair.Configuration.Side == Side.Buy
                    ? (lastPrice * 1.005m) > takeProfitPrice
                    : (lastPrice * 0.995m) < takeProfitPrice;

            if (lastPriceMoreThanTakeProfitPrice)
            {
                ApiResponse<OrderResult, object>? responsePlaceOrderMarket = await PlaceOrder(
                    Category.Linear,
                    Symbol,
                    takeProfitSide,
                    OrderType.Market,
                    ActiveTradingPair.Position.Size!,
                    timeInForce: TimeInForce.ImmediateOrCancel,
                    reduceOnly: true);

                if (responsePlaceOrderMarket?.RetCode != 0)
                {
                    LogAndPrint(LogLevel.Error, "Error placing reduce only market order for active trading pair {0}: {1}", Symbol, responsePlaceOrderMarket?.RetMsg);
                    return false;
                }

                return true;
            }

            // Place take profit order
            ApiResponse<OrderResult, object>? responsePlaceOrder = await PlaceOrder(
                Category.Linear,
                Symbol,
                takeProfitSide,
                OrderType.Limit,
                ActiveTradingPair.Position.Size!,
                takeProfitPrice.ToString(),
                TimeInForce.PostOnly,
                true);

            if (responsePlaceOrder?.RetCode != 0)
            {
                LogAndPrint(LogLevel.Error, "Error placing take profit order for active trading pair {0}: {1}", Symbol, responsePlaceOrder?.RetMsg);
                return false;
            }

            return true;
        }

        /// <summary>
        /// Places scaling orders for active trading pairs in batches of 10 maximum
        /// </summary>
        internal static async Task<bool> PlaceScalingOrders(string Symbol, ActiveTradingPair ActiveTradingPair)
        {
            if (!TryParseDecimal(ActiveTradingPair.Position.Size, out decimal size) || size <= 0)
                return false;

            if (ActiveTradingPair.ScalingLevels.Count > 0 && ActiveTradingPair.ScalingLevelsToBePlaced.Count <= 0)
                return false;

            if (ActiveTradingPair.ScalingLevelsToBePlaced.Count <= 0)
            {
                // Get open orders
                ApiResponse<GetOpenAndClosedOrdersResult, object>? responseOpenOrders = await GetOpenAndClosedOrders(Category.Linear, Symbol, OpenOnly.True);
                if (responseOpenOrders?.RetCode != 0)
                {
                    LogAndPrint(LogLevel.Error, "Error getting open orders for active trading pair {0}: {1}", Symbol, responseOpenOrders?.RetMsg);
                    return false;
                }

                // Check if there is any open order with same side
                if (responseOpenOrders.Result?.List?.Any(order =>
                    order.Side == ActiveTradingPair.Configuration.Side) == true)
                    return false;

                // Get positon info
                ApiResponse<GetPositionInfoResult, object>? responsePositionInfo = await GetPositionInfo(Category.Linear, Symbol);
                if (responsePositionInfo?.RetCode != 0)
                {
                    LogAndPrint(LogLevel.Error, "Error getting position info for active trading pair {0}: {1}", Symbol, responsePositionInfo?.RetMsg);
                    return false;
                }

                // Check if position size is more than calculated initial quantity
                if (!TryParseDecimal(responsePositionInfo.Result?.List?.FirstOrDefault()?.Size, out decimal positionSize) || positionSize > ActiveTradingPair.CalculatedInitialQuantity)
                    return false;
            }

            if (ActiveTradingPair.ScalingLevels.Count <= 0)
            {
                if (!await CalculateScalingLevels(Symbol, ActiveTradingPair))
                {
                    LogAndPrint(LogLevel.Error, "Error calculating scaling levels for active trading pair {0}", Symbol);
                    return false;
                }
            }

            // Batch place orders in groups of 10
            while (ActiveTradingPair.ScalingLevelsToBePlaced.Count > 0)
            {
                List<ScalingLevel> scalingLevelsBatch = ActiveTradingPair.ScalingLevelsToBePlaced.Take(10).ToList();

                // Place batch order
                ApiResponse<BatchOrderResult, BatchOrderRetExtInfo>? responseBatchOrder = await BatchPlaceOrder(
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
                    LogAndPrint(LogLevel.Error, "Error placing scaling orders for active trading pair {0}: {1}", Symbol, responseBatchOrder?.RetMsg);
                    return false;
                }

                // Get open orders
                ApiResponse<GetOpenAndClosedOrdersResult, object>? responseOpenOrders = await GetOpenAndClosedOrders(Category.Linear, Symbol, OpenOnly.True);
                if (responseOpenOrders?.RetCode != 0)
                {
                    LogAndPrint(LogLevel.Error, "Error getting open orders for active trading pair {0}: {1}", Symbol, responseOpenOrders?.RetMsg);
                    return false;
                }

                // Remove successfully placed orders from scaling levels to be placed
                ActiveTradingPair.ScalingLevelsToBePlaced.Where(scalingLevel => responseOpenOrders.Result?.List?.Any(order =>
                    order.Side == ActiveTradingPair.Configuration.Side &&
                    TryParseDecimal(order.Price, out decimal orderPrice) &&
                    TryParseDecimal(order.Quantity, out decimal orderQuantity) &&
                    scalingLevel.Price == orderPrice &&
                    scalingLevel.Quantity == orderQuantity) == true).ToList().ForEach(scalingLevel => ActiveTradingPair.ScalingLevelsToBePlaced.Remove(scalingLevel));

                // Check if any order placement failed
                for (int i = 0; i < responseBatchOrder.RetExtInfo?.List?.Count; i++)
                {
                    ScalingLevel scalingLevel = scalingLevelsBatch[i];
                    if (responseBatchOrder.RetExtInfo.List[i].Code != 0)
                    {
                        LogAndPrint(LogLevel.Error, "Error placing scaling order for active trading pair {0} at price {1} and quantity {2}: {3}", Symbol, scalingLevel.Price, scalingLevel.Quantity, responseBatchOrder.RetExtInfo.List[i].Msg);
                        return false;
                    }
                }
            }

            return true;
        }

        /// <summary>
        /// Calculates scaling levels based on configuration
        /// </summary>
        private static async Task<bool> CalculateScalingLevels(string symbol, ActiveTradingPair activeTradingPair)
        {
            List<ScalingLevel> scalingLevels = [];

            // Get position info
            ApiResponse<GetPositionInfoResult, object>? responsePositionInfo = await GetPositionInfo(Category.Linear, symbol);
            if (responsePositionInfo?.RetCode != 0)
            {
                LogAndPrint(LogLevel.Error, "Error getting position info for active trading pair {0}: {1}", symbol, responsePositionInfo?.RetMsg);
                return false;
            }
            activeTradingPair.Position = responsePositionInfo?.Result?.List?.FirstOrDefault() ?? new();

            if (!TryParseDecimal(activeTradingPair.Position.Size, out decimal currentQty) || currentQty <= 0 ||
                !TryParseDecimal(activeTradingPair.Position.AvgPrice, out decimal currentAvgPrice) || currentAvgPrice <= 0)
            {
                LogAndPrint(LogLevel.Error, "Invalid current quantity or average price for pair {0}", symbol);
                return false;
            }

            if (!TryParseDecimal(_instrumentsInfo[symbol].LotSizeFilter?.QtyStep, out decimal qtyStep) || qtyStep <= 0 ||
            !TryParseDecimal(_instrumentsInfo[symbol].PriceFilter?.TickSize, out decimal priceTickSize) || priceTickSize <= 0)
            {
                LogAndPrint(LogLevel.Error, "Invalid quantity step or price tick size for pair {0}", symbol);
                return false;
            }

            decimal pnlFactor = 1m;
            decimal levelQty = currentQty / 2;

            for (int i = 0; i < activeTradingPair.Configuration.NumberOfScalingLevels; i++)
            {
                decimal pnlPercentage = Math.Min(
                    activeTradingPair.Configuration.InitialScalingUnrealizedPnL.GetValueOrDefault() * pnlFactor,
                    activeTradingPair.Configuration.MaxScalingUnrealizedPnL.GetValueOrDefault());

                decimal takeProfitFactor = pnlPercentage / (100m * activeTradingPair.Configuration.Leverage.GetValueOrDefault());

                decimal levelPrice = activeTradingPair.Position.Side == Side.Buy
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

            activeTradingPair.ScalingLevels = scalingLevels.ToList();
            activeTradingPair.ScalingLevelsToBePlaced = scalingLevels;
            return true;
        }

        /// <summary>
        /// Gets instruments info for category and optional symbol and limit
        /// </summary>
        private static async Task<ApiResponse<GetInstrumentsInfoResult, object>?> GetInstrumentsInfo(string category, string symbol = "", int limit = 1000)
        {
            Dictionary<string, string> queryParams = new()
            {
                {nameof(category), category},
                {nameof(limit), limit.ToString()}
            };

            if (!string.IsNullOrEmpty(symbol))
                queryParams[nameof(symbol)] = symbol.ToUpper();

            string query = string.Join("&", queryParams.Select(kvp => $"{kvp.Key}={kvp.Value}"));
            string uri = BuildUri(_settings.Endpoint, EndpointProduct.Market, string.Concat(EndpointModule.InstrumentsInfo, '?', query));

            string timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString();
            string signature = GenerateSignature(_settings, timestamp, query);

            return await _baseHttpClient.GetAsync<ApiResponse<GetInstrumentsInfoResult, object>?>(uri, _settings.APIKey, timestamp, signature, _settings.RecvWindow);
        }

        /// <summary>
        /// Gets tickers for category and symbol
        /// </summary>
        private static async Task<ApiResponse<GetTickersResult, object>?> GetTickers(string category, string symbol)
        {
            Dictionary<string, string> queryParams = new()
            {
                {nameof(category), category},
                {nameof(symbol), symbol.ToUpper()}
            };

            string query = string.Join("&", queryParams.Select(kvp => $"{kvp.Key}={kvp.Value}"));
            string uri = BuildUri(_settings.Endpoint, EndpointProduct.Market, string.Concat(EndpointModule.Tickers, '?', query));

            string timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString();
            string signature = GenerateSignature(_settings, timestamp, query);

            return await _baseHttpClient.GetAsync<ApiResponse<GetTickersResult, object>?>(uri, _settings.APIKey, timestamp, signature, _settings.RecvWindow);
        }

        /// <summary>
        /// Gets position info for category and symbol
        /// </summary>
        private static async Task<ApiResponse<GetPositionInfoResult, object>?> GetPositionInfo(string category, string symbol)
        {
            string query = $"{nameof(category)}={category}&{nameof(symbol)}={symbol.ToUpper()}";
            string uri = BuildUri(_settings.Endpoint, EndpointProduct.Position, string.Concat(EndpointModule.List, '?', query));

            string timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString();
            string signature = GenerateSignature(_settings, timestamp, query);

            return await _baseHttpClient.GetAsync<ApiResponse<GetPositionInfoResult, object>?>(uri, _settings.APIKey, timestamp, signature, _settings.RecvWindow);
        }

        /// <summary>
        /// Gets open and closed orders for category, symbol and openOnly with optional limit
        /// </summary>
        private static async Task<ApiResponse<GetOpenAndClosedOrdersResult, object>?> GetOpenAndClosedOrders(string category, string symbol, int openOnly, int limit = 50)
        {
            string query = $"{nameof(category)}={category}&{nameof(symbol)}={symbol.ToUpper()}&{nameof(openOnly)}={openOnly}&{nameof(limit)}={limit}";
            string uri = BuildUri(_settings.Endpoint, EndpointProduct.Order, string.Concat(EndpointModule.RealTime, '?', query));

            string timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString();
            string signature = GenerateSignature(_settings, timestamp, query);

            return await _baseHttpClient.GetAsync<ApiResponse<GetOpenAndClosedOrdersResult, object>?>(uri, _settings.APIKey, timestamp, signature, _settings.RecvWindow);
        }

        /// <summary>
        /// Sets leverage for category and symbol
        /// </summary>
        private static async Task<ApiResponse<object, object>?> SetLeverage(string category, string symbol, string buyLeverage, string sellLeverage)
        {
            string uri = BuildUri(_settings.Endpoint, EndpointProduct.Position, EndpointModule.SetLeverage);

            Dictionary<string, object> parameters = new()
            {
                {nameof(category), category},
                {nameof(symbol), symbol.ToUpper()},
                {nameof(buyLeverage), buyLeverage},
                {nameof(sellLeverage), sellLeverage}
            };

            string timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString();
            string jsonPayload = JsonConvert.SerializeObject(parameters, _jsonSerializerSettings);
            string signature = GenerateSignature(_settings, timestamp, jsonPayload);

            return await _baseHttpClient.PostAsync<ApiResponse<object, object>?>(uri, jsonPayload, _settings.APIKey, timestamp, signature, _settings.RecvWindow);
        }

        /// <summary>
        /// Places order for category, symbol, side, order type and quantity with optional price, timeInforce and reduceOnly
        /// </summary>
        private static async Task<ApiResponse<OrderResult, object>?> PlaceOrder(string category, string symbol, string side, string orderType, string qty, string price = "0", string timeInForce = "PostOnly", bool reduceOnly = false)
        {
            string uri = BuildUri(_settings.Endpoint, EndpointProduct.Order, EndpointModule.Create);

            Dictionary<string, object> parameters = new()
                {
                    {nameof(category), category},
                    {nameof(symbol), symbol.ToUpper()},
                    {nameof(side), side},
                    {nameof(orderType), orderType},
                    {nameof(qty), qty},
                    {nameof(price), price},
                    {nameof(timeInForce), timeInForce},
                    {nameof(reduceOnly), reduceOnly}
                };

            string timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString();
            string jsonPayload = JsonConvert.SerializeObject(parameters, _jsonSerializerSettings);
            string signature = GenerateSignature(_settings, timestamp, jsonPayload);

            return await _baseHttpClient.PostAsync<ApiResponse<OrderResult, object>?>(uri, jsonPayload, _settings.APIKey, timestamp, signature, _settings.RecvWindow);
        }

        /// <summary>
        /// Batch places orders
        /// </summary>
        private static async Task<ApiResponse<BatchOrderResult, BatchOrderRetExtInfo>?> BatchPlaceOrder(ApiRequest<BatchOrderRequest> request)
        {
            string uri = BuildUri(_settings.Endpoint, EndpointProduct.Order, EndpointModule.CreateBatch);

            Dictionary<string, object> parameters = [];

            foreach (PropertyInfo property in request.GetType().GetProperties())
            {
                object? value = property.GetValue(request);

                if (value is not null && (value is not string stringValue || !string.IsNullOrEmpty(stringValue)))
                    parameters[property.Name.ToLower()] = value;
            }

            string timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString();
            string jsonPayload = JsonConvert.SerializeObject(parameters, _jsonSerializerSettings);
            string signature = GenerateSignature(_settings, timestamp, jsonPayload);

            return await _baseHttpClient.PostAsync<ApiResponse<BatchOrderResult, BatchOrderRetExtInfo>?>(uri, jsonPayload, _settings.APIKey, timestamp, signature, _settings.RecvWindow);
        }

        /// <summary>
        /// Amends order for category, symbol, orderId, qty and price
        /// </summary>
        private static async Task<ApiResponse<OrderResult, object>?> AmendOrder(string category, string symbol, string orderId, string qty, string price)
        {
            string uri = BuildUri(_settings.Endpoint, EndpointProduct.Order, EndpointModule.Amend);

            Dictionary<string, object> parameters = new()
                {
                    {nameof(category), category},
                    {nameof(symbol), symbol.ToUpper()},
                    {nameof(orderId), orderId},
                    {nameof(qty), qty},
                    {nameof(price), price}
                };

            string timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString();
            string jsonPayload = JsonConvert.SerializeObject(parameters, _jsonSerializerSettings);
            string signature = GenerateSignature(_settings, timestamp, jsonPayload);

            return await _baseHttpClient.PostAsync<ApiResponse<OrderResult, object>?>(uri, jsonPayload, _settings.APIKey, timestamp, signature, _settings.RecvWindow);
        }

        /// <summary>
        /// Cancels all orders for category and symbol
        /// </summary>
        private static async Task<ApiResponse<CancelAllOrdersResult, object>?> CancelAllOrders(string category, string symbol)
        {
            string uri = BuildUri(_settings.Endpoint, EndpointProduct.Order, EndpointModule.CancelAll);

            Dictionary<string, object> parameters = new()
                {
                    {nameof(category), category},
                    {nameof(symbol), symbol.ToUpper()}
                };

            string timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString();
            string jsonPayload = JsonConvert.SerializeObject(parameters, _jsonSerializerSettings);
            string signature = GenerateSignature(_settings, timestamp, jsonPayload);

            return await _baseHttpClient.PostAsync<ApiResponse<CancelAllOrdersResult, object>?>(uri, jsonPayload, _settings.APIKey, timestamp, signature, _settings.RecvWindow);
        }

        /// <summary>
        /// Builds URI from base URI, replacing route parameters with specified values
        /// </summary>
        private static string BuildUri(string baseUri, params string[] routeParameters)
        {
            if (routeParameters.Length == 0)
                return baseUri;

            int paramIndex = 0;

            return RouteParameterRegex().Replace(baseUri, match =>
                paramIndex < routeParameters.Length ? routeParameters[paramIndex++] ?? match.Value : match.Value);
        }

        /// <summary>
        /// Generates signature with data
        /// </summary>
        private static string GenerateSignature(Settings settings, string timestamp, string data)
        {
            string rawData = string.Concat(timestamp, settings.APIKey, settings.RecvWindow, data);
            using HMACSHA256 hmac = new(Encoding.UTF8.GetBytes(settings.APISecret));
            byte[] signature = hmac.ComputeHash(Encoding.UTF8.GetBytes(rawData));

            return Convert.ToHexStringLower(signature);
        }

        /// <summary>
        /// Converts string to decimal with decimal point, invariant culture and normalizes value
        /// </summary>
        private static bool TryParseDecimal(string? input, out decimal result)
        {
            bool success = decimal.TryParse(input, NumberStyles.Number, CultureInfo.InvariantCulture, out result);

            if (success)
                result = result.Normalize();

            return success;
        }

        /// <summary>
        /// Normalizes decimal
        /// </summary>
        private static decimal Normalize(this decimal value) =>
            value / 1.000000000000000000000000000000000m;

        /// <summary>
        /// Logs and prints message
        /// </summary>
        private static void LogAndPrint(LogLevel logLevel, string errorMessage, params object?[] parameters)
        {
            string formattedMessage = string.Format(errorMessage, parameters);
            _logger.Log(logLevel, formattedMessage);

            Console.ForegroundColor = logLevel switch
            {
                LogLevel.Critical => ConsoleColor.DarkMagenta,
                LogLevel.Error => ConsoleColor.DarkRed,
                LogLevel.Warning => ConsoleColor.DarkYellow,
                LogLevel.Information => ConsoleColor.DarkBlue,
                LogLevel.Debug => ConsoleColor.DarkGreen,
                LogLevel.Trace => ConsoleColor.DarkGray,
                _ => ConsoleColor.White
            };

            Console.WriteLine(formattedMessage);
        }
    }
}
