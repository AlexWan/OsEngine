using System;
using System.Collections.Generic;
using System.Drawing;
using OsEngine.Entity;
using OsEngine.Indicators;

namespace CustomIndicators.Scripts
{
    public class RSI : Aindicator
    {
        private IndicatorParameterInt _lenght;


        private IndicatorDataSeries _series;

        public override void OnStateChange(IndicatorState state)
        {
            _lenght = CreateParameterInt("Lenght", 14);

            _series = CreateSeries("Ma", Color.DodgerBlue, IndicatorChartPaintType.Line, true);
        }

        public override void OnProcess(List<Candle> candles, int index)
        {
            if (index - _lenght.ValueInt - 1 <= 0)
            {
                return;
            }

            int startIndex = 1;

            if (index > 150)
            {
                startIndex = index - 150;
            }

            decimal[] priceChangeHigh = new decimal[candles.Count];
            decimal[] priceChangeLow = new decimal[candles.Count];

            decimal[] priceChangeHighAverage = new decimal[candles.Count];
            decimal[] priceChangeLowAverage = new decimal[candles.Count];

            for (int i = startIndex; i < candles.Count; i++)
            {
                if (candles[i].Close - candles[i - 1].Close > 0)
                {
                    priceChangeHigh[i] = candles[i].Close - candles[i - 1].Close;
                    priceChangeLow[i] = 0;
                }
                else
                {
                    priceChangeLow[i] = candles[i - 1].Close - candles[i].Close;
                    priceChangeHigh[i] = 0;
                }

                MovingAverageHard(priceChangeHigh, priceChangeHighAverage, _lenght.ValueInt, i);
                MovingAverageHard(priceChangeLow, priceChangeLowAverage, _lenght.ValueInt, i);
            }

            decimal averageHigh = priceChangeHighAverage[index];
            decimal averageLow = priceChangeLowAverage[index];

            decimal rsi;

            if (averageHigh != 0 &&
                averageLow != 0)
            {
                rsi = 100 * (1 - averageLow / (averageLow + averageHigh));
                //rsi = 100 - 100 / (1 + averageHigh / averageLow);
            }
            else
            {
                rsi = 100;
            }

            _series.Values[index] = Math.Round(rsi, 2);
        }

        private void MovingAverageHard(decimal[] valuesSeries, decimal[] moving, int length, int index)
        {
            if (index == length)
            {

                decimal lastMoving = 0;

                for (int i = index; i > index - 1 - length; i--)
                {
                    lastMoving += valuesSeries[i];
                }
                lastMoving = lastMoving / length;

                moving[index] = lastMoving;
            }
            else if (index > length)
            {

                decimal a = Math.Round(2.0m / (length * 2), 7);
                decimal lastValueMoving = moving[index - 1];
                decimal lastValueSeries = Math.Round(valuesSeries[index], 7);
                decimal nowValueMoving;
                nowValueMoving = Math.Round(lastValueMoving + a * (lastValueSeries - lastValueMoving), 7);

                moving[index] = nowValueMoving;
            }
        }
    }
}
