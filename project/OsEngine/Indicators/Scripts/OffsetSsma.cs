using System.Collections.Generic;
using System.Drawing;
using OsEngine.Entity;

namespace OsEngine.Indicators
{
    [Indicator("OffsetSsma")]
    public class OffsetSsma : Aindicator
    {
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