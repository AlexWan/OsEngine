using System.Collections.Generic;
using System.Drawing;
using OsEngine.Entity;

namespace OsEngine.Indicators
{
    [Indicator("NRTR")]
    public class NRTR : Aindicator
    {
        private IndicatorParameterInt _period;

        private IndicatorParameterDecimal _deviation;

        private IndicatorDataSeries _seriesUp;

        private IndicatorDataSeries _seriesDown;

        private IndicatorDataSeries _seriesCenter;

        private int _direction;

        public override void OnStateChange(IndicatorState state)
        {
            _period = CreateParameterInt("Length", 15);
            _deviation = CreateParameterDecimal("Deviation %", 1.0m);

            _seriesUp = CreateSeries("Up line", Color.Aqua, IndicatorChartPaintType.Line, false);
            _seriesDown = CreateSeries("Down line", Color.BlueViolet, IndicatorChartPaintType.Line, false);
            _seriesCenter = CreateSeries("Center Line ", Color.Red, IndicatorChartPaintType.Line, true);
        }

        public override void OnProcess(List<Candle> candles, int index)
        {
            if (index < _period.ValueInt)
            {
                return;
            }

            decimal closePrice = candles[index].Close;
            decimal closePricePrew = candles[index - 1].Close;

            decimal maxHigh = 0;

            if (index - _period.ValueInt > 0)
            {
                for (int i = index; i > -1 && i > index - _period.ValueInt; i--)
                {
                    if (maxHigh < candles[i].High)
                    {
                        maxHigh = candles[i].High;
                    }
                }
            }

            decimal minLow = 0;

            if (index - _period.ValueInt > 0)
            {
                minLow = decimal.MaxValue;

                for (int i = index; i > -1 && i > index - _period.ValueInt; i--)
                {
                    if (minLow > candles[i].Low)
                    {
                        minLow = candles[i].Low;
                    }
                }
            }

            _seriesUp.Values[index] = maxHigh * (1 - _deviation.ValueDecimal / 100);
            _seriesDown.Values[index] = minLow * (1 + _deviation.ValueDecimal / 100);

            _direction = (closePrice > _seriesUp.Values[index - 1]) ? 1 :
                    (closePricePrew < _seriesDown.Values[index - 1]) ? -1 : _direction;

            _seriesCenter.Values[index] = _direction == 1 ? _seriesDown.Values[index] : _seriesUp.Values[index];
        }
    }
}