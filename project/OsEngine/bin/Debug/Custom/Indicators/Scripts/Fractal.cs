using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using OsEngine.Entity;
using OsEngine.Indicators;

namespace CustomIndicators.Scripts
{
    public class Fractal: Aindicator
    {
        private IndicatorDataSeries _seriesUp;
        private IndicatorDataSeries _seriesDown;

        public override void OnStateChange(IndicatorState state)
        {
            if (state == IndicatorState.Configure)
            {
                _seriesDown = CreateSeries("FractalDown", Color.OrangeRed, IndicatorChartPaintType.Point, true);
                _seriesUp = CreateSeries("FractalUp", Color.DeepSkyBlue, IndicatorChartPaintType.Point, true);
                _seriesDown.CanReBuildHistoricalValues = true;
                _seriesUp.CanReBuildHistoricalValues = true;
            }
            else if (state == IndicatorState.Dispose)
            {
                if (ValuesUp != null)
                {
                    ValuesUp.Clear();
                }
                if (ValuesDown != null)
                {
                    ValuesDown.Clear();
                }
            }
        }

        public override void OnProcess(List<Candle> candles, int index)
        {
            if (index - 5 <= 0)
            {
                return;
            }

            _seriesUp.Values[index-2] = GetValueUp(candles, index);
            _seriesDown.Values[index-2] = GetValueDown(candles, index);
        }

        private decimal GetValueUp(List<Candle> candles, int index)
        {
            // fractal considered to be formed only after two candles have passed
            // фрактал у нас считается сформированным только после прошедших уже двух свечей
            // looking at trird candle from index
            // т.ч. смотрим трейтью свечу от индекса
            if (index - 5 <= 0)
            {
                return 0;
            }

            if (candles[index - 2].High >= candles[index - 1].High &&
                candles[index - 2].High >= candles[index].High &&
                candles[index - 2].High >= candles[index - 3].High &&
                candles[index - 2].High >= candles[index - 4].High)
            {
                return candles[index - 2].High;
            }



            return 0;
        }

        /// <summary>
        /// take lower value of indicator by index
        /// взять нижнее значение индикатора по индексу
        /// </summary>
        private decimal GetValueDown(List<Candle> candles, int index)
        {
            // fractal considered to be formed only after two candles have passed
            // фрактал у нас считается сформированным только после прошедших уже двух свечей
            // looking at trird candle from index
            // т.ч. смотрим трейтью свечу от индекса
            if (index - 5 <= 0)
            {
                return 0;
            }

            if (candles[index - 2].Low <= candles[index - 1].Low &&
                candles[index - 2].Low <= candles[index].Low &&
                candles[index - 2].Low <= candles[index - 3].Low &&
                candles[index - 2].Low <= candles[index - 4].Low)
            {
                return candles[index - 2].Low;
            }



            return 0;
        }


        private List<decimal> ValuesUp = new List<decimal>();
        private List<decimal> ValuesDown = new List<decimal>();

    }

}
