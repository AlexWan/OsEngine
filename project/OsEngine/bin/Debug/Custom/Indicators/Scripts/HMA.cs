//https://www.incrediblecharts.com/indicators/hull-moving-average.php
// creator: Alan HULL

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Drawing;
using OsEngine.Entity;
using OsEngine.Indicators;

public class HMA : Aindicator
{
    private IndicatorParameterInt _length;

    private IndicatorParameterString _candlePoint;

    private IndicatorDataSeries _series;
    private IndicatorDataSeries _series2;

    public override void OnStateChange(IndicatorState state)
    {
        if (state == IndicatorState.Configure)
        {
            _length = CreateParameterInt("Length", 14);

            _candlePoint = CreateParameterStringCollection("Candle point", "Close", Entity.CandlePointsArray);

            _series = CreateSeries("LWMA", Color.Red, IndicatorChartPaintType.Line, true);
            _series2 = CreateSeries("LWMA2", Color.Red, IndicatorChartPaintType.Line, false);
            Save();
            Reload();
        }
    }

    public override void OnProcess(List<Candle> candles, int index)
    {
        if (_length.ValueInt > index) { return; }

        List<decimal> list = new List<decimal>();

        decimal wma1 = GetWma(candles, index, (int)Math.Round((double)_length.ValueInt / 2));

        decimal wma2 = GetWma(candles, index, _length.ValueInt);

        _series2.Values[index] = 2 * wma1 - wma2;

        decimal wma3 = GetWmaSeries(_series2, index, (int)Math.Round(Math.Sqrt(_length.ValueInt)));

        _series.Values[index] = wma3;

    }

    decimal GetWmaSeries(IndicatorDataSeries candles, int index, int period)
    {

        decimal sum = 0;
        decimal weights = 0;

        //расчет WMA
        for (int i = 0; i <= period; i++)
        {
            sum += candles.Values[index - (period - i)] * i;
            weights += i;
        }
        sum = weights != 0 ? sum / weights : 0;
        return Math.Round(sum, 4);

    }

    decimal GetWma(List<Candle> candles, int index, int period)
    {
        decimal sum = 0;
        decimal weights = 0;

        //расчет WMA
        for (int i = 0; i <= period; i++)
        {
            sum += candles[index - (period - i)].GetPoint(_candlePoint.ValueString) * i;
            weights += i;
        }
        sum = weights != 0 ? sum / weights : 0;
        return Math.Round(sum, 4);
    }
}