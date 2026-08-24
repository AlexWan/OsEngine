/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;

namespace OsEngine.UpdateModule
{
    public static class UpdateServerTls
    {
        public const string ServerHost = "185.186.143.9";

        // TLS-порты сервера обновлений (plain-порты 49152/49153 новые версии не используют)
        public const int UpdateProtocolPort = 23453;
        public const int FileServerPort = 23452;

        // отпечаток самоподписанного сертификата сервера обновлений (пиннинг)
        public const string CertThumbprint = "3E4C8EB10433F78F64FA3C218E705A7F9CA89E8E";

        #region Public methods

        public static SslStream ConnectPinned(string host, int port, int timeoutMs)
        {
            TcpClient client = new TcpClient();

            client.ReceiveTimeout = timeoutMs;
            client.SendTimeout = timeoutMs;

            client.Connect(host, port);

            SslStream stream = new SslStream(client.GetStream(), false, ValidateServerCertificate);

            stream.AuthenticateAsClient(new SslClientAuthenticationOptions
            {
                TargetHost = host,
                EnabledSslProtocols = SslProtocols.Tls12 | SslProtocols.Tls13
            });

            return stream;
        }

        public static HttpClientHandler CreatePinnedHandler()
        {
            HttpClientHandler handler = new HttpClientHandler
            {
                AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate
            };

            handler.ServerCertificateCustomValidationCallback = ValidateHttpCertificate;

            return handler;
        }

        public static string GetSecureFileUrl(string url)
        {
            // сервер отдаёт ссылки на plain-порт, новые версии качают только по HTTPS
            return url.Replace($"http://{ServerHost}:49153", $"https://{ServerHost}:{FileServerPort}");
        }

        #endregion

        #region Certificate validation

        private static bool ValidateServerCertificate(object sender, X509Certificate certificate, X509Chain chain, SslPolicyErrors errors)
        {
            if (certificate == null)
            {
                return false;
            }

            X509Certificate2 cert = new X509Certificate2(certificate);

            return string.Equals(cert.GetCertHashString(), CertThumbprint, StringComparison.OrdinalIgnoreCase);
        }

        private static bool ValidateHttpCertificate(HttpRequestMessage message, X509Certificate2 cert, X509Chain chain, SslPolicyErrors errors)
        {
            if (cert == null)
            {
                return false;
            }

            return string.Equals(cert.GetCertHashString(), CertThumbprint, StringComparison.OrdinalIgnoreCase);
        }

        #endregion
    }
}
