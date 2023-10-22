using System;

namespace OsEngine.Market.Servers.Alor.Tools
{
    public class SocketErrorEventArgs : System.EventArgs
    {
        public DateTime TimeStamp { get; set; }
        public string ErrorMessage { get; set; }
    }
}