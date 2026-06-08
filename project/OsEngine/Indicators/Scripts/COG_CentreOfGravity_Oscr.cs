using System;
using System.Collections.Generic;
using System.Drawing;
using OsEngine.Entity;

namespace OsEngine.Indicators
{
    [Indicator("COG_CentreOfGravity_Oscr")]
    public class COG_CentreOfGravity_Oscr : Aindicator
    {
        private IndicatorParameterInt _length;

        private IndicatorDataSeries _lastCOG_series;

        private IndicatorDataSeries _prevCOG_series;

        private IndicatorParameterString _candlePoint;

        public override void OnStateChange(IndicatorState state)
        {
            if (state == IndicatorState.Configure)
            {
                _length = CreateParameterInt("Length", 14);
                _candlePoint = CreateParameterStringCollection("Candle Point", "Close", OsEngine.Indicators.Entity.CandlePointsArray);

                _lastCOG_series = CreateSeries("COG series", Color.Red, IndicatorChartPaintType.Line, true);
                _prevCOG_series = CreateSeries("Prev COG", Color.Aqua, IndicatorChartPaintType.Line, true);
            }
        }

        public override void OnProcess(List<Candle> candles, int index)
        {
            if (index < _length.ValueInt)
                return;

            decimal COG = Cacl(candles, index);
            decimal PrevCOG = _lastCOG_series.Values[index - 1];

            _lastCOG_series.Values[index] = Math.Round(COG, 3);
            _prevCOG_series.Values[index] = Math.Round(PrevCOG, 3);
        }
     
        public decimal Cacl(List<Candle> candles, int index)
        {
            decimal temp = 0;
            decimal temp2 = 0;

            for (int i = index - _length.ValueInt, j = 0; i < index; i++, j++)
            {
                temp += candles[i].GetPoint(_candlePoint.ValueString) * (j + 1);
                temp2 += candles[i].GetPoint(_candlePoint.ValueString);
            }

            decimal result = temp / temp2 - (_length.ValueInt + 1) / 2;

            return result;
        }
    }
}