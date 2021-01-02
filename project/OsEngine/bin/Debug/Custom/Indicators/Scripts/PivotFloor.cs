using System;
using System.Collections.Generic;
using System.Drawing;
using OsEngine.Entity;
using OsEngine.Indicators;

namespace CustomIndicators.Scripts
{
    public class PivotFloor : Aindicator
    {
        private IndicatorParameterString _period;

        private IndicatorDataSeries _seriesP;

        private IndicatorDataSeries _seriesR1;
        private IndicatorDataSeries _seriesR2;
        private IndicatorDataSeries _seriesR3;

        private IndicatorDataSeries _seriesS1;
        private IndicatorDataSeries _seriesS2;
        private IndicatorDataSeries _seriesS3;

        public override void OnStateChange(IndicatorState state)
        {
            if (state == IndicatorState.Configure)
            {
                _period = CreateParameterStringCollection("Period", "Daily", new List<string>() { "Daily", "Weekly" });

                _seriesP = CreateSeries("P", Color.LawnGreen, IndicatorChartPaintType.Line, true);
                _seriesP.CanReBuildHistoricalValues = false;

                _seriesR1 = CreateSeries("R1", Color.DodgerBlue, IndicatorChartPaintType.Line, true);
                _seriesR1.CanReBuildHistoricalValues = false;
                _seriesR2 = CreateSeries("R2", Color.DodgerBlue, IndicatorChartPaintType.Line, true);
                _seriesR2.CanReBuildHistoricalValues = false;
                _seriesR3 = CreateSeries("R3", Color.DodgerBlue, IndicatorChartPaintType.Line, true);
                _seriesR3.CanReBuildHistoricalValues = false;

                _seriesS1 = CreateSeries("S1", Color.DarkRed, IndicatorChartPaintType.Line, true);
                _seriesS1.CanReBuildHistoricalValues = false;
                _seriesS2 = CreateSeries("S2", Color.DarkRed, IndicatorChartPaintType.Line, true);
                _seriesS2.CanReBuildHistoricalValues = false;
                _seriesS3 = CreateSeries("S3", Color.DarkRed, IndicatorChartPaintType.Line, true);
                _seriesS3.CanReBuildHistoricalValues = false;
            }

            else if (state == IndicatorState.Dispose)
            {
                if(_valuesP != null)
                _valuesP.Clear();

                if(_valuesR1 != null)
                _valuesR1.Clear();

                if(_valuesR2 != null) 
                _valuesR2.Clear();

                if (_valuesR3 != null)
                    _valuesR3.Clear();

                if(_valuesS1 != null)
                _valuesS1.Clear();

                if (_valuesS2 != null)
                    _valuesS2.Clear();

                if (_valuesS3 != null)
                    _valuesS3.Clear();
            }
        }

        public override void OnProcess(List<Candle> candles, int index)
        {
            if (index == 0)
            {
                if (_valuesP != null)
                    _valuesP.Clear();

                if (_valuesR1 != null)
                    _valuesR1.Clear();

                if (_valuesR2 != null)
                    _valuesR2.Clear();

                if (_valuesR3 != null)
                    _valuesR3.Clear();

                if (_valuesS1 != null)
                    _valuesS1.Clear();

                if (_valuesS2 != null)
                    _valuesS2.Clear();

                if (_valuesS3 != null)
                    _valuesS3.Clear();

                _r1 = 0;
                _r2 = 0;
                _r3 = 0;
                _s1 = 0;
                _s2 = 0;
                _s3 = 0;
                _pivot = 0;
            }

            Process(candles);

            _seriesP.Values[index] = _valuesP[index];
            _seriesR1.Values[index] = _valuesR1[index];
            _seriesR2.Values[index] = _valuesR2[index];
            _seriesR3.Values[index] = _valuesR3[index];
            _seriesS1.Values[index] = _valuesS1[index];
            _seriesS2.Values[index] = _valuesS2[index];
            _seriesS3.Values[index] = _valuesS3[index];

        }
        private List<decimal> _valuesP = new List<decimal>();
        private List<decimal> _valuesR1 = new List<decimal>();
        private List<decimal> _valuesR2 = new List<decimal>();
        private List<decimal> _valuesR3 = new List<decimal>();
        private List<decimal> _valuesS1 = new List<decimal>();
        private List<decimal> _valuesS2 = new List<decimal>();
        private List<decimal> _valuesS3 = new List<decimal>();

        public void Process(List<Candle> candles)
        {
            if (_valuesP != null &&
                _valuesP.Count + 1 == candles.Count && index.Count >= 2)
            {
                ProcessOne(candles);
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

            if (candles.Count != 1)
            {
                if (_lastTimeReload == DateTime.MinValue
                    ||
                    (_period.ValueString == "Daily" &&
                     _lastTimeReload.Day != candles[candles.Count - 1].TimeStart.Day)
                    ||
                    (_period.ValueString == "Weekly" &&
                     candles[candles.Count - 1].TimeStart.DayOfWeek == DayOfWeek.Monday &&
                     _lastTimeReload.DayOfYear != candles[candles.Count - 1].TimeStart.DayOfYear)
                )
                {
                    _lastTimeReload = candles[candles.Count - 1].TimeStart;
                    index.Add(candles.Count - 1);
                    Reload(candles, index);
                }
            }

            _valuesR1.Add(_r1);
            _valuesR2.Add(_r2);
            _valuesR3.Add(_r3);

            _valuesP.Add(_pivot);

            _valuesS1.Add(_s1);
            _valuesS2.Add(_s2);
            _valuesS3.Add(_s3);
        }

        private List<int> index = new List<int>();

        DateTime _lastTimeReload = DateTime.MinValue;

        /// <summary>
        /// to upload from the beginning
        /// прогрузить с самого начала
        /// </summary>
        private void ProcessAll(List<Candle> candles)
        {
            _lastTimeReload = DateTime.MinValue;

            if (candles == null)
            {
                return;
            }

            _valuesR1 = new List<decimal>();
            _valuesR2 = new List<decimal>();
            _valuesR3 = new List<decimal>();

            _valuesP = new List<decimal>();

            _valuesS1 = new List<decimal>();
            _valuesS2 = new List<decimal>();
            _valuesS3 = new List<decimal>();

            _r1 = 0;
            _r2 = 0;
            _r3 = 0;
            _s1 = 0;
            _s2 = 0;
            _s3 = 0;
            _pivot = 0;

            // candle indexes that starting a trading day
            // индексы свечей, начинающих торговый день
            index = new List<int>();

            List<Candle> newCandles = new List<Candle>();

            int count = 0;

            for (int i = 0; i < candles.Count; i++)
            {
                newCandles.Add(candles[i]);

                if (_lastTimeReload == DateTime.MinValue
                    ||
                    (_period.ValueString == "Daily" && _lastTimeReload.Day != candles[i].TimeStart.Day)
                    ||
                    (_period.ValueString == "Weekly" &&
                     candles[i].TimeStart.DayOfWeek == DayOfWeek.Monday
                     && _lastTimeReload.DayOfYear != candles[i].TimeStart.DayOfYear)
                    )
                {
                    _lastTimeReload = candles[i].TimeStart;
                    index.Add(i);
                    count++;

                    if (count >= 2)
                        Reload(newCandles, index);
                }

                _valuesR1.Add(_r1);
                _valuesR2.Add(_r2);
                _valuesR3.Add(_r3);

                _valuesP.Add(_pivot);

                _valuesS1.Add(_s1);
                _valuesS2.Add(_s2);
                _valuesS3.Add(_s3);

            }
        }

        /// <summary>
        /// variables to calculate the indicator
        /// переменные для расчета индикатора
        /// </summary>
        private decimal _r1;
        private decimal _r2;
        private decimal _r3;

        private decimal _pivot;

        private decimal _s1;
        private decimal _s2;
        private decimal _s3;

        /// <summary>
        /// update indicator values
        /// обновить значения индикатора
        /// </summary>
        /// <param name="newCandles"></param>
        /// <param name="index"></param>
        private void Reload(List<Candle> newCandles, List<int> index)
        {
            decimal H = 0;

            decimal L = decimal.MaxValue;

            decimal C = 0;

            for (int i = index[index.Count - 2]; i < index[index.Count - 1]; i++)
            {
                if (H < newCandles[i].High)
                    H = newCandles[i].High;

                if (L > newCandles[i].Low)
                    L = newCandles[i].Low;

                C = newCandles[i].Close;
            }

            if (L == decimal.MaxValue ||
                H == 0 ||
                C == 0)
            {
                _r1 = 0;
                _r2 = 0;
                _r3 = 0;
                _s1 = 0;
                _s2 = 0;
                _s3 = 0;
                _pivot = 0;
                return;
            }
            // calculation of indicator levels
            // расчет уровней индикатора
            _pivot = (H + L + C) / 3;

            // Pivot-Floor

            _s1 = (_pivot * 2) - H;
            _r1 = (2 * _pivot) - L;
            _r2 = _pivot + H - L;
            _r3 = (_pivot - _s1) + _r2;
            _s2 = _pivot - H + L;
            _s3 = _pivot - (_r2 - _s1);

        }
    }
}
