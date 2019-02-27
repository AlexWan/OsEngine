using System;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Text;

namespace OsEngine.Market.Servers.Lmax.LmaxEntity
{
    /// <summary>
	/// data exchange session
    /// сессия обмена данными
    /// </summary>
    public class Session : IDisposable
    {
        public const int BufSize = 8192;
        byte[] _readBuffer = new byte[BufSize];
        private SslStream _stream;
        private TcpClient _tcpClient;

        public Session(TcpClient tcpClient, string uri)
        {
            _tcpClient = tcpClient;
            _stream = new SslStream(tcpClient.GetStream()) { ReadTimeout = 1000 };

            try
            {
                _stream.AuthenticateAsClient(uri);
            }
            catch (AuthenticationException e)
            {
                _tcpClient.Close();
            }
        }

        /// <summary>
		/// send data
        /// отправить данные
        /// </summary>
        public int Send(string data)
        {
            byte[] rawData = Encoding.UTF8.GetBytes(data);
            _stream.Write(rawData, 0, rawData.Length);
            return rawData.Length;
        }

        public event Action<StringBuilder> NewMessageEvent;

        public void Read()
        {
            try
            {
                int bytesRead = _stream.ReadAsync(_readBuffer, 0, _readBuffer.Length).Result;

                if (bytesRead > 0)
                {
                    StringBuilder messageData = new StringBuilder();
                    Decoder decoder = Encoding.UTF8.GetDecoder();
                    char[] chars = new char[decoder.GetCharCount(_readBuffer, 0, bytesRead)];
                    decoder.GetChars(_readBuffer, 0, bytesRead, chars, 0);
                    messageData.Append(chars);

                    NewMessageEvent?.Invoke(messageData);
                }
            }
            catch (Exception e)
            {
                throw e;
            }
        }

        public void Dispose()
        {
            _stream.Close();
            _stream.Dispose();
            _tcpClient.Close();
        }
    }


}
