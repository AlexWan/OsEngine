using System.Collections.Generic;
using System.Drawing;
using OsEngine.Entity;
using OsEngine.Language;

namespace OsEngine.Indicators
{
    [Indicator("NRTR")]
    public class NRTR : Aindicator
    {
        public override string Description
        {
            get
            {
                string eng = "NRTR tracks local highs and lows over a period and builds dynamic support/resistance levels with a set deviation, switching between bullish and bearish mode when a level is broken. " +
                             "Traders use NRTR as a trend filter and for trailing stops: they follow the trend as long as price stays on the appropriate side of the line.";

                string ru = "NRTR отслеживает локальные максимумы и минимумы за период и строит динамические уровни поддержки/сопротивления с заданным отклонением, переключаясь между бычьим и медвежьим режимом при пробое уровня. " +
                            "Трейдеры применяют NRTR как трендовый фильтр и для trailing-stop: следуют за трендом, пока цена держится с соответствующей стороны линии.";

                return OsLocalization.ConvertToLocString($"Eng:{eng}_Ru:{ru}_");
            }
        }

        private IndicatorParameterInt _period;

        private IndicatorParameterDecimal _deviation;

        private IndicatorDataSeries _seriesUp;

        private IndicatorDataSeries _seriesDown;

        private IndicatorDataSeries _seriesCenter;

        private int _direction;

        private List<int> _directions;

        public override void OnStateChange(IndicatorState state)
        {
            _period = CreateParameterInt("Length", 15);
            _deviation = CreateParameterDecimal("Deviation %", 1.0m);

            _seriesUp = CreateSeries("Up line", Color.Aqua, IndicatorChartPaintType.Line, false);
            _seriesDown = CreateSeries("Down line", Color.BlueViolet, IndicatorChartPaintType.Line, false);
            _seriesCenter = CreateSeries("Center Line ", Color.Red, IndicatorChartPaintType.Line, true);

            _directions = new List<int>();
        }

        public override void OnProcess(List<Candle> candles, int index)
        {
            if (index < _period.ValueInt)
            {
                return;
            }

            CheckDirectionsLength(index);

            decimal closePrice = candles[index].Close;
            decimal closePricePrew = candles[index - 1].Close;

            decimal maxHigh = candles[index].High;

            for (int i = index; i > -1 && i > index - _period.ValueInt; i--)
            {
                if (maxHigh < candles[i].High)
                {
                    maxHigh = candles[i].High;
                }
            }

            decimal minLow = candles[index].Low;

            for (int i = index; i > -1 && i > index - _period.ValueInt; i--)
            {
                if (minLow > candles[i].Low)
                {
                    minLow = candles[i].Low;
                }
            }

            decimal upLine = maxHigh * (1 - _deviation.ValueDecimal / 100);
            decimal downLine = minLow * (1 + _deviation.ValueDecimal / 100);

            _seriesUp.Values[index] = upLine;
            _seriesDown.Values[index] = downLine;

            if (index == _period.ValueInt || _seriesCenter.Values[index - 1] == 0)
            {
                _direction = closePrice >= closePricePrew ? 1 : -1;
                _seriesCenter.Values[index] = _direction == 1 ? upLine : downLine;
                _directions[index] = _direction;
                return;
            }

            int direction = _directions[index - 1];

            if (direction == 0)
            {
                direction = closePricePrew >= _seriesCenter.Values[index - 1] ? 1 : -1;
            }

            decimal prevCenter = _seriesCenter.Values[index - 1];

            if (direction == 1)
            {
                if (closePrice < prevCenter)
                {
                    direction = -1;
                    _seriesCenter.Values[index] = downLine;
                }
                else
                {
                    _seriesCenter.Values[index] = upLine > prevCenter ? upLine : prevCenter;
                }
            }
            else
            {
                if (closePrice > prevCenter)
                {
                    direction = 1;
                    _seriesCenter.Values[index] = upLine;
                }
                else
                {
                    _seriesCenter.Values[index] = downLine < prevCenter ? downLine : prevCenter;
                }
            }

            _direction = direction;
            _directions[index] = direction;
        }

        private void CheckDirectionsLength(int index)
        {
            while (_directions.Count <= index)
            {
                _directions.Add(0);
            }
        }
    }
}
