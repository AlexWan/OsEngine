using System;
using System.Collections.Generic;
using System.Drawing;
using OsEngine.Entity;
using OsEngine.Indicators;

namespace CustomIndicators.Scripts
{
    public class OBV:Aindicator
    {
        private IndicatorDataSeries _series;
        public override void OnStateChange(IndicatorState state)
        {
            if (state == IndicatorState.Configure)
            {
                _series = CreateSeries("OBV", Color.Orange, IndicatorChartPaintType.Line, true);
            }
            else if (state == IndicatorState.Dispose)
            {
                if (_temp != null)
                {
                    _temp.Clear();
                }
            }
        }
        public override void OnProcess(List<Candle> candles, int index)
        {
            _series.Values[index] = GetValue(candles, index);
        }

        private decimal GetValue(List<Candle> candles, int index)
        {
			if(index == 0)
			{
				_temp.Clear();
			}
			
            while(index >= _temp.Count)
            {
                _temp.Add(0);
            }
            if (index < 2)
            {
                return 0;
            }

            if (index > 2)
            {

                decimal p1 = candles[index].Close;
                decimal p2 = candles[index - 1].Close;

                if (p1 > p2)
                {
                    _temp[index] = _temp[index - 1] + candles[index].Volume;
                }

                if (p1 < p2)
                {
                    _temp[index] = _temp[index - 1] - candles[index].Volume;
                }

                if (p1 == p2)
                {
                    _temp[index] = _temp[index - 1];
                }

                return Math.Round(_temp[index]);
            }
            return 0;
        }

        private List<decimal> _temp = new List<decimal>();

    }
}
