using System;

namespace OsEngine.Market.Servers.CoinEx.Futures.Entity.Enums
{
    /**
     * https://docs.coinex.com/api/v2/enum#order_type
     */

    public sealed class CexOrderType
    {

        private readonly String name;
        private readonly int value;

        // Limit order (Always Valid, Good Till Cancel)
        public static readonly CexOrderType LIMIT = new CexOrderType(1, "limit");

        // Market order
        public static readonly CexOrderType MARKET = new CexOrderType(1, "market");

        // Maker only order (Post only order)
        public static readonly CexOrderType MAKER_ONLY = new CexOrderType(1, "maker_only");

        // Immediate or Cancel
        public static readonly CexOrderType IOC = new CexOrderType(1, "ioc");
        
        // Fill or Kill
        public static readonly CexOrderType FOK = new CexOrderType(1, "fok");

        private CexOrderType(int value, String name)
        {
            this.name = name;
            this.value = value;
        }

        public override String ToString()
        {
            return name;
        }

    }
}
