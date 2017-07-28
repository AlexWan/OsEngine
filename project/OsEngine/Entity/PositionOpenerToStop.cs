/*
 *Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using OsEngine.OsTrader.Panels.Tab;

namespace OsEngine.Entity
{
    /// <summary>
    /// объект инкапсулирующий данные для открытия сделки по Стопу. OpenAtStop
    /// </summary>
    public class PositionOpenerToStop
    {
        /// <summary>
        /// цена выставляемого ордера
        /// </summary>
        public decimal PriceOrder;

        /// <summary>
        /// цена линии которую смотрим на пробой
        /// </summary>
        public decimal PriceRedLine;

        /// <summary>
        /// цена от которой смотрим пробой
        /// </summary>
        public StopActivateType ActivateType;

        /// <summary>
        /// объём для открытия позиции
        /// </summary>
        public int Volume;

        /// <summary>
        /// сторона открываемой позиции
        /// </summary>
        public Side Side;

    }
}
