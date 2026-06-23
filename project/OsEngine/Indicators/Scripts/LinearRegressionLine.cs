using System.Collections.Generic;
using System.Drawing;
using OsEngine.Entity;
using OsEngine.Language;

namespace OsEngine.Indicators
{
    [Indicator("LinearRegressionLine")]
    public class LinearRegressionLine : Aindicator
    {
        public override string Description
        {
            get
            {
                string eng = "The Linear Regression Line approximates price data with a straight line using the least squares method, showing the main direction of price movement over the selected period. " +
                             "Traders use it as a trend filter: price above the line indicates bullish sentiment, below indicates bearish sentiment, and deviation from the line helps assess overbought/oversold conditions.";

                string ru = "Линия линейной регрессии аппроксимирует ценовые данные прямой по методу наименьших квадратов, показывая основное направление движения цены за выбранный период. " +
                            "Трейдеры применяют её как фильтр тренда: цена выше линии указывает на бычий настрой, ниже — на медвежий, а отклонение от линии помогает оценить перекупленность/перепроданность.";

                return OsLocalization.ConvertToLocString($"Eng:{eng}_Ru:{ru}_");
            }
        }

        private IndicatorParameterInt _period;

        private IndicatorDataSeries _seriesRegressionLine;

        private IndicatorParameterString _candlePoint;

        public override void OnStateChange(IndicatorState state)
        {
            if (state == IndicatorState.Configure)
            {
                _period = CreateParameterInt("Period", 14);
                _candlePoint = CreateParameterStringCollection("Candle Point", "Close", Entity.CandlePointsArray);

                _seriesRegressionLine = CreateSeries("Regression Line ", Color.HotPink,
                    IndicatorChartPaintType.Line, true);
            }
        }

        public override void OnProcess(List<Candle> candles, int index)
        {
            if (index - _period.ValueInt <= 0)
            {
                return;
            }

            // variables
            decimal a,
                b,
                c,
                sumy = 0.0m,
                sumx = 0.0m,
                sumxy = 0.0m,
                sumx2 = 0.0m;

            for (int i = index - _period.ValueInt + 1, g = 0; i < index + 1; i++, g++)
            {
                sumy += candles[i].GetPoint(_candlePoint.ValueString);
                sumxy += candles[i].GetPoint(_candlePoint.ValueString) * g;
                sumx += g;
                sumx2 += g * g;
            }

            c = sumx2 * _period.ValueInt - sumx * sumx;

            if (c == 0.0m)
            {
                return;
            }

            b = (sumxy * _period.ValueInt - sumx * sumy) / c;
            a = (sumy - sumx * b) / _period.ValueInt;

            _seriesRegressionLine.Values[index] = a + b * -(index - _period.ValueInt + 1 - index);

        }
    }
}