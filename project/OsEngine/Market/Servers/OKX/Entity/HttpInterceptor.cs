using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;

namespace OsEngine.Market.Servers.OKX.Entity
{
    public class HttpInterceptor : DelegatingHandler
    {
        private string _apiKey;
        private string _passPhrase;
        private string _secret;
        private bool _demoMode;

        public HttpInterceptor(string apiKey, string secret, string passPhrase, bool demoMode, WebProxy myProxy)
        {
            this._apiKey = apiKey;
            this._passPhrase = passPhrase;
            this._secret = secret;
            this._demoMode = demoMode;

            if (myProxy == null)
            {
                InnerHandler = new HttpClientHandler();
            }
            else if (myProxy != null)
            {
                InnerHandler = new HttpClientHandler
                {
                    Proxy = myProxy
                };
            }
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var method = request.Method.Method;
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            request.Headers.Add("OK-ACCESS-KEY", this._apiKey);

            var now = DateTime.Now;
            var timeStamp = TimeZoneInfo.ConvertTimeToUtc(now).ToString("yyyy-MM-ddTHH:mm:ss.fffZ");
            var requestUrl = request.RequestUri.PathAndQuery;

            // the signed body is the request content itself, no side channel is needed
            string bodyStr = request.Content == null
                ? null
                : await request.Content.ReadAsStringAsync();

            string sign;
            if (!String.IsNullOrEmpty(bodyStr))
            {
                sign = Encryptor.HmacSHA256($"{timeStamp}{method}{requestUrl}{bodyStr}", this._secret);
            }
            else
            {
                sign = Encryptor.HmacSHA256($"{timeStamp}{method}{requestUrl}", this._secret);
            }

            request.Headers.Add("OK-ACCESS-SIGN", sign);
            request.Headers.Add("OK-ACCESS-TIMESTAMP", timeStamp.ToString());
            request.Headers.Add("OK-ACCESS-PASSPHRASE", this._passPhrase);

            if (_demoMode)
            {
                request.Headers.Add("x-simulated-trading", "1");
            }
            else
            {
                request.Headers.Add("x-simulated-trading", "0");
            }

            return await base.SendAsync(request, cancellationToken);
        }
    }
}
