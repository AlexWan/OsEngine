using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using OsEngine.Entity;
using OsEngine.Indicators;


namespace CustomIndicators.Scripts
{
    public class Trix:Aindicator
    {
        public IndicatorDataSeries _series;
        public IndicatorParameterInt _period;
        public IndicatorParameterString _candlePoint;

        public override void OnStateChange(IndicatorState state)
        {

            if (state == IndicatorState.Configure)
            {
                _period = CreateParameterInt("Length", 9);
                _candlePoint = CreateParameterStringCollection("Candle point", "Close", Entity.CandlePointsArray);
                _series = CreateSeries("Trix", Color.Aqua, IndicatorChartPaintType.Line, true);
            }
        }

        public override void OnProcess(List<Candle> candles, int index)
        {
            _series.Values[index] = GetValue(candles,index);
        }
        private decimal GetValue(List<Candle> candles, int index)
        {
            if (index == 1)
            {
                prices.Clear();
                _vtrixMa1.Clear();
                _vtrixMa2.Clear();
                _vtrixMa3.Clear();
            }

            while(index >= prices.Count)
            { prices.Add(candles[index].GetPoint(_candlePoint.ValueString)); }

            while (index >= _vtrixMa1.Count)
            { _vtrixMa1.Add(GetEma1(index, prices)); }

            while (index >= _vtrixMa2.Count)
            { _vtrixMa2.Add(GetEma2(index,_vtrixMa1)); }

            while (index >= _vtrixMa3.Count)
            { _vtrixMa3.Add(GetEma3(index, _vtrixMa2)); }

            if (index <= _period.ValueInt)
            {
                return 0;
            }

            if (index < _period.ValueInt * 7 - 1 || (index <2 && index>0)|| index >= _vtrixMa3.Count || _vtrixMa3[index - 1] == 0 || _vtrixMa3[index] == 0)
            {
                return 0;
            }

            decimal value = (_vtrixMa3[index] - _vtrixMa3[index - 1]) * 100 / _vtrixMa3[index - 1];

            return Math.Round(value, 4);
        }

        private decimal GetEma1(int index, List<decimal> list)
        {
            decimal result = 0;
            if(index<_period.ValueInt)
            {
                return 0;
            }
            if (index == _period.ValueInt)
            {
                decimal lastMoving = 0;

                for (int i = index - _period.ValueInt + 1; i < index+1; i++)
                {
                    lastMoving += list[i];
                }

                lastMoving = lastMoving / _period.ValueInt;
                result = lastMoving;
                return Math.Round(result, 8);
            }
            if (index > _period.ValueInt)
            {
                decimal a = Math.Round(2.0m / (_period.ValueInt + 1), 8);
                decimal emaLast = _vtrixMa1[index - 1];
                decimal p = list[index];
                result = emaLast + (a * (p - emaLast));
                return Math.Round(result, 8);
            }
            return Math.Round(result, 8);
        }
        private decimal GetEma2(int index, List<decimal> list)
        {
            decimal result = 0;
            if (index < _period.ValueInt)
            {
                return 0;
            }
            if (index == _period.ValueInt)
            {
                decimal lastMoving = 0;

                for (int i = index - _period.ValueInt + 1; i < index + 1; i++)
                {
                    lastMoving += list[i];
                }

                lastMoving = lastMoving / _period.ValueInt;
                result = lastMoving;
                return Math.Round(result, 8);
            }
            if (index > _period.ValueInt)
            {
                decimal a = Math.Round(2.0m / (_period.ValueInt + 1), 8);
                decimal emaLast = _vtrixMa2[index - 1];
                decimal p = list[index];
                result = emaLast + (a * (p - emaLast));
                return Math.Round(result, 8);
            }
            return Math.Round(result, 8);
        }
        private decimal GetEma3(int index, List<decimal> list)
        {
            decimal result = 0;
            if (index < _period.ValueInt)
            {
                return 0;
            }
            if (index == _period.ValueInt)
            {
                decimal lastMoving = 0;

                for (int i = index - _period.ValueInt + 1; i < index + 1; i++)
                {
                    lastMoving += list[i];
                }

                lastMoving = lastMoving / _period.ValueInt;
                result = lastMoving;
                return Math.Round(result, 8);
            }
            if (index > _period.ValueInt)
            {
                decimal a = Math.Round(2.0m / (_period.ValueInt + 1), 8);
                decimal emaLast = _vtrixMa3[index - 1];
                decimal p = list[index];
                result = emaLast + (a * (p - emaLast));
                return Math.Round(result, 8);
            }
            return Math.Round(result, 8);
        }

        private List<decimal> _vtrixMa1 = new List<decimal>();

        private List<decimal> _vtrixMa2 = new List<decimal>();

        private List<decimal> _vtrixMa3 = new List<decimal>();

        private List<decimal> prices = new List<decimal>();
    }
}
