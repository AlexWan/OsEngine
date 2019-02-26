/*
 *Your rights to use the code are governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 *Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

namespace OsEngine.Market.Servers.BitMex.BitMexEntity
{
    public class BitMexSecurity
    {
        public string symbol { get; set; }

        //public string rootSymbol { get; set; }

        public string state { get; set; }

        public string typ { get; set; }

        //public string listing { get; set; }
        //public string front { get; set; }

        public string expiry { get; set; }

        //public string settle { get; set; }
        //public string relistInterval { get; set; }
        //public string inverseLeg { get; set; }
        //public string sellLeg { get; set; }
        //public string buyLeg { get; set; }
        //public string positionCurrency { get; set; }
        //public string underlying { get; set; }
        //public string quoteCurrency { get; set; }
        //public string underlyingSymbol { get; set; }
        //public string reference { get; set; }
        //public string referenceSymbol { get; set; }
        //public object calcInterval { get; set; }
        //public object publishInterval { get; set; }
        //public object publishTime { get; set; }
        //public int maxOrderQty { get; set; }
        //public int maxPrice { get; set; }

        /// <summary>
        /// lot size
        /// размер лота
        /// </summary>
        public int lotSize { get; set; }

        public decimal tickSize { get; set; }
        //public int multiplier { get; set; }
        //public string settlCurrency { get; set; }
        //public int? underlyingToPositionMultiplier { get; set; }
        //public int? underlyingToSettleMultiplier { get; set; }
        //public int? quoteToSettleMultiplier { get; set; }
        //public bool isQuanto { get; set; }
        //public bool isInverse { get; set; }
        //public double initMargin { get; set; }
        //public double maintMargin { get; set; }
        //public long? riskLimit { get; set; }
        //public long? riskStep { get; set; }
        //public double? limit { get; set; }
        //public bool capped { get; set; }
        //public bool taxed { get; set; }
        //public bool deleverage { get; set; }
        //public double makerFee { get; set; }
        //public double takerFee { get; set; }
        //public double settlementFee { get; set; }
        //public int insuranceFee { get; set; }
        //public string fundingBaseSymbol { get; set; }
        //public string fundingQuoteSymbol { get; set; }
        //public string fundingPremiumSymbol { get; set; }
        //public string fundingTimestamp { get; set; }
        //public string fundingInterval { get; set; }
        //public double? fundingRate { get; set; }
        //public double? indicativeFundingRate { get; set; }
        //public object rebalanceTimestamp { get; set; }
        //public object rebalanceInterval { get; set; }
        //public string openingTimestamp { get; set; }
        //public string closingTimestamp { get; set; }
        //public string sessionInterval { get; set; }
        //public double prevClosePrice { get; set; }
        //public double? limitDownPrice { get; set; }
        //public double? limitUpPrice { get; set; }
        //public object bankruptLimitDownPrice { get; set; }
        //public object bankruptLimitUpPrice { get; set; }
        //public long? prevTotalVolume { get; set; }
        //public long? totalVolume { get; set; }
        //public int volume { get; set; }
        //public int volume24h { get; set; }
        //public long? prevTotalTurnover { get; set; }
        //public long? totalTurnover { get; set; }
        //public object turnover { get; set; }
        //public long? turnover24h { get; set; }
        //public double prevPrice24h { get; set; }
        //public double? vwap { get; set; }
        //public double? highPrice { get; set; }
        //public double? lowPrice { get; set; }
        //public double lastPrice { get; set; }
        //public double lastPriceProtected { get; set; }
        //public string lastTickDirection { get; set; }
        //public double lastChangePcnt { get; set; }
        //public double bidPrice { get; set; }
        //public double midPrice { get; set; }
        //public double askPrice { get; set; }
        //public double impactBidPrice { get; set; }
        //public double impactMidPrice { get; set; }
        //public double impactAskPrice { get; set; }
        //public bool hasLiquidity { get; set; }
        //public int openInterest { get; set; }
        //public long? openValue { get; set; }
        //public string fairMethod { get; set; }
        //public double fairBasisRate { get; set; }
        //public double? fairBasis { get; set; }
        //public double? fairPrice { get; set; }
        //public string markMethod { get; set; }
        //public double markPrice { get; set; }
        //public int indicativeTaxRate { get; set; }
        //public double? indicativeSettlePrice { get; set; }
        //public object settledPrice { get; set; }
        //public string timestamp { get; set; }
    }
}
