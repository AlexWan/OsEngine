/*
 *Your rights to use the code are governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 *Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using OsEngine.Entity;
using OsEngine.Entity.WebSocketOsEngine;
using System;
using System.Collections.Generic;

namespace OsEngine.Market.Servers.BCS.Entity
{
    /// <summary>
    /// A market data websocket plus its subscription state.
    /// The remembered securities let a dead socket reconnect and resubscribe
    /// in one batch instead of restarting the whole connector (OKX/TInvest pattern).
    /// </summary>
    public class BcsSocketWrapper
    {
        public WebSocket Socket { get; set; }

        // securities subscribed on this socket; batched subscribe frames are rebuilt from them on reconnect
        public List<Security> Subscriptions { get; } = new List<Security>();

        public DateTime LastReconnectTime { get; set; } = DateTime.MinValue;

        public int ReconnectAttempts { get; set; }

        // when the current socket was created: the OsEngine wrapper keeps ReadyState = Closed
        // until the handshake completes (there is no Connecting state in practice),
        // so the watchdog must give a fresh socket time to open instead of treating it as dead
        public DateTime CreatedTime { get; set; } = DateTime.MinValue;

        // last message on this socket: the canary heartbeat (a liquid benchmark trades
        // subscription per socket) makes a silent socket visible even when ReadyState is Open
        public DateTime LastMessageTime { get; set; } = DateTime.MinValue;

        // the canary trades frame is already sent on the current socket:
        // a user subscription to the same instrument must not duplicate the trades channel
        public bool CanaryActive { get; set; }

        // REST arbiter throttle: the "is the market actually trading now" check
        // runs at most once in 10 minutes per socket
        public DateTime LastArbiterTime { get; set; } = DateTime.MinValue;
    }
}
