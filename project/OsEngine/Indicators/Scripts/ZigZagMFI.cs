using System;
using System.Collections.Generic;
using System.Linq;
using System.Drawing;
using OsEngine.Entity;

namespace OsEngine.Indicators
{
    [Indicator("ZigZagMFI")]
    public class ZigZagMFI : Aindicator
    {
        private Aindicator _MFI;

        private IndicatorDataSeries _seriesMFI;

        private IndicatorParameterInt _lengthMFI;

        private IndicatorParameterInt _lengthZigZag;

        private IndicatorDataSeries _seriesZigZag;

        private IndicatorDataSeries _seriesToLine;

        private IndicatorDataSeries _seriesZigZagHighs;

        private IndicatorDataSeries _seriesZigZagLows;

        private IndicatorDataSeries _seriesZigZagUpChannel;

        private IndicatorDataSeries _seriesZigZagDownChannel;

        public override void OnStateChange(IndicatorState state)
        {
            if (state == IndicatorState.Configure)
            {
                _lengthMFI = CreateParameterInt("Length", 3);
                _lengthZigZag = CreateParameterInt("Length ZigZag", 14);

                _seriesMFI = CreateSeries("MFI", Color.Aqua, IndicatorChartPaintType.Line, true);

                _seriesZigZag = CreateSeries("ZigZag", Color.CornflowerBlue, IndicatorChartPaintType.Point, false);
                _seriesZigZag.CanReBuildHistoricalValues = true;

                _seriesToLine = CreateSeries("ZigZagLine", Color.CornflowerBlue, IndicatorChartPaintType.Point, true);
                _seriesToLine.CanReBuildHistoricalValues = true;

                _seriesZigZagHighs = CreateSeries("_seriesZigZagHighs", Color.GreenYellow, IndicatorChartPaintType.Point, false);
                _seriesZigZagHighs.CanReBuildHistoricalValues = true;

                _seriesZigZagLows = CreateSeries("_seriesZigZagLows", Color.Red, IndicatorChartPaintType.Point, false);
                _seriesZigZagLows.CanReBuildHistoricalValues = true;

                _seriesZigZagUpChannel = CreateSeries("_seriesZigZagUpChannel", Color.DarkRed, IndicatorChartPaintType.Point, true);
                _seriesZigZagUpChannel.CanReBuildHistoricalValues = true;

                _seriesZigZagDownChannel = CreateSeries("_seriesZigZagDownChannel", Color.DarkGreen, IndicatorChartPaintType.Point, true);
                _seriesZigZagDownChannel.CanReBuildHistoricalValues = true;

                _MFI = IndicatorsFactory.CreateIndicatorByName("MFI", Name + "MFI", false);
                ((IndicatorParameterInt)_MFI.Parameters[0]).Bind(_lengthMFI);
                ProcessIndicator("MFI", _MFI);

            }
        }

        public override void OnProcess(List<Candle> candles, int index)
        {
            _seriesMFI.Values[index] = _MFI.DataSeries[0].Values[index];

            List<decimal> values = _MFI.DataSeries[0].Values;

            if (index < _lengthZigZag.ValueInt * 2)
            {
                _currentZigZagHigh = 0;
                _currentZigZagLow = 0;
                _lastSwingIndex = -1;
                _lastSwingPrice = 0;
                _trendDir = 0;
                return;
            }

            List<decimal> valuesMFI = new List<decimal>();

            for (int i = 0; i < index + 1; i++)
            {
                valuesMFI.Add(values[i]);
            }

            decimal High = 0;
            decimal Low = 0;

            High = valuesMFI[valuesMFI.Count - 1];
            Low = valuesMFI[valuesMFI.Count - 1];


            if (_lastSwingPrice == 0)
                _lastSwingPrice = Low + (High - Low) / 2;

            bool isSwingHigh = High == GetExtremum(values, _lengthZigZag.ValueInt, "High", index);
            bool isSwingLow = Low == GetExtremum(values, _lengthZigZag.ValueInt, "Low", index);
            decimal saveValue = 0;
            bool addHigh = false;
            bool addLow = false;
            bool updateHigh = false;
            bool updateLow = false;

            if (!isSwingHigh && !isSwingLow)
            {
                ReBuildChannel(_seriesZigZagUpChannel, _seriesZigZagDownChannel, _seriesZigZagHighs.Values, _seriesZigZagLows.Values, index);
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
                    _seriesZigZag.Values[_lastSwingIndex] = 0;
                    _seriesZigZagHighs.Values[_lastSwingIndex] = 0;
                }
                else if (updateLow && _lastSwingIndex >= 0)
                {
                    _seriesZigZag.Values[_lastSwingIndex] = 0;
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
            ReBuildChannel(_seriesZigZagUpChannel, _seriesZigZagDownChannel, _seriesZigZagHighs.Values, _seriesZigZagLows.Values, index);
        }

        private decimal _currentZigZagHigh = 0;

        private decimal _currentZigZagLow = 0;

        private int _lastSwingIndex = -1;

        private decimal _lastSwingPrice = 0;

        private int _trendDir = 0;

        private decimal GetExtremum(List<decimal> values, int period, string points, int index)
        {
            try
            {
                List<decimal> values1 = new List<decimal>();
                for (int i = index; i >= index - period; i--)
                    values1.Add(values[i]);

                if (points == "High")
                    return values1.Max();
                if (points == "Low")
                    return values1.Min();

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

        private void ReBuildChannel(IndicatorDataSeries _seriesZigZagUpChannel, IndicatorDataSeries _seriesZigZagDownChannel,
       List<decimal> _seriesZigZagHighs, List<decimal> _seriesZigZagLows, int index)
        {

            List<int> _ZigZagHighs = new List<int>();

            for (int i = index; i >= 0; i--)
            {
                if (_ZigZagHighs.Count == 3) { break; }
                if (_seriesZigZagHighs[i] == 0) { continue; }
                _ZigZagHighs.Add(i);
            }


            List<int> _ZigZagLows = new List<int>();

            for (int i = index; i >= 0; i--)
            {
                if (_ZigZagLows.Count == 3) { break; }
                if (_seriesZigZagLows[i] == 0) { continue; }
                _ZigZagLows.Add(i);
            }
            if (_ZigZagHighs.Count < 3 || _ZigZagLows.Count < 3) { return; }

            if (_ZigZagHighs[0] > _ZigZagLows[0])
            {

                RedRawVectorLine(_seriesZigZagUpChannel, _seriesZigZagHighs, _ZigZagHighs[2], _ZigZagHighs[1], index);

                RedRawVectorLine(_seriesZigZagDownChannel, _seriesZigZagLows, _ZigZagLows[1], _ZigZagLows[0], index);
            }
            else
            {
                RedRawVectorLine(_seriesZigZagUpChannel, _seriesZigZagHighs, _ZigZagHighs[1], _ZigZagHighs[0], index);

                RedRawVectorLine(_seriesZigZagDownChannel, _seriesZigZagLows, _ZigZagLows[2], _ZigZagLows[1], index);
            }
        }

        private void RedRawVectorLine(IndicatorDataSeries _targetSeries, List<decimal> _sourceSeries, int _startVectorIndex, int _directionVectorIndex, int _endPointIndex)
        {
            decimal _increment = (_sourceSeries[_directionVectorIndex] - _sourceSeries[_startVectorIndex]) / (_directionVectorIndex - _startVectorIndex);
            _targetSeries.Values[_startVectorIndex] = _sourceSeries[_startVectorIndex];
            for (int i = _startVectorIndex + 1; i <= _endPointIndex; i++)
            {
                _targetSeries.Values[i] = _targetSeries.Values[i - 1] + _increment;
            }
        }
    }
}