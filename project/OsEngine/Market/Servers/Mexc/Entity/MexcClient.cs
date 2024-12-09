using OsEngine.Market.Servers.Entity;
using System;
using System.Net.Http;
using System.Text;
using System.Security.Cryptography;
using Google.Protobuf.WellKnownTypes;
using System.Collections.Generic;
using OsEngine.Market.Services;
using System.Windows.Input;

namespace OsEngine.Market.Servers.Mexc.Json
{

    public class MexcRestClient
    {
        private readonly string _restApiHost = "https://api.mexc.com";

        private string _apiKey;
        private string _apiSecret;


        private HttpClient _client = new HttpClient();

        //rate limiter
        public RateGate _rateGateRest = new RateGate(1, TimeSpan.FromMilliseconds(30));


        // https://mexcdevelop.github.io/apidocs/spot_v3_en/#signed

        public MexcRestClient(string apiKey, string apiSecret)
        {
            this._apiKey = apiKey;
            this._apiSecret = apiSecret;
        }

        private string GetQueryString(Dictionary<string, string> queryParams = null)
        {
            string query = "";

            if (queryParams != null)
            {
                foreach (var onePar in queryParams)
                {
                    if (query.Length > 0)
                        query += "&";
                    query += onePar.Key + "=" + onePar.Value;
                }
            }

            return query;
        }

        private string GetSecuredQueryString(Dictionary<string, string> queryParams = null)
        {
            string query = GetQueryString(queryParams);

            string timestamp = GetTimestamp();

            if (query.Length > 0)
                query += "&";

            query += "recvWindow=10000&timestamp=" + timestamp;

            string signature = GenerateSignature(query, _apiSecret);

            query += "&signature=" + signature;

            return query;

        }
        public HttpResponseMessage Post(string endPoint, Dictionary<string, string> queryParams = null, bool secured = false)
        {
            return this.Query(HttpMethod.Post, endPoint, queryParams, secured);
        }

        public HttpResponseMessage Put(string endPoint, Dictionary<string, string> queryParams = null, bool secured = false)
        {
            return this.Query(HttpMethod.Put, endPoint, queryParams, secured);
        }

        public HttpResponseMessage Get(string endPoint, Dictionary<string, string> queryParams = null, bool secured = false)
        {
            return this.Query(HttpMethod.Get, endPoint, queryParams, secured);
        }

        public HttpResponseMessage Delete(string endPoint, Dictionary<string, string> queryParams = null, bool secured = false)
        {
            return this.Query(HttpMethod.Delete, endPoint, queryParams, secured);
        }


        private HttpResponseMessage Query(HttpMethod method, string endPoint, Dictionary<string, string> queryParams = null, bool secured = false)
        {
            _rateGateRest.WaitToProceed();

            HttpRequestMessage request = null;
            
            if (secured)
            {
                string query = GetSecuredQueryString(queryParams);

                string url = _restApiHost + endPoint;
                if (!string.IsNullOrEmpty(query))
                {
                    url += "?" + query;
                }

                request = new HttpRequestMessage(method, url);
                request.Headers.Add("X-MEXC-APIKEY", this._apiKey);
            } 
            else
            {
                string query = GetQueryString(queryParams);

                string url = _restApiHost + endPoint;
                if (!string.IsNullOrEmpty(query))
                {
                    url += "?" + query;
                }
                request = new HttpRequestMessage(method, url);
            }

            HttpResponseMessage response = _client.SendAsync(request).Result;

            return response;
        }

        /// <summary>
        /// take milisecond time from 01.01.1970
        /// взять время в милисекундах, прошедшее с 1970, 1, 1 года
        /// </summary>
        /// <returns></returns>
        public static string GetTimestamp()
        {
            return DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString();
        }

        public static string GenerateSignature(string source, string key)
        {
            byte[] keyBytes = Encoding.UTF8.GetBytes(key);
            using (HMACSHA256 hmacsha256 = new HMACSHA256(keyBytes))
            {
                byte[] sourceBytes = Encoding.UTF8.GetBytes(source);

                byte[] hash = hmacsha256.ComputeHash(sourceBytes);

                return BitConverter.ToString(hash).Replace("-", "").ToLower();
            }
        }

    }

}
