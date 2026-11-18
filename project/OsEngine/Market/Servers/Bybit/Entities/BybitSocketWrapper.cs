/*
 *Your rights to use the code are governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 *Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System;
using System.Collections.Generic;
using OsEngine.Entity.WebSocketOsEngine;

namespace OsEngine.Market.Servers.Bybit.Entities
{
    public class BybitSocketWrapper
    {
        public WebSocket Socket;

        public List<string> Subscriptions = new List<string>();

        public DateTime LastReconnectTime = DateTime.MinValue;

        public int ReconnectAttempts;
    }
}
