/*
 *Your rights to use the code are governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 *Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

namespace OsEngine.Market.Servers.BCS.Entity
{
    public class BcsAuthResponse
    {
        public string access_token { get; set; }
        public string expires_in { get; set; }
        public string refresh_expires_in { get; set; }
        public string refresh_token { get; set; }
        public string token_type { get; set; }
        public string notbeforepolicy { get; set; }
        public string session_state { get; set; }
        public string scope { get; set; }
    }

    public class BcsSecurity
    {
        public string ticker { get; set; }
        public Board[] boards { get; set; }
        public string shortName { get; set; }
        public string displayName { get; set; }
        public string type { get; set; }
        public string isin { get; set; }
        public string registrationCode { get; set; }
        public string issuerName { get; set; }
        public string tradingCurrency { get; set; }
        public string faceValue { get; set; }
        public string scale { get; set; }
        public string minimumStep { get; set; }
        public string accruedInt { get; set; }
        public string currencyStepPrice { get; set; }
        public string settleCode { get; set; }
        public string instrumentType { get; set; }
        public string settlementCurrency { get; set; }
        public string settlementDate { get; set; }
        public string maturityDate { get; set; }
        public string lotSize { get; set; }
        public string isQualifiedOnly { get; set; }
        public string isCanShort { get; set; }
        public string baseAsset { get; set; }
        public string qualifiedTestId { get; set; }
        public string qualifiedTestIdTm { get; set; }
        public string availableForUnqualified { get; set; }
        public string currencyNominal { get; set; }
        public string stepPrice { get; set; }
        public string isBcsProduct { get; set; }
        public string couponsPerYear { get; set; }
        public string couponRate { get; set; }
        public string nextCoupon { get; set; }
        public string complexProduct { get; set; }
        public string baseAssetFuture { get; set; }
        public string subType { get; set; }
        public string percentTargetCurrent { get; set; }
        public string businessSector { get; set; }
        public string peNorm { get; set; }
        public string priceTangible { get; set; }
        public string epsGrowthRate { get; set; }
        public string predictedDps { get; set; }
        public string dividendYield { get; set; }
        public string priceChangeYear { get; set; }
        public string targetPrice { get; set; }
        public string mktcap { get; set; }
        public string isBlocked { get; set; }
        public string businessSectorId { get; set; }
        public string primaryBoard { get; set; }
        public string[] secondaryBoards { get; set; }
        public string isCanMargin { get; set; }
        public string isReplacementBond { get; set; }
        public string subTitle { get; set; }
        public string couponTypeName { get; set; }
        public string emissionDate { get; set; }
        public string creditRating { get; set; }
        public string liquidityRating { get; set; }
        public string bcsScore { get; set; }
        public string bcsScoreColor { get; set; }
        public string nrdCode { get; set; }
        public string strike { get; set; }
        public string baseAssetSecuritySecCode { get; set; }
        public string baseAssetSecurityClassCode { get; set; }
        public string businessCountry { get; set; }
        public string businessCountryCode { get; set; }
        public string priceChangeHalfYear { get; set; }
        public string priceChangeMonth { get; set; }
        public string priceChangeEarlyYear { get; set; }
        public string firstCurrCode { get; set; }
        public string amortisedMty { get; set; }
    }

    public class Board
    {
        public string classCode { get; set; }
        public string exchange { get; set; }
    }

    public class BcsPortfolioRest
    {
        public string type { get; set; }
        public string account { get; set; }
        public string exchange { get; set; }
        public string ticker { get; set; }
        public string displayName { get; set; }
        public string baseAssetTicker { get; set; }
        public string currency { get; set; }
        public string upperType { get; set; }
        public string instrumentType { get; set; }
        public string term { get; set; }
        public string quantity { get; set; }
        public string locked { get; set; }
        public string balancePrice { get; set; }
        public string currentPrice { get; set; }
        public string balanceValue { get; set; }
        public string balanceValueRub { get; set; }
        public string balanceValueUsd { get; set; }
        public string balanceValueEur { get; set; }
        public string currentValue { get; set; }
        public string currentValueRub { get; set; }
        public string currentValueUsd { get; set; }
        public string currentValueEur { get; set; }
        public string unrealizedPL { get; set; }
        public string unrealizedPercentPL { get; set; }
        public string dailyPL { get; set; }
        public string dailyPercentPL { get; set; }
        public string portfolioShare { get; set; }
        public string scale { get; set; }
        public string minimumStep { get; set; }
        public string board { get; set; }
        public string priceUnit { get; set; }
        public string faceValue { get; set; }
        public string accruedIncome { get; set; }
        public string logoLink { get; set; }
        public string isBlocked { get; set; }
        public string isBlockedTradeAccount { get; set; }
        public string lockedForFutures { get; set; }
        public string ratioQuantity { get; set; }
        public string expireDate { get; set; }
    }

    public class BcsCandles
    {
        public string ticker { get; set; }
        public string classCode { get; set; }
        public string startDate { get; set; }
        public string endDate { get; set; }
        public string timeFrame { get; set; }
        public Bar[] bars { get; set; }
    }

    public class Bar
    {
        public string time { get; set; }
        public string open { get; set; }
        public string close { get; set; }
        public string high { get; set; }
        public string low { get; set; }
        public string volume { get; set; }
    }

    public class BcsOrderResponse
    {
        public string clientOrderId { get; set; }
        public string status { get; set; }
    }

    public class BcsOrdersListResponse
    {
        public Record[] records { get; set; }
        public string totalRecords { get; set; }
        public string totalPages { get; set; }
    }

    public class Record
    {
        public string orderNum { get; set; }
        public string orderId { get; set; }
        public string clientCode { get; set; }
        public string executionDateTime { get; set; }
        public string executedValue { get; set; }
        public string orderDateTime { get; set; }
        public string tradeDate { get; set; }
        public string updateDateTime { get; set; }
        public string ticker { get; set; }
        public string classCode { get; set; }
        public string takePrice { get; set; }
        public string stopPrice { get; set; }
        public string price { get; set; }
        public string settlementCurrency { get; set; }
        public string orderQuantity { get; set; }
        public string remainedQuantity { get; set; }
        public string executedQuantity { get; set; }
        public string rejectReason { get; set; }
        public string averagePrice { get; set; }
        public string calculationVolume { get; set; }
        public string contractSum { get; set; }
        public string orderStatus { get; set; }
        public string orderType { get; set; }
        public string side { get; set; }
        public string orderQuantityLots { get; set; }
        public string remainedQuantityLots { get; set; }
        public string executedQuantityLots { get; set; }
        public string linkedOrder { get; set; }
        public string stopOrder { get; set; }
        public string visible { get; set; }
        public string marketTakeProfit { get; set; }
        public string marketStopLoss { get; set; }
        public string positionPriceStop { get; set; }
        public string positionPriceLimit { get; set; }
    }
 }
