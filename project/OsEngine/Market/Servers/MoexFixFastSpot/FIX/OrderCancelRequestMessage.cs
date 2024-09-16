using System.Text;

namespace OsEngine.Market.Servers.MoexFixFastSpot.FIX
{    
    class OrderCancelRequestMessage: AFIXMessageBody
    {
        public string OrigClOrdID;
        public string OrderID = "";
        public string ClOrdID;
        public string Side;
        public string TransactTime;

        public override string ToString()
        {
            StringBuilder sb = new StringBuilder();

            sb.Append("41=").Append(OrigClOrdID).Append('\u0001');
            if (OrderID != "")
            {
                sb.Append("37=").Append(OrderID).Append('\u0001');
            }
            sb.Append("11=").Append(ClOrdID).Append('\u0001');
            sb.Append("54=").Append(Side).Append('\u0001');
            sb.Append("60=").Append(TransactTime).Append('\u0001');

            return sb.ToString();
        }
    }
}
