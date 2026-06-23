using System.Collections.Generic;
using System.Drawing;
using OsEngine.Entity;
using OsEngine.Language;

namespace OsEngine.Indicators
{
    [Indicator("OffsetEma")]
    public class OffsetEma : Aindicator
    {
        public override string Description
        {
            get
            {
                string eng = "OffsetEma builds an exponential moving average and shifts it to the right by a set number of bars, visually carrying historical EMA values into the current chart area. " +
                             "Traders use the shifted EMA to compare past levels with current price and find recurring support or resistance zones.";

                string ru = "OffsetEma строит экспоненциальную скользящую среднюю и сдвигает её вправо на заданное число баров, визуально перенося исторические значения EMA в текущую область графика. " +
                            "Трейдеры применяют сдвинутую EMA для сопоставления прошлых уровней с текущей ценой и поиска повторяющихся зон поддержки или сопротивления.";

                return OsLocalization.ConvertToLocString($"Eng:{eng}_Ru:{ru}_");
            }
        }

        private IndicatorParameterInt _length;

        private IndicatorParameterInt _offSet;

        private IndicatorDataSeries _shift;

        private Aindicator _emaOffset;

        public override void OnStateChange(IndicatorState state)
        {
            if (state == IndicatorState.Configure)
            {
                _length = CreateParameterInt("Length", 5);
                _offSet = CreateParameterInt("Offset", 5);
                _shift = CreateSeries("Offset", Color.DodgerBlue, IndicatorChartPaintType.Line, true);
                _emaOffset = IndicatorsFactory.CreateIndicatorByName("Ema", Name + "EmaLine", false);

                ((IndicatorParameterInt)_emaOffset.Parameters[0]).Bind(_length);
                ProcessIndicator("EmaOffset", _emaOffset);
            }
        }

        public override void OnProcess(List<Candle> source, int index)
        {
            if (index - _offSet.ValueInt >= 0)
            {
                _shift.Values[index] = _emaOffset.DataSeries[0].Values[index - _offSet.ValueInt];
            }
        }
    }
}