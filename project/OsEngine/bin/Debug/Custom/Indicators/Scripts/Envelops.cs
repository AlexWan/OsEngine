using System;
using System.Collections.Generic;
using System.Drawing;
using OsEngine.Entity;
using OsEngine.Indicators;

namespace CustomIndicators.Scripts
{
    public class Envelops : Aindicator
    {
        private IndicatorParameterInt _lenght;
        private IndicatorParameterDecimal _deviation;

        private IndicatorDataSeries _seriesUp;
        private IndicatorDataSeries _seriesDown;
        private IndicatorDataSeries _seriesCenter;

        private Aindicator _sma;

        public override void OnStateChange(IndicatorState state)
        {
            if (state == IndicatorState.Configure)
            {
                _lenght = CreateParameterInt("Length", 21);
                _deviation = CreateParameterDecimal("Deviation", 2);

                _seriesUp = CreateSeries("Up line", Color.Green, IndicatorChartPaintType.Line, true);
                _seriesCenter = CreateSeries("Centre line", Color.Green, IndicatorChartPaintType.Line, false);
                _seriesDown = CreateSeries("Down line", Color.Green, IndicatorChartPaintType.Line, true);

                _sma = IndicatorsFactory.CreateIndicatorByName("Sma", Name + "Sma", false);
                ((IndicatorParameterInt)_sma.Parameters[0]).Bind(_lenght);
                ProcessIndicator("Central SMA", _sma);
            }
        }

        public override void OnProcess(List<Candle> candles, int index)
        {
            if (index <= _lenght.ValueInt)
            {
                return;
            }

            decimal valueSma = _sma.DataSeries[0].Values[index];

            _seriesCenter.Values[index] = valueSma;
            _seriesUp.Values[index] = Math.Round(valueSma + valueSma * _deviation.ValueDecimal / 100, 6);
            _seriesDown.Values[index] = Math.Round(valueSma - valueSma * _deviation.ValueDecimal / 100, 6);
        }
    }
}