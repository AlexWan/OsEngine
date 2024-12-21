using OsEngine.Entity;
using System.Collections.Generic;
using System.Drawing;

namespace OsEngine.Indicators
{
    [Indicator("DeltaByCandles")]
    public class DeltaByCandles : Aindicator
    {
        private IndicatorDataSeries _seriesSmaDelta;

        private IndicatorDataSeries _seriesTrade;

        private IndicatorDataSeries _seriesBuy;

        private IndicatorDataSeries _seriesSell;

        private IndicatorDataSeries _seriesDelta;

        private IndicatorParameterInt _lengthSmaDelta;

        public override void OnStateChange(IndicatorState state)
        {
            if (state == IndicatorState.Configure)
            {
                _lengthSmaDelta = CreateParameterInt("Length Sma Delta", 10);

                _seriesSmaDelta = CreateSeries("Sma Delta", Color.White, IndicatorChartPaintType.Column, true);
                _seriesDelta = CreateSeries("Delta", Color.Blue, IndicatorChartPaintType.Line, true);
                _seriesTrade = CreateSeries("Sum volume", Color.Black, IndicatorChartPaintType.Line, true);
                _seriesBuy = CreateSeries("Buy volume", Color.Green, IndicatorChartPaintType.Line, true);
                _seriesSell = CreateSeries("Sell volume", Color.Red, IndicatorChartPaintType.Line, true);
            }
            else if (state == IndicatorState.Dispose)
            {
                _seriesTrade = null;
                _seriesBuy = null;
                _seriesSell = null;
                _seriesDelta = null;
                _lengthSmaDelta = null;
            }
        }

        public override void OnProcess(List<Candle> candles, int index)
        {
            if (candles[index].Trades == null)
            {
                return;
            }

            _seriesBuy.Values[index] = GetTradesInfo(candles, index, Side.Buy);
            _seriesSell.Values[index] = GetTradesInfo(candles, index, Side.Sell);
            _seriesDelta.Values[index] = _seriesBuy.Values[index] - _seriesSell.Values[index];
            _seriesTrade.Values[index] = _seriesBuy.Values[index] + _seriesSell.Values[index];
            _seriesSmaDelta.Values[index] = Sma(_seriesDelta.Values, index);
        }

        private decimal GetTradesInfo(List<Candle> candles, int index, Side side)
        {
            decimal value = 0;

            if (side == Side.Buy)
            {
                for (int i = 0; i < candles[index].Trades.Count; i++)
                {
                    if (candles[index].Trades[i].Side == Side.Buy)
                    {
                        value += candles[index].Trades[i].Volume;
                    }
                }

                return value;
            }
            else
            {
                for (int i = 0; i < candles[index].Trades.Count; i++)
                {
                    if (candles[index].Trades[i].Side == Side.Sell)
                    {
                        value += candles[index].Trades[i].Volume;
                    }
                }

                return value;
            }
        }

        private decimal Sma(List<decimal> value, int index)
        {
            if (index < _lengthSmaDelta.ValueInt)
            {
                return 0;
            }

            decimal values = 0;

            for (int i = 0; i < _lengthSmaDelta.ValueInt - 1; i++)
            {
                values += value[index - i];
            }

            return values / _lengthSmaDelta.ValueInt;
        }
    }
}