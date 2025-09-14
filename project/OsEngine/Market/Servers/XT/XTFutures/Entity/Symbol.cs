using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OsEngine.Market.Servers.XT.XTFutures.Entity
{
   public class Symbol
    {
        public class XTFuturesResponseRest<T>
        {
            public string returnCode { get; set; }
            public string msgInfo { get; set; }
            public XTFuturesApiError error { get; set; }   // может быть null
            public T result { get; set; }
        }

        public class XTFuturesApiError
        {
            public string code { get; set; }
            public string msg { get; set; }
        }

        public class XTFuturesSymbolListResult
        {
            public string time { get; set; }
            public string version { get; set; }
            public List<XTFuturesSymbol> symbols { get; set; }
        }

        public class XTFuturesSymbol
        {
            public string id { get; set; }
            public string symbol { get; set; }
            public string symbolGroupId { get; set; }
            public string pair { get; set; }
            public string contractType { get; set; }
            public string productType { get; set; }
            public string predictEventType { get; set; }
            public string predictEventParam { get; set; }
            public string predictEventSort { get; set; }
            public string underlyingType { get; set; }
            public string contractSize { get; set; } // Числа, приходящие в кавычках —  string
            public string tradeSwitch { get; set; }
            public string openSwitch { get; set; }
            public string isDisplay { get; set; }
            public string isOpenApi { get; set; }
            public string state { get; set; }
            public string initLeverage { get; set; }
            public string initPositionType { get; set; }
            public string baseCoin { get; set; }
            public string spotCoin { get; set; }
            public string quoteCoin { get; set; }
            public string baseCoinPrecision { get; set; }
            public string baseCoinDisplayPrecision { get; set; }
            public string quoteCoinPrecision { get; set; }
            public string quoteCoinDisplayPrecision { get; set; }
            public string quantityPrecision { get; set; }
            public string pricePrecision { get; set; }
            public string supportOrderType { get; set; }
            public string supportTimeInForce { get; set; }
            public string supportEntrustType { get; set; }
            public string supportPositionType { get; set; }
            public string minQty { get; set; }
            public string minNotional { get; set; }
            public string maxNotional { get; set; }
            public string multiplierDown { get; set; }
            public string multiplierUp { get; set; }
            public string maxOpenOrders { get; set; }
            public string maxEntrusts { get; set; }
            public string makerFee { get; set; }
            public string takerFee { get; set; }
            public string liquidationFee { get; set; }
            public string marketTakeBound { get; set; }
            public string depthPrecisionMerge { get; set; }
            public List<string> labels { get; set; }
            public string onboardDate { get; set; }
            public string enName { get; set; }
            public string cnName { get; set; }
            public string minStepPrice { get; set; }
            public string minPrice { get; set; } // nullable поля
            public string maxPrice { get; set; }
            public string deliveryDate { get; set; }
            public string deliveryPrice { get; set; }
            public string deliveryCompletion { get; set; }
            public string cnDesc { get; set; }
            public string enDesc { get; set; }
            public string cnRemark { get; set; }
            public string enRemark { get; set; }
            public List<string> plates { get; set; }
            public string fastTrackCallbackRate1 { get; set; }
            public string fastTrackCallbackRate2 { get; set; }
            public string mstringrackCallbackRate { get; set; }
            public string maxTrackCallbackRate { get; set; }
            public string latestPriceDeviation { get; set; }
            public string marketOpenTakeBound { get; set; }
            public string marketCloseTakeBound { get; set; }
            public string offTime { get; set; }
            public string updatedTime { get; set; }
            public string displaySwitch { get; set; }
            public string curMaxLeverage { get; set; }
            public string riskNominalValueCoefficient { get; set; }
            public string riskExpireTime { get; set; }
        }

    }
}
