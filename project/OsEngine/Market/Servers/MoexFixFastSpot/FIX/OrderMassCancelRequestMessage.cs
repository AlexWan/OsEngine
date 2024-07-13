
namespace OsEngine.Market.Servers.MoexFixFastSpot.FIX
{    
    class OrderMassCancelRequestMessage: AFIXMessageBody
    {

        public string ClOrdID;
        public string MassCancelRequestType = "7"; // 1 - cancel for security, 7 - cancel for all matching orders
        public string TradingSessionID;
        public string Symbol;
        public string TransactTime;
        public string Account;

        // группа Parties
        public string NoPartyID = "1";
        public string PartyID;
        public string PartyIDSource = "D";
        public string PartyRole = "3";

        public override string ToString()
        {
            string TradingSessionIDString = MassCancelRequestType == "1" ? $"336={TradingSessionID}\u0001" : "";
            string Instrument = MassCancelRequestType == "1" ? $"55={Symbol}\u0001" : "";

            return $"11={ClOrdID}\u0001530={MassCancelRequestType}\u0001{TradingSessionIDString}{Instrument}60={TransactTime}\u00011={Account}\u0001453={NoPartyID}\u0001448={PartyID}\u0001447={PartyIDSource}\u0001452={PartyRole}\u0001";
        }
    }
}
