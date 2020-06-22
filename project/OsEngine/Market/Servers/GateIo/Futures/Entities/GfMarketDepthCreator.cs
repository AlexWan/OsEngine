using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using OsEngine.Entity;
using OsEngine.Market.Servers.GateIo.Futures.Response;
using OsEngine.Market.Services;

namespace OsEngine.Market.Servers.GateIo.Futures.Entities
{
    class GfMarketDepthCreator : BaseMarketDepthUpdater
    {
        private List<MarketDepth> _allDepths = new List<MarketDepth>();

        public List<MarketDepth> Create(string data)
        {
            if (data.Contains("all"))
            {
                var jt = JsonConvert.DeserializeObject<GfSubscribeDepthResponseAll>(data);
                return CreateNew(jt);
            }

            var jn = JsonConvert.DeserializeObject<GfSubscribeDepthResponseUpdate>(data);

            return Update(jn);
        }

        private List<MarketDepth> CreateNew(GfSubscribeDepthResponseAll jt)
        {
            List<MarketDepth> returnedDepth = new List<MarketDepth>();

            var newDepth = new MarketDepth();

            newDepth.Time = DateTime.UtcNow;

            newDepth.SecurityNameCode = jt.Result.Contract;

            var needDepth = _allDepths.Find(d => d.SecurityNameCode == newDepth.SecurityNameCode);

            if (needDepth != null)
            {
                _allDepths.Remove(needDepth);
            }

            var bids = jt.Result.Bids;
            var asks = jt.Result.Asks;

            foreach (var bid in bids)
            {
                newDepth.Bids.Add(new MarketDepthLevel()
                {
                    Price = Converter.StringToDecimal(bid.P),
                    Bid = bid.S
                });
            }

            foreach (var ask in asks)
            {
                newDepth.Asks.Add(new MarketDepthLevel()
                {
                    Price = Converter.StringToDecimal(ask.P),
                    Ask = ask.S
                });
            }

            _allDepths.Add(newDepth);
            SortBids(newDepth.Bids);
            SortAsks(newDepth.Asks);

            returnedDepth.Add(newDepth.GetCopy());

            return returnedDepth;
        }

        private List<MarketDepth> Update(GfSubscribeDepthResponseUpdate quotes)
        {
            List<MarketDepth> returnedDepth = new List<MarketDepth>();

            foreach (var quote in quotes.Result)
            {
                var needDepth = _allDepths.Find(d => d.SecurityNameCode == quote.C);

                if (needDepth == null)
                {
                    continue;
                }

                if (quote.S >= 0)
                {
                    decimal price = Converter.StringToDecimal(quote.P);
                    decimal bid = Math.Abs(quote.S);

                    if (bid != 0)
                    {
                        InsertLevel(price, bid, Side.Buy, needDepth);
                    }
                    else
                    {
                        DeleteLevel(price, Side.Buy, needDepth);
                    }

                    SortBids(needDepth.Bids);
                }

                if (quote.S <= 0)
                {
                    decimal price = Converter.StringToDecimal(quote.P);
                    decimal ask = Math.Abs(quote.S);

                    if (ask != 0)
                    {
                        InsertLevel(price, ask, Side.Sell, needDepth);
                    }
                    else
                    {
                        DeleteLevel(price, Side.Sell, needDepth);
                    }

                    SortAsks(needDepth.Asks);
                }

                returnedDepth.Add( needDepth.GetCopy());
            }

            return returnedDepth;
        }
    }
}
