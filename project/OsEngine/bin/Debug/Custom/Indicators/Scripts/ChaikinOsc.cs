using System.Collections.Generic;
using System.Drawing;
using OsEngine.Entity;
using OsEngine.Indicators;


namespace CustomIndicators.Scripts
{
    public class ChaikinOsc : Aindicator
    {
        private IndicatorDataSeries _seriesLine;
        private IndicatorDataSeries _seriesVi;
        private IndicatorDataSeries _seriesAccDistr;

        private IndicatorParameterInt _longPeriod;
        private IndicatorParameterInt _shortPeriod;

        public override void OnStateChange(IndicatorState state)
        {
            if (state == IndicatorState.Configure)
            {
                _shortPeriod = CreateParameterInt("Short Period", 3);

                _longPeriod = CreateParameterInt("Long Period", 10);

                _seriesLine = CreateSeries("Chaikin Oscillator", Color.Gold, IndicatorChartPaintType.Line, true);
                _seriesLine.CanReBuildHistoricalValues = false;

                _seriesVi = CreateSeries("Series Vi", Color.AliceBlue, IndicatorChartPaintType.Line, false);
                _seriesVi.CanReBuildHistoricalValues = false;

                _seriesAccDistr = CreateSeries("Series Acc Distr", Color.Red, IndicatorChartPaintType.Line, false);
                _seriesAccDistr.CanReBuildHistoricalValues = false;
            }
        }

        public override void OnProcess(List<Candle> source, int index)
        {
            if (index < _longPeriod.ValueInt ||
                index < _shortPeriod.ValueInt)
            {
                return;
            }

            if (index <= 0)
            {
                return;
            }

            _seriesVi.Values[index] = GetVi(source, index);

            _seriesAccDistr.Values[index] = GetAccDist(_seriesVi.Values, index);

            _seriesLine.Values[index] =
                    GetSma(_seriesAccDistr.Values, _shortPeriod.ValueInt, index)
                  - GetSma(_seriesAccDistr.Values, _longPeriod.ValueInt, index);
        }

        private decimal GetVi(List<Candle> candles, int index)
        {
            decimal high = candles[index].High;
            decimal low = candles[index].Low;

            if (high == low)
            {
                return 0;
            }
            decimal close = candles[index].Close;
            decimal volume = candles[index].Volume;

            decimal result = ((2 * close) - (high + low)) / (high - low) * volume;

            return result;
        }

        private decimal GetAccDist(List<decimal> vi, int index)
        {
            if (index == 0)
            {
                return vi[index];
            }

            return _seriesAccDistr.Values[index - 1] + vi[index];
        }

        private decimal GetSma(List<decimal> values, int lenght, int index)
        {
            decimal result = 0;

            int lenghtReal = 0;

            for (int i = index; i > 0 && i > index - lenght; i--)
            {
                result += values[i];
                lenghtReal++;
            }

            return result / lenghtReal; ;
        }
    }
}