/*
 *Ваши права на использования кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using OsEngine.Entity;

namespace OsEngine.Alerts
{
    /// <summary>
    /// сигнал из Алерта
    /// </summary>
    public class AlertSignal
    {
        /// <summary>
        /// тип сигнала
        /// </summary>
        public SignalType SignalType;

        /// <summary>
        /// объём
        /// </summary>
        public int Volume;

        /// <summary>
        /// проскальзывание
        /// </summary>
        public decimal Slipage;

        /// <summary>
        /// тип ордера для сделки
        /// </summary>
        public OrderPriceType PriceType;

        /// <summary>
        /// номер сделки которую нужно закрыть, если сигнал на закрытие одной сделки
        /// </summary>
        public int NumberClosingPosition;
    }

    /// <summary>
    /// тип сигнала алерта
    /// </summary>
    public enum SignalType
    {
        /// <summary>
        /// купить
        /// </summary>
        Buy,

        /// <summary>
        /// продать
        /// </summary>
        Sell,

        /// <summary>
        /// закрыть все позиции
        /// </summary>
        CloseAll,

        /// <summary>
        /// закрыть одну позицию
        /// </summary>
        CloseOne,

        /// <summary>
        /// отсутствует
        /// </summary>
        None,

        /// <summary>
        /// выставить новый стоп
        /// </summary>
        ReloadStop,

        /// <summary>
        /// выставить новый профит
        /// </summary>
        ReloadProfit,

        /// <summary>
        /// модифицировать позицию
        /// </summary>
        Modificate,

        /// <summary>
        /// открыть новую сделку
        /// </summary>
        OpenNew,

        /// <summary>
        /// удалить позицию
        /// </summary>
        DeletePos
    }
}
