namespace OsEngine.Market.Servers.Binance.Futures.Entity
{
    public class TradesResponseReserches
    {
        public string symbol;
        public long id;
        public long orderid;
        public string side;
        public string price;
        public string qty;
        public string realizedPnl;
        public string marginAsset;
        public string quoteQty;
        public string commision;
        public string commisionAsset;
        public long time;
        public string positionSide;
        public bool maker;
        public bool buyer;
    }
}
