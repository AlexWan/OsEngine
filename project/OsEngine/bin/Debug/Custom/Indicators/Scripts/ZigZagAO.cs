﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Drawing;
using OsEngine.Entity;

namespace OsEngine.Indicators.Indicator
{
    internal class ZigZagAO : Aindicator
    {
        private Aindicator _aO;
        private IndicatorParameterInt _lenghtFastLine;
        private IndicatorParameterInt _lenghtSlowLine;
        public IndicatorParameterString _candlePoint;

        private IndicatorDataSeries _seriesAO;
       
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
                _lenghtFastLine = CreateParameterInt("Fast line length", 5);
                _lenghtSlowLine = CreateParameterInt("Slow line length", 32);
                _candlePoint = CreateParameterStringCollection("Candle point", "Typical", Entity.CandlePointsArray);

                _lengthZigZag = CreateParameterInt("Length ZigZag", 14);

                _seriesAO = CreateSeries("AO", Color.DarkGreen, IndicatorChartPaintType.Column, true);
                //_seriesZigZagAO = CreateSeries("ZigZagAO", Color.Cyan, IndicatorChartPaintType.Point, true);

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

                _aO = IndicatorsFactory.CreateIndicatorByName("AO", Name + "AO", false);
                ((IndicatorParameterInt)_aO.Parameters[0]).Bind(_lenghtFastLine);
                ((IndicatorParameterInt)_aO.Parameters[1]).Bind(_lenghtSlowLine);
                ((IndicatorParameterString)_aO.Parameters[2]).ValueString = _candlePoint.ValueString;
                ProcessIndicator("AO", _aO);

            }
        }

        public override void OnProcess(List<Candle> candles, int index)
        {
            _seriesAO.Values[index] = _aO.DataSeries[0].Values[index];

            // получаем последнее значение индикатора АО
            List<decimal> values = _aO.DataSeries[0].Values;

            if (index < _lengthZigZag.ValueInt * 2)
            {
                currentZigZagHigh = 0;
                currentZigZagLow = 0;
                lastSwingIndex = -1;
                lastSwingPrice = 0;
                trendDir = 0;
                return;
            }

            if (index < _lenghtFastLine.ValueInt || index < _lenghtSlowLine.ValueInt)
            {
                return;
            }

            List<decimal> valuesAO = new List<decimal>();

            for (int i = 0; i < index + 1; i++)
            {
                // бежим по прошлым периодам и собираем значения индикатора АО в массив, чтоб была история
                valuesAO.Add(values[i]);
            }

            // находим хай-лой предыдущего значения АО. 
            // подходит не для всех индикаторов
            decimal High = 0;
            decimal Low = 0;
            if (valuesAO[valuesAO.Count - 1] > 0)
            {
                High = valuesAO[valuesAO.Count - 1];
                Low = 0;
            }
            else
            {
                High = 0;
                Low = valuesAO[valuesAO.Count - 1];
            }                     


            //всё что ниже скопировал с индикатора ZigZag с небольшими корректировками
            if (lastSwingPrice == 0)
                lastSwingPrice = Low + (High - Low) / 2;
            // меняем candles на values в 2-х строчках
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
                    _seriesZigZag.Values[lastSwingIndex] = 0; // тут в оригинале double.NaN
                    _seriesZigZagHighs.Values[lastSwingIndex] = 0;
                }
                else if (updateLow && lastSwingIndex >= 0)
                {
                    _seriesZigZag.Values[lastSwingIndex] = 0; // тут в оригинале double.NaN
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
       
        // копируем метод
        private decimal GetExtremum(List<decimal> values, int period, string points, int index)
        {
            try
            {
                List<decimal> values1 = new List<decimal>();
                for (int i = index; i >= index - period; i--)
                    values1.Add(values[i]); // тут также поменял candles на values

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

        // этот метод тупо копируем
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

        // копируем метод
        private void ReBuildChannel(IndicatorDataSeries _seriesZigZagUpChannel, IndicatorDataSeries _seriesZigZagDownChannel,
       List<decimal> _seriesZigZagHighs, List<decimal> _seriesZigZagLows, int index)
        {
            // найти три последних максимума 
            List<int> _ZigZagHighs = new List<int>();

            for (int i = index; i >= 0; i--)
            {
                if (_ZigZagHighs.Count == 3) { break; }
                if (_seriesZigZagHighs[i] == 0) { continue; }
                _ZigZagHighs.Add(i);
            }

            // найти три последних минимума 
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
                // на максимуме
                // UpChannel - предпоследние два экстремума
                RedRawVectorLine(_seriesZigZagUpChannel, _seriesZigZagHighs, _ZigZagHighs[2], _ZigZagHighs[1], index);

                // DownCannel - последние два экстремума
                RedRawVectorLine(_seriesZigZagDownChannel, _seriesZigZagLows, _ZigZagLows[1], _ZigZagLows[0], index);
            }
            else
            {
                // на минимуме
                // UpCannel - последние два экстремума
                RedRawVectorLine(_seriesZigZagUpChannel, _seriesZigZagHighs, _ZigZagHighs[1], _ZigZagHighs[0], index);
                // DownChannel - предпоследние два экстремума
                RedRawVectorLine(_seriesZigZagDownChannel, _seriesZigZagLows, _ZigZagLows[2], _ZigZagLows[1], index);
            }
        }

        // копируем метод
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
