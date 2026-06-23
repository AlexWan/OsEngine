using System;
using System.Collections.Generic;
using System.Drawing;
using OsEngine.Entity;
using OsEngine.Language;

namespace OsEngine.Indicators
{
    [Indicator("AO")]
    public class AO:Aindicator
    {
        public override string Description
        {
            get
            {
                string eng = "AO plots the difference between a fast (5 periods) and a slow (32 periods) smoothed typical price, displaying current market momentum as a histogram. " +
                             "Traders use it to spot reversals via the saucer, zero-line cross, and twin peaks patterns, as well as to confirm trend strength before entering a trade.";

                string ru = "AO строит разницу между быстрой (5 периодов) и медленной (32 периода) сглаженной типичной ценой, отображая текущий рыночный импульс в виде гистограммы. " +
                            "Трейдеры применяют его для поиска разворотов по паттернам «блюдце», «пересечение нулевой линии» и двум пикам, а также для подтверждения силы тренда перед входом.";

                return OsLocalization.ConvertToLocString($"Eng:{eng}_Ru:{ru}_");
            }
        }

        private IndicatorParameterInt _lengthFastLine;

        private IndicatorParameterInt _lengthSlowLine;

        public IndicatorParameterString _candlePoint;

        private IndicatorDataSeries _series;

        private Aindicator _emaSlow;

        private Aindicator _emaFast;

        public override void OnStateChange(IndicatorState state)
        {
            if (state == IndicatorState.Configure)
            {
                _lengthFastLine = CreateParameterInt("Fast line length", 5);
                _lengthSlowLine = CreateParameterInt("Slow line length", 32);
                _candlePoint = CreateParameterStringCollection("Candle point", "Typical", Entity.CandlePointsArray);

                _series = CreateSeries("AO", Color.DarkGreen, IndicatorChartPaintType.Column, true);

                _emaFast = IndicatorsFactory.CreateIndicatorByName("Ema", Name + "Ema fast", false);
                _emaFast.Parameters[0].Bind(_lengthFastLine);
                _emaFast.Parameters[1].Bind(_candlePoint);
                ProcessIndicator("Ema fast", _emaFast);

                _emaSlow = IndicatorsFactory.CreateIndicatorByName("Ema", Name + "Ema slow", false);
                _emaSlow.Parameters[0].Bind(_lengthSlowLine);
                _emaSlow.Parameters[1].Bind(_candlePoint);
                ProcessIndicator("Ema slow", _emaSlow);
            }
        }

        public override void OnProcess(List<Candle> candles, int index)
        {
            _series.Values[index] = GetValue(candles, index);
        }

        private decimal GetValue(List<Candle> candles, int index)
        {
            if (index - _lengthSlowLine.ValueInt <= 0 ||
                index - _lengthFastLine.ValueInt <= 0)
            {
                return 0;
            }

            return Math.Round(_emaFast.DataSeries[0].Values[index] - _emaSlow.DataSeries[0].Values[index], 6);
        }
    }
}