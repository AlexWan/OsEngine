using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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
            return $"8={BeginString}\u00019={BodyLength}\u0001" + GetHalfMessage();
        }
        public int GetHeaderSize()
        {
            string tmpString = GetHalfMessage();
            return tmpString.Length;
        }
    }
}
