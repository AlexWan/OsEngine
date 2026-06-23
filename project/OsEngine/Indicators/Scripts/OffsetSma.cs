using System.Collections.Generic;
using System.Drawing;
using OsEngine.Entity;
using OsEngine.Language;

namespace OsEngine.Indicators
{
    [Indicator("OffsetSma")]
    public class OffsetSma : Aindicator
    {
        public override string Description
        {
            get
            {
                string eng = "OffsetSma calculates a simple moving average and shifts it to the right by a specified number of bars, allowing you to see where the SMA was in the past relative to current prices. " +
                             "Traders use it to analyze historical price levels and identify zones where reversals or consolidations occurred in the past.";

                string ru = "OffsetSma рассчитывает простую скользящую среднюю и сдвигает её вправо на указанное количество баров, позволяя видеть, где находилась SMA в прошлом, относительно текущих цен. " +
                            "Трейдеры используют индикатор для анализа исторических уровней цен и выявления зон, где в прошлом происходили развороты или консолидации.";

                return OsLocalization.ConvertToLocString($"Eng:{eng}_Ru:{ru}_");
            }
        }

        private IndicatorParameterInt _lengthSma;

        private IndicatorParameterInt _offset;

        private IndicatorDataSeries _series;

        private Aindicator _OffsetSma;

        public override void OnStateChange(IndicatorState state)
        {
            if (state == IndicatorState.Configure)
            {
                _lengthSma = CreateParameterInt("Sma length", 5);

                _offset = CreateParameterInt("Sma offset", 5);

                _series = CreateSeries("Sma", Color.DarkRed, IndicatorChartPaintType.Line, true);

                _OffsetSma = IndicatorsFactory.CreateIndicatorByName("Sma", Name + "Sma", false);
                _OffsetSma.Parameters[0].Bind(_lengthSma);
                ProcessIndicator("OffsetSma", _OffsetSma);
            }
        }

        public override void OnProcess(List<Candle> candles, int index)
        {
            if (index >= _offset.ValueInt)
            {
                    _series.Values[index] = _OffsetSma.DataSeries[0].Values[index - _offset.ValueInt];
            }
        }
    }
}