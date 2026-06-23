using System;
using System.Collections.Generic;
using System.Drawing;
using OsEngine.Entity;
using OsEngine.Language;

namespace OsEngine.Indicators
{
    [Indicator("CMO")]
    public class CMO : Aindicator
    {
        public override string Description
        {
            get
            {
                string eng = "CMO calculates a momentum index based on the difference between total positive and negative price changes over a period, displaying the strength of bulls and bears as an oscillator. " +
                             "Traders use CMO to identify overbought and oversold conditions, filter trends, and find divergences with the price chart.";

                string ru = "CMO рассчитывает индекс денежного потока на основе разницы между суммарными положительными и отрицательными изменениями цены за период, отображая силу быков и медведей в виде осциллятора. " +
                            "Трейдеры применяют CMO для определения перекупленности и перепроданности, фильтрации тренда и поиска дивергенций с ценовым графиком.";

                return OsLocalization.ConvertToLocString($"Eng:{eng}_Ru:{ru}_");
            }
        }

        private IndicatorDataSeries _series;
        private IndicatorParameterInt _period;

        private List<decimal> _cmo1;
        private List<decimal> _cmo2;

        public override void OnStateChange(IndicatorState state)
        {
            if (state == IndicatorState.Configure)
            {
                _period = CreateParameterInt("Period", 14);
                _series = CreateSeries("CMO", Color.Orange, IndicatorChartPaintType.Line, true);
            }
            else if (state == IndicatorState.Dispose)
            {
                _cmo1?.Clear();
                _cmo2?.Clear();
            }
        }

        public override void OnProcess(List<Candle> candles, int index)
        {
            if (index == 0)
            {
                _cmo1 = new List<decimal>();
                _cmo2 = new List<decimal>();
            }

            while (_cmo1.Count <= index)
            {
                _cmo1.Add(0);
            }

            while (_cmo2.Count <= index)
            {
                _cmo2.Add(0);
            }

            decimal cmoValue = CalculateCMO(candles, index);
            _series.Values[index] = Math.Round(cmoValue, 2);
        }

        private decimal CalculateCMO(List<Candle> candles, int index)
        {
            if (index == 0)
            {
                _cmo1[0] = 0;
                _cmo2[0] = 0;
                return 0;
            }

            decimal priceChange = candles[index].Close - candles[index - 1].Close;

            if (priceChange > 0)
            {
                _cmo1[index] = priceChange;
                _cmo2[index] = 0;
            }
            else if (priceChange < 0)
            {
                _cmo1[index] = 0;
                _cmo2[index] = Math.Abs(priceChange);
            }
            else
            {
                _cmo1[index] = 0;
                _cmo2[index] = 0;
            }

            if (index < _period.ValueInt)
            {
                return 0;
            }

            decimal sumPositive = 0;
            decimal sumNegative = 0;

            for (int i = index - _period.ValueInt + 1; i > 0 && i <= index; i++)
            {
                sumPositive += _cmo1[i];
                sumNegative += _cmo2[i];
            }

            if (sumPositive + sumNegative == 0)
            {
                return 0;
            }

            decimal cmo = 100 * (sumPositive - sumNegative) / (sumPositive + sumNegative);
            return cmo;
        }
    }
}