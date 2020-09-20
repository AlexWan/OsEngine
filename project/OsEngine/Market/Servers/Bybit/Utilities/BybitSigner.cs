using OsEngine.Market.Servers.Bybit.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace OsEngine.Market.Servers.Bybit.Utilities
{
    public static class BybitSigner
    {
        public static string CreateSignature(Client client, string message)
        {
            var signatureBytes = Hmacsha256(Encoding.UTF8.GetBytes(client.ApiSecret), Encoding.UTF8.GetBytes(message));

            return ByteArrayToString(signatureBytes);
        }

        private static byte[] Hmacsha256(byte[] keyByte, byte[] messageBytes)
        {
            using (var hash = new HMACSHA256(keyByte))
            {
                return hash.ComputeHash(messageBytes);
            }
        }

        public static string ByteArrayToString(byte[] ba)
        {
            var hex = new StringBuilder(ba.Length * 2);

            foreach (var b in ba)
            {
                hex.AppendFormat("{0:x2}", b);
            }
            return hex.ToString();
        }
    }
}
