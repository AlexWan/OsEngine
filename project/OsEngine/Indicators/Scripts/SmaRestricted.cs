using System.Collections.Generic;
using System.Drawing;
using OsEngine.Entity;
using OsEngine.Indicators;

namespace CustomIndicators.Scripts
{
    public class SmaRestricted : Aindicator
    {
        private IndicatorParameterInt _length;

        private IndicatorParameterString _candlePoint;

        private IndicatorDataSeries _series;

        public override void OnStateChange(IndicatorState state)
        {
            if (state == IndicatorState.Configure)
            {
                _length = CreateParameterInt("Length", 14);
                _candlePoint = CreateParameterStringCollection("Candle Point", "Close", Entity.CandlePointsArray);
                _series = CreateSeries("Ma", Color.DodgerBlue, IndicatorChartPaintType.Line, true);
            }
        }

        public override void OnProcess(List<Candle> candles, int index)
        {
            if (_length.ValueInt > index || candles[index].TimeStart.Hour >= 19)
            {
                _series.Values[index] = 0;
                return;
            }

            decimal summ = 0;

            int countPoints = 0;

            for (int i = index; i >= 0 && i > index - _length.ValueInt; i--)
            {
                // filterOut candles
                if (candles[i].TimeStart.Hour >= 19)
                    continue;
                
                countPoints++;
                summ += candles[i].Close;
            }

            decimal sma = 0;

            if (countPoints > 0)
            {
                sma = summ / countPoints;
            }

            _series.Values[index] = sma;
        }
    }
}