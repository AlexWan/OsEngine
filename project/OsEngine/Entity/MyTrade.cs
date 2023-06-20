/*
 * Your rights to use code governed by this license http://o-s-a.net/doc/license_simple_engine.pdf
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System;
using System.Globalization;

namespace OsEngine.Entity
{
    /// <summary>
    /// customer transaction on the exchange
    /// </summary>
    public class MyTrade
    {
        /// <summary>
        /// Volume
        /// </summary>
        public decimal Volume;

        /// <summary>
        /// Price
        /// </summary>
        public decimal Price;

        /// <summary>
        /// Trade number
        /// </summary>
        public string NumberTrade;

        /// <summary>
        /// Parent's warrant number
        /// </summary>
        public string NumberOrderParent;

        /// <summary>
        /// The robot's position number in OsEngine
        /// </summary>
        public string NumberPosition;

        /// <summary>
        /// Instrument code
        /// </summary>
        public string SecurityNameCode;

        /// <summary>
        /// Time
        /// </summary>
        public DateTime Time;

        /// <summary>
        /// Microseconds
        /// </summary>
        public int MicroSeconds;

        /// <summary>
        /// Party to the transaction
        /// </summary>
        public Side Side;

        private static readonly CultureInfo CultureInfo = new CultureInfo("ru-RU");

        /// <summary>
        /// To take a line to save
        /// </summary>
        public string GetStringFofSave()
        {
            string result = "";

            result += Volume.ToString(CultureInfo) + "&";
            result += Price.ToString(CultureInfo) + "&";
            result += NumberOrderParent.ToString(CultureInfo) + "&";
            result += Time.ToString(CultureInfo) + "&";
            result += NumberTrade.ToString(CultureInfo) + "&";
            result += Side + "&";
            result += SecurityNameCode + "&";
            result += NumberPosition + "&";

            return result;
        }

        /// <summary>
        /// Upload from an incoming line
        /// </summary>
        public void SetTradeFromString(string saveString)
        {
            string[] arraySave = saveString.Split('&');

            Volume = arraySave[0].ToDecimal();
            Price = arraySave[1].ToDecimal();
            NumberOrderParent = arraySave[2];
            Time = Convert.ToDateTime(arraySave[3], CultureInfo);
            NumberTrade = arraySave[4];
            Enum.TryParse(arraySave[5], out Side);
            SecurityNameCode = arraySave[6];
            NumberPosition = arraySave[7];
        }

        /// <summary>
        /// To take a line for a hint
        /// </summary>
        public string ToolTip
        {
            get
            {
                if (_toolTip != null)
                {
                    return _toolTip;
                }

                if (NumberPosition != null)
                {
                    _toolTip = "Pos. num: " + NumberPosition + "\r\n";
                }

                if(!NumberTrade.StartsWith("emu"))
                {
                    _toolTip += "Ord. num: " + NumberOrderParent + "\r\n";
                    _toolTip += "Trade num: " + NumberTrade + "\r\n";
                }

                _toolTip += "Side: " + Side + "\r\n";
                _toolTip += "Time: " + Time + "\r\n";
                _toolTip += "Price: " + Price.ToStringWithNoEndZero() + "\r\n";
                _toolTip += "Volume: " + Volume.ToStringWithNoEndZero() + "\r\n";

                return _toolTip;
            }
        }

        private string _toolTip;
    }
}
