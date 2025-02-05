using OsEngine.Entity;

namespace OsEngine.Market.Servers.CoinEx.Futures.Entity
{
    /*
        https://docs.coinex.com/api/v2/assets/balance/http/get-spot-balance#http-request
    */
    struct CexPortfolioItem
    {
        // Currency name [USDT]
        public string ccy { get; set; }

        // Balance available
        public string available { get; set; }

        // Frozen balance. Unavailable asset balances, including frozen assets in orders and other cases
        public string frozen { get; set; }

        public static explicit operator PositionOnBoard(CexPortfolioItem cexPortfolioItem)
        {
            return new PositionOnBoard()
            {
                PortfolioName = "",
                SecurityNameCode = cexPortfolioItem.ccy,
                ValueBlocked = cexPortfolioItem.frozen.ToString().ToDecimal(),
                ValueCurrent = cexPortfolioItem.available.ToString().ToDecimal()
            };
        }
    }
}
