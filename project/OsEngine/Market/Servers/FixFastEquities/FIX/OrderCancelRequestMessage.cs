
namespace OsEngine.Market.Servers.FixFastEquities.FIX
{    
    class OrderCancelRequestMessage
    {
        public string OrigClOrdID;
        public string OrderID;
        public string ClOrdID;
        public string Side;
        public string TransactTime;


        public override string ToString()
        {
            return $"41={OrigClOrdID}\u000137={OrderID}\u000111={ClOrdID}\u000154={Side}\u000160={TransactTime}\u0001";
        }

        public int GetMessageSize()
        {
            return ToString().Length;
        }
    }
}
