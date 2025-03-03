using OsEngine.Entity;

namespace OsEngine.Market.Servers.CoinEx.Spot.Entity
{
    /*
        https://docs.coinex.com/api/v2/assets/balance/http/get-spot-balance#http-request
    */
    struct CexSpotPortfolioItem
    {
        // Currency name [USDT]
        public string ccy { get; set; }

        // Balance available
        public string available { get; set; }

        // Frozen balance. Unavailable asset balances, including frozen assets in orders and other cases
        public string frozen { get; set; }

    }
}