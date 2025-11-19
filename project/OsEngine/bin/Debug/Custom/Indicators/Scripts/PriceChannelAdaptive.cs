using System;
using System.Collections.Generic;
using System.Drawing;
using OsEngine.Entity;
using OsEngine.Indicators;

namespace OsEngine.Indicators
{
    [Indicator("PriceChannelAdaptive")]
    public class PriceChannelAdaptive : Aindicator
    {
        private IndicatorParameterInt _lengthAdx;

        private IndicatorParameterInt _ratio;

        private IndicatorDataSeries _upChannel;

        private IndicatorDataSeries _downChannel;

        private IndicatorDataSeries _seriesX;

        private Aindicator _adx;

        public override void OnStateChange(IndicatorState state)
        {
            if (state == IndicatorState.Configure)
            {
                _lengthAdx = CreateParameterInt("Adx Period", 10);
                _ratio = CreateParameterInt("Ratio", 100);

                _upChannel = CreateSeries("Up Channel", Color.DodgerBlue, IndicatorChartPaintType.Line, true);
                _downChannel = CreateSeries("Down Channel", Color.Red, IndicatorChartPaintType.Line, true);
                _seriesX = CreateSeries("Channel adaptive length", Color.WhiteSmoke, IndicatorChartPaintType.Line, false);

                _adx = IndicatorsFactory.CreateIndicatorByName("ADX", Name + "ADX", false);

                ((IndicatorParameterInt)_adx.Parameters[0]).Bind(_lengthAdx);

                ProcessIndicator("ADX", _adx);
            }
        }

        public override void OnProcess(List<Candle> candles, int index)
        {
            if (index - _lengthAdx.ValueInt * 2 + 2 < 0)
            {
                return;
            }

            decimal adxLast = _adx.DataSeries[0].Last;
            decimal adxOnIndex = _adx.DataSeries[0].Values[index];

            if (adxLast == 0
                || adxOnIndex == 0)
            {
                return;
            }

            _seriesX.Values[index] = Math.Max(Math.Truncate(_ratio.ValueInt / adxOnIndex), 1);

            int x = (Int32)_seriesX.Values[index];

            _upChannel.Values[index] = candles.Highest(index - x, index);

            _downChannel.Values[index] = candles.Lowest(index - x, index);
        }
    }
}