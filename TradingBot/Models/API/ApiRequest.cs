namespace BybitPerpetualsTradingBot.Models.API
{
    internal sealed class ApiRequest<TBusinessData>
    {
        public string? Category { get; set; }

        public List<TBusinessData>? Request { get; set; }
    }

    internal sealed class BatchOrderRequest
    {
        public string? Symbol { get; set; }

        public int? IsLeverage { get; set; }

        public string? Side { get; set; }

        public string? OrderType { get; set; }

        public string? Qty { get; set; }

        public string? MarketUnit { get; set; }

        public string? Price { get; set; }

        public int? TriggerDirection { get; set; }

        public string? OrderFilter { get; set; }

        public string? TriggerPrice { get; set; }

        public string? TriggerBy { get; set; }

        public string? OrderIv { get; set; }

        public string? TimeInForce { get; set; }

        public int? PositionIdx { get; set; }

        public string? OrderLinkId { get; set; }

        public string? TakeProfit { get; set; }

        public string? StopLoss { get; set; }

        public string? TpTriggerBy { get; set; }

        public string? SlTriggerBy { get; set; }

        public bool? ReduceOnly { get; set; }

        public bool? CloseOnTrigger { get; set; }

        public string? SmpType { get; set; }

        public bool? Mmp { get; set; }

        public string? TpslMode { get; set; }

        public string? TpLimitPrice { get; set; }

        public string? SlLimitPrice { get; set; }

        public string? TpOrderType { get; set; }

        public string? SlOrderType { get; set; }
    }
}
