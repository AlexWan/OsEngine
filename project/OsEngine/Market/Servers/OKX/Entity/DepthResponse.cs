using System.Collections.Generic;

namespace OsEngine.Market.Servers.OKX.Entity
{
    public class DepthResponse
    {
        public DepthResponseArg arg;
        //public string action;
        public List<DepthResponseData> data;

    }

    public class DepthResponseData
    {
        public List<string[]> asks;
        public List<string[]> bids;
        public string ts;
        //public int checksum;
    }

    public class DepthResponseArg
    {
        public string channel;
        public string instId;
    }

}
