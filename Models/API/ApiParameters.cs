namespace BybitPerpetualsTradingBot.Models.API
{
    internal sealed class ApiParameters
    {
        internal sealed class EndpointProduct
        {
            public const string Market = "market";
            public const string Order = "order";
            public const string Position = "position";
        }

        internal sealed class EndpointModule
        {
            public const string InstrumentsInfo = "instruments-info";
            public const string Tickers = "tickers";
            public const string List = "list";
            public const string RealTime = "realtime";
            public const string SetLeverage = "set-leverage";
            public const string Create = "create";
            public const string CreateBatch = "create-batch";
            public const string Amend = "amend";
            public const string CancelAll = "cancel-all";
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

        internal sealed class OpenOnly
        {
            public const int True = 0;
            public const int False = 1;
        }
    }
}
