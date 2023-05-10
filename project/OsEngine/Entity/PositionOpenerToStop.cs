/*
 * Your rights to use code governed by this license http://o-s-a.net/doc/license_simple_engine.pdf
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System;
using System.Globalization;

namespace OsEngine.Entity
{
    /// <summary>
    /// an object encapsulating data for opening a Stop transaction. OpenAtStop
    /// объект инкапсулирующий данные для открытия сделки по Стопу. OpenAtStop
    /// </summary>
    public class PositionOpenerToStopLimit
    {
        public PositionOpenerToStopLimit()
        {
            ExpiresBars = 0;
        }

        public string Security;

        public string TabName;

        public int Number;

        public PositionOpenerToStopLifeTimeType LifeTimeType;

        /// <summary>
        /// order price
        /// цена выставляемого ордера
        /// </summary>
        public decimal PriceOrder;

        /// <summary>
        /// the price of the line that we look at the breakdown
        /// цена линии которую смотрим на пробой
        /// </summary>
        public decimal PriceRedLine;

        /// <summary>
        /// price from which we look at the breakdown
        /// цена от которой смотрим пробой
        /// </summary>
        public StopActivateType ActivateType;

        /// <summary>
        /// volume for opening a position
        /// объём для открытия позиции
        /// </summary>
        public decimal Volume;

        /// <summary>
        /// side of the position being opened
        /// сторона открываемой позиции
        /// </summary>
        public Side Side;

        private int _expiresBars;

        /// <summary>
        /// Order Lifetime in Bars
        /// Время жизни ордера в барах
        /// </summary>
        public int ExpiresBars
        {
            get { return _expiresBars; }
            set { _expiresBars = value; }
        }

        /// <summary>
        /// The bar number at which the order was created
        /// Номер бара при котором был создан ордер
        /// </summary>
        private int _orderCreateBarNumber;

        public int OrderCreateBarNumber
        {
            get { return _orderCreateBarNumber; }
            set { _orderCreateBarNumber = value; }
        }

        /// <summary>
        /// последнее время свечке при отсечке 
        /// </summary>
        public DateTime LastCandleTime;

        /// <summary>
        /// тип сигнала на открытие
        /// </summary>
        public string SignalType;

        /// <summary>
        /// время создания приказа
        /// </summary>
        public DateTime TimeCreate;

        public string GetSaveString()
        {
            string saveStr = "";

            saveStr += Security + "&";
            saveStr += TabName + "&";
            saveStr += Number + "&";
            saveStr += LifeTimeType + "&";
            saveStr += PriceOrder + "&";
            saveStr += PriceRedLine + "&";
            saveStr += ActivateType + "&";
            saveStr += Volume + "&";
            saveStr += Side + "&";
            saveStr += _expiresBars + "&";
            saveStr += _orderCreateBarNumber + "&";
            saveStr += LastCandleTime.ToString(CultureInfo) + "&";
            saveStr += SignalType + "&";
            saveStr += TimeCreate.ToString(CultureInfo);

            return saveStr;
        }

        public void LoadFromString(string str)
        {
            string[] savStr = str.Split('&');

            Security = savStr[0];
            TabName = savStr[1];
            Number = Convert.ToInt32(savStr[2]);
            Enum.TryParse(savStr[3], out LifeTimeType);
            PriceOrder = savStr[4].ToDecimal();
            PriceRedLine = savStr[5].ToDecimal();
            Enum.TryParse(savStr[6], out ActivateType);
            Volume = savStr[7].ToDecimal();
            Enum.TryParse(savStr[8], out Side);
            _expiresBars = Convert.ToInt32(savStr[9]);
            _orderCreateBarNumber = Convert.ToInt32(savStr[10]);
            LastCandleTime = Convert.ToDateTime(savStr[11], CultureInfo);
            SignalType = savStr[12];
            TimeCreate = Convert.ToDateTime(savStr[13], CultureInfo);
        }

        private static readonly CultureInfo CultureInfo = new CultureInfo("ru-RU");
    }

    /// <summary>
    /// activation type stop order / 
    /// тип активации стоп приказа
    /// </summary>
    public enum StopActivateType
    {

        /// <summary>
        /// activate when the price is higher or equal
        /// активировать когда цена будет выше или равно
        /// </summary>
        HigherOrEqual,

        /// <summary>
        /// activate when the price is lower or equal / 
        /// активировать когда цена будет ниже или равно
        /// </summary>
        LowerOrEqyal
    }

    public enum PositionOpenerToStopLifeTimeType
    {
        CandlesCount,

        NoLifeTime
    }
}