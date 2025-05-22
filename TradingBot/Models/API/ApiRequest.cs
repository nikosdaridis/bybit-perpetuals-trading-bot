namespace BybitPerpetualsTradingBot.Models.API
{
    internal sealed class ApiRequest<TBusinessData>
    {
        public string? Category { get; init; }

        public List<TBusinessData>? Request { get; init; }
    }

    internal sealed class BatchOrderRequest
    {
        public string? Symbol { get; init; }

        public int? IsLeverage { get; init; }

        public string? Side { get; init; }

        public string? OrderType { get; init; }

        public string? Qty { get; init; }

        public string? MarketUnit { get; init; }

        public string? Price { get; init; }

        public int? TriggerDirection { get; init; }

        public string? OrderFilter { get; init; }

        public string? TriggerPrice { get; init; }

        public string? TriggerBy { get; init; }

        public string? OrderIv { get; init; }

        public string? TimeInForce { get; init; }

        public int? PositionIdx { get; init; }

        public string? OrderLinkId { get; init; }

        public string? TakeProfit { get; init; }

        public string? StopLoss { get; init; }

        public string? TpTriggerBy { get; init; }

        public string? SlTriggerBy { get; init; }

        public bool? ReduceOnly { get; init; }

        public bool? CloseOnTrigger { get; init; }

        public string? SmpType { get; init; }

        public bool? Mmp { get; init; }

        public string? TpslMode { get; init; }

        public string? TpLimitPrice { get; init; }

        public string? SlLimitPrice { get; init; }

        public string? TpOrderType { get; init; }

        public string? SlOrderType { get; init; }
    }
}
