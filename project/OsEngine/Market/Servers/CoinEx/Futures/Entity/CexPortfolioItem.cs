using OsEngine.Entity;

namespace OsEngine.Market.Servers.CoinEx.Futures.Entity
{
    /*
        https://docs.coinex.com/api/v2/assets/balance/http/get-futures-balance#http-request
    */
    struct CexPortfolioItem
    {
        // Currency name [USDT]
        public string ccy { get; set; }

        /// <summary>
        /// Balance available.
        /// Balance available = Account balance - frozen margin
        /// Account balance = Incoming transfer - outgoing transfer + realized PNL - (position margin - unrealized PNL)
        /// </summary>
        public string available { get; set; }

        /// <summary>
        /// Frozen balance. The frozen initial margin and trading fees, when the current order cannot be executed immediately
        /// </summary>
        public string frozen { get; set; }

        /// <summary>
        /// Position margin. The margin used and locked by the current position.
        /// Position margin = Initial margin + added margin - reduced margin + unrealized PNL + settled PNL
        /// </summary>
        public string margin { get; set; }

        /// <summary>
        /// Balance available for transfers.
        /// Transferable balance = Balance available - balance to be settled
        /// </summary>
        public string transferrable { get; set; }

        /// <summary>
        /// Unrealized profit. The current PNL of open positions, estimated at the Mark Price or the Latest Price.
        /// </summary>
        public string unrealized_pnl { get; set; }

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
