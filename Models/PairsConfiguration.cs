namespace BybitPerpetualsTradingBot.Models
{
    internal sealed class PairsConfiguration
    {
        public string[] ActiveTradingPairs { get; set; } = ["Add", "Pairs", "Here", "And", "Configurations", "Below"];

        public Dictionary<string, PairConfiguration> PairsConfigurations { get; set; } = new()
        {
            {
                "BTCUSDT", new ()
                {
                    Side = "Buy",
                    Leverage = 20m,
                    InitialQuantity = 0.001m,
                    TakeProfitPercentage = 10m,
                    NumberOfScalingLevels = 5,
                    InitialScalingUnrealizedPnL = 100m,
                    ScalingUnrealisedPnlMultiplier = 1.4m,
                    ScalingQuantityMultiplier = 1.2m
                }
            },
            {
                "ETHUSDT", new ()
                {
                    Side = "Buy",
                    Leverage = 20m,
                    InitialQuantity = 0.01m,
                    TakeProfitPercentage = 10m,
                    NumberOfScalingLevels = 5,
                    InitialScalingUnrealizedPnL = 100m,
                    ScalingUnrealisedPnlMultiplier = 1.4m,
                    ScalingQuantityMultiplier = 1.2m
                }
            }
        };


        internal sealed class PairConfiguration
        {
            public string? Side { get; set; }
            public decimal? Leverage { get; set; }
            public decimal? InitialQuantity { get; set; }
            public decimal? TakeProfitPercentage { get; set; }
            public int? NumberOfScalingLevels { get; set; }
            public decimal? InitialScalingUnrealizedPnL { get; set; }
            public decimal? ScalingUnrealisedPnlMultiplier { get; set; }
            public decimal? ScalingQuantityMultiplier { get; set; }
        }
    }
}
