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
        /// party to the transaction
        /// сторона сделки
        /// </summary>
        public Side Side;

        /// <summary>
        /// to take a line to save
        /// взять строку для сохранения
        /// </summary>
        public string GetStringFofSave()
        {
            string result = "";

            result += Volume.ToString(new CultureInfo("ru-RU")) + "&";
            result += Price.ToString(new CultureInfo("ru-RU")) + "&";
            result += NumberOrderParent.ToString(new CultureInfo("ru-RU")) + "&";
            result += Time.ToString(new CultureInfo("ru-RU")) + "&";
            result += NumberTrade.ToString(new CultureInfo("ru-RU")) + "&";
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

            Volume = Convert.ToDecimal(arraySave[0].Replace(",", CultureInfo.InvariantCulture.NumberFormat.NumberDecimalSeparator), CultureInfo.InvariantCulture);
            Price = Convert.ToDecimal(arraySave[1].Replace(",", CultureInfo.InvariantCulture.NumberFormat.NumberDecimalSeparator), CultureInfo.InvariantCulture);
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
                    _toolTip = "Pos. num: " + NumberPosition;
                }

                _toolTip += " Ord. num: " + NumberOrderParent;
                _toolTip += " Trade num: " + NumberTrade + "\r\n";

                _toolTip += "Side: " + Side + "\r\n";
                _toolTip += "Time: " + Time + "\r\n";
                _toolTip += "Price: " + Price + "\r\n";
                _toolTip += "Volume: " + Volume + "\r\n";

                return _toolTip;
            }
        }

        private string _toolTip;
    }
}
