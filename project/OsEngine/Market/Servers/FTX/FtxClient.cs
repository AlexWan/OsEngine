using System;
using System.Globalization;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using OsEngine.Entity;
using OsEngine.Market.Servers.Entity;
using System.Collections.Generic;
using System.Net;
using System.IO;

namespace OsEngine.Market.Servers.FTX.FtxApi
{
    public class FtxRestApi
    {
        private string Url = "https://ftx.com/";

        private readonly Client _client;

        private readonly HttpClient _httpClient;

        private readonly HMACSHA256 _hashMaker;

        private long _nonce;

        public FtxRestApi(Client client)
        {
            _client = client;
            _httpClient = new HttpClient
            {
                BaseAddress = new Uri(Url),
                Timeout = TimeSpan.FromSeconds(30)
            };

            _hashMaker = new HMACSHA256(Encoding.UTF8.GetBytes(_client.ApiSecret));
        }

        #region Futures

        public async Task<JToken> GetExpiredFuturesAsync()
        {
            var resultString = $"api/expired_futures";

            var result = await CallAsync(HttpMethod.Get, resultString);

            return ParseResponce(result);
        }

        #endregion

        #region Markets

        public async Task<JToken> GetMarketsAsync()
        {
            var resultString = $"api/markets";

            var result = await CallAsync(HttpMethod.Get, resultString);

            return ParseResponce(result);
        }

        public async Task<JToken> GetMarketTradesAsync(string marketName, int limit, DateTime start, DateTime end)
        {
            var resultString = $"api/markets/{marketName}/trades?limit={limit}&start_time={Util.Util.GetSecondsFromEpochStart(start)}&end_time={Util.Util.GetSecondsFromEpochStart(end)}";

            var result = await CallAsync(HttpMethod.Get, resultString);

            return ParseResponce(result);
        }

        public async Task<JToken> GetHistoricalPricesAsync(string marketName, int resolution, int limit, DateTime start, DateTime end)
        {
            var resultString = $"api/markets/{marketName}/candles?resolution={resolution}&limit={limit}&start_time={Util.Util.GetSecondsFromEpochStart(start)}&end_time={Util.Util.GetSecondsFromEpochStart(end)}";

            var result = await CallAsync(HttpMethod.Get, resultString);

            return ParseResponce(result);
        }

        #endregion

        #region Account

        public async Task<JToken> GetAccountInfoAsync()
        {
            var resultString = $"api/account";
            var sign = GenerateSignature(HttpMethod.Get, "/api/account", "");

            var result = await CallAsyncSign(HttpMethod.Get, resultString, sign);

            return ParseResponce(result);
        }

        public async Task<JToken> GetPositionsAsync()
        {
            var resultString = $"api/positions";
            var sign = GenerateSignature(HttpMethod.Get, "/api/positions", "");

            var result = await CallAsyncSign(HttpMethod.Get, resultString, sign);

            return ParseResponce(result);
        }

        #endregion

        #region Orders

        public async Task<JToken> PlaceOrderAsync(string instrument, Side side, decimal? price, OrderPriceType orderType, decimal amount, bool reduceOnly = true)
        {
            var path = $"api/orders";

            var body =
                $"{{\"market\": \"{instrument}\"," +
                $"\"side\": \"{side.ToString().ToLower()}\"," +
                $"\"price\": {price?.ToString(CultureInfo.InvariantCulture) ?? "null" }," +
                $"\"type\": \"{orderType.ToString().ToLower()}\"," +
                $"\"size\": {amount.ToString(CultureInfo.InvariantCulture)}," +
                $"\"reduceOnly\": {reduceOnly.ToString().ToLower()}}}";

            var sign = GenerateSignature(HttpMethod.Post, "/api/orders", body);
            var result = await CallAsyncSign(HttpMethod.Post, path, sign, body);

            return ParseResponce(result);
        }

        public async Task<JToken> CancelOrderAsync(string id)
        {
            var resultString = $"api/orders/{id}";

            var sign = GenerateSignature(HttpMethod.Delete, $"/api/orders/{id}", "");

            var result = await CallAsyncSign(HttpMethod.Delete, resultString, sign);

            return ParseResponce(result);
        }

        public async Task<JToken> CancelAllOrdersAsync(string instrument)
        {
            var resultString = $"api/orders";

            var body =
                $"{{\"market\": \"{instrument}\"}}";

            var sign = GenerateSignature(HttpMethod.Delete, $"/api/orders", body);

            var result = await CallAsyncSign(HttpMethod.Delete, resultString, sign, body);

            return ParseResponce(result);
        }

        #endregion

        #region Util

        private string _queryLocker = "queryLocker";

        RateGate _rateGate = new RateGate(0, new TimeSpan(0, 0, 0, 2));

        private async Task<string> CallAsync(HttpMethod method, string endpoint)
        {
            //endpoint = Url + endpoint;

            //return CreatePrivatePostQuery(endpoint, method, new Dictionary<string, string>());

            var request = new HttpRequestMessage(method, endpoint);

            var response = await _httpClient.SendAsync(request).ConfigureAwait(false);

            var result = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

            return result;
        }

        public string CreatePrivatePostQuery(string end_point, HttpMethod method, Dictionary<string, string> parameters)
        {
            lock (_queryLocker)
            {
                _rateGate.WaitToProceed();

                if (parameters == null)
                {
                    parameters = new Dictionary<string, string>();
                }

                StringBuilder sb = new StringBuilder();

                int i = 0;
                foreach (var param in parameters)
                {
                    if (param.Value.StartsWith("["))
                    {
                        sb.Append("\"" + param.Key + "\"" + ": " + param.Value);
                    }
                    else if (param.Value.StartsWith("{"))
                    {
                        sb.Append("\"" + param.Key + "\"" + ": " + param.Value);
                    }
                    else
                    {
                        sb.Append("\"" + param.Key + "\"" + ": \"" + param.Value + "\"");
                    }

                    i++;
                    if (i < parameters.Count)
                    {
                        sb.Append(",");
                    }
                }


                string url = end_point;

                string str_data = "{" + sb.ToString() + "}";

                byte[] data = Encoding.UTF8.GetBytes(str_data);

                Uri uri = new Uri(url);

                var web_request = (HttpWebRequest)WebRequest.Create(uri);

                web_request.Accept = "application/json";

                if (method == HttpMethod.Get)
                {
                    web_request.Method = "GET";
                }
                else
                {
                    web_request.Method = "POST";
                }
               
                web_request.ContentType = "application/json";
                web_request.ContentLength = data.Length;

                using (Stream req_tream = web_request.GetRequestStream())
                {
                    req_tream.Write(data, 0, data.Length);
                }

                var resp = web_request.GetResponse();

                HttpWebResponse httpWebResponse = (HttpWebResponse)resp;

                string response_msg;

                using (var stream = httpWebResponse.GetResponseStream())
                {
                    using (StreamReader reader = new StreamReader(stream))
                    {
                        response_msg = reader.ReadToEnd();
                    }
                }

                httpWebResponse.Close();

                return response_msg;
            }
        }


        private async Task<string> CallAsyncSign(HttpMethod method, string endpoint, string sign, string body = null)
        {
            var request = new HttpRequestMessage(method, endpoint);

            if (body != null)
            {
                request.Content = new StringContent(body, Encoding.UTF8, "application/json");
            }

            request.Headers.Add("FTX-KEY", _client.ApiKey);
            request.Headers.Add("FTX-SIGN", sign);
            request.Headers.Add("FTX-TS", _nonce.ToString());

            var response = await _httpClient.SendAsync(request).ConfigureAwait(false);

            var result = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

            return result;
        }

        private string GenerateSignature(HttpMethod method, string url, string requestBody)
        {
            _nonce = GetNonce();
            var signature = $"{_nonce}{method.ToString().ToUpper()}{url}{requestBody}";
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
    }
}