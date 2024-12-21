using OsEngine.Entity;
using System;
using System.Collections.Generic;
using System.Drawing;

namespace OsEngine.Indicators
{
    [Indicator("Aroon")]
    public class Aroon : Aindicator
    {
        public IndicatorParameterInt _lengthPeriod;

        public IndicatorParameterInt UpHorizontalLineLevel;

        public IndicatorParameterInt DownHorizontalLineLevel;

        public IndicatorDataSeries _seriesUp;

        public IndicatorDataSeries _seriesDown;

        public IndicatorDataSeries _seriesOsc;

        public IndicatorDataSeries _seriesHorizontalUpLine;

        public IndicatorDataSeries _seriesHorizontalDownLine;

        public override void OnStateChange(IndicatorState state)
        {
            if (state == IndicatorState.Configure)
            {
                _lengthPeriod = CreateParameterInt("Period", 14);

                UpHorizontalLineLevel = CreateParameterInt("Up Horizontal Line", 70);
                DownHorizontalLineLevel = CreateParameterInt("Down Horizontal Line", 30);

                _seriesUp = CreateSeries("Series Up", Color.Green, IndicatorChartPaintType.Line, true);
                _seriesDown = CreateSeries("Series Down", Color.Red, IndicatorChartPaintType.Line, true);

                _seriesOsc = CreateSeries("series Oscillator", Color.Red, IndicatorChartPaintType.Line, false);

                _seriesHorizontalUpLine = CreateSeries("Series Horizontal Up Line", Color.DarkGray, IndicatorChartPaintType.Line, true);
                _seriesHorizontalDownLine = CreateSeries("Series Horizontal Down Line", Color.DarkGray, IndicatorChartPaintType.Line, true);

                _seriesHorizontalUpLine.CanReBuildHistoricalValues = true;
                _seriesHorizontalDownLine.CanReBuildHistoricalValues = true;
            }
        }

        public override void OnProcess(List<Candle> candles, int index)
        {
            if (_lengthPeriod.ValueInt >= index)
            {
                return;
            }
            CalcAroon(candles, index);

            _seriesUp.Values[index] = AroonUp;
            _seriesDown.Values[index] = AroonDown;
            _seriesOsc.Values[index] = AroonOsc;

            _seriesHorizontalUpLine.Values[index] = UpHorizontalLineLevel.ValueInt;
            _seriesHorizontalDownLine.Values[index] = DownHorizontalLineLevel.ValueInt;
        }

        decimal AroonUp;

        decimal AroonDown;

        decimal AroonOsc;

        public void CalcAroon(List<Candle> candles, int index)
        {
            int numBarUp = 0;
            int numBarDown = 0;

            decimal lastPrice = candles[index].Close;

            decimal max = decimal.MinValue;
            decimal min = decimal.MaxValue;

            //if the last price did NOT update hi or low, then the series will be equal to "Aroon = 100 * (n - the number of candles from the last max / min) / n".
            for (int i = index - _lengthPeriod.ValueInt; i < index; i++)
            {
                if (max < candles[i].Close)
                {
                    max = candles[i].Close;
                    numBarUp = index - i;
                    AroonUp = 100 * (_lengthPeriod.ValueInt - numBarUp) / _lengthPeriod.ValueInt;
                }

                if (min > candles[i].Close)
                {
                    min = candles[i].Close;
                    numBarDown = index - i;
                    AroonDown = 100 * (_lengthPeriod.ValueInt - numBarDown) / _lengthPeriod.ValueInt;
                }
            }

            // if the last price updated hi or low, the series will be equal to 100.
            if (max < lastPrice)
            {
                AroonUp = 100;
            }
            if (min > lastPrice)
            {
                AroonDown = 100;
            }
            AroonOsc = Math.Abs(AroonUp - AroonDown);
        }
    }
}