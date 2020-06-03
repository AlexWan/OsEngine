using System;
using System.Collections.Generic;
using OsEngine.Entity;

namespace OsEngine.Indicators
{
    public static class ScriptSpells
    {

        public static decimal Summ(this List<Candle> values, int startIndex, int endIndex, string type)
        {
            decimal result = 0;

            if (endIndex < startIndex)
            {
                int i = endIndex;
                endIndex = startIndex;
                startIndex = i;
            }

            if (startIndex < 0)
            {
                startIndex = 0;
            }

            if (endIndex >= values.Count)
            {
                endIndex = values.Count - 1;
            }

            for (int i = startIndex + 1; i < endIndex + 1; i++)
            {
                result += values[i].GetPoint(type);
            }

            return result;
        }

        public static decimal GetPoint(this Candle candle, string type)
        {
            if (type == "Close")
            {
                return candle.Close;
            }
            else if (type == "High")
            {
                return candle.High;
            }
            else if (type == "Low")
            {
                return candle.Low;
            }
            else if (type == "Open")
            {
                return candle.Open;
            }
            else if (type == "Median")
            {
                return (candle.High + candle.Low) / 2;
            }
            else //if (type == Entity.CandlePointType.Typical)
            {
                return (candle.High + candle.Low + candle.Close) / 3;
            }
        }

        public static List<decimal> ByName(this List<IndicatorDataSeries> values, string name)
        {
            IndicatorDataSeries result = null;

            for (int i = 0; i < values.Count; i++)
            {
                if (values[i].Name == name)
                {
                    return values[i].Values;
                }
            }

            return null;
        }

        public static decimal Highest(this List<Candle> values, int startIndex, int endIndex)
        {
            if (endIndex < startIndex)
            {
                int i = endIndex;
                endIndex = startIndex;
                startIndex = i;
            }

            if (startIndex < 0)
            {
                startIndex = 0;
            }

            if (endIndex >= values.Count)
            {
                endIndex = values.Count - 1;
            }

            if (endIndex == startIndex)
            {
                return 0;
            }

            decimal result = decimal.MinValue;

            for (int i = startIndex + 1; i < endIndex + 1; i++)
            {
                if (values[i].High > result)
                {
                    result = values[i].High;
                }
            }

            return result;
        }

        public static decimal Lowest(this List<Candle> values, int startIndex, int endIndex)
        {
            if (endIndex < startIndex)
            {
                int i = endIndex;
                endIndex = startIndex;
                startIndex = i;
            }

            if (startIndex < 0)
            {
                startIndex = 0;
            }

            if (endIndex >= values.Count)
            {
                endIndex = values.Count - 1;
            }

            if (endIndex == startIndex)
            {
                return 0;
            }

            decimal result = decimal.MaxValue;

            for (int i = startIndex + 1; i < endIndex + 1; i++)
            {
                if (values[i].Low < result)
                {
                    result = values[i].Low;
                }
            }

            return result;
        }
    }
}
