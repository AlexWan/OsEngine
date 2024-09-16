using System.Text;

namespace OsEngine.Market.Servers.MoexFixFastSpot.FIX
{
    class ResendRequestMessage: AFIXMessageBody
    {
        public long BeginSeqNo;
        public long EndSeqNo = 0; // 0 - infinity, i.e. all messages

        public override string ToString()
        {
            StringBuilder sb = new StringBuilder();

            sb.Append("7=").Append(BeginSeqNo).Append('\u0001');
            sb.Append("16=").Append(EndSeqNo).Append('\u0001');

            return sb.ToString();

        }        
    }
}
