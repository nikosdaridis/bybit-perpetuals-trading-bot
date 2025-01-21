using static BybitPerpetualsTradingBot.Models.API.ApiParams;

namespace BybitPerpetualsTradingBot.Models.API
{
    internal sealed class ApiParams
    {
        internal sealed class EndpointProduct
        {
            public const string Position = "position";
            public const string Market = "market";
            public const string Order = "order";
        }

        internal sealed class EndpointModule
        {
            public const string SetLeverage = "set-leverage";
            public const string InstrumentsInfo = "instruments-info";
            public const string List = "list";
            public const string Create = "create";
        }

        internal sealed class Category
        {
            public const string Spot = "spot";
            public const string Linear = "linear";
            public const string Inverse = "inverse";
            public const string Option = "option";
        }

        internal sealed class Side
        {
            public const string Buy = "Buy";
            public const string Sell = "Sell";
        }

        internal sealed class OrderType
        {
            public const string Market = "Market";
            public const string Limit = "Limit";
        }

        internal sealed class TimeInForce
        {
            public const string GoodTillCancel = "GTC";
            public const string ImmediateOrCancel = "IOC";
            public const string FillOrKill = "FOK";
            public const string PostOnly = "PostOnly";
        }
    }
}
