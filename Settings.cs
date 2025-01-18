namespace CryptoFuturesTradingBot
{
    internal sealed class Settings
    {
        public string APIKey { get; set; } = string.Empty;
        public string APISecret { get; set; } = string.Empty;
        public string Endpoint { get; set; } = "https://api-demo.bybit.com/v5/{product}/{module}";
        public string RecvWindow { get; set; } = "5000";
    }
}
