using System.Runtime.InteropServices;

namespace BybitPerpetualsTradingBot.Models
{
    internal sealed class Settings
    {
        public string APIKey { get; init; } = string.Empty;
        public string APISecret { get; init; } = string.Empty;
        public ushort APIRateLimit { get; init; } = 50;
        public string Endpoint { get; init; } = "https://api.bybit.com/v5/{product}/{module}";
        public string RecvWindow { get; init; } = "20000";
        public ushort RunningTasks { get; init; } = 10;
        public SerilogConfig Logs { get; init; } = new();

        internal sealed class SerilogConfig
        {
            public SerilogPathsConfig Paths { get; init; } = new();
            public string MinimumLevel { get; init; } = "Information";
            public long FileSizeLimitBytes { get; init; } = 209715200;
            public string RollingInterval { get; init; } = "Day";
            public bool RollOnFileSizeLimit { get; init; } = true;
            public bool Shared { get; init; } = true;
            public int FlushToDiskIntervalSeconds { get; init; } = 1;
            public int? RetainedFileCountLimit { get; init; } = null;
            public string FormatProviderCulture { get; init; } = "en-US";

            internal sealed class SerilogPathsConfig
            {
                public string Windows { get; init; } = @"C:\logs\bybit-perpetuals-trading-bot\.log";
                public string Linux { get; init; } = @"/var/log/bybit-perpetuals-trading-bot/.log";

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
