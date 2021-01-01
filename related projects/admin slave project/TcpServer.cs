using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using AdminSlave.Model;
using Newtonsoft.Json;

namespace AdminSlave
{
    public class TcpServer
    { 
        string[] _permittedIp;
       

        public int Port { get; set; } = 9999;

        string _permittedToken;

        private string PermittedToken
        {
            get { return _permittedToken;}
            set
            {
                _permittedToken = value;

                foreach (var clientObject in _clients)
                {
                    clientObject.Token = value;
                }
            }
        }

        static TcpListener _tcpListener;
        readonly List<AdminPanelClient> _clients = new List<AdminPanelClient>();

        public event Action Started;
        public event Action Disconnected;
        public event Action<string> ClientNeedRebootEvent;


        private Task _listenTask;

        public void Connect(string[] ips, string token)
        {
            this._permittedIp = ips;
            this.PermittedToken = token;

            _listenTask = Task.Run(Listen).ContinueWith(task =>
            {
                if (task.IsFaulted || task.IsCanceled)
                {
                    Disconnect();
                }
            });
        }

        protected internal void AddConnection(AdminPanelClient adminPanelClient)
        {
            _clients.Add(adminPanelClient);
        }

        protected internal void RemoveConnection(string id)
        {
            AdminPanelClient adminPanelClient = _clients.FirstOrDefault(c => c.Id == id);
            if (adminPanelClient != null)
                _clients.Remove(adminPanelClient);
        }

        protected internal void Listen()
        {
            try
            {
                _tcpListener = new TcpListener(IPAddress.Any, Port);
                _tcpListener.Start();
                Task.Run(Sender);
                Started?.Invoke();

                while (true)
                {
                    TcpClient tcpClient = _tcpListener.AcceptTcpClient();

                    var ip = ((IPEndPoint)tcpClient.Client.RemoteEndPoint).Address.ToString();
                    if (!_permittedIp.Contains(ip))
                    {
                        tcpClient.Close();
                        continue;
                    }

                    AdminPanelClient adminPanelClient = new AdminPanelClient(tcpClient, this);
                    adminPanelClient.Token = PermittedToken;

                    Thread clientThread = new Thread(adminPanelClient.Process);
                    clientThread.Start();
                }
            }
            catch (Exception ex)
            {
                Disconnect();
                Disconnected?.Invoke();
            }
        }

        private void BroadcastMessage(string message)
        {
            byte[] data = Encoding.UTF8.GetBytes(message);
            for (int i = 0; i < _clients.Count; i++)
            {
                if (_clients[i].Authorized)
                {
                    try
                    {
                        _clients[i].Stream.Write(data, 0, data.Length);
                    }
                    catch (Exception e)
                    {
                        _clients[i].Close();
                        RemoveConnection(_clients[i].Id);
                    }
                }
            }
        }

        protected internal void HandleClientMessage(string message)
        {
            if (message.StartsWith("reboot_"))
            {
                var id = message.Replace("reboot_", "");
                ClientNeedRebootEvent?.Invoke(id);
            }
        }

        protected internal void Disconnect()
        {
            if (_clients.Count != 0)
            {
                Send("close");
            }

            Thread.Sleep(200);
            _tcpListener?.Stop();
            _tcpListener = null;
            for (int i = 0; i < _clients.Count; i++)
            {
                _clients[i].Close();
            }
        }

        private readonly ConcurrentQueue<string> _messageToSend = new ConcurrentQueue<string>();

        public void Send(string message)
        {
            _messageToSend.Enqueue(message);
        }

        private void Sender()
        {
            while (true)
            {
                if (!_messageToSend.IsEmpty)
                {
                    _messageToSend.TryDequeue(out var message);
                    BroadcastMessage(message + "~");
                }
                Thread.Sleep(100);
            }
        }
    }

    public class AdminPanelClient
    {
        protected internal string Id { get; private set; }
        protected internal NetworkStream Stream { get; private set; }

        readonly TcpClient _client;
        TcpServer _tcpServer;

        public bool Authorized { get; private set; } = false;

        public string Token { get; set; }

        public AdminPanelClient(TcpClient tcpClient, TcpServer tcpServer)
        {
            Id = Guid.NewGuid().ToString();
            _client = tcpClient;
            _tcpServer = tcpServer;
            tcpServer.AddConnection(this);
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
                    Thread.Sleep(1000);
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
                    catch (Exception e)
                    {
                        break;
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
            do
            {
                var bytes = Stream.Read(data, 0, data.Length);
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
