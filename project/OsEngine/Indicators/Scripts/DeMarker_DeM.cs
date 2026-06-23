using OsEngine.Entity;
using OsEngine.Language;
using System.Collections.Generic;
using System.Drawing;

namespace OsEngine.Indicators
{
    [Indicator("DeMarker_DeM")]
    public class DeMarker_DeM : Aindicator
    {
        public override string Description
        {
            get
            {
                string eng = "DeMarker compares the sizes of upward and downward price movements over a period and outputs an oscillator between 0 and 1, showing overbought and oversold zones. " +
                             "Traders use moves above 0.7 and below 0.3 together with divergences to find reversals and filter entry signals.";

                string ru = "DeMarker сравнивает размеры восходящих и нисходящих движений цены за период и выводит осциллятор в диапазоне от 0 до 1, показывая зоны перекупленности и перепроданности. " +
                            "Трейдеры используют выход линии за уровни 0.7 и 0.3 вместе с дивергенциями для поиска разворотов и фильтрации сигналов на вход.";

                return OsLocalization.ConvertToLocString($"Eng:{eng}_Ru:{ru}_");
            }
        }

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