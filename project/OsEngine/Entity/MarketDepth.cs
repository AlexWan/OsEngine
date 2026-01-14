/*
 * Your rights to use code governed by this license http://o-s-a.net/doc/license_simple_engine.pdf
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using OsEngine.Market.Servers.Entity;
using OsEngine.OsData.BinaryEntity;
using System;
using System.Collections.Generic;

namespace OsEngine.Entity
{
    public class MarketDepth
    {
        /// <summary>
        /// Constructor
        /// </summary>
        public MarketDepth()
        {
            Asks = new List<MarketDepthLevel>();
            Bids = new List<MarketDepthLevel>();
        }

        /// <summary>
        /// Time to create a market depth
        /// </summary>
        public DateTime Time;

        /// <summary>
        /// Levels of sales. Best with index 0
        /// </summary>
        public List<MarketDepthLevel> Asks;

        /// <summary>
        /// Levels of buy. Best with index 0
        /// </summary>
        public List<MarketDepthLevel> Bids;

        /// <summary>
        /// Total sales volume
        /// </summary>
        public decimal AskSummVolume
        {
            get
            {
                decimal vol = 0;
                
                try
                {
                    for (int i = 0; Asks != null && i < Asks.Count; i++)
                    {
                        vol += Asks[i].Ask.ToDecimal();
                    }
                }
                catch
                {
                    return vol;
                }

                return vol;
            }
        }

        /// <summary>
        /// Total buy volume
        /// </summary>
        public decimal BidSummVolume
        {
            get
            {
                decimal vol = 0;

                try
                {
                    for (int i = 0; Bids != null && i < Bids.Count; i++)
                    {
                        vol += Bids[i].Bid.ToDecimal();
                    }
                }
                catch
                {
                    return vol;
                }

                return vol;
            }
        }

        /// <summary>
        /// Security name that owns to market depth
        /// </summary>
        public string SecurityNameCode;

        /// <summary>
        /// Price of the center of market depth
        /// </summary>
        public double CentrePrice
        {
            get
            {
                if (Asks == null
                    || Asks.Count == 0)
                {
                    return -1;
                }
                if (Bids == null
                    || Bids.Count == 0)
                {
                    return -1;
                }

                double bestAsk = Asks[0].Price;
                double bestBid = Bids[0].Price;

                if (bestAsk == 0
                    || bestBid == 0)
                {
                    return -1;
                }

                if (bestBid >= bestAsk)
                {
                    return -1;
                }

                double result = bestBid + ((bestAsk - bestBid)/2);

                return result;
            }
        }

        /// <summary>
        /// Distance between the best buy and the best sell in absolute terms
        /// If the calculation is unsuccessful, it returns -1
        /// </summary>
        public double SpreadAbsolute
        {
            get
            {
                if(Asks == null 
                    || Asks.Count == 0)
                {
                    return -1;
                }
                if (Bids == null
                    || Bids.Count == 0)
                {
                    return -1;
                }

                double bestAsk = Asks[0].Price;
                double bestBid = Bids[0].Price;

                if(bestAsk == 0 
                    ||  bestBid == 0)
                {
                    return -1;
                }

                if(bestBid >= bestAsk)
                {
                    return -1;
                }

                double spread = bestAsk - bestBid;

                return spread;
            }
        }

        /// <summary>
        /// The distance between the best buy and the best sell as a percentage. 
        /// If the calculation is unsuccessful, it returns -1
        /// </summary>
        public double SpreadPercent
        {
            get
            {
                if (Asks == null
                    || Asks.Count == 0)
                {
                    return -1;
                }
                if (Bids == null
                    || Bids.Count == 0)
                {
                    return -1;
                }

                double bestAsk = Asks[0].Price;
                double bestBid = Bids[0].Price;

                if (bestAsk == 0
                    || bestBid == 0)
                {
                    return -1;
                }

                if (bestBid >= bestAsk)
                {
                    return -1;
                }

                double spreadAbs = bestAsk - bestBid;

                double spreadPercent = spreadAbs / (bestBid / 100);

                return spreadPercent;
            }
        }

        /// <summary>
        /// Slippage from center of market depth to purchase for a certain amount of money
        /// </summary>
        /// <param name="direction">Direction for transaction</param>
        /// <param name="security">Class of security that wields the market depth</param>
        /// <param name="money">Amount for which the transaction is expected</param>
        public double GetSlippagePercentToEntry(Side direction, Security security, decimal money)
        {
            if(direction == Side.None
                || security == null
                || money <= 0)
            {
                return -1;
            }

            double centrePrice = this.CentrePrice;

            if(centrePrice == -1)
            {
                return -1;
            }

            double moneyOnPosition = (double)money;

            if (direction == Side.Buy)
            {
                // ПОКУПКА
                double moneySumm = 0;
                double furtherPrice = 0;

                for(int i = 0;i < Asks.Count;i++)
                {
                    if(moneySumm > moneyOnPosition)
                    {
                        break;
                    }
                    MarketDepthLevel currentLevel = Asks[i];

                    if(currentLevel.Ask == 0
                        || currentLevel.Price == 0)
                    {
                        continue;
                    }

                    furtherPrice = currentLevel.Price;

                    double moneyOnLevel = currentLevel.Ask * currentLevel.Price;

                    if (security.PriceStep != security.PriceStepCost
                       && security.PriceStep != 0
                       && security.PriceStepCost != 0)
                    {
                        moneyOnLevel = moneyOnLevel
                            / (double)security.PriceStep * (double)security.PriceStepCost;
                    }

                    if (security.Lot != 0)
                    {
                        moneyOnLevel = moneyOnLevel * (double)security.Lot;
                    }

                    moneySumm += moneyOnLevel;
                }

                if(furtherPrice != 0)
                {
                    double slippageAbs = furtherPrice - centrePrice;
                    double slippagePercent = slippageAbs / (centrePrice / 100);

                    return Math.Round(slippagePercent, 4);
                }
            }
            if (direction == Side.Sell)
            {
                // ПРОДАЖА
                double moneySumm = 0;
                double furtherPrice = 0;

                for (int i = 0; i < Bids.Count; i++)
                {
                    if (moneySumm > moneyOnPosition)
                    {
                        break;
                    }
                    MarketDepthLevel currentLevel = Bids[i];

                    if (currentLevel.Bid == 0
                        || currentLevel.Price == 0)
                    {
                        continue;
                    }

                    furtherPrice = currentLevel.Price;

                    double moneyOnLevel = currentLevel.Bid * currentLevel.Price;

                    if (security.PriceStep != security.PriceStepCost
                       && security.PriceStep != 0
                       && security.PriceStepCost != 0)
                    {
                        moneyOnLevel = moneyOnLevel
                            / (double)security.PriceStep * (double)security.PriceStepCost;
                    }

                    if (security.Lot != 0)
                    {
                        moneyOnLevel = moneyOnLevel * (double)security.Lot;
                    }

                    moneySumm += moneyOnLevel;
                }

                if (furtherPrice != 0)
                {
                    double slippageAbs = centrePrice - furtherPrice;
                    double slippagePercent = slippageAbs / (centrePrice / 100);

                    return Math.Round(slippagePercent, 4);
                }
            }

            return -1;
        }

        #region Service

        public long LastBinaryPrice = 0;

        public void SetMarketDepthFromBinaryFile(DataBinaryReader dr, decimal priceStep, decimal volumeStep, long lastMilliseconds)
        {
            long count = dr.ReadLeb128();

            for (int i = 0; i < count; i++)
            {
                LastBinaryPrice += dr.ReadLeb128();

                int priceScale = BitConverter.GetBytes(decimal.GetBits(priceStep)[3])[2];
                decimal priceDecimal = (decimal)LastBinaryPrice * priceStep;
                priceDecimal = Math.Round(priceDecimal, priceScale);
                double normalPrice = (double)priceDecimal;

                long volumeRaw = dr.ReadLeb128();
                int volumeScale = BitConverter.GetBytes(decimal.GetBits(volumeStep)[3])[2];
                decimal volumeDecimal = (decimal)volumeRaw * volumeStep;
                volumeDecimal = Math.Round(volumeDecimal, volumeScale);
                double normalvolume = (double)volumeDecimal;

                if (normalvolume == 0)
                {
                    bool isDelete = false;
                    for (int i2 = Bids.Count - 1; i2 >= 0; i2--)
                    {
                        if (Bids[i2].Price == normalPrice)
                        {
                            Bids.Remove(Bids[i2]);
                            isDelete = true;
                            break;
                        }
                    }

                    if (isDelete) continue;

                    for (int i2 = Asks.Count - 1; i2 >= 0; i2--)
                    {
                        if (Asks[i2].Price == normalPrice)
                        {
                            Asks.Remove(Asks[i2]);
                            break;
                        }
                    }
                }
                else if (normalvolume > 0)
                {
                    bool inArray = false;

                    if (Asks.Count > 0)
                    {
                        for (int i2 = 0; i2 < Asks.Count; i2++)
                        {
                            if (Asks[i2].Price == normalPrice)
                            {
                                Asks[i2].Ask = normalvolume;
                                inArray = true;
                                break;
                            }
                        }

                        if (inArray) continue;

                        for (int i2 = Bids.Count - 1; i2 >= 0; i2--)
                        {
                            if (Bids[i2].Price == normalPrice)
                            {
                                Bids.Remove(Bids[i2]);
                            }
                        }
                    }

                    MarketDepthLevel newAsk = new MarketDepthLevel();
                    newAsk.Price = normalPrice;
                    newAsk.Ask = normalvolume;
                    Asks.Add(newAsk);
                }
                else if (normalvolume < 0)
                {
                    bool inArray = false;
                    if (Bids.Count > 0)
                    {
                        for (int i2 = 0; i2 < Bids.Count; i2++)
                        {
                            if (Bids[i2].Price == normalPrice)
                            {
                                Bids[i2].Bid = -normalvolume;
                                inArray = true;
                                break;
                            }
                        }

                        if (inArray) continue;

                        for (int i2 = Asks.Count - 1; i2 >= 0; i2--)
                        {
                            if (Asks[i2].Price == normalPrice)
                            {
                                Asks.Remove(Asks[i2]);
                            }
                        }
                    }

                    MarketDepthLevel newBid = new MarketDepthLevel();
                    newBid.Price = normalPrice;
                    newBid.Bid = -normalvolume;
                    Bids.Add(newBid);
                }
            }

            Bids.Sort((bid1, bid2) => bid2.Price.CompareTo(bid1.Price));
            Asks.Sort((ask1, ask2) => ask1.Price.CompareTo(ask2.Price));
        }

        /// <summary>
        /// Set the market depth from the stored value
        /// </summary>
        public void SetMarketDepthFromString(string str)
        {
            string[] save = str.Split('_');

            Time = DateTimeParseHelper.ParseFromTwoStrings(save[0], save[1]);

            Time = Time.AddMilliseconds(Convert.ToInt32(save[2]));

            string[] bids = save[3].Split('*');

            Asks = new List<MarketDepthLevel>();

            for (int i = 0; i < bids.Length - 1; i++)
            {
                string[] val = bids[i].Split('&');

                MarketDepthLevel newBid = new MarketDepthLevel();
                newBid.Ask = val[0].ToDouble();
                newBid.Price = val[1].ToDouble();
                Asks.Add(newBid);
            }

            string[] asks = save[4].Split('*');

            Bids = new List<MarketDepthLevel>();

            for (int i = 0; i < asks.Length - 1; i++)
            {
                string[] val = asks[i].Split('&');

                MarketDepthLevel newAsk = new MarketDepthLevel();
                newAsk.Bid = val[0].ToDouble();
                newAsk.Price = val[1].ToDouble();
                Bids.Add(newAsk);
            }
        }

        /// <summary>
        /// Take the save string for the whole market depth
        /// </summary>
        /// <param name="depth">depth of market depth to keep</param>
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
        /// Take a "deep" copy of the market depth
        /// </summary>
        public MarketDepth GetCopy(int levelsCount = 100)
        {
            MarketDepth newDepth = new MarketDepth();
            newDepth.Time = Time;
            newDepth.SecurityNameCode = SecurityNameCode;

            if(levelsCount == 100)
            {
                newDepth.Asks = new List<MarketDepthLevel>();

                for (int i = 0; Asks != null && i < Asks.Count; i++)
                {
                    newDepth.Asks.Add(new MarketDepthLevel());
                    newDepth.Asks[i].Ask = Asks[i].Ask;
                    newDepth.Asks[i].Price = Asks[i].Price;
                }

                newDepth.Bids = new List<MarketDepthLevel>();

                for (int i = 0; Bids != null && i < Bids.Count; i++)
                {
                    newDepth.Bids.Add(new MarketDepthLevel());
                    newDepth.Bids[i].Bid = Bids[i].Bid;
                    newDepth.Bids[i].Price = Bids[i].Price;
                }
            }
            else
            {
                newDepth.Asks = new List<MarketDepthLevel>();

                for (int i = 0; Asks != null && i < Asks.Count && i < levelsCount; i++)
                {
                    newDepth.Asks.Add(new MarketDepthLevel());
                    newDepth.Asks[i].Ask = Asks[i].Ask;
                    newDepth.Asks[i].Price = Asks[i].Price;
                }

                newDepth.Bids = new List<MarketDepthLevel>();

                for (int i = 0; Bids != null && i < Bids.Count && i < levelsCount; i++)
                {
                    newDepth.Bids.Add(new MarketDepthLevel());
                    newDepth.Bids[i].Bid = Bids[i].Bid;
                    newDepth.Bids[i].Price = Bids[i].Price;
                }
            }

            return newDepth;
        }

        #endregion

    }

    /// <summary>
    /// Market depth representing one price level
    /// </summary>
    public class MarketDepthLevel
    {
        /// <summary>
        /// Number of contracts for SALE at this price level
        /// </summary>
        public double Ask;

        /// <summary>
        /// Number of contracts for BUY this price level
        /// </summary>
        public double Bid;

        /// <summary>
        /// Price
        /// </summary>
        public double Price;

        /// <summary>
        /// Unique price level number
        /// </summary>
        public long Id;
    }
}