using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OsEngine.Market.Servers.BybitSpot.Entities
{
    public class ResponseRestMessage<T>
    {
        public string retCode;
        public string retMsg;
        public string time;
        public T result;
    }
    public class ArraySymbols
    {
        public List<ResponseSymbol> list;
    }
    public class ArrayBars
    {
        public List<ResponseBars> list;
    }
    public class ArrayPortfolios
    {
        public List<ResponsePortfolio> balances;
    }
    public class ResponseBars
    {
        public string t;
        public string s;
        public string sn;
        public string c;
        public string h;
        public string l;
        public string o;
        public string v;
    }
    public class ResponsePortfolio
    {
        public string coin;
        public string coinId;
        public string total;
        public string free;
        public string locked;
    }
    public class ResponseSymbol
    {
        public string name;
        public string alias;
        public string baseCoin;
        public string quoteCoin;
        public string basePrecision;
        public string quotePrecision;
        public string minTradeQty;
        public string minTradeAmt;
        public string maxTradeQty;
        public string maxTradeAmt;
        public string minPricePrecision;
        public string category;
        public string showStatus;
        public string innovation;
    }
    public class ResponseServerTime
    {
        public string timeSecond;
        public string timeNano;
    }
}
