using System;
using System.Collections.Generic;
using System.Drawing;
using OsEngine.Entity;
using OsEngine.Indicators;

namespace CustomIndicators.Scripts
{
    public class StdDev: Aindicator
    {
        private IndicatorDataSeries _series;
        private IndicatorParameterInt _period;
        private IndicatorParameterString _candlePoint;

        public override void OnStateChange(IndicatorState state)
        {
            if (state == IndicatorState.Configure)
            {
                _period = CreateParameterInt("Period", 20);
                _candlePoint = CreateParameterStringCollection("Candle point", "Close", Entity.CandlePointsArray);
                _series = CreateSeries("StdDev", Color.Green, IndicatorChartPaintType.Line, true);
            }

        }

        public override void OnProcess(List<Candle> candles, int index)
        {
            _series.Values[index] = GetValue(candles, index);
        }
		
        private decimal GetValue(List<Candle> candles, int index)
        {
            if (index - _period.ValueInt <= 0)
            {
                return 0;
            }

            decimal sd = 0;

            int lenght2;
            if (index - _period.ValueInt <= _period.ValueInt) lenght2 = index - _period.ValueInt; else lenght2 = _period.ValueInt;

            decimal sum = 0;
            for (int j = index; j > index - _period.ValueInt; j--)
            {
                sum += GetPoint(candles, j);
            }

            var m = sum / _period.ValueInt;

            for (int i = index; i > index - lenght2; i--)
            {
                decimal x = GetPoint(candles, i) - m;  //Difference between values for period and average/разница между значениями за период и средней
                double g = Math.Pow((double)x, 2.0);   // difference square/ квадрат зницы
                sd += (decimal)g;   //square footage/ сумма квадратов
            }
            sd = (decimal)Math.Sqrt((double)sd / lenght2);  //find the root of sum/period // находим корень из суммы/период 

            return Math.Round(sd, 5);

        }

        private decimal GetPoint(List<Candle> candles, int index)
        {
            if (_candlePoint.ValueString == "Close")
            {
                return candles[index].Close;
            }
            if (_candlePoint.ValueString == "High")
            {
                return candles[index].High;
            }
            if (_candlePoint.ValueString == "Low")
            {
                return candles[index].Low;
            }
            if (_candlePoint.ValueString == "Open")
            {
                return candles[index].Open;
            }
            if (_candlePoint.ValueString == "Median")
            {
                return (candles[index].High + candles[index].Low) / 2;
            }
            if (_candlePoint.ValueString == "Typical")
            {
                return (candles[index].High + candles[index].Low + candles[index].Close) / 3;
            }
            return 0;
        }
    }
}
