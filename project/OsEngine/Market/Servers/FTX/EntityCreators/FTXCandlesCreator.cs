using Newtonsoft.Json.Linq;
using OsEngine.Entity;
using System;
using System.Collections.Generic;

namespace OsEngine.Market.Servers.FTX.EntityCreators
{
    public class FTXCandlesCreator
    {
        private const string StartTimePath = "startTime";
        private const string OpenPath = "open";
        private const string ClosePath = "close";
        private const string LowPath = "low";
        private const string HighPath = "high";
        private const string VolumePath = "volume";

        public List<Candle> Create(JToken data)
        {
            var candles = new List<Candle>();

            var JProperties = data.Children();

            foreach(var jProperty in JProperties)
            {
                Candle candle = new Candle();

                candle.TimeStart = jProperty.SelectToken(StartTimePath).Value<DateTime>();
                candle.Open = jProperty.SelectToken(OpenPath).Value<decimal>();
                candle.Close = jProperty.SelectToken(ClosePath).Value<decimal>();
                candle.Low = jProperty.SelectToken(LowPath).Value<decimal>();
                candle.High = jProperty.SelectToken(HighPath).Value<decimal>();
                candle.Volume = jProperty.SelectToken(VolumePath).Value<decimal>();

                candles.Add(candle);
            }

            return candles;
        }
    }
}
