/*
 * Your rights to use code governed by this license http://o-s-a.net/doc/license_simple_engine.pdf
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System;
using System.Globalization;

namespace OsEngine.Entity
{
    /// <summary>
    /// Tick
    /// </summary>
    public class Trade
    {
        // standard part
        
        /// <summary>
        /// Instrument code for which the transaction took place
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
        /// Transaction number in the system
        /// </summary>
        public string Id;

        /// <summary>
        /// Transaction number in the system. In Tester and Optimizer
        /// </summary>
        public long IdInTester;

        /// <summary>
        /// Volume
        /// </summary>
        public decimal Volume;

        /// <summary>
        /// Transaction price
        /// </summary>
        public decimal Price;

        /// <summary>
        /// Deal time
        /// </summary>
        public DateTime Time;

        /// <summary>
        /// Microseconds
        /// </summary>
        public int MicroSeconds;

        /// <summary>
        ///  Transaction direction
        /// </summary>
        public Side Side;

        /// <summary>
        /// Open interest
        /// </summary>
        public decimal OpenInterest;

        /// <summary>
        /// Tester only. Timeframe of the candlestick that generated the trade
        /// </summary>
        public TimeFrame TimeFrameInTester;

        // a new part. This part of the final is not to be downloaded. It can be obtained from OsData, only from standard connectors

        /// <summary>
        /// The best buy in the market depth when this trade came in.
        /// </summary>
        public decimal Bid;

        /// <summary>
        /// The best sale in a market depth when this trade came in.
        /// </summary>
        public decimal Ask;

        /// <summary>
        /// The total volume of buy in the market depth at the moment when this trade came in
        /// </summary>
        public decimal BidsVolume;

        /// <summary>
        /// The total volume of sales in a market depth at the moment when this trade came in
        /// </summary>
        public decimal AsksVolume;

        // //saving / loading ticks
        
        /// <summary>
        ///To take a line to save
        /// </summary>
        /// <returns>line with the state of the object</returns>
        public string GetSaveString()
        {
            //20150401,100000,86160.000000000,2
            // либо 20150401,100000,86160.000000000,2, Buy/Sell
            string result = "";
            result += Time.ToString("yyyyMMdd,HHmmss") + ",";
            result += Price.ToString(CultureInfo.InvariantCulture) + ",";
            result += Volume.ToString(CultureInfo.InvariantCulture) + ",";
            result += Side + ",";
            result += MicroSeconds;

            if (Id != null)
            {
                result += ",";
                result += Id;
            }
            else
            {
                result += ",";
            }

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
        /// Upload a tick from a saved line
        /// </summary>
        /// <param name="In">incoming data</param>
        public void SetTradeFromString(string In)
        {
            //20150401,100000,86160.000000000,2
            // либо 20150401,100000,86160.000000000,2, Buy/Sell

            if(string.IsNullOrWhiteSpace(In))
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

            Time = DateTimeParseHelper.ParseFromTwoStrings(sIn[0], sIn[1]);
            
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

            if (sIn.Length > 8)
            {
                Bid = sIn[7].ToDecimal();
                Ask = sIn[8].ToDecimal();
                BidsVolume = sIn[9].ToDecimal();
                AsksVolume = sIn[10].ToDecimal();
            }
        }

        private Random _rand = null;

        /// <summary>
        /// direction generation for transactions from IqFeed
        /// </summary>
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
