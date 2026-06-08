using OsEngine.Entity;
using OsEngine.Indicators;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

 public class ParabolicBollinger_indicator : Aindicator
{
    private IndicatorParameterInt _lenght;
    private IndicatorParameterDecimal _deviation;

    private IndicatorParameterString _period;
    private IndicatorParameterInt _averagingPeriod;
    private IndicatorParameterDecimal _volatMult;

    private IndicatorDataSeries _seriesUp;
    private IndicatorDataSeries _seriesDown;
    private IndicatorDataSeries _seriesP;
    private IndicatorDataSeries _seriesValueVolatility;

    private Aindicator _sma;

    List<decimal> HiLowPercent = new List<decimal>();

    public override void OnStateChange(IndicatorState state)
    {
        if (state == IndicatorState.Configure)
        {
            _lenght = CreateParameterInt("Period ParabolicBollinger", 21);
            _deviation = CreateParameterDecimal("Diviation", 2);

            _period = CreateParameterStringCollection("Volatility calculation period", "Day", new List<string> { "Day", "Week", "Month" });
            _averagingPeriod = CreateParameterInt("Averaging Period Volatility", 15);
            _volatMult = CreateParameterDecimal("Volatility multi", 0.1m);

            _seriesUp = CreateSeries("Up line", Color.Aqua, IndicatorChartPaintType.Line, true);
            _seriesDown = CreateSeries("Down line", Color.BlueViolet, IndicatorChartPaintType.Line, true);
            _seriesP = CreateSeries("ParabolicBollindger series", Color.Yellow, IndicatorChartPaintType.Point, true);

            _seriesValueVolatility = CreateSeries("Volatility value series", Color.Aquamarine, IndicatorChartPaintType.Line, false);

            _sma = IndicatorsFactory.CreateIndicatorByName("Sma", Name + "Sma", false);
            ((IndicatorParameterInt)_sma.Parameters[0]).Bind(_lenght);
            ProcessIndicator("Central SMA", _sma);
        }
    }

    public override void OnProcess(List<Candle> candles, int index)
    {
        if (index <= _lenght.ValueInt)
        {
            return;
        }

        if (index == 0)
            HiLowPercent.Clear();
        if (index <= 20)
            return;

        decimal HiLowDifference = GetVolatility(candles, index);

        if(HiLowDifference == 0)
        {
            return;
        }

        decimal volatilityPercent = GetVolatilityPercent(candles, index, HiLowDifference);

        if (HiLowPercent.Count < _averagingPeriod.ValueInt)
        {
            HiLowPercent.Add(volatilityPercent);
        }
        else
        {
            HiLowPercent.RemoveAt(0);
            HiLowPercent.Add(volatilityPercent);
        }

        decimal ValueVolatility = HiLowDifference * _volatMult.ValueDecimal;
        decimal ValueVolatilityAverage = GetAverageVolatilityValue(candles, index);

        _seriesValueVolatility.Values[index] = Math.Round(ValueVolatility, 6);

        decimal valueSma = _sma.DataSeries[0].Values[index];

        decimal[] valueDev = new decimal[_lenght.ValueInt];

        for (int i = index - _lenght.ValueInt + 1, i2 = 0; i < index + 1; i++, i2++)
        {
            valueDev[i2] = candles[i].Close - valueSma;
        }

        for (int i = 0; i < valueDev.Length; i++)
        {
            valueDev[i] = Convert.ToDecimal(Math.Pow(Convert.ToDouble(valueDev[i]), 2));
        }

        double summ = 0;

        for (int i = 0; i < valueDev.Length; i++)
        {
            summ += Convert.ToDouble(valueDev[i]);
        }

        if (_lenght.ValueInt > 30)
        {
            summ = summ / (_lenght.ValueInt - 1);
        }
        else
        {
            summ = summ / _lenght.ValueInt;
        }

        summ = Math.Sqrt(summ);

        decimal upLine = Math.Round(valueSma + Convert.ToDecimal(summ) * _deviation.ValueDecimal, 6);
        decimal downLine = Math.Round(valueSma - Convert.ToDecimal(summ) * _deviation.ValueDecimal, 6);

        _seriesUp.Values[index] = upLine;
        _seriesDown.Values[index] = downLine;

        if (candles[index].High > upLine)
        {
            drawBelow = true;
        }
        if (candles[index].Low < downLine)
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
            if (_seriesP.Values[index - 1] != 0 &&
                changePosition == false)
            {
                decimal value = _seriesP.Values[index - 1] + ValueVolatilityAverage;

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
            else
            {
                _seriesP.Values[index] = _seriesDown.Values[index];
            }
        }
        else
        {
            if (_seriesP.Values[index - 1] != 0 &&
                changePosition == false)
            {
                decimal value = _seriesP.Values[index - 1] - ValueVolatilityAverage;
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
            else
            {
                _seriesP.Values[index] = _seriesUp.Values[index];
            }
        }
        lastDrawBelow = drawBelow;
    }
    bool drawBelow = false;
    bool lastDrawBelow = false;

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

    public decimal GetAverageVolatilityValue(List<Candle> candles, int index)
    {
        if (_seriesValueVolatility.Values.Count <= _averagingPeriod.ValueInt)
        {
            return _seriesValueVolatility.Values[index];
        }
        decimal avr = 0;
        for (int i = _seriesValueVolatility.Values.Count - _averagingPeriod.ValueInt; i < _seriesValueVolatility.Values.Count; i++)
        {
            avr += _seriesValueVolatility.Values[i] * _volatMult.ValueDecimal;
        }
        avr = avr / _averagingPeriod.ValueInt;

        return avr;
    }
}

