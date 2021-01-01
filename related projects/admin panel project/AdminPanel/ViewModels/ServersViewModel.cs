using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using AdminPanel.Entity;
using AdminPanel.Language;
using Newtonsoft.Json.Linq;

namespace AdminPanel.ViewModels
{
    public class ServersViewModel : NotificationObject, ILocalization
    {
        private ObservableCollection<Server> _servers = new ObservableCollection<Server>();

        public ObservableCollection<Server> Servers
        {
            get { return _servers; }
            set { SetProperty(ref _servers, value, () => Servers); }
        }

        private Server _selectedServer;

        public Server SelectedServer
        {
            get => _selectedServer;
            set { SetProperty(ref _selectedServer, value, () => SelectedServer); }
        }

        #region Severs local

        private string _sourceHeader = OsLocalization.Entity.ColumnServers1;

        public string SourceHeader
        {
            get { return _sourceHeader; }
            set { SetProperty(ref _sourceHeader, value, () => SourceHeader); }
        }

        private string _serverStateHeader = OsLocalization.Entity.ColumnServers2;

        public string ServerStateHeader
        {
            get { return _serverStateHeader; }
            set { SetProperty(ref _serverStateHeader, value, () => ServerStateHeader); }
        }

        private string _timeHeader = OsLocalization.Entity.TradeColumn4;

        public string TimeHeader
        {
            get { return _timeHeader; }
            set { SetProperty(ref _timeHeader, value, () => TimeHeader); }
        }

        private string _logTypeHeader = OsLocalization.Entity.OrderColumn11;

        public string LogTypeHeader
        {
            get { return _logTypeHeader; }
            set { SetProperty(ref _logTypeHeader, value, () => LogTypeHeader); }
        }

        private string _messageHeader = OsLocalization.Entity.LogMessage;

        public string MessageHeader
        {
            get { return _messageHeader; }
            set { SetProperty(ref _messageHeader, value, () => MessageHeader); }
        }

        private string _serversLbl = OsLocalization.Entity.ServersLbl;

        public string ServersLbl
        {
            get { return _serversLbl; }
            set { SetProperty(ref _serversLbl, value, () => ServersLbl); }
        }

        private string _serverLogLbl = OsLocalization.Entity.ServerLogLbl;

        public string ServerLogLbl
        {
            get { return _serverLogLbl; }
            set { SetProperty(ref _serverLogLbl, value, () => ServerLogLbl); }
        }

        public void ChangeLocal()
        {
            SourceHeader = OsLocalization.Entity.ColumnServers1;
            ServerStateHeader = OsLocalization.Entity.ColumnServers2;
            TimeHeader = OsLocalization.Entity.TradeColumn4;
            LogTypeHeader = OsLocalization.Entity.OrderColumn11;
            MessageHeader = OsLocalization.Entity.LogMessage;
            ServersLbl = OsLocalization.Entity.ServersLbl;
            ServerLogLbl = OsLocalization.Entity.ServerLogLbl;
        }
        #endregion

        public void UpdateServerState(JToken jt)
        {
            var name = jt["Server"].Value<string>();
            var state = jt["State"].Value<string>();

            var needServer = Servers.FirstOrDefault(s => s.ServerName == name);

            if (needServer == null)
            {
                needServer = new Server();
                needServer.ServerName = name;
                needServer.State = ServerState.Disconnect;

                AddElement(needServer, Servers);
            }
            needServer.State = state == "Connect" ? ServerState.Connect : ServerState.Disconnect;
        }

        public void HandleServerLog(JToken jt)
        {
            var name = jt.Value<string>("Server");
            var logMessage = jt.Value<string>("LogMessage");
            var logMessageType = jt.Value<string>("LogMessageType");
            
            var needServer = Servers.ToList().Find(s => s.ServerName == name);

            if (needServer == null)
            {
                needServer = new Server();
                needServer.ServerName = name;
                needServer.State = ServerState.Disconnect;

                AddElement(needServer, Servers);
            }

            var logMsg = new LogMessage
            {
                Message = logMessage,
                Type = Enum.Parse<LogMessageType>(logMessageType),
                Time = DateTime.Now
            };

            AddElement(logMsg, needServer.Log);
        }
    }

    public class LogMessage : NotificationObject
    {
        public DateTime Time { get; set; }
        public LogMessageType Type { get; set; }
        public string Message { get; set; }
    }

    public enum ServerState
    {
        Connect,
        Disconnect
    }
}
