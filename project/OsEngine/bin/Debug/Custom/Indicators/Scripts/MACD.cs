using System;
using System.Collections.Generic;
using System.Drawing;
using OsEngine.Entity;
using OsEngine.Indicators;

namespace CustomIndicators.Scripts
{
    public class MACD : Aindicator
    {
        private IndicatorParameterInt _lenghtFastLine;
        private IndicatorParameterInt _lenghtSlowLine;
        private IndicatorParameterInt _lenghtSignalLine;

        private IndicatorDataSeries _seriesMacd;
        private IndicatorDataSeries _seriesSignalLine;
        private IndicatorDataSeries _seriesMacdHistogramm;

        private Aindicator _emaSlow;
        private Aindicator _emaFast;

        public override void OnStateChange(IndicatorState state)
        {
            if (state == IndicatorState.Configure)
            {
                _lenghtFastLine = CreateParameterInt("Fast line length", 12);
                _lenghtSlowLine = CreateParameterInt("Slow line length", 26);
                _lenghtSignalLine = CreateParameterInt("Signal line length", 9);

                _seriesMacdHistogramm = CreateSeries("MACD Histogramm", Color.DodgerBlue, IndicatorChartPaintType.Column, true);
                _seriesMacd = CreateSeries("MACD", Color.DarkGreen, IndicatorChartPaintType.Line, false);
                _seriesSignalLine = CreateSeries("Signal line", Color.DarkRed, IndicatorChartPaintType.Line, true);

                _emaFast = IndicatorsFactory.CreateIndicatorByName("Ema", Name + "Ema fast", false);
                ((IndicatorParameterInt)_emaFast.Parameters[0]).Bind(_lenghtFastLine);
                ProcessIndicator("Ema fast", _emaFast);

                _emaSlow = IndicatorsFactory.CreateIndicatorByName("Ema", Name + "Ema slow", false);
                ((IndicatorParameterInt)_emaSlow.Parameters[0]).Bind(_lenghtSlowLine);
                ProcessIndicator("Ema slow", _emaSlow);
            }
        }

        public override void OnProcess(List<Candle> candles, int index)
        {
            if (index < _lenghtFastLine.ValueInt ||
                index < _lenghtSlowLine.ValueInt)
            {
                return;
            }

            _seriesMacd.Values[index] = _emaFast.DataSeries[0].Values[index] - _emaSlow.DataSeries[0].Values[index];

            ProcessSignalLine(_seriesMacd.Values, index);

            _seriesMacdHistogramm.Values[index] = _seriesMacd.Values[index] - _seriesSignalLine.Values[index];
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