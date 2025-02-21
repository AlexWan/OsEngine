using OsEngine.Entity;
using System.Collections.Generic;

namespace OsEngine.Market.Servers.CoinEx.Spot.Entity
{
    /*
        https://docs.coinex.com/api/v2/spot/market/http/list-market-kline#http-request
    */
    struct CexCandleExtra
    {
        // Market name [ETHUSDT]
        public string market { get; set; }

        // Candle data
        public List<object> data { get; set; }

        public static implicit operator CexCandle(CexCandleExtra cexCandleExtra)
        {
            CexCandle candle = new CexCandle();
            candle.market = cexCandleExtra.market;

            List<object> data = cexCandleExtra.data;

            candle.created_at = 1000 * (long)data[0];
            candle.open = data[1].ToString();
            candle.close = data[2].ToString();
            candle.high = data[3].ToString();
            candle.low = data[4].ToString();
            candle.volume = data[5].ToString();
            candle.value = data[6].ToString();
            return candle;
        }

        public static implicit operator Candle(CexCandleExtra cexCandleExtra)
        {
            return (CexCandle)cexCandleExtra;
        }
    }
}
