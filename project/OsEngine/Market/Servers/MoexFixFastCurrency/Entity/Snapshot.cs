using OsEngine.Entity;
using System;
using System.Collections.Generic;


namespace OsEngine.Market.Servers.MoexFixFastCurrency.Entity
{
    public class Snapshot
    {
        public string Symbol { get; set; }
        public string TradingSessionID { get; set; }
        public int RptSeq { get; set; }
        public List<SnapshotFragment> SnapshotFragments { get; set; }
        public bool SnapshotWasApplied { get; set; } = false;

        public bool IsComletedSnapshot(List<SnapshotFragment> fragments)
        {
            fragments.Sort((x, y) => x.MsgSeqNum.CompareTo(y.MsgSeqNum));

            if ((fragments[0].RouteFirst == true && fragments[fragments.Count - 1].LastFragment == true) &&
               (fragments[0].RptSeq == fragments[fragments.Count - 1].RptSeq))
            {
                return true;
            }
            return false;
        }
    }

    public class SnapshotFragment
    {
        public long MsgSeqNum { get; set; }
        public int RptSeq { get; set; }
        public string TradingSessionID { get; set; }
        public string Symbol { get; set; }
        public bool LastFragment { get; set; }
        public bool RouteFirst { get; set; }
        public List<Trade> trades;
        public List<MarketDepthLevel> mdLevel;
    }
}
