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
        public string c { get; set; } //change time
        public string f { get; set; }  //free balance
        public string fd { get; set; }  //free changed amount
        public string l { get; set; }  //frozen amount
        public string ld { get; set; }  //frozen changed amount
        public string o { get; set; }
    }

    public class MexcSocketOrder
    {
        public string A { get; set; }
        public string O { get; set; }
        public string S { get; set; }
        public string V { get; set; }
        public string a { get; set; }
        public string c { get; set; }
        public string i { get; set; }
        public string m { get; set; }
        public string o { get; set; }
        public string p { get; set; }
        public string s { get; set; }
        public string v { get; set; }
        public string ap { get; set; }
        public string cv { get; set; }
        public string ca { get; set; }
    }
}
