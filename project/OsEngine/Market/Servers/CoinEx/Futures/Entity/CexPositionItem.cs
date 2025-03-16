using OsEngine.Entity;

namespace OsEngine.Market.Servers.CoinEx.Futures.Entity
{
    /*
        https://docs.coinex.com/api/v2/futures/position/http/list-pending-position
    */
    struct CexPositionItem
    {
        // Position ID
        public long position_id { get; set; }

        // Market name
        public string market { get; set; }

        // Market Type
        // Rest request only
        public string market_type { get; set; }

        // Position side
        public string side { get; set; }

        // Margin Mode [isolated, cross]
        public string margin_mode { get; set; }

        // open_interest
        public string open_interest { get; set; }

        // Amount that's available for position closing.
        // Remaining amount that's available for position closing.
        public string close_avbl { get; set; }

        // 	ATH position amount
        public string ath_position_amount { get; set; }

        // Unrealized profit.The current PNL of open positions,
        // estimated at the Mark Price or the Latest Price.
        public string unrealized_pnl { get; set; }

        // Realized PNL
        public string realized_pnl { get; set; }

        // Average entry price
        public string avg_entry_price { get; set; }

        // Cumulative position value
        public string cml_position_value { get; set; }

        // Max. position value
        public string max_position_value { get; set; }

        // Take-profit price
        public string take_profit_price { get; set; }

        // Stop-loss price
        public string stop_loss_price { get; set; }

        // Take profit trigger price type.On CoinEx,
        // you can choose different trigger price types
        // when setting "TP/SL" (Take Profit and Stop Loss),
        // using "Latest Price" or "Mark Price" as the trigger price.
        // [latest_price, mark_price]
        public string take_profit_type { get; set; }

        // Stop loss trigger price type.On CoinEx,
        // you can choose different trigger price types
        // when setting "TP/SL" (Take Profit and Stop Loss),
        // using "Latest Price" or "Mark Price" as the trigger price.
        // [latest_price, mark_price]
        public string stop_loss_type { get; set; }

        // Leverage
        public string leverage { get; set; }

        // Margin available.
        // Current margin amount = Initial margin + added margin - reduced margin
        public string margin_avbl { get; set; }
        
        // ATH margin amount
        public string ath_margin_size { get; set; }

        // Position margin rate
        public string position_margin_rate { get; set; }

        // Maintenance margin rate
        public string maintenance_margin_rate { get; set; }
        
        // Maintenance margin value
        public string maintenance_margin_value { get; set; }

        // Liquidation price
        public string liq_price { get; set; }

        // Bankruptcy price
        public string bkr_price { get; set; }

        // ADL (Auto-deleveraging) risk level,
        // a number in the range of [1, 5].
        // The smaller the number, the lower the risk level,
        // and vice versa.
        public int adl_level { get; set; }

        // Settlement price, calculated as mark price
        public string settle_price { get; set; }

        // Settlement value, calculated as mark price
        public string settle_value { get; set; }

        // The first filled price of the position
        // WS Update only
        public string first_filled_price { get; set; }

        // The latest filled price of the position
        // WS Update only
        public string latest_filled_price { get; set; }

        // Order creation time
        public long created_at { get; set; }

        // Order update time
        public long updated_at { get; set; }
    }
}
