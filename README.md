# Bybit Perpetuals Trading Bot

A **Crypto perpetual futures trading bot** that connects to **Bybit API v5** for automated trading. The bot simply follows the configuration you provide. **It does not perform any risk management or analysis**.

It is designed to operate in **Cross Margin Mode** with **One-Way Position Mode** utilizing a **DCA strategy** by placing scaling orders.

[Configurator Website](https://bybit-perpetuals-trading-bot.pages.dev/)

---

## DISCLAIMER

This software is for **educational purposes only**. **DO NOT** use it with a real trading account or real funds. Doing so may result in significant financial loss.

By using this software, you acknowledge and agree that:

- This software is provided "as is" without any warranty of accuracy, reliability, or performance.
- The authors and developers are not responsible for any financial losses, damages, or other consequences.
- **USE THE SOFTWARE AT YOUR OWN RISK.**
- If you choose to use it, **you accept full responsibility** for your trading results.

## `settings.json`

This file contains settings for **API connection, execution limits, and logging**.

### 🔹 API Configuration

| Parameter          | Description                                                                           |
| ------------------ | ------------------------------------------------------------------------------------- |
| **`APIKey`**       | Your Bybit API v5 key (System-generated HMAC)                                         |
| **`APISecret`**    | Your Bybit API v5 secret (System-generated HMAC)                                      |
| **`APIRateLimit`** | Maximum number of API requests per second                                             |
| **`Endpoint`**     | URI for Bybit API v5 requests, formatted with `{product}` and `{module}` placeholders |
| **`RecvWindow`**   | The allowed time window (in milliseconds) for API request validation                  |
| **`RunningTasks`** | Maximum number of concurrent trading tasks (pairs)                                    |

### 🔹 Logging Configuration

| Parameter                             | Description                                                     |
| ------------------------------------- | --------------------------------------------------------------- |
| **`Logs.Paths.Windows`**              | Log file path for Windows                                       |
| **`Logs.Paths.Linux`**                | Log file path for Linux                                         |
| **`Logs.MinimumLevel`**               | Minimum log level                                               |
| **`Logs.FileSizeLimitBytes`**         | Maximum log file size in bytes before rolling                   |
| **`Logs.RollingInterval`**            | How often logs are rolled                                       |
| **`Logs.RollOnFileSizeLimit`**        | Whether to create a new log file once the size limit is reached |
| **`Logs.Shared`**                     | Whether log files are shared across multiple instances          |
| **`Logs.FlushToDiskIntervalSeconds`** | How frequently logs are written to disk                         |
| **`Logs.RetainedFileCountLimit`**     | Number of old log files to keep                                 |
| **`Logs.FormatProviderCulture`**      | Culture settings for log formatting                             |

---

## `pairs.json`

This file contains **active trading pairs** and their **configurations**

### 🔹 Pairs Configurations

Each pair has a **customizable strategy** defined by the following parameters:

| Parameter                              | Description                                                       |
| -------------------------------------- | ----------------------------------------------------------------- |
| **`Side`**                             | **Buy** or **Sell**, indicating the direction of the trade        |
| **`Leverage`**                         | Leverage multiplier                                               |
| **`InitialMargin`**                    | Margin for the initial position                                   |
| **`InitialPriceTickSizeOffset`**       | Offset from the current price when placing initial position order |
| **`InitialPriceTickSizeThreshold`**    | Threshold from the current price to cancel initial position order |
| **`TakeProfitPercentage`**             | Profit percentage for the take profit order                       |
| **`NumberOfScalingLevels`**            | Number of scaling levels                                          |
| **`InitialScalingUnrealizedPnL`**      | Unrealized loss percentage for first scaling level                |
| **`InitialScalingQuantityMultiplier`** | Multiplier for first scaling level quantity                       |
| **`MaxScalingUnrealizedPnL`**          | Maximum unrealized loss percentage for scaling levels             |
| **`ScalingUnrealisedPnlMultiplier`**   | Unrealized loss percentage multiplier for scaling levels          |
| **`ScalingQuantityMultiplier`**        | Quantity multiplier for scaling levels                            |
