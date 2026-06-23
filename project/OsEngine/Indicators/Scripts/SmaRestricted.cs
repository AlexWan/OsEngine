using System.Collections.Generic;
using System.Drawing;
using OsEngine.Entity;
using OsEngine.Indicators;
using OsEngine.Language;

namespace CustomIndicators.Scripts
{
    public class SmaRestricted : Aindicator
    {
        public override string Description
        {
            get
            {
                string eng = "SmaRestricted calculates a simple moving average excluding candles that fall within a set time filter (for example, late trading hours) to avoid distortion during inactive periods. " +
                             "Traders use it to get a cleaner trend signal during main trading hours.";

                string ru = "SmaRestricted рассчитывает простую скользящую среднюю, исключая из расчёта свечи, попадающие в заданный временной фильтр (например, поздние часы торгов), чтобы избежать искажений в неактивные периоды. " +
                            "Трейдеры применяют её для получения более чистого сигнала тренда в основные торговые часы.";

                return OsLocalization.ConvertToLocString($"Eng:{eng}_Ru:{ru}_");
            }
        }

        private IndicatorParameterInt _length;

        private IndicatorParameterString _candlePoint;

        private IndicatorDataSeries _series;

        public override void OnStateChange(IndicatorState state)
        {
            if (state == IndicatorState.Configure)
            {
                _length = CreateParameterInt("Length", 14);
                _candlePoint = CreateParameterStringCollection("Candle Point", "Close", Entity.CandlePointsArray);
                _series = CreateSeries("Ma", Color.DodgerBlue, IndicatorChartPaintType.Line, true);
            }
        }

        public override void OnProcess(List<Candle> candles, int index)
        {
            if (_length.ValueInt > index || candles[index].TimeStart.Hour >= 19)
            {
                _series.Values[index] = 0;
                return;
            }

            decimal summ = 0;

            int countPoints = 0;

            for (int i = index; i >= 0 && i > index - _length.ValueInt; i--)
            {
                // filterOut candles
                if (candles[i].TimeStart.Hour >= 19)
                    continue;
                
                countPoints++;
                summ += candles[i].Close;
            }

            decimal sma = 0;

            if (countPoints > 0)
            {
                sma = summ / countPoints;
            }

            _series.Values[index] = sma;
        }
    }
}