using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using RestSharp;

namespace OsEngine.Market.Servers.BitStamp.BitStampEntity
{
    public class RequestAuthenticator
    {
        public RequestAuthenticator(string apiKey, string apiSecret, string clientId)
        {
            _nonce = UnixTimeStampUtc();

            int lastSaveNonce = Load();

            if (_nonce < lastSaveNonce)
            {
                _nonce = lastSaveNonce;
            }

            _clientId = clientId;
            _apiKey = apiKey;
            _apiSecret = apiSecret;


            Thread worker = new Thread(NonceNumThreadSaver);
            worker.IsBackground = true;
            worker.Start();
        }

        public void Authenticate(RestRequest request)
        {
            lock (_lock)
            {
                string nonce = _nonce.ToString();
                request.AddParameter("key", _apiKey);
                request.AddParameter("nonce", nonce);
                request.AddParameter("signature", CreateSignature(nonce));
                _nonce++;
                _neadToSave = true;
            }
        }

        public void Dispose()
        {
            _isDisposed = true;
        }

        private readonly string _clientId;
        private readonly string _apiKey;
        private readonly string _apiSecret;
        private long _nonce;
        private object _lock = new object();

        private bool _neadToSave = true;

        private bool _isDisposed;

        private void NonceNumThreadSaver()
        {
            while (true)
            {
                try
                {
                    Thread.Sleep(1000);

                    if (MainWindow.ProccesIsWorked == false)
                    {
                        return;
                    }

                    if (_isDisposed)
                    {
                        return;
                    }

                    if (_neadToSave)
                    {
                        Save();
                        _neadToSave = false;
                    }
                }
                catch (Exception)
                {
                    return;
                }
 
            }
        }

        /// <summary>
        /// upload server settings from file
        /// загрузить настройки сервера из файла
        /// </summary>
        private int Load()
        {
            if (!File.Exists(@"Engine\" + @"BitStampNonceSave.txt"))
            {
                return 0;
            }

            try
            {
                using (StreamReader reader = new StreamReader(@"Engine\" + @"BitStampNonceSave.txt"))
                {
                    int i = Convert.ToInt32(reader.ReadLine());

                    reader.Close();

                    return i;
                }
            }
            catch (Exception)
            {
                // ignored
                return 0;
            }
        }

        /// <summary>
        /// save server settings in file
        /// сохранить настройки сервера в файл
        /// </summary>
        private void Save()
        {
            try
            {
                using (StreamWriter writer = new StreamWriter(@"Engine\" + @"BitStampNonceSave.txt", false))
                {
                    writer.WriteLine(_nonce);

                    writer.Close();
                }
            }
            catch (Exception)
            {
                // ignored
            }
        }

        private string CreateSignature(string nonce)
        {
            var msg = string.Format("{0}{1}{2}", nonce, _clientId, _apiKey);
            return ByteArrayToString(SignHMACSHA256(_apiSecret, StringToByteArray(msg))).ToUpper();
        }

        private static byte[] SignHMACSHA256(string key, byte[] data)
        {
            var hashMaker = new HMACSHA256(Encoding.ASCII.GetBytes(key));
            return hashMaker.ComputeHash(data);
        }

        private static byte[] StringToByteArray(string str)
        {
            return Encoding.ASCII.GetBytes(str);
        }

        private static string ByteArrayToString(byte[] hash)
        {
            return BitConverter.ToString(hash).Replace("-", "").ToLower();
        }

        private static long UnixTimeStampUtc()
        {
            int unixTimeStamp;
            var currentTime = DateTime.Now;
            var dt = currentTime.ToUniversalTime();
            var unixEpoch = new DateTime(1970, 1, 1);
            unixTimeStamp = (Int32)(dt.Subtract(unixEpoch)).TotalSeconds;
            return unixTimeStamp;
        }

    }
}