using OsEngine.Entity;
using OsEngine.Indicators;
using System;
using System.Collections.Generic;
using System.Drawing;

namespace OsEngine.Charts.CandleChart.Indicators.Indicator
{
   // [IndicatorAttribute("RAVI")]
    internal class RAVI : Aindicator
    {
        /// <summary>
        ///Slow sma period
        /// </summary>
        private IndicatorParameterInt _lenghtSlow;
        /// <summary>
        /// fast sma period
        /// </summary>
        private IndicatorParameterInt _lenghtFast;
        /// <summary>
        /// candlestick closing price type
        /// </summary>
        private IndicatorParameterString _candlePoint;
        /// <summary>
        ///Slow sma period
        /// </summary>
        private IndicatorParameterDecimal _UpLineParam;
        /// <summary>
        ///Slow sma period
        /// </summary>
        private IndicatorParameterDecimal _DownLineParam;
        /// <summary>
        /// data series indicator
        /// </summary>
        private IndicatorDataSeries _seriesSma;
        /// <summary>
        /// data series indicator
        /// </summary>
        private IndicatorDataSeries _seriesUp;
        /// <summary>
        /// data series indicator
        /// </summary>
        private IndicatorDataSeries _seriesDown;

        public override void OnStateChange(IndicatorState state)
        {
            if (state == IndicatorState.Configure)
            {
                _lenghtSlow = CreateParameterInt("Slow line length", 65);
                _lenghtFast = CreateParameterInt("Fast line length", 7);

                _UpLineParam = CreateParameterDecimal("Up line", 3m);
                _DownLineParam = CreateParameterDecimal("Down line", -3m);

                _candlePoint = CreateParameterStringCollection("Candle Point", "Close", OsEngine.Indicators.Entity.CandlePointsArray);

                _seriesSma = CreateSeries("EMA", Color.DarkRed, IndicatorChartPaintType.Line, true);

                _seriesUp = CreateSeries("Up Line", Color.LightYellow, IndicatorChartPaintType.Line, true);
                _seriesDown = CreateSeries("Down Line", Color.LightYellow, IndicatorChartPaintType.Line, true);

            }
        }
        /// <summary>
        /// an iterator method to fill the indicator 
        /// </summary>
        /// <param name="candles">collection candles</param>
        /// <param name="index">index to use in the collection of candles</param>
        public override void OnProcess(List<Candle> candles, int index)
        {
            _seriesUp.Values[index] = _UpLineParam.ValueDecimal;
            _seriesDown.Values[index] = _DownLineParam.ValueDecimal;

            if (_lenghtFast.ValueInt + 3 > index || _lenghtSlow.ValueInt + 3 > index)
            {
                _seriesSma.Values[index] = 0;

                return;
            }

            decimal smaSlow = CalcSma(candles, index, _lenghtSlow.ValueInt);
            decimal smaFast = CalcSma(candles, index, _lenghtFast.ValueInt);

            _seriesSma.Values[index] = Math.Round((smaFast - smaSlow) / smaSlow * 100, 5);

        }

        /// <summary>
        /// Moving Average Calculation
        /// </summary>
        /// <param name="candles">candle collection</param>
        /// <param name="index">candlestick index</param>
        /// <param name="_lenght">the period for which Sma is calculated</param>
        /// <returns></returns>
        public decimal CalcSma(List<Candle> candles, int index, int _lenght)
        {
            decimal sma = 0;

            for (int i = index - _lenght; i < index; i++)
            {
                sma += candles[i].GetPoint(_candlePoint.ValueString);
            }
            decimal result = sma / _lenght;

            return result;
        }
    }
}
