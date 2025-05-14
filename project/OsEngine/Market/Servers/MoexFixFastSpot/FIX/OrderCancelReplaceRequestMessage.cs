using System.Text;

namespace OsEngine.Market.Servers.MoexFixFastSpot.FIX
{    
    class OrderCancelReplaceRequestMessage: AFIXMessageBody
    {
        public string ClOrdID;
        public string OrigClOrdID;
        public string SecondaryClOrdID;
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
            StringBuilder sb = new StringBuilder();

            sb.Append("11=").Append(ClOrdID).Append('\u0001');
            sb.Append("41=").Append(OrigClOrdID).Append('\u0001');
            sb.Append("37=").Append(OrderID).Append('\u0001');
            sb.Append("1=").Append(Account).Append('\u0001');
            sb.Append("453=").Append(NoPartyID).Append('\u0001');
            sb.Append("448=").Append(PartyID).Append('\u0001');
            sb.Append("447=").Append(PartyIDSource).Append('\u0001');
            sb.Append("452=").Append(PartyRole).Append('\u0001');
            sb.Append("55=").Append(Symbol).Append('\u0001');
            sb.Append("44=").Append(Price).Append('\u0001');
            sb.Append("38=").Append(OrderQty).Append('\u0001');
            
            if (string.IsNullOrEmpty(SecondaryClOrdID) == false)
            {
                sb.Append("526=").Append(SecondaryClOrdID).Append('\u0001');
            }
            
            sb.Append("9619=").Append(CancelOrigOnReject).Append('\u0001');
            sb.Append("386=").Append(NoTradingSessions).Append('\u0001');
            sb.Append("336=").Append(TradingSessionID).Append('\u0001');
            sb.Append("40=").Append(OrdType).Append('\u0001');
            sb.Append("54=").Append(Side).Append('\u0001');
            sb.Append("60=").Append(TransactTime).Append('\u0001');

            return sb.ToString();
        }
    }
}
