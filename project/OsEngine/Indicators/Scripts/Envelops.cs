using System;
using System.Collections.Generic;
using System.Drawing;
using OsEngine.Entity;
using OsEngine.Language;

namespace OsEngine.Indicators
{
    [Indicator("Envelops")]
    public class Envelops : Aindicator
    {
        public override string Description
        {
            get
            {
                string eng = "Envelopes build two parallel lines at a fixed percentage deviation from a central moving average, forming a channel around price. " +
                             "Traders use Envelopes to find overbought/oversold zones when price approaches the upper or lower boundary, and to identify the bounds of a sideways range.";

                string ru = "Конверты строят две параллельные линии на фиксированном процентном отклонении от центральной скользящей средней, формируя канал вокруг цены. " +
                            "Трейдеры используют Envelopes для поиска зон перекупленности/перепроданности при подходе цены к верхней или нижней границе, а также для определения границ бокового диапазона.";

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
                _seriesCenter = CreateSeries("Centre line", Color.Green, IndicatorChartPaintType.Line, false);
                _seriesDown = CreateSeries("Down line", Color.Green, IndicatorChartPaintType.Line, true);

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
            _seriesUp.Values[index] = Math.Round(valueSma + valueSma * _deviation.ValueDecimal / 100, 6);
            _seriesDown.Values[index] = Math.Round(valueSma - valueSma * _deviation.ValueDecimal / 100, 6);
        }
    }
}