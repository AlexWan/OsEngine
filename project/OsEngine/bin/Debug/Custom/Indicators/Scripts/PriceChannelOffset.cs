using System.Collections.Generic;
using System.Drawing;
using OsEngine.Entity;

namespace OsEngine.Indicators
{
    [Indicator("PriceChannelOffset")]
    public class PriceChannelOffset : Aindicator
    {
        private IndicatorParameterInt _length;

        private IndicatorParameterDecimal _offset;

        private IndicatorDataSeries _seriesUpBorder;

        private IndicatorDataSeries _seriesDownBorder;

        private IndicatorDataSeries _seriesUp;

        private IndicatorDataSeries _seriesDown;

        public override void OnStateChange(IndicatorState state)
        {
            if (state == IndicatorState.Configure)
            {
                _length = CreateParameterInt("Length", 50);
                _offset = CreateParameterDecimal("Offset percent", 30);

                _seriesUp = CreateSeries("Up line", Color.Aqua, IndicatorChartPaintType.Line, true);
                _seriesDown = CreateSeries("Down line", Color.BlueViolet, IndicatorChartPaintType.Line, true);

                _seriesUpBorder = CreateSeries("Up line border", Color.WhiteSmoke, IndicatorChartPaintType.Line, true);
                _seriesDownBorder = CreateSeries("Down line border", Color.WhiteSmoke, IndicatorChartPaintType.Line, true);
            }
        }

        public override void OnProcess(List<Candle> candles, int index)
        {
            decimal upLine = 0;

            if (index - _length.ValueInt > 0)
            {
                for (int i = index; i > -1 && i > index - _length.ValueInt; i--)
                {
                    if (upLine < candles[i].High)
                    {
                        upLine = candles[i].High;
                    }
                }
            }

            decimal downLine = 0;

            if (index - _length.ValueInt > 0)
            {
                downLine = decimal.MaxValue;

                for (int i = index; i > -1 && i > index - _length.ValueInt; i--)
                {
                    if (downLine > candles[i].Low)
                    {
                        downLine = candles[i].Low;
                    }
                }
            }

            _seriesUpBorder.Values[index] = upLine;
            _seriesDownBorder.Values[index] = downLine;

            decimal channelHeight = _seriesUpBorder.Values[index] - _seriesDownBorder.Values[index];

            decimal offset = channelHeight / 100 * _offset.ValueDecimal;

            _seriesUp.Values[index] = _seriesUpBorder.Values[index] - offset;
            _seriesDown.Values[index] = _seriesDownBorder.Values[index] + offset;

        }
    }
}