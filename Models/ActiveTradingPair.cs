using static BybitPerpetualsTradingBot.Models.API.GetPositionInfoResult;
using static BybitPerpetualsTradingBot.Models.PairsConfiguration;

namespace BybitPerpetualsTradingBot.Models
{
    internal sealed class ActiveTradingPair
    {
        public PairConfiguration Configuration { get; set; } = new();
        public GetPositionInfoList Position { get; set; } = new();
        public decimal CalculatedInitialQuantity { get; set; }  
        public List<ScalingLevel> ScalingLevels { get; set; } = [];
        public List<ScalingLevel> ScalingLevelsToBePlaced { get; set; } = [];
    }
}
