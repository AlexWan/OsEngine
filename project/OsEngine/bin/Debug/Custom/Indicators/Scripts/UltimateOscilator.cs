using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using OsEngine.Entity;
using OsEngine.Indicators;

namespace CustomIndicators.Scripts
{
    public class UltimateOscilator:Aindicator
    {
        private IndicatorDataSeries series1;
        private IndicatorParameterInt period1;
        private IndicatorParameterInt period2;
        private IndicatorParameterInt period3;

        public override void OnStateChange(IndicatorState state)
        {
            if (state == IndicatorState.Configure)
            {
                period1 = CreateParameterInt("Period 1", 7);
                period2 = CreateParameterInt("Period 2", 14);
                period3 = CreateParameterInt("Period 3", 28);
                series1 = CreateSeries("UO", Color.DodgerBlue, IndicatorChartPaintType.Line, true);
            }
        }

        public override void OnProcess(List<Candle> candles, int index)
        {
            series1.Values[index] = GetValue(candles, index);
        }

        private decimal GetValue(List<Candle> candles, int index)
        {
            if (index == 0)
            {
                _bp = new List<decimal>();
                _tr = new List<decimal>();
            }

            if (index < period1.ValueInt ||
                index < period2.ValueInt ||
                index < period3.ValueInt)
            {
                return 0;
            }

            ReloadBuyingPressure(candles, index);
            ReloadTrueRange(candles, index);

            decimal bpPer1 = SummList(index - period1.ValueInt, index, _bp);
            decimal trPer1 = SummList(index - period1.ValueInt, index, _tr);

            decimal bpPer2 = SummList(index - period2.ValueInt, index, _bp);
            decimal trPer2 = SummList(index - period2.ValueInt, index, _tr);

            decimal bpPer3 = SummList(index - period3.ValueInt, index, _bp);
            decimal trPer3 = SummList(index - period3.ValueInt, index, _tr);

            if (trPer1 == 0 ||
                trPer2 == 0 ||
                trPer3 == 0)
            {
                return 0;
            }

            decimal average7 = bpPer1 / trPer1;
            decimal average14 = bpPer2 / trPer2;
            decimal average28 = bpPer3 / trPer3;

            return 100 * ((4 * average7) + (2 * average14) + average28) / (4 + 3 + 2);
        }

        private decimal SummList(int indxStart, int indxEnd, List<decimal> array)
        {
            decimal result = 0;

            for (int i = indxStart; i < array.Count && i < indxEnd + 1; i++)
            {
                result += array[i];
            }

            return result;
        }

        List<decimal> _bp = new List<decimal>();

        private void ReloadBuyingPressure(List<Candle> candles, int index)
        {
            // Buying Pressure(BP) = Close - Minimum(Lowest between Current Low or Previous Close) 
            //=Закрытие - Минимальное (минимальное между текущим или предыдущим закрытием)

            decimal result = candles[index].Close - Math.Min(candles[index].Low, candles[index - 1].Close);

            while (_bp.Count <= index)
            {
                _bp.Add(0);
            }

            _bp[index] = result;
        }

        List<decimal> _tr = new List<decimal>();

        private void ReloadTrueRange(List<Candle> candles, int index)
        {
            decimal hiToLow = Math.Abs(candles[index].High - candles[index].Low);
            decimal closeToHigh = Math.Abs(candles[index - 1].Close - candles[index].High);
            decimal closeToLow = Math.Abs(candles[index - 1].Close - candles[index].Low);

            decimal result = Math.Max(Math.Max(hiToLow, closeToHigh), closeToLow);

            while (_tr.Count <= index)
            {
                _tr.Add(0);
            }

            _tr[index] = result;
        }
    }
}
