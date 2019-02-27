/*
 *Your rights to use the code are governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 *Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System.Collections.Generic;

namespace OsEngine.Market.Servers.BitMex.BitMexEntity
{
    public class Types
    {
        public string account { get; set; }
        //public string currency { get; set; }
        //public string riskLimit { get; set; }
        //public string prevState { get; set; }
        //public string state { get; set; }
        //public string action { get; set; }
        //public string amount { get; set; }
        //public string pendingCredit { get; set; }
        //public string pendingDebit { get; set; }
        //public string confirmedDebit { get; set; }
        //public string prevRealisedPnl { get; set; }
        //public string prevUnrealisedPnl { get; set; }
        //public string grossComm { get; set; }
        //public string grossOpenCost { get; set; }
        //public string grossOpenPremium { get; set; }
        //public string grossExecCost { get; set; }
        //public string grossMarkValue { get; set; }
        //public string riskValue { get; set; }
        //public string taxableMargin { get; set; }
        //public string initMargin { get; set; }
        //public string maintMargin { get; set; }
        //public string sessionMargin { get; set; }
        //public string targetExcessMargin { get; set; }
        //public string varMargin { get; set; }
        //public string realisedPnl { get; set; }
        //public string unrealisedPnl { get; set; }
        //public string indicativeTax { get; set; }
        public string unrealisedProfit { get; set; }
        //public string syntheticMargin { get; set; }
        public string walletBalance { get; set; }
        public string marginBalance { get; set; }
        //public string marginBalancePcnt { get; set; }
        //public string marginLeverage { get; set; }
        //public string marginUsedPcnt { get; set; }
        //public string excessMargin { get; set; }
        //public string excessMarginPcnt { get; set; }
        public string availableMargin { get; set; }
        //public string withdrawableMargin { get; set; }
        //public string timestamp { get; set; }
        //public string grossLastValue { get; set; }
        //public string commission { get; set; }
    }

    public class ForeignKeys
    {
    }

    public class Attributes
    {
        public string account { get; set; }
        public string currency { get; set; }
    }

    public class Datum
    {
        public int account { get; set; }
        //public string currency { get; set; }
        //public long riskLimit { get; set; }
        //public string prevState { get; set; }
        //public string state { get; set; }
        //public string action { get; set; }
        //public int amount { get; set; }
        //public int pendingCredit { get; set; }
        //public int pendingDebit { get; set; }
        //public int confirmedDebit { get; set; }
        //public int prevRealisedPnl { get; set; }
        //public int prevUnrealisedPnl { get; set; }
        //public int grossComm { get; set; }
        //public int grossOpenCost { get; set; }
        //public int grossOpenPremium { get; set; }
        //public int grossExecCost { get; set; }
        //public int grossMarkValue { get; set; }
        //public int riskValue { get; set; }
        //public int taxableMargin { get; set; }
        //public int initMargin { get; set; }
        //public int maintMargin { get; set; }
        //public int sessionMargin { get; set; }
        //public int targetExcessMargin { get; set; }
        //public int varMargin { get; set; }
        //public int realisedPnl { get; set; }
        public int unrealisedPnl { get; set; }
        //public int indicativeTax { get; set; }
        public decimal unrealisedProfit { get; set; }
        //public int syntheticMargin { get; set; }
        public decimal walletBalance { get; set; }
        public decimal marginBalance { get; set; }
        //public int marginBalancePcnt { get; set; }
        //public int marginLeverage { get; set; }
        //public int marginUsedPcnt { get; set; }
        //public int excessMargin { get; set; }
        //public int excessMarginPcnt { get; set; }
        public decimal availableMargin { get; set; }
        //public int withdrawableMargin { get; set; }
        //public string timestamp { get; set; }
        //public int grossLastValue { get; set; }
        //public object commission { get; set; }
    }

    public class Filter
    {
        public int account { get; set; }
    }

    public class BitMexPortfolio
    {
        public string table { get; set; }
        public List<string> keys { get; set; }
        public Types types { get; set; }
        public ForeignKeys foreignKeys { get; set; }
        public Attributes attributes { get; set; }
        public string action { get; set; }
        public List<Datum> data { get; set; }
        public Filter filter { get; set; }
    }
}
