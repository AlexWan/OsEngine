/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using Grpc.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Tinkoff.InvestApi.V1;

namespace OsEngine.Market.Servers.TInvest.Entity
{
    // Вид подписки, отслеживаемый для проверки ack (BUG-0130 R1).
    public enum MarketDataSubType
    {
        Trades,
        OrderBook,
        Candles,
        LastPrice
    }

    // Одно решение state-машины ack за вызов: гарантия «ровно одна Error-строка на эскалацию, не на запись».
    public enum MarketDataAckAction
    {
        None,
        LogError,
        Resubscribe,
        Escalate
    }

    public class MarketDataStreamWrapper
    {
        public AsyncDuplexStreamingCall<MarketDataRequest, MarketDataResponse> StreamClient { get; set; }
        public List<MarketDataRequest> Subscriptions { get; set; } = new List<MarketDataRequest>();
        public bool IsConnected { get; set; }
        public DateTime LastMessageTime { get; set; }
        public string Name { get; set; } // For logging purposes
        public Task ReadingTask { get; set; }

        // время запроса GetMySubscriptions для реконсиляции подписок (минимальное значение - не запрошено)
        public DateTime ReconcileRequestTime { get; set; }

        // FIX-0472 (R5/R7.1): состояние блока стрима по бюджету брокера (80001).
        // Ставит читатель при ResourceExhausted, читает ватчдог перед respawn
        public DateTime StreamBlockedUntilUtc { get; set; }
        public int StreamBlockCount { get; set; }
        public Exception LastReconnectError { get; set; }
        #region Subscriptions ack verification (BUG-0130)

        public static readonly TimeSpan MARKET_DATA_ACK_TIMEOUT = TimeSpan.FromSeconds(60);
        public static readonly TimeSpan RESUB_BACKOFF = TimeSpan.FromMinutes(2);

        public volatile bool NeedEscalation;      // читатель/state-машина выставляют, ватчдог исполняет (R3)
        public DateTime NextResubDeadlineUtc;     // до этого момента ватчдог не ретраит и не эскалирует (R2)

        // Pending per (instrumentUid × вид): дедлайн UTC от своей WriteAsync (R1).
        private readonly Dictionary<(string Uid, MarketDataSubType Type), DateTime> _ackPending =
            new Dictionary<(string Uid, MarketDataSubType Type), DateTime>();
        private readonly object _ackLock = new object(); // микро-лок: без I/O, без вложенности в другие локи (R6)
        private bool _ackLogged;   // первый промах уже зафиксирован — ровно один Error на эскалацию
        private bool _ackRetried;  // ретрай батча уже выдавался: следующий промах эскалирует

        public int PendingAckCount
        {
            get { lock (_ackLock) { return _ackPending.Count; } }
        }

        public DateTime? GetAckDeadline(string instrumentUid, MarketDataSubType type)
        {
            lock (_ackLock)
            {
                if (_ackPending.TryGetValue((instrumentUid, type), out var deadline))
                {
                    return deadline;
                }
                return null;
            }
        }

        // Вызывается сразу после успешного WriteAsync СВОЕЙ записи подписки (R1). Счётчики цикла не сбрасывает.
        public void RegisterAckPending(string instrumentUid, MarketDataSubType type, DateTime nowUtc)
        {
            lock (_ackLock)
            {
                _ackPending[(instrumentUid, type)] = nowUtc + MARKET_DATA_ACK_TIMEOUT;
            }
        }

        // SUCCESS-ack из потока читателя (R3). Идемпотентно: дубль без Error (R5). Полный drain сбрасывает цикл.
        public bool RemoveAckPending(string instrumentUid, MarketDataSubType type)
        {
            lock (_ackLock)
            {
                bool removed = _ackPending.Remove((instrumentUid, type));
                // REWORK P1-2: drain-reset ТОЛЬКО при реальном снятии и опустении — поздний или
                // дублирующий Success-ack не должен стирать вооружённый 80001-бэк-офф
                if (removed && _ackPending.Count == 0)
                {
                    _ackLogged = false;
                    _ackRetried = false;
                    NextResubDeadlineUtc = DateTime.MinValue;
                }
                return removed;
            }
        }

        // Rollback SubscribeLoop (R4): снять ВСЕ записи инструмента; возвращает число снятых.
        public int RemoveAckPendingForInstrument(string instrumentUid)
        {
            lock (_ackLock)
            {
                var keys = _ackPending.Keys.Where(k => k.Uid == instrumentUid).ToList();
                foreach (var key in keys)
                {
                    _ackPending.Remove(key);
                }
                // REWORK P1-3: drain-reset только после реального снятия (R4): rollback чужого
                // инструмента не обязан обнулять живой цикл эскалации
                if (keys.Count > 0 && _ackPending.Count == 0)
                {
                    _ackLogged = false;
                    _ackRetried = false;
                    NextResubDeadlineUtc = DateTime.MinValue;
                }
                return keys.Count;
            }
        }

        // У успехе TryReconnectDataStream: цикл ack начинается заново (REWORK P0-1: без сброса флага
        // R3-форс недостижим на следующем цикле). pending не трогаем — батч переза сеет записи сам.
        public void ResetAckState()
        {
            lock (_ackLock)
            {
                NeedEscalation = false;
                _ackLogged = false;
                _ackRetried = false;
                NextResubDeadlineUtc = DateTime.MinValue;
            }
        }

        // Вооружение бэк-оффа извне обёртки (кэтч TryReconnectDataStream, сайт A при 80001) — под локом (R6).
        public void ArmResubDeadline(DateTime nowUtc)
        {
            lock (_ackLock)
            {
                NextResubDeadlineUtc = nowUtc + RESUB_BACKOFF;
            }
        }

        // R3-гейт ватчдога под локом (R6): дедлайн не вооружён или истёк — эскалацию пропускаем.
        public bool IsBackoffOpen(DateTime nowUtc)
        {
            lock (_ackLock)
            {
                return NextResubDeadlineUtc == DateTime.MinValue || nowUtc >= NextResubDeadlineUtc;
            }
        }

        // Вызов из ватчдога ConnectionCheckThread раз за итерацию на каждой обёртке.
        // Исполняет решение ватчдог, не читатель (R3): LogError → SendLogMessage Error;
        // Resubscribe → TryReconnectDataStream; Escalate → форс-дисконнект участка ConnectionCheckThread.
        public MarketDataAckAction CheckAckDeadlines(DateTime nowUtc)
        {
            if (NeedEscalation)
            {
                return MarketDataAckAction.Escalate;
            }

            bool hasExpired;
            lock (_ackLock)
            {
                hasExpired = _ackPending.Values.Any(d => d < nowUtc);
            }

            if (!hasExpired)
            {
                return MarketDataAckAction.None;
            }

            lock (_ackLock) // REWORK P2-4 (R6): счётчики цикла и дедлайн — только под микро-локом
            {
                if (!_ackLogged)
                {
                    _ackLogged = true;
                    NextResubDeadlineUtc = nowUtc + RESUB_BACKOFF;
                    return MarketDataAckAction.LogError;
                }

                if (!_ackRetried && nowUtc >= NextResubDeadlineUtc)
                {
                    _ackRetried = true;
                    return MarketDataAckAction.Resubscribe;
                }

                if (_ackRetried && nowUtc >= NextResubDeadlineUtc)
                {
                    NeedEscalation = true;
                    return MarketDataAckAction.Escalate;
                }

                return MarketDataAckAction.None;
            }
        }

        // Вызов из потока читателя на каждый FAIL subscription_result (R5). Вторых Error-строк не создаёт —
        // штатная строка «Failed to subscribe ... Status:» уже написана читателем.
        public MarketDataAckAction OnSubscriptionAckFailed(bool fatal, DateTime nowUtc)
        {
            if (fatal)
            {
                NeedEscalation = true;
                return MarketDataAckAction.Escalate;
            }

            lock (_ackLock) // REWORK P2-4 (R6)
            {
                if (!_ackLogged)
                {
                    _ackLogged = true;
                    NextResubDeadlineUtc = nowUtc + RESUB_BACKOFF;
                }
            }

            return MarketDataAckAction.None;
        }

        #endregion
    }
}
