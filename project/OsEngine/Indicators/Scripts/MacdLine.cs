using System;
using System.Collections.Generic;
using System.Drawing;
using OsEngine.Entity;
using OsEngine.Language;

namespace OsEngine.Indicators
{
    [Indicator("MacdLine")]
    public class MacdLine : Aindicator
    {
        public override string Description
        {
            get
            {
                string eng = "MacdLine calculates the classic MACD line as the difference between fast and slow EMAs and complements it with a signal line and histogram, visually displaying momentum and trend direction. " +
                             "Traders use the MACD line crossing the signal line for trade entries and changes in the histogram sign to confirm the strength of a move.";

                string ru = "MacdLine рассчитывает классическую линию MACD как разницу между быстрой и медленной EMA и дополняет её сигнальной линией и гистограммой, визуально отображая импульс и направление тренда. " +
                            "Трейдеры используют пересечение линии MACD со сигнальной для входов в сделки и изменение знака гистограммы для подтверждения силы движения.";

                return OsLocalization.ConvertToLocString($"Eng:{eng}_Ru:{ru}_");
            }
        }

        private IndicatorParameterInt _lengthFastLine;

        private IndicatorParameterInt _lengthSlowLine;

        private IndicatorParameterInt _lengthSignalLine;

        private IndicatorDataSeries _seriesMacd;

        private IndicatorDataSeries _seriesSignalLine;

        private IndicatorDataSeries _seriesMacdHistogram;

        private Aindicator _emaSlow;

        private Aindicator _emaFast;

        public override void OnStateChange(IndicatorState state)
        {
            if (state == IndicatorState.Configure)
            {
                _lengthFastLine = CreateParameterInt("Fast line length", 12);
                _lengthSlowLine = CreateParameterInt("Slow line length", 26);
                _lengthSignalLine = CreateParameterInt("Signal line length", 9);

                _seriesMacd = CreateSeries("MACD", Color.DarkGreen, IndicatorChartPaintType.Line, true);
                _seriesSignalLine = CreateSeries("Signal line", Color.DarkRed, IndicatorChartPaintType.Line, true);
                _seriesMacdHistogram = CreateSeries("MACD Histogram", Color.DodgerBlue, IndicatorChartPaintType.Column, false);

                _emaFast = IndicatorsFactory.CreateIndicatorByName("Ema", Name + "Ema fast", false);
                _emaFast.Parameters[0].Bind(_lengthFastLine);
                ProcessIndicator("Ema fast", _emaFast);

                _emaSlow = IndicatorsFactory.CreateIndicatorByName("Ema", Name + "Ema slow", false);
                _emaSlow.Parameters[0].Bind(_lengthSlowLine);
                ProcessIndicator("Ema slow", _emaSlow);
            }
        }

        public override void OnProcess(List<Candle> candles, int index)
        {
            if (index < _lengthFastLine.ValueInt ||
                index < _lengthSlowLine.ValueInt)
            {
                return;
            }

            _seriesMacd.Values[index] = _emaFast.DataSeries[0].Values[index] - _emaSlow.DataSeries[0].Values[index];

            ProcessSignalLine(_seriesMacd.Values, index);

            _seriesMacdHistogram.Values[index] = _seriesMacd.Values[index] - _seriesSignalLine.Values[index];
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