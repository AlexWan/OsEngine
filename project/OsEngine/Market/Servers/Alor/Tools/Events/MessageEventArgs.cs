using System;

namespace OsEngine.Market.Servers.Alor.Tools.Events
{
    public class MessageEventArgs : EventArgs
    {
        public DateTime TimeStamp { get; set; }
        public byte[] Buffer { get; set; }
    }
}