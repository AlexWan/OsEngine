using System.Collections.Generic;

namespace OsEngine.Market.Servers.MoexFixFastTwimeFutures.Entity
{
    public class ControlFastDepth
    {
        public ControlFastDepth()
        {
            AsksF = new List<ControlDepthLevel>();
            BidsF = new List<ControlDepthLevel>();
        }

        public List<ControlDepthLevel> AsksF;

        public List<ControlDepthLevel> BidsF;
    }

    public class ControlDepthLevel
    {
        public int ImmutabilityCount { get; set; }

        public decimal Ask;

        public decimal Bid;

        public decimal Price;
    }
}
