using OsEngine.Entity;
using OsEngine.Market.Servers.Mexc.Json;
using System;

namespace OsEngine.Market.Servers.CoinEx.Spot.Entity
{
    /*
        https://docs.coinex.com/api/v2/spot/market/http/list-market-kline#http-request
    */
    struct CexCandle
    {
        // Market name [ETHUSDT]
        public string market { get; set; }

        // Opening price
        public string open { get; set; }

        // Closing price
        public string close { get; set; }

        // Highest price
        public string high { get; set; }

        // Lowest price
        public string low { get; set; }

        // Filled volume
        public string volume { get; set; }

        // Filled value
        public string value { get; set; }

        // Timestamp (millisecond)
        public long created_at { get; set; }

        public static implicit operator Candle(CexCandle cexCandle)
        {
            Candle candle = new Candle();
            candle.Open = cexCandle.open.ToString().ToDecimal();
            candle.High = cexCandle.high.ToString().ToDecimal();
            candle.Low = cexCandle.low.ToString().ToDecimal();
            candle.Close = cexCandle.close.ToString().ToDecimal();
            candle.Volume = cexCandle.volume.ToString().ToDecimal();
            //candle.TimeStart = CoinExServerRealization.ConvertToDateTimeFromUnixFromMilliseconds(cexCandle.created_at);
            candle.TimeStart = new DateTime(1970, 1, 1).AddMilliseconds(cexCandle.created_at);

            //fix candle
            if (candle.Open < candle.Low)
                candle.Open = candle.Low;
            if (candle.Open > candle.High)
                candle.Open = candle.High;

            if (candle.Close < candle.Low)
                candle.Close = candle.Low;
            if (candle.Close > candle.High)
                candle.Close = candle.High;

            return candle;
        }
    }
}
