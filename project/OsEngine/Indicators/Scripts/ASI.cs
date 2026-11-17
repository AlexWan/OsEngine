using OsEngine.Entity;
using System;
using System.Collections.Generic;
using System.Drawing;

namespace OsEngine.Indicators
{
    [Indicator("ASI")]
    public class ASI : Aindicator
    {
        public IndicatorParameterDecimal _limitMoves;

        public IndicatorParameterInt _lengthSma;

        public IndicatorDataSeries _seriesASI;

        public IndicatorDataSeries _seriesSma;

        public override void OnStateChange(IndicatorState state)
        {
            if (state == IndicatorState.Configure)
            {
                _limitMoves = CreateParameterDecimal("Limit Moves", 10000m);
                _lengthSma = CreateParameterInt("Period Sma", 14);

                _seriesASI = CreateSeries("Series ASI", Color.Green, IndicatorChartPaintType.Line, true);
                _seriesSma = CreateSeries("Series Sma", Color.Aqua, IndicatorChartPaintType.Line, true);
            }
        }

        public override void OnProcess(List<Candle> candles, int index)
        {

            if (index - 1 > candles.Count
                || index < 2
                || _lengthSma.ValueInt > index)
            {
                return;
            }

            decimal SI = CaclSI(candles, index);

            if (SI != 0)
            {
                SI = SI + _seriesASI.Values[index - 1];
            }

            _seriesASI.Values[index] = SI;

            _seriesSma.Values[index] = CaclSMAFromASI(index);

        }
        
        public decimal CaclSI(List<Candle> candles, int index)
        {
            decimal _lastClose = candles[index].Close;
            decimal _prevClose = candles[index - 1].Close;
            decimal _lastOpen = candles[index].Open;
            decimal _prevOpen = candles[index - 1].Open;
            decimal _lastHigh = candles[index].High;
            decimal _lastLow = candles[index].Low;

            decimal H_Cprev = Math.Abs(_lastHigh - _prevClose);
            decimal L_Cprev = Math.Abs(_lastLow - _prevClose);
            decimal H_L = Math.Abs(_lastHigh - _lastLow);
            decimal Cprev_Oprev = Math.Abs(_prevClose - _prevOpen);

            decimal K = Math.Max(H_Cprev, L_Cprev);

            decimal R = 0;

            if (H_Cprev >= Math.Max(L_Cprev, H_L))
            {
                R = H_Cprev - (0.5m * L_Cprev) + (0.25m * Cprev_Oprev);
            }
            else if (L_Cprev >= Math.Max(H_Cprev, H_L))
            {
                R = L_Cprev - (0.5m * H_Cprev) + (0.25m * Cprev_Oprev);
            }
            else if (H_L >= Math.Max(H_Cprev, L_Cprev))
            {
                R = H_L + (0.25m * Cprev_Oprev);
            }

            if (R == 0)
            {
                return 0;
            }
            decimal SI = 50 * ((_lastClose - _prevClose + 0.5m * (_lastClose - _lastOpen) + 0.25m * (_prevClose - _prevOpen)) / R) * (K / _limitMoves.ValueDecimal);

            return SI;
        }

        public decimal CaclSMAFromASI(int index)
        {
            decimal sma = 0;
            for (int i = index - _lengthSma.ValueInt + 1; i <= index; i++)
            {
                sma += _seriesASI.Values[i];
            }
            sma = sma / _lengthSma.ValueInt;

            return sma;
        }
    }
}