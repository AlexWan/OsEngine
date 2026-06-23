using OsEngine.Entity;
using OsEngine.Language;
using System;
using System.Collections.Generic;
using System.Drawing;

namespace OsEngine.Indicators
{
    [Indicator("RAVI")]
    public class RAVI : Aindicator
    {
        public override string Description
        {
            get
            {
                string eng = "RAVI measures the percentage difference between fast and slow moving averages, helping to determine trend strength and direction. " +
                             "Traders use RAVI as a trend filter: values above the upper level indicate a strong uptrend, below the lower level indicate a strong downtrend, and movement between levels signals a sideways market.";

                string ru = "RAVI измеряет разницу между быстрой и медленной скользящими средними в процентах, помогая определить силу и направление тренда. " +
                            "Трейдеры применяют RAVI как трендовый фильтр: значения выше верхнего уровня указывают на сильный восходящий тренд, ниже нижнего — на сильный нисходящий, а движение между уровнями сигнализирует о боковике.";

                return OsLocalization.ConvertToLocString($"Eng:{eng}_Ru:{ru}_");
            }
        }

        private IndicatorParameterInt _lengthSlow;

        private IndicatorParameterInt _lengthFast;

        private IndicatorParameterString _candlePoint;

        private IndicatorParameterDecimal _upLineParam;

        private IndicatorParameterDecimal _downLineParam;

        private IndicatorDataSeries _seriesSma;

        private IndicatorDataSeries _seriesUp;

        private IndicatorDataSeries _seriesDown;

        public override void OnStateChange(IndicatorState state)
        {
            if (state == IndicatorState.Configure)
            {
                _lengthSlow = CreateParameterInt("Slow line length", 65);
                _lengthFast = CreateParameterInt("Fast line length", 7);

                _upLineParam = CreateParameterDecimal("Up line", 3m);
                _downLineParam = CreateParameterDecimal("Down line", -3m);

                _candlePoint = CreateParameterStringCollection("Candle Point", "Close", OsEngine.Indicators.Entity.CandlePointsArray);

                _seriesSma = CreateSeries("EMA", Color.DarkRed, IndicatorChartPaintType.Line, true);

                _seriesUp = CreateSeries("Up Line", Color.LightYellow, IndicatorChartPaintType.Line, true);
                _seriesDown = CreateSeries("Down Line", Color.LightYellow, IndicatorChartPaintType.Line, true);

            }
        }

        public override void OnProcess(List<Candle> candles, int index)
        {
            _seriesUp.Values[index] = _upLineParam.ValueDecimal;
            _seriesDown.Values[index] = _downLineParam.ValueDecimal;

            if (_lengthFast.ValueInt + 3 > index || _lengthSlow.ValueInt + 3 > index)
            {
                _seriesSma.Values[index] = 0;

                return;
            }

            decimal smaSlow = CalcSma(candles, index, _lengthSlow.ValueInt);
            decimal smaFast = CalcSma(candles, index, _lengthFast.ValueInt);

            _seriesSma.Values[index] = Math.Round((smaFast - smaSlow) / smaSlow * 100, 5);

        }

        public decimal CalcSma(List<Candle> candles, int index, int length)
        {
            decimal sma = 0;

            for (int i = index - length; i < index; i++)
            {
                sma += candles[i].GetPoint(_candlePoint.ValueString);
            }
            decimal result = sma / length;

            return result;
        }
    }
}