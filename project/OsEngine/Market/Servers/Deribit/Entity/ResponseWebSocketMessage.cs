using System.Collections.Generic;

namespace OsEngine.Market.Servers.Deribit.Entity
{
    public class ResponseWebSocketMessage<T>
    {
        public ResponseWebsocketMessageParams @params;
        public string method;
        public string error;
    }

    public class ResponseWebsocketMessageParams
    {
        public string channel;
     
    }

    public class ResponseWebSocketMessageSubscrible
    {
        public string error;
    }

    public class ResponseChannelTrades
    {
        public Params @params { get; set; }

        public class Params
        {
            public List<Data> data { get; set; }
        }

        public class Data
        {
            public string instrument_name { get; set; }
            public string price { get; set; }
            public string amount { get; set; }
            public string direction { get; set; }
            public string timestamp { get; set; }
            public string trade_id { get; set; }
        }
    }

    public class ResponseChannelBook
    {
        public Params @params { get; set; }

        public class Params
        {
            public Data data { get; set; }
        }

        public class Data
        {
            public List<List<string>> asks { get; set; }
            public List<List<string>> bids { get; set; }
            public string timestamp { get; set; }
            public string instrument_name { get; set; }
        }
    }

    public class ResponseChannelUserOrders
    {
        public Params @params { get; set; }

        public class Params
        {
            public List<Data> data { get; set; }
        }

        public class Data
        {
            public string creation_timestamp { get; set; }
            public string instrument_name { get; set; }
            public string amount { get; set; }
            public string direction { get; set; }
            public string filled_amount { get; set; }
            public string order_id { get; set; }
            public string order_state { get; set; }
            public string order_type { get; set; }
            public string price { get; set; }
            public string label { get; set; }
        }
    }
    public class ResponseChannelUserTrades
    {
        public Params @params { get; set; }

        public class Params
        {
            public List<Data> data { get; set; }
        }

        public class Data
        {
            public string timestamp { get; set; }
            public string instrument_name { get; set; }
            public string amount { get; set; }
            public string direction { get; set; }
            public string order_id { get; set; }
            public string state { get; set; }
            public string price { get; set; }
            public string trade_id { get; set; }

        }
    }
    public class ResponseWebSocketMessageAuth
    {
        public Result result { get; set; }

        public class Result
        {
            public string access_token { get; set; }
        }
    }

    public class ResponseChannelPortfolio
    {
        public Params @params { get; set; }
        public class Params
        {
            public Data data { get; set; }
        }

        public class Data
        {
            public string currency { get; set; }
            public string equity { get; set; }
        }
    }

    public class ResponseChannelUserChanges
    {
        public Params @params { get; set; }

        public class Params
        {
            public Data data { get; set; }
        }

        public class Data
        {
            public List<Trades> trades { get; set; }
            public List<Orders> orders { get; set; }
            public List<Positions> positions { get; set; }

            public string price { get; set; }
            public string amount { get; set; }
            public string direction { get; set; }
            public string timestamp { get; set; }
            public string trade_id { get; set; }
        }

        public class Trades
        {
            public string timestamp { get; set; }
            public string instrument_name { get; set; }
            public string amount { get; set; }
            public string direction { get; set; }
            public string order_id { get; set; }
            public string state { get; set; }
            public string price { get; set; }
            public string trade_id { get; set; }
        }

        public class Positions
        {
            public string instrument_name { get; set; }
            public string size { get; set; }
        }

        public class Orders
        {
            public string creation_timestamp { get; set; }
            public string instrument_name { get; set; }
            public string amount { get; set; }
            public string direction { get; set; }
            public string filled_amount { get; set; }
            public string order_id { get; set; }
            public string order_state { get; set; }
            public string order_type { get; set; }
            public string price { get; set; }
            public string label { get; set; }
        }
    }
}

