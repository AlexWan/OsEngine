using System.Collections.Generic;

namespace OsEngine.Market.Servers.KiteConnect.Json
{
    public class ResponseRestKite<T>
    {
        public string status { get; set; }
        public string message { get; set; }
        public string error_type { get; set; }
        public T data { get; set; }
    }

    public class UserProfile
    {
        public string user_id { get; set; } // The unique, permanent user id registered with the broker and the exchanges
        public string user_name { get; set; } // User's real name
        public string user_shortname { get; set; } // Shortened version of the user's real name
        public string email { get; set; } // User's email
        public string user_type { get; set; } // User's registered role at the broker. This will be individual for all retail users
        public string broker { get; set; } // The broker ID
        public List<string> exchanges { get; set; } // Exchanges enabled for trading on the user's account
        public List<string> products { get; set; } // Margin product types enabled for the user
        public List<string> order_types { get; set; } // Order types enabled for the user
        public Meta meta { get; set; } // demat_consent: empty, consent or physical
        public string avatar_url { get; set; } // Full URL to the user's avatar (PNG image) if there's one
    }

    public class Meta
    {
        public string demat_consent { get; set; } // demat_consent: empty, consent or physical
    }

    public class UserMargins
    {
        public Equity equity { get; set; } // 
        public Commodity commodity { get; set; } // 
    }

    public class Equity
    {
        public string enabled { get; set; } // Indicates whether the segment is enabled for the user
        public string net { get; set; } // Net cash balance available for trading (intraday_payin + adhoc_margin + collateral)
        public Available available { get; set; } // 
        public Utilised utilised { get; set; } // 
    }

    public class Commodity
    {
        public string enabled { get; set; } // Indicates whether the segment is enabled for the user
        public string net { get; set; } // Net cash balance available for trading (intraday_payin + adhoc_margin + collateral)
        public Available available { get; set; } // 
        public Utilised utilised { get; set; } // 
    }

    public class Available
    {
        public string adhoc_margin { get; set; } // Additional margin provided by the broker
        public string cash { get; set; } // Raw cash balance in the account available for trading (also includes intraday_payin)
        public string opening_balance { get; set; } // Opening balance at the day start
        public string live_balance { get; set; } // Current available balance
        public string collateral { get; set; } // Margin derived from pledged stocks
        public string intraday_payin { get; set; } // Amount that was deposited during the day
    }

    public class Utilised
    {
        public string debits { get; set; } // Sum of all utilised margins (unrealised M2M + realised M2M + SPAN + Exposure + Premium + Holding sales)
        public string exposure { get; set; } // Exposure margin blocked for all open F&O positions
        public string m2m_realised { get; set; } // Booked intraday profits and losses
        public string m2m_unrealised { get; set; } // Un-booked (open) intraday profits and losses
        public string option_premium { get; set; } // Value of options premium received by shorting
        public string payout { get; set; } // Funds paid out or withdrawn to bank account during the day
        public string span { get; set; } // SPAN margin blocked for all open F&O positions
        public string holding_sales { get; set; } // Value of holdings sold during the day
        public string turnover { get; set; } // Utilised portion of the maximum turnover limit (only applicable to certain clients)
        public string liquid_collateral { get; set; } // Margin utilised against pledged liquidbees ETFs and liquid mutual funds
        public string stock_collateral { get; set; } // Margin utilised against pledged stocks/ETFs
        public string delivery { get; set; } // Margin blocked when you sell securities (20% of the value of stocks sold) from your demat or T1 holdings
    }

    public class UserAuthentication
    {
        public string user_id { get; set; } // The unique, permanent user id registered with the broker and the exchanges
        public string user_name { get; set; } // User's real name
        public string user_type { get; set; } // User's registered role at the broker. This will be individual for all retail users
        public string email { get; set; } // User's email
        public string user_shortname { get; set; } // Shortened version of the user's real name
        public string broker { get; set; } // The broker ID
        public List<string> exchanges { get; set; } // Exchanges enabled for trading on the user's account
        public List<string> products { get; set; } // Margin product types enabled for the user
        public List<string> order_types { get; set; } // Order types enabled for the user
        public string avatar_url { get; set; } // Full URL to the user's avatar (PNG image) if there's one
        public string api_key { get; set; } // The API key for which the authentication was performed
        public string access_token { get; set; } // The authentication token that's used with every subsequent request Unless this is invalidated using the API,
                                                 // or invalidated by a master-logout from the Kite Web trading terminal,
                                                 // it'll expire at 6 AM on the next day (regulatory requirement)
        public string public_token { get; set; } // A token for public session validation where requests may be exposed to the public
        public string enctoken { get; set; } // 
        public string refresh_token { get; set; } // A token for getting long standing read permissions. This is only available to certain approved platforms
        public string silo { get; set; } // 
        public string login_time { get; set; } // User's last login time
        public Meta meta { get; set; } // demat_consent: empty, consent or physical
    }

    public class HistoricalCandles
    {
        public List<string[]> candles { get; set; }
    }

    public class OrderResponse
    {
        public string user_id { get; set; }
        public string unfilled_quantity { get; set; }
        public string app_id { get; set; }
        public string checksum { get; set; }
        public string placed_by { get; set; }
        public string order_id { get; set; }
        public string exchange_order_id { get; set; }
        public string parent_order_id { get; set; }
        public string status { get; set; }
        public string status_message { get; set; }
        public string status_message_raw { get; set; }
        public string order_timestamp { get; set; }
        public string exchange_update_timestamp { get; set; }
        public string exchange_timestamp { get; set; }
        public string variety { get; set; }
        public string exchange { get; set; }
        public string tradingsymbol { get; set; }
        public string instrument_token { get; set; }
        public string order_type { get; set; }
        public string transaction_type { get; set; }
        public string validity { get; set; }
        public string product { get; set; }
        public string quantity { get; set; }
        public string disclosed_quantity { get; set; }
        public string price { get; set; }
        public string trigger_price { get; set; }
        public string average_price { get; set; }
        public string filled_quantity { get; set; }
        public string pending_quantity { get; set; }
        public string cancelled_quantity { get; set; }
        public string market_protection { get; set; }
        //public string meta { get; set; }
        public string tag { get; set; }
        public string guid { get; set; }
    }
    public class TradeResponse
    {
        public string trade_id { get; set; }
        public string order_id { get; set; }
        public string exchange { get; set; }
        public string tradingsymbol { get; set; }
        public string instrument_token { get; set; }
        public string product { get; set; }
        public string average_price { get; set; }
        public string quantity { get; set; }
        public string exchange_order_id { get; set; }
        public string transaction_type { get; set; }
        public string fill_timestamp { get; set; }
        public string order_timestamp { get; set; }
        public string exchange_timestamp { get; set; }
    }

    public class HoldingsPortfolio
    {
        public string tradingsymbol { get; set; }
        public string exchange { get; set; }
        public string instrument_token { get; set; }
        public string isin { get; set; }
        public string product { get; set; }
        public string price { get; set; }
        public string quantity { get; set; }
        public string used_quantity { get; set; }
        public string t1_quantity { get; set; }
        public string realised_quantity { get; set; }
        public string authorised_quantity { get; set; }
        public string authorised_date { get; set; }
        public string opening_quantity { get; set; }
        public string collateral_quantity { get; set; }
        public string collateral_type { get; set; }
        public string discrepancy { get; set; }
        public string average_price { get; set; }
        public string last_price { get; set; }
        public string close_price { get; set; }
        public string pnl { get; set; }
        public string day_change { get; set; }
        public string day_change_percentage { get; set; }
    }

    public class ResponsePositions
    {
        public List<NetPosition> net { get; set; }
        public List<DayPosition> day { get; set; }
    }

    public class NetPosition
    {
        public string tradingsymbol { get; set; }
        public string exchange { get; set; }
        public string instrument_token { get; set; }
        public string product { get; set; }
        public string quantity { get; set; }
        public string overnight_quantity { get; set; }
        public string multiplier { get; set; }
        public string average_price { get; set; }
        public string close_price { get; set; }
        public string last_price { get; set; }
        public string value { get; set; }
        public string pnl { get; set; }
        public string m2m { get; set; }
        public string unrealised { get; set; }
        public string realised { get; set; }
        public string buy_quantity { get; set; }
        public string buy_price { get; set; }
        public string buy_value { get; set; }
        public string buy_m2m { get; set; }
        public string sell_quantity { get; set; }
        public string sell_price { get; set; }
        public string sell_value { get; set; }
        public string sell_m2m { get; set; }
        public string day_buy_quantity { get; set; }
        public string day_buy_price { get; set; }
        public string day_buy_value { get; set; }
        public string day_sell_quantity { get; set; }
        public string day_sell_price { get; set; }
        public string day_sell_value { get; set; }
    }

    public class DayPosition
    {
        public string tradingsymbol { get; set; }
        public string exchange { get; set; }
        public string instrument_token { get; set; }
        public string product { get; set; }
        public string quantity { get; set; }
        public string overnight_quantity { get; set; }
        public string multiplier { get; set; }
        public string average_price { get; set; }
        public string close_price { get; set; }
        public string last_price { get; set; }
        public string value { get; set; }
        public string pnl { get; set; }
        public string m2m { get; set; }
        public string unrealised { get; set; }
        public string realised { get; set; }
        public string buy_quantity { get; set; }
        public string buy_price { get; set; }
        public string buy_value { get; set; }
        public string buy_m2m { get; set; }
        public string sell_quantity { get; set; }
        public string sell_price { get; set; }
        public string sell_value { get; set; }
        public string sell_m2m { get; set; }
        public string day_buy_quantity { get; set; }
        public string day_buy_price { get; set; }
        public string day_buy_value { get; set; }
        public string day_sell_quantity { get; set; }
        public string day_sell_price { get; set; }
        public string day_sell_value { get; set; }
    }
}
