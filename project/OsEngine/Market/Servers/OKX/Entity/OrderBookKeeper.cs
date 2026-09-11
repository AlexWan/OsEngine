using System;
using System.Collections.Generic;
using System.Globalization;
using OsEngine.Entity;

namespace OsEngine.Market.Servers.OKX.Entity
{
    /// <summary>
    /// Incremental order book state for one instrument (OKX "books" channel, up to 400 levels).
    /// Bids are kept best first (descending), asks ascending.
    /// Not thread-safe: one market depth reader thread owns the keeper per instrument class.
    /// </summary>
    public class OrderBookKeeper
    {
        public bool HasSnapshot { get; private set; }

        public long SeqId { get; private set; }

        private readonly SortedDictionary<decimal, decimal> _bids =
            new SortedDictionary<decimal, decimal>(Comparer<decimal>.Create((a, b) => b.CompareTo(a)));

        private readonly SortedDictionary<decimal, decimal> _asks =
            new SortedDictionary<decimal, decimal>();

        public void ApplySnapshot(List<List<string>> bids, List<List<string>> asks, long seqId)
        {
            _bids.Clear();
            _asks.Clear();
            ApplyRows(_bids, bids);
            ApplyRows(_asks, asks);
            SeqId = seqId;
            HasSnapshot = true;
        }

        // false = seqId gap, the local book is inconsistent, the caller must resubscribe
        public bool ApplyUpdate(List<List<string>> bids, List<List<string>> asks, long seqId, long prevSeqId)
        {
            if (!HasSnapshot
                || prevSeqId != SeqId)
            {
                return false;
            }

            ApplyRows(_bids, bids);
            ApplyRows(_asks, asks);
            SeqId = seqId;
            return true;
        }

        public void Reset()
        {
            _bids.Clear();
            _asks.Clear();
            HasSnapshot = false;
            SeqId = 0;
        }

        private static void ApplyRows(SortedDictionary<decimal, decimal> book, List<List<string>> rows)
        {
            if (rows == null)
            {
                return;
            }

            for (int i = 0; i < rows.Count; i++)
            {
                List<string> row = rows[i];

                if (row == null
                    || row.Count < 2)
                {
                    continue;
                }

                decimal price = decimal.Parse(row[0], CultureInfo.InvariantCulture);
                decimal size = decimal.Parse(row[1], CultureInfo.InvariantCulture);

                if (size == 0m)
                {
                    book.Remove(price);
                }
                else
                {
                    book[price] = size;
                }
            }
        }

        // copies top levels (best first) into the given lists, up to maxLevels
        public void CopyTopLevels(List<MarketDepthLevel> bidsOut, List<MarketDepthLevel> asksOut, int maxLevels)
        {
            int i = 0;

            foreach (KeyValuePair<decimal, decimal> level in _bids)
            {
                if (i >= maxLevels)
                {
                    break;
                }

                bidsOut.Add(new MarketDepthLevel { Price = (double)level.Key, Bid = (double)level.Value });
                i++;
            }

            i = 0;

            foreach (KeyValuePair<decimal, decimal> level in _asks)
            {
                if (i >= maxLevels)
                {
                    break;
                }

                asksOut.Add(new MarketDepthLevel { Price = (double)level.Key, Ask = (double)level.Value });
                i++;
            }
        }
    }
}
