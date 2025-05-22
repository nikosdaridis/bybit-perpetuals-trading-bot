namespace BybitPerpetualsTradingBot.Models
{
    internal sealed class PairsConfiguration
    {
        public string[] ActiveTradingPairs { get; init; } = ["BTCUSDT", "ETHUSDT"];

        public Dictionary<string, PairConfiguration> PairsConfigurations { get; init; } = new()
        {
            {
                "BTCUSDT", new ()
                {
                    Side = "Buy",
                    Leverage = 30,
                    InitialMargin = 10,
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
                    Leverage = 30,
                    InitialMargin = 10,
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
            public string? Side { get; init; }
            public decimal? Leverage { get; init; }
            public decimal? InitialMargin { get; init; }
            public ushort? InitialPriceTickSizeOffset { get; init; }
            public ushort? InitialPriceTickSizeThreshold { get; init; }
            public decimal? TakeProfitPercentage { get; init; }
            public ushort? NumberOfScalingLevels { get; init; }
            public decimal? InitialScalingUnrealizedPnL { get; init; }
            public decimal? InitialScalingQuantityMultiplier { get; init; }
            public decimal? MaxScalingUnrealizedPnL { get; init; }
            public decimal? ScalingUnrealisedPnlMultiplier { get; init; }
            public decimal? ScalingQuantityMultiplier { get; init; }
        }
    }
}
