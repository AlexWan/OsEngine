using System.Collections.Generic;
using System.Drawing;
using OsEngine.Entity;

namespace OsEngine.Indicators
{
    [Indicator("OffsetEma")]
    public class OffsetEma : Aindicator
    {
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