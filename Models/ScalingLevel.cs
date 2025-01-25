namespace BybitPerpetualsTradingBot.Models
{
    internal sealed class ScalingLevel
    {
        public int Level { get; set; }
        public decimal AveragePrice { get; set; }
        public decimal UnrealizedPnLPercentage { get; set; }
        public decimal Quantity { get; set; }
        public decimal Margin { get; set; }
    }
}
