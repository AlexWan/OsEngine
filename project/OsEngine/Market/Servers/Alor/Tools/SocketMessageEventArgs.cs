using System;
using OsEngine.Market.Servers.Alor.Dto;

namespace OsEngine.Market.Servers.Alor.Tools
{
    public class SocketMessageEventArgs : System.EventArgs
    {
        public DateTime TimeStamp { get; set; }
        public MessageData Data { get; set; }
    }
}