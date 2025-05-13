using System.Text;

namespace OsEngine.Market.Servers.MoexFixFastSpot.FIX
{    
    class NewOrderSingleMessage: AFIXMessageBody
    {
        public string ClOrdID;
        public string SecondaryClOrdID;
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
            StringBuilder sb = new StringBuilder();

            sb.Append("11=").Append(ClOrdID).Append('\u0001');
            sb.Append("453=").Append(NoPartyID).Append('\u0001');
            sb.Append("448=").Append(PartyID).Append('\u0001');
            sb.Append("447=").Append(PartyIDSource).Append('\u0001');
            sb.Append("452=").Append(PartyRole).Append('\u0001');
            sb.Append("1=").Append(Account).Append('\u0001');

            if (!string.IsNullOrEmpty(SecondaryClOrdID))
            {
                sb.Append("526=").Append(SecondaryClOrdID).Append('\u0001');
            }

            sb.Append("386=").Append(NoTradingSessions).Append('\u0001');
            sb.Append("336=").Append(TradingSessionID).Append('\u0001');
            sb.Append("55=").Append(Symbol).Append('\u0001');
            sb.Append("54=").Append(Side).Append('\u0001');
            sb.Append("60=").Append(TransactTime).Append('\u0001');
            
            sb.Append("38=").Append(OrderQty).Append('\u0001');
            sb.Append("40=").Append(OrdType).Append('\u0001');
            sb.Append("44=").Append(Price).Append('\u0001');

            return sb.ToString();
        }
    }
}
