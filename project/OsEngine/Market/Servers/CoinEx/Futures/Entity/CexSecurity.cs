using OsEngine.Entity;
using OsEngine.OsData;
using System;

namespace OsEngine.Market.Servers.CoinEx.Futures.Entity
{
    /*
        https://docs.coinex.com/api/v2/futures/market/http/list-market#http-request
    */
    struct CexSecurity
    {
        // ETHUSDT
        public string market { get; set; }

        // [linear|inverse]
        // https://docs.coinex.com/api/v2/enum#contract_type
        public string contract_type { get; set; }

        // ETH
        public string base_ccy { get; set; }

        // 8
        public long base_ccy_precision { get; set; }

        // USDT
        public string quote_ccy { get; set; }

        // 2
        public long quote_ccy_precision { get; set; }

        // 0.002
        public string maker_fee_rate { get; set; }

        // 0.002
        public string taker_fee_rate { get; set; }

        // Min. transaction volume
        // Amount - объём в единицах тикера
        // Value - объём в деньгах
        public string min_amount { get; set; }

        // Tick size
        public string tick_size { get; set; }

        // Leverage ["3", "5", "8", "10", "15", "20", "30", "50", "100"]
        public int[] leverage { get; set; }

        // Futures position size
        public string open_interest_volume { get; set; }

        // Whether the market is open
        public bool is_market_available { get; set; }

        // Whether to enable copy trading
        public bool is_copy_trading_available { get; set; }
    }
}
