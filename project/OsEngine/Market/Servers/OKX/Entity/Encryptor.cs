using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;

namespace OsEngine.Market.Servers.OKX.Entity
{
    static class Encryptor
    {
        public static string HmacSHA256(string infoStr, string secret)
        {
            byte[] sha256Data = Encoding.UTF8.GetBytes(infoStr);
            byte[] secretData = Encoding.UTF8.GetBytes(secret);
            using (HMACSHA256 hmacsha256 = new HMACSHA256(secretData))
            {
                byte[] buffer = hmacsha256.ComputeHash(sha256Data);
                return Convert.ToBase64String(buffer);
            }
        }

        public static string MakeAuthRequest(string apiKey, string secret, string phrase)
        {
            long timeStamp = Convert.ToInt64((DateTime.UtcNow - new DateTime(1970, 1, 1)).TotalSeconds);
            string sign = Encryptor.HmacSHA256($"{timeStamp}GET/users/self/verify", secret);

            RequestAuth requestAuth = new RequestAuth();
            requestAuth.args = new List<AuthObject>();
            requestAuth.args.Add(new AuthObject());
            requestAuth.args[0].apiKey = apiKey;
            requestAuth.args[0].passphrase = phrase;
            requestAuth.args[0].timestamp = timeStamp.ToString();
            requestAuth.args[0].sign = sign;

            string json = JsonConvert.SerializeObject(requestAuth);

            return json;
        }

        public class AuthObject
        {
            public string apiKey;
            public string passphrase;
            public string timestamp;
            public string sign;
        }

        public class RequestAuth
        {
            public string op = "login";
            public List<AuthObject> args;
        }
    }
}
