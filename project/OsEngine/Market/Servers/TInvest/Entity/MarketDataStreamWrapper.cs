/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using Grpc.Core;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Tinkoff.InvestApi.V1;

namespace OsEngine.Market.Servers.TInvest.Entity
{
    public class MarketDataStreamWrapper
    {
        public AsyncDuplexStreamingCall<MarketDataRequest, MarketDataResponse> StreamClient { get; set; }
        public List<MarketDataRequest> Subscriptions { get; set; } = new List<MarketDataRequest>();
        public bool IsConnected { get; set; }
        public DateTime LastMessageTime { get; set; }
        public string Name { get; set; } // For logging purposes
        public Task ReadingTask { get; set; }
    }
}
