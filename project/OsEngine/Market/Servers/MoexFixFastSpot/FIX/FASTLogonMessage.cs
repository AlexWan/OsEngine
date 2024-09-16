using System.Text;

namespace OsEngine.Market.Servers.MoexFixFastSpot.FIX
{
    class FASTLogonMessage: AFIXMessageBody
    {
        public string Username = "user0"; // user1, user2
        public string Password = "pass0"; // pass1, pass2
        public string DefaultApplVerID = "9";

        public override string ToString()
        {            
            StringBuilder sb = new StringBuilder();

            sb.Append("553=").Append(Username).Append('\u0001');
            sb.Append("554=").Append(Password).Append('\u0001');
            sb.Append("1137=").Append(DefaultApplVerID).Append('\u0001');

            return sb.ToString();
        }        
    }
}
