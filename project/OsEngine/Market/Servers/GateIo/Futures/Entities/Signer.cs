using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace OsEngine.Market.Servers.GateIo.Futures.Entities
{
    public class Signer : IDisposable
    {
        HMACSHA512 hmac;
        string _sKey = "";
        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="key">The secrect key that is used to sign</param>
        public Signer(string sKey)
        {
            _sKey = sKey;
            hmac = new HMACSHA512(Encoding.UTF8.GetBytes(_sKey));
        }

        /// <summary>
        /// Generate sigature
        /// </summary>
        /// <param name="method">HTTP method, use "GET" for websocket</param>
        /// <param name="path">Path</param>
        /// <param name="parameters">Parameter pairs</param>
        /// <returns></returns>
        public string SignWs(string channel, string iEvent, string time)
        {
            if (string.IsNullOrEmpty(channel) || string.IsNullOrEmpty(iEvent) || string.IsNullOrEmpty(time))
            {
                return string.Empty;
            }

            StringBuilder sb = new StringBuilder();

            sb.Append($"channel={channel}&");
            sb.Append($"event={iEvent}&");
            sb.Append($"time={time}");

            return SingData(sb.ToString());
        }

        public string SingData(string signatureString)
        {
            hmac.Initialize();

            byte[] buffer = Encoding.UTF8.GetBytes(signatureString);

            return BitConverter.ToString(hmac.ComputeHash(buffer)).Replace("-", "").ToLower();
        }

        public string GetSignStringRest(string method, string fullPath, string query_param, string bodyContent, string timeStamp)
        {
            string bodyHash = SHA512HexHashString(bodyContent);

            StringBuilder sb = new StringBuilder();

            sb.Append(method + "\n");
            sb.Append(fullPath + "\n");
            sb.Append(query_param + "\n");
            sb.Append(bodyHash + "\n");
            sb.Append(timeStamp);

            Console.WriteLine(sb.ToString());

            return SingData(sb.ToString());
        }

        private string SHA512HexHashString(string StringIn)
        {
            string hashString;
            using (var sha256 = SHA512Managed.Create())
            {
                var hash = sha256.ComputeHash(Encoding.UTF8.GetBytes(StringIn));
                hashString = ToHex(hash, false);
            }

            return hashString;
        }

        private string ToHex(byte[] bytes, bool upperCase)
        {
            StringBuilder result = new StringBuilder(bytes.Length * 2);
            for (int i = 0; i < bytes.Length; i++)
                result.Append(bytes[i].ToString(upperCase ? "X2" : "x2"));
            return result.ToString();
        }


        private bool _isDisposed = false;

        protected virtual void Dispose(bool disposing)
        {
            if (!_isDisposed)
            {
                if (disposing)
                {
                    hmac.Dispose();
                }

                _isDisposed = true;
            }
        }

        public void Dispose()
        {
            Dispose(true);
        }

    }
}