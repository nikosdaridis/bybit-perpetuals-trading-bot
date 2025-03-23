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
                    Leverage = 20,
                    InitialMargin = 5,
                    InitialPriceTickSizeOffset = 2,
                    InitialPriceTickSizeThreshold = 5,
                    TakeProfitPercentage = 50,
                    NumberOfScalingLevels = 10,
                    InitialScalingUnrealizedPnL = 100,
                    InitialScalingQuantityMultiplier = 0.5m,
                    MaxScalingUnrealizedPnL = 700,
                    ScalingUnrealisedPnlMultiplier = 1.3m,
                    ScalingQuantityMultiplier = 1.2m
                }
            },
            {
                "ETHUSDT", new ()
                {
                    Side = "Buy",
                    Leverage = 20,
                    InitialMargin = 5,
                    InitialPriceTickSizeOffset = 2,
                    InitialPriceTickSizeThreshold = 5,
                    TakeProfitPercentage = 50,
                    NumberOfScalingLevels = 10,
                    InitialScalingUnrealizedPnL = 100,
                    InitialScalingQuantityMultiplier = 0.5m,
                    MaxScalingUnrealizedPnL = 700,
                    ScalingUnrealisedPnlMultiplier = 1.3m,
                    ScalingQuantityMultiplier = 1.2m
                }
            }
        };

        internal sealed class PairConfiguration
        {
            public string? Side { get; set; }
            public decimal? Leverage { get; set; }
            public decimal? InitialMargin { get; set; }
            public ushort? InitialPriceTickSizeOffset { get; set; }
            public ushort? InitialPriceTickSizeThreshold { get; set; }
            public decimal? TakeProfitPercentage { get; set; }
            public ushort? NumberOfScalingLevels { get; set; }
            public decimal? InitialScalingUnrealizedPnL { get; set; }
            public decimal? InitialScalingQuantityMultiplier { get; set; }
            public decimal? MaxScalingUnrealizedPnL { get; set; }
            public decimal? ScalingUnrealisedPnlMultiplier { get; set; }
            public decimal? ScalingQuantityMultiplier { get; set; }
        }
    }
}
