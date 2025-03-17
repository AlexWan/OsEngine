using System;

namespace OsEngine.Market.Servers.CoinEx.Futures.Entity.Enums
{
    /**
     * https://docs.coinex.com/api/v2/enum#market_type
     */
    public sealed class CexMarketType
    {

        private readonly String name;
        private readonly int value;

        // For Spot market
        public static readonly CexMarketType SPOT = new CexMarketType(1, "SPOT");
        // For Spot market
        public static readonly CexMarketType MARGIN = new CexMarketType(2, "MARGIN");
        // For Futures market
        public static readonly CexMarketType FUTURES = new CexMarketType(3, "FUTURES");

        private CexMarketType(int value, String name)
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
