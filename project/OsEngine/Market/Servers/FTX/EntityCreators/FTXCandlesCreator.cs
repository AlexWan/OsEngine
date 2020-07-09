using Newtonsoft.Json.Linq;
using OsEngine.Entity;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OsEngine.Market.Servers.FTX.EntityCreators
{
    public class FTXCandlesCreator
    {
        private const string SearchPath = "result";
        private const string PathForStartTime = "startTime";
        private const string PathForOpen = "open";
        private const string PathForClose = "close";
        private const string PathForLow = "low";
        private const string PathForHigh = "high";
        private const string PathForVolume = "volume";

        public List<Candle> Create(JToken jt)
        {
            var candles = new List<Candle>();

            var JProperties = jt.SelectTokens(SearchPath).Children();

            foreach(var jProperty in JProperties)
            {
                Candle candle = new Candle();

                candle.TimeStart = jProperty.SelectToken(PathForStartTime).Value<DateTime>();
                candle.Open = jProperty.SelectToken(PathForOpen).Value<decimal>();
                candle.Close = jProperty.SelectToken(PathForClose).Value<decimal>();
                candle.Low = jProperty.SelectToken(PathForLow).Value<decimal>();
                candle.High = jProperty.SelectToken(PathForHigh).Value<decimal>();
                candle.Volume = jProperty.SelectToken(PathForVolume).Value<decimal>();

                candles.Add(candle);
            }

            return candles;
        }
    }
}
