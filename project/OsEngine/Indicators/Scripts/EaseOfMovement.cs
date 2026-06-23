using System;
using System.Collections.Generic;
using System.Drawing;
using OsEngine.Entity;
using OsEngine.Language;

namespace OsEngine.Indicators
{
    [Indicator("EaseOfMovement")]
    public class EaseOfMovement : Aindicator
    {
        public override string Description
        {
            get
            {
                string eng = "EaseOfMovement calculates the ratio of average daily price movement to trading volume, showing how easily price advances at the current market activity. " +
                             "Traders use EOM to assess trend strength and find divergences: rising price on falling EOM often signals weak buyers and a possible reversal.";

                string ru = "EaseOfMovement рассчитывает отношение среднего дневного движения цены к объёму торгов, показывая, насколько легко цена продвигается при текущей активности рынка. " +
                            "Трейдеры используют EOM для оценки силы тренда и поиска дивергенций: рост цены на падающем EOM часто сигнализирует о слабости быков и возможном развороте.";

                return OsLocalization.ConvertToLocString($"Eng:{eng}_Ru:{ru}_");
            }
        }

        private IndicatorDataSeries _seriesLine;

        private IndicatorDataSeries _seriesEomRaw;

        private IndicatorParameterInt _period;

        public override void OnStateChange(IndicatorState state)
        {
            if (state == IndicatorState.Configure)
            {
                _period = CreateParameterInt("Period", 14);

                _seriesLine = CreateSeries("EaseOfMovement", Color.Aqua, IndicatorChartPaintType.Line, true);
                _seriesLine.CanReBuildHistoricalValues = false;

                _seriesEomRaw = CreateSeries("Eom Raw", Color.Red, IndicatorChartPaintType.Line, false);
            }
        }

        public override void OnProcess(List<Candle> candles, int index)
        {
            _seriesLine.Values[index] = GetValue(candles, index);
        }

        private decimal GetValue(List<Candle> candles, int index)
        {
            if (index == 0)
            {
                return 0;
            }
            if (index - _period.ValueInt <= 0)
            {
                return 0;
            }

            decimal mm = GetMm(candles, index);
            decimal box = GetBoxRatio(candles, index);

            if (box == 0)
            {
                return 0;
            }
            else
            {
                _seriesEomRaw.Values[index] = mm / box;
            }

            decimal result = GetSma(_seriesEomRaw.Values, _period.ValueInt, index);

            return result;
        }

        private decimal GetMm(List<Candle> candles, int index)
        {
            decimal high = candles[index].High;
            decimal low = candles[index].Low;

            decimal highMinOne = candles[index - 1].High;
            decimal lowMinOne = candles[index - 1].Low;

            return (high + low) / 2 - (highMinOne + lowMinOne) / 2;
        }

        private decimal GetBoxRatio(List<Candle> candles, int index)
        {
            decimal vol = candles[index].Volume;

            decimal high = candles[index].High;
            decimal low = candles[index].Low;

            if (high - low == 0)
            {
                return 0;
            }

            decimal result = Math.Round((vol / 10000) / (high - low), 5);

            return result;
        }

        private decimal GetSma(List<decimal> values, int length, int index)
        {
            decimal result = 0;

            int lengthReal = 0;

            for (int i = index; i > 0 && i > index - length; i--)
            {
                result += values[i];
                lengthReal++;
            }

            return result / lengthReal; ;
        }
    }
}