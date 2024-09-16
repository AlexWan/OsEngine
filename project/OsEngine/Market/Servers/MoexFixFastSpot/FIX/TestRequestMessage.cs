using System.Text;

namespace OsEngine.Market.Servers.MoexFixFastSpot.FIX
{
    class TestRequestMessage: AFIXMessageBody
    {
        public string TestReqID;

        public override string ToString()
        {
            StringBuilder sb = new StringBuilder();

            sb.Append("112=").Append(TestReqID).Append('\u0001');

            return sb.ToString();
        }        
    }
}
