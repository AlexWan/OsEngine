using System.Collections.Generic;
using System.Drawing;
using OsEngine.Entity;
using OsEngine.Indicators;

namespace CustomIndicators.Scripts
{
    public class Ichimoku : Aindicator
    {
        private IndicatorParameterInt _first;
        private IndicatorParameterInt _second;
        private IndicatorParameterInt _fird;
        private IndicatorParameterInt _sdvig;
        private IndicatorParameterInt _chinkou;

        private IndicatorDataSeries _seriesEtalon;
        private IndicatorDataSeries _seriesRoundet;
        private IndicatorDataSeries _seriesLate;
        private IndicatorDataSeries _seriesFirst;
        private IndicatorDataSeries _seriesSecond;

        public override void OnStateChange(IndicatorState state)
        {
            if (state == IndicatorState.Configure)
            {
                _first = CreateParameterInt("Tenkan", 9);
                _second = CreateParameterInt("Kijun", 26);
                _fird = CreateParameterInt("Sencou", 52);
                _sdvig = CreateParameterInt("Chinkou", 26);
                _chinkou = CreateParameterInt("Deviation", 26);


                _seriesEtalon = CreateSeries("Tenkan", Color.BlueViolet, IndicatorChartPaintType.Line, true);
                _seriesEtalon.CanReBuildHistoricalValues = false;

                _seriesRoundet = CreateSeries("Kijun", Color.OrangeRed, IndicatorChartPaintType.Line, true);
                _seriesRoundet.CanReBuildHistoricalValues = false;

                _seriesLate = CreateSeries("Chinkou", Color.DarkRed, IndicatorChartPaintType.Point, true);
                _seriesLate.CanReBuildHistoricalValues = true;

                _seriesFirst = CreateSeries("Sencou A", Color.LimeGreen, IndicatorChartPaintType.Line, true);
                _seriesFirst.CanReBuildHistoricalValues = false;

                _seriesSecond = CreateSeries("Sencou B", Color.DodgerBlue, IndicatorChartPaintType.Line, true);
                _seriesSecond.CanReBuildHistoricalValues = false;

            }
            else if (state == IndicatorState.Dispose)
            {
                if (_valuesEtalonLine_Kejun_sen != null)
                    _valuesEtalonLine_Kejun_sen.Clear();

                if (_valuesLineRounded_Teken_sen != null)
                    _valuesLineRounded_Teken_sen.Clear();

                if (_valuesLineLate_Chinkou_span != null)
                    _valuesLineLate_Chinkou_span.Clear();

                if (_valuesLineFirst_Senkkou_span_A != null)
                    _valuesLineFirst_Senkkou_span_A.Clear();

                if (_valuesLineSecond_Senkou_span_B != null)
                    _valuesLineSecond_Senkou_span_B.Clear();
            }


        }

        public override void OnProcess(List<Candle> candles, int index)
        {
            if (index == 0
                && _valuesEtalonLine_Kejun_sen != null
                && _valuesLineRounded_Teken_sen != null
                && _valuesLineLate_Chinkou_span != null
                && _valuesLineFirst_Senkkou_span_A != null
                && _valuesLineSecond_Senkou_span_B != null)
            {
                _valuesEtalonLine_Kejun_sen.Clear();
                _valuesLineRounded_Teken_sen.Clear();
                _valuesLineLate_Chinkou_span.Clear();
                _valuesLineFirst_Senkkou_span_A.Clear();
                _valuesLineSecond_Senkou_span_B.Clear();
            }


            Process(candles);

            _seriesEtalon.Values[index] = _valuesEtalonLine_Kejun_sen[index];
            _seriesRoundet.Values[index] = _valuesLineRounded_Teken_sen[index];

            if (index - _chinkou.ValueInt > 0)
            {
                _seriesLate.Values[index - _chinkou.ValueInt] = _valuesLineLate_Chinkou_span[index - _chinkou.ValueInt];
            }

            _seriesFirst.Values[index] = _valuesLineFirst_Senkkou_span_A[index];
            _seriesSecond.Values[index] = _valuesLineSecond_Senkou_span_B[index];
        }


        private List<decimal> _valuesEtalonLine_Kejun_sen = new List<decimal>();
        private List<decimal> _valuesLineRounded_Teken_sen = new List<decimal>();
        private List<decimal> _valuesLineLate_Chinkou_span = new List<decimal>();
        private List<decimal> _valuesLineFirst_Senkkou_span_A = new List<decimal>();
        private List<decimal> _valuesLineSecond_Senkou_span_B = new List<decimal>();

        public void Process(List<Candle> candles)
        {


            if (_valuesEtalonLine_Kejun_sen != null &&
                _valuesEtalonLine_Kejun_sen.Count + 1 == candles.Count)
            {
                ProcessOne(candles);
            }
            else if (_valuesEtalonLine_Kejun_sen != null &&
                     _valuesEtalonLine_Kejun_sen.Count == candles.Count)
            {
                ProcessLast(candles);
            }
            else
            {
                ProcessAll(candles);
            }
        }


        /// <summary>
        /// load only last candle
        /// прогрузить только последнюю свечку
        /// </summary>
        private void ProcessOne(List<Candle> candles)
        {
            if (candles == null)
            {
                return;
            }

            if (_valuesEtalonLine_Kejun_sen == null)
            {
                _valuesEtalonLine_Kejun_sen = new List<decimal>();
                _valuesLineRounded_Teken_sen = new List<decimal>();
                _valuesLineLate_Chinkou_span = new List<decimal>();
                _valuesLineFirst_Senkkou_span_A = new List<decimal>();
                _valuesLineSecond_Senkou_span_B = new List<decimal>();
            }

            _valuesEtalonLine_Kejun_sen.Add(GetLine(candles, candles.Count - 1, _second.ValueInt, 0));
            _valuesLineRounded_Teken_sen.Add(GetLine(candles, candles.Count - 1, _first.ValueInt, 0));

            if (candles.Count - 1 >= _chinkou.ValueInt)
            {
                _valuesLineLate_Chinkou_span.Add(GetLineLate(candles, candles.Count - 1 - _chinkou.ValueInt));
            }

            _valuesLineFirst_Senkkou_span_A.Add(GetLineFirst(candles, candles.Count - 1));
            _valuesLineSecond_Senkou_span_B.Add(GetLine(candles, candles.Count - 1, _fird.ValueInt, _sdvig.ValueInt));
        }

        /// <summary>
        /// to upload from the beginning
        /// прогрузить с самого начала
        /// </summary>
        private void ProcessAll(List<Candle> candles)
        {
            if (candles == null)
            {
                return;
            }

            _valuesEtalonLine_Kejun_sen = new List<decimal>();
            _valuesLineRounded_Teken_sen = new List<decimal>();
            _valuesLineLate_Chinkou_span = new List<decimal>();
            _valuesLineFirst_Senkkou_span_A = new List<decimal>();
            _valuesLineSecond_Senkou_span_B = new List<decimal>();

            for (int i = 0; i < candles.Count; i++)
            {

                _valuesEtalonLine_Kejun_sen.Add(GetLine(candles, i, _second.ValueInt, 0));
                _valuesLineRounded_Teken_sen.Add(GetLine(candles, i, _first.ValueInt, 0));

                if (i >= _chinkou.ValueInt)
                {
                    _valuesLineLate_Chinkou_span.Add(GetLineLate(candles, i - _chinkou.ValueInt));
                }

                _valuesLineFirst_Senkkou_span_A.Add(GetLineFirst(candles, i));
                _valuesLineSecond_Senkou_span_B.Add(GetLine(candles, i, _fird.ValueInt, _sdvig.ValueInt));
            }
        }

        /// <summary>
        /// overload last value
        /// перегрузить последнее значение
        /// </summary>
        private void ProcessLast(List<Candle> candles)
        {
            if (candles == null)
            {
                return;
            }

            _valuesEtalonLine_Kejun_sen[_valuesEtalonLine_Kejun_sen.Count - 1] =
                (GetLine(candles, candles.Count - 1, _second.ValueInt, 0));
            _valuesLineRounded_Teken_sen[_valuesLineRounded_Teken_sen.Count - 1] =
                (GetLine(candles, candles.Count - 1, _first.ValueInt, 0));

            if (candles.Count >= _chinkou.ValueInt)
            {
                _valuesLineLate_Chinkou_span[_valuesLineLate_Chinkou_span.Count - 1] =
                    (GetLineLate(candles, candles.Count - 1 - _chinkou.ValueInt));
            }

            _valuesLineFirst_Senkkou_span_A[_valuesLineFirst_Senkkou_span_A.Count - 1] =
                (GetLineFirst(candles, candles.Count - 1));
            _valuesLineSecond_Senkou_span_B[_valuesLineSecond_Senkou_span_B.Count - 1] =
                (GetLine(candles, candles.Count - 1, _fird.ValueInt, _sdvig.ValueInt));

        }

        private decimal GetLine(List<Candle> candles, int index, int length, int shift)
        {
            index = index - shift;

            if (index < 0)
            {
                return candles[candles.Count - 1].Close;
            }

            decimal high = 0;
            decimal low = decimal.MaxValue;

            for (int i = index; i > -1 && i > index - length; i--)
            {
                if (candles[i].High > high)
                {
                    high = candles[i].High;
                }

                if (candles[i].Low < low)
                {
                    low = candles[i].Low;
                }
            }

            decimal val = (low + high) / 2;
            return (low + high) / 2;
        }

        public decimal GetLineLate(List<Candle> candles, int index)
        {

            if (index + _chinkou.ValueInt >= candles.Count)
            {
                return candles[candles.Count - 1].Close;
            }

            return candles[index + _chinkou.ValueInt].Close;
        }

        public decimal GetLineFirst(List<Candle> candles, int index)
        {
            if (_sdvig.ValueInt >= index + 1 ||
                _first.ValueInt >= index + 1 ||
                index - _sdvig.ValueInt < _sdvig.ValueInt ||
                index - _sdvig.ValueInt < _sdvig.ValueInt)
            {
                return 0;
            }
            return (_valuesEtalonLine_Kejun_sen[index - _sdvig.ValueInt] + _valuesLineRounded_Teken_sen[index - _sdvig.ValueInt]) / 2;
        }
    }
}