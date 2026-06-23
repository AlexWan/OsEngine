using OsEngine.Entity;
using OsEngine.Language;
using System;
using System.Collections.Generic;
using System.Drawing;

namespace OsEngine.Indicators
{
    [Indicator("SmaChannel")]
    public class SmaChannel : Aindicator
    {
        public override string Description
        {
            get
            {
                string eng = "SmaChannel builds a channel around a simple moving average, shifting the upper and lower boundaries by a set percentage of the average price. " +
                             "Traders use this channel to identify overbought/oversold zones, find bounces from boundaries, and filter trend direction by the center line.";

                string ru = "SmaChannel строит канал вокруг простой скользящей средней, смещая верхнюю и нижнюю границы на заданный процент от цены средней. " +
                            "Трейдеры используют этот канал для определения зон перекупленности/перепроданности, поиска отскоков от границ и фильтрации направления тренда по центральной линии.";

                return OsLocalization.ConvertToLocString($"Eng:{eng}_Ru:{ru}_");
            }
        }

        private IndicatorParameterInt _length;

        private IndicatorParameterDecimal _deviation;

        private IndicatorDataSeries _seriesUp;

        private IndicatorDataSeries _seriesDown;

        private IndicatorDataSeries _seriesCenter;

        private Aindicator _sma;

        public override void OnStateChange(IndicatorState state)
        {
            if (state == IndicatorState.Configure)
            {
                _length = CreateParameterInt("Length", 21);
                _deviation = CreateParameterDecimal("Deviation", 2);

                _seriesUp = CreateSeries("Up line", Color.Green, IndicatorChartPaintType.Line, true);
                _seriesCenter = CreateSeries("Centre line", Color.LightBlue, IndicatorChartPaintType.Line, false);
                _seriesDown = CreateSeries("Down line", Color.Red, IndicatorChartPaintType.Line, true);

                _sma = IndicatorsFactory.CreateIndicatorByName("Sma", Name + "Sma", false);
                _sma.Parameters[0].Bind(_length);
                ProcessIndicator("Central SMA", _sma);
            }
        }

        public override void OnProcess(List<Candle> candles, int index)
        {
            if (index <= _length.ValueInt)
            {
                return;
            }

            decimal valueSma = _sma.DataSeries[0].Values[index];

            _seriesCenter.Values[index] = valueSma;
            _seriesUp.Values[index] = valueSma + CalcDeviation(valueSma);
            _seriesDown.Values[index] = valueSma - CalcDeviation(valueSma);
        }

        private decimal CalcDeviation(decimal valueSma)
        {
            return Math.Round(valueSma * _deviation.ValueDecimal / 100, 6);
        }
    }
}