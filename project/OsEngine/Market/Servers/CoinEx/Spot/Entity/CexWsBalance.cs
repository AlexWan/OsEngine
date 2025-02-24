using OsEngine.Entity;

namespace OsEngine.Market.Servers.CoinEx.Spot.Entity
{
    struct CexWsBalance
    {
        public CexWsBalanceItem[] balance_list { get; set; }
    }

    struct CexWsBalanceItem
    {
        // Margin account name, null for non-margin markets [BTCUSDT]
        public string margin_market { get; set; }

        // Currency Asset name [BTC]
        public string ccy { get; set; }

        // Balance available [44.62207740]
        public string available { get; set; }

        // Frozen balance
        public string frozen { get; set; }

        public long updated_at { get; set; }
    }
}