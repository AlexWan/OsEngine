using OsEngine.Entity;
using System;
using System.Collections.Generic;
using System.Drawing;

namespace OsEngine.Indicators
{
    [Indicator("QStick")]
    public class QStick : Aindicator
    {
        private IndicatorParameterInt _length;

        private IndicatorParameterString _typeMA;

        private IndicatorDataSeries _series;

        public override void OnStateChange(IndicatorState state)
        {
            List<string> typeMA = new List<string> { "SMA", "EMA" };

            _length = CreateParameterInt("Length", 14);
            _typeMA = CreateParameterStringCollection("Type MA", "SMA", typeMA);
            _series = CreateSeries("QStick", Color.Red, IndicatorChartPaintType.Line, true);
        }

        public override void OnProcess(List<Candle> candles, int index)
        {
            if (_typeMA.ValueString == "SMA")
                CaclQstickForSMA(candles, index);
            else
                CalcQstickForEMA(candles, index);
        }

        private void CalcQstickForEMA(List<Candle> candles, int index)
        {
            decimal result = 0;

            if (index == _length.ValueInt)
            {
                decimal lastMoving = 0;

                for (int i = index - _length.ValueInt + 1; i < index + 1; i++)
                {
                    lastMoving += candles[i].Close - candles[i].Open;
                }
                lastMoving = lastMoving / _length.ValueInt;
                result = lastMoving;
            }
            else if (index > _length.ValueInt)
            {
                decimal a = Math.Round(2.0m / (_length.ValueInt + 1), 8);
                decimal emaLast = _series.Values[index - 1];
                decimal p = candles[index].Close - candles[index].Open;
                result = emaLast + (a * (p - emaLast));
            }
            _series.Values[index] = Math.Round(result, 8);
        }

        public void CaclQstickForSMA(List<Candle> candles, int index)
        {
            if (_length.ValueInt > index)
            {
                _series.Values[index] = 0;
                return;
            }
            string typeClose = "Close";
            string typeOpen = "Open";

            decimal temp = candles.Summ(index - _length.ValueInt, index, typeClose) - candles.Summ(index - _length.ValueInt, index, typeOpen);

            _series.Values[index] = temp / _length.ValueInt;
        }
    }
}