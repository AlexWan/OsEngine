using System.Collections.Generic;

namespace OsEngine.Market.Servers.Woo.Entity
{    
    public class ResponseMessageSecurities
    {
        public List<Rows> rows { get; set; }

        public class Rows
        {
            public string symbol { get; set; }
            public string status { get; set; }
            public string bid_cap_ratio { get; set; }
            public string bid_floor_ratio { get; set; }
            public string ask_cap_ratio { get; set; }
            public string ask_floor_ratio { get; set; }
            public string quote_min { get; set; }
            public string quote_max { get; set; }
            public string quote_tick { get; set; }
            public string base_min { get; set; }
            public string base_max { get; set; }
            public string base_tick { get; set; }
            public string min_notional { get; set; }
            public string price_range { get; set; }
            public string price_scope { get; set; }
            public string created_time { get; set; }
            public string updated_time { get; set; }
            public string is_stable { get; set; }
            public string is_trading { get; set; }
            public List<string> precisions { get; set; }
            public string is_prediction { get; set; }
            public string base_mmr { get; set; }
            public string base_imr { get; set; }
            public string funding_interval_hours { get; set; }
            public string funding_cap { get; set; }
            public string funding_floor { get; set; }
            public string order_mode { get; set; }
            public string base_asset_multiplier { get; set; }
        }
    }

    public class ResponseMessagePortfolios
    {       
        public Dictionary<string, Symbol> balances { get; set; }

        public class Symbol
        {
            public string holding { get; set; }
            public string frozen { get; set; }            
        }        
    }

    public class ResponseMessagePositions
    {
        public Data data { get; set; }

        public class Data
        {
            public List<Positions> positions { get; set; }           
        }
        public class Positions
        {
            public string symbol { get; set; }
            public string holding { get; set; }
        }
    }

    public class ResponseMessageCandles
    {
        public Data data { get; set; }

        public class Data
        {
            public List<Rows> rows { get; set; }

        }

        public class Rows
        {
            public string open { get; set; }
            public string close { get; set; }
            public string high { get; set; }
            public string low { get; set; }
            public string volume { get; set; }
            public string start_timestamp { get; set; }
        }
            
       
    }    
}
