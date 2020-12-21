using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace OsEngine.OsTrader.AdminPanelApi
{
    public class TcpServer : IApiServer
    {
        public int ExitPort { get; set; }

        string[] _permittedIp;
       
        string _permittedToken;

        private string PermittedToken
        {
            get { return _permittedToken; }
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

        protected internal void AddConnection(AdminPanelClient clientObject)
        {
            _clients.Add(clientObject);
        }

        protected internal void RemoveConnection(string id)
        {
            AdminPanelClient client = _clients.FirstOrDefault(c => c.Id == id);
            if (client != null)
                _clients.Remove(client);
        }

        protected internal void Listen()
        {
            try
            {
                _tcpListener = new TcpListener(IPAddress.Any, ExitPort);
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
                    
                    AdminPanelClient clientObject = new AdminPanelClient(tcpClient, this);
                    clientObject.Token = PermittedToken;

                    Thread clientThread = new Thread(clientObject.Process);
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
            
        }

        protected internal void Disconnect()
        {
            Send("close");
            _tcpListener.Stop();
            
            for (int i = 0; i < _clients.Count; i++)
            {
                _clients[i].Close();
            }
            Environment.Exit(0);
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

}
