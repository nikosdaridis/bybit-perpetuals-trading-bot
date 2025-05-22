using Newtonsoft.Json;

namespace BybitPerpetualsTradingBot.Models.API
{
    internal sealed class ApiResponse<TBusinessData, TRetExtInfo>
    {
        [JsonProperty("retCode")]
        public int RetCode { get; init; }

        [JsonProperty("retMsg")]
        public string? RetMsg { get; init; }

        [JsonProperty("result")]
        public TBusinessData? Result { get; init; }

        [JsonProperty("retExtInfo")]
        public TRetExtInfo? RetExtInfo { get; init; }

        [JsonProperty("time")]
        public long Time { get; init; }
    }

    internal sealed class GetInstrumentsInfoResult
    {
        [JsonProperty("category")]
        public string? Category { get; init; }

        [JsonProperty("nextPageCursor")]
        public string? NextPageCursor { get; init; }

        [JsonProperty("list")]
        public List<InstrumentList>? List { get; init; }

        internal sealed class InstrumentList
        {
            [JsonProperty("symbol")]
            public string? Symbol { get; init; }

            [JsonProperty("contractType")]
            public string? ContractType { get; init; }

            [JsonProperty("status")]
            public string? Status { get; init; }

            [JsonProperty("baseCoin")]
            public string? BaseCoin { get; init; }

            [JsonProperty("quoteCoin")]
            public string? QuoteCoin { get; init; }

            [JsonProperty("launchTime")]
            public string? LaunchTime { get; init; }

            [JsonProperty("deliveryTime")]
            public string? DeliveryTime { get; init; }

            [JsonProperty("deliveryFeeRate")]
            public string? DeliveryFeeRate { get; init; }

            [JsonProperty("priceScale")]
            public string? PriceScale { get; init; }

            [JsonProperty("leverageFilter")]
            public LeverageFilter? LeverageFilter { get; init; }

            [JsonProperty("priceFilter")]
            public PriceFilter? PriceFilter { get; init; }

            [JsonProperty("lotSizeFilter")]
            public LotSizeFilter? LotSizeFilter { get; init; }

            [JsonProperty("unifiedMarginTrade")]
            public bool UnifiedMarginTrade { get; init; }

            [JsonProperty("fundingInterval")]
            public int FundingInterval { get; init; }

            [JsonProperty("settleCoin")]
            public string? SettleCoin { get; init; }

            [JsonProperty("copyTrading")]
            public string? CopyTrading { get; init; }

            [JsonProperty("upperFundingRate")]
            public string? UpperFundingRate { get; init; }

            [JsonProperty("lowerFundingRate")]
            public string? LowerFundingRate { get; init; }

            [JsonProperty("isPreListing")]
            public bool IsPreListing { get; init; }

            [JsonProperty("preListingInfo")]
            public PreListingInfo? PreListingInfo { get; init; }

            [JsonProperty("riskParameters")]
            public RiskParameters? RiskParameters { get; init; }
        }

        internal sealed class LeverageFilter
        {
            [JsonProperty("minLeverage")]
            public string? MinLeverage { get; init; }

            [JsonProperty("maxLeverage")]
            public string? MaxLeverage { get; init; }

            [JsonProperty("leverageStep")]
            public string? LeverageStep { get; init; }
        }

        internal sealed class PriceFilter
        {
            [JsonProperty("minPrice")]
            public string? MinPrice { get; init; }

            [JsonProperty("maxPrice")]
            public string? MaxPrice { get; init; }

            [JsonProperty("tickSize")]
            public string? TickSize { get; init; }
        }

        internal sealed class LotSizeFilter
        {
            [JsonProperty("minNotionalValue")]
            public string? MinNotionalValue { get; init; }

            [JsonProperty("maxOrderQty")]
            public string? MaxOrderQty { get; init; }

            [JsonProperty("maxMktOrderQty")]
            public string? MaxMktOrderQty { get; init; }

            [JsonProperty("minOrderQty")]
            public string? MinOrderQty { get; init; }

            [JsonProperty("qtyStep")]
            public string? QtyStep { get; init; }

            [JsonProperty("postOnlyMaxOrderQty")]
            public string? PostOnlyMaxOrderQty { get; init; }
        }

        internal sealed class RiskParameters
        {
            [JsonProperty("priceLimitRatioX")]
            public string? PriceLimitRatioX { get; init; }

            [JsonProperty("priceLimitRatioY")]
            public string? PriceLimitRatioY { get; init; }
        }

        internal sealed class PreListingInfo
        {
            [JsonProperty("curAuctionPhase")]
            public string? CurAuctionPhase { get; init; }

            [JsonProperty("phases")]
            public List<Phase>? Phases { get; init; }

            [JsonProperty("auctionFeeInfo")]
            public AuctionFeeInfo? AuctionFeeInfo { get; init; }

        }
        internal sealed class Phase
        {
            [JsonProperty("phase")]
            public string? PhaseName { get; init; }

            [JsonProperty("startTime")]
            public string? StartTime { get; init; }

            [JsonProperty("endTime")]
            public string? EndTime { get; init; }
        }

        internal sealed class AuctionFeeInfo
        {
            [JsonProperty("auctionFeeRate")]
            public string? AuctionFeeRate { get; init; }

            [JsonProperty("takerFeeRate")]
            public string? TakerFeeRate { get; init; }

            [JsonProperty("makerFeeRate")]
            public string? MakerFeeRate { get; init; }
        }
    }

    internal sealed class GetTickersResult
    {
        [JsonProperty("category")]
        public string? Category { get; init; }

        [JsonProperty("list")]
        public List<TickersList>? List { get; init; }

        internal sealed class TickersList
        {
            [JsonProperty("symbol")]
            public string? Symbol { get; init; }

            [JsonProperty("lastPrice")]
            public string? LastPrice { get; init; }

            [JsonProperty("indexPrice")]
            public string? IndexPrice { get; init; }

            [JsonProperty("markPrice")]
            public string? MarkPrice { get; init; }

            [JsonProperty("prevPrice24h")]
            public string? PrevPrice24h { get; init; }

            [JsonProperty("price24hPcnt")]
            public string? Price24hPcnt { get; init; }

            [JsonProperty("highPrice24h")]
            public string? HighPrice24h { get; init; }

            [JsonProperty("lowPrice24h")]
            public string? LowPrice24h { get; init; }

            [JsonProperty("prevPrice1h")]
            public string? PrevPrice1h { get; init; }

            [JsonProperty("openInterest")]
            public string? OpenInterest { get; init; }

            [JsonProperty("openInterestValue")]
            public string? OpenInterestValue { get; init; }

            [JsonProperty("turnover24h")]
            public string? Turnover24h { get; init; }

            [JsonProperty("volume24h")]
            public string? Volume24h { get; init; }

            [JsonProperty("fundingRate")]
            public string? FundingRate { get; init; }

            [JsonProperty("nextFundingTime")]
            public string? NextFundingTime { get; init; }

            [JsonProperty("predictedDeliveryPrice")]
            public string? PredictedDeliveryPrice { get; init; }

            [JsonProperty("basisRate")]
            public string? BasisRate { get; init; }

            [JsonProperty("basis")]
            public string? Basis { get; init; }

            [JsonProperty("deliveryFeeRate")]
            public string? DeliveryFeeRate { get; init; }

            [JsonProperty("deliveryTime")]
            public string? DeliveryTime { get; init; }

            [JsonProperty("ask1Size")]
            public string? Ask1Size { get; init; }

            [JsonProperty("bid1Price")]
            public string? Bid1Price { get; init; }

            [JsonProperty("ask1Price")]
            public string? Ask1Price { get; init; }

            [JsonProperty("bid1Size")]
            public string? Bid1Size { get; init; }

            [JsonProperty("preOpenPrice")]
            public string? PreOpenPrice { get; init; }

            [JsonProperty("preQty")]
            public string? PreQty { get; init; }

            [JsonProperty("curPreListingPhase")]
            public string? CurPreListingPhase { get; init; }
        }
    }

    internal sealed class GetPositionInfoResult
    {
        [JsonProperty("list")]
        public List<GetPositionInfoList>? List { get; init; }

        [JsonProperty("nextPageCursor")]
        public string? NextPageCursor { get; init; }

        [JsonProperty("category")]
        public string? Category { get; init; }

        internal sealed class GetPositionInfoList
        {
            [JsonProperty("positionIdx")]
            public int PositionIdx { get; init; }

            [JsonProperty("riskId")]
            public int RiskId { get; init; }

            [JsonProperty("riskLimitValue")]
            public string? RiskLimitValue { get; init; }

            [JsonProperty("symbol")]
            public string? Symbol { get; init; }

            [JsonProperty("side")]
            public string? Side { get; init; }

            [JsonProperty("size")]
            public string? Size { get; init; }

            [JsonProperty("avgPrice")]
            public string? AvgPrice { get; init; }

            [JsonProperty("positionValue")]
            public string? PositionValue { get; init; }

            [JsonProperty("tradeMode")]
            public int TradeMode { get; init; }

            [JsonProperty("positionStatus")]
            public string? PositionStatus { get; init; }

            [JsonProperty("autoAddMargin")]
            public int AutoAddMargin { get; init; }

            [JsonProperty("adlRankIndicator")]
            public int AdlRankIndicator { get; init; }

            [JsonProperty("leverage")]
            public string? Leverage { get; init; }

            [JsonProperty("positionBalance")]
            public string? PositionBalance { get; init; }

            [JsonProperty("markPrice")]
            public string? MarkPrice { get; init; }

            [JsonProperty("liqPrice")]
            public string? LiqPrice { get; init; }

            [JsonProperty("bustPrice")]
            public string? BustPrice { get; init; }

            [JsonProperty("positionMM")]
            public string? PositionMM { get; init; }

            [JsonProperty("positionIM")]
            public string? PositionIM { get; init; }

            [JsonProperty("tpslMode")]
            public string? TpslMode { get; init; }

            [JsonProperty("takeProfit")]
            public string? TakeProfit { get; init; }

            [JsonProperty("stopLoss")]
            public string? StopLoss { get; init; }

            [JsonProperty("trailingStop")]
            public string? TrailingStop { get; init; }

            [JsonProperty("unrealisedPnl")]
            public string? UnrealisedPnl { get; init; }

            [JsonProperty("curRealisedPnl")]
            public string? CurRealisedPnl { get; init; }

            [JsonProperty("cumRealisedPnl")]
            public string? CumRealisedPnl { get; init; }

            [JsonProperty("seq")]
            public long Seq { get; init; }

            [JsonProperty("isReduceOnly")]
            public bool IsReduceOnly { get; init; }

            [JsonProperty("mmrSysUpdateTime")]
            public string? MmrSysUpdateTime { get; init; }

            [JsonProperty("leverageSysUpdatedTime")]
            public string? LeverageSysUpdatedTime { get; init; }

            [JsonProperty("sessionAvgPrice")]
            public string? SessionAvgPrice { get; init; }

            [JsonProperty("createdTime")]
            public string? CreatedTime { get; init; }

            [JsonProperty("updatedTime")]
            public string? UpdatedTime { get; init; }
        }
    }

    internal sealed class GetOpenAndClosedOrdersResult
    {
        [JsonProperty("category")]
        public string? Category { get; init; }

        [JsonProperty("nextPageCursor")]
        public string? NextPageCursor { get; init; }

        [JsonProperty("list")]
        public List<GetOpenAndClosedOrdersDetails>? List { get; init; }

        internal sealed class GetOpenAndClosedOrdersDetails
        {
            [JsonProperty("orderId")]
            public string? OrderId { get; init; }

            [JsonProperty("orderLinkId")]
            public string? OrderLinkId { get; init; }

            [JsonProperty("blockTradeId")]
            public string? BlockTradeId { get; init; }

            [JsonProperty("symbol")]
            public string? Symbol { get; init; }

            [JsonProperty("price")]
            public string? Price { get; init; }

            [JsonProperty("qty")]
            public string? Quantity { get; init; }

            [JsonProperty("side")]
            public string? Side { get; init; }

            [JsonProperty("isLeverage")]
            public string? IsLeverage { get; init; }

            [JsonProperty("positionIdx")]
            public int? PositionIdx { get; init; }

            [JsonProperty("orderStatus")]
            public string? OrderStatus { get; init; }

            [JsonProperty("createType")]
            public string? CreateType { get; init; }

            [JsonProperty("cancelType")]
            public string? CancelType { get; init; }

            [JsonProperty("rejectReason")]
            public string? RejectReason { get; init; }

            [JsonProperty("avgPrice")]
            public string? AvgPrice { get; init; }

            [JsonProperty("leavesQty")]
            public string? LeavesQty { get; init; }

            [JsonProperty("leavesValue")]
            public string? LeavesValue { get; init; }

            [JsonProperty("cumExecQty")]
            public string? CumExecQty { get; init; }

            [JsonProperty("cumExecValue")]
            public string? CumExecValue { get; init; }

            [JsonProperty("cumExecFee")]
            public string? CumExecFee { get; init; }

            [JsonProperty("timeInForce")]
            public string? TimeInForce { get; init; }

            [JsonProperty("orderType")]
            public string? OrderType { get; init; }

            [JsonProperty("stopOrderType")]
            public string? StopOrderType { get; init; }

            [JsonProperty("orderIv")]
            public string? OrderIv { get; init; }

            [JsonProperty("marketUnit")]
            public string? MarketUnit { get; init; }

            [JsonProperty("triggerPrice")]
            public string? TriggerPrice { get; init; }

            [JsonProperty("takeProfit")]
            public string? TakeProfit { get; init; }

            [JsonProperty("stopLoss")]
            public string? StopLoss { get; init; }

            [JsonProperty("tpslMode")]
            public string? TpslMode { get; init; }

            [JsonProperty("ocoTriggerBy")]
            public string? OcoTriggerBy { get; init; }

            [JsonProperty("tpLimitPrice")]
            public string? TpLimitPrice { get; init; }

            [JsonProperty("slLimitPrice")]
            public string? SlLimitPrice { get; init; }

            [JsonProperty("tpTriggerBy")]
            public string? TpTriggerBy { get; init; }

            [JsonProperty("slTriggerBy")]
            public string? SlTriggerBy { get; init; }

            [JsonProperty("triggerDirection")]
            public int? TriggerDirection { get; init; }

            [JsonProperty("triggerBy")]
            public string? TriggerBy { get; init; }

            [JsonProperty("lastPriceOnCreated")]
            public string? LastPriceOnCreated { get; init; }

            [JsonProperty("reduceOnly")]
            public bool? ReduceOnly { get; init; }

            [JsonProperty("closeOnTrigger")]
            public bool? CloseOnTrigger { get; init; }

            [JsonProperty("placeType")]
            public string? PlaceType { get; init; }

            [JsonProperty("smpType")]
            public string? SmpType { get; init; }

            [JsonProperty("smpGroup")]
            public int? SmpGroup { get; init; }

            [JsonProperty("smpOrderId")]
            public string? SmpOrderId { get; init; }

            [JsonProperty("createdTime")]
            public string? CreatedTime { get; init; }

            [JsonProperty("updatedTime")]
            public string? UpdatedTime { get; init; }
        }
    }

    internal sealed class OrderResult
    {
        [JsonProperty("orderId")]
        public string? OrderId { get; init; }

        [JsonProperty("orderLinkId")]
        public string? OrderLinkId { get; init; }
    }

    internal sealed class BatchOrderResult
    {
        [JsonProperty("list")]
        public List<BatchOrderDetails>? List { get; init; }

        internal sealed class BatchOrderDetails
        {
            [JsonProperty("category")]
            public string? Category { get; init; }

            [JsonProperty("symbol")]
            public string? Symbol { get; init; }

            [JsonProperty("orderId")]
            public string? OrderId { get; init; }

            [JsonProperty("orderLinkId")]
            public string? OrderLinkId { get; init; }

            [JsonProperty("createAt")]
            public string? CreateAt { get; init; }
        }
    }

    internal sealed class BatchOrderRetExtInfo
    {
        [JsonProperty("list")]
        public List<RetExtDetails>? List { get; init; }

        internal sealed class RetExtDetails
        {
            [JsonProperty("code")]
            public int Code { get; init; }

            [JsonProperty("msg")]
            public string? Msg { get; init; }
        }
    }

    internal sealed class CancelAllOrdersResult
    {
        [JsonProperty("list")]
        public List<OrderResult>? List { get; init; }

        [JsonProperty("success")]
        public string? Success { get; init; }
    }
}
