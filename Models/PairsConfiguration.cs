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
                    InitialOrderTickSize = 2,
                    InitialOrderTickSizeThreshold = 5,
                    TakeProfitPercentage = 50,
                    NumberOfScalingLevels = 15,
                    InitialScalingUnrealizedPnL = 100,
                    MaxScalingUnrealizedPnL = 2000,
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
                    InitialOrderTickSize = 2,
                    InitialOrderTickSizeThreshold = 5,
                    TakeProfitPercentage = 50,
                    NumberOfScalingLevels = 15,
                    InitialScalingUnrealizedPnL = 100,
                    MaxScalingUnrealizedPnL = 2000,
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
            public ushort? InitialOrderTickSize { get; set; }
            public ushort? InitialOrderTickSizeThreshold { get; set; }
            public decimal? TakeProfitPercentage { get; set; }
            public ushort? NumberOfScalingLevels { get; set; }
            public decimal? InitialScalingUnrealizedPnL { get; set; }
            public decimal? MaxScalingUnrealizedPnL { get; set; }
            public decimal? ScalingUnrealisedPnlMultiplier { get; set; }
            public decimal? ScalingQuantityMultiplier { get; set; }
        }
    }
}
