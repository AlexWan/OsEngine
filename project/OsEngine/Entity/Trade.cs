/*
 *Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System;
using System.Globalization;

namespace OsEngine.Entity
{
    /// <summary>
    /// тик
    /// </summary>
    public class Trade
    {
        /// <summary>
        /// объём
        /// </summary>
        public int Volume;

        /// <summary>
        /// цена сделки
        /// </summary>
        public decimal Price;

        /// <summary>
        /// время сделки
        /// </summary>
        public DateTime Time;

        /// <summary>
        /// микросекунды
        /// </summary>
        public int MicroSeconds;

        /// <summary>
        /// направление сделки
        /// </summary>
        public Side Side;

        /// <summary>
        /// код инструмента по которому прошла сделка
        /// </summary>
        public string SecurityNameCode
        {
            get { return name; }
            set
            {
                name = value;

            }
        }

        private string name;
        /// <summary>
        /// номер сделки в системе
        /// </summary>
        public string Id;

        private string _saveString;
        /// <summary>
        /// взять строку для сохранения
        /// </summary>
        /// <returns>строка с состоянием объекта</returns>
        public string GetSaveString()
        {
            //20150401,100000,86160.000000000,2
            // либо 20150401,100000,86160.000000000,2, Buy/Sell
            if (!string.IsNullOrWhiteSpace(_saveString))
            {
                return _saveString;
            }

            string result = "";
            result += Time.ToString("yyyyMMdd,HHmmss") + ",";
            result += Price.ToString(new CultureInfo("en-US")) + ",";
            result += Volume + ",";
            result += Side + ",";
            result += MicroSeconds;

            _saveString = result;
            return _saveString;
        }

        /// <summary>
        /// загрузить тик из сохранённой строки
        /// </summary>
        /// <param name="In"></param>
        public void SetTradeFromString(string In)
        {
            //20150401,100000,86160.000000000,2
            // либо 20150401,100000,86160.000000000,2, Buy/Sell

            _saveString = In;

            string[] sIn = In.Split(',');

            int year = Convert.ToInt32(sIn[0].Substring(0, 4));
            int month = Convert.ToInt32(sIn[0].Substring(4, 2)); 
            int day = Convert.ToInt32(sIn[0].Substring(6, 2));

            int hour = Convert.ToInt32(sIn[1].Substring(0, 2));
            int minute = Convert.ToInt32(sIn[1].Substring(2, 2));
            int second = Convert.ToInt32(sIn[1].Substring(4, 2));

            Time = new DateTime(year, month, day, hour, minute, second);

            Price = Convert.ToDecimal(sIn[2].Replace(".",","));
            
            Volume = Convert.ToInt32(sIn[3]);

            if (sIn.Length > 4)
            {
                Enum.TryParse(sIn[4], true, out Side);
            }

            if (sIn.Length > 5)
            {
                MicroSeconds = Convert.ToInt32(sIn[5]);
            }

        }

    }
}
