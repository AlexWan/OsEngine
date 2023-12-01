/*
 *Your rights to use the code are governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 *Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System.Collections.Generic;

namespace OsEngine.Market.Servers.Alor.Json
{
    public class CandlesHistoryAlor
    {
        public List<AlorCandle> history;
        public string next;
        public string prev;
    }

    public class AlorCandle
    {
        public string time;
        public string close;
        public string open;
        public string high;
        public string low;
        public string volume;
    }
}
