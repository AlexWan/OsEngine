using OsEngine.Logging;
using OsEngine.Market.Servers;

namespace OsEngine.OsTrader.AdminPanelApi.Model
{
    public class ServerModel
    {
        public IServer Server { get; }

        public string Name { get; }

        public IApiServer ApiServer { get; }

        public ServerModel(IServer server, IApiServer apiServer)
        {
            Server = server;
            Name = server.ServerType.ToString();
            ApiServer = apiServer;

            server.ConnectStatusChangeEvent += ServerOnConnectStatusChangeEvent;
            server.LogMessageEvent += ServerOnLogMessageEvent;
        }

        private void ServerOnLogMessageEvent(string message, LogMessageType logMessageType)
        {
            string msg = "serverLog_" + $"{{\"Server\":\"{Name}\", \"LogMessage\":\"{message}\", \"LogMessageType\":\"{logMessageType}\"}}";
            ApiServer.Send(msg);
        }

        private void ServerOnConnectStatusChangeEvent(string state)
        {
            string msg = "serverState_" + $"{{\"Server\":\"{Name}\", \"State\":\"{state}\"}}";
            ApiServer.Send(msg);
        }
    }
}
