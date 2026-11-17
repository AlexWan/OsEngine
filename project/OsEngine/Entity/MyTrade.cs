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
            result += EscapingId(NumberOrderParent) + "&";
            result += Time.ToString(CultureInfo) + "&";
            result += EscapingId(NumberTrade) + "&";
            result += Side + "&";
            result += SecurityNameCode.Replace("@","%") + "&";
            result += EscapingId(NumberPosition) + "&";

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
            NumberOrderParent = UnescapingId(arraySave[2]);
            Time = Convert.ToDateTime(arraySave[3], CultureInfo);
            NumberTrade = UnescapingId(arraySave[4]);
            Enum.TryParse(arraySave[5], out Side);
            SecurityNameCode = arraySave[6].Replace('%', '@');
            NumberPosition = UnescapingId(arraySave[7]);
        }

        /// <summary>
        /// Escaping of the identifier for saving to a line. Reversible with UnescapingId
        /// </summary>
        private static string EscapingId(string value)
        {
            if (value == null)
            {
                return "";
            }
            // percent first so that own sequences are not re-escaped
            return value
                .Replace("%", "%25")
                .Replace("@", "%40")
                .Replace("#", "%23")
                .Replace("^", "%5E")
                .Replace("*", "%2A")
                .Replace("&", "%26")
                .Replace("\r", "%0D")
                .Replace("\n", "%0A");
        }

        /// <summary>
        /// Reverse unescaping of the identifier after EscapingId
        /// </summary>
        private static string UnescapingId(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return "";
            }
            // reverse order, percent last so that own "%25" decodes only after all others
            return value
                .Replace("%0A", "\n")
                .Replace("%0D", "\r")
                .Replace("%26", "&")
                .Replace("%2A", "*")
                .Replace("%5E", "^")
                .Replace("%23", "#")
                .Replace("%40", "@")
                .Replace("%25", "%");
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

        /// <summary>
        /// Service info to tester. Number candle
        /// </summary>
        public int NumberCandleInTester;
    }
}
