using OsEngine.Market.Servers.Entity;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using OsEngine.Logging;

namespace OsEngine.Market.Servers.CoinEx.Spot.Entity
{
    public class CoinExRestClient : IDisposable
    {
        private readonly string _apiUrl = "https://api.coinex.com/v2";

        private string _apiKey;

        private string _apiSecret;

        private HttpClient _client = new HttpClient();

        public CoinExRestClient(string apiKey, string apiSecret)
        {
            this._apiKey = apiKey;
            this._apiSecret = apiSecret;
        }

        protected string Sign(string method, string path, string body, long timestamp)
        {
            return Signer.RestSign(method, path, body, timestamp, _apiSecret);
        }

        private T Request<T>(string method, string path,
            Dictionary<string, object> args, Dictionary<string, object> body, bool isSign = false)
        {
            if (args != null)
            {
                List<KeyValuePair<string, string>> _args = new List<KeyValuePair<string, string>>();
                for (int i = 0; i < args.Count; i++)
                {
                    KeyValuePair<string, object> itm = args.ElementAt(i);
                    _args.Add(new KeyValuePair<string, string>(itm.Key, itm.Value.ToString()));
                }
                string query = new FormUrlEncodedContent(_args).ReadAsStringAsync().Result;
                path += "?" + query;
            }


            HttpRequestMessage req = new HttpRequestMessage(new HttpMethod(method), _apiUrl + path);
            string bodyContent = "";
            if (body != null)
            {
                bodyContent = JsonConvert.SerializeObject(body);
                req.Content = new StringContent(bodyContent, Encoding.UTF8, "application/json");
            }

            if (isSign)
            {
                long now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                req.Headers.Add("X-COINEX-KEY", _apiKey);
                req.Headers.Add("X-COINEX-SIGN", Sign(method, req.RequestUri!.PathAndQuery, bodyContent, now));
                req.Headers.Add("X-COINEX-TIMESTAMP", now.ToString());
            }

            HttpResponseMessage response = _client.SendAsync(req).Result;
            response.EnsureSuccessStatusCode();
            try
            {
                string responseContent = response.Content.ReadAsStringAsync().Result;
                CoinExHttpResp<T> resp = JsonConvert.DeserializeObject<CoinExHttpResp<T>>(responseContent);
                resp!.EnsureSuccessStatusCode();
                return resp.data;
            }
            catch (HttpRequestException ex)
            {
                SendLogMessage(ex.Message, LogMessageType.Error);
            }
            catch (Exception ex)
            {
                SendLogMessage(response.Content.ReadAsStringAsync().ToString(), LogMessageType.Error);
                SendLogMessage(ex.Message, LogMessageType.Error);
            }
            return (new CoinExHttpResp<T>()).data;
        }

        public T Get<T>(string path, bool isSign = false, Dictionary<string, object> args = null) => Request<T>("GET", path, args, null, isSign);

        public T Post<T>(string path, Dictionary<string, object> body, bool isSign = false) => Request<T>("POST", path, null, body, isSign);

        public void Dispose()
        {
            _client.Dispose();
        }

        private void SendLogMessage(string message, LogMessageType messageType)
        {
            LogMessageEvent?.Invoke(message, messageType);
        }

        public event Action<string, LogMessageType> LogMessageEvent;

    }
}