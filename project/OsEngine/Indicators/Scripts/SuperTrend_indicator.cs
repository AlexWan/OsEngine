using System;
using System.Collections.Generic;
using System.Drawing;
using OsEngine.Entity;

namespace OsEngine.Indicators
{
    [Indicator("SuperTrend_indicator")]
    public class SuperTrend_indicator : Aindicator
    {
        private IndicatorParameterInt _period;

        private IndicatorParameterDecimal _deviation;

        private IndicatorParameterString _candlePoint;

        private IndicatorParameterBool _wicks;

        private IndicatorDataSeries _seriesLower;

        private IndicatorDataSeries _seriesUpper;

        private IndicatorDataSeries _seriesCenter;

        private Aindicator _atr;

        public override void OnStateChange(IndicatorState state)
        {
            _period = CreateParameterInt("Length", 15);
            _deviation = CreateParameterDecimal("Deviation factor", 1.0m);
            _candlePoint = CreateParameterStringCollection("Candle Point", "Median", new List<string>() { "Median", "Typical" });
            _wicks = CreateParameterBool("Use candle shadow", true);

            _seriesLower = CreateSeries("Lower channel", Color.Aqua, IndicatorChartPaintType.Line, false);
            _seriesLower.CanReBuildHistoricalValues = false;

            _seriesUpper = CreateSeries("Upper channel", Color.OrangeRed, IndicatorChartPaintType.Line, false);
            _seriesUpper.CanReBuildHistoricalValues = false;

            _seriesCenter = CreateSeries("Center Line ", Color.Gold, IndicatorChartPaintType.Line, true);
            _seriesCenter.CanReBuildHistoricalValues = false;

            _atr = IndicatorsFactory.CreateIndicatorByName("ATR", Name + "ATR", false);
            ((IndicatorParameterInt)_atr.Parameters[0]).Bind(_period);
            ProcessIndicator("ATR", _atr);        
        }

        private int _direction;

        public override void OnProcess(List<Candle> candles, int index)
        {
            if (index < _period.ValueInt)
            {
                return;
            }
            decimal atr_deviation = _deviation.ValueDecimal * _atr.DataSeries[0].Values[index];

            decimal highPrice = _wicks.ValueBool ? candles[index].High : candles[index].Close;
            decimal highPricePrew = _wicks.ValueBool ? candles[index - 1].High : candles[index - 1].Close;
            decimal lowPrice = _wicks.ValueBool ? candles[index].Low : candles[index].Close;
            decimal lowPricePrew = _wicks.ValueBool ? candles[index - 1].Low : candles[index - 1].Close;

            bool doji4price = candles[index].Open == candles[index].Close && candles[index].Open == candles[index].Low && candles[index].Open == candles[index].High;

            _seriesLower.Values[index] = candles[index].GetPoint(_candlePoint.ValueString) - atr_deviation;

            if (_seriesLower.Values[index] > 0)
            {
                if (doji4price)
                {
                    _seriesLower.Values[index] = _seriesLower.Values[index - 1];
                }
                else
                {
                    _seriesLower.Values[index] = (lowPricePrew > _seriesLower.Values[index - 1]) ? Math.Max(_seriesLower.Values[index], _seriesLower.Values[index - 1]) : _seriesLower.Values[index];
                }
            }
            else
            {
                _seriesLower.Values[index] = _seriesLower.Values[index - 1];
            }

            _seriesUpper.Values[index] = candles[index].GetPoint(_candlePoint.ValueString) + atr_deviation;

            if (_seriesUpper.Values[index] > 0)
            {
                if (doji4price)
                {
                    _seriesUpper.Values[index] = _seriesUpper.Values[index - 1];
                }
                else
                {
                    _seriesUpper.Values[index] = (highPricePrew < _seriesUpper.Values[index - 1]) ? Math.Min(_seriesUpper.Values[index], _seriesUpper.Values[index - 1]) : _seriesUpper.Values[index];
                }
            }
            else
            {
                _seriesUpper.Values[index] = _seriesUpper.Values[index - 1];
            }
            _direction = (highPrice > _seriesUpper.Values[index - 1]) ? 1 :
                    (lowPrice < _seriesLower.Values[index - 1]) ? -1 : _direction;

            _seriesCenter.Values[index] = _direction == 1 ? _seriesLower.Values[index] : _seriesUpper.Values[index];
        } 
    }
}