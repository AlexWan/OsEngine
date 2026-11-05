/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
 */

using OsEngine.Entity;
using System;
using System.Collections.Generic;

namespace OsEngine.Market.Servers.FinamGrpc.Entity
{
    public enum FinamOrderBookAction
    {
        Unspecified,
        Remove,
        Add,
        Update
    }

    public sealed class FinamOrderBookRow
    {
        public decimal Price { get; set; }

        public decimal Size { get; set; }

        public bool IsBid { get; set; }

        public FinamOrderBookAction Action { get; set; }

        public string Mpid { get; set; }
    }

    public sealed class FinamOrderBook
    {
        private readonly object _locker = new object();

        private readonly SortedDictionary<decimal, Dictionary<string, decimal>> _bids =
            new SortedDictionary<decimal, Dictionary<string, decimal>>(Comparer<decimal>.Create((first, second) => second.CompareTo(first)));

        private readonly SortedDictionary<decimal, Dictionary<string, decimal>> _asks =
            new SortedDictionary<decimal, Dictionary<string, decimal>>();

        public bool HasSnapshot
        {
            get
            {
                lock (_locker)
                {
                    return _hasSnapshot;
                }
            }
        }

        private bool _hasSnapshot;

        public bool ApplySnapshot(IEnumerable<FinamOrderBookRow> rows)
        {
            lock (_locker)
            {
                Clear();

                if (rows != null)
                {
                    foreach (FinamOrderBookRow row in rows)
                    {
                        if (ApplyRow(row, true) == false)
                        {
                            Clear();
                            return false;
                        }
                    }
                }

                _hasSnapshot = true;
                return true;
            }
        }

        public bool ApplyDelta(IEnumerable<FinamOrderBookRow> rows)
        {
            lock (_locker)
            {
                if (_hasSnapshot == false)
                {
                    return false;
                }

                if (rows != null)
                {
                    foreach (FinamOrderBookRow row in rows)
                    {
                        if (ApplyRow(row, false) == false)
                        {
                            Clear();
                            return false;
                        }
                    }
                }

                return true;
            }
        }

        public void Reset()
        {
            lock (_locker)
            {
                Clear();
            }
        }

        public MarketDepth CreateDepth(string securityName, DateTime time, decimal volumeDivisor, bool fullDepth)
        {
            lock (_locker)
            {
                MarketDepth depth = new MarketDepth();
                depth.SecurityNameCode = securityName;
                depth.Time = time;

                if (volumeDivisor <= 0)
                {
                    volumeDivisor = 1;
                }

                int levels = fullDepth ? int.MaxValue : 1;
                AddLevels(depth.Bids, _bids, true, levels, volumeDivisor);
                AddLevels(depth.Asks, _asks, false, levels, volumeDivisor);

                return depth;
            }
        }

        private bool ApplyRow(FinamOrderBookRow row, bool snapshot)
        {
            if (row == null || row.Size < 0)
            {
                return false;
            }

            if (snapshot && row.Action == FinamOrderBookAction.Remove)
            {
                return false;
            }

            if (snapshot == false && row.Action == FinamOrderBookAction.Unspecified)
            {
                return false;
            }

            SortedDictionary<decimal, Dictionary<string, decimal>> book = row.IsBid ? _bids : _asks;
            Dictionary<string, decimal> participants;

            if (book.TryGetValue(row.Price, out participants) == false)
            {
                participants = new Dictionary<string, decimal>(StringComparer.Ordinal);
                book.Add(row.Price, participants);
            }

            string mpid = row.Mpid ?? "";

            if (row.Action == FinamOrderBookAction.Remove || row.Size == 0)
            {
                participants.Remove(mpid);

                if (participants.Count == 0)
                {
                    book.Remove(row.Price);
                }

                return true;
            }

            participants[mpid] = row.Size;
            return true;
        }

        private void Clear()
        {
            _bids.Clear();
            _asks.Clear();
            _hasSnapshot = false;
        }

        private static void AddLevels(
            List<MarketDepthLevel> target,
            SortedDictionary<decimal, Dictionary<string, decimal>> source,
            bool bids,
            int maxLevels,
            decimal volumeDivisor)
        {
            int count = 0;

            foreach (KeyValuePair<decimal, Dictionary<string, decimal>> row in source)
            {
                if (count >= maxLevels)
                {
                    break;
                }

                decimal size = 0;

                foreach (KeyValuePair<string, decimal> participant in row.Value)
                {
                    size += participant.Value;
                }

                MarketDepthLevel level = new MarketDepthLevel();
                level.Price = (double)row.Key;

                if (bids)
                {
                    level.Bid = (double)(size / volumeDivisor);
                }
                else
                {
                    level.Ask = (double)(size / volumeDivisor);
                }

                target.Add(level);
                count++;
            }
        }
    }
}
