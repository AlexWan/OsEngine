using System.Collections.Generic;
using System.Drawing;
using OsEngine.Entity;
using OsEngine.Language;

namespace OsEngine.Indicators
{
    [Indicator("VWMA")]
    public class VWMA:Aindicator
    {
        public override string Description
        {
            get
            {
                string eng = "VWMA calculates a moving average weighted by trading volume, giving more weight to bars with high activity and less to illiquid bars. " +
                             "Traders use VWMA to determine key support/resistance levels accounting for volume, and its crossovers with price or other averages.";

                string ru = "VWMA рассчитывает скользящую среднюю, взвешенную по объёму торгов, придавая больший вес барам с высокой активностью и меньший — малоликвидным барам. " +
                            "Трейдеры используют VWMA для определения ключевых уровней поддержки/сопротивления с учётом объёма и пересечения её с ценой или другими средними.";

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
                _length = CreateParameterInt("Length", 14);
                _candlePoint = CreateParameterStringCollection("Candle point", "Close", Entity.CandlePointsArray);
                _series = CreateSeries("Vwma", Color.Aqua, IndicatorChartPaintType.Line, true);
            }
        }

        public override void OnProcess(List<Candle> candles, int index)
        {
            if (_length.ValueInt > index)
            {
                return;
            }

            decimal average = 0;
            decimal weights = 0;

            for (int i = index; i > index - _length.ValueInt; i--)
            {
                average += candles[i].GetPoint(_candlePoint.ValueString) * candles[i].Volume;
                weights += candles[i].Volume;
            }

            if(weights != 0)
            {
                average = average / weights;
            }
            
            _series.Values[index] = average;
        }
    }
}