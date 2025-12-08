using System;
using System.Collections.Generic;


namespace OsEngine.Market.Servers.CoinEx.Spot.Entity
{
    public class ResponseRestMessage<T>
    {
        public string code { get; set; }
        public T data { get; set; }
        public string message { get; set; }
        public Pagination pagination { get; set; }
    }

    public class Pagination
    {
        public string total { get; set; }
        public string has_next { get; set; }
    }

    public class SpotMarketInfo
    {
        public string market { get; set; }
        public string taker_fee_rate { get; set; }
        public string maker_fee_rate { get; set; }
        public string min_amount { get; set; }
        public string base_ccy { get; set; }
        public string quote_ccy { get; set; }
        public string base_ccy_precision { get; set; }
        public string quote_ccy_precision { get; set; }
        public string status { get; set; }
        public string delisted_at { get; set; }
        public string is_amm_available { get; set; }
        public string is_margin_available { get; set; }
        public string is_pre_trading_available { get; set; }
        public string is_api_trading_available { get; set; }
    }

    public class ResponseCandle
    {
        public string market { get; set; }
        public string created_at { get; set; }
        public string open { get; set; }
        public string close { get; set; }
        public string high { get; set; }
        public string low { get; set; }
        public string volume { get; set; }
        public string value { get; set; }
    }

    public class ResponseBalanceSpot
    {
        public string ccy { get; set; }
        public string available { get; set; }
        public string frozen { get; set; }
    }

    public class ResponseBalanceMargin
    {
        public string margin_account { get; set; }
        public string base_ccy { get; set; }
        public string quote_ccy { get; set; }
        public CurrencyAmount available { get; set; }
        public CurrencyAmount frozen { get; set; }
        public CurrencyAmount repaid { get; set; }
        public CurrencyAmount interest { get; set; }
        public string rik_rate { get; set; }
        public string liq_price { get; set; }
    }

    public class CurrencyAmount
    {
        public string base_ccy { get; set; }
        public string quote_ccy { get; set; }
    }

    public class SpotOrderResponse
    {
        public string order_id { get; set; }
        public string market { get; set; }
        public string market_type { get; set; }
        public string ccy { get; set; }
        public string side { get; set; }
        public string type { get; set; }
        public string amount { get; set; }
        public string price { get; set; }
        public string unfilled_amount { get; set; }
        public string filled_amount { get; set; }
        public string filled_value { get; set; }
        public string client_id { get; set; }
        public string base_fee { get; set; }
        public string quote_fee { get; set; }
        public string discount_fee { get; set; }
        public string maker_fee_rate { get; set; }
        public string taker_fee_rate { get; set; }
        public string last_fill_amount { get; set; }
        public string last_fill_price { get; set; }
        public string created_at { get; set; }
        public string updated_at { get; set; }
        public string status { get; set; }
    }

    public class MyTradeSpotResponse
    {
        public string deal_id { get; set; }
        public string created_at { get; set; } 
        public string market { get; set; }
        public string side { get; set; } 
        public string order_id { get; set; }
        public string margin_market { get; set; }
        public string price { get; set; } 
        public string amount { get; set; }
        public string role { get; set; } 
        public string fee { get; set; }
        public string fee_ccy { get; set; }
    }
}
