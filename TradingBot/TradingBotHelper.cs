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

        private static BaseHttpClient _baseHttpClient = default!;
        private static ILogger<TradingBot> _logger = default!;
        private static Settings _settings = default!;
        private static PairsConfiguration _pairsConfiguration = default!;
        private static Dictionary<string, InstrumentList> _instrumentsInfo = [];
        private static readonly Dictionary<string, ActiveTradingPair> _activeTradingPairs = [];

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
                LogAndPrint(LogLevel.Error, "Invalid Json - Error reading file '{0}': {1}", filePath, ex.Message);

                if (File.Exists(filePath))
                {
                    try
                    {
                        string backupFilePath = Path.ChangeExtension(filePath, $".invalid.{DateTime.Now:MMddHHmmss}.json");

                        File.Move(filePath, backupFilePath);
                        LogAndPrint(LogLevel.Warning, "Existing file backed up as {0}", backupFilePath);
                    }
                    catch (Exception backupEx)
                    {
                        LogAndPrint(LogLevel.Error, "Failed to back up the existing file: {0}", backupEx.Message);
                    }
                }

                string defaultJson = JsonConvert.SerializeObject(defaultModel, Formatting.Indented);
                File.WriteAllText(filePath, defaultJson);
                return defaultModel;
            }
        }

        /// <summary>
        /// Executes trading tasks concurrently for pairs
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
                            LogAndPrint(LogLevel.Information, "({0}) {1} {2:F1} sec", taskFunc.Method.Name, pair.Key, pairStopwatch.Elapsed.TotalSeconds);

                        return result;
                    }
                    catch (Exception ex)
                    {
                        LogAndPrint(LogLevel.Error, "({0}) Exception in task execution for {1}: {2}", taskFunc.Method.Name, pair.Key, ex.Message);
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
        /// Initializes active trading pairs and validates pairs configuration data with instruments info and adds position info
        /// </summary>
        internal static async Task<bool> InitializeActiveTradingPairs()
        {
            Console.ForegroundColor = ConsoleColor.DarkYellow;
            Console.WriteLine("Initializing active trading pairs and validating configuration data");

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
                if (pairConfiguration.Side is null || (pairConfiguration.Side != Side.Buy && pairConfiguration.Side != Side.Sell))
                {
                    LogAndPrint(LogLevel.Error, "Active trading pair {0} side {1} is invalid", pair, pairConfiguration.Side);
                    return false;
                }

                //Check if leverage is more then 0
                if (pairConfiguration.Leverage is null || pairConfiguration.Leverage <= 0)
                {
                    LogAndPrint(LogLevel.Error, "Active trading pair {0} leverage {1} is invalid", pair, pairConfiguration.Leverage);
                    return false;
                }

                // Check if leverage is within expected range and step
                if (TryParseDecimal(instrumentList.LeverageFilter?.MinLeverage, out decimal minLeverage) &&
                    TryParseDecimal(instrumentList.LeverageFilter?.MaxLeverage, out decimal maxLeverage) &&
                    TryParseDecimal(instrumentList.LeverageFilter?.LeverageStep, out decimal leverageStep))
                {
                    if (pairConfiguration.Leverage < minLeverage || pairConfiguration.Leverage > maxLeverage || (pairConfiguration.Leverage - minLeverage) % leverageStep != 0)
                    {
                        LogAndPrint(LogLevel.Error, "Active trading pair {0} leverage {1} is invalid", pair, pairConfiguration.Leverage);
                        return false;
                    }
                }
                else
                {
                    LogAndPrint(LogLevel.Error, "Active trading pair {0} leverage filter data is invalid", pair);
                    return false;
                }

                //Check if initial margin is not negative
                if (pairConfiguration.InitialMargin is null || pairConfiguration.InitialMargin < 0)
                {
                    LogAndPrint(LogLevel.Error, "Active trading pair {0} initial margin {1} is invalid", pair, pairConfiguration.InitialMargin);
                    return false;
                }

                //Check if initial price tick size offset is not negative
                if (pairConfiguration.InitialPriceTickSizeOffset is null || pairConfiguration.InitialPriceTickSizeOffset < 0)
                {
                    LogAndPrint(LogLevel.Error, "Active trading pair {0} initial price tick size offset {1} is invalid", pair, pairConfiguration.InitialPriceTickSizeOffset);
                    return false;
                }

                //Check if initial price tick size threshold is more than 0 and more than initial order tick size
                if (pairConfiguration.InitialPriceTickSizeThreshold is null || pairConfiguration.InitialPriceTickSizeThreshold <= 0 ||
                    pairConfiguration.InitialPriceTickSizeThreshold <= pairConfiguration.InitialPriceTickSizeOffset)
                {
                    LogAndPrint(LogLevel.Error, "Active trading pair {0} initial price tick size threshold {1} is invalid", pair, pairConfiguration.InitialPriceTickSizeThreshold);
                    return false;
                }

                // Check if take profit percentage is more than 0
                if (pairConfiguration.TakeProfitPercentage is null || pairConfiguration.TakeProfitPercentage <= 0)
                {
                    LogAndPrint(LogLevel.Error, "Active trading pair {0} take profit percentage {1} is invalid", pair, pairConfiguration.TakeProfitPercentage);
                    return false;
                }

                //Check if number of scaling levels is not negative
                if (pairConfiguration.NumberOfScalingLevels is null || pairConfiguration.NumberOfScalingLevels < 0)
                {
                    LogAndPrint(LogLevel.Error, "Active trading pair {0} number of scaling levels {1} is invalid", pair, pairConfiguration.NumberOfScalingLevels);
                    return false;
                }

                // Check if initial step unrealised PnL percentage is more than 0
                if (pairConfiguration.InitialScalingUnrealizedPnL is null || pairConfiguration.InitialScalingUnrealizedPnL <= 0)
                {
                    LogAndPrint(LogLevel.Error, "Active trading pair {0} initial step unrealised PnL percentage {1} is invalid", pair, pairConfiguration.InitialScalingUnrealizedPnL);
                    return false;
                }

                // Check if initial step quantity multiplier is more than 0
                if (pairConfiguration.InitialScalingQuantityMultiplier is null || pairConfiguration.InitialScalingQuantityMultiplier <= 0)
                {
                    LogAndPrint(LogLevel.Error, "Active trading pair {0} initial step quantity multiplier {1} is invalid", pair, pairConfiguration.InitialScalingQuantityMultiplier);
                    return false;
                }

                // Check if max step unrealised PnL percentage is more than initial step unrealised PnL percentage
                if (pairConfiguration.MaxScalingUnrealizedPnL is null || pairConfiguration.MaxScalingUnrealizedPnL < pairConfiguration.InitialScalingUnrealizedPnL)
                {
                    LogAndPrint(LogLevel.Error, "Active trading pair {0} max step unrealised PnL percentage {1} is invalid", pair, pairConfiguration.MaxScalingUnrealizedPnL);
                    return false;
                }

                // Check if step unrealised PnL multiplier is more than 0
                if (pairConfiguration.ScalingUnrealisedPnlMultiplier is null || pairConfiguration.ScalingUnrealisedPnlMultiplier <= 0)
                {
                    LogAndPrint(LogLevel.Error, "Active trading pair {0} step unrealised PnL multiplier {1} is invalid", pair, pairConfiguration.ScalingUnrealisedPnlMultiplier);
                    return false;
                }

                // Check if step quantity multiplier is more than 0
                if (pairConfiguration.ScalingQuantityMultiplier is null || pairConfiguration.ScalingQuantityMultiplier <= 0)
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
            }

            Console.ForegroundColor = ConsoleColor.DarkGreen;
            Console.WriteLine("Active trading pairs initialized successfully");

            return true;
        }

        /// <summary>
        /// Places initial position for active trading pair
        /// </summary>
        internal static async Task<bool> PlaceInitialPosition(string Symbol, ActiveTradingPair ActiveTradingPair)
        {
            // Get position info
            ApiResponse<GetPositionInfoResult, object>? responsePositionInfo = await GetPositionInfo(Category.Linear, Symbol);
            if (responsePositionInfo?.RetCode != 0)
            {
                LogAndPrint(LogLevel.Error, "Error getting position info for active trading pair {0}: {1}", Symbol, responsePositionInfo?.RetMsg);
                return false;
            }
            ActiveTradingPair.Position = responsePositionInfo?.Result?.List?.FirstOrDefault() ?? new();

            // Check if position size is 0
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

            // Check if there is open order with price close to last price within tick size threshold to wait for it to be filled
            if (responseOpenOrders.Result?.List?.Any(order =>
            {
                if (TryParseDecimal(order.Price, out decimal orderPrice) &&
                    TryParseDecimal(responseTickers.Result?.List?.FirstOrDefault()?.LastPrice, out decimal lastPrice) &&
                    TryParseDecimal(_instrumentsInfo[Symbol].PriceFilter?.TickSize, out decimal tickSize))
                {
                    return Math.Abs(orderPrice - lastPrice) < tickSize * ActiveTradingPair.Configuration.InitialPriceTickSizeThreshold;
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
                if (responseSetLeverage?.RetCode != 0 && responseSetLeverage?.RetCode != 110043) // 110043: leverage is the same
                {
                    LogAndPrint(LogLevel.Error, "Error setting leverage for active trading pair {0}: {1}", Symbol, responseSetLeverage?.RetMsg);
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
            if (!TryParseDecimal(ActiveTradingPair.Position.Size, out decimal latestSize) || latestSize > 0)
                return false;

            // Calculate quantity based on leverage, initial margin and qty step and check if it is more than min order qty and min notional value
            decimal adjustedQuantity = 0;
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

            // Update active trading pair with calculated initial quantity and reset scaling levels
            ActiveTradingPair.CalculatedInitialQuantity = adjustedQuantity;
            ActiveTradingPair.ScalingLevels = [];
            ActiveTradingPair.ScalingLevelsToBePlaced = [];

            return true;
        }

        /// <summary>
        /// Places or amends take profit orders for active trading pair
        /// </summary>
        internal static async Task<bool> PlaceOrAmendTakeProfitOrder(string Symbol, ActiveTradingPair ActiveTradingPair)
        {
            // Get and update position info
            ApiResponse<GetPositionInfoResult, object>? responsePositionInfo = await GetPositionInfo(Category.Linear, Symbol);
            if (responsePositionInfo?.RetCode != 0)
            {
                LogAndPrint(LogLevel.Error, "Error getting position info for active trading pair {0}: {1}", Symbol, responsePositionInfo?.RetMsg);
                return false;
            }
            ActiveTradingPair.Position = responsePositionInfo?.Result?.List?.FirstOrDefault() ?? new();

            // Check if position size is more than 0
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

            takeProfitPrice = (Math.Round(takeProfitPrice / priceTickSize) * priceTickSize).Normalize();
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

            if (!TryParseDecimal(responseTickers.Result?.List?.FirstOrDefault()?.LastPrice, out decimal lastPrice) || lastPrice <= 0)
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
        /// Places scaling orders for active trading pair in batches of 10 max
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
                if (responseOpenOrders.Result?.List?.Any(order => order.Side == ActiveTradingPair.Configuration.Side) == true)
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

            // Batch place orders in groups of 10 max
            while (ActiveTradingPair.ScalingLevelsToBePlaced.Count > 0)
            {
                List<ScalingLevel> scalingLevelsBatch = [.. ActiveTradingPair.ScalingLevelsToBePlaced.Take(10)];

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

                List<GetOpenAndClosedOrdersDetails> openOrders = responseOpenOrders.Result?.List ?? [];

                // Remove successfully placed orders from scaling levels to be placed
                ActiveTradingPair.ScalingLevelsToBePlaced.RemoveAll(scalingLevel =>
                    openOrders.Any(order =>
                        order.Side == ActiveTradingPair.Configuration.Side &&
                        TryParseDecimal(order.Price, out decimal orderPrice) &&
                        TryParseDecimal(order.Quantity, out decimal orderQuantity) &&
                        scalingLevel.Price == orderPrice &&
                        scalingLevel.Quantity == orderQuantity));

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
        /// Calculates scaling levels for active trading pair based on configuration
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
            decimal levelQty = currentQty * activeTradingPair.Configuration.InitialScalingQuantityMultiplier ?? 1;

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

            activeTradingPair.ScalingLevels = [.. scalingLevels];
            activeTradingPair.ScalingLevelsToBePlaced = [.. scalingLevels];
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
            (string timestamp, string signature) = GenerateSignature(_settings, query);

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
            (string timestamp, string signature) = GenerateSignature(_settings, query);

            return await _baseHttpClient.GetAsync<ApiResponse<GetTickersResult, object>?>(uri, _settings.APIKey, timestamp, signature, _settings.RecvWindow);
        }

        /// <summary>
        /// Gets position info for category and symbol
        /// </summary>
        private static async Task<ApiResponse<GetPositionInfoResult, object>?> GetPositionInfo(string category, string symbol)
        {
            string query = $"{nameof(category)}={category}&{nameof(symbol)}={symbol.ToUpper()}";
            string uri = BuildUri(_settings.Endpoint, EndpointProduct.Position, string.Concat(EndpointModule.List, '?', query));
            (string timestamp, string signature) = GenerateSignature(_settings, query);

            return await _baseHttpClient.GetAsync<ApiResponse<GetPositionInfoResult, object>?>(uri, _settings.APIKey, timestamp, signature, _settings.RecvWindow);
        }

        /// <summary>
        /// Gets open and closed orders for category, symbol and openOnly with optional limit
        /// </summary>
        private static async Task<ApiResponse<GetOpenAndClosedOrdersResult, object>?> GetOpenAndClosedOrders(string category, string symbol, int openOnly, int limit = 50)
        {
            string query = $"{nameof(category)}={category}&{nameof(symbol)}={symbol.ToUpper()}&{nameof(openOnly)}={openOnly}&{nameof(limit)}={limit}";
            string uri = BuildUri(_settings.Endpoint, EndpointProduct.Order, string.Concat(EndpointModule.RealTime, '?', query));
            (string timestamp, string signature) = GenerateSignature(_settings, query);

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

            string jsonPayload = JsonConvert.SerializeObject(parameters, _jsonSerializerSettings);
            (string timestamp, string signature) = GenerateSignature(_settings, jsonPayload);

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
            string jsonPayload = JsonConvert.SerializeObject(parameters, _jsonSerializerSettings);
            (string timestamp, string signature) = GenerateSignature(_settings, jsonPayload);

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

            string jsonPayload = JsonConvert.SerializeObject(parameters, _jsonSerializerSettings);
            (string timestamp, string signature) = GenerateSignature(_settings, jsonPayload);

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

            string jsonPayload = JsonConvert.SerializeObject(parameters, _jsonSerializerSettings);
            (string timestamp, string signature) = GenerateSignature(_settings, jsonPayload);

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

            string jsonPayload = JsonConvert.SerializeObject(parameters, _jsonSerializerSettings);
            (string timestamp, string signature) = GenerateSignature(_settings, jsonPayload);

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
        private static (string Timestamp, string Signature) GenerateSignature(Settings settings, string data)
        {
            string timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString();
            string rawData = string.Concat(timestamp, settings.APIKey, settings.RecvWindow, data);
            using HMACSHA256 hmac = new(Encoding.UTF8.GetBytes(settings.APISecret));
            byte[] signature = hmac.ComputeHash(Encoding.UTF8.GetBytes(rawData));

            return (timestamp, Convert.ToHexStringLower(signature));
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

            _logger?.Log(logLevel, formattedMessage);

            Console.ForegroundColor = logLevel switch
            {
                LogLevel.Critical => ConsoleColor.DarkMagenta,
                LogLevel.Error => ConsoleColor.DarkRed,
                LogLevel.Warning => ConsoleColor.DarkYellow,
                LogLevel.Information => ConsoleColor.DarkCyan,
                LogLevel.Debug => ConsoleColor.DarkGreen,
                LogLevel.Trace => ConsoleColor.DarkGray,
                _ => ConsoleColor.White
            };

            Console.WriteLine(formattedMessage);
        }
    }
}
