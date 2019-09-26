using System.Collections.Generic;
using OsEngine.Entity;

namespace OsEngine.Market.Services
{
    class BaseMarketDepthUpdater
    {
        protected void InsertLevel(decimal price, decimal value, Side side, MarketDepth marketDepth)
        {
            var needDepthPart = side == Side.Buy ? marketDepth.Bids : marketDepth.Asks;

            var needLevel = needDepthPart.Find(level => level.Price == price);

            if (needLevel != null)
            {
                if (side == Side.Buy)
                {
                    needLevel.Bid = value;
                }
                else
                {
                    needLevel.Ask = value;
                }
            }
            else
            {
                needLevel = new MarketDepthLevel();
                needLevel.Price = price;

                if (side == Side.Buy)
                {
                    needLevel.Bid = value;
                }
                else
                {
                    needLevel.Ask = value;
                }

                needDepthPart.Add(needLevel);
                SortBids(needDepthPart);
            }
        }

        protected void DeleteLevel(decimal price, Side side, MarketDepth marketDepth)
        {
            var needDepthPart = side == Side.Buy ? marketDepth.Bids : marketDepth.Asks;

            var needLevel = needDepthPart.Find(level => level.Price == price);

            needDepthPart.Remove(needLevel);
        }

        protected void SortBids(List<MarketDepthLevel> levels)
        {
            levels.Sort((a, b) =>
            {
                if (a.Price > b.Price)
                {
                    return -1;
                }
                else if (a.Price < b.Price)
                {
                    return 1;
                }
                else
                {
                    return 0;
                }
            });
        }

        protected void SortAsks(List<MarketDepthLevel> levels)
        {
            levels.Sort((a, b) =>
            {
                if (a.Price > b.Price)
                {
                    return 1;
                }
                else if (a.Price < b.Price)
                {
                    return -1;
                }
                else
                {
                    return 0;
                }
            });
        }
    }
}
