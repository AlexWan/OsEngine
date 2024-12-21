using System.Collections.Generic;
using System.Drawing;
using OsEngine.Entity;

namespace OsEngine.Indicators
{
    [Indicator("OffsetVwma")]
    public class OffsetVwma : Aindicator
    {
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