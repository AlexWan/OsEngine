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
        /// Levels of sales. best with index 0
        /// </summary>
        public List<MarketDepthLevel> Asks;

        /// <summary>
        /// Levels of buy. best with index 0
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
                for (int i = 0; Asks != null && i < Asks.Count; i++)
                {
                    vol += Asks[i].Ask.ToDecimal();
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
                for (int i = 0; Bids != null && i < Bids.Count; i++)
                {
                    vol += Bids[i].Bid.ToDecimal();
                }
                return vol;
            }
        }

        /// <summary>
        /// Security that owns to market depth
        /// </summary>
        public string SecurityNameCode;

        public int LastBinaryPrice = 0;

        public void SetMarketDepthFromBinaryFile(DataBinaryReader dr, decimal priceStep, double volumeStep, long lastMilliseconds)
        {
            lastMilliseconds = dr.ReadGrowing(lastMilliseconds);
            Time = TimeManager.GetDateTimeFromStartTimeMilliseconds(lastMilliseconds);

            int n = (int)dr.ReadLeb128();

            for (int i = 0; i < n; i++)
            {
                LastBinaryPrice += (int)dr.ReadLeb128();
                double volume = (int)dr.ReadLeb128() * volumeStep;

                if (volume == 0)
                {
                    bool isDelete = false;
                    for (int i2 = Bids.Count - 1; i2 >= 0; i2--)
                    {
                        if (Bids[i2].Price == LastBinaryPrice * (double)priceStep)
                        {
                            Bids.Remove(Bids[i2]);
                            isDelete = true;
                            break;
                        }
                    }

                    if (isDelete) continue;

                    for (int i2 = Asks.Count - 1; i2 >= 0; i2--)
                    {
                        if (Asks[i2].Price == LastBinaryPrice * (double)priceStep)
                        {
                            Asks.Remove(Asks[i2]);
                            break;
                        }
                    }
                }
                else if (volume > 0)
                {
                    bool inArray = false;

                    if (Asks.Count > 0)
                    {
                        for (int i2 = 0; i2 < Asks.Count; i2++)
                        {
                            if (Asks[i2].Price == LastBinaryPrice * (double)priceStep)
                            {
                                Asks[i2].Ask = volume * volumeStep;
                                inArray = true;
                                break;
                            }
                        }

                        for (int i2 = Bids.Count - 1; i2 >= 0; i2--)
                        {
                            if (Bids[i2].Price == LastBinaryPrice * (double)priceStep)
                            {
                                Bids.Remove(Bids[i2]);
                                break;
                            }
                        }
                    }

                    if (inArray) continue;

                    MarketDepthLevel newAsk = new MarketDepthLevel();
                    newAsk.Price = LastBinaryPrice * (double)priceStep;
                    newAsk.Ask = volume * volumeStep;
                    Asks.Add(newAsk);
                }
                else if (volume < 0)
                {
                    bool inArray = false;
                    if (Bids.Count > 0)
                    {
                        for (int i2 = 0; i2 < Bids.Count; i2++)
                        {
                            if (Bids[i2].Price == LastBinaryPrice * (double)priceStep)
                            {
                                Bids[i2].Bid = -(volume * volumeStep);
                                inArray = true;
                                break;
                            }
                        }

                        for (int i2 = Asks.Count - 1; i2 >= 0; i2--)
                        {
                            if (Asks[i2].Price == LastBinaryPrice * (double)priceStep)
                            {
                                Asks.Remove(Asks[i2]);
                                break;
                            }
                        }
                    }

                    if (inArray) continue;

                    MarketDepthLevel newBid = new MarketDepthLevel();
                    newBid.Price = LastBinaryPrice * (double)priceStep;
                    newBid.Bid = -(volume * volumeStep);
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
                newDepth.Asks[i].Price = Asks[i].Price;
            }


            newDepth.Bids = new List<MarketDepthLevel>();

            for (int i = 0; Bids != null && i < Bids.Count; i++)
            {
                newDepth.Bids.Add(new MarketDepthLevel());
                newDepth.Bids[i].Bid = Bids[i].Bid;
                newDepth.Bids[i].Price = Bids[i].Price;
            }

            return newDepth;
        }
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