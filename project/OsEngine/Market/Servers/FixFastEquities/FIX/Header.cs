using System;


namespace OsEngine.Market.Servers.FixFastEquities.FIX
{
    class Header
    {
        public string BeginString { get; set; }
        public int BodyLength { get; set; }
        public string MsgType { get; set; }
        public string SenderCompID { get; set; }
        public string TargetCompID { get; set; }
        public int MsgSeqNum { get; set; }
        public DateTime SendingTime { get; set; }

        public Header()
        {
            SendingTime = DateTime.UtcNow;
        }

        public override string ToString()
        {
            return $"8={BeginString}\u00019={BodyLength}\u0001" + GetHalfMessage();
        }

        public string GetHalfMessage()
        {
            string sendingTime = SendingTime.ToString("yyyyMMdd-HH:mm:ss.fff");
            return $"35={MsgType}\u000134={MsgSeqNum}\u000149={SenderCompID}\u000152={sendingTime}\u000156={TargetCompID}\u0001";
        }

        public int GetHeaderSize()
        {
            string tmpString = GetHalfMessage();
            return tmpString.Length;
        }
    }
}
