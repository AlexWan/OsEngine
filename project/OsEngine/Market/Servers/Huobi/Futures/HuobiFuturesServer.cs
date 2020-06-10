using OsEngine.Entity;
using OsEngine.Language;
using System;
using System.Collections.Generic;

namespace OsEngine.Market.Servers.Huobi.Futures
{
    public class HuobiFuturesServer : AServer
    {
        public HuobiFuturesServer()
        {
            HuobiFuturesServerRealization realization = new HuobiFuturesServerRealization(ServerType.HuobiFutures,
                "api.hbdm.com",
                "/notification");

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
            return ((HuobiFuturesServerRealization)ServerRealization).GetCandleHistory(nameSec, tf);
        }
    }
}
