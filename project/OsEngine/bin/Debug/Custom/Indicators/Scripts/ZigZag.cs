using System;
using System.Collections.Generic;
using System.Linq;
using System.Drawing;
using OsEngine.Entity;

namespace OsEngine.Indicators
{
    [Indicator("ZigZag")]
    public class ZigZag : Aindicator
    {
        private IndicatorParameterInt _period;

        private IndicatorDataSeries _seriesZigZag;

        private IndicatorDataSeries _seriesToLine;

        private IndicatorDataSeries _seriesZigZagHighs;

        private IndicatorDataSeries _seriesZigZagLows;

        public override void OnStateChange(IndicatorState state)
        {
            if(state == IndicatorState.Configure)
            {
                _period = CreateParameterInt("Length", 14);
                _seriesZigZag = CreateSeries("ZigZag", Color.CornflowerBlue, IndicatorChartPaintType.Point, false);
                _seriesZigZag.CanReBuildHistoricalValues = true;

                _seriesToLine = CreateSeries("ZigZagLine", Color.CornflowerBlue, IndicatorChartPaintType.Point, true);
                _seriesToLine.CanReBuildHistoricalValues = true;

                _seriesZigZagHighs = CreateSeries("_seriesZigZagHighs", Color.GreenYellow, IndicatorChartPaintType.Point, false);
                _seriesZigZagHighs.CanReBuildHistoricalValues = true;

                _seriesZigZagLows = CreateSeries("_seriesZigZagLows", Color.Red, IndicatorChartPaintType.Point, false);
                _seriesZigZagLows.CanReBuildHistoricalValues = true;
            }
        }

        private decimal _currentZigZagHigh = 0;

        private decimal _currentZigZagLow = 0;

        private int _lastSwingIndex = -1;

        private decimal _lastSwingPrice = 0;

        private int _trendDir = 0;

        public override void OnProcess(List<Candle> candles, int index)
        {
            if (index < _period.ValueInt * 2)
            {
                _currentZigZagHigh = 0;
                _currentZigZagLow = 0;
                _lastSwingIndex = -1;
                _lastSwingPrice = 0;
                _trendDir = 0;
                return;
            }

            decimal High = candles[index].High;
            decimal Low = candles[index].Low;

            if (_lastSwingPrice == 0)
                _lastSwingPrice = Low + (High - Low) / 2;

            bool isSwingHigh = High == GetExtremum(candles, _period.ValueInt, "High", index);
            bool isSwingLow = Low == GetExtremum(candles, _period.ValueInt, "Low", index);
            decimal saveValue = 0;
            bool addHigh = false;
            bool addLow = false;
            bool updateHigh = false;
            bool updateLow = false;

            if (!isSwingHigh && !isSwingLow)
            {
                return;
            }

            if (_trendDir == 1 && isSwingHigh && High >= _lastSwingPrice)
            {
                saveValue = High;
                updateHigh = true;
            }
            else if (_trendDir == -1 && isSwingLow && Low <= _lastSwingPrice)
            {
                saveValue = Low;
                updateLow = true;
            }
            else if (_trendDir <= 0 && isSwingHigh)
            {
                saveValue = High;
                addHigh = true;
                _trendDir = 1;
            }
            else if (_trendDir >= 0 && isSwingLow)
            {
                saveValue = Low;
                addLow = true;
                _trendDir = -1;
            }

            if (addHigh || addLow || updateHigh || updateLow)
            {
                if (updateHigh && _lastSwingIndex >= 0)
                {
                    _seriesZigZag.Values[_lastSwingIndex] = 0; // тут в оригинале double.NaN
                    _seriesZigZagHighs.Values[_lastSwingIndex] = 0;
                }
                else if (updateLow && _lastSwingIndex >= 0)
                {
                    _seriesZigZag.Values[_lastSwingIndex] = 0; // тут в оригинале double.NaN
                    _seriesZigZagLows.Values[_lastSwingIndex] = 0;
                }

                if (addHigh || updateHigh)
                {
                    _currentZigZagHigh = saveValue;
                    _seriesZigZag.Values[index] = _currentZigZagHigh;
                    _seriesZigZagHighs.Values[index] = _currentZigZagHigh;

                }
                else if (addLow || updateLow)
                {
                    _currentZigZagLow = saveValue;
                    _seriesZigZag.Values[index] = _currentZigZagLow;
                    _seriesZigZagLows.Values[index] = _currentZigZagLow;

                }

                _lastSwingIndex = index;
                _lastSwingPrice = saveValue;

                if (updateHigh || updateLow)
                {
                    ReBuildLine(_seriesZigZag.Values, _seriesToLine.Values);
                }
            }
        }

        private decimal GetExtremum(List<Candle> candles, int period, string points, int index)
        {
            try
            {
                List<decimal> values = new List<decimal>();
                for (int i = index; i >= index - period; i--)
                    values.Add(candles[i].GetPoint(points));

                if (points == "High")
                    return values.Max();
                if (points == "Low")
                    return values.Min();

            }
            catch (Exception e)
            {

            }

            return 0;
        }

        private void ReBuildLine(List<decimal> zigZag, List<decimal> line)
        {
            decimal curPoint = 0;
            int lastPointIndex = 0;

            for (int i = 0; i < zigZag.Count; i++)
            {
                if (zigZag[i] == 0)
                {
                    continue;
                }

                if (curPoint == 0)
                {
                    curPoint = zigZag[i];
                    lastPointIndex = i;
                    continue;
                }

                decimal mult = Math.Abs(curPoint - zigZag[i]) / (i - lastPointIndex);

                if (zigZag[i] < curPoint)
                {
                    mult = mult * -1;
                }

                decimal curValue = curPoint;

                for (int i2 = lastPointIndex; i2 < i; i2++)
                {
                    line[i2] = curValue;
                    curValue += mult;
                }

                curPoint = zigZag[i];
                lastPointIndex = i;
            }
        }
    }
}