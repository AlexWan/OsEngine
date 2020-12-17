using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace AdminPanel.Utils
{
    public class ProxyClient : NotificationObject
    {
        TcpClient _client;

        private NetworkStream _stream;

        protected bool TryConnect(string ip, string port, string token)
        {
            if (!DataIsValid(ip, port, token))
            {
                return false;
            }
            
            return Connect(ip, port, token);
        }

        protected bool Connected()
        {
            if (_client == null)
            {
                return false;
            }
            return _client.Connected;
        }

        private bool DataIsValid(string ip, string port, string token)
        {
            if (!IPAddress.TryParse(ip, out _))
            {
                return false;
            }

            if (!Int32.TryParse(port, out _))
            {
                return false;
            }

            if (string.IsNullOrEmpty(token))
            {
                return false;
            }
            
            return true;
        }

        private bool Connect(string ip, string port, string token)
        {
            if (_client == null)
            {
                _client = new TcpClient();
            }
            
            try
            {
                _client.Connect(ip, Convert.ToInt32(port));
            }
            catch (Exception e)
            {
                Close();
                Thread.Sleep(500);
                return false;
            }
            
            _stream = _client.GetStream();

            while (!_client.Connected)
            {
                Thread.Sleep(500);
            }
            if (_client.Connected)
            {
                StartListen();
                var message = $"{{\"Token\":\"{token}\"}}";
                Send(message);
                return true;
            }

            return false;
        }

        protected void Send(string message)
        {
            try
            {
                message = "~" + message;
                byte[] data = Encoding.UTF8.GetBytes(message);
                _stream.Write(data, 0, data.Length);
            }
            catch (Exception e)
            {
                Close();
            }
        }

        private StringBuilder _stringBuilder = new StringBuilder(1000);

        private void StartListen()
        {
            Task.Run(() =>
            {
                try
                {
                    while (Connected())
                    {
                        byte[] data = new byte[1024];
                        StringBuilder builder = new StringBuilder();
                        int bytes = 0;
                        do
                        {
                            bytes = _stream.Read(data, 0, data.Length);
                            builder.Append(Encoding.UTF8.GetString(data, 0, bytes));
                        } while (_client.GetStream().DataAvailable);

                        for (int i = 0; i < builder.Length; i++)
                        {
                            if (builder[i] != '~')
                            {
                                _stringBuilder.Append(builder[i]);
                            }
                            else
                            {
                                HandleData(_stringBuilder.ToString());
                                _stringBuilder = new StringBuilder(1024);
                            }
                        }
                    }
                }
                finally
                {
                    Close();
                }
            });
        }

        public virtual void HandleData(string message)
        {
            throw new NotImplementedException();
        }

        public void Close()
        {
            _stream?.Close();
            _client?.Close();
            _client?.Dispose();
            _client = null;
        }
    }
}
