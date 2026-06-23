using OsEngine.Entity;
using OsEngine.Language;
using System.Collections.Generic;
using System.Drawing;

namespace OsEngine.Indicators
{
    [Indicator("DPO_Detrended_Price_Oscillator")]
    public class DPO_Detrended_Price_Oscillator : Aindicator
    {
        public override string Description
        {
            get
            {
                string eng = "The Detrended Price Oscillator subtracts a displaced moving average from price, removing the long-term trend and leaving only short-term cycles. " +
                             "Traders use DPO to find local cycles, overbought/oversold conditions within corrections, and entry points against short-term extremes.";

                string ru = "Детрендированный ценовой осциллятор вычитает из цены её смещённую скользящую среднюю, убирая долгосрочный тренд и оставляя только краткосрочные циклы. " +
                            "Трейдеры применяют DPO для поиска локальных циклов, перекупленности/перепроданности в рамках коррекций и точек входа против краткосрочных экстремумов.";

                return OsLocalization.ConvertToLocString($"Eng:{eng}_Ru:{ru}_");
            }
        }

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