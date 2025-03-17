using System;

namespace OsEngine.Market.Servers.CoinExFuturesOriginal.Entity.Enums
{
    /**
     * https://docs.coinex.com/api/v2/enum#contract_type
     */
    public sealed class CexFuturesContractType
    {

        private readonly String name;
        private readonly int value;

        // For Spot market
        public static readonly CexFuturesContractType LINEAR = new CexFuturesContractType(1, "linear");
        // For Spot market
        public static readonly CexFuturesContractType INVERSE = new CexFuturesContractType(2, "inverse");

        private CexFuturesContractType(int value, String name)
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
