﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Drawing;
using OsEngine.Entity;

namespace OsEngine.Indicators.My_ind
{
    internal class ZigZagSMI : Aindicator
    {
        private Aindicator _SMI;
        private IndicatorParameterInt StochasticPeriod1;
        private IndicatorParameterInt StochasticPeriod2;
        private IndicatorParameterInt StochasticPeriod3;
        private IndicatorParameterInt StochasticPeriod4;

        private IndicatorDataSeries _seriesSMI;

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

                StochasticPeriod1 = CreateParameterInt("StochasticPeriod1", 13);
                StochasticPeriod2 = CreateParameterInt("StochasticPeriod2", 25);
                StochasticPeriod3 = CreateParameterInt("StochasticPeriod3", 2);
                StochasticPeriod4 = CreateParameterInt("StochasticPeriod4", 3);
                _lengthZigZag = CreateParameterInt("Length ZigZag", 14);

                _seriesSMI = CreateSeries("StochasticMomentumIndex", Color.Blue, IndicatorChartPaintType.Line, true);

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

                _SMI = IndicatorsFactory.CreateIndicatorByName("StochasticMomentumIndex", Name + "StochasticMomentumIndex", false);
                ((IndicatorParameterInt)_SMI.Parameters[0]).Bind(StochasticPeriod1);
                ((IndicatorParameterInt)_SMI.Parameters[1]).Bind(StochasticPeriod2);
                ((IndicatorParameterInt)_SMI.Parameters[2]).Bind(StochasticPeriod3);
                ((IndicatorParameterInt)_SMI.Parameters[3]).Bind(StochasticPeriod4);
                ProcessIndicator("StochasticMomentumIndex", _SMI);

            }
        }

        public override void OnProcess(List<Candle> candles, int index)
        {
            _seriesSMI.Values[index] = _SMI.DataSeries[0].Values[index];

            List<decimal> values = _SMI.DataSeries[1].Values;

            if (index < _lengthZigZag.ValueInt * 2)
            {
                currentZigZagHigh = 0;
                currentZigZagLow = 0;
                lastSwingIndex = -1;
                lastSwingPrice = 0;
                trendDir = 0;
                return;
            }

            List<decimal> valuesStochastic = new List<decimal>();

            for (int i = 0; i < index + 1; i++)
            {
                valuesStochastic.Add(values[i]);
            }

            decimal High = 0;
            decimal Low = 0;

            High = valuesStochastic[valuesStochastic.Count - 1];
            Low = valuesStochastic[valuesStochastic.Count - 1];


            if (lastSwingPrice == 0)
                lastSwingPrice = Low + (High - Low) / 2;

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

            if (trendDir == 1 && isSwingHigh && High >= lastSwingPrice)
            {
                saveValue = High;
                updateHigh = true;
            }
            else if (trendDir == -1 && isSwingLow && Low <= lastSwingPrice)
            {
                saveValue = Low;
                updateLow = true;
            }
            else if (trendDir <= 0 && isSwingHigh)
            {
                saveValue = High;
                addHigh = true;
                trendDir = 1;
            }
            else if (trendDir >= 0 && isSwingLow)
            {
                saveValue = Low;
                addLow = true;
                trendDir = -1;
            }

            if (addHigh || addLow || updateHigh || updateLow)
            {
                if (updateHigh && lastSwingIndex >= 0)
                {
                    _seriesZigZag.Values[lastSwingIndex] = 0;
                    _seriesZigZagHighs.Values[lastSwingIndex] = 0;
                }
                else if (updateLow && lastSwingIndex >= 0)
                {
                    _seriesZigZag.Values[lastSwingIndex] = 0;
                    _seriesZigZagLows.Values[lastSwingIndex] = 0;
                }

                if (addHigh || updateHigh)
                {
                    currentZigZagHigh = saveValue;
                    _seriesZigZag.Values[index] = currentZigZagHigh;
                    _seriesZigZagHighs.Values[index] = currentZigZagHigh;

                }
                else if (addLow || updateLow)
                {
                    currentZigZagLow = saveValue;
                    _seriesZigZag.Values[index] = currentZigZagLow;
                    _seriesZigZagLows.Values[index] = currentZigZagLow;

                }

                lastSwingIndex = index;
                lastSwingPrice = saveValue;

                if (updateHigh || updateLow)
                {
                    ReBuildLine(_seriesZigZag.Values, _seriesToLine.Values);
                }
            }
            ReBuildChannel(_seriesZigZagUpChannel, _seriesZigZagDownChannel, _seriesZigZagHighs.Values, _seriesZigZagLows.Values, index);
        }

        private decimal currentZigZagHigh = 0;
        private decimal currentZigZagLow = 0;
        private int lastSwingIndex = -1;
        private decimal lastSwingPrice = 0;
        private int trendDir = 0;


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


