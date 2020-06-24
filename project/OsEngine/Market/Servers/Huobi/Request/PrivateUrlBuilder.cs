using System;

namespace OsEngine.Market.Servers.Huobi.Request
{
    public class PrivateUrlBuilder
    {
        private readonly string _host;

        private const string _aKKey = "AccessKeyId";
        private readonly string _aKValue;
        private const string _sMKey = "SignatureMethod";
        private const string _sMVaue = "HmacSHA256";
        private const string _sVKey = "SignatureVersion";
        private const string _sVValue = "2";
        private const string _tKey = "Timestamp";

        private readonly Signer _signer;

        public PrivateUrlBuilder(string accessKey, string secretKey, string host)
        {
            _aKValue = accessKey;
            _signer = new Signer(secretKey);

            _host = host;
        }

        public string Build(string method, string path)
        {
            return Build(method, path, DateTime.UtcNow, null);
        }

        public string Build(string method, string path, DateTime utcDateTime)
        {
            return Build(method, path, utcDateTime, null);
        }

        public string Build(string method, string path, GetRequest request)
        {
            return Build(method, path, DateTime.UtcNow, request);
        }

        public string Build(string method, string path, DateTime utcDateTime, GetRequest request)
        {
            string strDateTime = utcDateTime.ToString("s");

            var req = new GetRequest(request)
                .AddParam(_aKKey, _aKValue)
                .AddParam(_sMKey, _sMVaue)
                .AddParam(_sVKey, _sVValue)
                .AddParam(_tKey, strDateTime);

            string signature = _signer.Sign(method, _host, path, req.BuildParams());

            string url = $"https://{_host}{path}?{req.BuildParams()}&Signature={Uri.EscapeDataString(signature)}";

            return url;
        }
    }
}
