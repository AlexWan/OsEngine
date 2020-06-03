using System.Collections.Generic;
using System.Drawing;
using OsEngine.Entity;
using OsEngine.Indicators;

namespace CustomIndicators.Scripts
{
    public class PriceChannel : Aindicator
    {
        private IndicatorParameterInt _lenghtUp;
        private IndicatorParameterInt _lenghtDown;

        private IndicatorDataSeries _seriesUp;
        private IndicatorDataSeries _seriesDown;

        public override void OnStateChange(IndicatorState state)
        {
            if (state == IndicatorState.Configure)
            {
                _lenghtUp = CreateParameterInt("Length up", 21);
                _lenghtDown = CreateParameterInt("Lenght down", 21);

                _seriesUp = CreateSeries("Up line", Color.Aqua, IndicatorChartPaintType.Line, true);
                _seriesDown = CreateSeries("Down line", Color.BlueViolet, IndicatorChartPaintType.Line, true);
            }
        }

        public override void OnProcess(List<Candle> candles, int index)
        {
            decimal upLine = 0;

            if (index - _lenghtUp.ValueInt > 0)
            {
                for (int i = index; i > -1 && i > index - _lenghtUp.ValueInt; i--)
                {
                    if (upLine < candles[i].High)
                    {
                        upLine = candles[i].High;
                    }
                }
            }

            decimal downLine = 0;

            if (index - _lenghtDown.ValueInt > 0)
            {
                downLine = decimal.MaxValue;

                for (int i = index; i > -1 && i > index - _lenghtDown.ValueInt; i--)
                {
                    if (downLine > candles[i].Low)
                    {
                        downLine = candles[i].Low;
                    }
                }
            }

            _seriesUp.Values[index] = upLine;
            _seriesDown.Values[index] = downLine;
        }
    }
}