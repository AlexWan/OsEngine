using System.Collections.Generic;

namespace OsEngine.Market.Servers.TraderNet.Entity
{
    public class GetSID
    {
        public string SID;
    }

    public class ResponseUserStockLists
    {
        public List<UserStockLists> userStockLists;
    }

    public class UserStockLists
    {
        public string id;
        public string name;
        public List<string> tickers;
    }

    public class ResponseMessageSecurities
    {
        public string total;
        public List<ListSecurities> securities;
    }

    public class ListSecurities
    {
        public string ticker;
        public string instr_id;
        public string instr_type_c;
        public string code_sec;
        public string mkt_short_code;
        public string step_price;
        public string min_step;
        public string lot_size_q;
        //public Quotes quotes;
    }

    public class Quotes
    {
        public string x_lot;
    }

    public class ResponseCandle
    {
        public Dictionary<string, List<List<string>>> hloc { get; set; }

        public Dictionary<string, List<string>> vl { get; set; }

        public Dictionary<string, List<string>> xSeries { get; set; }
    }

    public class OrderResponse
    {
        public string order_id { get; set; }
        public OrderItem order { get; set; }
        public string warning { get; set; }
    }

    public class OrderItem
    {
        public string auth_login { get; set; }
        public string user_id { get; set; }
        public string order_id { get; set; }
        public string date { get; set; }
        public string stat { get; set; }
        public string stat_orig { get; set; }
        public string stat_d { get; set; }
        public string instr { get; set; }
        public string instr_type { get; set; }
        public string oper { get; set; }
        public string type { get; set; }
        public string cur { get; set; }
        public string p { get; set; }
        public string stop { get; set; }
        public string stop_init_price { get; set; }
        public string stop_activated { get; set; }
        public string q { get; set; }
        public string leaves_qty { get; set; }
        public string exp { get; set; }
        public string stat_prev { get; set; }
        public string user_order_id { get; set; }
        public string trailing_price { get; set; }
        public string trades { get; set; }
        public string changetime { get; set; }
        public string profit { get; set; }
        public string curr_q { get; set; }
        public string trades_json { get; set; }
        public string error { get; set; }
        public string market_time { get; set; }
        public string creator_login { get; set; }
        public string owner_login { get; set; }
        public string safety_type_id { get; set; }
        public string repo_start_date { get; set; }
        public string repo_end_date { get; set; }
        public string repo_start_cash { get; set; }
        public string repo_end_cash { get; set; }
        public string order_nb { get; set; }
        public string orig_cl_ord_id { get; set; }
        public string last_checked_datetime { get; set; }
        public string id { get; set; }
    }

    public class ResponseRestOrders
    {
        public Result result;
    }

    public class Result
    {
        public ResultOrders orders;
    }

    public class ResultOrders
    {
        public List<ResponseOrders> order;
    }

    public class RestResponsePortfolio
    {
        public ResultPortfolio result;
    }

    public class ResultPortfolio
    {
        public Ps ps;
    }

    public class Ps
    {
        public List<AccRest> acc;
        public List<PosRest> pos;
    }

    public class AccRest
    {
        public string currval;
        public string curr;
        public string s;
    }

    public class PosRest
    {
        public string market_value;
        public string curr;
        public string currval;
    }
}