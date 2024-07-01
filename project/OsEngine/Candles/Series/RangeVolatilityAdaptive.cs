/*
 *Your rights to use the code are governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 *Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using OsEngine.Candles.Factory;
using OsEngine.Entity;
using OsEngine.Language;
using System;
using System.Collections.Generic;

namespace OsEngine.Candles.Series
{
    [Candle("RangeVolatilityAdaptive")]
    public class RangeVolatilityAdaptive : ACandlesSeriesRealization
    {
        public CandlesParameterString ValueType;

        public CandlesParameterDecimal RangeCandlesPoints;

        public CandlesParameterDecimal PointsMinMoveVolatilityMult;

        public CandlesParameterInt VolatilityDivider;

        public CandlesParameterInt DaysLookBack;

        public override void OnStateChange(CandleSeriesState state)
        {
            if (state == CandleSeriesState.Configure)
            {
                ValueType
                  = CreateParameterStringCollection("vT", OsLocalization.Market.Label122,
                  "Percent", new List<string> { "Absolute", "Percent" });

                RangeCandlesPoints = CreateParameterDecimal("MinMove", OsLocalization.Market.Label18, 0.2m);

                DaysLookBack = CreateParameterInt("DLB", OsLocalization.Market.Label126, 1);

                VolatilityDivider = CreateParameterInt("CInD", OsLocalization.Market.Label129, 100);

                PointsMinMoveVolatilityMult = CreateParameterDecimal("MMVM", OsLocalization.Market.Label127, 1.2m);
            }
            else if (state == CandleSeriesState.ParametersChange)
            {
                if (DaysLookBack.ValueInt <= 0)
                {
                    DaysLookBack.ValueInt = 1;
                }

                if (VolatilityDivider.ValueInt <= 0)
                {
                    VolatilityDivider.ValueInt = 100;
                }

                if(PointsMinMoveVolatilityMult.ValueDecimal <= 0)
                {
                    PointsMinMoveVolatilityMult.ValueDecimal = 1.2m;
                }

                if(RangeCandlesPoints.ValueDecimal <= 0)
                {
                    RangeCandlesPoints.ValueDecimal = 0.2m;
                }
            }
        }

        private void RebuildCandlesCount()
        {
            if (VolatilityDivider.ValueInt <= 0
                || DaysLookBack.ValueInt <= 0)
            {
                return;
            }

            // 1 рассчитываем движение от хая до лоя внутри N дней

            decimal minValueInDay = decimal.MaxValue;
            decimal maxValueInDay = decimal.MinValue;

            List<decimal> volaInDaysAbs = new List<decimal>();
            List<decimal> volaInDaysPercent = new List<decimal>();

            DateTime date = CandlesAll[CandlesAll.Count - 1].TimeStart.Date;

            int days = 0;

            for (int i = CandlesAll.Count - 1; i >= 0; i--)
            {
                Candle curCandle = CandlesAll[i];

                if (curCandle.TimeStart.Date < date)
                {
                    date = curCandle.TimeStart.Date;
                    days++;

                    decimal volaAbsToday = maxValueInDay - minValueInDay;
                    decimal volaPercentToday = volaAbsToday / (minValueInDay / 100);

                    volaInDaysAbs.Add(volaAbsToday);
                    volaInDaysPercent.Add(volaPercentToday);


                    minValueInDay = decimal.MaxValue;
                    maxValueInDay = decimal.MinValue;
                }

                if (days >= DaysLookBack.ValueInt)
                {
                    break;
                }

                if (curCandle.High > maxValueInDay)
                {
                    maxValueInDay = curCandle.High;
                }
                if (curCandle.Low < minValueInDay)
                {
                    minValueInDay = curCandle.Low;
                }

                if (i == 0)
                {
                    days++;

                    decimal volaAbsToday = maxValueInDay - minValueInDay;
                    decimal volaPercentToday = volaAbsToday / (minValueInDay / 100);

                    volaInDaysAbs.Add(volaAbsToday);
                    volaInDaysPercent.Add(volaPercentToday);
                }
            }

            if (volaInDaysAbs.Count == 0
                || volaInDaysPercent.Count == 0)
            {
                return;
            }

            if (days == 0)
            {
                days = 1;
            }

            // 2 усредняем это движение. Нужна усреднённая волатильность. Абс / процент

            decimal volaAbsSma = 0;
            decimal volaPercentSma = 0;

            for (int i = 0; i < volaInDaysAbs.Count; i++)
            {
                volaAbsSma += volaInDaysAbs[i];
                volaPercentSma += volaInDaysPercent[i];
            }

            volaAbsSma = volaAbsSma / volaInDaysAbs.Count;
            volaPercentSma = volaPercentSma / volaInDaysPercent.Count;

            // 3 считаем средний размер свечи с учётом этой волатильности

            decimal volaAbsOneCandle = volaAbsSma / VolatilityDivider.ValueInt;
            decimal volaPercentOneCandle = volaPercentSma / VolatilityDivider.ValueInt;

            decimal oneCandleMinMoveAbs = volaAbsOneCandle * PointsMinMoveVolatilityMult.ValueDecimal;
            decimal oneCandleMinMovePercent = volaPercentOneCandle * PointsMinMoveVolatilityMult.ValueDecimal;

            //"Absolute", "Percent" 
            if (ValueType.ValueString == "Absolute")
            {
                RangeCandlesPoints.ValueDecimal = Math.Round(oneCandleMinMoveAbs, 9);
            }
            else if (ValueType.ValueString == "Percent")
            {
                RangeCandlesPoints.ValueDecimal = Math.Round(oneCandleMinMovePercent, 9);
            }
        }

        public override void UpDateCandle(DateTime time, decimal price, decimal volume, bool canPushUp, Side side)
        {
            if (CandlesAll != null && CandlesAll.Count > 0 && CandlesAll[CandlesAll.Count - 1] != null &&
                        CandlesAll[CandlesAll.Count - 1].TimeStart > time)
            {// если пришли старые данные
                return;
            }

            if (CandlesAll == null
                || CandlesAll.Count == 0)
            {
                // пришла первая сделка
                CandlesAll = new List<Candle>();

                DateTime timeNextCandle = time;

                while (timeNextCandle.Millisecond != 0)
                {
                    timeNextCandle = timeNextCandle.AddMilliseconds(-1);
                }

                timeNextCandle = new DateTime(timeNextCandle.Year, timeNextCandle.Month, timeNextCandle.Day,
                    timeNextCandle.Hour, timeNextCandle.Minute, timeNextCandle.Second, timeNextCandle.Millisecond);

                Candle candle = new Candle()
                {
                    Close = price,
                    High = price,
                    Low = price,
                    Open = price,
                    State = CandleState.Started,
                    TimeStart = timeNextCandle,
                    Volume = volume
                };

                CandlesAll.Add(candle);

                if (canPushUp)
                {
                    UpdateChangeCandle();
                }

                return;
            }

            if (CandlesAll != null
                && CandlesAll.Count > 0
                && CandlesAll[CandlesAll.Count - 1].TimeStart.Date < time.Date)
            {
                // пришли данные из нового дня

                RebuildCandlesCount();

                if (CandlesAll[CandlesAll.Count - 1].State != CandleState.Finished)
                {
                    // если последнюю свечку ещё не закрыли и не отправили
                    CandlesAll[CandlesAll.Count - 1].State = CandleState.Finished;

                    if (canPushUp)
                    {
                        UpdateFinishCandle();
                    }
                }

                DateTime timeNextCandle = time;

                while (timeNextCandle.Millisecond != 0)
                {
                    timeNextCandle = timeNextCandle.AddMilliseconds(-1);
                }

                Candle candle = new Candle()
                {
                    Close = price,
                    High = price,
                    Low = price,
                    Open = price,
                    State = CandleState.Started,
                    TimeStart = timeNextCandle,
                    Volume = volume
                };

                CandlesAll.Add(candle);

                if (canPushUp)
                {
                    UpdateChangeCandle();
                }

                return;
            }

            bool isNewCandle = false;

            if (ValueType.ValueString == "Absolute")
            {
                if (CandlesAll[CandlesAll.Count - 1].High - CandlesAll[CandlesAll.Count - 1].Low >= RangeCandlesPoints.ValueDecimal)
                {
                    isNewCandle = true;
                }
            }
            else if (ValueType.ValueString == "Percent")
            {
                decimal distance = CandlesAll[CandlesAll.Count - 1].High - CandlesAll[CandlesAll.Count - 1].Low;

                decimal movePercent = distance / (CandlesAll[CandlesAll.Count - 1].Low / 100);

                if (distance != 0
                    && movePercent != 0
                    && movePercent > RangeCandlesPoints.ValueDecimal)
                {
                    isNewCandle = true;
                }
            }

            if (isNewCandle)
            {
                // если пришли данные из новой свечки

                if (CandlesAll[CandlesAll.Count - 1].State != CandleState.Finished)
                {
                    // если последнюю свечку ещё не закрыли и не отправили
                    CandlesAll[CandlesAll.Count - 1].State = CandleState.Finished;

                    if (canPushUp)
                    {
                        UpdateFinishCandle();
                    }
                }

                DateTime timeNextCandle = time;

                while (timeNextCandle.Millisecond != 0)
                {
                    timeNextCandle = timeNextCandle.AddMilliseconds(-1);
                }

                timeNextCandle = new DateTime(timeNextCandle.Year, timeNextCandle.Month, timeNextCandle.Day,
                    timeNextCandle.Hour, timeNextCandle.Minute, timeNextCandle.Second, timeNextCandle.Millisecond);

                Candle newCandle = new Candle()
                {
                    Close = price,
                    High = price,
                    Low = price,
                    Open = price,
                    State = CandleState.Started,
                    TimeStart = timeNextCandle,
                    Volume = volume
                };

                CandlesAll.Add(newCandle);

                if (canPushUp)
                {
                    UpdateChangeCandle();
                }

                return;
            }
            else
            {
                // если пришли данные внутри свечи

                CandlesAll[CandlesAll.Count - 1].Volume += volume;
                CandlesAll[CandlesAll.Count - 1].Close = price;

                if (CandlesAll[CandlesAll.Count - 1].High < price)
                {
                    CandlesAll[CandlesAll.Count - 1].High = price;
                }

                if (CandlesAll[CandlesAll.Count - 1].Low > price)
                {
                    CandlesAll[CandlesAll.Count - 1].Low = price;
                }

                if (canPushUp)
                {
                    UpdateChangeCandle();
                }
            }
        }
    }
}