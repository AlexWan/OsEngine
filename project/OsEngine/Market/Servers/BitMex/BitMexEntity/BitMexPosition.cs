/*
 *Your rights to use the code are governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 *Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System.Collections.Generic;

namespace OsEngine.Market.Servers.BitMex.BitMexEntity
{
    public class DatumPos
    {
        public string account { get; set; }    // Your unique account ID.
        public string symbol { get; set; }     // The contract for this position.
        public string currency { get; set; }   //The margin currency for this position.
        public string currentQty { get; set; } // The current position amount in contracts.
        public string markPrice { get; set; }  // The mark price of the symbol in quoteCurrency.
        public string markValue { get; set; } // The currentQty at the mark price in the settlement currency of the symbol (currency).
        public string riskValue { get; set; }
        public string foreignNotional { get; set; } // Value of position in units of quoteCurrency.
        public string posComm { get; set; }
        public string posMargin { get; set; }
        public string posMaint { get; set; }
        public string maintMargin { get; set; }
        public string unrealisedPnl { get; set; }
        public string unrealisedPnlPcnt { get; set; }
        public string unrealisedRoePcnt { get; set; }
        public string liquidationPrice { get; set; }
        public string timestamp { get; set; }
    }

    public class BitMexPosition
    {
        public string table { get; set; }
        public string action { get; set; }
        public List<DatumPos> data { get; set; }
    }
}
