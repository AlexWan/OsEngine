using System;
using System.Collections.Generic;
using System.Drawing;
using OsEngine.Entity;
using OsEngine.Indicators;

public class KeltnerChannel : Aindicator
{
    private IndicatorParameterInt _emaPeriod;
    private IndicatorParameterInt _atrPeriod;
    private IndicatorParameterInt _atrEmaPeriod;
    private IndicatorParameterDecimal _atrCoef;
    private IndicatorParameterString _candlePoint;

    private Aindicator _ema;
    private Aindicator _atr;

    private IndicatorDataSeries _seriesEmaAtr;
    private IndicatorDataSeries _seriesUpChannel;
    private IndicatorDataSeries _seriesDownChannel;
    private IndicatorDataSeries _seriesMidleLine;

    public override void OnStateChange(IndicatorState state)
    {
        if (state == IndicatorState.Configure)
        {
            _emaPeriod = CreateParameterInt("EMA Period", 20);
            _atrPeriod = CreateParameterInt("ATR Period", 20);
            _atrEmaPeriod = CreateParameterInt("ATR EMA Period", 20);
            _atrCoef = CreateParameterDecimal("ATR multiplier", 2m);
            _candlePoint = CreateParameterStringCollection("Candle Point", "Typical", Entity.CandlePointsArray);

            _ema = IndicatorsFactory.CreateIndicatorByName("Ema", Name + "Ema", true);
            ((IndicatorParameterInt)_ema.Parameters[0]).Bind(_emaPeriod);
            ProcessIndicator("EMA", _ema);

            _atr = IndicatorsFactory.CreateIndicatorByName("ATR", Name + "ATR", true);
            ((IndicatorParameterInt)_atr.Parameters[0]).Bind(_atrPeriod);
            ProcessIndicator("ATR", _atr);

            _seriesEmaAtr = CreateSeries("EMA ATR", Color.Gray, IndicatorChartPaintType.Line, false);
            _seriesUpChannel = CreateSeries("Up Channel", Color.DodgerBlue, IndicatorChartPaintType.Line, true);
            _seriesDownChannel = CreateSeries("Down Channel", Color.DodgerBlue, IndicatorChartPaintType.Line, true);
            _seriesMidleLine = CreateSeries("Middle EMA", Color.Gray, IndicatorChartPaintType.Line, true);
        }

    }

    public override void OnProcess(List<Candle> candles, int index)
    {

        if (Math.Max(_atrPeriod.ValueInt + _atrEmaPeriod.ValueInt, _emaPeriod.ValueInt) <= index) // проверка на atr + ema от atr период
        {
            if (index == _atrPeriod.ValueInt + _atrEmaPeriod.ValueInt)
            {
                decimal smaAtr = 0;
                for (int i = index - _atrEmaPeriod.ValueInt + 1; i <= index; i++)
                {
                    smaAtr += _atr.DataSeries[0].Values[index];
                }

                _seriesEmaAtr.Values[index] = smaAtr / _atrEmaPeriod.ValueInt;
            }
            else if (index > _atrPeriod.ValueInt + _atrEmaPeriod.ValueInt)
            {
                decimal a = 2m / (_atrEmaPeriod.ValueInt + 1);
                decimal emaAtrLast = _seriesEmaAtr.Values[index - 1];
                decimal currentAtr = _atr.DataSeries[0].Values[index];
                _seriesEmaAtr.Values[index] = emaAtrLast + (a * (currentAtr - emaAtrLast));
            }

            _seriesMidleLine.Values[index] = _ema.DataSeries[0].Values[index];
            _seriesUpChannel.Values[index] = _ema.DataSeries[0].Values[index] + _atrCoef.ValueDecimal * _seriesEmaAtr.Values[index];
            _seriesDownChannel.Values[index] = _ema.DataSeries[0].Values[index] - _atrCoef.ValueDecimal * _seriesEmaAtr.Values[index];
        }
    }


}

