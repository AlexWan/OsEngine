using System;
using System.Collections.Generic;
using System.Drawing;
using OsEngine.Entity;
using OsEngine.Language;

namespace OsEngine.Indicators
{
    [Indicator("CCI")]
    public class CCI : Aindicator
    {
        public override string Description
        {
            get
            {
                string eng = "CCI compares the current price with the average price over a period and outputs an oscillator showing how far price deviates from its typical range. " +
                             "Traders use CCI to find overbought/oversold conditions, divergences with price, and zero-line crossovers as confirmation of trade direction.";

                string ru = "CCI сравнивает текущую цену со средней ценой за период и выводит осциллятор, показывающий отклонение цены от типичного диапазона. " +
                            "Трейдеры используют CCI для поиска перекупленности/перепроданности, дивергенций с ценой и пробоя нулевой линии в качестве подтверждения направления сделки.";

                return OsLocalization.ConvertToLocString($"Eng:{eng}_Ru:{ru}_");
            }
        }

        private IndicatorParameterInt _length;

        private IndicatorParameterString _candlePoint;

        private IndicatorDataSeries _series;

        public override void OnStateChange(IndicatorState state)
        {
            if (state == IndicatorState.Configure)
            {
                _length = CreateParameterInt("Length", 20);
                _candlePoint = CreateParameterStringCollection("Candle Point", "Typical", Entity.CandlePointsArray);
                _series = CreateSeries("Cci", Color.CadetBlue, IndicatorChartPaintType.Line, true);
            }
        }

        public override void OnProcess(List<Candle> candles, int index)
        {
            if (index - _length.ValueInt <= 0)
            {
                return;
            }

            decimal sum = 0;
            for (int i = index; i > index - _length.ValueInt; i--)
            {
                sum += candles[i].GetPoint(_candlePoint.ValueString);
            }
            // average count
            // подсчет средней
            var ma = sum / _length.ValueInt;

            decimal md = 0;
            for (int i = index; i > index - _length.ValueInt; i--)
            {
                md += Math.Abs(ma - candles[i].GetPoint(_candlePoint.ValueString));
            }

            if(md == 0)
            {
                return;
            }

            var cciP = (candles[index].GetPoint(_candlePoint.ValueString) - ma) / (md * 0.015m / _length.ValueInt);

            _series.Values[index] = Math.Round(cciP, 5);
        }
    }
}