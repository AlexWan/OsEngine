/*
 *Your rights to use the code are governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 *Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

namespace OsEngine.Market.Servers.Mexc.Json
{
    public class MexcListenKey
    {
        public string listenKey;
    }

    // Example
    //    {
    //    "c": "spot@private.account.v3.api",
    //    "d": {
    //        "a": "USDT",
    //        "c": 1678185928428,
    //        "f": "302.185113007893322435",
    //        "fd": "-4.990689704",
    //        "l": "4.990689704",
    //        "ld": "4.990689704",
    //        "o": "ENTRUST_PLACE"
    //    },
    //    "t": 1678185928435
    //}

    public class MexcSocketBalance
    {
        public string a { get; set; }
        public long c { get; set; } //change time
        public string f { get; set; }  //free balance
        public string fd { get; set; }  //free changed amount
        public string l { get; set; }  //frozen amount
        public string ld { get; set; }  //frozen changed amount
        public string o { get; set; }
    }

    public class MexcSocketOrder
    {
        public decimal A { get; set; }
        public ulong O { get; set; }
        public int S { get; set; }
        public decimal V { get; set; }
        public decimal a { get; set; }
        public string c { get; set; }
        public string i { get; set; }
        public int m { get; set; }
        public int o { get; set; }
        public decimal p { get; set; }
        public int s { get; set; }
        public decimal v { get; set; }
        public decimal ap { get; set; }
        public decimal cv { get; set; }
        public decimal ca { get; set; }
    }
}
