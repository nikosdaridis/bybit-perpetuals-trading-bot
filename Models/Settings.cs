using System.Runtime.InteropServices;

namespace BybitPerpetualsTradingBot.Models
{
    internal sealed class Settings
    {
        public string APIKey { get; set; } = string.Empty;
        public string APISecret { get; set; } = string.Empty;
        public ushort APIRateLimit { get; set; } = 10;
        public string Endpoint { get; set; } = "https://api.bybit.com/v5/{product}/{module}";
        public string RecvWindow { get; set; } = "20000";
        public SerilogConfig Logs { get; set; } = new();

        internal sealed class SerilogConfig
        {
            public SerilogPathsConfig Paths { get; set; } = new();
            public string MinimumLevel { get; set; } = "Information";
            public long FileSizeLimitBytes { get; set; } = 209715200;
            public string RollingInterval { get; set; } = "Day";
            public bool RollOnFileSizeLimit { get; set; } = true;
            public bool Shared { get; set; } = true;
            public int FlushToDiskIntervalSeconds { get; set; } = 1;
            public int? RetainedFileCountLimit { get; set; } = null;
            public string FormatProviderCulture { get; set; } = "en-US";

            internal sealed class SerilogPathsConfig
            {
                public string Windows { get; set; } = @"C:\logs\bybit-perpetuals-trading-bot\.log";
                public string Linux { get; set; } = @"/var/log/bybit-perpetuals-trading-bot/.log";

                internal string GetPath(OSPlatform osPlatform) =>
                    osPlatform.ToString() switch
                    {
                        "WINDOWS" => Windows,
                        "LINUX" => Linux,
                        _ => throw new PlatformNotSupportedException($"No log path configured for the platform {osPlatform}.")
                    };
            }
        }
    }
}
