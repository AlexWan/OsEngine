using System;
using System.Collections.Generic;
using System.Drawing;
using OsEngine.Entity;

namespace OsEngine.Indicators
{
    [Indicator("AO")]
    public class AO:Aindicator
    {
        private IndicatorParameterInt _lengthFastLine;

        private IndicatorParameterInt _lengthSlowLine;

        public IndicatorParameterString _candlePoint;

        private IndicatorDataSeries _series;

        private Aindicator _emaSlow;

        private Aindicator _emaFast;

        public override void OnStateChange(IndicatorState state)
        {
            if (state == IndicatorState.Configure)
            {
                _lengthFastLine = CreateParameterInt("Fast line length", 5);
                _lengthSlowLine = CreateParameterInt("Slow line length", 32);
                _candlePoint = CreateParameterStringCollection("Candle point", "Typical", Entity.CandlePointsArray);

                _series = CreateSeries("AO", Color.DarkGreen, IndicatorChartPaintType.Column, true);

                _emaFast = IndicatorsFactory.CreateIndicatorByName("Ema", Name + "Ema fast", false);
                _emaFast.Parameters[0].Bind(_lengthFastLine);
                _emaFast.Parameters[1].Bind(_candlePoint);
                ProcessIndicator("Ema fast", _emaFast);

                _emaSlow = IndicatorsFactory.CreateIndicatorByName("Ema", Name + "Ema slow", false);
                _emaSlow.Parameters[0].Bind(_lengthSlowLine);
                _emaSlow.Parameters[1].Bind(_candlePoint);
                ProcessIndicator("Ema slow", _emaSlow);
            }
        }

        public override void OnProcess(List<Candle> candles, int index)
        {
            _series.Values[index] = GetValue(candles, index);
        }

        private decimal GetValue(List<Candle> candles, int index)
        {
            if (index - _lengthSlowLine.ValueInt <= 0 ||
                index - _lengthFastLine.ValueInt <= 0)
            {
                return 0;
            }

            return Math.Round(_emaFast.DataSeries[0].Values[index] - _emaSlow.DataSeries[0].Values[index], 6);
        }
    }
}