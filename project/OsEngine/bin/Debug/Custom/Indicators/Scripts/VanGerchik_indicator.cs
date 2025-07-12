using OsEngine.Entity;
using OsEngine.Indicators;
using System.Collections.Generic;
using System.Drawing;

[Indicator("VanGerchik_indicator")]
public class VanGerchik_indicator : Aindicator
{
    private IndicatorParameterInt _lenght;

    private IndicatorParameterDecimal _deviation;

    private IndicatorDataSeries _seriesUpPC;
    private IndicatorDataSeries _seriesDownPC;

    private IndicatorDataSeries _seriesUpDev;
    private IndicatorDataSeries _seriesDownDev;

    public override void OnStateChange(IndicatorState state)
    {
        if (state == IndicatorState.Configure)
        {
            _lenght = CreateParameterInt("Period", 21);

            _deviation = CreateParameterDecimal("Deviation %", 10m);

            _seriesUpPC = CreateSeries("Up line PC", Color.Aqua, IndicatorChartPaintType.Line, true);
            _seriesDownPC = CreateSeries("Down line PC", Color.Yellow, IndicatorChartPaintType.Line, true);

            _seriesUpDev = CreateSeries("Deviation Up line", Color.Aquamarine, IndicatorChartPaintType.Point, true);
            _seriesDownDev = CreateSeries("Deviation Down line", Color.LightGoldenrodYellow, IndicatorChartPaintType.Point, true);
        }
    }

    public override void OnProcess(List<Candle> candles, int index)
    {
        if (index <= _lenght.ValueInt)
        {
            return;
        }

        decimal upLine = 0;

        if (index - _lenght.ValueInt > 0)
        {
            for (int i = index; i > -1 && i > index - _lenght.ValueInt; i--)
            {
                if (upLine < candles[i].High)
                {
                    upLine = candles[i].High;
                }
            }
        }

        decimal downLine = 0;

        if (index - _lenght.ValueInt > 0)
        {
            downLine = decimal.MaxValue;

            for (int i = index; i > -1 && i > index - _lenght.ValueInt; i--)
            {
                if (downLine > candles[i].Low)
                {
                    downLine = candles[i].Low;
                }
            }
        }

        _seriesUpPC.Values[index] = upLine;
        _seriesDownPC.Values[index] = downLine;

        decimal vola = upLine - downLine;

        decimal deviation = (vola / 100) * _deviation.ValueDecimal;

        _seriesUpDev.Values[index] = upLine - deviation;
        _seriesDownDev.Values[index] = downLine + deviation;

    }
}