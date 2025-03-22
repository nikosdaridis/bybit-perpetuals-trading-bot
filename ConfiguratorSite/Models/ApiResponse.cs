using Newtonsoft.Json;

namespace TradingBotConfigurator.Models
{
    internal sealed class ApiResponse<TBusinessData, TRetExtInfo>
    {
        [JsonProperty("retCode")]
        public int RetCode { get; set; }

        [JsonProperty("retMsg")]
        public string? RetMsg { get; set; }

        [JsonProperty("result")]
        public TBusinessData? Result { get; set; }

        [JsonProperty("retExtInfo")]
        public TRetExtInfo? RetExtInfo { get; set; }

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

    internal sealed class GetTickersResult
    {
        [JsonProperty("category")]
        public string? Category { get; set; }

        [JsonProperty("list")]
        public List<TickersList>? List { get; set; }

        internal sealed class TickersList
        {
            [JsonProperty("symbol")]
            public string? Symbol { get; set; }

            [JsonProperty("lastPrice")]
            public string? LastPrice { get; set; }

            [JsonProperty("indexPrice")]
            public string? IndexPrice { get; set; }

            [JsonProperty("markPrice")]
            public string? MarkPrice { get; set; }

            [JsonProperty("prevPrice24h")]
            public string? PrevPrice24h { get; set; }

            [JsonProperty("price24hPcnt")]
            public string? Price24hPcnt { get; set; }

            [JsonProperty("highPrice24h")]
            public string? HighPrice24h { get; set; }

            [JsonProperty("lowPrice24h")]
            public string? LowPrice24h { get; set; }

            [JsonProperty("prevPrice1h")]
            public string? PrevPrice1h { get; set; }

            [JsonProperty("openInterest")]
            public string? OpenInterest { get; set; }

            [JsonProperty("openInterestValue")]
            public string? OpenInterestValue { get; set; }

            [JsonProperty("turnover24h")]
            public string? Turnover24h { get; set; }

            [JsonProperty("volume24h")]
            public string? Volume24h { get; set; }

            [JsonProperty("fundingRate")]
            public string? FundingRate { get; set; }

            [JsonProperty("nextFundingTime")]
            public string? NextFundingTime { get; set; }

            [JsonProperty("predictedDeliveryPrice")]
            public string? PredictedDeliveryPrice { get; set; }

            [JsonProperty("basisRate")]
            public string? BasisRate { get; set; }

            [JsonProperty("basis")]
            public string? Basis { get; set; }

            [JsonProperty("deliveryFeeRate")]
            public string? DeliveryFeeRate { get; set; }

            [JsonProperty("deliveryTime")]
            public string? DeliveryTime { get; set; }

            [JsonProperty("ask1Size")]
            public string? Ask1Size { get; set; }

            [JsonProperty("bid1Price")]
            public string? Bid1Price { get; set; }

            [JsonProperty("ask1Price")]
            public string? Ask1Price { get; set; }

            [JsonProperty("bid1Size")]
            public string? Bid1Size { get; set; }

            [JsonProperty("preOpenPrice")]
            public string? PreOpenPrice { get; set; }

            [JsonProperty("preQty")]
            public string? PreQty { get; set; }

            [JsonProperty("curPreListingPhase")]
            public string? CurPreListingPhase { get; set; }
        }
    }
}
