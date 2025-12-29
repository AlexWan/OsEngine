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
        public string open_bal;
        public string curr;
        public string currval;
    }        
}