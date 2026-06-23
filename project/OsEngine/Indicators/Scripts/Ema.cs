using System;
using System.Collections.Generic;
using System.Drawing;
using OsEngine.Entity;
using OsEngine.Language;

namespace OsEngine.Indicators
{
    [Indicator("Ema")]
    public class Ema : Aindicator
    {
        public override string Description
        {
            get
            {
                string eng = "EMA smooths price data exponentially, giving more weight to recent bars and reacting faster to changes than a simple moving average. " +
                             "Traders use EMA to determine trend direction, find entry points at crossovers of multiple EMAs with different periods, and set dynamic stop-losses.";

                string ru = "EMA сглаживает ценовые данные экспоненциально, придавая больший вес последним барам и быстрее реагируя на изменения по сравнению с обычной скользящей средней. " +
                            "Трейдеры применяют EMA для определения направления тренда, поиска точек входа на пересечении нескольких EMA с разными периодами и размещения динамических стоп-лоссов.";

                return OsLocalization.ConvertToLocString($"Eng:{eng}_Ru:{ru}_");
            }
        }

        private IndicatorParameterInt _length;

        private IndicatorParameterString _candlePoint;

        private IndicatorDataSeries _series;

        public override void OnStateChange(IndicatorState state)
        {
            _length = CreateParameterInt("Length", 14);
            _candlePoint = CreateParameterStringCollection("Candle Point", "Close", Entity.CandlePointsArray);
            _series = CreateSeries("Ema", Color.DarkRed, IndicatorChartPaintType.Line, true);
        }

        public override void OnProcess(List<Candle> candles, int index)
        {
            decimal result = 0;

            if (index == _length.ValueInt)
            {
                decimal lastMoving = 0;

                for (int i = index - _length.ValueInt + 1; i < index + 1; i++)
                {
                    lastMoving += candles[i].GetPoint(_candlePoint.ValueString);
                }

                lastMoving = lastMoving / _length.ValueInt;
                result = lastMoving;
            }
            else if (index > _length.ValueInt)
            {
                decimal a = Math.Round(2.0m / (_length.ValueInt + 1), 8);
                decimal emaLast = _series.Values[index - 1];
                decimal p = candles[index].GetPoint(_candlePoint.ValueString);
                result = emaLast + (a * (p - emaLast));
            }

            _series.Values[index] = Math.Round(result, 8);
        }
    }
}