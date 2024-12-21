using OsEngine.Entity;
using System.Collections.Generic;
using System.Drawing;

namespace OsEngine.Indicators
{
    [Indicator("DPO_Detrended_Price_Oscillator")]
    public class DPO_Detrended_Price_Oscillator : Aindicator
    {
        private IndicatorParameterInt _lengthSma;

        private IndicatorParameterString _candlePoint;

        private IndicatorDataSeries _series;

        public override void OnStateChange(IndicatorState state)
        {
            if (state == IndicatorState.Configure)
            {
                _lengthSma = CreateParameterInt("Length Sma", 14);
                _candlePoint = CreateParameterStringCollection("Candle Point", "Close", OsEngine.Indicators.Entity.CandlePointsArray);
                _series = CreateSeries("Ma", Color.DodgerBlue, IndicatorChartPaintType.Line, true);
            }
        }

        public override void OnProcess(List<Candle> candles, int index)
        {
            if (index < _lengthSma.ValueInt)
                return;

            decimal X_PrevPeriod = candles[index - (_lengthSma.ValueInt / 2) + 1].GetPoint(_candlePoint.ValueString);
            decimal sma = CalcSMA(candles, index);
            decimal DPO = X_PrevPeriod - sma;

            _series.Values[index] = DPO;
        }

        public decimal CalcSMA(List<Candle> candles, int index)
        {
            decimal SMA = 0;

            if (_lengthSma.ValueInt > index)
            {
                SMA = 0;
            }
            else
                SMA = candles.Summ(index - _lengthSma.ValueInt, index, _candlePoint.ValueString) / _lengthSma.ValueInt;

            return SMA;
        }
    }
}