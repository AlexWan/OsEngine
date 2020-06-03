using System.Collections.Generic;
using System.Drawing;
using OsEngine.Entity;

namespace OsEngine.Indicators
{
    class AdaptiveLookBack : Aindicator
    {
        private IndicatorParameterInt _length;
        private IndicatorDataSeries _series;

        public override void OnStateChange(IndicatorState state)
        {
            if (state == IndicatorState.Configure)
            {
                _length = CreateParameterInt("Period", 5);
                _series = CreateSeries("Adaptive Look Back", Color.WhiteSmoke, IndicatorChartPaintType.Line, true);
            }
            else
            {
                if (_swingBarArray != null)
                {
                    _swingBarArray.Clear();
                    _swingBarArray = null;
                }
            }
        }

        public override void OnProcess(List<Candle> candles, int index)
        {
            if (index == 0)
            {
                _swingBarArray = new List<int>();
                _lastUpdInex = index;
            }

            if (index != _lastUpdInex)
            {
                _series.Values[index] = GetValue(candles, index, false);
            }
            else
            {
                _series.Values[index] = GetValue(candles, index, true);
            }

            _lastUpdInex = index;
        }

        private int _lastUpdInex;

        private List<int> _swingBarArray = new List<int>();

        private decimal GetValue(List<Candle> candles, int index, bool updateOnly)
        {
            if (index < 5 ||
                          index < _length.ValueInt + 3)
            {
                return 0;
            }

            // SwingLo - true, if: index - 2 => fallen more than one bar in a row. && [index] =>  And by now, we're growing two in a row
            // SwingHi - true, if: index - 2 =>  grown more than one bar in a row. && [index] =>  And by now, we're already falling two in a row
            // SwingLo - true, если: index - 2 => падали больше одного бара подряд. && [index] => И к текущему моменту уже растём две подряд
            // SwingHi - true, если: index - 2 => росли больше одного бара подряд. && [index] => И к текущему моменту уже падаем две подряд

            bool swingLo = candles[index - 4].Low > candles[index - 3].Low &&
                           candles[index - 3].Low > candles[index - 2].Low &&
                           candles[index - 3].High >= candles[index - 2].High &&
                           candles[index - 2].High < candles[index - 1].High &&
                           candles[index - 1].High < candles[index].High;

            bool swingHi = candles[index - 4].High < candles[index - 3].High &&
                           candles[index - 3].High < candles[index - 2].High &&
                           candles[index - 3].Low <= candles[index - 2].Low &&
                           candles[index - 2].Low > candles[index - 1].Low &&
                           candles[index - 1].Low > candles[index].Low;

            int so = swingLo ? -1 : swingHi ? 1 : 0;

            if (so != 0)
            {
                // if we're turning around, we add candle to candle array with turns.
                // если у нас разворот, добавляем свечу в массив свечей с разворотами
                if (!updateOnly)
                {
                    _swingBarArray.Add(index);
                }
                else
                {
                    if (_swingBarArray.Count > 1)
                    {
                        _swingBarArray[_swingBarArray.Count - 1] = index;
                    }
                    else
                    {
                        _swingBarArray.Add(index);
                    }
                }


            }

            int lastSwingInCalc = (_swingBarArray.Count - _length.ValueInt);

            if (lastSwingInCalc >= 0)
            {
                return (index - _swingBarArray[lastSwingInCalc]) / _length.ValueInt;
            }

            return 0;
        }
    }
}