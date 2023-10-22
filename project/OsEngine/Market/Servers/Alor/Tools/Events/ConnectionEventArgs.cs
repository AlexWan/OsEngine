using System;
using System.Net.WebSockets;

namespace OsEngine.Market.Servers.Alor.Tools.Events
{
    public class ConnectionEventArgs : EventArgs
    {
        public DateTime TimeStamp { get; set; }
        public WebSocketState State { get; set; }
        public string StatusText { get; set; }
    }
}