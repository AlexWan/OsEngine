using System.Collections.Generic;

namespace OsEngine.Market.Servers.Deribit.Entity
{  
    public class ResponseMessageError
    {
        public Error error { get; set; }

        public class Error
        {
            public string message { get; set; }
            public Data data { get; set; }
        }
        public class Data
        {
            public string param { get; set; }
            public string reason { get; set; }
        }
    }

    public class ResponseMessageSendOrder
    {
        public Result result { get; set; }

        public class Result
        {          
            public Order order { get; set; }
        }
        public class Order
        {
            public string order_state { get; set; }
            public string order_id { get; set;}
            public string order_type { get; set;}            
        }
    }

    public class ResponseMessageSecurities
    {
        public List<Result> result { get; set; }
                
        public class Result
        {
            public string is_active { get; set; }
            public string min_trade_amount { get; set; }
            public string contract_size { get; set; }
            public string tick_size { get; set; }
            public string instrument_name { get; set; }
            public string kind { get; set; }
            public string quote_currency { get; set; }
            public string instrument_id { get; set; }
            public string base_currency { get; set; }
            public string strike { get; set; }
            public string option_type { get; set; }
            public string expiration_timestamp { get; set; }
        }
    }

    public class ResponseMessagePortfolios
    {
        public Result result { get; set; }

        public class Result
        {
            public string currency { get; set; }
            public string equity { get; set; }
        }
    }

    public class ResponseMessagePositions
    {
        public List<Result> result { get; set; }

        public class Result
        {
            public string instrument_name { get; set; }
            public string size { get; set; }
        }
    }

    public class ResponseMessageCandles
    {
        public Result result { get; set; }

        public class Result
        {
            public List<string> open { get; set; }
            public List<string> close { get; set; }
            public List<string> high { get; set; }
            public List<string> low { get; set; }
            public List<string> volume { get; set; }
            public List<string> ticks { get; set; }
        }
    }

    public class ResponseMessageAllOrders
    {
        public List<Result> result { get; set; }

        public class Result
        {
            public string price { get; set; }
            public string order_state { get; set; }
            public string order_id { get; set; }
            public string label { get; set; }
            public string last_update_timestamp { get; set; }
            public string instrument_name { get; set; }
            public string direction { get; set; }
            public string creation_timestamp { get; set; }
            public string amount { get; set; }
        }
    }

    public class ResponseMessageGetOrder
    {
        public Result result { get; set; }

        public class Result
        {
            public string price { get; set; }
            public string order_state { get; set; }
            public string order_id { get; set; }
            public string label { get; set; }
            public string last_update_timestamp { get; set; }
            public string instrument_name { get; set; }
            public string direction { get; set; }
            public string creation_timestamp { get; set; }
            public string amount { get; set; }
        }
    }

    public class ResponseMessageGetMyTradesBySecurity
    {
        public List<Result> result { get; set; }

        public class Result
        {
            public string price { get; set; }           
            public string order_id { get; set; }           
            public string instrument_name { get; set; }            
            public string direction { get; set; }
            public string timestamp { get; set; }
            public string amount { get; set; }
            public string trade_id { get; set; }
        }
    }
}
