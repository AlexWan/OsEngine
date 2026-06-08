using System.Collections.Generic;
using System.Drawing;
using OsEngine.Entity;

namespace OsEngine.Indicators
{
    [Indicator("OffsetSma")]
    public class OffsetSma : Aindicator
    {
        private IndicatorParameterInt _lengthSma;

        private IndicatorParameterInt _offset;

        private IndicatorDataSeries _series;

        private Aindicator _OffsetSma;

        public override void OnStateChange(IndicatorState state)
        {
            if (state == IndicatorState.Configure)
            {
                _lengthSma = CreateParameterInt("Sma length", 5);

                _offset = CreateParameterInt("Sma offset", 5);

                _series = CreateSeries("Sma", Color.DarkRed, IndicatorChartPaintType.Line, true);

                _OffsetSma = IndicatorsFactory.CreateIndicatorByName("Sma", Name + "Sma", false);
                _OffsetSma.Parameters[0].Bind(_lengthSma);
                ProcessIndicator("OffsetSma", _OffsetSma);
            }
        }

        public override void OnProcess(List<Candle> candles, int index)
        {
            if (index >= _offset.ValueInt)
            {
                    _series.Values[index] = _OffsetSma.DataSeries[0].Values[index - _offset.ValueInt];
            }
        }
    }
}