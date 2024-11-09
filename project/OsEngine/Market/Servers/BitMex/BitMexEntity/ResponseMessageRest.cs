using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OsEngine.Market.Servers.BitMex.BitMexEntity
{
    public class ResponseMessageRest<T>
    {
        public string code;
        public string msg;
        public T data;
    }
    public class ResponseAsset
    {
        public string account; // 0000000
        public string currency; // "USDt",
        public string unrealisedPnl;
        public string walletBalance;
        public string marginBalance;
        public string availableMargin;

        //public string currency; // "USDt",
        //public string deposited; //11062082,
        //public string withdrawn; //0,
        //public string transferIn; //0,
        //public string transferOut;// 0,
        //public string amount; // 11062082,
        //public string pendingCredit; // 0,
        //public string pendingDebit; // 0,
        //public string confirmedDebit; // 0,
        //public string timestamp; // "2024-04-16T14:09:18.523Z"

    }
}
