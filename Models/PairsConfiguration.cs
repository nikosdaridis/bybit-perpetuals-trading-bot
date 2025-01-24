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
                    Leverage = 50m,
                    UpdateLeverageOpenPosition = false,
                    InitialQuantity = 0.001m,
                    NumberOfSteps = 5,
                    TakeProfitPercentage = 10m,
                    InitialStepUnrealisedPnlPercentage = 100m,
                    StepUnrealisedPnlMultiplier = 1.4m,
                    StepQuantityMultiplier = 1.2m
                }
            },
            {
                "ETHUSDT", new ()
                {
                    Side = "Buy",
                    Leverage = 50m,
                    UpdateLeverageOpenPosition = false,
                    InitialQuantity = 0.01m,
                    NumberOfSteps = 5,
                    TakeProfitPercentage = 10m,
                    InitialStepUnrealisedPnlPercentage = 100m,
                    StepUnrealisedPnlMultiplier = 1.4m,
                    StepQuantityMultiplier = 1.2m
                }
            }
        };


        internal sealed class PairConfiguration
        {
            public string? Side { get; set; }
            public decimal? Leverage { get; set; }
            public bool UpdateLeverageOpenPosition { get; set; }
            public decimal? InitialQuantity { get; set; }
            public int? NumberOfSteps { get; set; }
            public decimal? TakeProfitPercentage { get; set; }
            public decimal? InitialStepUnrealisedPnlPercentage { get; set; }
            public decimal? StepUnrealisedPnlMultiplier { get; set; }
            public decimal? StepQuantityMultiplier { get; set; }
        }
    }
}
