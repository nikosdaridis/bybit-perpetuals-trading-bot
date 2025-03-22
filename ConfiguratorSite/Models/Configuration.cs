namespace TradingBotConfigurator.Models;

internal sealed class Configuration(
    string side, decimal? leverage, decimal? initialMargin, ushort? initialPriceTickSizeOffset,
    ushort? initialPriceTickSizeThreshold, decimal? takeProfitPercentage, ushort? numberOfScalingLevels,
    decimal? initialScalingUnrealizedPnL, decimal? initialScalingQuantityMultiplier, decimal? maxScalingUnrealizedPnL,
    decimal? scalingUnrealisedPnlMultiplier, decimal? scalingQuantityMultiplier)
{
    public string Side { get; set; } = side;
    public decimal? Leverage { get; set; } = leverage;
    public decimal? InitialMargin { get; set; } = initialMargin;
    public ushort? InitialPriceTickSizeOffset { get; set; } = initialPriceTickSizeOffset;
    public ushort? InitialPriceTickSizeThreshold { get; set; } = initialPriceTickSizeThreshold;
    public decimal? TakeProfitPercentage { get; set; } = takeProfitPercentage;
    public ushort? NumberOfScalingLevels { get; set; } = numberOfScalingLevels;
    public decimal? InitialScalingUnrealizedPnL { get; set; } = initialScalingUnrealizedPnL;
    public decimal? InitialScalingQuantityMultiplier { get; set; } = initialScalingQuantityMultiplier;
    public decimal? MaxScalingUnrealizedPnL { get; set; } = maxScalingUnrealizedPnL;
    public decimal? ScalingUnrealisedPnlMultiplier { get; set; } = scalingUnrealisedPnlMultiplier;
    public decimal? ScalingQuantityMultiplier { get; set; } = scalingQuantityMultiplier;
}
