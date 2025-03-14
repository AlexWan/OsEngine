using OsEngine.Entity;

namespace OsEngine.Market.Servers.CoinEx.Futures.Entity
{
    struct CexWsBalance
    {
        public CexWsBalanceItem[] balance_list { get; set; }
    }

    struct CexWsBalanceItem
    {
        // Asset name [BTC]
        public string ccy { get; set; }

        // Balance available [44.62207740]
        public string available { get; set; }

        // Frozen balance
        public string frozen { get; set; }

        // Margin balance
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

        /// <summary>
        /// Equity
        /// </summary>
        public string equity { get; set; }
    }
}