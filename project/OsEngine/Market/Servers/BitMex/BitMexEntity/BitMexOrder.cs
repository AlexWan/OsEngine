/*
 * Your rights to use the code are governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System.Collections.Generic;

namespace OsEngine.Market.Servers.BitMex.BitMexEntity
{
    public class DatumOrder
    {
        public string orderID { get; set; }
        public string clOrdID { get; set; }
        public string clOrdLinkID { get; set; }
        public string account { get; set; }
        public string symbol { get; set; }
        public string side { get; set; }
        public string simpleOrderQty { get; set; }
        public string orderQty { get; set; }
        public string price { get; set; }
        public string displayQty { get; set; }
        public string stopPx { get; set; }
        public string pegOffsetValue { get; set; }
        public string pegPriceType { get; set; }
        public string currency { get; set; }
        public string settlCurrency { get; set; }
        public string ordType { get; set; }
        public string timeInForce { get; set; }
        public string execInst { get; set; }
        public string contingencyType { get; set; }
        public string exDestination { get; set; }
        public string ordStatus { get; set; }
        public string triggered { get; set; }
        public string workingIndicator { get; set; }
        public string ordRejReason { get; set; }
        public string simpleLeavesQty { get; set; }
        public string leavesQty { get; set; }
        public string simpleCumQty { get; set; }
        public string cumQty { get; set; }
        public string avgPx { get; set; }
        public string multiLegReportingType { get; set; }
        public string text { get; set; }
        public string transactTime { get; set; }
        public string timestamp { get; set; }
    }

    public class BitMexOrder
    {
        public string table { get; set; }
        public string action { get; set; }
        public List<DatumOrder> data { get; set; }
    }
}
