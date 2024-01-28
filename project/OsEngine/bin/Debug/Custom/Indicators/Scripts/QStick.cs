using OsEngine.Entity;
using System;
using System.Collections.Generic;
using System.Drawing;
using OsEngine.Indicators;


namespace OsEngine.Charts.CandleChart.Indicators.Indicator
{
   // [IndicatorAttribute("QStick")]
    internal class QStick : Aindicator
    {
        private IndicatorParameterInt _lenght;

        private IndicatorParameterString _typeMA;

        private IndicatorDataSeries _series;

        public override void OnStateChange(IndicatorState state)
        {
            List<string> typeMA = new List<string> { "SMA", "EMA" };

            _lenght = CreateParameterInt("Length", 14);
            _typeMA = CreateParameterStringCollection("Type MA", "SMA", typeMA);
            _series = CreateSeries("QStick", Color.Red, IndicatorChartPaintType.Line, true);
        }
        /// <summary>
        /// Calculate the MAMA value
        /// </summary>
        /// <param name="candles">collection candles</param>
        /// <param name="index">index to use in the collection of candles</param>
        public override void OnProcess(List<Candle> candles, int index)
        {
            if (_typeMA.ValueString == "SMA")
                CaclQstickForSMA(candles, index);
            else
                CalcQstickForEMA(candles, index);
        }
        /// <summary>
        /// Calculate the Qstick For EMA value
        /// </summary>
        /// <param name="candles">collection candles</param>
        /// <param name="index">index to use in the collection of candles</param>
        private void CalcQstickForEMA(List<Candle> candles, int index)
        {
            decimal result = 0;

            if (index == _lenght.ValueInt)
            {
                decimal lastMoving = 0;

                for (int i = index - _lenght.ValueInt + 1; i < index + 1; i++)
                {
                    lastMoving += candles[i].Close - candles[i].Open;
                }
                lastMoving = lastMoving / _lenght.ValueInt;
                result = lastMoving;
            }
            else if (index > _lenght.ValueInt)
            {
                decimal a = Math.Round(2.0m / (_lenght.ValueInt + 1), 8);
                decimal emaLast = _series.Values[index - 1];
                decimal p = candles[index].Close - candles[index].Open;
                result = emaLast + (a * (p - emaLast));
            }
            _series.Values[index] = Math.Round(result, 8);
        }
        /// <summary>
        /// Calculate the Qstick For Sma value 
        /// </summary>
        /// <param name="candles">collection candles</param>
        /// <param name="index">index to use in the collection of candles</param>
        public void CaclQstickForSMA(List<Candle> candles, int index)
        {
            if (_lenght.ValueInt > index)
            {
                _series.Values[index] = 0;
                return;
            }
            string typeClose = "Close";
            string typeOpen = "Open";

            decimal temp = candles.Summ(index - _lenght.ValueInt, index, typeClose) - candles.Summ(index - _lenght.ValueInt, index, typeOpen);

            _series.Values[index] = temp / _lenght.ValueInt;
        }
    }
}
