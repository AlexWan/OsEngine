/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System.Collections.Generic;

namespace OsEngine.Market.Servers.FinamGrpc.Entity
{
    public class FinamIssResponse
    {
        public FinamIssBlock securities { get; set; }
    }

    public class FinamIssBlock
    {
        public List<string> columns { get; set; }

        public List<object[]> data { get; set; }
    }
}
