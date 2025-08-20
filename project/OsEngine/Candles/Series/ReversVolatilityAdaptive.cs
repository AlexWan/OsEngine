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
    [Candle("ReversVolatilityAdaptive")]
    public class ReversVolatilityAdaptive : ACandlesSeriesRealization
    {
        public CandlesParameterString ValueType;

        public CandlesParameterDecimal ReversCandlesPointsMinMove;

        public CandlesParameterDecimal ReversCandlesPointsBackMove;

        public CandlesParameterDecimal PointsMinMoveVolatilityMult;

        public CandlesParameterDecimal PointsBackMoveVolatilityMult;

        public CandlesParameterInt VolatilityDivider; 

        public CandlesParameterInt DaysLookBack;

        public override void OnStateChange(CandleSeriesState state)
        {
            if (state == CandleSeriesState.Configure)
            {
                ValueType
                  = CreateParameterStringCollection("vT", OsLocalization.Market.Label122,
                  "Percent", new List<string> { "Absolute", "Percent" });

                ReversCandlesPointsMinMove = CreateParameterDecimal("MM", OsLocalization.Market.Label18, 0.2m);

                ReversCandlesPointsBackMove = CreateParameterDecimal("BM", OsLocalization.Market.Label19, 0.1m);

                DaysLookBack = CreateParameterInt("DLB", OsLocalization.Market.Label126, 1);

                VolatilityDivider = CreateParameterInt("CInD", OsLocalization.Market.Label129, 100);
                
                PointsMinMoveVolatilityMult = CreateParameterDecimal("MMVM", OsLocalization.Market.Label127, 1.2m);

                PointsBackMoveVolatilityMult = CreateParameterDecimal("BMVM", OsLocalization.Market.Label128, 0.35m);
            }
            else if (state == CandleSeriesState.ParametersChange)
            {
                if(ReversCandlesPointsMinMove.ValueDecimal <= 0)
                {
                    ReversCandlesPointsMinMove.ValueDecimal = 0.2m;
                }

                if(ReversCandlesPointsBackMove.ValueDecimal <= 0)
                {
                    ReversCandlesPointsBackMove.ValueDecimal = 0.1m;
                }

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

                if(PointsBackMoveVolatilityMult.ValueDecimal <= 0)
                {
                    PointsBackMoveVolatilityMult.ValueDecimal = 0.35m;
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

            decimal oneCandleBackMoveAbs = volaAbsOneCandle * PointsBackMoveVolatilityMult.ValueDecimal;
            decimal oneCandleBackMovePercent = volaPercentOneCandle * PointsBackMoveVolatilityMult.ValueDecimal;

            //"Absolute", "Percent" 
            if (ValueType.ValueString == "Absolute")
            {
                ReversCandlesPointsMinMove.ValueDecimal = Math.Round(oneCandleMinMoveAbs,9);
                ReversCandlesPointsBackMove.ValueDecimal = Math.Round(oneCandleBackMoveAbs, 9);
            }
            else if (ValueType.ValueString == "Percent")
            {
                ReversCandlesPointsMinMove.ValueDecimal = Math.Round(oneCandleMinMovePercent, 9);
                ReversCandlesPointsBackMove.ValueDecimal = Math.Round(oneCandleBackMovePercent, 9);
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

                while (timeNextCandle.Second % 1 != 0)
                {
                    timeNextCandle = timeNextCandle.AddSeconds(-1);
                }


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

                if (CandlesAll.Count > 0)
                {
                    candle.OpenInterest = CandlesAll[^1].OpenInterest;
                }

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

                if (CandlesAll.Count > 0)
                {
                    candle.OpenInterest = CandlesAll[^1].OpenInterest;
                }

                CandlesAll.Add(candle);

                if (canPushUp)
                {
                    UpdateChangeCandle();
                }

                return;
            }

            bool candleReady = false;

            Candle lastCandle = CandlesAll[CandlesAll.Count - 1];

            if (ValueType.ValueString == "Absolute")
            {
                if (lastCandle.High - lastCandle.Open >= ReversCandlesPointsMinMove.ValueDecimal
                    &&
                    lastCandle.High - lastCandle.Close >= ReversCandlesPointsBackMove.ValueDecimal)
                { // есть откат от хая
                    candleReady = true;
                }

                if (lastCandle.Open - lastCandle.Low >= ReversCandlesPointsMinMove.ValueDecimal
                    &&
                    lastCandle.Close - lastCandle.Low >= ReversCandlesPointsBackMove.ValueDecimal)
                { // есть откат от лоя
                    candleReady = true;
                }
            }
            else if (ValueType.ValueString == "Percent")
            {
                if (lastCandle.High - lastCandle.Open > 0
                    && lastCandle.High - lastCandle.Close > 0)
                {
                    decimal moveUpPercent = (lastCandle.High - lastCandle.Open) / (lastCandle.Open / 100);
                    decimal backMoveFromHighPercent = (lastCandle.High - lastCandle.Close) / (lastCandle.Close / 100);

                    if (moveUpPercent >= ReversCandlesPointsMinMove.ValueDecimal
                    &&
                    backMoveFromHighPercent >= ReversCandlesPointsBackMove.ValueDecimal)
                    {// есть откат от хая
                        candleReady = true;
                    }
                }

                if (lastCandle.Open - lastCandle.Low > 0
                    && lastCandle.Close - lastCandle.Low > 0)
                {
                    decimal moveDownPercent = (lastCandle.Open - lastCandle.Low) / (lastCandle.Low / 100);

                    decimal backMoveFromLowPercent = (lastCandle.Close - lastCandle.Low) / (lastCandle.Low / 100);

                    if (moveDownPercent >= ReversCandlesPointsMinMove.ValueDecimal &&
                        backMoveFromLowPercent >= ReversCandlesPointsBackMove.ValueDecimal)
                    { // есть откат от лоя
                        candleReady = true;
                    }
                }
            }

            if (CandlesAll != null &&
                candleReady)
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

                if (CandlesAll.Count > 0)
                {
                    newCandle.OpenInterest = CandlesAll[^1].OpenInterest;
                }

                CandlesAll.Add(newCandle);

                if (canPushUp)
                {
                    UpdateChangeCandle();
                }

                return;
            }

            if (CandlesAll != null &&
                candleReady == false)
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