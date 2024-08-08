using OsEngine.Market.Servers.Entity;
using System;
using System.Net.Http;
using System.Text;
using System.Security.Cryptography;

namespace OsEngine.Market.Servers.BitMart.Json
{

    public class BitMartRestClient
    {
        private readonly string _restApiHost = "https://api-cloud.bitmart.com";

        private string _apiKey;
        private string _apiSecret;
        private string _apiMemo;

        private HttpClient _client = new HttpClient();

        //rate limiter
        public RateGate _rateGateRest = new RateGate(1, TimeSpan.FromMilliseconds(30));

        public BitMartRestClient(string apiKey, string apiSecret, string apiMemo)
        {
            this._apiKey = apiKey;
            this._apiSecret = apiSecret;
            this._apiMemo = apiMemo;
        }

        public HttpResponseMessage Get(string endPoint, bool secured = false)
        {
            _rateGateRest.WaitToProceed();

            string url = _restApiHost + endPoint;

            var request = new HttpRequestMessage(HttpMethod.Get, url);
            if (secured)
            {
                request.Headers.Add("X-BM-KEY", this._apiKey);
            }

            HttpResponseMessage response = _client.SendAsync(request).Result;

            return response;
        }

        public HttpResponseMessage Post(string endPoint, string bodyStr, bool secured = false)
        {
            _rateGateRest.WaitToProceed();

            string url = _restApiHost + endPoint;

            var request = new HttpRequestMessage(HttpMethod.Post, url);
            request.Content = new StringContent(bodyStr, Encoding.UTF8, "application/json");
            if (secured)
            {
                string timestamp = BitMartEncriptor.GetTimestamp();

                string signature = BitMartEncriptor.GenerateSignature(timestamp, bodyStr, _apiMemo, _apiSecret);

                request.Headers.Add("X-BM-KEY", this._apiKey);
                request.Headers.Add("X-BM-TIMESTAMP", timestamp);
                request.Headers.Add("X-BM-SIGN", signature);
            }

            HttpResponseMessage response = _client.SendAsync(request).Result;

            return response;
        }

    }

    public class BitMartEncriptor
    {

        /// <summary>
        /// take milisecond time from 01.01.1970
        /// взять время в милисекундах, прошедшее с 1970, 1, 1 года
        /// </summary>
        /// <returns></returns>
        public static string GetTimestamp()
        {
            return DateTimeOffset.Now.ToUnixTimeMilliseconds().ToString();
        }

        public static string GenerateSignature(string timestamp, string body, string apiMemo, string apiSecret)
        {
            string message = $"{timestamp}#{apiMemo}#{body}";
            using HMACSHA256 hmac = new HMACSHA256(Encoding.UTF8.GetBytes(apiSecret));
            byte[] hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(message));
            return BitConverter.ToString(hash).Replace("-", "").ToLower();
        }

        public static WSRequestAuth.AuthArgs GetWSAuthArgs(string apiKey, string secretKey, string memo)
        {
            WSRequestAuth.AuthArgs args = new WSRequestAuth.AuthArgs();

            var timeStamp = GetTimestamp();

            args.apiKey = apiKey;
            args.timestamp = timeStamp;

            string signature = BitMartEncriptor.GenerateSignature(timeStamp, "bitmart.WebSocket", memo, secretKey);

            args.sign = signature;

            return args;
        }
    }

}
