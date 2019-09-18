/*
 * Your rights to use code governed by this license http://o-s-a.net/doc/license_simple_engine.pdf
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System;
using System.Collections.Generic;

namespace OsEngine.Entity
{
    public class MarketDepth
    {
        public MarketDepth()
        {
            Asks = new List<MarketDepthLevel>();
            Bids = new List<MarketDepthLevel>();
        }
        /// <summary>
        /// time to create a glass
        /// время создания стакана
        /// </summary>
        public DateTime Time;

        /// <summary>
        /// levels of sales. best with index 0
        /// уровни продаж. лучшая с индексом 0
        /// </summary>
        public List<MarketDepthLevel> Asks;

        /// <summary>
        /// purchase levels. best with index 0
        /// уровни покупок. лучшая с индексом 0
        /// </summary>
        public List<MarketDepthLevel> Bids;

        /// <summary>
        /// total sales volume
        /// суммарный объём в продажах
        /// </summary>
        public decimal AskSummVolume
        {
            get
            {
                decimal vol = 0;
                for (int i = 0; Asks != null && i < Asks.Count; i++)
                {
                    vol += Asks[i].Ask;
                }
                return vol;
            }
        }

        /// <summary>
        /// total amount in purchases
        /// суммарный объём в покупках
        /// </summary>
        public decimal BidSummVolume
        {
            get
            {
                decimal vol = 0;
                for (int i = 0; Bids != null && i < Bids.Count; i++)
                {
                    vol += Bids[i].Bid;
                }
                return vol;
            }
        }

        /// <summary>
        /// security that owns to glass
        /// бумага, которой принадлежит стакан
        /// </summary>
        public string SecurityNameCode;

        /// <summary>
        /// set the cup from the stored value
        /// установить стакан из сохранённого значения
        /// </summary>
        public void SetMarketDepthFromString(string str)
        {
            string[] save = str.Split('_');

            int year =
            Convert.ToInt32(save[0][0].ToString() + save[0][1].ToString() + save[0][2].ToString() +
                      save[0][3].ToString());
            int month = Convert.ToInt32(save[0][4].ToString() + save[0][5].ToString());
            int day = Convert.ToInt32(save[0][6].ToString() + save[0][7].ToString());
            int hour = Convert.ToInt32(save[1][0].ToString() + save[1][1].ToString());
            int minute = Convert.ToInt32(save[1][2].ToString() + save[1][3].ToString());
            int second = Convert.ToInt32(save[1][4].ToString() + save[1][5].ToString());

            Time = new DateTime(year, month, day, hour, minute, second);

            Time = Time.AddMilliseconds(Convert.ToInt32(save[2]));

            
            string[] bids = save[3].Split('*');

            Asks = new List<MarketDepthLevel>();

            for (int i = 0; i < bids.Length - 1; i++)
            {
                string[] val = bids[i].Split('&');

                MarketDepthLevel newBid = new MarketDepthLevel();
                newBid.Ask = Convert.ToDecimal(val[0]);
                newBid.Price = Convert.ToDecimal(val[1]);
                Asks.Add(newBid);
            }

            string[] asks = save[4].Split('*');

            Bids = new List<MarketDepthLevel>();

            for (int i = 0; i < asks.Length - 1; i++)
            {
                string[] val = asks[i].Split('&');

                MarketDepthLevel newAsk = new MarketDepthLevel();
                newAsk.Bid = Convert.ToDecimal(val[0]);
                newAsk.Price = Convert.ToDecimal(val[1]);
                Bids.Add(newAsk);
            }
        }

        /// <summary>
        /// take the save string for the whole glass
        /// взять строку сохранения для всего стакана
        /// </summary>
        /// <param name="depth">depth of glass to keep/глубина стакана которую нужно сохранить</param>
        public string GetSaveStringToAllDepfh(int depth)
        {
            // NameSecurity_Time_Bids_Asks
            // Bids: level*level*level
            // level: Bid&Ask&Price

            if (depth == 0)
            {
                depth = 1;
            }

            string result = "";

            result += Time.ToString("yyyyMMdd_HHmmss") + "_";


            result += Time.Millisecond + "_"; 

            for (int i = 0; i < Asks.Count && i < depth; i++)
            {
                result += Asks[i].Ask + "&" + Asks[i].Price + "*";
            }
            result += "_";

            for (int i = 0; i < Bids.Count && i < depth; i++)
            {
                result += Bids[i].Bid + "&" + Bids[i].Price + "*";
            }

            return result;
        }

        /// <summary>
        /// take a "deep" copy of the glass
        /// взять "глубокую" копию стакана
        /// </summary>
        public MarketDepth GetCopy()
        {
            MarketDepth newDepth = new MarketDepth();
            newDepth.Time = Time;
            newDepth.SecurityNameCode = SecurityNameCode;
            newDepth.Asks = new List<MarketDepthLevel>();

            for (int i = 0; Asks != null && i < Asks.Count; i++)
            {
                newDepth.Asks.Add(new MarketDepthLevel());
                newDepth.Asks[i].Ask = Asks[i].Ask;
                newDepth.Asks[i].Bid = Asks[i].Bid;
                newDepth.Asks[i].Price = Asks[i].Price;
            }


            newDepth.Bids = new List<MarketDepthLevel>();

            for (int i = 0; Bids != null && i < Bids.Count; i++)
            {
                newDepth.Bids.Add(new MarketDepthLevel());
                newDepth.Bids[i].Ask = Bids[i].Ask;
                newDepth.Bids[i].Bid = Bids[i].Bid;
                newDepth.Bids[i].Price = Bids[i].Price;
            }

            return newDepth;
        }
    }

    /// <summary>
    /// class representing one price level in a glass
    /// класс представляющий один ценовой уровень в стакане
    /// </summary>
    public class MarketDepthLevel
    {

        /// <summary>
        /// number of contracts for sale at this price level
        /// количество контрактов на продажу по этому уровню цены
        /// </summary>
        public decimal Ask;

        /// <summary>
        /// \number of purchase contracts at this price level
        /// количество контрактов на покупку по этому уровню цены
        /// </summary>
        public decimal Bid;

        /// <summary>
        /// price
        /// цена
        /// </summary>
        public decimal Price;

        /// <summary>
        /// Unique price level number, required for working with BitMex
        /// уникальный номер ценового уровня, необходим для работы с BitMex
        /// </summary>
        public long Id;
    }
}
