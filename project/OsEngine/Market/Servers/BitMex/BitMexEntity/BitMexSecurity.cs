/*
 *Your rights to use the code are governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 *Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

namespace OsEngine.Market.Servers.BitMex.BitMexEntity
{
    public class BitMexSecurity
    {
        public string symbol;
        public string rootSymbol; //string,
        public string state;
        public string typ;
        public string listing;
        public string quoteCurrency; //string
        public string maxOrderQty;  // 0
        public string maxPrice;     // 0
        public string lotSize;
        public string tickSize;
        public string multiplier; // 0
        public string initMargin; // 0
        public string maintMargin; // 0
        public string makerFee; // 0
        public string takerFee; // 0
        public string fundingRat; // 0
        public string indicativeFundingRate; // 0
        public string underlyingToSettleMultiplier;
        public string underlyingToPositionMultiplier;
    }
}
