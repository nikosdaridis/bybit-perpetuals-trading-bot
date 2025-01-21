using Newtonsoft.Json;

namespace BybitPerpetualsTradingBot.Models.API
{
    internal sealed class ApiResponse<TBusinessData>
    {
        [JsonProperty("retCode")]
        public int RetCode { get; set; }

        [JsonProperty("retMsg")]
        public string? RetMsg { get; set; }

        [JsonProperty("result")]
        public TBusinessData? Result { get; set; }

        [JsonProperty("retExtInfo")]
        public object? RetExtInfo { get; set; }

        [JsonProperty("time")]
        public long Time { get; set; }
    }

    internal sealed class GetInstrumentsInfoResult
    {
        [JsonProperty("category")]
        public string? Category { get; set; }

        [JsonProperty("nextPageCursor")]
        public string? NextPageCursor { get; set; }

        [JsonProperty("list")]
        public List<InstrumentList>? List { get; set; }

        internal sealed class InstrumentList
        {
            [JsonProperty("symbol")]
            public string? Symbol { get; set; }

            [JsonProperty("contractType")]
            public string? ContractType { get; set; }

            [JsonProperty("status")]
            public string? Status { get; set; }

            [JsonProperty("baseCoin")]
            public string? BaseCoin { get; set; }

            [JsonProperty("quoteCoin")]
            public string? QuoteCoin { get; set; }

            [JsonProperty("launchTime")]
            public string? LaunchTime { get; set; }

            [JsonProperty("deliveryTime")]
            public string? DeliveryTime { get; set; }

            [JsonProperty("deliveryFeeRate")]
            public string? DeliveryFeeRate { get; set; }

            [JsonProperty("priceScale")]
            public string? PriceScale { get; set; }

            [JsonProperty("leverageFilter")]
            public LeverageFilter? LeverageFilter { get; set; }

            [JsonProperty("priceFilter")]
            public PriceFilter? PriceFilter { get; set; }

            [JsonProperty("lotSizeFilter")]
            public LotSizeFilter? LotSizeFilter { get; set; }

            [JsonProperty("unifiedMarginTrade")]
            public bool UnifiedMarginTrade { get; set; }

            [JsonProperty("fundingInterval")]
            public int FundingInterval { get; set; }

            [JsonProperty("settleCoin")]
            public string? SettleCoin { get; set; }

            [JsonProperty("copyTrading")]
            public string? CopyTrading { get; set; }

            [JsonProperty("upperFundingRate")]
            public string? UpperFundingRate { get; set; }

            [JsonProperty("lowerFundingRate")]
            public string? LowerFundingRate { get; set; }

            [JsonProperty("isPreListing")]
            public bool IsPreListing { get; set; }

            [JsonProperty("preListingInfo")]
            public PreListingInfo? PreListingInfo { get; set; }

            [JsonProperty("riskParameters")]
            public RiskParameters? RiskParameters { get; set; }
        }

        internal sealed class LeverageFilter
        {
            [JsonProperty("minLeverage")]
            public string? MinLeverage { get; set; }

            [JsonProperty("maxLeverage")]
            public string? MaxLeverage { get; set; }

            [JsonProperty("leverageStep")]
            public string? LeverageStep { get; set; }
        }

        internal sealed class PriceFilter
        {
            [JsonProperty("minPrice")]
            public string? MinPrice { get; set; }

            [JsonProperty("maxPrice")]
            public string? MaxPrice { get; set; }

            [JsonProperty("tickSize")]
            public string? TickSize { get; set; }
        }

        internal sealed class LotSizeFilter
        {
            [JsonProperty("minNotionalValue")]
            public string? MinNotionalValue { get; set; }

            [JsonProperty("maxOrderQty")]
            public string? MaxOrderQty { get; set; }

            [JsonProperty("maxMktOrderQty")]
            public string? MaxMktOrderQty { get; set; }

            [JsonProperty("minOrderQty")]
            public string? MinOrderQty { get; set; }

            [JsonProperty("qtyStep")]
            public string? QtyStep { get; set; }

            [JsonProperty("postOnlyMaxOrderQty")]
            public string? PostOnlyMaxOrderQty { get; set; }
        }

        internal sealed class RiskParameters
        {
            [JsonProperty("priceLimitRatioX")]
            public string? PriceLimitRatioX { get; set; }

            [JsonProperty("priceLimitRatioY")]
            public string? PriceLimitRatioY { get; set; }
        }

        internal sealed class PreListingInfo
        {
            [JsonProperty("curAuctionPhase")]
            public string? CurAuctionPhase { get; set; }

            [JsonProperty("phases")]
            public List<Phase>? Phases { get; set; }

            [JsonProperty("auctionFeeInfo")]
            public AuctionFeeInfo? AuctionFeeInfo { get; set; }

        }
        internal sealed class Phase
        {
            [JsonProperty("phase")]
            public string? PhaseName { get; set; }

            [JsonProperty("startTime")]
            public string? StartTime { get; set; }

            [JsonProperty("endTime")]
            public string? EndTime { get; set; }
        }

        internal sealed class AuctionFeeInfo
        {
            [JsonProperty("auctionFeeRate")]
            public string? AuctionFeeRate { get; set; }

            [JsonProperty("takerFeeRate")]
            public string? TakerFeeRate { get; set; }

            [JsonProperty("makerFeeRate")]
            public string? MakerFeeRate { get; set; }
        }
    }

    internal sealed class GetPositionInfoResult
    {
        [JsonProperty("list")]
        public List<GetPositionInfoList>? List { get; set; }

        [JsonProperty("nextPageCursor")]
        public string? NextPageCursor { get; set; }

        [JsonProperty("category")]
        public string? Category { get; set; }

        internal sealed class GetPositionInfoList
        {
            [JsonProperty("positionIdx")]
            public int PositionIdx { get; set; }

            [JsonProperty("riskId")]
            public int RiskId { get; set; }

            [JsonProperty("riskLimitValue")]
            public string? RiskLimitValue { get; set; }

            [JsonProperty("symbol")]
            public string? Symbol { get; set; }

            [JsonProperty("side")]
            public string? Side { get; set; }

            [JsonProperty("size")]
            public string? Size { get; set; }

            [JsonProperty("avgPrice")]
            public string? AvgPrice { get; set; }

            [JsonProperty("positionValue")]
            public string? PositionValue { get; set; }

            [JsonProperty("tradeMode")]
            public int TradeMode { get; set; }

            [JsonProperty("positionStatus")]
            public string? PositionStatus { get; set; }

            [JsonProperty("autoAddMargin")]
            public int AutoAddMargin { get; set; }

            [JsonProperty("adlRankIndicator")]
            public int AdlRankIndicator { get; set; }

            [JsonProperty("leverage")]
            public string? Leverage { get; set; }

            [JsonProperty("positionBalance")]
            public string? PositionBalance { get; set; }

            [JsonProperty("markPrice")]
            public string? MarkPrice { get; set; }

            [JsonProperty("liqPrice")]
            public string? LiqPrice { get; set; }

            [JsonProperty("bustPrice")]
            public string? BustPrice { get; set; }

            [JsonProperty("positionMM")]
            public string? PositionMM { get; set; }

            [JsonProperty("positionIM")]
            public string? PositionIM { get; set; }

            [JsonProperty("tpslMode")]
            public string? TpslMode { get; set; }

            [JsonProperty("takeProfit")]
            public string? TakeProfit { get; set; }

            [JsonProperty("stopLoss")]
            public string? StopLoss { get; set; }

            [JsonProperty("trailingStop")]
            public string? TrailingStop { get; set; }

            [JsonProperty("unrealisedPnl")]
            public string? UnrealisedPnl { get; set; }

            [JsonProperty("curRealisedPnl")]
            public string? CurRealisedPnl { get; set; }

            [JsonProperty("cumRealisedPnl")]
            public string? CumRealisedPnl { get; set; }

            [JsonProperty("seq")]
            public long Seq { get; set; }

            [JsonProperty("isReduceOnly")]
            public bool IsReduceOnly { get; set; }

            [JsonProperty("mmrSysUpdateTime")]
            public string? MmrSysUpdateTime { get; set; }

            [JsonProperty("leverageSysUpdatedTime")]
            public string? LeverageSysUpdatedTime { get; set; }

            [JsonProperty("sessionAvgPrice")]
            public string? SessionAvgPrice { get; set; }

            [JsonProperty("createdTime")]
            public string? CreatedTime { get; set; }

            [JsonProperty("updatedTime")]
            public string? UpdatedTime { get; set; }
        }
    }

    internal sealed class PlaceOrderResult
    {
        [JsonProperty("orderId")]
        public string? OrderId { get; set; }

        [JsonProperty("orderLinkId")]
        public string? OrderLinkId { get; set; }
    }
}
