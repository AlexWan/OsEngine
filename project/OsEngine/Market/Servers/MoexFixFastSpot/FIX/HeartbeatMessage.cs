using System.Text;

namespace OsEngine.Market.Servers.MoexFixFastSpot.FIX
{
    class HeartbeatMessage: AFIXMessageBody
    {
        public string TestReqID { get; set; }

        public override string ToString()
        {
            StringBuilder sb = new StringBuilder();

            sb.Append("112=").Append(TestReqID).Append('\u0001');

            return sb.ToString();
        }        
    }
}
