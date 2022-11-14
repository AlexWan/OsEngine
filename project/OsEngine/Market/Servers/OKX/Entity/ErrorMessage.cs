using System.Collections.Generic;

namespace OsEngine.Market.Servers.OKX.Entity
{
    public class ErrorMessage<ErrorObject>
    {
        public string id;
        public string op;
        public string code;
        public string msg;
        public List<ErrorObject> data;
    }

    public class ErrorObjectOrders
    {
        public string tag;
        public string ordId;
        public string clOrdId;
        public string sCode;
        public string sMsg;
    }
}
