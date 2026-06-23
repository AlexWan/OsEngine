using System.Collections.Generic;
using System.Drawing;
using OsEngine.Entity;
using OsEngine.Language;

namespace OsEngine.Indicators
{
    [Indicator("PriceChannel")]
    public class PriceChannel : Aindicator
    {
        public override string Description
        {
            get
            {
                string eng = "PriceChannel builds upper and lower boundaries as the highest high and lowest low over the selected period, forming a channel within which price moves. " +
                             "Traders use it to determine trend direction, take a channel breakout as an entry signal, and place stop-losses beyond the opposite boundary.";

                string ru = "PriceChannel строит верхнюю и нижнюю границы как максимальные high и минимальные low за выбранный период, формируя канал, внутри которого движется цена. " +
                            "Трейдеры применяют его для определения направления тренда, пробоя канала как сигнала к входу и размещения стоп-лоссов за противоположной границей.";

                return OsLocalization.ConvertToLocString($"Eng:{eng}_Ru:{ru}_");
            }
        }

        private IndicatorParameterInt _lengthUp;

        private IndicatorParameterInt _lengthDown;

        private IndicatorDataSeries _seriesUp;

        private IndicatorDataSeries _seriesDown;

        public override void OnStateChange(IndicatorState state)
        {
            if (state == IndicatorState.Configure)
            {
                _lengthUp = CreateParameterInt("Length up", 21);
                _lengthDown = CreateParameterInt("Length down", 21);

                _seriesUp = CreateSeries("Up line", Color.Aqua, IndicatorChartPaintType.Line, true);
                _seriesDown = CreateSeries("Down line", Color.BlueViolet, IndicatorChartPaintType.Line, true);
            }
        }

        public override void OnProcess(List<Candle> candles, int index)
        {
            decimal upLine = 0;

            if (index - _lengthUp.ValueInt > 0)
            {
                for (int i = index; i > -1 && i > index - _lengthUp.ValueInt; i--)
                {
                    if (upLine < candles[i].High)
                    {
                        upLine = candles[i].High;
                    }
                }
            }

            decimal downLine = 0;

            if (index - _lengthDown.ValueInt > 0)
            {
                downLine = decimal.MaxValue;

                for (int i = index; i > -1 && i > index - _lengthDown.ValueInt; i--)
                {
                    if (downLine > candles[i].Low)
                    {
                        downLine = candles[i].Low;
                    }
                }
            }

            _seriesUp.Values[index] = upLine;
            _seriesDown.Values[index] = downLine;
        }
    }
}