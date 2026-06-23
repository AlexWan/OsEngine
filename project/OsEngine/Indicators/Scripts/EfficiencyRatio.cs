using System;
using System.Collections.Generic;
using System.Drawing;
using OsEngine.Entity;
using OsEngine.Language;

namespace OsEngine.Indicators
{
    [Indicator("EfficiencyRatio")]
    public class EfficiencyRatio:Aindicator
    {
        public override string Description
        {
            get
            {
                string eng = "Efficiency Ratio compares net price change over a period to the sum of all intra-period movements, showing how directional the movement was. " +
                             "Traders use ER to assess trend strength and adapt parameters of other indicators: low values indicate noise and sideways movement, while high values indicate a sustained trend.";

                string ru = "Коэффициент эффективности сравнивает чистое изменение цены за период с суммой всех внутрипериодных движений, показывая, насколько направленным было движение. " +
                            "Трейдеры используют ER для оценки силы тренда и адаптации параметров других индикаторов: низкое значение указывает на шум и боковик, высокое — на устойчивый тренд.";

                return OsLocalization.ConvertToLocString($"Eng:{eng}_Ru:{ru}_");
            }
        }

        private IndicatorDataSeries _series;

        private IndicatorParameterInt _length;

        public override void OnStateChange(IndicatorState state)
        {
            if (state == IndicatorState.Configure)
            {
                _length = CreateParameterInt("Length", 14);
                _series = CreateSeries("ER", Color.DodgerBlue, IndicatorChartPaintType.Line, true);
            }
        }

        public override void OnProcess(List<Candle> candles, int index)
        {
            _series.Values[index] = GetValue(candles, index);
        }

        private decimal GetValue(List<Candle> candles, int index)
        {
            decimal eR1 = 0;
            decimal eR2 = 0;
            int Length = _length.ValueInt;

            if (index - Length > 0)
            {
                eR1 = Math.Abs(candles[index].Close - candles[index - Length].Close);

                eR2 = 0;

                for (int i = 0; i <= Length; i++)
                {
                    eR2 += Math.Abs(candles[index - i].Close - candles[index - i - 1].Close);
                }
            }

            if (eR2 != 0)
            {
                return Math.Round(eR1 / eR2, 4);
            }

            return 0;
        }
    }
}