using System;
using System.Collections.Generic;
using System.Drawing;
using OsEngine.Entity;
using OsEngine.Indicators;
using OsEngine.Language;

namespace OsEngine.Indicators
{
    [Indicator("PriceChannelAdaptive")]
    public class PriceChannelAdaptive : Aindicator
    {
        public override string Description
        {
            get
            {
                string eng = "PriceChannelAdaptive changes the price channel length based on the ADX value: the channel lengthens in a strong trend and shortens in a weak one, adapting to current market structure. " +
                             "Traders use it to more accurately determine movement boundaries in different market phases and filter false breakouts.";

                string ru = "PriceChannelAdaptive изменяет длину ценового канала в зависимости от значения ADX: при сильном тренде канал удлиняется, при слабом — укорачивается, адаптируясь к текущей рыночной структуре. " +
                            "Трейдеры используют его для более точного определения границ движения в разных фазах рынка и фильтрации ложных пробоев.";

                return OsLocalization.ConvertToLocString($"Eng:{eng}_Ru:{ru}_");
            }
        }

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