using static BybitPerpetualsTradingBot.Models.API.GetOpenAndClosedOrdersResult;
using static BybitPerpetualsTradingBot.Models.API.GetPositionInfoResult;
using static BybitPerpetualsTradingBot.Models.PairsConfiguration;

namespace BybitPerpetualsTradingBot.Models
{
    internal sealed class ActiveTradingPair
    {
        public PairConfiguration Configuration { get; set; } = new();
        public GetPositionInfoList Position { get; set; } = new();
        public List<GetOpenAndClosedOrdersList> Orders { get; set; } = [];
    }
}
