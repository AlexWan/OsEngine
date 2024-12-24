using System;

namespace OsEngine.Market.Servers.KiteConnect.Json
{
    public class DepthItem
    {
        public UInt32 Quantity { get; set; }
        public decimal Price { get; set; }
        public UInt32 Orders { get; set; }
    }
}
