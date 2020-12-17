using Newtonsoft.Json;
using System;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace OsEngine.OsTrader.AdminPanelApi
{
    public class AdminPanelClient
    {
        protected internal string Id { get; private set; }
        protected internal NetworkStream Stream { get; private set; }

        readonly TcpClient _client;
        TcpServer _tcpServer;

        public bool Authorized { get; private set; } = false;

        public string Token { get; set; }

        public AdminPanelClient(TcpClient tcpClient, TcpServer server)
        {
            Id = Guid.NewGuid().ToString();
            _client = tcpClient;
            _tcpServer = server;
            server.AddConnection(this);
        }

        private DateTime _lastMessageTime = DateTime.Now;

        public void Process()
        {
            try
            {
                if (!_client.Connected)
                {
                    throw new NetworkInformationException();
                }
                Stream = _client.GetStream();

                while (true)
                {
                    Thread.Sleep(100);
                    CheckTimeOut();
                    try
                    {
                        var message = GetMessage();

                        if (string.IsNullOrEmpty(message))
                        {
                            continue;
                        }

                        var listMessages = message.Split('~');

                        foreach (var msg in listMessages)
                        {
                            if (string.IsNullOrEmpty(msg))
                            {
                                continue;
                            }

                            if (!Authorized)
                            {
                                var res = TryAuthorization(msg, Token);
                                Authorized = res;
                                if (res)
                                {
                                    _tcpServer.Send("auth");
                                }

                                continue;
                            }
                            if (msg == "ping")
                            {
                                _lastMessageTime = DateTime.Now;
                                continue;
                            }
                            _tcpServer.HandleClientMessage(msg);
                        }
                    }
                    catch
                    {
                        continue;
                    }
                }
            }
            finally
            {
                _tcpServer.RemoveConnection(this.Id);
                Close();
            }
        }

        private void CheckTimeOut()
        {
            if (_lastMessageTime.AddMinutes(1) < DateTime.Now)
            {
                _tcpServer.RemoveConnection(this.Id);
                Close();
            }
        }

        private bool TryAuthorization(string message, string token)
        {
            try
            {
                var authorization = JsonConvert.DeserializeObject<AuthorizationMessage>(message);
                if (authorization.Token == token)
                {
                    return true;
                }

                return false;

            }
            catch (Exception e)
            {
                return false;
            }
        }

        private string GetMessage()
        {
            byte[] data = new byte[1024];
            StringBuilder builder = new StringBuilder();
            int bytes = 0;
            do
            {
                bytes = Stream.Read(data, 0, data.Length);
                builder.Append(Encoding.UTF8.GetString(data, 0, bytes));
            }
            while (Stream.DataAvailable);

            return builder.ToString();
        }

        protected internal void Close()
        {
            Stream?.Close();
            _client?.Close();
        }
    }
}
