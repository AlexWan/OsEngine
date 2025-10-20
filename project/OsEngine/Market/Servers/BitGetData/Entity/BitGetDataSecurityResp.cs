
namespace OsEngine.Market.Servers.BitGetData.Entity
{
    public class BitGetDataSecurityResp<T>
    {
        public string code;
        public string msg;
        public string requestTime;
        public T data;
    }

    public class BitGetDataSymbol
    {
        public string symbol;
        public string quoteCoin;
        public string baseCoin;
        public string status;
        public string symbolStatus;
    }
}
