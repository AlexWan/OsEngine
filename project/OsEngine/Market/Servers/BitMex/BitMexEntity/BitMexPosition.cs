/*
 *Your rights to use the code are governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 *Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System.Collections.Generic;

namespace OsEngine.Market.Servers.BitMex.BitMexEntity
{
    public class TypesPos
    {
        /// <summary>
        /// account number
        /// номер счета
        /// </summary>
        public string account { get; set; }

        /// <summary>
        /// instrument
        /// инструмент
        /// </summary>
        public string symbol { get; set; }

        /// <summary>
        /// current volume
        /// текущий объем
        /// </summary>
        public string currentQty { get; set; }

        //public string currency { get; set; }
        //public string underlying { get; set; }
        //public string quoteCurrency { get; set; }
        //public string commission { get; set; }
        //public string initMarginReq { get; set; }
        //public string maintMarginReq { get; set; }
        //public string riskLimit { get; set; }
        //public string leverage { get; set; }
        //public string crossMargin { get; set; }
        //public string deleveragePercentile { get; set; }
        //public string rebalancedPnl { get; set; }
        //public string prevRealisedPnl { get; set; }
        //public string prevUnrealisedPnl { get; set; }
        //public string prevClosePrice { get; set; }
        //public string openingTimestamp { get; set; }
        //public string openingQty { get; set; }
        //public string openingCost { get; set; }
        //public string openingComm { get; set; }
        //public string openOrderBuyQty { get; set; }
        //public string openOrderBuyCost { get; set; }
        //public string openOrderBuyPremium { get; set; }
        //public string openOrderSellQty { get; set; }
        //public string openOrderSellCost { get; set; }
        //public string openOrderSellPremium { get; set; }
        //public string execBuyQty { get; set; }
        //public string execBuyCost { get; set; }
        //public string execSellQty { get; set; }
        //public string execSellCost { get; set; }
        //public string execQty { get; set; }
        //public string execCost { get; set; }
        //public string execComm { get; set; }
        //public string currentTimestamp { get; set; }
        //public string currentCost { get; set; }
        //public string currentComm { get; set; }
        //public string realisedCost { get; set; }
        //public string unrealisedCost { get; set; }
        //public string grossOpenCost { get; set; }
        //public string grossOpenPremium { get; set; }
        //public string grossExecCost { get; set; }
        //public string isOpen { get; set; }
        //public string markPrice { get; set; }
        //public string markValue { get; set; }
        //public string riskValue { get; set; }
        //public string homeNotional { get; set; }
        //public string foreignNotional { get; set; }
        //public string posState { get; set; }
        //public string posCost { get; set; }
        //public string posCost2 { get; set; }
        //public string posCross { get; set; }
        //public string posInit { get; set; }
        //public string posComm { get; set; }
        //public string posLoss { get; set; }
        //public string posMargin { get; set; }
        //public string posMaint { get; set; }
        //public string posAllowance { get; set; }
        //public string taxableMargin { get; set; }
        //public string initMargin { get; set; }
        //public string maintMargin { get; set; }
        //public string sessionMargin { get; set; }
        //public string targetExcessMargin { get; set; }
        //public string varMargin { get; set; }
        //public string realisedGrossPnl { get; set; }
        //public string realisedTax { get; set; }
        //public string realisedPnl { get; set; }
        //public string unrealisedGrossPnl { get; set; }
        //public string longBankrupt { get; set; }
        //public string shortBankrupt { get; set; }
        //public string taxBase { get; set; }
        //public string indicativeTaxRate { get; set; }
        //public string indicativeTax { get; set; }
        //public string unrealisedTax { get; set; }
        //public string unrealisedPnl { get; set; }
        //public string unrealisedPnlPcnt { get; set; }
        //public string unrealisedRoePcnt { get; set; }
        //public string simpleQty { get; set; }
        //public string simpleCost { get; set; }
        //public string simpleValue { get; set; }
        //public string simplePnl { get; set; }
        //public string simplePnlPcnt { get; set; }
        //public string avgCostPrice { get; set; }
        //public string avgEntryPrice { get; set; }
        //public string breakEvenPrice { get; set; }
        //public string marginCallPrice { get; set; }
        //public string liquidationPrice { get; set; }
        //public string bankruptPrice { get; set; }
        //public string timestamp { get; set; }
        //public string lastPrice { get; set; }
        //public string lastValue { get; set; }
    }

    public class ForeignKeysPos
    {
        public string symbol { get; set; }
    }

    public class AttributesPos
    {
        public string account { get; set; }
        public string symbol { get; set; }
        public string currency { get; set; }
        public string underlying { get; set; }
        public string quoteCurrency { get; set; }
    }

    public class DatumPos
    {
        /// <summary>
        /// account number
        /// номер счета
        /// </summary>
        public int account { get; set; }

        /// <summary>
        /// instrument
        /// инструмент
        /// </summary>
        public string symbol { get; set; }

        /// <summary>
        /// volume
        /// объем
        /// </summary>
        public string currentQty { get; set; }

        //public string currency { get; set; }
        //public string underlying { get; set; }
        //public string quoteCurrency { get; set; }
        //public double commission { get; set; }
        //public double initMarginReq { get; set; }
        //public double maintMarginReq { get; set; }
        //public long riskLimit { get; set; }
        //public int leverage { get; set; }
        //public bool crossMargin { get; set; }
        //public int deleveragePercentile { get; set; }
        //public int rebalancedPnl { get; set; }
        //public int prevRealisedPnl { get; set; }
        //public int prevUnrealisedPnl { get; set; }
        //public double prevClosePrice { get; set; }
        //public string openingTimestamp { get; set; }
        //public int openingQty { get; set; }
        //public int openingCost { get; set; }
        //public int openingComm { get; set; }
        //public int openOrderBuyQty { get; set; }
        //public int openOrderBuyCost { get; set; }
        //public int openOrderBuyPremium { get; set; }
        //public int openOrderSellQty { get; set; }
        //public int openOrderSellCost { get; set; }
        //public int openOrderSellPremium { get; set; }
        //public int execBuyQty { get; set; }
        //public int execBuyCost { get; set; }
        //public int execSellQty { get; set; }
        //public int execSellCost { get; set; }
        //public int execQty { get; set; }
        //public int execCost { get; set; }
        //public int execComm { get; set; }
        //public string currentTimestamp { get; set; }
        //public int currentCost { get; set; }
        //public int currentComm { get; set; }
        //public int realisedCost { get; set; }
        //public int unrealisedCost { get; set; }
        //public int grossOpenCost { get; set; }
        //public int grossOpenPremium { get; set; }
        //public int grossExecCost { get; set; }
        //public bool isOpen { get; set; }
        //public double markPrice { get; set; }
        //public int markValue { get; set; }
        //public int riskValue { get; set; }
        //public double homeNotional { get; set; }
        //public int foreignNotional { get; set; }
        //public string posState { get; set; }
        //public int posCost { get; set; }
        //public int posCost2 { get; set; }
        //public int posCross { get; set; }
        //public int posInit { get; set; }
        //public int posComm { get; set; }
        //public int posLoss { get; set; }
        //public int posMargin { get; set; }
        //public int posMaint { get; set; }
        //public int posAllowance { get; set; }
        //public int taxableMargin { get; set; }
        //public int initMargin { get; set; }
        //public int maintMargin { get; set; }
        //public int sessionMargin { get; set; }
        //public int targetExcessMargin { get; set; }
        //public int varMargin { get; set; }
        //public int realisedGrossPnl { get; set; }
        //public int realisedTax { get; set; }
        //public int realisedPnl { get; set; }
        //public int unrealisedGrossPnl { get; set; }
        //public int longBankrupt { get; set; }
        //public int shortBankrupt { get; set; }
        //public int taxBase { get; set; }
        //public int indicativeTaxRate { get; set; }
        //public int indicativeTax { get; set; }
        //public int unrealisedTax { get; set; }
        //public int unrealisedPnl { get; set; }
        //public double unrealisedPnlPcnt { get; set; }
        //public double unrealisedRoePcnt { get; set; }
        //public double simpleQty { get; set; }
        //public int simpleCost { get; set; }
        //public double simpleValue { get; set; }
        //public double simplePnl { get; set; }
        //public double simplePnlPcnt { get; set; }
        //public double avgCostPrice { get; set; }
        //public double avgEntryPrice { get; set; }
        //public double breakEvenPrice { get; set; }
        //public double marginCallPrice { get; set; }
        //public double liquidationPrice { get; set; }
        //public double bankruptPrice { get; set; }
        //public string timestamp { get; set; }
        //public double lastPrice { get; set; }
        //public int lastValue { get; set; }
    }

    public class FilterPos
    {
        public int account { get; set; }
    }

    public class BitMexPosition
    {
        public string table { get; set; }
        public List<string> keys { get; set; }
        public TypesPos types { get; set; }
        public ForeignKeysPos foreignKeys { get; set; }
        public AttributesPos attributes { get; set; }
        public string action { get; set; }
        public List<DatumPos> data { get; set; }
        public FilterPos filter { get; set; }
    }
}
