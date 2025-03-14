using System;

namespace OsEngine.Market.Servers.CoinEx.Futures.Entity.Enums
{
    /**
     * https://docs.coinex.com/api/v2/enum#order_side
     */

    public sealed class CexOrderSide
    {

        private readonly String name;
        private readonly int value;
        public static readonly CexOrderSide BUY = new CexOrderSide(1, "buy");
        public static readonly CexOrderSide SELL = new CexOrderSide(1, "sell");

        private CexOrderSide(int value, String name)
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
