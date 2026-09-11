using System;
using System.Collections.Generic;
using OsEngine.Entity.WebSocketOsEngine;

namespace OsEngine.Market.Servers.OKX.Entity
{
    /// <summary>
    /// A websocket plus its subscription state.
    /// The remembered args let a dead socket reconnect and resubscribe on its own
    /// instead of restarting the whole connector (TInvest pattern).
    /// </summary>
    public class OkxSocketWrapper
    {
        public WebSocket Socket { get; set; }

        // all subscribe args sent on this socket; one batched frame is rebuilt from them on reconnect
        public List<Dictionary<string, string>> Subscriptions { get; } = new List<Dictionary<string, string>>();

        public DateTime LastReconnectTime { get; set; } = DateTime.MinValue;

        public int ReconnectAttempts { get; set; }
    }
}
