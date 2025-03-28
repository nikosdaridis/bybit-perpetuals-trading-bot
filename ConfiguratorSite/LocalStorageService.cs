using Blazored.LocalStorage;
using TradingBotConfigurator.Models;

namespace TradingBotConfigurator;

internal sealed class LocalStorageService(ILocalStorageService localStorageService)
{
    internal sealed class StorageKeys
    {
        public const string Configurations = "Configurations";
    }

    /// <summary>
    /// Gets configurations from local storage
    /// </summary>
    public async Task<Dictionary<string, Configuration>> GetConfigurationsAsync()
    {
        try
        {
            return await localStorageService.GetItemAsync<Dictionary<string, Configuration>>(StorageKeys.Configurations) ?? [];
        }
        catch
        {
            await localStorageService.SetItemAsync(StorageKeys.Configurations, new Dictionary<string, Configuration>());
            return [];
        }
    }

    /// <summary>
    /// Gets configuration for a pair from local storage
    /// </summary>
    public async Task<Configuration?> GetConfigurationAsync(string key) =>
        (await GetConfigurationsAsync()).GetValueOrDefault(key);

    /// <summary>
    /// Sets configuration for a pair in local storage
    /// </summary>
    public async Task SetConfigurationAsync(string key, Configuration configuration)
    {
        Dictionary<string, Configuration> configurations = await GetConfigurationsAsync();
        configurations[key] = configuration;
        await localStorageService.SetItemAsync(StorageKeys.Configurations, configurations);
    }
}
