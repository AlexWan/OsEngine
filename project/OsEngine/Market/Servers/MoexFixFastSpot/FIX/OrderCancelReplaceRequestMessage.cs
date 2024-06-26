
namespace OsEngine.Market.Servers.MoexFixFastSpot.FIX
{    
    class OrderCancelReplaceRequestMessage: AFIXMessageBody
    {
        public string ClOrdID;
        public string OrigClOrdID;
        public string OrderID;
        public string Account;
        
        // группа Parties
        public string NoPartyID = "1";
        public string PartyID;
        public string PartyIDSource = "D";
        public string PartyRole = "3";

        public string Symbol;

        public string Price;
        public string OrderQty;
                
        public string CancelOrigOnReject = "N"; // снять исходную заявку если ее изменение невозможно

        public string NoTradingSessions = "1";
        public string TradingSessionID; //"TQBR";
        public string OrdType;
        public string Side;
        public string TransactTime; // UTCTimestamp
        
        public override string ToString()
        {
            return $"11={ClOrdID}\u000141={OrigClOrdID}\u000137={OrderID}\u00011={Account}\u0001453={NoPartyID}\u0001448={PartyID}\u0001447={PartyIDSource}\u0001452={PartyRole}\u000155={Symbol}\u000144={Price}\u000138={OrderQty}\u00019619={CancelOrigOnReject}\u0001386={NoTradingSessions}\u0001336={TradingSessionID}\u000140={OrdType}\u000154={Side}\u000160={TransactTime}\u0001";
        }
    }
}
