using System.Text;

namespace OsEngine.Market.Servers.MoexFixFastSpot.FIX
{
    class LogoutMessage: AFIXMessageBody
    {        
        public string Text = " ";

        public override string ToString()
        {
            StringBuilder sb = new StringBuilder();

            sb.Append("58=").Append(Text).Append('\u0001');

            return sb.ToString();
        }
    }
}
