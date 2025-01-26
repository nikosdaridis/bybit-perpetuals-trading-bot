using BybitPerpetualsTradingBot.Models;
using BybitPerpetualsTradingBot.Models.API;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using System.Globalization;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using static BybitPerpetualsTradingBot.Models.API.ApiParameters;
using static BybitPerpetualsTradingBot.Models.API.GetTickersResult;
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
        private static Dictionary<string, GetInstrumentsInfoResult.InstrumentList> _instrumentsInfo = [];
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
        /// Loads data from file or creates file with default data
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
            catch
            {
                string defaultJson = JsonConvert.SerializeObject(defaultModel, Formatting.Indented);
                File.WriteAllText(filePath, defaultJson);
                return defaultModel;
            }
        }

        /// <summary>
        /// Initializes active trading pairs and validates pairs configuration data with instruments info and adds position info, open orders and sets leverage
        /// </summary>
        internal static async Task<bool> InitializeActiveTradingPairs()
        {
            ApiResponse<GetInstrumentsInfoResult, object>? responseInstrumentsInfo = await GetInstrumentsInfo(Category.Linear);
            if (responseInstrumentsInfo?.RetCode != 0)
            {
                _logger.LogError("Error getting instruments info: {RetMsg}", responseInstrumentsInfo?.RetMsg);
                Console.WriteLine($"Error getting instruments info: {responseInstrumentsInfo?.RetMsg}");
                return false;
            }

            _instrumentsInfo = responseInstrumentsInfo?.Result?.List?.ToDictionary(List => List.Symbol ?? string.Empty, List => List) ?? [];

            if (_instrumentsInfo.Count == 0)
            {
                _logger.LogError("No instruments info found");
                Console.WriteLine("No instruments info found");
                return false;
            }

            foreach (string pair in _pairsConfiguration.ActiveTradingPairs)
            {
                if (!_instrumentsInfo.TryGetValue(pair, out GetInstrumentsInfoResult.InstrumentList? instrumentList))
                {
                    _logger.LogError("Active trading pair {ActiveTradingPair} not found in instruments info", pair);
                    Console.WriteLine($"Active trading pair {pair} not found in instruments info");
                    return false;
                }

                if (instrumentList is null)
                {
                    _logger.LogError("Active trading pair {ActiveTradingPair} instrument list is null", pair);
                    Console.WriteLine($"Active trading pair {pair} instrument list is null");
                    return false;
                }

                if (!_pairsConfiguration.PairsConfigurations.TryGetValue(pair, out PairConfiguration? pairConfiguration))
                {
                    _logger.LogError("Active trading pair {ActiveTradingPair} not found in pairs configuration", pair);
                    Console.WriteLine($"Active trading pair {pair} not found in pairs configuration");
                    return false;
                }

                if (pairConfiguration is null)
                {
                    _logger.LogError("Active trading pair {ActiveTradingPair} configuration is null", pair);
                    Console.WriteLine($"Active trading pair {pair} configuration is null");
                    return false;
                }

                // Check if side is Buy or Sell
                if (pairConfiguration.Side is not null && (pairConfiguration.Side != Side.Buy && pairConfiguration.Side != Side.Sell))
                {
                    _logger.LogError("Active trading pair {ActiveTradingPair} side {Side} is invalid", pair, pairConfiguration.Side);
                    Console.WriteLine($"Active trading pair {pair} side {pairConfiguration.Side} is invalid");
                    return false;
                }

                // Check if leverage is within expected range
                if (pairConfiguration.Leverage is not null &&
                    TryParseDecimal(instrumentList.LeverageFilter?.MinLeverage, out decimal minLeverage) &&
                    TryParseDecimal(instrumentList.LeverageFilter?.MaxLeverage, out decimal maxLeverage))
                {
                    if (pairConfiguration.Leverage < minLeverage || pairConfiguration.Leverage > maxLeverage)
                    {
                        _logger.LogError("Active trading pair {ActiveTradingPair} leverage {Leverage} is not within the expected range of {MinLeverage} and {MaxLeverage}",
                            pair, pairConfiguration.Leverage, minLeverage, maxLeverage);
                        Console.WriteLine($"Active trading pair {pair} leverage {pairConfiguration.Leverage} is not within the expected range of {minLeverage} and {maxLeverage}");
                        return false;
                    }
                }
                else
                {
                    _logger.LogError("Invalid leverage value for active trading pair {ActiveTradingPair}", pair);
                    Console.WriteLine($"Invalid leverage value for active trading pair {pair}");
                    return false;
                }

                // Check if leverage is correct for leverage step size
                if (pairConfiguration.Leverage is not null &&
                    TryParseDecimal(instrumentList.LeverageFilter?.LeverageStep, out decimal leverageStep))
                {
                    if ((pairConfiguration.Leverage - minLeverage) % leverageStep != 0)
                    {
                        _logger.LogError("Active trading pair {ActiveTradingPair} leverage {Leverage} is not correct for leverage step size {LeverageStep}",
                            pair, pairConfiguration.Leverage, leverageStep);
                        Console.WriteLine($"Active trading pair {pair} leverage {pairConfiguration.Leverage} is not correct for leverage step size {leverageStep}");
                        return false;
                    }
                }

                // Check if initial quantity is within expected range
                if (pairConfiguration.InitialQuantity is not null &&
                    TryParseDecimal(instrumentList.LotSizeFilter?.MinOrderQty, out decimal minQty) &&
                    TryParseDecimal(instrumentList.LotSizeFilter?.MaxOrderQty, out decimal maxQty))
                {
                    if (pairConfiguration.InitialQuantity < minQty || pairConfiguration.InitialQuantity > maxQty)
                    {
                        _logger.LogError("Active trading pair {ActiveTradingPair} initial quantity {InitialQuantity} is not within the expected range of {MinQty} and {MaxQty}",
                            pair, pairConfiguration.InitialQuantity, minQty, maxQty);
                        Console.WriteLine($"Active trading pair {pair} initial quantity {pairConfiguration.InitialQuantity} is not within the expected range of {minQty} and {maxQty}");
                        return false;
                    }
                }
                else
                {
                    _logger.LogError("Invalid initial quantity value for active trading pair {ActiveTradingPair}", pair);
                    Console.WriteLine($"Invalid initial quantity value for active trading pair {pair}");
                    return false;
                }

                // Check if initial quantity is correct for quantity step size
                if (pairConfiguration.InitialQuantity is not null &&
                    TryParseDecimal(instrumentList.LotSizeFilter?.QtyStep, out decimal qtyStep))
                {
                    if ((pairConfiguration.InitialQuantity - minQty) % qtyStep != 0)
                    {
                        _logger.LogError("Active trading pair {ActiveTradingPair} initial quantity {InitialQuantity} is not correct for quantity step size {QtyStep}",
                            pair, pairConfiguration.InitialQuantity, qtyStep);
                        Console.WriteLine($"Active trading pair {pair} initial quantity {pairConfiguration.InitialQuantity} is not correct for quantity step size {qtyStep}");
                        return false;
                    }
                }

                //Check if number of steps is more than 0
                if (pairConfiguration.NumberOfScalingLevels is not null && pairConfiguration.NumberOfScalingLevels <= 0)
                {
                    _logger.LogError("Active trading pair {ActiveTradingPair} number of steps {NumberOfSteps} is invalid", pair, pairConfiguration.NumberOfScalingLevels);
                    Console.WriteLine($"Active trading pair {pair} number of steps {pairConfiguration.NumberOfScalingLevels} is invalid");
                    return false;
                }

                // Check if take profit percentage is more than 0
                if (pairConfiguration.TakeProfitPercentage is not null && pairConfiguration.TakeProfitPercentage <= 0)
                {
                    _logger.LogError("Active trading pair {ActiveTradingPair} take profit percentage {TakeProfitPercentage} is invalid", pair, pairConfiguration.TakeProfitPercentage);
                    Console.WriteLine($"Active trading pair {pair} take profit percentage {pairConfiguration.TakeProfitPercentage} is invalid");
                    return false;
                }

                // Check if initial step unrealised PnL percentage is more than 0
                if (pairConfiguration.InitialScalingUnrealizedPnL is not null && pairConfiguration.InitialScalingUnrealizedPnL <= 0)
                {
                    _logger.LogError("Active trading pair {ActiveTradingPair} initial step unrealised PnL percentage {InitialStepUnrealisedPnlPercentage} is invalid", pair, pairConfiguration.InitialScalingUnrealizedPnL);
                    Console.WriteLine($"Active trading pair {pair} initial step unrealised PnL percentage {pairConfiguration.InitialScalingUnrealizedPnL} is invalid");
                    return false;
                }

                // Check if step unrealised PnL multiplier is more than 0
                if (pairConfiguration.ScalingUnrealisedPnlMultiplier is not null && pairConfiguration.ScalingUnrealisedPnlMultiplier <= 0)
                {
                    _logger.LogError("Active trading pair {ActiveTradingPair} step unrealised PnL multiplier {StepUnrealisedPnlMultiplier} is invalid", pair, pairConfiguration.ScalingUnrealisedPnlMultiplier);
                    Console.WriteLine($"Active trading pair {pair} step unrealised PnL multiplier {pairConfiguration.ScalingUnrealisedPnlMultiplier} is invalid");
                    return false;
                }

                // Check if step quantity multiplier is more than 0
                if (pairConfiguration.ScalingQuantityMultiplier is not null && pairConfiguration.ScalingQuantityMultiplier <= 0)
                {
                    _logger.LogError("Active trading pair {ActiveTradingPair} step quantity multiplier {StepQuantityMultiplier} is invalid", pair, pairConfiguration.ScalingQuantityMultiplier);
                    Console.WriteLine($"Active trading pair {pair} step quantity multiplier {pairConfiguration.ScalingQuantityMultiplier} is invalid");
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
                    _logger.LogError("Error getting position info for active trading pair {ActiveTradingPair}: {RetMsg}", pair, responsePositionInfo?.RetMsg);
                    Console.WriteLine($"Error getting position info for active trading pair {pair}: {responsePositionInfo?.RetMsg}");
                    return false;
                }
                _activeTradingPairs[pair].Position = responsePositionInfo?.Result?.List?.FirstOrDefault() ?? new();

                // Get tickers
                ApiResponse<GetTickersResult, object>? responseTickers = await GetTickers(Category.Linear, pair);
                if (responseTickers?.RetCode != 0)
                {
                    _logger.LogError("Error getting tickers for active trading pair {ActiveTradingPair}: {RetMsg}", pair, responseTickers?.RetMsg);
                    Console.WriteLine($"Error getting tickers for active trading pair {pair}: {responseTickers?.RetMsg}");
                    return false;
                }

                // Check if price and quantity is more than min notional value
                if (TryParseDecimal(instrumentList.LotSizeFilter?.MinNotionalValue, out decimal minNotionalValue) &&
                    TryParseDecimal(responseTickers.Result?.List?.FirstOrDefault()?.LastPrice, out decimal lastPrice) &&
                    lastPrice * pairConfiguration.InitialQuantity.GetValueOrDefault() < minNotionalValue)
                {
                    decimal notionalValue = lastPrice * pairConfiguration.InitialQuantity.GetValueOrDefault();
                    _logger.LogError("Active trading pair {ActiveTradingPair} initial quantity {InitialQuantity} * latest price {LastPrice} results in notional value {NotionalValue}, which is less than the min notional value {MinNotionalValue}",
                        pair, pairConfiguration.InitialQuantity, lastPrice, notionalValue, minNotionalValue);
                    Console.WriteLine($"Active trading pair {pair} initial quantity {pairConfiguration.InitialQuantity} * latest price {lastPrice} results in notional value {notionalValue}, which is less than the min notional value {minNotionalValue}");
                    return false;
                }

                // Get open orders for active trading pair
                ApiResponse<GetOpenAndClosedOrdersResult, object>? responseOpenOrders = await GetOpenAndClosedOrders(Category.Linear, pair, OpenOnly.True);
                if (responseOpenOrders?.RetCode != 0)
                {
                    _logger.LogError("Error getting open orders for active trading pair {ActiveTradingPair}: {RetMsg}", pair, responseOpenOrders?.RetMsg);
                    Console.WriteLine($"Error getting open orders for active trading pair {pair}: {responseOpenOrders?.RetMsg}");
                    return false;
                }
                _activeTradingPairs[pair].Orders = responseOpenOrders?.Result?.List ?? [];
            }

            return true;
        }

        /// <summary>
        /// Places initial positions for active trading pairs if position size is 0
        /// </summary>
        internal static async Task<bool> PlaceInitialPositions()
        {
            foreach ((string Symbol, ActiveTradingPair ActiveTradingPair) in _activeTradingPairs)
            {
                // Get position info
                ApiResponse<GetPositionInfoResult, object>? responsePositionInfo = await GetPositionInfo(Category.Linear, Symbol);
                if (responsePositionInfo?.RetCode != 0)
                {
                    _logger.LogError("Error getting position info for active trading pair {ActiveTradingPair}: {RetMsg}", Symbol, responsePositionInfo?.RetMsg);
                    Console.WriteLine($"Error getting position info for active trading pair {Symbol}: {responsePositionInfo?.RetMsg}");
                    return false;
                }
                ActiveTradingPair.Position = responsePositionInfo?.Result?.List?.FirstOrDefault() ?? new();

                // Check if position size is 0 to place initial order
                if (!TryParseDecimal(ActiveTradingPair.Position.Size, out decimal size) || size != 0)
                    continue;

                // Get open orders
                ApiResponse<GetOpenAndClosedOrdersResult, object>? responseOpenOrders = await GetOpenAndClosedOrders(Category.Linear, Symbol, OpenOnly.True);
                if (responseOpenOrders?.RetCode != 0)
                {
                    _logger.LogError("Error getting open orders for active trading pair {ActiveTradingPair}: {RetMsg}", Symbol, responseOpenOrders?.RetMsg);
                    Console.WriteLine($"Error getting open orders for active trading pair {Symbol}: {responseOpenOrders?.RetMsg}");
                    return false;
                }

                // Get tickers
                ApiResponse<GetTickersResult, object>? responseTickers = await GetTickers(Category.Linear, Symbol);
                if (responseTickers?.RetCode != 0)
                {
                    _logger.LogError("Error getting tickers for active trading pair {ActiveTradingPair}: {RetMsg}", Symbol, responseTickers?.RetMsg);
                    Console.WriteLine($"Error getting tickers for active trading pair {Symbol}: {responseTickers?.RetMsg}");
                    return false;
                }

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
                    continue;

                // Cancel all orders
                ApiResponse<CancelAllOrdersResult, object>? responseCancelAll = await CancelAllOrders(Category.Linear, Symbol);
                if (responseCancelAll?.RetCode != 0)
                {
                    _logger.LogError("Error cancelling all orders for active trading pair {ActiveTradingPair}: {RetMsg}", Symbol, responseCancelAll?.RetMsg);
                    Console.WriteLine($"Error cancelling all orders for active trading pair {Symbol}: {responseCancelAll?.RetMsg}");
                    return false;
                }
                ActiveTradingPair.Orders = [];

                if (!TryParseDecimal(ActiveTradingPair.Position.Leverage, out decimal leverage))
                {
                    _logger.LogError("Error parsing leverage for active trading pair {Symbol}", Symbol);
                    Console.WriteLine($"Error parsing leverage for active trading pair {Symbol}");
                    return false;
                }

                // Set leverage
                if (leverage != ActiveTradingPair.Configuration.Leverage)
                {
                    ApiResponse<object, object>? responseSetLeverage = await SetLeverage(Category.Linear, Symbol, ActiveTradingPair.Configuration.Leverage.ToString()!, ActiveTradingPair.Configuration.Leverage.ToString()!);
                    if (responseSetLeverage?.RetCode != 0 && responseSetLeverage?.RetCode != 110043)
                    {
                        _logger.LogError("Error setting leverage for active trading pair {ActiveTradingPair}: {RetMsg}", Symbol, responseSetLeverage?.RetMsg);
                        Console.WriteLine($"Error setting leverage for active trading pair {Symbol}: {responseSetLeverage?.RetMsg}");
                        return false;
                    }
                }

                // Calculate order price based on last price offset by 2 tick size
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
                    _logger.LogError("Error parsing price or tick size for active trading pair {Symbol}", Symbol);
                    Console.WriteLine($"Error parsing price or tick size for active trading pair {Symbol}");
                    return false;
                }
                orderPrice = price + (priceModifier * tickSize);

                // Place initial order
                ApiResponse<OrderResult, object>? responsePlaceOrder = await PlaceOrder(
                    Category.Linear,
                    Symbol,
                    ActiveTradingPair.Configuration.Side!,
                    OrderType.Limit,
                    ActiveTradingPair.Configuration.InitialQuantity.ToString()!,
                    orderPrice.ToString());
                if (responsePlaceOrder?.RetCode != 0)
                {
                    _logger.LogError("Error placing initial order for active trading pair {ActiveTradingPair}: {RetMsg}", Symbol, responsePlaceOrder?.RetMsg);
                    Console.WriteLine($"Error placing initial order for active trading pair {Symbol}: {responsePlaceOrder?.RetMsg}");
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Places take profit orders for active trading pairs if position size is more than 0 and no open orders
        /// </summary>
        internal static async Task<bool> PlaceTakeProfitOrders()
        {
            foreach ((string Symbol, ActiveTradingPair ActiveTradingPair) in _activeTradingPairs)
            {
                // Get and update position info
                ApiResponse<GetPositionInfoResult, object>? responsePositionInfo = await GetPositionInfo(Category.Linear, Symbol);
                if (responsePositionInfo?.RetCode != 0)
                {
                    _logger.LogError("Error getting position info for active trading pair {ActiveTradingPair}: {RetMsg}", Symbol, responsePositionInfo?.RetMsg);
                    Console.WriteLine($"Error getting position info for active trading pair {Symbol}: {responsePositionInfo?.RetMsg}");
                    return false;
                }
                ActiveTradingPair.Position = responsePositionInfo?.Result?.List?.FirstOrDefault() ?? new();

                // Check if position size is more than 0 to place reduce only order
                if (!TryParseDecimal(ActiveTradingPair.Position.Size, out decimal size) || size <= 0)
                    continue;

                // Check if there is open reduce only order
                ApiResponse<GetOpenAndClosedOrdersResult, object>? responseOpenOrders = await GetOpenAndClosedOrders(Category.Linear, Symbol, OpenOnly.True);
                if (responseOpenOrders?.RetCode != 0)
                {
                    _logger.LogError("Error getting open orders for active trading pair {ActiveTradingPair}: {RetMsg}", Symbol, responseOpenOrders?.RetMsg);
                    Console.WriteLine($"Error getting open orders for active trading pair {Symbol}: {responseOpenOrders?.RetMsg}");
                    return false;
                }
                if (responseOpenOrders.Result?.List?.Any(order => order.ReduceOnly.GetValueOrDefault()) == true)
                    continue;

                if (!TryParseDecimal(ActiveTradingPair.Position.AvgPrice, out decimal averagePrice) || averagePrice <= 0)
                {
                    _logger.LogError("Error parsing average price for active trading pair {Symbol}", Symbol);
                    Console.WriteLine($"Error parsing average price for active trading pair {Symbol}");
                    return false;
                }

                string reduceOnlySide = ActiveTradingPair.Configuration.Side == Side.Buy ? Side.Sell : Side.Buy;
                decimal takeProfitFactor = ActiveTradingPair.Configuration.TakeProfitPercentage!.Value / (100 * ActiveTradingPair.Configuration.Leverage!.Value);
                decimal takeProfitPrice;
                if (ActiveTradingPair.Configuration.Side == Side.Buy)
                    takeProfitPrice = averagePrice * (1 + takeProfitFactor);
                else
                    takeProfitPrice = averagePrice * (1 - takeProfitFactor);

                // Place take profit order with reduce only
                ApiResponse<OrderResult, object>? responsePlaceOrder = await PlaceOrder(Category.Linear, Symbol, reduceOnlySide, OrderType.Limit, "0", takeProfitPrice.ToString(), TimeInForce.PostOnly, true);
                if (responsePlaceOrder?.RetCode != 0)
                {
                    _logger.LogError("Error placing reduce only order for active trading pair {ActiveTradingPair}: {RetMsg}", Symbol, responsePlaceOrder?.RetMsg);
                    Console.WriteLine($"Error placing reduce only order for active trading pair {Symbol}: {responsePlaceOrder?.RetMsg}");
                    return false;
                }
            }

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
        /// Amends order for category, symbol, orderId and price
        /// </summary>
        private static async Task<ApiResponse<OrderResult, object>?> AmendOrder(string category, string symbol, string orderId, string price)
        {
            string uri = BuildUri(_settings.Endpoint, EndpointProduct.Order, EndpointModule.Amend);

            Dictionary<string, object> parameters = new()
                {
                    {nameof(category), category},
                    {nameof(symbol), symbol.ToUpper()},
                    {nameof(orderId), orderId},
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
        /// Converts string to decimal with decimal point and invariant culture
        /// </summary>
        private static bool TryParseDecimal(string? input, out decimal result) =>
            decimal.TryParse(input, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out result);
    }
}
