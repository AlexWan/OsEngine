using OsEngine.Entity;
using System;
using System.Collections.Generic;
using System.Drawing;

namespace OsEngine.Indicators
{
    [Indicator("VolatilityAverage")]
    public class VolatilityAverage : Aindicator
    {
        private IndicatorParameterString _typeVolatilityPeriod;

        private IndicatorParameterString _typeVolatilityVariable;

        private IndicatorParameterInt _lenSma;

        private IndicatorDataSeries _seriesVolatility; 

        private IndicatorDataSeries _seriesSma;

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

                _lenSma = CreateParameterInt("Sma len", 10);

                _seriesVolatility = CreateSeries("Volatility", Color.DodgerBlue, IndicatorChartPaintType.Line, true);
                _seriesVolatility.CanReBuildHistoricalValues = true;

                _seriesSma = CreateSeries("Sma", Color.DarkRed, IndicatorChartPaintType.Line, true);
                _seriesSma.CanReBuildHistoricalValues = true;
            }
        }

        public override void OnProcess(List<Candle> candles, int index)
        {
            if (candles.Count == 0)
            {
                return;
            }

            UpDateVolatilityBase(candles,index);
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

            // SMA

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

                if(countChangeVola >= _lenSma.ValueInt)
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

                _seriesSma.Values[i] = Math.Round(valueSma, 5);
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

            // SMA

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

                if (countChangeVola >= _lenSma.ValueInt)
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

                _seriesSma.Values[i] = Math.Round(valueSma, 5);
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

            decimal summValue = 0;
            int countChangeVola = 0;

            for (int i = index; i >= 0; i--)
            {
                countChangeVola++;
                summValue += _seriesVolatility.Values[i];

                if (countChangeVola >= _lenSma.ValueInt)
                {
                    break;
                }
            }

            decimal valueSma = summValue / countChangeVola;

            _seriesSma.Values[index] = Math.Round(valueSma, 5);

        }
    }
}