using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using OsEngine.Entity;
using OsEngine.Market.Servers.Bybit.Utilities;
using OsEngine.Market.Services;

namespace OsEngine.Market.Servers.Bybit.Entities
{
    class BybitMarketDepthCreator : BaseMarketDepthUpdater
    {
        private List<MarketDepth> all_depths = new List<MarketDepth>();

        private object locker = new object();

        public List<MarketDepth> CreateNew(JToken data, Client client)
        {
            lock (locker)
            {
                var new_depth = new MarketDepth();

                new_depth.Time = DateTime.UtcNow;

                JToken[] jt_md_points = data.Children().ToArray();

                string security_name = "";

                if (client.FuturesMode == "Inverse")
                {
                    jt_md_points = data.Children().ToArray();
                    security_name = jt_md_points[0].SelectToken("symbol").Value<string>();
                }

                if (client.FuturesMode == "USDT")
                {
                    jt_md_points = data.SelectToken("order_book").ToArray();
                    security_name = jt_md_points[0].SelectToken("symbol").Value<string>();
                }

                new_depth.SecurityNameCode = security_name;
                new_depth.Time = DateTime.Now;

                foreach (var jt_item in jt_md_points.Where(x => x.SelectToken("side").Value<string>() == "Sell"))
                {
                    new_depth.Asks.Add(new MarketDepthLevel()
                    {
                        Ask = jt_item.SelectToken("size").Value<decimal>(),
                        Bid = 0,
                        Price = jt_item.SelectToken("price").Value<decimal>(),
                    });
                }

                foreach (var jt_item in jt_md_points.Where(x => x.SelectToken("side").Value<string>() == "Buy"))
                {
                    new_depth.Bids.Add(new MarketDepthLevel()
                    {
                        Ask = 0,
                        Bid = jt_item.SelectToken("size").Value<decimal>(),
                        Price = jt_item.SelectToken("price").Value<decimal>(),
                    });
                }

                MarketDepth md_to_remove = all_depths.Find(x => x.SecurityNameCode == security_name);

                if (md_to_remove != null)
                {
                    all_depths.Remove(md_to_remove);
                }

                SortBids(new_depth.Bids);
                SortAsks(new_depth.Asks);

                all_depths.Add(new_depth);

                return all_depths;
            }
        }

        public List<MarketDepth> Update(JToken data)
        {
            if (all_depths.Count() == 0)
                return all_depths;

            lock (locker)
            {
                JToken[] jt_delete = data.SelectToken("delete").ToArray();

                JToken[] jt_update = data.SelectToken("update").ToArray();

                JToken[] jt_insert = data.SelectToken("insert").ToArray();

                if (jt_delete != null && jt_delete.Count() > 0)
                {
                    foreach (var jt_item in jt_delete)
                    {
                        if (jt_item.SelectToken("side").Value<string>() == "Sell")
                        {
                            try
                            {
                                string security_name = jt_item.SelectToken("symbol").Value<string>();

                                var price_to_del = jt_item.SelectToken("price").Value<decimal>();

                                all_depths.Find(x => x.SecurityNameCode == security_name).Asks.Remove(all_depths.Find(x => x.SecurityNameCode == security_name).Asks.Find(x => x.Price == price_to_del));

                            }
                            catch (Exception ex)
                            {
                                MessageBox.Show(ex.ToString());
                            }
                        }

                        if (jt_item.SelectToken("side").Value<string>() == "Buy")
                        {
                            try
                            {
                                string security_name = jt_item.SelectToken("symbol").Value<string>();

                                var price_to_del = jt_item.SelectToken("price").Value<decimal>();

                                all_depths.Find(x => x.SecurityNameCode == security_name).Bids.Remove(all_depths.Find(x => x.SecurityNameCode == security_name).Bids.Find(x => x.Price == price_to_del));

                            }
                            catch (Exception ex)
                            {
                                MessageBox.Show(ex.ToString());
                            }
                        }
                    }
                }

                if (jt_update != null && jt_update.Count() > 0)
                {
                    foreach (var jt_item in jt_update)
                    {
                        if (jt_item.SelectToken("side").Value<string>() == "Sell")
                        {
                            try
                            {
                                string security_name = jt_item.SelectToken("symbol").Value<string>();

                                var new_ask_level = new MarketDepthLevel()
                                {
                                    Ask = jt_item.SelectToken("size").Value<decimal>(),
                                    Bid = 0,
                                    Price = jt_item.SelectToken("price").Value<decimal>(),
                                };

                                all_depths.Find(x => x.SecurityNameCode == security_name).Asks.Find(x => x.Price == new_ask_level.Price).Ask = new_ask_level.Ask;

                            }
                            catch (Exception ex)
                            {
                                MessageBox.Show(ex.ToString());
                            }
                        }

                        if (jt_item.SelectToken("side").Value<string>() == "Buy")
                        {
                            try
                            {
                                string security_name = jt_item.SelectToken("symbol").Value<string>();

                                var new_bid_level = new MarketDepthLevel()
                                {
                                    Ask = 0,
                                    Bid = jt_item.SelectToken("size").Value<decimal>(),
                                    Price = jt_item.SelectToken("price").Value<decimal>(),
                                };

                                all_depths.Find(x => x.SecurityNameCode == security_name).Bids.Find(x => x.Price == new_bid_level.Price).Bid = new_bid_level.Bid;

                            }
                            catch (Exception ex)
                            {
                                MessageBox.Show(ex.ToString());
                            }
                        }
                    }
                }

                if (jt_insert != null && jt_insert.Count() > 0)
                {
                    foreach (var jt_item in jt_insert)
                    {
                        if (jt_item.SelectToken("side").Value<string>() == "Sell")
                        {
                            try
                            {
                                string security_name = jt_item.SelectToken("symbol").Value<string>();

                                var new_ask_level = new MarketDepthLevel()
                                {
                                    Ask = jt_item.SelectToken("size").Value<decimal>(),
                                    Bid = 0,
                                    Price = jt_item.SelectToken("price").Value<decimal>(),
                                };

                                InsertLevel(new_ask_level.Price, new_ask_level.Ask, Side.Sell, all_depths.Find(x => x.SecurityNameCode == security_name));

                            }
                            catch (Exception ex)
                            {
                                MessageBox.Show(ex.ToString());
                            }
                        }

                        if (jt_item.SelectToken("side").Value<string>() == "Buy")
                        {
                            try
                            {
                                string security_name = jt_item.SelectToken("symbol").Value<string>();

                                var new_bid_level = new MarketDepthLevel()
                                {
                                    Ask = 0,
                                    Bid = jt_item.SelectToken("size").Value<decimal>(),
                                    Price = jt_item.SelectToken("price").Value<decimal>(),
                                };

                                InsertLevel(new_bid_level.Price, new_bid_level.Bid, Side.Buy, all_depths.Find(x => x.SecurityNameCode == security_name));

                            }
                            catch (Exception ex)
                            {
                                MessageBox.Show(ex.ToString());
                            }
                        }
                    }
                }

                return all_depths;
            }
        }
    }
}
