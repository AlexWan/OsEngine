
namespace OsEngine.Market.Servers.CoinEx.Spot.Entity
{
    /*
        https://docs.coinex.com/api/v2/spot/market/http/list-market#http-request
    */
    struct CexSecurity
    {
        // ETHUSDT
        public string market { get; set; }

        // ETH
        // BaseCurrency
        public string base_ccy { get; set; }

        // BaseCurrencyDecimal 8
        public long base_ccy_precision { get; set; }

        // QuoteCurrency USDT
        public string quote_ccy { get; set; }

        // QuoteCurrencyDecimal 2
        public long quote_ccy_precision { get; set; }

        // MakerFeeRate 0.002
        public string maker_fee_rate { get; set; }

        // TakerFeeRate 0.002
        public string taker_fee_rate { get; set; }

        // MinAmountMin. transaction volume
        // Amount - объём в единицах тикера
        // Value - объём в деньгах
        public string min_amount { get; set; }

        // Whether to enable AMM function
        public bool is_amm_available { get; set; }

        // Whether to enable margin trading
        public bool is_margin_available { get; set; }

        public bool is_pre_trading_available { get; set; }

    }
}