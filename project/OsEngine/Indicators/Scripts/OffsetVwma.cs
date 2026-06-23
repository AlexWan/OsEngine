using System.Collections.Generic;
using System.Drawing;
using OsEngine.Entity;
using OsEngine.Language;

namespace OsEngine.Indicators
{
    [Indicator("OffsetVwma")]
    public class OffsetVwma : Aindicator
    {
        public override string Description
        {
            get
            {
                string eng = "OffsetVwma calculates a volume-weighted moving average and shifts it to the right by a set number of bars, carrying historical VWMA values into the current chart area. " +
                             "Traders use the shifted VWMA to find zones where price historically interacted with the volume-weighted average, helping assess the strength of support or resistance.";

                string ru = "OffsetVwma рассчитывает объёмно-взвешенную скользящую среднюю и сдвигает её вправо на заданное число баров, перенося исторические значения VWMA в текущую область графика. " +
                            "Трейдеры используют сдвинутую VWMA для поиска зон, где цена в прошлом взаимодействовала со средней с учётом объёма, что помогает оценить силу поддержки или сопротивления.";

                return OsLocalization.ConvertToLocString($"Eng:{eng}_Ru:{ru}_");
            }
        }

        private IndicatorParameterInt _lengthVwma;

        private IndicatorParameterInt _offset;

        private IndicatorDataSeries _series;

        private Aindicator _OffsetVwma;

        public override void OnStateChange(IndicatorState state)
        {
            if (state == IndicatorState.Configure)
            {
                _lengthVwma = CreateParameterInt("Vwma length", 5);

                _offset = CreateParameterInt("Vwma offset", 5);

                _series = CreateSeries("Vwma", Color.DarkRed, IndicatorChartPaintType.Line, true);

                _OffsetVwma = IndicatorsFactory.CreateIndicatorByName("VWMA", Name + "VWMA", false);
                ((IndicatorParameterInt)_OffsetVwma.Parameters[0]).Bind(_lengthVwma);
                ProcessIndicator("OffsetVwma", _OffsetVwma);

            }
        }

        public override void OnProcess(List<Candle> candles, int index)
        {
            if (index >= _offset.ValueInt)
            {
                _series.Values[index] = _OffsetVwma.DataSeries[0].Values[index - _offset.ValueInt];
            }
        }
    }
}