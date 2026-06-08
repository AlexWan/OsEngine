using OsEngine.Entity;
using System;
using System.Collections.Generic;
using System.Drawing;

namespace OsEngine.Indicators
{
    [Indicator("VolatilityAverageChannel")]
    public class VolatilityAverageChannel : Aindicator
    {
        private IndicatorParameterString _typeVolatilityPeriod;

        private IndicatorParameterString _typeVolatilityVariable;

        private IndicatorParameterInt _lenSmaSlow;

        private IndicatorParameterInt _lenSmaFast;

        private IndicatorParameterDecimal _channelDeviation;

        private IndicatorDataSeries _seriesVolatility;

        private IndicatorDataSeries _seriesSmaSlow;

        private IndicatorDataSeries _seriesSmaFast;

        private IndicatorDataSeries _seriesUpChannel;

        private IndicatorDataSeries _seriesDownChannel;

        public override void OnStateChange(IndicatorState state)
        {
            if (state == IndicatorState.Configure)
            {
                _typeVolatilityPeriod = CreateParameterStringCollection(
                    "Volatility base type",
                    "Day",
                    new List<string> { "Day", "Week", "Candle" });

                _typeVolatilityVariable = CreateParameterStringCollection(
                    "Volatility variable type",
                    "Percent",
                    new List<string> { "Percent", "Absolute" });

                _lenSmaSlow = CreateParameterInt("Slow sma len", 21);
                _lenSmaFast = CreateParameterInt("Fast sma len", 7);
                _channelDeviation = CreateParameterDecimal("Channel Deviation", 0.25m);

                _seriesUpChannel = CreateSeries("Up channel", Color.WhiteSmoke, IndicatorChartPaintType.Line, true);
                _seriesUpChannel.CanReBuildHistoricalValues = true;

                _seriesDownChannel = CreateSeries("Down channel", Color.WhiteSmoke, IndicatorChartPaintType.Line, true);
                _seriesDownChannel.CanReBuildHistoricalValues = true;

                _seriesVolatility = CreateSeries("Volatility", Color.DodgerBlue, IndicatorChartPaintType.Line, true);
                _seriesVolatility.CanReBuildHistoricalValues = true;

                _seriesSmaSlow = CreateSeries("Sma slow", Color.WhiteSmoke, IndicatorChartPaintType.Line, false);
                _seriesSmaSlow.CanReBuildHistoricalValues = true;

                _seriesSmaFast = CreateSeries("Sma fast", Color.DarkRed, IndicatorChartPaintType.Line, true);
                _seriesSmaFast.CanReBuildHistoricalValues = true;
            }
        }

        public override void OnProcess(List<Candle> candles, int index)
        {
            if (candles.Count == 0)
            {
                return;
            }

            UpDateVolatilityBase(candles, index);
        }

        private void UpDateVolatilityBase(List<Candle> candles, int index)
        {
            if (_typeVolatilityPeriod.ValueString == "Day")
            {
                GetVolatilityDay(candles, index);
            }
            else if (_typeVolatilityPeriod.ValueString == "Week")
            {
                GetVolatilityWeek(candles, index);
            }
            else if (_typeVolatilityPeriod.ValueString == "Candle")
            {
                GetVolatilityCandle(candles, index);
            }
        }

        private void GetVolatilityDay(List<Candle> candles, int index)
        {
            decimal lowToday = decimal.MaxValue;
            decimal highToday = decimal.MinValue;
            DateTime curDay = candles[index].TimeStart.Date;

            for (int i = index; i >= 0; i--)
            {
                if (candles[i].TimeStart.Date != curDay)
                {
                    break;
                }

                if (lowToday > candles[i].Low)
                {
                    lowToday = candles[i].Low;
                }
                if (highToday < candles[i].High)
                {
                    highToday = candles[i].High;
                }
            }

            if (lowToday == decimal.MaxValue)
            {
                return;
            }

            if (highToday == decimal.MinValue)
            {
                return;
            }

            decimal value = 0;

            if (_typeVolatilityVariable.ValueString == "Absolute")
            {
                value = highToday - lowToday;
            }
            else if (_typeVolatilityVariable.ValueString == "Percent")
            {
                value = (highToday - lowToday) / (lowToday / 100);
                value = Math.Round(value, 5);
            }

            curDay = candles[index].TimeStart.Date;

            for (int i = index; i >= 0; i--)
            {
                if (candles[i].TimeStart.Date != curDay)
                {
                    break;
                }

                _seriesVolatility.Values[i] = value;
            }

            // SMA Fast

            decimal summValue = 0;
            int countChangeVola = 0;
            decimal lastValue = 0;

            for (int i = index; i >= 0; i--)
            {
                if (lastValue != 0 &&
                    _seriesVolatility.Values[i] == lastValue)
                {
                    continue;
                }

                lastValue = _seriesVolatility.Values[i];
                countChangeVola++;
                summValue += lastValue;

                if (countChangeVola >= _lenSmaFast.ValueInt)
                {
                    break;
                }
            }

            decimal valueSma = summValue / countChangeVola;

            for (int i = index; i >= 0; i--)
            {
                if (candles[i].TimeStart.Date != curDay)
                {
                    break;
                }

                _seriesSmaFast.Values[i] = Math.Round(valueSma, 5);
            }

            // SMA Slow / Channel

            summValue = 0;
            countChangeVola = 0;
            lastValue = 0;

            for (int i = index; i >= 0; i--)
            {
                if (lastValue != 0 &&
                    _seriesVolatility.Values[i] == lastValue)
                {
                    continue;
                }

                lastValue = _seriesVolatility.Values[i];
                countChangeVola++;
                summValue += lastValue;

                if (countChangeVola >= _lenSmaSlow.ValueInt)
                {
                    break;
                }
            }

            valueSma = summValue / countChangeVola;

            for (int i = index; i >= 0; i--)
            {
                if (candles[i].TimeStart.Date != curDay)
                {
                    break;
                }

                _seriesSmaSlow.Values[i] = Math.Round(valueSma, 5);
                _seriesUpChannel.Values[i] = Math.Round(valueSma + valueSma * _channelDeviation.ValueDecimal,5);
                _seriesDownChannel.Values[i] = Math.Round(valueSma - valueSma * _channelDeviation.ValueDecimal, 5);
            }

        }

        private void GetVolatilityWeek(List<Candle> candles, int index)
        {
            // ищем дату предыдущего воскресенья от текущей даты

            DateTime lastTimeDate;
            DayOfWeek curDay = candles[index].TimeStart.DayOfWeek;
            DateTime curTime = candles[index].TimeStart;

            if (curDay == DayOfWeek.Sunday)
            {
                curTime = curTime.AddDays(-1);
            }

            while (curTime.DayOfWeek != DayOfWeek.Sunday)
            {
                curTime = curTime.AddDays(-1);
            }

            lastTimeDate = curTime;


            decimal lowToday = decimal.MaxValue;
            decimal highToday = decimal.MinValue;

            for (int i = index; i >= 0; i--)
            {
                if (candles[i].TimeStart.Date <= lastTimeDate)
                {
                    break;
                }

                if (lowToday > candles[i].Low)
                {
                    lowToday = candles[i].Low;
                }
                if (highToday < candles[i].High)
                {
                    highToday = candles[i].High;
                }
            }

            if (lowToday == decimal.MaxValue)
            {
                return;
            }

            if (highToday == decimal.MinValue)
            {
                return;
            }

            decimal value = 0;

            if (_typeVolatilityVariable.ValueString == "Absolute")
            {
                value = highToday - lowToday;
            }
            else if (_typeVolatilityVariable.ValueString == "Percent")
            {
                value = (highToday - lowToday) / (lowToday / 100);
                value = Math.Round(value, 5);
            }

            for (int i = index; i >= 0; i--)
            {
                if (candles[i].TimeStart.Date <= lastTimeDate)
                {
                    break;
                }

                _seriesVolatility.Values[i] = value;
            }

            // SMA Fast

            decimal summValue = 0;
            int countChangeVola = 0;
            decimal lastValue = 0;

            for (int i = index; i >= 0; i--)
            {
                if (lastValue != 0 &&
                    _seriesVolatility.Values[i] == lastValue)
                {
                    continue;
                }

                lastValue = _seriesVolatility.Values[i];
                countChangeVola++;
                summValue += lastValue;

                if (countChangeVola >= _lenSmaFast.ValueInt)
                {
                    break;
                }
            }

            decimal valueSma = summValue / countChangeVola;

            for (int i = index; i >= 0; i--)
            {
                if (candles[i].TimeStart.Date <= lastTimeDate)
                {
                    break;
                }

                _seriesSmaFast.Values[i] = valueSma;
            }

            // SMA Slow / Channel

            summValue = 0;
            countChangeVola = 0;
            lastValue = 0;

            for (int i = index; i >= 0; i--)
            {
                if (lastValue != 0 &&
                    _seriesVolatility.Values[i] == lastValue)
                {
                    continue;
                }

                lastValue = _seriesVolatility.Values[i];
                countChangeVola++;
                summValue += lastValue;

                if (countChangeVola >= _lenSmaSlow.ValueInt)
                {
                    break;
                }
            }

            valueSma = summValue / countChangeVola;

            for (int i = index; i >= 0; i--)
            {
                if (candles[i].TimeStart.Date <= lastTimeDate)
                {
                    break;
                }

                _seriesSmaSlow.Values[i] = valueSma;
                _seriesUpChannel.Values[i] = Math.Round(valueSma + valueSma * _channelDeviation.ValueDecimal, 5);
                _seriesDownChannel.Values[i] = Math.Round(valueSma - valueSma * _channelDeviation.ValueDecimal, 5);
            }
        }

        private void GetVolatilityCandle(List<Candle> candles, int index)
        {
            decimal value = 0;

            if (_typeVolatilityVariable.ValueString == "Absolute")
            {
                value = candles[index].High - candles[index].Low;
            }
            else if (_typeVolatilityVariable.ValueString == "Percent")
            {
                value = (candles[index].High - candles[index].Low) / (candles[index].Low / 100);
                value = Math.Round(value, 5);
            }

            _seriesVolatility.Values[index] = value;

            // Sma fast

            decimal summValue = 0;
            int countChangeVola = 0;

            for (int i = index; i >= 0; i--)
            {
                countChangeVola++;
                summValue += _seriesVolatility.Values[i];

                if (countChangeVola >= _lenSmaFast.ValueInt)
                {
                    break;
                }
            }

            decimal valueSma = summValue / countChangeVola;
            _seriesSmaFast.Values[index] = valueSma;

            // Sma slow

            summValue = 0;
            countChangeVola = 0;

            for (int i = index; i >= 0; i--)
            {
                countChangeVola++;
                summValue += _seriesVolatility.Values[i];

                if (countChangeVola >= _lenSmaSlow.ValueInt)
                {
                    break;
                }
            }

            valueSma = summValue / countChangeVola;
            _seriesSmaSlow.Values[index] = valueSma;
            _seriesUpChannel.Values[index] = Math.Round(valueSma + valueSma * _channelDeviation.ValueDecimal, 5);
            _seriesDownChannel.Values[index] = Math.Round(valueSma - valueSma * _channelDeviation.ValueDecimal, 5);
        }
    }
}