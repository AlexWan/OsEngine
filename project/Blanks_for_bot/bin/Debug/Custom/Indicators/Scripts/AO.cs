using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using OsEngine.Entity;
using OsEngine.Indicators;

namespace CustomIndicators.Scripts
{
    public class AO:Aindicator
    {
        private IndicatorParameterInt _lenghtFastLine;
        private IndicatorParameterInt _lenghtSlowLine;
        public IndicatorParameterString _candlePoint;

        private IndicatorDataSeries _series;

        private Aindicator _emaSlow;
        private Aindicator _emaFast;

        public override void OnStateChange(IndicatorState state)
        {
            if (state == IndicatorState.Configure)
            {
                _lenghtFastLine = CreateParameterInt("Fast line length", 5);
                _lenghtSlowLine = CreateParameterInt("Slow line length", 32);
                _candlePoint = CreateParameterStringCollection("Candle point", "Typical", Entity.CandlePointsArray);

                _series = CreateSeries("AO", Color.DarkGreen, IndicatorChartPaintType.Column, true);

                _emaFast = IndicatorsFactory.CreateIndicatorByName("Ema", Name + "Ema fast", false);
                ((IndicatorParameterInt)_emaFast.Parameters[0]).Bind(_lenghtFastLine);
                ((IndicatorParameterString)_emaFast.Parameters[1]).Bind(_candlePoint);
                ProcessIndicator("Ema fast", _emaFast);

                _emaSlow = IndicatorsFactory.CreateIndicatorByName("Ema", Name + "Ema slow", false);
                ((IndicatorParameterInt)_emaSlow.Parameters[0]).Bind(_lenghtSlowLine);
                ((IndicatorParameterString)_emaSlow.Parameters[1]).Bind(_candlePoint);
                ProcessIndicator("Ema slow", _emaSlow);
            }
        }

        public override void OnProcess(List<Candle> candles, int index)
        {
            _series.Values[index] = GetValue(candles, index);
        }

        private decimal GetValue(List<Candle> candles, int index)
        {
            if (index - _lenghtSlowLine.ValueInt <= 0 ||
                index - _lenghtFastLine.ValueInt <= 0)
            {
                return 0;
            }

            return Math.Round(_emaFast.DataSeries[0].Values[index] - _emaSlow.DataSeries[0].Values[index], 6);
        }
    }
}
