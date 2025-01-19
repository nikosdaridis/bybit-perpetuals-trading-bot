using Microsoft.Extensions.DependencyInjection;
using Serilog;
using Serilog.Events;
using System.Globalization;
using System.Runtime.InteropServices;

namespace CryptoFuturesTradingBot
{
    internal static class ConfigureServices
    {
        private static readonly OSPlatform[] _supportedOSPlatforms = [OSPlatform.Windows, OSPlatform.Linux];

        /// <summary>
        /// Adds services
        /// </summary>
        public static void AddServices(ServiceCollection serviceCollection, Settings? settings)
        {
            serviceCollection.AddSingleton<HttpClient>();
            serviceCollection.AddSingleton<BaseHttpClient>();
            serviceCollection.AddSingleton<TradingBot>();

            ConfigureSerilog(settings);
            serviceCollection.AddLogging(loggingBuilder => loggingBuilder.AddSerilog(dispose: true));
        }

        /// <summary>
        /// Configures Serilog
        /// </summary>
        private static void ConfigureSerilog(Settings? settings)
        {
            ArgumentNullException.ThrowIfNull(settings);

            Enum.TryParse(settings.Logs.MinimumLevel, true, out LogEventLevel minimumLevel);
            Enum.TryParse(settings.Logs.RollingInterval, true, out RollingInterval rollingInterval);

            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Is(minimumLevel)
                .WriteTo.File(
                    path: settings.Logs.Paths.GetPath(_supportedOSPlatforms.FirstOrDefault(RuntimeInformation.IsOSPlatform)),
                    fileSizeLimitBytes: settings.Logs.FileSizeLimitBytes,
                    rollingInterval: rollingInterval,
                    rollOnFileSizeLimit: settings.Logs.RollOnFileSizeLimit,
                    shared: settings.Logs.Shared,
                    flushToDiskInterval: TimeSpan.FromSeconds(settings.Logs.FlushToDiskIntervalSeconds),
                    retainedFileCountLimit: settings.Logs.RetainedFileCountLimit,
                    formatProvider: new CultureInfo(settings.Logs.FormatProviderCulture)
                )
                .CreateLogger();
        }
    }
}
