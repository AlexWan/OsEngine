/*
 * Your rights to use code governed by this license http://o-s-a.net/doc/license_simple_engine.pdf
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System;
using System.Globalization;

namespace OsEngine.Entity
{
    /// <summary>
    /// tick
    /// тик
    /// </summary>
    public class Trade
    {
        // standard part
        // стандартная часть

        /// <summary>
        /// instrument code for which the transaction took place
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
        /// transaction number in the system
        /// номер сделки в системе
        /// </summary>
        public string Id;

        /// <summary>
        /// volume
        /// объём
        /// </summary>
        public decimal Volume;

        /// <summary>
        /// transaction price
        /// цена сделки
        /// </summary>
        public decimal Price;

        /// <summary>
        /// deal time
        /// время сделки
        /// </summary>
        public DateTime Time;

        /// <summary>
        /// microseconds
        /// микросекунды
        /// </summary>
        public int MicroSeconds;

        /// <summary>
        ///  transaction direction
        /// направление сделки
        /// </summary>
        public Side Side;
        // a new part. This part of the final is not to be downloaded. It can be obtained from OsData, only from standard connectors
        // новая часть. Эту часть с финама не скачать. Её можно добыть OsData, только из стандартных коннекторов

        /// <summary>
        /// the best sale in a glass when this trade came in.
        /// лучшая продажа в стакане, на момент когда пришёл этот трейд
        /// </summary>
        public decimal Bid;

        /// <summary>
        /// the best buy in the glass when this trade came in.
        /// лучшая покупка в стакане, на момент когда пришёл этот трейд
        /// </summary>
        public decimal Ask;

        /// <summary>
        /// the total volume of sales in a glass at the moment when this trade came in
        /// суммарный объём в продажах в стакане, на момент когда пришёл этот трейд
        /// </summary>
        public decimal BidsVolume;

        /// <summary>
        /// the total volume of purchases in the glass at the moment when this trade came in
        /// суммарный объём в покупках в стакане, на момент когда пришёл этот трейд
        /// </summary>
        public decimal AsksVolume;
        // //saving / loading ticks
        //сохранение / загрузка тика

        /// <summary>
        /// to take a line to save
        /// взять строку для сохранения
        /// </summary>
        /// <returns>line with the state of the object/строка с состоянием объекта</returns>
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
        /// upload a tick from a saved line
        /// загрузить тик из сохранённой строки
        /// </summary>
        /// <param name="In"></param>
        public void SetTradeFromString(string In)
        {
            //20150401,100000,86160.000000000,2
            // либо 20150401,100000,86160.000000000,2, Buy/Sell

            if (string.IsNullOrWhiteSpace(In))
            {
                return;
            }

            string[] sIn = In.Split(',');

            if (sIn.Length >= 6 && (sIn[5] == "C" || sIn[5] == "S"))
            {
                // download data from IqFeed
                // загружаем данные из IqFeed
                Time = Convert.ToDateTime(sIn[0]);
                Price = sIn[1].ToDecimal();
                Volume = sIn[2].ToDecimal();
                Bid = sIn[3].ToDecimal();
                Ask = sIn[4].ToDecimal();
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

            Price = sIn[2].ToDecimal();

            Volume = sIn[3].ToDecimal();

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
                Bid = sIn[7].ToDecimal();
                Ask = sIn[8].ToDecimal();
                BidsVolume = Convert.ToInt32(sIn[9]);
                AsksVolume = Convert.ToInt32(sIn[10]);
            }


        }

        private Random _rand = null;
        private Side GetSideIqFeed()
        {
            if (Bid == Price && Bid != Ask) //the deal was for sale/ сделка была на продажу
            {
                return Side.Sell;
            }
            else if (Ask == Price && Bid != Ask) //the deal was to buy/ сделка была на покупку
            {
                return Side.Buy;
            }
            //if (Bid == Ask && Ask == Price) // in other cases, we indicate a random direction/ в остальных случаях указываем случайное направление
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
