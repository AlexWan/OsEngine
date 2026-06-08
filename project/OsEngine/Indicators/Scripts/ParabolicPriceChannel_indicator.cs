using System;
using System.Collections.Generic;
using System.Drawing;
using OsEngine.Entity;
using OsEngine.Indicators;

public class ParabolicPriceChannel_indicator : Aindicator
{
    private IndicatorParameterInt _lengthUp;
    private IndicatorParameterInt _lengthDown;

    private IndicatorParameterString _period;
    private IndicatorParameterInt _averagingPeriod;
    private IndicatorParameterDecimal _volatilityMult;

    private IndicatorDataSeries _seriesUp;
    private IndicatorDataSeries _seriesDown;
    private IndicatorDataSeries _seriesP;
    private IndicatorDataSeries _seriesValueVolatility;

    public override void OnStateChange(IndicatorState state)
    {
        if (state == IndicatorState.Configure)
        {
            _lengthUp = CreateParameterInt("Length up", 21);
            _lengthDown = CreateParameterInt("Length down", 21);

            _period = CreateParameterStringCollection("Volatility calculation period", "Day", new List<string> { "Day", "Week", "Month" });
            _averagingPeriod = CreateParameterInt("Averaging Period Volatility", 15);
            _volatilityMult = CreateParameterDecimal("Volatility multi", 0.1m);

            _seriesUp = CreateSeries("Up line", Color.Aqua, IndicatorChartPaintType.Line, true);
            _seriesDown = CreateSeries("Down line", Color.BlueViolet, IndicatorChartPaintType.Line, true);
            _seriesP = CreateSeries("Parabolic line", Color.Aqua, IndicatorChartPaintType.Point, true);

            _seriesValueVolatility = CreateSeries("Volatility value series", Color.Aquamarine, IndicatorChartPaintType.Line, false);
        }
    }

    bool drawBelow = false;

    bool lastDrawBelow = false;

    public override void OnProcess(List<Candle> candles, int index)
    {
        if (index == 0)
        {
            drawBelow = false;
            lastDrawBelow = false;
        }

        if (index <= 20)
        {
            return;
        }

        if (index < _lengthUp.ValueInt
            || index < _lengthDown.ValueInt)
        {
            return;
        }

        decimal hiLowDifference = GetVolatility(candles, index);

        decimal valueVolatility = hiLowDifference * _volatilityMult.ValueDecimal;

        _seriesValueVolatility.Values[index] = Math.Round(valueVolatility, 6);

        decimal valueVolatilityAverage = GetAverageVolatilityValue(index, _seriesValueVolatility.Values); ;

        decimal upLine = 0;

        if (index - _lengthUp.ValueInt > 0)
        {
            for (int i = index; i > -1 && i > index - _lengthUp.ValueInt; i--)
            {
                if (upLine < candles[i].High)
                {
                    upLine = candles[i].High;
                }
            }
        }

        decimal downLine = 0;

        if (index - _lengthDown.ValueInt > 0)
        {
            downLine = decimal.MaxValue;

            for (int i = index; i > -1 && i > index - _lengthDown.ValueInt; i--)
            {
                if (downLine > candles[i].Low)
                {
                    downLine = candles[i].Low;
                }
            }
        }

        _seriesUp.Values[index] = upLine;
        _seriesDown.Values[index] = downLine;

        if (candles[index].High >= upLine)
        {
            drawBelow = true;
        }
        if (candles[index].Low <= downLine)
        {
            drawBelow = false;
        }

        bool changePosition = false;

        if (lastDrawBelow != drawBelow)
        {
            changePosition = true;
        }

        if (drawBelow)
        {
            if (changePosition == true)
            {
                _seriesP.Values[index] = _seriesDown.Values[index];
                _seriesP.Values[index - 1] = _seriesDown.Values[index];
            }
            else
            {
                decimal value = _seriesP.Values[index - 1] + valueVolatilityAverage;

                if (value < _seriesDown.Values[index])
                {
                    value = _seriesDown.Values[index];
                }
                if (value > _seriesUp.Values[index])
                {
                    value = _seriesUp.Values[index];
                }

                _seriesP.Values[index] = value;
            }
        }
        else
        {
            if (changePosition == true)
            {
                _seriesP.Values[index] = _seriesUp.Values[index];
                _seriesP.Values[index - 1] = _seriesUp.Values[index];
            }
            else
            {
                decimal value = _seriesP.Values[index - 1] - valueVolatilityAverage;

                if (value > _seriesUp.Values[index])
                {
                    value = _seriesUp.Values[index];
                }
                if (value < _seriesDown.Values[index])
                {
                    value = _seriesDown.Values[index];
                }

                _seriesP.Values[index] = value;
            }
        }

        lastDrawBelow = drawBelow;
    }

    public decimal GetVolatility(List<Candle> candles, int index)
    {
        DateTime calcStartDay = candles[index].TimeStart;
        DateTime time = candles[index].TimeStart;

        if (_period.ValueString.Contains("Day"))
        {
            time = calcStartDay.AddDays(-1);
        }
        else if (_period.ValueString.Contains("Week"))
        {
            time = calcStartDay.AddDays(-7);
        }
        else if (_period.ValueString.Contains("Month"))
        {
            time = calcStartDay.AddMonths(-1);
        }

        decimal high = candles[index].High;
        decimal low = candles[index].Low;

        int l = 1;

        while (index - l >= 0 && candles[index - l].TimeStart >= time)
        {
            if (candles[index - l].High > high)
            {
                high = candles[index - l].High;
            }
            if (candles[index - l].Low < low)
            {
                low = candles[index - l].Low;
            }

            l++;
        }

        decimal result = high - low;

        return result;
    }

    public decimal GetVolatilityPercent(List<Candle> candles, int index, decimal HiLowDifference)
    {

        decimal percent = ((candles[index].High - candles[index].Low) / HiLowDifference) * 100;

        return percent;
    }

    public decimal GetAverageVolatilityValue(int index, List<decimal> values)
    {
        if (index <= _averagingPeriod.ValueInt)
        {
            return 0;
        }

        decimal avr = 0;

        int startIndex = index - _averagingPeriod.ValueInt;

        if (startIndex < 0)
        {
            startIndex = 0;
        }

        for (int i = startIndex; i < index + 1; i++)
        {
            avr += _seriesValueVolatility.Values[i] * _volatilityMult.ValueDecimal;
        }

        avr = avr / _averagingPeriod.ValueInt;

        return avr;
    }
}