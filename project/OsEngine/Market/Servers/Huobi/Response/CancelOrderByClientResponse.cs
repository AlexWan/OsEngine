using Newtonsoft.Json;

namespace OsEngine.Market.Servers.Huobi.Response
{
    public class CancelOrderByClientResponse
    {
        /// <summary>
        /// Response status
        /// </summary>
        public string status;

        /// <summary>
        /// Cancellation status code
        /// </summary>
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public int data;

        /// <summary>
        /// Error code
        /// </summary>
        [JsonProperty("err-code", NullValueHandling = NullValueHandling.Ignore)]
        public string errorCode;

        /// <summary>
        /// Error message
        /// </summary>
        [JsonProperty("err-msg", NullValueHandling = NullValueHandling.Ignore)]
        public string errorMessage;
    }
}
