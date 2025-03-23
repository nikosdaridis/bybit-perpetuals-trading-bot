namespace TradingBotConfigurator.Models
{
    internal sealed class ScalingLevel
    {
        public ushort Level { get; set; }
        public decimal AveragePrice { get; set; }
        public decimal PnL { get; set; }
        public decimal Price { get; set; }
        public decimal Quantity { get; set; }
        public decimal Margin { get; set; }
        public decimal TotalQuantity { get; set; }
        public decimal TotalMargin { get; set; }
    }
}
