using System.Collections.Generic;


namespace OsEngine.Market.Servers.OKX.Entity
{
    public class TradeDetailsResponce
    {
        public string code;
        public string msg;
        public List<TradeDetailsObject> data;
    }


    public class TradeDetailsObject
    {
        public string instType;
        public string instId;
        public string tradeId;
        public string ordId;
        public string clOrdId;
        public string billId;
        public string tag;
        public string fillPx;
        public string fillSz;
        public string side;
        public string posSide;
        public string execType;
        public string feeCcy;
        public string fee;
        public string ts;

    }
}
