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
            using (var hmacsha256 = new HMACSHA256(secretData))
            {
                byte[] buffer = hmacsha256.ComputeHash(sha256Data);
                return Convert.ToBase64String(buffer);
            }
        }

        public static string MakeAuthRequest(string apiKey, string secret, string phrase)
        {
            var timeStamp = Convert.ToInt64((DateTime.UtcNow - new DateTime(1970, 1, 1)).TotalSeconds);
            var sign = Encryptor.HmacSHA256($"{timeStamp}GET/users/self/verify", secret);

            List<AuthObject> listObject = new List<AuthObject>();

            listObject.Add(new AuthObject()
            {
                apiKey = apiKey,
                passphrase = phrase,
                timestamp = timeStamp.ToString(),
                sign = sign
            });

            var info = new
            {
                op = "login",
                args = listObject
            };

            var json = JsonConvert.SerializeObject(info);

            return json;
        }

        public class AuthObject
        {
            public string apiKey;
            public string passphrase;
            public string timestamp;
            public string sign;
        }

    }
}
