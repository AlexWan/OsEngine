using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OsEngine.Market.Servers.BitMex.BitMexEntity
{
    public class DatumOrder
    {
        public string orderID { get; set; }
        public string clOrdID { get; set; }
        public string clOrdLinkID { get; set; }
        public int account { get; set; }
        public string symbol { get; set; }
        public string side { get; set; }
        public object simpleOrderQty { get; set; }
        public int? orderQty { get; set; }
        public decimal price { get; set; }
        public object displayQty { get; set; }
        public object stopPx { get; set; }
        public object pegOffsetValue { get; set; }
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
        public bool workingIndicator { get; set; }
        public string ordRejReason { get; set; }
        public double simpleLeavesQty { get; set; }
        public int leavesQty { get; set; }
        public decimal simpleCumQty { get; set; }
        public int cumQty { get; set; }
        public object avgPx { get; set; }
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
