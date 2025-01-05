using System.Collections.Generic;


namespace OsEngine.Market.Servers.YahooFinance.Entity
{
    public class SecurityResponce
    {
        public string code;
        public List<SecurityResponceItem> data;
    }


    public class SecurityResponceItem
    {
        public string alias;
        public string baseCcy;
        public string category;
        public string ctMult;
        public string ctType;
        public string ctVal;
        public string ctValCcy;
        public string expTime;
        public string instId;
        public string instType;
        public string lever;
        public string listTime;
        public string lotSz;
        public string maxIcebergSz;
        public string maxLmtSz;
        public string maxMktSz;
        public string maxStopSz;
        public string maxTriggerSz;
        public string maxTwapSz;
        public string minSz;
        public string optType;
        public string quoteCcy;
        public string settleCcy;
        public string state;
        public string stk;
        public string tickSz;
        public string uly;
    }

}
