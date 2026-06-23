using System.Collections.Generic;
using System.Drawing;
using OsEngine.Entity;
using OsEngine.Language;

namespace OsEngine.Indicators
{
    [Indicator("OffsetSsma")]
    public class OffsetSsma : Aindicator
    {
        public override string Description
        {
            get
            {
                string eng = "OffsetSsma builds a smoothed moving average and shifts it to the right by a set number of bars, carrying past indicator values into the current chart zone. " +
                             "Traders use the shifted SSMA to compare current price with historical average levels and find recurring supply/demand areas.";

                string ru = "OffsetSsma строит сглаженную скользящую среднюю и сдвигает её вправо на заданное число баров, перенося прошлые значения индикатора в текущую зону графика. " +
                            "Трейдеры применяют сдвинутую SSMA для сравнения текущей цены с историческими уровнями средней и поиска повторяющихся областей спроса/предложения.";

                return OsLocalization.ConvertToLocString($"Eng:{eng}_Ru:{ru}_");
            }
        }

        private IndicatorParameterInt _lengthSsma;

        private IndicatorParameterInt _offset;

        private IndicatorDataSeries _series;

        private Aindicator _OffsetSsma;

        public override void OnStateChange(IndicatorState state)
        {
            if (state == IndicatorState.Configure)
            {
                _lengthSsma = CreateParameterInt("Ssma length", 5);

                _offset = CreateParameterInt("Ssma offset", 5);

                _series = CreateSeries("Ssma", Color.DarkRed, IndicatorChartPaintType.Line, true);

                _OffsetSsma = IndicatorsFactory.CreateIndicatorByName("Ssma", Name + "Ssma", false);
                _OffsetSsma.Parameters[0].Bind(_lengthSsma);
                ProcessIndicator("OffsetSsma", _OffsetSsma);
            }
        }

        public override void OnProcess(List<Candle> candles, int index)
        {
            if (index >= _offset.ValueInt)
            {
                _series.Values[index] = _OffsetSsma.DataSeries[0].Values[index - _offset.ValueInt];
            }
        }
    }
}