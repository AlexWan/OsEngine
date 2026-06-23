using System.Collections.Generic;
using System.Drawing;
using OsEngine.Entity;
using OsEngine.Language;

namespace OsEngine.Indicators
{
    [Indicator("Sma")]
    public class Sma : Aindicator
    {
        public override string Description
        {
            get
            {
                string eng = "SMA smooths price fluctuations on the chart and helps determine the current trend direction. " +
                             "Traders often use the crossover of fast and slow SMA as a trading signal.";

                string ru = "SMA сглаживает ценовые колебания на графике и помогает определить текущее направление тренда. " +
                            "Трейдеры часто используют пересечение быстрой и медленной SMA как торговый сигнал.";

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
                _candlePoint = CreateParameterStringCollection("Candle Point", "Close", Entity.CandlePointsArray);
                _series = CreateSeries("Ma", Color.DodgerBlue, IndicatorChartPaintType.Line, true);
            }
        }

        public override void OnProcess(List<Candle> candles, int index)
        {
            if (_length.ValueInt > index)
            {
                _series.Values[index] = 0;
                return;
            }

            _series.Values[index] = candles.Summ(index - _length.ValueInt, index, _candlePoint.ValueString) / _length.ValueInt;
        }
    }
}