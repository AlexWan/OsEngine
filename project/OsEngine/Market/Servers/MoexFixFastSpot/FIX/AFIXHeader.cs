using System;
using System.Text;

namespace OsEngine.Market.Servers.MoexFixFastSpot.FIX
{
    internal abstract class AFIXHeader
    {
        public string BeginString;
        public int BodyLength;
        public string MsgType;
        public string SenderCompID;
        public string TargetCompID;

        public DateTime SendingTime;
        public long MsgSeqNum;
        public AFIXHeader()
        {
            SendingTime = DateTime.UtcNow;
        }

        public abstract string GetHalfMessage();

        public override string ToString()
        {
            StringBuilder sb = new StringBuilder();

            sb.Append("8=").Append(BeginString).Append('\u0001');
            sb.Append("9=").Append(BodyLength).Append('\u0001');
            sb.Append(GetHalfMessage());

            return sb.ToString();
        }
        public int GetHeaderSize()
        {
            string tmpString = GetHalfMessage();
            return tmpString.Length;
        }
    }
}
