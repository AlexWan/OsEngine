/*
 * Your rights to use code governed by this license http://o-s-a.net/doc/license_simple_engine.pdf
 *Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System;
using System.Collections.Generic;
using System.Globalization;

namespace OsEngine.Entity
{
    /// <summary>
    /// Candle
    /// Свеча
    /// </summary>
    public class Candle
    {
        /// <summary>
        /// candle start time
        /// время начала свечи
        /// </summary>
        public DateTime TimeStart
        {
            get { return _timeStart; }
            set
            {
                _timeStart = value;
            }
        }
        private DateTime _timeStart;

        /// <summary>
        ///  opening price
        /// цена открытия
        /// </summary>
        public decimal Open;

        /// <summary>
        /// maximum price for the period
        /// максимальная цена за период
        /// </summary>
        public decimal High;

        /// <summary>
        /// closing price
        /// цена закрытия
        /// </summary>
        public decimal Close;

        /// <summary>
        /// minimum price for the period
        /// минимальная цена за период
        /// </summary>
        public decimal Low;

        /// <summary>
        /// volume
        /// объём
        /// </summary>
        public decimal Volume;

        /// <summary>
        /// candles completion status
        /// статус завершённости свечи
        /// </summary>
        public CandleState State;

        /// <summary>
        /// the trades that make up this candle
        /// трейды составляющие эту свечу
        /// </summary>
        public List<Trade> Trades
        {
            set
            {
                _trades = value;
            }
            get { return _trades; }
        }

        private List<Trade> _trades = new List<Trade>();

        /// <summary>
        /// if this growing candle
        /// растущая ли эта свеча
        /// </summary>
        public bool IsUp
        {
            get
            {
                if (Close > Open)
                {
                    return true;
                }
                return false;
            }
        }

        /// <summary>
        /// if that candle is falling
        /// падающая ли эта свеча
        /// </summary>
        public bool IsDown
        {
            get
            {
                if (Close < Open)
                {
                    return true;
                }
                return false;
            }
        }

        /// <summary>
        /// if type of that candle is doji (indecision in the market, Close = Open)
        /// если тип этой свечи доджи (нерешительность на рынке, Close = Open)
        /// </summary>
        public bool IsDoji
        {
            get
            {
                if (Close == Open)
                {
                    return true;
                }
                return false;
            }
        }

        /// <summary>
        /// shadow top
        /// тень сверху
        /// </summary>
        public decimal ShadowTop
        {
            get
            {
                if (IsUp)
                {
                    return High - Close;
                }
                else
                {
                    return High - Open;
                }
            }
        }

        /// <summary>
        /// shadow bottom
        /// тень снизу
        /// </summary>
        public decimal ShadowBottom
        {
            get
            {
                if (IsUp)
                {
                    return Open - Low;
                }
                else
                {
                    return Close - Low;
                }
            }
        }

        /// <summary>
        /// candle body with shadows
        /// тело свечи с учетом теней
        /// </summary>
        public decimal ShadowBody
        {
            get
            {
                return High - Low;
            }
        }

        /// <summary>
        /// candle body without shadows
        /// тело свечи без учета теней
        /// </summary>
        public decimal Body
        {
            get
            {
                if (IsUp)
                {
                    return Close - Open;
                }
                else
                {
                    return Open - Close;
                }
            }
        }

        /// <summary>
        /// to load the status of the candlestick from the line
        /// загрузить состояние свечи из строки
        /// </summary>
        /// <param name="In">status line/строка состояния</param>
        public void SetCandleFromString(string In)
        {
            //20131001,100000,97.8000000,97.9900000,97.7500000,97.9000000,1
            //<DATE>,<TIME>,<OPEN>,<HIGH>,<LOW>,<CLOSE>,<VOLUME>
            string[] sIn = In.Split(',');

            int year = Convert.ToInt32(sIn[0].Substring(0, 4));
            int month = Convert.ToInt32(sIn[0].Substring(4, 2));
            int day = Convert.ToInt32(sIn[0].Substring(6, 2));

            int hour = Convert.ToInt32(sIn[1].Substring(0, 2));
            int minute = Convert.ToInt32(sIn[1].Substring(2, 2));
            int second = Convert.ToInt32(sIn[1].Substring(4, 2));

            TimeStart = new DateTime(year, month, day, hour, minute, second);

            Open = sIn[2].ToDecimal();
            High = sIn[3].ToDecimal();
            Low = sIn[4].ToDecimal();
            Close = sIn[5].ToDecimal();

            try
            {
                Volume = sIn[6].ToDecimal();
            }
            catch (Exception)
            {
                Volume = 1;
            }
        }

        /// <summary>
        /// take a line of signatures
        /// взять строку с подписями
        /// </summary>
        public string ToolTip
        {
            //Date - 20131001 Time - 100000 
            // Open - 97.8000000 High - 97.9900000 Low - 97.7500000 Close - 97.9000000
            get
            {

                string result = string.Empty;

                if (TimeStart.Day > 9)
                {
                    result += TimeStart.Day.ToString();
                }
                else
                {
                    result += "0" + TimeStart.Day;
                }

                result += ".";

                if (TimeStart.Month > 9)
                {
                    result += TimeStart.Month.ToString();
                }
                else
                {
                    result += "0" + TimeStart.Month;
                }

                result += ".";
                result += TimeStart.Year.ToString();

                result += " ";

                if (TimeStart.Hour > 9)
                {
                    result += TimeStart.Hour.ToString();
                }
                else
                {
                    result += "0" + TimeStart.Hour;
                }

                result += ":";

                if (TimeStart.Minute > 9)
                {
                    result += TimeStart.Minute.ToString();
                }
                else
                {
                    result += "0" + TimeStart.Minute;
                }

                result += ":";

                if (TimeStart.Second > 9)
                {
                    result += TimeStart.Second.ToString();
                }
                else
                {
                    result += "0" + TimeStart.Second;
                }

                result += "  \r\n";

                result += " O: ";
                result += Open.ToStringWithNoEndZero();
                result += " H: ";
                result += High.ToStringWithNoEndZero();
                result += " L: ";
                result += Low.ToStringWithNoEndZero();
                result += " C: ";
                result += Close.ToStringWithNoEndZero();

                return result;
            }
        }

        private string _stringToSave;
        private decimal _closeWhenGotLastString;
        public string StringToSave
        {
            get
            {
                if (_closeWhenGotLastString == Close)
                {
                    // If we've taken candles before, we're not counting on that line.
                    // если мы уже брали свечи раньше, не рассчитываем заного строку
                    return _stringToSave;
                }

                _closeWhenGotLastString = Close;

                _stringToSave = "";

                //20131001,100000,97.8000000,97.9900000,97.7500000,97.9000000,1
                //<DATE>,<TIME>,<OPEN>,<HIGH>,<LOW>,<CLOSE>,<VOLUME>

                string result = "";
                result += TimeStart.ToString("yyyyMMdd,HHmmss") + ",";

                result += Open.ToString(CultureInfo.InvariantCulture) + ",";
                result += High.ToString(CultureInfo.InvariantCulture) + ",";
                result += Low.ToString(CultureInfo.InvariantCulture) + ",";
                result += Close.ToString(CultureInfo.InvariantCulture) + ",";
                result += Volume.ToString(CultureInfo.InvariantCulture);

                _stringToSave = result;

                return _stringToSave;
            }
        }

    }

    /// <summary>
    /// candle formation status
    /// состояние формирования свечи
    /// </summary>
    public enum CandleState
    {
        /// <summary>
        /// completed
        /// завершено
        /// </summary>
        Finished,

        /// <summary>
        /// started
        /// начато
        /// </summary>
        Started,

        /// <summary>
        /// indefinitely
        /// неизвестно
        /// </summary>
        None
    }
}
