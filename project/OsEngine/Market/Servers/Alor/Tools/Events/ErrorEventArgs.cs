using System;
using System.Net.WebSockets;

namespace OsEngine.Market.Servers.Alor.Tools.Events
{
    public class ErrorEventArgs: EventArgs
    {
        public DateTime TimeStamp { get; set; }
        public string ErrorDetails { get; set; }
        public WebSocketState ClientWebSocketState { get; set; }
    }
}