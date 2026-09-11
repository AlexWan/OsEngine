using System.Collections.Generic;


namespace OsEngine.Market.Servers.OKX.Entity
{
    public class SecurityResponse
    {
        public string code { get; set; }
        public List<SecurityResponseItem> data { get; set; }
    }

    public class SecurityResponseItem
    {
        public string alias { get; set; }
        public string baseCcy { get; set; }
        public string category { get; set; }
        public string ctMult { get; set; }
        public string ctType { get; set; }
        public string ctVal { get; set; }
        public string ctValCcy { get; set; }
        public string expTime { get; set; }
        public string instId { get; set; }
        public string instType { get; set; }
        public string lever { get; set; }
        public string listTime { get; set; }
        public string lotSz { get; set; }
        public string maxIcebergSz { get; set; }
        public string maxLmtSz { get; set; }
        public string maxMktSz { get; set; }
        public string maxStopSz { get; set; }
        public string maxTriggerSz { get; set; }
        public string maxTwapSz { get; set; }
        public string minSz { get; set; }
        public string optType { get; set; }
        public string quoteCcy { get; set; }
        public string settleCcy { get; set; }
        public string state { get; set; }
        public string stk { get; set; }
        public string tickSz { get; set; }
        public string uly { get; set; } // underlying security
    }

    public class SecurityUnderlyingResponse
    {
        public string code { get; set; }
        public List<List<string>> data { get; set; }
    }
}
