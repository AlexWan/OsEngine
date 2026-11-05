/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
 */

using OsEngine.Entity;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace OsEngine.Market.Servers.FinamGrpc.Entity
{
    internal enum FinamStreamKind
    {
        JwtRenewal,
        Account,
        Orders,
        Trades,
        LatestTrades,
        OrderBook,
        Quote
    }

    internal sealed class FinamStreamLease
    {
        public FinamStreamLease(FinamStreamKind kind, string key, long generation, CancellationTokenSource cancellation)
        {
            Kind = kind;
            Key = key;
            Generation = generation;
            Cancellation = cancellation;
            LastMessageUtc = DateTime.UtcNow;
        }

        public FinamStreamKind Kind { get; }

        public string Key { get; }

        public long Generation { get; }

        public CancellationTokenSource Cancellation { get; }

        public IDisposable Call { get; set; }

        public Task Reader { get; set; }

        public Security Security { get; set; }

        public string AccountId { get; set; }

        public bool RestartRequested { get; set; }

        public int RestartAttempts { get; set; }

        public DateTime LastMessageUtc { get; set; }

        public DateTime FirstRestartFailureUtc { get; set; }
    }
}
