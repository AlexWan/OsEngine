using System.Collections.Generic;
using System.Drawing;
using OsEngine.Entity;
using OsEngine.Language;

namespace OsEngine.Indicators
{
    [Indicator("Ssma")]
    public class Ssma : Aindicator
    {
        public override string Description
        {
            get
            {
                string eng = "SSMA smooths price data, giving more weight to recent values than a regular SMA but less than EMA, making the line smoother. " +
                             "Traders use SSMA to determine trend direction and find entry points on crosses with price or other moving averages.";

                string ru = "SSMA сглаживает ценовые данные, придавая больше веса последним значениям по сравнению с обычной SMA, но меньше, чем EMA, что делает линию более плавной. " +
                            "Трейдеры используют SSMA для определения направления тренда и поиска точек входа на пересечении с ценой или другими скользящими средними.";

                return OsLocalization.ConvertToLocString($"Eng:{eng}_Ru:{ru}_");
            }
        }

        private IndicatorParameterInt _length;

        private IndicatorParameterString _candlePoint;

        private IndicatorDataSeries _series;

        public override void OnStateChange(IndicatorState state)
        {
            _length = CreateParameterInt("Length", 14);
            _candlePoint = CreateParameterStringCollection("Candle Point", "Close", Entity.CandlePointsArray);
            _series = CreateSeries("Ma", Color.DodgerBlue, IndicatorChartPaintType.Line, true);
        }

        public override void OnProcess(List<Candle> candles, int index)
        {
            decimal result = 0;

            if (index == _length.ValueInt)
            {
                decimal lastMoving = 0;

                for (int i = index - _length.ValueInt + 1; i < index + 1; i++)
                {
                    lastMoving += candles[i].GetPoint(_candlePoint.ValueString);
                }

                lastMoving = lastMoving / _length.ValueInt;

                result = lastMoving;
            }
            else if (index > _length.ValueInt)
            {
                decimal ssmaLast = _series.Values[index - 1];

                decimal p = candles[index].GetPoint(_candlePoint.ValueString);

                result = (ssmaLast * (_length.ValueInt - 1) + p) / _length.ValueInt;
            }

            _series.Values[index] = result;
        }
    }
}