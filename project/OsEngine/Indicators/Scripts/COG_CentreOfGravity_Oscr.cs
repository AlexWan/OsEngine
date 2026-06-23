using System;
using System.Collections.Generic;
using System.Drawing;
using OsEngine.Entity;
using OsEngine.Language;

namespace OsEngine.Indicators
{
    [Indicator("COG_CentreOfGravity_Oscr")]
    public class COG_CentreOfGravity_Oscr : Aindicator
    {
        public override string Description
        {
            get
            {
                string eng = "Center of Gravity (COG) calculates the weighted position of price over a period, showing how far the current price has deviated from its \"center of mass\" and helping to identify overbought/oversold moments. " +
                             "Traders use COG to find potential reversal zones and divergences, especially in sideways markets where price oscillates around its average.";

                string ru = "Центр тяжести (COG) рассчитывает взвешенное положение цены за период, показывая, насколько текущая цена отклонилась от своего «центра масс» и помогая определить моменты перекупленности/перепроданности. " +
                            "Трейдеры применяют COG для поиска зон возможного разворота и дивергенций, особенно в боковых рынках, когда цена колеблется вокруг среднего значения.";

                return OsLocalization.ConvertToLocString($"Eng:{eng}_Ru:{ru}_");
            }
        }

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