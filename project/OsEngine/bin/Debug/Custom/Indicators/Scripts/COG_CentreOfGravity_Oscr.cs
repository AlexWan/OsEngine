using System;
using System.Collections.Generic;
using System.Drawing;
using OsEngine.Entity;
using OsEngine.Indicators;

namespace OsEngine.Charts.CandleChart.Indicators.Indicator
{
   // [IndicatorAttribute("COG_CentreOfGravity_Oscr")]
    internal class COG_CentreOfGravity_Oscr : Aindicator
    {
        ///period for which the indicator is calculated
        /// </summary>
        private IndicatorParameterInt _lenght;
        /// <summary>
        /// indicator data series
        /// </summary>
        private IndicatorDataSeries _LastCOG_series;
        /// <summary>
        /// indicator data series
        /// </summary>
        private IndicatorDataSeries PrevCOG_series;
        /// <summary>
        /// Type close price
        /// </summary>
        private IndicatorParameterString _candlePoint;

        public override void OnStateChange(IndicatorState state)
        {
            if (state == IndicatorState.Configure)
            {
                _lenght = CreateParameterInt("Length", 14);
                _candlePoint = CreateParameterStringCollection("Candle Point", "Close", OsEngine.Indicators.Entity.CandlePointsArray);

                _LastCOG_series = CreateSeries("COG series", Color.Red, IndicatorChartPaintType.Line, true);
                PrevCOG_series = CreateSeries("Prev COG", Color.Aqua, IndicatorChartPaintType.Line, true);
            }
        }
        /// <summary>
        /// an iterator method to fill the indicator 
        /// </summary>
        /// <param name="candles">collection candles</param>
        /// <param name="index">index to use in the collection of candles</param>
        public override void OnProcess(List<Candle> candles, int index)
        {
            if (index < _lenght.ValueInt)
                return;

            decimal COG = cacl(candles, index); ;
            decimal PrevCOG = _LastCOG_series.Values[index - 1];

            _LastCOG_series.Values[index] = Math.Round(COG, 3);
            PrevCOG_series.Values[index] = Math.Round(PrevCOG, 3);
        }
        /// <summary>
        /// COG calculation
        /// </summary>
        /// <param name="candles">collection candles</param>
        /// <param name="index">index to use in the collection of candles</param>        
        public decimal cacl(List<Candle> candles, int index)
        {
            decimal result = 0;
            decimal temp = 0;
            decimal temp2 = 0;

            for (int i = index - _lenght.ValueInt, j = 0; i < index; i++, j++)
            {
                temp += candles[i].GetPoint(_candlePoint.ValueString) * (j + 1);
                temp2 += candles[i].GetPoint(_candlePoint.ValueString);
            }

            result = temp / temp2 - (_lenght.ValueInt + 1) / 2;

            return result;
        }
    }
}

