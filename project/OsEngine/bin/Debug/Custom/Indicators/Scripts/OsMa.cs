using OsEngine.Entity;
using System;
using System.Collections.Generic;
using System.Drawing;

namespace OsEngine.Indicators
{
    [Indicator("OsMa")]
    public class OsMa : Aindicator
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
        private IndicatorParameterInt _lengthSignalLine;
        /// <summary>
        /// Long line length
        /// </summary>
        private IndicatorParameterInt _lengthSlowLine;
        /// <summary>
        /// Short line length
        /// </summary>
        private IndicatorParameterInt _lengthFastLine;

        private Aindicator _MACD;
        public override void OnStateChange(IndicatorState state)
        {
            if (state == IndicatorState.Configure)
            {
                _lengthFastLine = CreateParameterInt("Fast line length", 12);
                _lengthSlowLine = CreateParameterInt("Slow line length", 26);
                _lengthSignalLine = CreateParameterInt("Signal line length", 9);

                _seriesOsMa = CreateSeries("OsMa", Color.Yellow, IndicatorChartPaintType.Line, true);
                _seriesSignalLine = CreateSeries("Signal Line", Color.Red, IndicatorChartPaintType.Line, true);
                _seriesHistogramm = CreateSeries("Histogramm", Color.DodgerBlue, IndicatorChartPaintType.Column, true);

                _MACD = IndicatorsFactory.CreateIndicatorByName("MACD", Name + "MACD", false);
                ((IndicatorParameterInt)_MACD.Parameters[0]).Bind(_lengthFastLine);
                ((IndicatorParameterInt)_MACD.Parameters[1]).Bind(_lengthSlowLine);
                ((IndicatorParameterInt)_MACD.Parameters[2]).Bind(_lengthSignalLine);
                ProcessIndicator("MACD", _MACD);
            }
        }
        public override void OnProcess(List<Candle> candles, int index)
        {
            if (index < _lengthFastLine.ValueInt || index < _lengthSlowLine.ValueInt || index < _lengthSignalLine.ValueInt)
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

            if (index == _lengthSignalLine.ValueInt)
            {
                decimal lastMoving = 0;

                for (int i = index - _lengthSignalLine.ValueInt + 1; i < index + 1; i++)
                {
                    lastMoving += values[i];
                }

                lastMoving = lastMoving / _lengthSignalLine.ValueInt;
                result = lastMoving;
            }
            else if (index > _lengthSignalLine.ValueInt)
            {
                decimal a = Math.Round(2.0m / (_lengthSignalLine.ValueInt + 1), 8);
                decimal emaLast = _seriesSignalLine.Values[index - 1];
                decimal p = values[index];
                result = emaLast + (a * (p - emaLast));
            }

            _seriesSignalLine.Values[index] = Math.Round(result, 8);
        }
    }
}
