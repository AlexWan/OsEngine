using OsEngine.Entity;

namespace OsEngine.Market.Servers.CoinEx.Spot.Entity
{
    /*
        https://docs.coinex.com/api/v2/assets/balance/http/get-spot-balance#http-request
    */
    struct CexMarginPortfolioItem
    {
        // Margin Account.Margin market name [CETUSDT]
        public string margin_account { get; set; }

        // Base currency name [CET]
        public string base_ccy { get; set; }

        // Quote currency name [USDT]
        public string quote_ccy { get; set; }

        // Balance available
        public CexMarginPortfolioBalanceItem available { get; set; }

        // Frozen balance. Unavailable asset balances, including frozen assets in orders and other cases
        public CexMarginPortfolioBalanceItem frozen { get; set; }

        // To-be-repaid amount.The amount of borrowed currency to be repaid.
        public CexMarginPortfolioBalanceItem repaid { get; set; }

        // Interest amount.The amount of interest to be repaid.
        public CexMarginPortfolioBalanceItem interest { get; set; }

        // Current risk rate. When the risk rate is 0, return an empty string
        public string risk_rate { get; set; }

        // Liquidation price. When there is no forced liquidation, return an empty strin
        public string liq_price { get; set; }

        //public static explicit operator PositionOnBoard(CexSpotPortfolioItem cexPortfolioItem)
        //{
        //    return new PositionOnBoard()
        //    {
        //        PortfolioName = "",
        //        SecurityNameCode = cexPortfolioItem.Name,
        //        ValueBlocked = cexPortfolioItem.Frozen.ToDecimal(),
        //        ValueCurrent = cexPortfolioItem.Available.ToDecimal()
        //    };
        //}
    }

    struct CexMarginPortfolioBalanceItem
    {
        // Base currency value
        public string base_ccy { get; set; }

        // Quote currency value
        public string quote_ccy { get; set; }
    }
}
