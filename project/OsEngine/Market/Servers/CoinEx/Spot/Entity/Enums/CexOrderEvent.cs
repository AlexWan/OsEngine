using System;

namespace OsEngine.Market.Servers.CoinEx.Spot.Entity.Enums
{
    /**
     * https://docs.coinex.com/api/v2/enum#trigger_price_type
     */

    public sealed class CexOrderEvent
    {

        private readonly String name;
        private readonly int value;

        // Order placed successfully (unfilled/partially filled)
        public static readonly CexOrderEvent PUT = new CexOrderEvent(1, "put");

        // Order updated (partially filled)
        public static readonly CexOrderEvent UPDATE = new CexOrderEvent(1, "update");

        // Order modified successfully (unfilled/partially filled)
        public static readonly CexOrderEvent MODIFY = new CexOrderEvent(1, "modify");

        // Order completed (filled or canceled)
        public static readonly CexOrderEvent FINISH = new CexOrderEvent(1, "finish");

        private CexOrderEvent(int value, String name)
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
