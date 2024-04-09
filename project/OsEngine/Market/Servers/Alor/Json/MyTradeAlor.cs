/*
 *Your rights to use the code are governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 *Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

namespace OsEngine.Market.Servers.Alor.Json
{
    public class MyTradeAlor
    {
        public string id;
        public string orderno;
        public string symbol;
        public string qty;
        public string price;
        public string date;
        public string side;
        public string oi;
        public string existing;
    }

    public class MyTradeAlorRest
    {
        public string id;
        public string orderno;
        public string comment;
        public string symbol;
        public string brokerSymbol;
        public string exchange;
        public string date;
        public string board;
        public string qtyUnits;
        public string qtyBatch;
        public string qty;
        public string price;
        public string accruedInt;
        public string side;
        public string existing;
        public string commission;
        public string repoSpecificFields;
        public string volume;
    }
}
