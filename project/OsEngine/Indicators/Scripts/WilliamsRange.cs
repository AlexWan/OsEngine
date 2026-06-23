using System;
using System.Collections.Generic;
using System.Drawing;
using OsEngine.Entity;
using OsEngine.Language;

namespace OsEngine.Indicators
{
    [Indicator("WilliamsRange")]
    public class WilliamsRange : Aindicator
    {
        public override string Description
        {
            get
            {
                string eng = "Williams %R measures the position of the current closing price relative to the high/low range over a period, outputting an oscillator from 0 to -100 as a mirror image of Stochastic. " +
                             "Traders use the line moving into the 0–20 and -80–-100 zones to find overbought/oversold conditions and reversals.";

                string ru = "Williams %R измеряет положение текущей цены закрытия относительно диапазона high/low за период, выводя осциллятор от 0 до -100 в зеркальном отображении к стохастику. " +
                            "Трейдеры используют выход линии в зоны 0–20 и -80–-100 для поиска перекупленности/перепроданности и разворотов.";

                return OsLocalization.ConvertToLocString($"Eng:{eng}_Ru:{ru}_");
            }
        }

        private IndicatorParameterInt _period;

        private IndicatorDataSeries _series;

        public override void OnStateChange(IndicatorState state)
        {
            if (state == IndicatorState.Configure)
            {
                _series = CreateSeries("WR", Color.DodgerBlue, IndicatorChartPaintType.Line, true);
                _period = CreateParameterInt("Period", 14);
            }
        }

        private List<decimal> _high = new List<decimal>();

        private List<decimal> _low = new List<decimal>();

        public override void OnProcess(List<Candle> candles, int index)
        {
            _series.Values[index] = GetValue(candles, index);
        }

        private decimal GetValue(List<Candle> candles, int index)
        {
            if (index < _period.ValueInt)
            {
                return 0;
            }

            while (index + 1 > _high.Count)
            {
                _high.Add(0);
                _low.Add(0);
            }

            _high[index] = GetHigh(candles, index);
            _low[index] = GetLow(candles, index);

            if (_high[index] - _low[index] == 0)
            {
                return _series.Values[_series.Values.Count - 1];
            }
            return Math.Round(-100 * (_high[index] - candles[index].Close) / (_high[index] - _low[index]), 2);

        }

        private decimal GetHigh(List<Candle> candles, int index)
        {
            if (index < _period.ValueInt)
            {
                return 0;
            }

            decimal maxhigh = 0;

            for (int i = index - _period.ValueInt + 1; i < index + 1; i++)
            {

                if (maxhigh < candles[i].High)
                {
                    maxhigh = candles[i].High;
                }
            }
            return maxhigh;
        }

        private decimal GetLow(List<Candle> candles, int index)
        {
            if (index < _period.ValueInt)
            {
                return 0;
            }

            decimal maxlow = decimal.MaxValue;

            for (int i = index - _period.ValueInt + 1; i < index + 1; i++)
            {
                if (maxlow > candles[i].Low)
                {
                    maxlow = candles[i].Low;
                }
            }
            return maxlow;
        }
    }
}