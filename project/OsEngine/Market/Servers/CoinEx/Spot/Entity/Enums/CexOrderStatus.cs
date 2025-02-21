using System;

namespace OsEngine.Market.Servers.CoinEx.Spot.Entity.Enums
{
    /**
     * https://docs.coinex.com/api/v2/enum#order_status
     */

    public sealed class CexOrderStatus
    {

        private readonly String name;
        private readonly int value;
        public static readonly CexOrderStatus OPEN = new CexOrderStatus(1, "open"); // the order is placed and pending execution
        public static readonly CexOrderStatus PART_FILLED = new CexOrderStatus(1, "part_filled"); // order partially executed (still pending)
        public static readonly CexOrderStatus FILLED = new CexOrderStatus(1, "filled"); // order fully executed (completed)
        public static readonly CexOrderStatus PART_CANCELED = new CexOrderStatus(1, "part_canceled"); // order partially executed and then canceled
        public static readonly CexOrderStatus CANCELED = new CexOrderStatus(1, "canceled"); // the order is canceled; to maintain server performance, any canceled orders without execution will not be saved

        private CexOrderStatus(int value, String name)
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
