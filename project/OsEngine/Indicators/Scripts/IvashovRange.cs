using System;
using System.Collections.Generic;
using System.Drawing;
using OsEngine.Entity;
using OsEngine.Language;

namespace OsEngine.Indicators
{
    [Indicator("IvashovRange")]
    public class IvashovRange: Aindicator
    {
        public override string Description
        {
            get
            {
                string eng = "IvashovRange assesses instrument volatility as the ratio of the average true range to the moving average price, showing what share of the price the typical bar movement represents. " +
                             "Traders use it to compare volatility across instruments and filter trades: high values warn of elevated risk, low values indicate a calm market.";

                string ru = "IvashovRange оценивает волатильность инструмента как отношение среднего истинного диапазона к скользящей средней цены, показывая, какую долю от цены составляет типичное движение за бар. " +
                            "Трейдеры используют индикатор для сравнения волатильности разных инструментов и фильтрации сделок: высокое значение предупреждает о повышенном риске, низкое — о спокойном рынке.";

                return OsLocalization.ConvertToLocString($"Eng:{eng}_Ru:{ru}_");
            }
        }

        private IndicatorDataSeries _series;

        private IndicatorParameterInt _lengthMa;

        private IndicatorParameterInt _lengthAvg;

        public override void OnStateChange(IndicatorState state)
        {
            if (state == IndicatorState.Configure)
            {
                _lengthMa = CreateParameterInt("Length MA", 100);
                _lengthAvg = CreateParameterInt("Length average range", 100);
                _series = CreateSeries("Ivashov", Color.Blue, IndicatorChartPaintType.Line, true);
            }
            else if (state == IndicatorState.Dispose)
            {
                if (_averageList != null)
                {
                    _averageList.Clear();
                }
                if (_movingList != null)
                {
                    _movingList.Clear();
                }
                if (_range != null)
                {
                    _range.Clear();
                }
            }
        }

        public override void OnProcess(List<Candle> candles, int index)
        {
            _series.Values[index] = GetValue(candles, index);
        }

        private decimal GetValue(List<Candle> candles, int index)
        {
            if (index < 2)
            {
                if (_averageList != null)
                {
                    _averageList.Clear();
                }
                if (_movingList != null)
                {
                    _movingList.Clear();
                }
                if (_range != null)
                {
                    _range.Clear();
                }
            }

            while (index >= _movingList.Count)
            {
                _movingList.Add(CandlesMA(candles, index));
            }
            while (index>=_range.Count)
            {
                _range.Add(GetRange(candles, _movingList, index));
            }
            while (index >= _averageList.Count)
            {
                _averageList.Add(GetAvg(_range, index));
            }

            if (index < _lengthAvg.ValueInt ||
                index < _lengthMa.ValueInt ||
                _movingList[index] == 0)
            {
                return 0;
            }
            return _averageList[index];
        }

        private decimal CandlesMA(List<Candle> candles, int index)
        {
            if (_lengthMa.ValueInt > index)
            {
                return 0;
            }
            return candles.Summ(index - _lengthMa.ValueInt, index, "Close") / _lengthMa.ValueInt;
        }

        private decimal GetRange(List<Candle> candles, List<decimal> moving, int index)
        {
            if (moving[index] == 0)
            {
                return 0;
            }
            return Math.Abs(moving[index] - candles[index].Close);
        }

        private decimal GetAvg(List<decimal> list, int index)
        {
            decimal value = 0;
            if (index >= _lengthAvg.ValueInt)
            {

                decimal var = 0;
                for (int i = index - _lengthAvg.ValueInt + 1; i < index + 1; i++)
                {
                    var += list[i];
                }
                var = var / _lengthAvg.ValueInt;
                value = var;
            }
            return Math.Round(value, 4);

        }

        private List<decimal> _range = new List<decimal>();

        private List<decimal> _movingList = new List<decimal>();

        private List<decimal> _averageList = new List<decimal>();
    }
}