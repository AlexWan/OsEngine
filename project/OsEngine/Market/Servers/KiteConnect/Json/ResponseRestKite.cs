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
        public string user_id { get; set; } // ID of the user for whom the order was placed.
        public string order_id { get; set; } // Unique order ID
        public string app_id { get; set; } // Your kiteconnect app ID
        public string checksum { get; set; } // SHA-256 hash of (order_id + timestamp + api_secret)
        public string placed_by { get; set; } // ID of the user that placed the order.
                                              // This may different from the user's id for orders placed outside of Kite,
                                              // for instance, by dealers at the brokerage using dealer terminals.
        public string exchange_order_id { get; set; } // Exchange generated order id. Orders that don't reach the exchange have null ids
        public string parent_order_id { get; set; } // Order ID of the parent order (only applicable in case of multi-legged orders like CO)
        public string status { get; set; } // Current status of the order. The possible values are COMPLETE, REJECTED, CANCELLED, and UPDATE.
        public string status_message { get; set; } // Textual description of the order's status. Failed orders come with human readable explanation
        public string status_message_raw { get; set; } // Raw textual description of the failed order's status, as received from the OMS
        public string order_timestamp { get; set; } // Timestamp at which the order was registered by the API
        public string exchange_update_timestamp { get; set; } // Timestamp at which an order's state changed at the exchange
        public string exchange_timestamp { get; set; } // Timestamp at which the order was registered by the exchange.
                                                       // Orders that don't reach the exchange have null timestamps
        public string variety { get; set; } // Order variety (regular, amo, co etc.)
        public string exchange { get; set; } // Exchange
        public string tradingsymbol { get; set; } // Exchange tradingsymbol of the of the instrument
        public string instrument_token { get; set; } // The numerical identifier issued by the exchange representing the instrument
        public string order_type { get; set; } // Order type (MARKET, LIMIT etc.)
        public string transaction_type { get; set; } // BUY or SELL
        public string validity { get; set; } // Order validity
        public string product { get; set; } // Margin product to use for the order
        public string quantity { get; set; } // Quantity ordered
        public string disclosed_quantity { get; set; } // Quantity to be disclosed (may be different from actual quantity) to the public exchange orderbook. Only for equities
        public string price { get; set; } // Price at which the order was placed (LIMIT orders)
        public string trigger_price { get; set; } // Trigger price (for SL, SL-M, CO orders)
        public string average_price { get; set; } // Average price at which the order was executed (only for COMPLETE orders)
        public string filled_quantity { get; set; } // Quantity that has been filled
        public string unfilled_quantity { get; set; } // Quantity that has not filled
        public string pending_quantity { get; set; } // Pending quantity for open order
        public string cancelled_quantity { get; set; } // Quantity that had been cancelled
        public string market_protection { get; set; }
        public Meta meta { get; set; } // Map of arbitrary fields that the system may attach to an order
        public string tag { get; set; } // An optional tag to apply to an order to identify it (alphanumeric, max 20 chars)
        public string guid { get; set; }
    }
    public class TradeResponse
    {
        public string trade_id { get; set; } // Exchange generated trade ID
        public string order_id { get; set; } // Unique order ID
        public string exchange { get; set; } // Exchange
        public string tradingsymbol { get; set; } // Exchange tradingsymbol of the instrument
        public string instrument_token { get; set; } // The numerical identifier issued by the exchange representing the instrument.
                                                     // Used for subscribing to live market data over WebSocket
        public string product { get; set; } // Margin product to use for the order (margins are blocked based on this)
        public string average_price { get; set; } // Price at which the quantity was filled
        public string quantity { get; set; } // Quantity ordered
        public string exchange_order_id { get; set; } // Exchange generated order ID
        public string transaction_type { get; set; } // BUY or SELL
        public string filled { get; set; } // Filled quantity
        public string fill_timestamp { get; set; } // Timestamp at which the trade was filled at the exchange
        public string order_timestamp { get; set; } // Timestamp at which the order was registered by the API
        public string exchange_timestamp { get; set; } // Timestamp at which the order was registered by the exchange
    }

    public class HoldingsPortfolio
    {
        public string tradingsymbol { get; set; } // Exchange tradingsymbol of the instrument
        public string exchange { get; set; } // Exchange
        public string instrument_token { get; set; } // Unique instrument identifier (used for WebSocket subscriptions)
        public string isin { get; set; } // The standard ISIN representing stocks listed on multiple exchanges
        public string product { get; set; } // Margin product applied to the holding
        public string price { get; set; }
        public string quantity { get; set; } // Net quantity (T+1 + realised)
        public string used_quantity { get; set; } // Quantity sold from the net holding quantity
        public string t1_quantity { get; set; } // Quantity on T+1 day after order execution.
                                                // Stocks are usually delivered into DEMAT accounts on T+2 ?
        public string realised_quantity { get; set; } // Quantity delivered to Demat
        public string authorised_quantity { get; set; } // Quantity authorised at the depository for sale
        public string authorised_date { get; set; } // Date on which user can sell required holding stock
        public string opening_quantity { get; set; } // Quantity carried forward over night
        public string collateral_quantity { get; set; } // Quantity used as collateral
        public string collateral_type { get; set; } // Type of collateral
        public string discrepancy { get; set; } // Indicates whether holding has any price discrepancy
        public string average_price { get; set; } // Average price at which the net holding quantity was acquired
        public string last_price { get; set; } // Last traded market price of the instrument
        public string close_price { get; set; } // Closing price of the instrument from the last trading day
        public string pnl { get; set; } // Net returns on the stock; Profit and loss
        public string day_change { get; set; } // Day's change in absolute value for the stock
        public string day_change_percentage { get; set; } // Day's change in percentage for the stock
    }

    public class ResponsePositions
    {
        public List<NetPosition> net { get; set; }
        public List<DayPosition> day { get; set; }
    }

    public class NetPosition
    {
        public string tradingsymbol { get; set; } // Exchange tradingsymbol of the instrument
        public string exchange { get; set; } // Exchange
        public string instrument_token { get; set; } // The numerical identifier issued by the exchange representing the instrument.
                                                     // Used for subscribing to live market data over WebSocket
        public string product { get; set; } // Margin product applied to the position
        public string quantity { get; set; } // Quantity held
        public string overnight_quantity { get; set; } // Quantity held previously and carried forward over night
        public string multiplier { get; set; } // The quantity/lot size multiplier used for calculating P&Ls.
        public string average_price { get; set; } // Average price at which the net position quantity was acquired
        public string close_price { get; set; } // Closing price of the instrument from the last trading day
        public string last_price { get; set; } // Last traded market price of the instrument
        public string value { get; set; } // Net value of the position
        public string pnl { get; set; } // Net returns on the position; Profit and loss
        public string m2m { get; set; } // Mark to market returns (computed based on the last close and the last traded price)
        public string unrealised { get; set; } // Unrealised intraday returns
        public string realised { get; set; } // Realised intraday returns
        public string buy_quantity { get; set; } // Quantity bought and added to the position
        public string buy_price { get; set; } // Average price at which quantities were bought
        public string buy_value { get; set; } // Net value of the bought quantities
        public string buy_m2m { get; set; } // Mark to market returns on the bought quantities
        public string sell_quantity { get; set; } // Quantity sold off from the position
        public string sell_price { get; set; } // Average price at which quantities were sold
        public string sell_value { get; set; } // Net value of the sold quantities
        public string sell_m2m { get; set; } // Mark to market returns on the sold quantities
        public string day_buy_quantity { get; set; } // Quantity bought and added to the position during the day
        public string day_buy_price { get; set; } // Average price at which quantities were bought during the day
        public string day_buy_value { get; set; } // Net value of the quantities bought during the day
        public string day_sell_quantity { get; set; } // Quantity sold off from the position during the day
        public string day_sell_price { get; set; } // Average price at which quantities were sold during the day
        public string day_sell_value { get; set; } // Net value of the quantities sold during the day
    }

    public class DayPosition
    {
        public string tradingsymbol { get; set; } // Exchange tradingsymbol of the instrument
        public string exchange { get; set; } // Exchange
        public string instrument_token { get; set; } // The numerical identifier issued by the exchange representing the instrument.
                                                     // Used for subscribing to live market data over WebSocket
        public string product { get; set; } // Margin product applied to the position
        public string quantity { get; set; } // Quantity held
        public string overnight_quantity { get; set; } // Quantity held previously and carried forward over night
        public string multiplier { get; set; } // The quantity/lot size multiplier used for calculating P&Ls.
        public string average_price { get; set; } // Average price at which the net position quantity was acquired
        public string close_price { get; set; } // Closing price of the instrument from the last trading day
        public string last_price { get; set; } // Last traded market price of the instrument
        public string value { get; set; } // Net value of the position
        public string pnl { get; set; } // Net returns on the position; Profit and loss
        public string m2m { get; set; } // Mark to market returns (computed based on the last close and the last traded price)
        public string unrealised { get; set; } // Unrealised intraday returns
        public string realised { get; set; } // Realised intraday returns
        public string buy_quantity { get; set; } // Quantity bought and added to the position
        public string buy_price { get; set; } // Average price at which quantities were bought
        public string buy_value { get; set; } // Net value of the bought quantities
        public string buy_m2m { get; set; } // Mark to market returns on the bought quantities
        public string sell_quantity { get; set; } // Quantity sold off from the position
        public string sell_price { get; set; } // Average price at which quantities were sold
        public string sell_value { get; set; } // Net value of the sold quantities
        public string sell_m2m { get; set; } // Mark to market returns on the sold quantities
        public string day_buy_quantity { get; set; } // Quantity bought and added to the position during the day
        public string day_buy_price { get; set; } // Average price at which quantities were bought during the day
        public string day_buy_value { get; set; } // Net value of the quantities bought during the day
        public string day_sell_quantity { get; set; } // Quantity sold off from the position during the day
        public string day_sell_price { get; set; } // Average price at which quantities were sold during the day
        public string day_sell_value { get; set; } // Net value of the quantities sold during the day
    }
}
