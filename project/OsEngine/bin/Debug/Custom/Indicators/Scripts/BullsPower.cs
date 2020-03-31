using System.Collections.Generic;
using System.Drawing;
using OsEngine.Entity;
using OsEngine.Indicators;

namespace CustomIndicators.Scripts
{
    public class BullsPower : Aindicator
    {
        private IndicatorParameterInt _length;
        private Aindicator _sma;
        private IndicatorDataSeries _series;

        public override void OnStateChange(IndicatorState state)
        {
            if (state == IndicatorState.Configure)
            {
                _length = CreateParameterInt("Period", 13);

                _series = CreateSeries("Values", Color.LimeGreen, IndicatorChartPaintType.Column, true);

                _sma = IndicatorsFactory.CreateIndicatorByName("Sma", Name + "Sma", false);
                ((IndicatorParameterInt)_sma.Parameters[0]).Bind(_length);
                ProcessIndicator("Sma", _sma);
            }
        }

        public override void OnProcess(List<Candle> candles, int index)
        {
            if (index < _length.ValueInt || _sma.DataSeries[0].Values.Count < _length.ValueInt ||
                _sma.DataSeries[0].Values[index] == 0)
            {
                return;
            }

            _series.Values[index] = candles[index].High - _sma.DataSeries[0].Values[index];
        }
    }
}