/*
 * Your rights to use code governed by this license http://o-s-a.net/doc/license_simple_engine.pdf
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System;
using System.Globalization;
using System.Linq;

namespace OsEngine.Entity
{
    /// <summary>
    /// customer transaction on the exchange
    /// клиентская транзакция, совершённая на бирже
    /// </summary>
    public class MyTrade
    {
        /// <summary>
        /// volume
        /// объём
        /// </summary>
        public decimal Volume;

        /// <summary>
        /// price
        /// цена
        /// </summary>
        public decimal Price;

        /// <summary>
        ///  trade number
        /// номер сделки в торговой системе
        /// </summary>
        public string NumberTrade;

        /// <summary>
        /// parent's warrant number
        /// номер ордера родителя
        /// </summary>
        public string NumberOrderParent;

        /// <summary>
        /// the robot's position number in OsEngine
        /// номер позиции у робота в OsEngine
        /// </summary>
        public string NumberPosition;

        /// <summary>
        /// instrument code
        /// код инструмента по которому прошла сделка
        /// </summary>
        public string SecurityNameCode;

        /// <summary>
        /// time
        /// время
        /// </summary>
        public DateTime Time;

        /// <summary>
        /// microseconds
        /// микросекунды
        /// </summary>
        public int MicroSeconds;

        /// <summary>
        /// party to the transaction
        /// сторона сделки
        /// </summary>
        public Side Side;

        private static readonly CultureInfo CultureInfo = new CultureInfo("ru-RU");

        /// <summary>
        /// to take a line to save
        /// взять строку для сохранения
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
        /// upload from an incoming line
        /// загрузить из входящей строки
        /// </summary>
        public void SetTradeFromString(string saveString)
        {
            string[] arraySave = saveString.Split('&');

            Volume = arraySave[0].ToDecimal();
            Price = arraySave[1].ToDecimal();
            NumberOrderParent = arraySave[2];
            Time = Convert.ToDateTime(arraySave[3]);
            NumberTrade = arraySave[4];
            Enum.TryParse(arraySave[5], out Side);
            SecurityNameCode = arraySave[6];
            NumberPosition = arraySave[7];

        }

        /// <summary>
        /// to take a line for a hint
        /// взять строку для подсказки
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
