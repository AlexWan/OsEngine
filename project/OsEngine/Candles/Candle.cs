/*
 * Your rights to use code governed by this license http://o-s-a.net/doc/license_simple_engine.pdf
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System;
using System.Collections.Generic;
using System.Globalization;

namespace OsEngine.Entity
{
    /// <summary>
    /// Candle
    /// </summary>
    public class Candle
    {
        /// <summary>
        /// Candle start time
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
        /// Opening price
        /// </summary>
        public decimal Open;

        /// <summary>
        /// Maximum price for the period
        /// </summary>
        public decimal High;

        /// <summary>
        /// Closing price
        /// </summary>
        public decimal Close;

        /// <summary>
        /// Minimum price for the period
        /// </summary>
        public decimal Low;

        /// <summary>
        /// Volume
        /// </summary>
        public decimal Volume;

        /// <summary>
        /// Certain point on the candle
        /// </summary>
        /// <param name="type"> "Close","High","Low","Open","Median","Typical"</param>
        public decimal GetPoint(string type)
        {
            char first = type[0];
            if (first == 'C')
            {
                return Close;
            }
            else if (first == 'H')
            {
                return High;
            }
            else if (first == 'L')
            {
                return Low;
            }
            else if (first == 'O')
            {
                return Open;
            }
            else if (first == 'M')
            {
                return (High + Low) / 2;
            }
            else //if (type == Entity.CandlePointType.Typical)
            {
                return (High + Low + Close) / 3;
            }
        }

        /// <summary>
        /// Candles completion status
        /// </summary>
        public CandleState State;

        /// <summary>
        /// The trades that make up this candle
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
        /// If this growing candle
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
        /// If that candle is falling
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
        /// If type of that candle is doji (indecision in the market, Close = Open)
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
        /// Shadow top
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
        /// Shadow bottom
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
        /// Candle body with shadows
        /// </summary>
        public decimal ShadowBody
        {
            get
            {
                return High - Low;
            }
        }

        /// <summary>
        /// Candle body without shadows
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
        /// Candle body (%)
        /// </summary>
        public decimal BodyPercent
        {
            get
            {
                if (Close <= 0m || Open <= 0m)
                {
                    return 0m;
                }
                if (IsUp)
                {
                    return (Close - Open) / Open * 100m;
                }
                else
                {
                    return (Open - Close) / Open * 100m;
                }
            }
        }

        /// <summary>
        /// Candle center
        /// </summary>
        public decimal Center
        {
            get
            {
                return (High - Low) / 2m + Low;
            }
        }

        /// <summary>
        /// Candle volatility (regarding center, %)
        /// </summary>
        public decimal Volatility
        {
            get
            {
                if (Center == 0m)
                {
                    return 0m;
                }
                return (High - Center) / Center * 100m;
            }
        }

        /// <summary>
        /// To load the status of the candlestick from the line
        /// </summary>
        /// <param name="In">status line</param>
        public void SetCandleFromString(string In)
        {
            //20131001,100000,97.8000000,97.9900000,97.7500000,97.9000000,1
            //<DATE>,<TIME>,<OPEN>,<HIGH>,<LOW>,<CLOSE>,<VOLUME>
            string[] sIn = In.Split(',');

            TimeStart = DateTimeParseHelper.ParseFromTwoStrings(sIn[0], sIn[1]);

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
        /// Take a line of signatures
        /// </summary>
        public string ToolTip
        {
            //Date - 20131001 Time - 100000 
            // Open - 97.8000000 High - 97.9900000 Low - 97.7500000 Close - 97.9000000 Body(%) - 0.97
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

                result += "  \r\n";

                result += " Body(%): ";
                result += (Math.Floor(BodyPercent * 100m) / 100m).ToStringWithNoEndZero();

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
                    return _stringToSave;
                }

                _closeWhenGotLastString = Close;

                _stringToSave = "";

                //20131001,100000,97.8000000,97.9900000,97.7500000,97.9000000,1,0.97
                //<DATE>,<TIME>,<OPEN>,<HIGH>,<LOW>,<CLOSE>,<VOLUME>,<BODY%>

                string result = "";
                result += TimeStart.ToString("yyyyMMdd,HHmmss") + ",";

                result += Open.ToString(CultureInfo.InvariantCulture) + ",";
                result += High.ToString(CultureInfo.InvariantCulture) + ",";
                result += Low.ToString(CultureInfo.InvariantCulture) + ",";
                result += Close.ToString(CultureInfo.InvariantCulture) + ",";
                result += Volume.ToString(CultureInfo.InvariantCulture) + ",";
                result += BodyPercent.ToString(CultureInfo.InvariantCulture);

                _stringToSave = result;

                return _stringToSave;
            }
        }

    }

    /// <summary>
    /// Candle formation status
    /// </summary>
    public enum CandleState
    {
        /// <summary>
        /// Completed
        /// </summary>
        Finished,

        /// <summary>
        /// Started
        /// </summary>
        Started,

        /// <summary>
        /// Indefinitely
        /// </summary>
        None
    }
}