using OsEngine.Entity;
using System.Collections.Generic;
using System.Drawing;

namespace OsEngine.Indicators
{
    [Indicator("DeMarker_DeM")]
    public class DeMarker_DeM : Aindicator
    {
        /// <summary>
        /// Calculation period Sma
        /// </summary>
        public IndicatorParameterInt _lengthSma;
        /// <summary>
        /// Up line parameter
        /// </summary>
        public IndicatorParameterDecimal _UpLineParam;
        /// <summary>
        /// Down line parameter
        /// </summary>
        public IndicatorParameterDecimal _DownLineParam;
        /// <summary>
        /// Data series for indicator output
        /// </summary>
        public IndicatorDataSeries _seriesDMark;
        /// <summary>
        /// Data series for indicator output
        /// </summary>
        public IndicatorDataSeries _seriesUpLine;
        /// <summary>
        /// Data series for indicator output
        /// </summary>
        public IndicatorDataSeries _seriesDownLine;
        public override void OnStateChange(IndicatorState state)
        {
            if (state == IndicatorState.Configure)
            {
                _lengthSma = CreateParameterInt("Period Sma", 14);

                _UpLineParam = CreateParameterDecimal("Up Line parameter", 0.7m);
                _DownLineParam = CreateParameterDecimal("Down line parameter", 0.3m);

                _seriesDMark = CreateSeries("Series DMark", Color.Aqua, IndicatorChartPaintType.Line, true);

                _seriesUpLine = CreateSeries("Up line", Color.Yellow, IndicatorChartPaintType.Line, true);
                _seriesDownLine = CreateSeries("Down line", Color.Yellow, IndicatorChartPaintType.Line, true);
            }
        }
        /// <summary>
        /// An iterator method to fill the indicator 
        /// </summary>
        /// <param name="candles">collection candles</param>
        /// <param name="index">index to use in the collection of candles</param>
        public override void OnProcess(List<Candle> candles, int index)
        {
            if (index == 0)
            {
                listDeMax.Clear();
                listDeMin.Clear();
            }

            if (index < _lengthSma.ValueInt)
            {
                return;
            }

            FillInValuesList(candles, index);

            if (listDeMax.Count < _lengthSma.ValueInt || listDeMin.Count < _lengthSma.ValueInt)
                return;

            _seriesUpLine.Values[index] = _UpLineParam.ValueDecimal;
            _seriesDownLine.Values[index] = _DownLineParam.ValueDecimal;

            decimal smaDeMax = CalcSmaDem(listDeMax, index);
            decimal smaDeMin = CalcSmaDem(listDeMin, index);

            if (smaDeMax + smaDeMin == 0)
            {
                _seriesDMark.Values[index] = 0;
                return;
            }

            decimal DMark = smaDeMax / (smaDeMax + smaDeMin);

            _seriesDMark.Values[index] = DMark;
        }

        private List<decimal> listDeMax = new List<decimal>();
        private List<decimal> listDeMin = new List<decimal>();
        /// <summary>
        /// Fill an List with values
        /// </summary>
        /// <param name="candles"></param>
        /// <param name="index"></param>
        public void FillInValuesList(List<Candle> candles, int index)
        {
            decimal _lastHigh = candles[index].High;
            decimal _prevHigh = candles[index - 1].High;

            decimal _lastLow = candles[index].Low;
            decimal _prevLow = candles[index - 1].Low;

            decimal DeMax;
            if (_lastHigh > _prevHigh)
            {
                DeMax = _lastHigh - _prevHigh;
            }
            else
                DeMax = 0;

            if (listDeMax.Count < _lengthSma.ValueInt)
            {
                listDeMax.Add(DeMax);
            }
            else
            {
                listDeMax.RemoveAt(0);
                listDeMax.Add(DeMax);
            }

            decimal DeMin;

            if (_lastLow < _prevLow)
            {
                DeMin = _prevLow - _lastLow;
            }
            else
                DeMin = 0;

            if (listDeMin.Count < _lengthSma.ValueInt)
            {
                listDeMin.Add(DeMin);
            }
            else
            {
                listDeMin.RemoveAt(0);
                listDeMin.Add(DeMin);
            }
        }
        /// <summary>
        /// calculation of smoothed DeMark max and min
        /// </summary>
        /// <param name="listD"></param>
        /// <param name="index"></param>
        /// <returns></returns>
        public decimal CalcSmaDem(List<decimal> listD, int index)
        {
            decimal smaD = 0;

            for (int i = 0; i < listDeMax.Count && i < listDeMin.Count; i++)
            {
                smaD += listD[i];
            }
            smaD = smaD / _lengthSma.ValueInt;

            return smaD;
        }
    }
}
