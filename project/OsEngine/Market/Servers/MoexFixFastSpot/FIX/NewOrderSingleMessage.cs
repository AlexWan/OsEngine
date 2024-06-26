
namespace OsEngine.Market.Servers.MoexFixFastSpot.FIX
{    
    class NewOrderSingleMessage: AFIXMessageBody
    {
        public string ClOrdID;
        public string NoPartyID = "1";
        public string PartyID;
        public string PartyIDSource = "D";
        public string PartyRole = "3";
        public string Account;
        public string NoTradingSessions = "1";
        public string TradingSessionID; //"TQBR";
        public string Symbol;
        public string Side;
        public string TransactTime; // UTCTimestamp
        public string OrdType;
        public string OrderQty;
        public string PriceType;
        public string Price = "0";

        public override string ToString()
        {
            return $"11={ClOrdID}\u0001453={NoPartyID}\u0001448={PartyID}\u0001447={PartyIDSource}\u0001452={PartyRole}\u00011={Account}\u0001386={NoTradingSessions}\u0001336={TradingSessionID}\u000155={Symbol}\u000154={Side}\u000160={TransactTime}\u000138={OrderQty}\u000140={OrdType}\u000144={Price}\u0001";
        }
    }
}
