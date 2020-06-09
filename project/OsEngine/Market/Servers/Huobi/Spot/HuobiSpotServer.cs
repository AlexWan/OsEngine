using OsEngine.Entity;
using OsEngine.Language;
using System;
using System.Collections.Generic;

namespace OsEngine.Market.Servers.Huobi.Spot
{
    public class HuobiSpotServer : AServer
    {
        public HuobiSpotServer()
        {
            HuobiServerRealization realization = new HuobiServerRealization(ServerType.HuobiSpot,
                "api.huobi.pro",
                "/ws/v2");

            ServerRealization = realization;

            CreateParameterString(OsLocalization.Market.ServerParamPublicKey, "");
            CreateParameterPassword(OsLocalization.Market.ServerParamSecretKey, "");
        }

        /// <summary>
        /// instrument history query
        /// запрос истории по инструменту
        /// </summary>
        public List<Candle> GetCandleHistory(string nameSec, TimeSpan tf)
        {
            return ((HuobiServerRealization)ServerRealization).GetCandleHistory(nameSec, tf);
        }
    }
}
