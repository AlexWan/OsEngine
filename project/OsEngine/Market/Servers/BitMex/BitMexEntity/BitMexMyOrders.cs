/*
 *Your rights to use the code are governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 *Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System.Collections.Generic;

namespace OsEngine.Market.Servers.BitMex.BitMexEntity
{
    public class DatumMyOrder
    {
        /// <summary>
        /// trade number in the system
        /// номер сделки в системе
        /// </summary>
        public string execID { get; set; }

        /// <summary>
        /// parent's order number
        /// номер ордера родителя
        /// </summary>
        public string orderID { get; set; }

        /// <summary>
        /// trade price
        /// цена сделки
        /// </summary>
        public string price { get; set; }

        /// <summary>
        /// trade instrument
        /// инструмент, по которому совершена сделка
        /// </summary>
        public string symbol { get; set; }

        /// <summary>
        /// trade side 
        /// направление сделки
        /// </summary>
        public string side { get; set; }

        /// <summary>
        /// trade volume
        /// объем сделки
        /// </summary>
        public int? lastQty { get; set; }

        /// <summary>
        /// trade time
        /// время сделки
        /// </summary>
        public string transactTime { get; set; }

        /// <summary>
        /// trade state
        /// статус сделки
        /// </summary>
        public string execType { get; set; }

        /// <summary>
        /// parent's order state
        /// статус ордера родителя
        /// </summary>
        public string ordStatus { get; set; }

        /// <summary>
        /// order number in the robot
        /// номер ордера в роботе
        /// </summary>
        public string clOrdID { get; set; }

        /// <summary>
        /// real execution price
        /// реальная цена исполнения
        /// </summary>
        public string avgPx { get; set; }

        //public string clOrdLinkID { get; set; }
        //public int account { get; set; }
        //public double lastPx { get; set; }
        //public object underlyingLastPx { get; set; }
        //public string lastMkt { get; set; }
        //public string lastLiquidityInd { get; set; }
        //public object simpleOrderQty { get; set; }
        //public int orderQty { get; set; }
        //public object displayQty { get; set; }
        //public object stopPx { get; set; }
        //public object pegOffsetValue { get; set; }
        //public string pegPriceType { get; set; }
        //public string currency { get; set; }
        //public string settlCurrency { get; set; }
        //public string ordType { get; set; }
        //public string timeInForce { get; set; }
        //public string execInst { get; set; }
        //public string contingencyType { get; set; }
        //public string exDestination { get; set; }
        //public string triggered { get; set; }
        //public bool workingIndicator { get; set; }
        //public string ordRejReason { get; set; }
        //public int simpleLeavesQty { get; set; }
        //public int leavesQty { get; set; }
        //public double simpleCumQty { get; set; }
        //public int cumQty { get; set; }
        //public double commission { get; set; }
        //public string tradePublishIndicator { get; set; }
        //public string multiLegReportingType { get; set; }
        //public string text { get; set; }
        //public string trdMatchID { get; set; }
        //public int execCost { get; set; }
        //public int execComm { get; set; }
        //public double homeNotional { get; set; }
        //public int foreignNotional { get; set; }
        //public string timestamp { get; set; }
    }

    public class BitMexMyOrders
    {
        public string table { get; set; }
        public string action { get; set; }
        public List<DatumMyOrder> data { get; set; }
    }
}
