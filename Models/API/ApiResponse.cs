using Newtonsoft.Json;

namespace BybitPerpetualsTradingBot.Models.API
{
    internal sealed class ApiResponse<TBusinessData>
    {
        [JsonProperty("retCode")]
        public int RetCode { get; set; }

        [JsonProperty("retMsg")]
        public string? RetMsg { get; set; }

        [JsonProperty("result")]
        public TBusinessData? Result { get; set; }

        [JsonProperty("retExtInfo")]
        public object? RetExtInfo { get; set; }

        [JsonProperty("time")]
        public long Time { get; set; }
    }
}
