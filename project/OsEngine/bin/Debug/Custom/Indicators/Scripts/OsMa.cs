using OsEngine.Entity;
using OsEngine.Indicators;
using System;
using System.Collections.Generic;
using System.Drawing;

namespace OsEngine.Charts.CandleChart.Indicators.Indicator
{
    //[IndicatorAttribute("OsMa")]
    internal class OsMa : Aindicator
    {
        /// <summary>
        /// indicator data series
        /// </summary>
        private IndicatorDataSeries _seriesOsMa;
        /// <summary>
        /// indicator data series
        /// </summary>
        private IndicatorDataSeries _seriesSignalLine;
        /// <summary>
        /// indicator data series
        /// </summary>
        private IndicatorDataSeries _seriesHistogramm;
        /// <summary>
        /// signal line length
        /// </summary>
        private IndicatorParameterInt _lenghtSignalLine;
        /// <summary>
        /// Long line length
        /// </summary>
        private IndicatorParameterInt _lenghtSlowLine;
        /// <summary>
        /// Short line length
        /// </summary>
        private IndicatorParameterInt _lenghtFastLine;

        private Aindicator _MACD;
        public override void OnStateChange(IndicatorState state)
        {
            if (state == IndicatorState.Configure)
            {
                _lenghtFastLine = CreateParameterInt("Fast line length", 12);
                _lenghtSlowLine = CreateParameterInt("Slow line length", 26);
                _lenghtSignalLine = CreateParameterInt("Signal line length", 9);

                _seriesOsMa = CreateSeries("OsMa", Color.Yellow, IndicatorChartPaintType.Line, true);
                _seriesSignalLine = CreateSeries("Signal Line", Color.Red, IndicatorChartPaintType.Line, true);
                _seriesHistogramm = CreateSeries("Histogramm", Color.DodgerBlue, IndicatorChartPaintType.Column, true);

                _MACD = IndicatorsFactory.CreateIndicatorByName("MACD", Name + "MACD", false);
                ((IndicatorParameterInt)_MACD.Parameters[0]).Bind(_lenghtFastLine);
                ((IndicatorParameterInt)_MACD.Parameters[1]).Bind(_lenghtSlowLine);
                ((IndicatorParameterInt)_MACD.Parameters[2]).Bind(_lenghtSignalLine);
                ProcessIndicator("MACD", _MACD);
            }
        }
        public override void OnProcess(List<Candle> candles, int index)
        {
            if (index < _lenghtFastLine.ValueInt || index < _lenghtSlowLine.ValueInt || index < _lenghtSignalLine.ValueInt)
            {
                return;
            }
                           
            decimal macd = _MACD.DataSeries[1].Values[index];
            decimal macdSignal = _MACD.DataSeries[2].Values[index];
            decimal OsMa = macd - macdSignal;

            _seriesOsMa.Values[index] = OsMa;

            ProcessSignalLine(_seriesOsMa.Values, index);

            _seriesHistogramm.Values[index] = _seriesOsMa.Values[index] - _seriesSignalLine.Values[index];
        }

        private void ProcessSignalLine(List<decimal> values, int index)
        {
            decimal result = 0;

            if (index == _lenghtSignalLine.ValueInt)
            {
                decimal lastMoving = 0;

                for (int i = index - _lenghtSignalLine.ValueInt + 1; i < index + 1; i++)
                {
                    lastMoving += values[i];
                }

                lastMoving = lastMoving / _lenghtSignalLine.ValueInt;
                result = lastMoving;
            }
            else if (index > _lenghtSignalLine.ValueInt)
            {
                decimal a = Math.Round(2.0m / (_lenghtSignalLine.ValueInt + 1), 8);
                decimal emaLast = _seriesSignalLine.Values[index - 1];
                decimal p = values[index];
                result = emaLast + (a * (p - emaLast));
            }

            _seriesSignalLine.Values[index] = Math.Round(result, 8);
        }
    }
}
