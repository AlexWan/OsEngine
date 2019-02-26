/*
 * Your rights to use code governed by this license http://o-s-a.net/doc/license_simple_engine.pdf
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
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

// стандартная часть

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

        /// <summary>
        /// объём
        /// </summary>
        public decimal Volume;

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

// новая часть. Эту часть с финама не скачать. Её можно добыть OsData, только из стандартных коннекторов

        /// <summary>
        /// лучшая продажа в стакане, на момент когда пришёл этот трейд
        /// </summary>
        public decimal Bid;

        /// <summary>
        /// лучшая покупка в стакане, на момент когда пришёл этот трейд
        /// </summary>
        public decimal Ask;

        /// <summary>
        /// суммарный объём в продажах в стакане, на момент когда пришёл этот трейд
        /// </summary>
        public decimal BidsVolume;

        /// <summary>
        /// суммарный объём в покупках в стакане, на момент когда пришёл этот трейд
        /// </summary>
        public decimal AsksVolume;

//сохранение / загрузка тика

        /// <summary>
        /// взять строку для сохранения
        /// </summary>
        /// <returns>строка с состоянием объекта</returns>
        public string GetSaveString()
        {
            //20150401,100000,86160.000000000,2
            // либо 20150401,100000,86160.000000000,2, Buy/Sell
            string result = "";
            result += Time.ToString("yyyyMMdd,HHmmss") + ",";
            result += Price.ToString(CultureInfo.InvariantCulture) + ",";
            result += Volume.ToString(CultureInfo.InvariantCulture) + ",";
            result += Side + ",";
            result += MicroSeconds + ",";
            result += Id;

            if (Bid != 0 && Ask != 0 &&
                BidsVolume != 0 && AsksVolume != 0)
            {
                result += ",";

                result += Bid.ToString(CultureInfo.InvariantCulture) + ",";
                result += Ask.ToString(CultureInfo.InvariantCulture) + ",";
                result += BidsVolume.ToString(CultureInfo.InvariantCulture) + ",";
                result += AsksVolume.ToString(CultureInfo.InvariantCulture);
                
            }

            return result;
        }

        /// <summary>
        /// загрузить тик из сохранённой строки
        /// </summary>
        /// <param name="In"></param>
        public void SetTradeFromString(string In)
        {
            //20150401,100000,86160.000000000,2
            // либо 20150401,100000,86160.000000000,2, Buy/Sell

            string[] sIn = In.Split(',');

            if (sIn.Length >= 6 && (sIn[5] == "C" || sIn[5] == "S"))
            {
                // загружаем данные из IqFeed
                Time = Convert.ToDateTime(sIn[0]);
                Price = Convert.ToDecimal(sIn[1].Replace(",", CultureInfo.InvariantCulture.NumberFormat.NumberDecimalSeparator), CultureInfo.InvariantCulture);
                Volume = Convert.ToDecimal(sIn[2].Replace(",", CultureInfo.InvariantCulture.NumberFormat.NumberDecimalSeparator), CultureInfo.InvariantCulture);
                Bid = Convert.ToDecimal(sIn[3].Replace(",", CultureInfo.InvariantCulture.NumberFormat.NumberDecimalSeparator), CultureInfo.InvariantCulture);
                Ask = Convert.ToDecimal(sIn[4].Replace(",", CultureInfo.InvariantCulture.NumberFormat.NumberDecimalSeparator), CultureInfo.InvariantCulture);
                Side = GetSideIqFeed();

                return;
            }

            int year = Convert.ToInt32(sIn[0].Substring(0, 4));
            int month = Convert.ToInt32(sIn[0].Substring(4, 2)); 
            int day = Convert.ToInt32(sIn[0].Substring(6, 2));

            int hour = Convert.ToInt32(sIn[1].Substring(0, 2));
            int minute = Convert.ToInt32(sIn[1].Substring(2, 2));
            int second = Convert.ToInt32(sIn[1].Substring(4, 2));

            Time = new DateTime(year, month, day, hour, minute, second);
            
            Price = Convert.ToDecimal(sIn[2].Replace(",", CultureInfo.InvariantCulture.NumberFormat.NumberDecimalSeparator), CultureInfo.InvariantCulture);

            Volume = Convert.ToDecimal(sIn[3].Replace(",", CultureInfo.InvariantCulture.NumberFormat.NumberDecimalSeparator), CultureInfo.InvariantCulture);

            if (sIn.Length > 4)
            {
                Enum.TryParse(sIn[4], true, out Side);
            }

            if (sIn.Length > 5)
            {
                MicroSeconds = Convert.ToInt32(sIn[5]);
            }

            if (sIn.Length > 6)
            {
                Id = sIn[6];
            }

            if (sIn.Length > 7)
            {
                Bid = Convert.ToDecimal(sIn[7].Replace(",", CultureInfo.InvariantCulture.NumberFormat.NumberDecimalSeparator), CultureInfo.InvariantCulture);
                Ask = Convert.ToDecimal(sIn[8].Replace(",", CultureInfo.InvariantCulture.NumberFormat.NumberDecimalSeparator), CultureInfo.InvariantCulture);
                BidsVolume = Convert.ToInt32(sIn[9]);
                AsksVolume = Convert.ToInt32(sIn[10]);
            }


        }

        private Random _rand = null;
        private Side GetSideIqFeed()
        {
            if (Bid == Price && Bid != Ask) // сделка была на продажу
            {
                return Side.Sell;
            }
            else if (Ask == Price && Bid != Ask) // сделка была на покупку
            {
                return Side.Buy;
            }
            //if (Bid == Ask && Ask == Price) // в остальных случаях указываем случайное направление
            if (_rand == null)
                _rand = new Random();

            int randValue = _rand.Next(100);
            if (randValue % 2 == 0)
                return Side.Buy;
            else
                return Side.Sell;
        }
    }
}
