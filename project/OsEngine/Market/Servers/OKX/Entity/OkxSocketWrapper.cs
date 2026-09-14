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

    /// <summary>
    /// A public socket message plus the socket it arrived on:
    /// notices (code 64008) are routed back to the exact socket for a proactive reconnect
    /// </summary>
    public class OkxPublicSocketMessage
    {
        public OkxPublicSocketMessage(WebSocket socket, string message)
        {
            Socket = socket;
            Message = message;
        }

        public WebSocket Socket { get; }

        public string Message { get; }
    }
}
