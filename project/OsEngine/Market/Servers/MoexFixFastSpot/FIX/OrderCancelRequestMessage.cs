
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
            string OrderIdString = OrderID == "" ? "" : $"37={OrderID}\u0001";
            return $"41={OrigClOrdID}\u0001{OrderIdString}11={ClOrdID}\u000154={Side}\u000160={TransactTime}\u0001";
        }
    }
}
