using System.Collections.Generic;
using System.Drawing;
using OsEngine.Entity;
using System;

namespace OsEngine.Indicators
{
    [Indicator("BullBearPower")]
    public class BullBearPower : Aindicator
    {
        private IndicatorParameterInt _length;

        private Aindicator _sma;

        private IndicatorDataSeries _series;

        public override void OnStateChange(IndicatorState state)
        {
            _length = CreateParameterInt("Period", 13);

            _series = CreateSeries("Values", Color.SaddleBrown, IndicatorChartPaintType.Column, true);

            _sma = IndicatorsFactory.CreateIndicatorByName("Sma", Name + "Sma", false);
            _sma.Parameters[0].Bind(_length);
            ProcessIndicator("Sma", _sma);
        }


        public override void OnProcess(List<Candle> candles, int index)
        {
            if (index < _length.ValueInt || _sma.DataSeries[0].Values.Count < _length.ValueInt ||
                _sma.DataSeries[0].Values[index] == 0)
            {
                return;
            }

            _series.Values[index] = Math.Round((candles[index].High - _sma.DataSeries[0].Values[index]) + (candles[index].Low - _sma.DataSeries[0].Values[index]), 7);
        }
    }
}
