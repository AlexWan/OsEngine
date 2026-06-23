using System;
using System.Collections.Generic;
using System.Drawing;
using OsEngine.Entity;
using OsEngine.Language;

namespace OsEngine.Indicators
{
    [Indicator("ROC")]
    public class ROC:Aindicator
    {
        public override string Description
        {
            get
            {
                string eng = "Rate of Change calculates the percentage price change over a selected period, showing the speed of an instrument's rise or fall. " +
                             "Traders use ROC to find overbought/oversold conditions, divergences with price, and zero-line crossovers as signals of a direction change.";

                string ru = "Rate of Change вычисляет процентное изменение цены за выбранный период, показывая скорость роста или падения инструмента. " +
                            "Трейдеры используют ROC для поиска перекупленности/перепроданности, дивергенций с ценой и пересечения нулевой линии как сигнала к смене направления.";

                return OsLocalization.ConvertToLocString($"Eng:{eng}_Ru:{ru}_");
            }
        }

        private IndicatorParameterInt _period;

        private IndicatorParameterString _candlePoint;

        private IndicatorDataSeries _series;

        public override void OnStateChange(IndicatorState state)
        {
            if (state == IndicatorState.Configure)
            {
                _period = CreateParameterInt("Period", 13);
                _candlePoint = CreateParameterStringCollection("Candle point", "Close", Entity.CandlePointsArray);
                _series = CreateSeries("ROC", Color.Aqua, IndicatorChartPaintType.Line, true);
            }
        }

        public override void OnProcess(List<Candle> candles, int index)
        {
            _series.Values[index] = GetValue(candles, index);
        }

        private decimal GetValue(List<Candle> candles, int index)
        {
            if (index < _period.ValueInt)
            {
                return 0;
            }

            decimal value = 0;

            if (_candlePoint.ValueString == "Close")
            {
                value = (candles[index].Close - candles[index - _period.ValueInt].Close) / candles[index - _period.ValueInt].Close * 100;
            }
            if (_candlePoint.ValueString == "Open")
            {
                value = (candles[index].Open - candles[index - _period.ValueInt].Open) / candles[index - _period.ValueInt].Open * 100;
            }
            if (_candlePoint.ValueString == "High")
            {
                value = (candles[index].High - candles[index - _period.ValueInt].High) / candles[index - _period.ValueInt].High * 100;
            }
            if (_candlePoint.ValueString == "Low")
            {
                value = (candles[index].Low - candles[index - _period.ValueInt].Low) / candles[index - _period.ValueInt].Low * 100;
            }

            return Math.Round(value, 3);
        }
    }
}