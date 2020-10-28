using System;
using System.Collections.Generic;
using System.Drawing;
using OsEngine.Entity;
using OsEngine.Indicators;

namespace CustomIndicators.Scripts
{
    public class KalmanFilter:Aindicator
    {
        private IndicatorDataSeries _series;
        private IndicatorParameterDecimal _sharpness;
        private IndicatorParameterDecimal K;

        public override void OnStateChange(IndicatorState state)
        {
            if (state == IndicatorState.Configure)
            {
                _series = CreateSeries("Kalman", Color.DodgerBlue, IndicatorChartPaintType.Line, true);
                _sharpness = CreateParameterDecimal("Sharpness", 1);
                K = CreateParameterDecimal("K", 1);
            }

            if (state == IndicatorState.Dispose)
            {
                _velocity.Clear();
            }
        }

        public override void OnProcess(List<Candle> candles, int index)
        {
            _series.Values[index] = GetValue(candles,index);
        }

        private List<double> _velocity = new List<double>();

        private decimal GetValue(List<Candle> candles, int index)
        {
            //Kalman[i]=Error+Velocity[i], where
            //Error=Kalman[i-1]+Distance*ShK,
            //Velocity[i]=Velocity[i-1]+Distance*K/100,
            //Distance=Price[i]-Kalman[i-1],
            //ShK=sqrt(Sharpness*K/100).

            if (index == 0)
            {
                _velocity.Add(0);
                return 0;
            }

            double shk = Math.Sqrt(Convert.ToDouble(_sharpness.ValueDecimal * K.ValueDecimal / 100));
            double distans = Convert.ToDouble(candles[index].Close - _series.Values[index - 1]);

            if (index + 1 > _velocity.Count)
            {
                _velocity.Add(0);
            }

            _velocity[index] = _velocity[index - 1] + distans * Convert.ToDouble(K.ValueDecimal) / 100;

            double error = Convert.ToDouble(_series.Values[index - 1]) + distans * shk;

            return Convert.ToDecimal(error + _velocity[index]);
        }
    }
}
