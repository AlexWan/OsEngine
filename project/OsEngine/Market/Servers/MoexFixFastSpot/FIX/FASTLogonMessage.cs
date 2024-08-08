
namespace OsEngine.Market.Servers.MoexFixFastSpot.FIX
{
    class FASTLogonMessage: AFIXMessageBody
    {
        public string Username = "user0"; // user1, user2
        public string Password = "pass0"; // pass1, pass2
        public string DefaultApplVerID = "9";

        public override string ToString()
        {            
            return $"553={Username}\u0001554={Password}\u00011137={DefaultApplVerID}\u0001";
           // return "98=0\u0001108=30\u0001553={Username}\u0001554={Password}\u00011137={DefaultApplVerID}\u0001";
        }        
    }
}
