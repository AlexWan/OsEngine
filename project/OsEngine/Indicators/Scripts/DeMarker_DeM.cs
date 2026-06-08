using OsEngine.Entity;
using System.Collections.Generic;
using System.Drawing;

namespace OsEngine.Indicators
{
    [Indicator("DeMarker_DeM")]
    public class DeMarker_DeM : Aindicator
    {
        public IndicatorParameterInt _lengthSma;

        public IndicatorParameterDecimal _upLineParam;

        public IndicatorParameterDecimal _downLineParam;

        public IndicatorDataSeries _seriesDMark;

        public IndicatorDataSeries _seriesUpLine;

        public IndicatorDataSeries _seriesDownLine;

        public override void OnStateChange(IndicatorState state)
        {
            if (state == IndicatorState.Configure)
            {
                _lengthSma = CreateParameterInt("Period Sma", 14);

                _upLineParam = CreateParameterDecimal("Up Line parameter", 0.7m);
                _downLineParam = CreateParameterDecimal("Down line parameter", 0.3m);

                _seriesDMark = CreateSeries("Series DMark", Color.Aqua, IndicatorChartPaintType.Line, true);

                _seriesUpLine = CreateSeries("Up line", Color.Yellow, IndicatorChartPaintType.Line, true);
                _seriesDownLine = CreateSeries("Down line", Color.Yellow, IndicatorChartPaintType.Line, true);
            }
        }

        public override void OnProcess(List<Candle> candles, int index)
        {
            if (index == 0)
            {
                _listDeMax.Clear();
                _listDeMin.Clear();
            }

            if (index < _lengthSma.ValueInt)
            {
                return;
            }

            FillInValuesList(candles, index);

            if (_listDeMax.Count < _lengthSma.ValueInt || _listDeMin.Count < _lengthSma.ValueInt)
                return;

            _seriesUpLine.Values[index] = _upLineParam.ValueDecimal;
            _seriesDownLine.Values[index] = _downLineParam.ValueDecimal;

            decimal smaDeMax = CalcSmaDem(_listDeMax, index);
            decimal smaDeMin = CalcSmaDem(_listDeMin, index);

            if (smaDeMax + smaDeMin == 0)
            {
                _seriesDMark.Values[index] = 0;
                return;
            }

            decimal DMark = smaDeMax / (smaDeMax + smaDeMin);

            _seriesDMark.Values[index] = DMark;
        }

        private List<decimal> _listDeMax = new List<decimal>();

        private List<decimal> _listDeMin = new List<decimal>();

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
            {
                DeMax = 0;
            }

            while (_listDeMax.Count < index+1)
            {
                _listDeMax.Add(0);
            }

            _listDeMax[index] = DeMax;

            decimal DeMin;

            if (_lastLow < _prevLow)
            {
                DeMin = _prevLow - _lastLow;
            }
            else
                DeMin = 0;

            while (_listDeMin.Count < index+1)
            {
                _listDeMin.Add(0);
            }

            _listDeMin[index] = DeMin;
        }

        public decimal CalcSmaDem(List<decimal> listD, int index)
        {
            decimal smaD = 0;
            int realSmaLen = 0;

            for (int i = index; i >= 0 && i > index - _lengthSma.ValueInt; i--)
            {
                realSmaLen++;
                smaD += listD[i];
            }

            if(realSmaLen == 0)
            {
                return 0;
            }

            smaD = smaD / realSmaLen;

            return smaD;
        }
    }
}