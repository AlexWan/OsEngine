using System.Collections.Generic;


namespace OsEngine.Market.Servers.OKX.Entity
{
    public class PorfolioResponse
    {
        public string code;

        public List<PorfolioData> data;

        public string msg;
    }

    public class PorfolioData
    {
        public string adjEq;
        public List<PortdolioDetails> details;
        public string imr;
        public string isoEq;
        public string mgnRatio;
        public string mmr;
        public string notionalUsd;
        public string ordFroz;
        public string totalEq;
        public string uTime;
        public string msg;
    }



    public class PortdolioDetails
    {
        public string availBal;
        public string availEq;
        public string cashBal;
        public string ccy;
        public string crossLiab;
        public string disEq;
        public string eq;
        public string eqUsd;
        public string fixedBal;
        public string frozenBal;
        public string interest;
        public string isoEq;
        public string isoLiab;
        public string liab;
        public string mgnRatio;
        public string notionalLever;
        public string ordFrozen;
        public string spotInUseAmt;
        public string stgyEq;
        public string twap;
        public string uTime;
        public string upl;
        public string uplLiab;
    }
}
