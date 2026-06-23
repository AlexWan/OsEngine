using System.Collections.Generic;
using System.Drawing;
using OsEngine.Entity;
using OsEngine.Language;

namespace OsEngine.Indicators
{
    [Indicator("Volume")]
    public class Volume : Aindicator
    {
        public override string Description
        {
            get
            {
                string eng = "Volume displays trading volume on each bar as a histogram, showing market participant activity. " +
                             "Traders analyze volume to confirm trend strength, identify abnormal spikes in activity, and find divergences between price and volume.";

                string ru = "Volume отображает объём торгов на каждом баре в виде столбчатой диаграммы, показывая активность участников рынка. " +
                            "Трейдеры анализируют объём для подтверждения силы тренда, выявления аномальных всплесков активности и поиска дивергенций между ценой и объёмом.";

                return OsLocalization.ConvertToLocString($"Eng:{eng}_Ru:{ru}_");
            }
        }

        private IndicatorDataSeries _series;

        public override void OnStateChange(IndicatorState state)
        {
            if (state == IndicatorState.Configure)
            {
                _series = CreateSeries("Volume", Color.DodgerBlue, IndicatorChartPaintType.Column, true);
                _series.CanReBuildHistoricalValues = true;
            }
            else if (state == IndicatorState.Dispose)
            {
                _series = null;
            }
        }

        public override void OnProcess(List<Candle> candles, int index)
        {
            _series.Values[index] = candles[index].Volume;

            if(index - 1 >= 0)
            {
                _series.Values[index - 1] = candles[index - 1].Volume;
            }
        }
    }
}