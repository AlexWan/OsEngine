using System;

namespace OsEngine.Market.Servers.CoinEx.Spot.Entity.Enums
{
    /**
     * https://docs.coinex.com/api/v2/enum#trigger_price_type
     */

    public sealed class CexPriceType
    {

        private readonly String name;
        private readonly int value;

        public static readonly CexPriceType LATEST_PRICE = new CexPriceType(1, "latest_price"); // current market price
        public static readonly CexPriceType MARK_PRICE = new CexPriceType(1, "mark_price"); // mark price  [Futures market]
        public static readonly CexPriceType INDEX_PRICE = new CexPriceType(1, "index_price"); // index price [Futures market]

        private CexPriceType(int value, String name)
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
