using OsEngine.Entity;
using System;
using System.Collections.Generic;
using System.Linq;

namespace OsEngine.Market.Services
{
    public class CandlesCreator
    {
        public static List<Candle> CreateCandlesRequiredInterval(int juniorInterval, int seniorInterval, List<Candle> oldCandles)
        {
            var candles = new List<Candle>();

            int index = oldCandles.FindIndex(can => can.TimeStart.Minute % seniorInterval == 0);

            int count = seniorInterval / juniorInterval;

            int counter = 0;

            Candle newCandle = new Candle();

            for (int i = index; i < oldCandles.Count; i++)
            {
                counter++;

                if (counter == 1)
                {
                    newCandle = new Candle();
                    newCandle.Open = oldCandles[i].Open;
                    newCandle.TimeStart = oldCandles[i].TimeStart;
                    newCandle.Low = Decimal.MaxValue;
                }

                newCandle.High = oldCandles[i].High > newCandle.High
                    ? oldCandles[i].High
                    : newCandle.High;

                newCandle.Low = oldCandles[i].Low < newCandle.Low
                    ? oldCandles[i].Low
                    : newCandle.Low;

                newCandle.Volume += oldCandles[i].Volume;

                if (counter == count)
                {
                    newCandle.Close = oldCandles[i].Close;
                    newCandle.State = CandleState.Finished;
                    candles.Add(newCandle);
                    counter = 0;
                }

                if (i == oldCandles.Count - 1 && counter != count)
                {
                    newCandle.Close = oldCandles[i].Close;
                    newCandle.State = CandleState.None;
                    candles.Add(newCandle);
                }
            }

            return candles;
        }

        /// <summary>
        /// определить подходящий интервал для запроса
        /// </summary>
        /// <returns></returns>
        public static string DetermineAppropriateIntervalForRequest(int requiredInterval, Dictionary<int, string> supportedIntervals, out int key)
        {
            if (supportedIntervals.ContainsKey(requiredInterval))
            {
                key = requiredInterval;
                return supportedIntervals[requiredInterval];
            }

            var needPair = supportedIntervals.Last(pair => pair.Key < requiredInterval && requiredInterval % pair.Key == 0);

            key = needPair.Key;

            return needPair.Value;
        }
    }
}
