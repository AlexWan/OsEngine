using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OsEngine.Market.Servers.CoinEx.Spot.Entity.Enums
{
    /**
     * https://docs.coinex.com/api/v2/enum#position_side
     */

    public sealed class CexPositionSide
    {

        private readonly String name;
        private readonly int value;

        public static readonly CexPositionSide SHORT = new CexPositionSide(1, "short"); // short position
        public static readonly CexPositionSide LONG = new CexPositionSide(2, "long"); // long position

        private CexPositionSide(int value, String name)
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
