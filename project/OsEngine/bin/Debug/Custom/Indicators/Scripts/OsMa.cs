using OsEngine.Entity;
using OsEngine.Indicators;
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
        private IndicatorDataSeries _series;
        /// <summary>
        /// indicator data series
        /// </summary>
        private IndicatorDataSeries _seriesMACD;
        /// <summary>
        /// indicator data series
        /// </summary>
        private IndicatorDataSeries _seriesMACD_Signal;
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
                _series = CreateSeries("OsMa", Color.DodgerBlue, IndicatorChartPaintType.Column, true);
                _seriesMACD = CreateSeries("MACD Line", Color.Yellow, IndicatorChartPaintType.Line, true);
                _seriesMACD_Signal = CreateSeries("MACD Signal Line", Color.Red, IndicatorChartPaintType.Line, true);

                _lenghtFastLine = CreateParameterInt("Fast line length", 12);
                _lenghtSlowLine = CreateParameterInt("Slow line length", 26);
                _lenghtSignalLine = CreateParameterInt("Signal line length", 9);

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
                return;
            decimal macd = _MACD.DataSeries[1].Values[index];
            decimal macdSignal = _MACD.DataSeries[2].Values[index];
            decimal OsMa = macd - macdSignal;

            _seriesMACD.Values[index] = macd;
            _seriesMACD_Signal.Values[index] = macdSignal;
            _series.Values[index] = OsMa;
        }
    }
}
