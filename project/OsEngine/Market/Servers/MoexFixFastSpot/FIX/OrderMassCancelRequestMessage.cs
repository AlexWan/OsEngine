using System.Text;

namespace OsEngine.Market.Servers.MoexFixFastSpot.FIX
{    
    class OrderMassCancelRequestMessage: AFIXMessageBody
    {

        public string ClOrdID;
        public string SecondaryClOrdID;
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
            StringBuilder sb = new StringBuilder();

            sb.Append("11=").Append(ClOrdID).Append('\u0001');
            
            if (!string.IsNullOrEmpty(SecondaryClOrdID))
            {
                sb.Append("526=").Append(SecondaryClOrdID).Append('\u0001');
            }

            sb.Append("530=").Append(MassCancelRequestType).Append('\u0001');
            if (MassCancelRequestType == "1")
            {
                sb.Append("336=").Append(TradingSessionID).Append('\u0001');
                sb.Append("55=").Append(Symbol).Append('\u0001');
            }
            sb.Append("60=").Append(TransactTime).Append('\u0001');
            sb.Append("1=").Append(Account).Append('\u0001');
            sb.Append("453=").Append(NoPartyID).Append('\u0001');
            sb.Append("448=").Append(PartyID).Append('\u0001');
            sb.Append("447=").Append(PartyIDSource).Append('\u0001');
            sb.Append("452=").Append(PartyRole).Append('\u0001');

            return sb.ToString();
        }
    }
}
