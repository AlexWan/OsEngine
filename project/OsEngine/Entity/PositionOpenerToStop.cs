/*
 * Your rights to use code governed by this license http://o-s-a.net/doc/license_simple_engine.pdf
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using OsEngine.OsTrader.Panels.Tab;

namespace OsEngine.Entity
{
    /// <summary>
    /// объект инкапсулирующий данные для открытия сделки по Стопу. OpenAtStop
    /// </summary>
    public class PositionOpenerToStop
    {
        public PositionOpenerToStop()
        {
            ExpiresBars = 0;
        }

        public PositionOpenerToStop(int thisBarNumber, int expiresBars)
        {
            OrderCreateBarNumber = thisBarNumber;
            ExpiresBars = expiresBars;
        }


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
        public decimal Volume;

        /// <summary>
        /// сторона открываемой позиции
        /// </summary>
        public Side Side;

        private int _expiresBars;

        /// <summary>
        /// Время жизни ордера в барах
        /// </summary>
        public int ExpiresBars
        {
            get { return _expiresBars; }
            set { _expiresBars = value; }
        }


        /// <summary>
        /// Номер бара при котором был создан ордер
        /// </summary>
        private int _orderCreateBarNumber;

        public int OrderCreateBarNumber
        {
            get { return _orderCreateBarNumber; }
            set { _orderCreateBarNumber = value; }
        }


    }
}
