using System.Collections.Generic;
using System.Drawing;
using OsEngine.Entity;
using OsEngine.Language;

namespace OsEngine.Indicators
{
    [Indicator("OpenInterest")]
    public class OpenInterest : Aindicator
    {
        public override string Description
        {
            get
            {
                string eng = "OpenInterest displays the open interest for an instrument, showing the total number of open derivative contracts on each bar. " +
                             "Traders analyze open interest dynamics together with price and volume to assess the strength of the current trend and the likelihood of continuation or reversal.";

                string ru = "OpenInterest отображает открытый интерес по инструменту, показывая общее количество незакрытых деривативных контрактов на каждом баре. " +
                            "Трейдеры анализируют динамику открытого интереса вместе с ценой и объёмом для оценки силы текущего тренда и вероятности продолжения или разворота движения.";

                return OsLocalization.ConvertToLocString($"Eng:{eng}_Ru:{ru}_");
            }
        }

        private IndicatorDataSeries _series;

        public override void OnStateChange(IndicatorState state)
        {
            if (state == IndicatorState.Configure)
            {
                _series = CreateSeries("OpenInterest", Color.DodgerBlue, IndicatorChartPaintType.Column, true);
            }
            else if (state == IndicatorState.Dispose)
            {
                _series = null;
            }
        }

        public override void OnProcess(List<Candle> candles, int index)
        {
            _series.Values[index] = candles[index].OpenInterest;
        }
    }
}