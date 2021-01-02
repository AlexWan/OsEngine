using System;
using System.Collections.Generic;
using System.Drawing;
using OsEngine.Entity;
using OsEngine.Indicators;

namespace CustomIndicators.Scripts
{
    public class ParabolicSAR : Aindicator
    {
        private IndicatorParameterDecimal _maxAf;
        private IndicatorParameterDecimal _af;
        private IndicatorDataSeries _series;

        public override void OnStateChange(IndicatorState state)
        {
            if (state == IndicatorState.Configure)
            {
                _af = CreateParameterDecimal("Step", (decimal)0.02);
                _maxAf = CreateParameterDecimal("MaxStep", (decimal)0.2);

                _series = CreateSeries("ParabolicSAR", Color.DodgerBlue, IndicatorChartPaintType.Point, true);
                _series.CanReBuildHistoricalValues = false;


            }
            else if (state == IndicatorState.Dispose)
            {
                if (_valuesUp != null)
                {
                    _valuesUp.Clear();
                }

                if (_valuesDown != null)
                {
                    _valuesDown.Clear();
                }
            }
        }

        public override void OnProcess(List<Candle> candles, int index)
        {
            if (index == 0 && _values
                != null &&
                MasTrend != null)
            {
                _values.Clear();

                MasTrend.Clear();

                if(MasHp != null)
                MasHp.Clear();

                if(MasLp != null)
                MasLp.Clear();

                if (MasAf != null)
                MasAf.Clear();

                if(psar != null)
                psar.Clear();
            }

            Process(candles);

            _series.Values[index] = _values[index];
        }

        // рассчёт индикатоа

        private List<decimal> _valuesUp = new List<decimal>();
        private List<decimal> _valuesDown = new List<decimal>();
        private List<decimal> _values = new List<decimal>();

        public List<decimal> MasTrend { get; set; }
        public List<decimal> MasHp { get; set; }
        public List<decimal> MasLp { get; set; }
        public List<decimal> MasAf { get; set; }
        public List<decimal> psar { get; set; }

        public void Process(List<Candle> candles)
        {
            if (candles == null)
            {
                return;
            }

            if (_values != null && _values.Count + 1 == candles.Count)
            {
                ProcessOne(candles);
            }
            else if (_values != null && _values.Count == candles.Count)
            {
                ProcessLast(candles);
            }
            else
            {
                ProcessAll(candles);
            }
        }

        private void ProcessOne(List<Candle> candles)
        {
            if (candles == null) return;

            if (MasTrend == null) MasTrend = new List<decimal>();
            if (MasHp == null) MasHp = new List<decimal>();
            if (MasLp == null) MasLp = new List<decimal>();
            if (MasAf == null) MasAf = new List<decimal>();
            if (psar == null) psar = new List<decimal>();

            if (_values == null) _values = new List<decimal>();
            if (_valuesUp == null) _valuesUp = new List<decimal>();
            if (_valuesDown == null) _valuesDown = new List<decimal>();

            decimal[] dop = new decimal[6];
            if (_values.Count == 0)
            {
                dop = GetValueParabolicSar(candles, candles.Count - 1, 0, 0, 0, 0, 0, 0);
            }
            else
            {
                dop = GetValueParabolicSar(candles, candles.Count - 1, 0, _values[_values.Count - 1], MasTrend[MasTrend.Count - 1],
                    MasHp[MasHp.Count - 1], MasLp[MasLp.Count - 1], MasAf[MasAf.Count - 1]);
            }

            if (dop[0] > candles[candles.Count - 1].High)
            {
                _valuesDown.Add(dop[0]);
                _valuesUp.Add(0);
            }
            else if (dop[0] < candles[candles.Count - 1].Low)
            {
                _valuesUp.Add(dop[0]);
                _valuesDown.Add(0);
            }
            else
            {
                _valuesUp.Add(0);
                _valuesDown.Add(0);
            }


            _values.Add(dop[0]);
            MasTrend.Add(dop[1]);
            MasHp.Add(dop[2]);
            MasLp.Add(dop[3]);
            MasAf.Add(dop[4]);
        }

        private void ProcessAll(List<Candle> candles)
        {
            if (candles == null) return;

            MasTrend = new List<decimal>();
            MasHp = new List<decimal>();
            MasLp = new List<decimal>();
            MasAf = new List<decimal>();

            _values = new List<decimal>();
            _valuesUp = new List<decimal>();
            _valuesDown = new List<decimal>();

            for (int i = 0; i < candles.Count; i++)
            {
                decimal[] dop = new decimal[6];
                if (_values.Count < 2)
                {
                    dop = GetValueParabolicSar(candles, i, 0, 0, 0, 0, 0, 0);
                }
                else
                {
                    dop = GetValueParabolicSar(candles, i, 0, _values[_values.Count - 1], MasTrend[MasTrend.Count - 1],
                        MasHp[MasHp.Count - 1], MasLp[MasLp.Count - 1], MasAf[MasAf.Count - 1]);
                }

                if (dop[0] > candles[i].High)
                {
                    _valuesDown.Add(dop[0]);
                    _valuesUp.Add(0);
                }
                else if (dop[0] < candles[i].Low)
                {
                    _valuesUp.Add(dop[0]);
                    _valuesDown.Add(0);
                }
                else
                {
                    if (dop[1] == 1.0m)
                    {
                        dop[0] = candles[i].Low;
                        _valuesUp.Add(dop[0]);
                        _valuesDown.Add(0);
                    }
                    else
                    {
                        dop[0] = candles[i].High;
                        _valuesUp.Add(0);
                        _valuesDown.Add(dop[0]);
                    }
                }

                _values.Add(dop[0]);
                MasTrend.Add(dop[1]);
                MasHp.Add(dop[2]);
                MasLp.Add(dop[3]);
                MasAf.Add(dop[4]);
                //Values.Add(GetValueParabolicSAR(candles, i));

            }
        }

        private void ProcessLast(List<Candle> candles)
        {
            if (candles == null) return;

            decimal[] dop = new decimal[6];
            if (_values.Count < 2)
            {
                dop = GetValueParabolicSar(candles, candles.Count - 1, 0, 0, 0, 0, 0, 0);
            }
            else
            {
                dop = GetValueParabolicSar(candles, candles.Count - 1, 0, _values[_values.Count - 2], MasTrend[MasTrend.Count - 2],
                    MasHp[MasHp.Count - 2], MasLp[MasLp.Count - 2], MasAf[MasAf.Count - 2]);
            }

            if (dop[0] > candles[candles.Count - 1].High)
            {
                _valuesDown[_valuesDown.Count - 1] = dop[0];
                _valuesUp[_valuesUp.Count - 1] = 0;
            }
            else if (dop[0] < candles[candles.Count - 1].Low)
            {
                _valuesUp[_valuesUp.Count - 1] = dop[0];
                _valuesDown[_valuesDown.Count - 1] = 0;
            }
            else
            {

                if (dop[1] == 1.0m)
                {
                    dop[0] = candles[candles.Count - 1].Low;
                    _valuesUp[_valuesUp.Count - 1] = dop[0];
                    _valuesDown[_valuesDown.Count - 1] = 0;
                }
                else
                {
                    dop[0] = candles[candles.Count - 1].High;
                    _valuesDown[_valuesDown.Count - 1] = dop[0];
                    _valuesUp[_valuesUp.Count - 1] = 0;

                }

            }

            _values[_values.Count - 1] = dop[0];
            MasTrend[MasTrend.Count - 1] = dop[1];
            MasHp[MasHp.Count - 1] = dop[2];
            MasLp[MasLp.Count - 1] = dop[3];
            MasAf[MasAf.Count - 1] = dop[4];
        }

        private decimal[] GetValueParabolicSar(List<Candle> candles, int index, int update, decimal lineP, decimal trendP, decimal hpP, decimal lpP, decimal afP)
        {
            decimal[] dop = new decimal[6];

            if (index - 2 < 1)
            {
                dop[0] = candles[index].Close;
                dop[1] = 1.0m;
                dop[2] = candles[index].High;
                dop[3] = candles[index].Low;
                dop[4] = _af.ValueDecimal;
                dop[5] = candles[index].High;
                return dop;
            }

            int reverseP = 0;

            if (trendP == 1.0m)
            {
                if (candles[index].Low < lineP)
                {
                    trendP = 0.0m;
                    reverseP = 1;
                    lineP = hpP;
                    lpP = candles[index].Low;
                    afP = _af.ValueDecimal;
                }
            }
            else
            {
                if (candles[index].High > lineP)
                {
                    trendP = 1.0m;
                    reverseP = 1;
                    lineP = lpP;
                    hpP = candles[index].High;
                    afP = _af.ValueDecimal;
                }
            }

            if (reverseP == 0)
            {
                if (trendP == 1.0m)
                {
                    if (candles[index].High > hpP)
                    {
                        hpP = candles[index].High;
                        afP = afP + _af.ValueDecimal;
                        if (afP > _maxAf.ValueDecimal) afP = _maxAf.ValueDecimal;
                    }

                }
                else
                {
                    if (candles[index].Low < lpP)
                    {
                        lpP = candles[index].Low;
                        afP = afP + _af.ValueDecimal;
                        if (afP > _maxAf.ValueDecimal) afP = _maxAf.ValueDecimal;
                    }

                }
            }

            // это нужно расчитать после вычисления afp.
            if (reverseP == 0)
            {
                if (trendP == 1.0m)
                {
                    lineP = lineP + afP * (hpP - lineP);

                    if (candles[index].Low < lineP)
                        lineP = candles[index].Low;

                }
                else
                {
                    lineP = lineP + afP * (lpP - lineP);

                    if (candles[index].High > lineP)
                        lineP = candles[index].High;

                }
            }

            dop[0] = Math.Round(lineP, 4);
            dop[1] = trendP;
            dop[2] = hpP;
            dop[3] = lpP;
            dop[4] = afP;
            return dop;
        }
    }
}