/*
 * Your rights to use the code are governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Если вы не покупали лицензии, то Ваши права на использования кода ограничены не коммерческим использованием и 
 * регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System;
using System.Collections.Generic;
using System.Linq;
using OsEngine.Entity;
using ru.micexrts.cgate.message;

namespace OsEngine.Market.Servers.Plaza.Internal
{
    public class PlazaMarketDepthCreator
    {
        
        private List<MarketDepth> _marketDepths;

        private List<RevisionInfo> _marketDepthsRevisions;

        public void ClearAll()
        {
            _marketDepths = new List<MarketDepth>();
        }

        public void ClearDepthToSecurity(Security security)
        {
            if (_marketDepths == null)
            {
                return;
            }

            MarketDepth myDepth = _marketDepths.Find(depth => depth.SecurityNameCode == security.Name);

            if (myDepth == null)
            {
                return;
            }

            _marketDepths.Remove(myDepth);
        }

        public void ClearFromRevision(P2ReplClearDeletedMessage msg)
        {
            // msg.TableIdx;
            // msg.TableRev;

            for (int i = 0; _marketDepthsRevisions != null && i < _marketDepthsRevisions.Count; i++)
            {
                if (_marketDepthsRevisions[i].TableRevision < msg.TableRev)
                {
                    ClearThisRevision(_marketDepthsRevisions[i]);
                }
            }

        }

        private void ClearThisRevision(RevisionInfo info)
        {
            try
            {
                _marketDepthsRevisions.Remove(info);

                MarketDepth myDepth = _marketDepths.Find(depth => depth.SecurityNameCode == info.Security);

                if (myDepth == null)
                {
                    return;
                }

                if (myDepth.Bids != null && myDepth.Bids.Count != 0)
                {
                    List<MarketDepthLevel> ask = myDepth.Bids.ToList();

                    for (int i = 0; i < ask.Count; i++)
                    {
                        if (ask[i].Price == info.Price)
                        {
                            ask.Remove(ask[i]);
                            myDepth.Bids = ask;
                        }
                    }
                }

                if (myDepth.Asks != null && myDepth.Asks.Count == 0)
                {
                    List<MarketDepthLevel> bid = myDepth.Asks.ToList();

                    for (int i = 0; i < bid.Count; i++)
                    {
                        if (bid[i].Price == info.Price)
                        {
                            bid.Remove(bid[i]);
                            myDepth.Asks = bid;
                        }
                    }
                }
            }
            catch (Exception)
            {
                // ignore
            }
        }

        /// <summary>
        /// download a new line with changing in the depth
        /// загрузить новую строку с изменением в станане
        /// </summary>
        /// <param name="replmsg">message line/строка сообщения</param>
        /// <param name="security">message instrument/инструмент которому принадлежит сообщение</param>
        /// <returns>returns depth with changings/возвращаем стакан в который было внесено изменение</returns>
        public MarketDepth SetNewMessage(StreamDataMessage replmsg, Security security)
        {
            long replAct = replmsg["replAct"].asLong();

            string[] str = replmsg.ToString().Split(':');

            int volume = replmsg["volume"].asInt();

            if (volume != 0 && replAct == 0 && replmsg.MsgName == "orders_aggr")
            {
                return Insert(replmsg, security);
            }
            else
            {
                return Delete(replmsg, security);
            }
        }

        public MarketDepth Insert(StreamDataMessage replmsg, Security security)
        {
            // process revision / обрабатываем ревизию

            // create new / создаём новую
            RevisionInfo revision = new RevisionInfo();
            revision.Price = Convert.ToDecimal(replmsg["price"].asDecimal());
            revision.TableRevision = replmsg["replRev"].asLong();
            revision.ReplId = replmsg["replID"].asLong();
            revision.Security = security.Name;

            if (_marketDepthsRevisions == null)
            {
                _marketDepthsRevisions = new List<RevisionInfo>();
            }

            // remove at the same price, if any / удаляем по этой же цене, если такая есть
            RevisionInfo revisionInArray =
                _marketDepthsRevisions.Find(info => info.Security == revision.Security && info.ReplId == revision.ReplId);

            if (revisionInArray != null)
            {
                _marketDepthsRevisions.Remove(revisionInArray);
            }

            // add new / добавляем новую
            _marketDepthsRevisions.Add(revision);


            // create line for depth / создаём строку для стакана

            MarketDepthLevel depthLevel = new MarketDepthLevel();

            int direction = replmsg["dir"].asInt(); // 1 buy 2 sell/1 покупка 2 продажа

            if (direction == 1)
            {
                depthLevel.Bid = replmsg["volume"].asInt();
            }
            else
            {
                depthLevel.Ask = replmsg["volume"].asInt();
            }

            depthLevel.Price = Convert.ToDecimal(replmsg["price"].asDecimal());
            // take our depth/берём наш стакан
            if (_marketDepths == null)
            {
                _marketDepths = new List<MarketDepth>();
            }

            MarketDepth myDepth = _marketDepths.Find(depth => depth.SecurityNameCode == security.Name);

            if (myDepth == null)
            {
                myDepth = new MarketDepth();
                myDepth.SecurityNameCode = security.Name;
                _marketDepths.Add(myDepth);
            }

            // add line in our depth/добавляем строку в наш стакан

            List<MarketDepthLevel> bids = null;

            if (myDepth.Bids != null)
            {
                bids = myDepth.Bids.ToList();
            }

            List<MarketDepthLevel> asks = null;

            if (myDepth.Asks != null)
            {
                asks = myDepth.Asks.ToList();
            }

            if (direction == 1)
            {
                // buy levels/уровни покупок
                if (bids == null || bids.Count == 0)
                {
                    bids = new List<MarketDepthLevel>();
                    bids.Add(depthLevel);
                }
                else
                {
                    bool isInArray = false;
                    for (int i = 0; i < bids.Count; i++)
                    {
                        // proccess the situation when this level is already there/обрабатываем ситуацию когда такой уровень уже есть
                        if (bids[i].Price == depthLevel.Price)
                        {
                            bids[i] = depthLevel;
                            isInArray = true;
                            break;
                        }
                    }

                    if (isInArray == false)
                    {
                        // proccess the situation when this level isn't there/обрабатываем ситуацию когда такого уровня нет
                        List<MarketDepthLevel> asksNew = new List<MarketDepthLevel>();
                        bool isIn = false;

                        for (int i = 0, i2 = 0; i2 < bids.Count + 1; i2++)
                        {
                            if (i == bids.Count && isIn == false ||
                                (isIn == false &&
                                 depthLevel.Price > bids[i].Price))
                            {
                                isIn = true;
                                asksNew.Add(depthLevel);
                            }
                            else
                            {
                                asksNew.Add(bids[i]);
                                i++;
                            }
                        }


                        while (asksNew.Count > 20)
                        {
                            asksNew.Remove(asksNew[asksNew.Count - 1]);
                        }

                        bids = asksNew;

                    }

                    if (asks != null && asks.Count != 0 &&
                        asks[0].Price <= bids[0].Price)
                    {
                        while (asks.Count != 0 &&
                               asks[0].Price <= bids[0].Price)
                        {
                            asks.Remove(asks[0]);
                        }

                        myDepth.Asks = asks;
                    }
                }

                myDepth.Bids = bids;
            }

            if (direction == 2)
            {
                // sell levels/уровни продажи
                if (asks == null || asks.Count == 0)
                {
                    asks = new List<MarketDepthLevel>();
                    asks.Add(depthLevel);
                }
                else
                {
                    bool isInArray = false;
                    for (int i = 0; i < asks.Count; i++)
                    {
                        // proccess the situation when this level is already there/обрабатываем ситуацию когда такой уровень уже есть
                        if (asks[i].Price == depthLevel.Price)
                        {
                            asks[i] = depthLevel;
                            isInArray = true;
                            break;
                        }
                    }
                    if (isInArray == false)
                    {
                        // proccess the situation when this level isn't there/обрабатываем ситуацию когда такого уровня нет
                        List<MarketDepthLevel> bidsNew = new List<MarketDepthLevel>();
                        bool isIn = false;
                        for (int i = 0, i2 = 0; i2 < asks.Count + 1; i2++)
                        {
                            if (i == asks.Count && isIn == false ||
                                (isIn == false &&
                                 depthLevel.Price < asks[i].Price))
                            {
                                isIn = true;
                                bidsNew.Add(depthLevel);
                            }
                            else
                            {
                                bidsNew.Add(asks[i]);
                                i++;
                            }
                        }

                        while (bidsNew.Count > 20)
                        {
                            bidsNew.Remove(bidsNew[bidsNew.Count - 1]);
                        }

                        asks = bidsNew;
                    }

                    if (bids != null && bids.Count != 0 &&
                        asks[0].Price <= bids[0].Price)
                    {
                        while (bids.Count != 0 && asks[0].Price <= bids[0].Price)
                        {
                            bids.Remove(bids[0]);
                        }

                        myDepth.Bids = bids;
                    }
                }

                myDepth.Asks = asks;
            }


            return myDepth;
        }

        public MarketDepth Delete(StreamDataMessage replmsg, Security security)
        {
            // process revision/обрабатываем ревизию

            if (_marketDepthsRevisions == null)
            {
                return null;
            }

            RevisionInfo revision = new RevisionInfo();
            revision.ReplId = replmsg["replID"].asLong();
            revision.Security = security.Name;

            // remove at the same price, if any/удаляем по этой же цене, если такая есть
            RevisionInfo revisionInArray =
                _marketDepthsRevisions.Find(info => info.Security == revision.Security && info.ReplId == revision.ReplId);

            if (revisionInArray != null)
            {
                _marketDepthsRevisions.Remove(revisionInArray);
            }

            MarketDepth myDepth = _marketDepths.Find(depth => depth.SecurityNameCode == security.Name);

            if (myDepth == null)
            {
                myDepth = new MarketDepth();
                myDepth.SecurityNameCode = security.Name;
                _marketDepths.Add(myDepth);
                return myDepth;
            }


            if (revisionInArray == null)
            {
                return myDepth;
            }

            int direction = replmsg["dir"].asInt(); // 1 покупка 2 продажа

            // remove our line from the depth/удаляем нашу строку из стакана

            List<MarketDepthLevel> asks = null;

            if (myDepth.Bids != null)
            {
                asks = myDepth.Bids.ToList();
            }

            if (direction == 1)
            {
                // buy levels/уровни покупок
                if (asks == null || asks.Count == 0)
                {
                    return myDepth;
                }
                else
                {

                    for (int i = 0; i < asks.Count; i++)
                    {
                        // proccess the situation when this level is already there/обрабатываем ситуацию когда такой уровень уже есть
                        if (asks[i].Price == revisionInArray.Price)
                        {
                            asks.Remove(asks[i]);
                            break;
                        }
                    }
                }

                myDepth.Bids = asks;
            }

            if (direction == 2)
            {
                // sell levels/уровни продажи
                List<MarketDepthLevel> bids = null;

                if (myDepth.Asks != null)
                {
                    bids = myDepth.Asks.ToList();
                }

                if (bids == null || bids.Count == 0)
                {
                    return myDepth;
                }
                else
                {
                    for (int i = 0; i < bids.Count; i++)
                    {
                        // proccess the situation when this level is already there/обрабатываем ситуацию когда такой уровень уже есть
                        if (bids[i].Price == revisionInArray.Price)
                        {
                            bids.Remove(bids[i]);
                            break;
                        }
                    }
                }

                myDepth.Asks = bids;
            }


            return myDepth;
        }

    }

    public class RevisionInfo
    {
        public long TableRevision;

        public decimal Price;

        public string Security;

        public long ReplId;
    }
}
