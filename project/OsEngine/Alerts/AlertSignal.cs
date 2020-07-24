/*
 *Your rights to use code governed by this license http://o-s-a.net/doc/license_simple_engine.pdf
 *Ваши права на использования кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using OsEngine.Entity;

namespace OsEngine.Alerts
{
    /// <summary>
    /// signal from Alert
    /// сигнал из Алерта
    /// </summary>
    public class AlertSignal
    {
        /// <summary>
        /// signal type
        /// тип сигнала
        /// </summary>
        public SignalType SignalType;

        /// <summary>
        /// volume
        /// объём
        /// </summary>
        public decimal Volume;

        /// <summary>
        /// slippage
        /// проскальзывание
        /// </summary>
        public decimal Slipage;

        /// <summary>
        /// transaction type
        /// тип ордера для сделки
        /// </summary>
        public OrderPriceType PriceType;

        /// <summary>
        /// number of transaction to be closed if signal to close single transaction
        /// номер сделки которую нужно закрыть, если сигнал на закрытие одной сделки
        /// </summary>
        public int NumberClosingPosition;
    }

    /// <summary>
    /// alert type
    /// тип сигнала алерта
    /// </summary>
    public enum SignalType
    {
        /// <summary>
        /// Buy
        /// купить
        /// </summary>
        Buy,

        /// <summary>
        /// Sell
        /// продать
        /// </summary>
        Sell,

        /// <summary>
        /// CloseAll
        /// закрыть все позиции
        /// </summary>
        CloseAll,

        /// <summary>
        /// CloseOne position
        /// закрыть одну позицию
        /// </summary>
        CloseOne,

        /// <summary>
        /// none
        /// отсутствует
        /// </summary>
        None,

        /// <summary>
        /// set new stop
        /// выставить новый стоп
        /// </summary>
        ReloadStop,

        /// <summary>
        /// set new takeprofit
        /// выставить новый профит
        /// </summary>
        ReloadProfit,

        /// <summary>
        /// modify position
        /// модифицировать позицию
        /// </summary>
        Modificate,

        /// <summary>
        /// open new deal
        /// открыть новую сделку
        /// </summary>
        OpenNew,

        /// <summary>
        /// delete position
        /// удалить позицию
        /// </summary>
        DeletePos,
        
        /// <summary>
        /// find position
        /// найти позицию
        /// </summary>
        FindPosition
    }
}
