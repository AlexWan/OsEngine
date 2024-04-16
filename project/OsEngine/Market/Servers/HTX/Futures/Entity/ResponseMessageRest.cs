using System.Collections.Generic;

namespace OsEngine.Market.Servers.HTX.Futures.Entity
{
    public class ResponseMessageSecurities
    {
        public List<Data> data { get; set; }

        public class Data
        {
            public string symbol { get; set; }
            public string contract_code { get; set; }
            public string contract_type { get; set; }
            public string contract_size { get; set; }
            public string price_tick { get; set; }
            public string delivery_date { get; set; }
            public string delivery_time { get; set; }
            public string create_date { get; set; }
            public string contract_status { get; set; }
            public string settlement_time { get; set; }           
        }
    }

    public class ResponseMessagePortfolios
    {
        public List<Data> data { get; set; }

        public class Data
        {
            public string symbol { get; set; }
            public string margin_balance { get; set; }
            public string margin_available { get; set; }
            public string margin_frozen { get; set; }
        }
    }

    public class ResponseMessagePositions
    {
        public List<Data> data { get; set; }

        public class Data
        {
            public string contract_code { get; set; }
            public string symbol { get; set; }
            public string contract_type { get; set; }
            public string volume { get; set; }
            public string frozen { get; set; }
        }
    }

    public class ResponseMessageCandles
    {
        public List<Data> data { get; set; }
        
        public class Data
        {           
            public string open { get; set; }
            public string close { get; set; }
            public string high { get; set; }
            public string low { get; set; }
            public string vol { get; set; }
            public string id { get; set; } //timestamp
        }
    }
    
    public class PlaceOrderResponse
    {
        public string status { get; set; }    
        public Data data { get; set; }

        public class Data
        {
            public string order_id { get; set; }
        }
    }

    public class ResponseMessageAllOrders
    {

        public Data data { get; set; }

        public class Data
        {
            public List<Orders> orders { get; set; }
        }
        
        public class Orders
        {
            public string symbol { get; set; }
            public string contract_type { get; set; }
            public string volume { get; set; }
            public string price { get; set; }
            public string direction { get; set; }
            public string offset { get; set; }
            public string status { get; set; }
            public string order_id { get; set; }
            public string created_at { get; set; }
            public string update_time { get; set; }
            public string client_order_id { get; set; }
        }       
    }

    public class ResponseMessageGetOrder
    {

        public List<Data> data { get; set; }

        public class Data
        {
            public string symbol { get; set; }
            public string contract_type { get; set; }
            public string volume { get; set; }
            public string price { get; set; }
            public string direction { get; set; }
            public string offset { get; set; }
            public string status { get; set; }
            public string order_id { get; set; }
            public string created_at { get; set; }
            public string client_order_id { get; set; }
        }
    }

    public class ResponseMessageGetMyTradesBySecurity
    {
        public Data data { get; set; }

        public class Data
        {
            public List<Trades> trades { get; set; }
            public string symbol { get; set; }
            public string order_id { get; set; }
            public string direction { get; set; }
        }
        public class Trades
        {            
            public string trade_id { get; set; }
            public string trade_volume { get; set; }
            public string trade_price { get; set; }            
            public string created_at { get; set; }           
        }
    }
}
