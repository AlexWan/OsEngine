using System;
using System.Globalization;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using OsEngine.Entity;
using OsEngine.Logging;
using System.Net;
using System.IO;
using OsEngine.Market.Servers.Entity;


namespace OsEngine.Market.Servers.FTX.FtxApi
{
    public class FtxRestClient
    {
        private const string Url = "https://ftx.com/";

        private readonly ClientApiKeys _client;

        private HttpClient _httpClient;

        private  HMACSHA256 _hashMaker;

        private long _nonce;

        public FtxRestClient(ClientApiKeys client)
        {
            _client = client;

            _httpClient = new HttpClient
            {
                BaseAddress = new Uri(Url),
                Timeout = TimeSpan.FromSeconds(30)
            };

            _hashMaker = new HMACSHA256(Encoding.UTF8.GetBytes(_client.ApiSecret));
        }

        public void Dispose()
        {
            if(_httpClient != null)
            {
                _httpClient.Dispose();
                _httpClient = null;
            }
            if(_hashMaker != null)
            {
                _hashMaker.Dispose();
                _hashMaker = null;
            }
        }

        public async Task<JToken> GetExpiredFuturesAsync()
        {
            try
            {
                string resultString = $"api/expired_futures";

                string result = await CallAsync(HttpMethod.Get, resultString);

                if (result != null)
                {
                    return ParseResponce(result);
                }
            }
            catch (Exception error)
            {
                SendLogMessage(error.ToString(), LogMessageType.Error);
            }

            return null;
        }

        #region Markets

        public async Task<JToken> GetMarketsAsync()
        {
            try
            {
                string resultString = $"api/markets";

                string result = await CallAsync(HttpMethod.Get, resultString);

                if(result != null)
                {
                    return ParseResponce(result);
                }
            }
            catch (Exception error)
            {
                SendLogMessage(error.ToString(), LogMessageType.Error);
            }

            return null;
        }

        public async Task<JToken> GetMarketTradesAsync(string marketName, int limit, DateTime start, DateTime end)
        {
            try
            {
                string resultString = 
                    $"api/markets/{marketName}/trades?limit={limit}&start_time={Util.Util.GetSecondsFromEpochStart(start)}&end_time={Util.Util.GetSecondsFromEpochStart(end)}";

                string result = await CallAsync(HttpMethod.Get, resultString);

                if (result != null)
                {
                    return ParseResponce(result);
                }
            }
            catch (Exception error)
            {
                SendLogMessage(error.ToString(), LogMessageType.Error);
            }

            return null;
        }

        public async Task<JToken> GetHistoricalPricesAsync(string marketName, int resolution, int limit, DateTime start, DateTime end)
        {
            try
            {
                string resultString 
                    = $"api/markets/{marketName}/candles?resolution={resolution}&limit={limit}&start_time={Util.Util.GetSecondsFromEpochStart(start)}&end_time={Util.Util.GetSecondsFromEpochStart(end)}";

                string result = await CallAsync(HttpMethod.Get, resultString);

                if (result != null)
                {
                    return ParseResponce(result);
                }
            }
            catch (Exception error)
            {
                SendLogMessage(error.ToString(), LogMessageType.Error);
            }

            return null;
        }
        #endregion

        #region Account

        public async Task<JToken> GetAccountInfoAsync()
        {
            try
            {
                string resultString = $"api/account";

                string sign = GenerateSignature(HttpMethod.Get, "/api/account", "");

                string result = await CallAsyncSign(HttpMethod.Get, resultString, sign);

                if (result != null)
                {
                    return ParseResponce(result);
                }
            }
            catch (Exception error)
            {
                SendLogMessage(error.ToString(), LogMessageType.Error);
            }

            return null;
        }

        #endregion

        #region Orders

        public async Task<JToken> PlaceOrderAsync(string instrument, Side side, decimal? price, OrderPriceType orderType, decimal amount, bool reduceOnly = true)
        {
            try
            {
                string path = $"api/orders";

                string body =
                    $"{{\"market\": \"{instrument}\"," +
                    $"\"side\": \"{side.ToString().ToLower()}\"," +
                    $"\"price\": {price?.ToString(CultureInfo.InvariantCulture) ?? "null" }," +
                    $"\"type\": \"{orderType.ToString().ToLower()}\"," +
                    $"\"size\": {amount.ToString(CultureInfo.InvariantCulture)}," +
                    $"\"reduceOnly\": {reduceOnly.ToString().ToLower()}}}";

                string sign = GenerateSignature(HttpMethod.Post, "/api/orders", body);
                string result = await CallAsyncSign(HttpMethod.Post, path, sign, body);

                if (result != null)
                {
                    return ParseResponce(result);
                }
            }
            catch (Exception error)
            {
                SendLogMessage(error.ToString(), LogMessageType.Error);
            }

            return null;
        }

        public async Task<JToken> GetOpenOrdersAsync(string instrument)
        {
            try
            {
                string path = $"api/orders?market={instrument}";

                string sign = GenerateSignature(HttpMethod.Get, $"/api/orders?market={instrument}", "");

                string result = await CallAsyncSign(HttpMethod.Get, path, sign);

                if (result != null)
                {
                    return ParseResponce(result);
                }
            }
            catch (Exception error)
            {
                SendLogMessage(error.ToString(), LogMessageType.Error);
            }

            return null;
        }

        public async Task<JToken> GetOrderStatusAsync(string id)
        {
            try
            {
                string resultString = $"api/orders/{id}";

                string sign = GenerateSignature(HttpMethod.Get, $"/api/orders/{id}", "");

                string result = await CallAsyncSign(HttpMethod.Get, resultString, sign);

                if (result != null)
                {
                    return ParseResponce(result);
                }
            }
            catch (Exception error)
            {
                SendLogMessage(error.ToString(), LogMessageType.Error);
            }

            return null;
        }

        public async Task<JToken> GetOrderStatusByClientIdAsync(string clientOrderId)
        {
            try
            {
                string resultString = $"api/orders/by_client_id/{clientOrderId}";

                string sign = GenerateSignature(HttpMethod.Get, $"/api/orders/by_client_id/{clientOrderId}", "");

                string result = await CallAsyncSign(HttpMethod.Get, resultString, sign);

                if (result != null)
                {
                    return ParseResponce(result);
                }
            }
            catch (Exception error)
            {
                SendLogMessage(error.ToString(), LogMessageType.Error);
            }

            return null;
        }

        public async Task<JToken> CancelOrderAsync(string id)
        {
            try
            {
                string resultString = $"api/orders/{id}";

                string sign = GenerateSignature(HttpMethod.Delete, $"/api/orders/{id}", "");

                string result = await CallAsyncSign(HttpMethod.Delete, resultString, sign);

                if (result != null)
                {
                    return ParseResponce(result);
                }
            }
            catch (Exception error)
            {
                SendLogMessage(error.ToString(), LogMessageType.Error);
            }

            return null;
        }

        public async Task<JToken> CancelOrderByClientIdAsync(string clientOrderId)
        {
            try
            {
                string resultString = $"api/orders/by_client_id/{clientOrderId}";

                string sign = GenerateSignature(HttpMethod.Delete, $"/api/orders/by_client_id/{clientOrderId}", "");

                string result = await CallAsyncSign(HttpMethod.Delete, resultString, sign);

                if (result != null)
                {
                    return ParseResponce(result);
                }
            }
            catch (Exception error)
            {
                SendLogMessage(error.ToString(), LogMessageType.Error);
            }

            return null;
        }

        public async Task<JToken> CancelAllOrdersAsync(string instrument)
        {
            try
            {
                string resultString = $"api/orders";

                string body =
                    $"{{\"market\": \"{instrument}\"}}";

                string sign = GenerateSignature(HttpMethod.Delete, $"/api/orders", body);

                string result = await CallAsyncSign(HttpMethod.Delete, resultString, sign, body);

                if (result != null)
                {
                    return ParseResponce(result);
                }
            }
            catch (Exception error)
            {
                SendLogMessage(error.ToString(), LogMessageType.Error);
            }

            return null;
        }

        #endregion

        #region Util

        private string _rateGateLocker = "rateGateLocker";

        RateGate _rateGate = new RateGate(1, TimeSpan.FromMilliseconds(1000));

        private async Task<string> CallAsync(HttpMethod method, string endpoint, string body = null)
        {
            try
            {
                lock (_rateGateLocker)
                {
                    _rateGate.WaitToProceed();
                }

                HttpRequestMessage request = new HttpRequestMessage(method, endpoint);

                if (body != null)
                {
                    request.Content = new StringContent(body, Encoding.UTF8, "application/json");
                }

                HttpResponseMessage response = await _httpClient.SendAsync(request).ConfigureAwait(false);

                string result = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

                return result;

            }
            catch (Exception error)
            {
                SendLogMessage(error.ToString(), LogMessageType.Error);
            }

            return null;
        }

        private async Task<string> CallAsyncSign(HttpMethod method, string endpoint, string sign, string body = null)
        {
            try
            {
                lock (_rateGateLocker)
                {
                    _rateGate.WaitToProceed();
                }

                HttpRequestMessage request = new HttpRequestMessage(method, endpoint);

                if (body != null)
                {
                    request.Content = new StringContent(body, Encoding.UTF8, "application/json");
                }

                request.Headers.Add("FTX-KEY", _client.ApiKey);
                request.Headers.Add("FTX-SIGN", sign);
                request.Headers.Add("FTX-TS", _nonce.ToString());

                HttpResponseMessage response = await _httpClient.SendAsync(request).ConfigureAwait(false);

                string result = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

                return result;

            }
            catch (Exception error)
            {
                SendLogMessage(error.ToString(), LogMessageType.Error);
            }

            return null;
        }

        private string GenerateSignature(HttpMethod method, string url, string requestBody)
        {
            _nonce = GetNonce();
            string signature = $"{_nonce}{method.ToString().ToUpper()}{url}{requestBody}";
            var hash = _hashMaker.ComputeHash(Encoding.UTF8.GetBytes(signature));
            var hashStringBase64 = BitConverter.ToString(hash).Replace("-", string.Empty);
            return hashStringBase64.ToLower();
        }

        private long GetNonce()
        {
            return Util.Util.GetMillisecondsFromEpochStart();
        }

        private JToken ParseResponce(string responce)
        {
            return JToken.Parse(responce);
        }

        #endregion

        // log messages / сообщения для лога

        /// <summary>
        /// add a new message in the log
        /// добавить в лог новое сообщение
        /// </summary>
        private void SendLogMessage(string message, LogMessageType type)
        {
            if (LogMessageEvent != null)
            {
                LogMessageEvent(message, type);
            }

            if (type == LogMessageType.Error
                && CriticalError != null)
            {
                CriticalError();

                if (LogMessageEvent != null)
                {
                    LogMessageEvent("FTX Rest server error. Try to Reconnect. ", type);
                }

            }
        }

        /// <summary>
        /// outgoing messages for the log
        /// исходящее сообщение для лога
        /// </summary>
        public event Action<string, LogMessageType> LogMessageEvent;

        public event Action CriticalError;
    }
}