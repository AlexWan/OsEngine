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
    [Candle("DeltaAdaptive")]
    public class DeltaAdaptive : ACandlesSeriesRealization
    {
        public CandlesParameterDecimal DeltaPeriods;

        public CandlesParameterInt CandlesCountInDay;

        public CandlesParameterInt DaysLookBack;

        public override void OnStateChange(CandleSeriesState state)
        {
            if (state == CandleSeriesState.Configure)
            {
                DeltaPeriods = CreateParameterDecimal("DeltaPeriods", OsLocalization.Market.Label13, 10000m);
                CandlesCountInDay = CreateParameterInt("CandlesCountInDay", OsLocalization.Market.Label125, 100);
                DaysLookBack = CreateParameterInt("DaysLookBack", OsLocalization.Market.Label126, 1);
            }
            else if (state == CandleSeriesState.ParametersChange)
            {
                if(DaysLookBack.ValueInt > 2)
                {
                    DaysLookBack.ValueInt = 2;
                }
                if (DaysLookBack.ValueInt <= 0)
                {
                    DaysLookBack.ValueInt = 1;
                }
                if(CandlesCountInDay.ValueInt <= 0)
                {
                    CandlesCountInDay.ValueInt = 1;
                }
            }
        }

        private void RebuildCandlesCount()
        {
            if (CandlesCountInDay.ValueInt <= 0
                || DaysLookBack.ValueInt <= 0)
            {
                return;
            }

            decimal candlesCount = 0;

            DateTime date = CandlesAll[CandlesAll.Count - 1].TimeStart.Date;

            int days = 0;

            for (int i = CandlesAll.Count - 1; i >= 0; i--)
            {
                Candle curCandle = CandlesAll[i];

                if (curCandle.TimeStart.Date < date)
                {
                    date = curCandle.TimeStart.Date;
                    days++;
                }

                if (days >= DaysLookBack.ValueInt)
                {
                    break;
                }

                candlesCount++;

                if (i == 0)
                {
                    days++;
                    break;
                }
            }

            if (candlesCount == 0)
            {
                return;
            }

            decimal countCandlesInDay = candlesCount / days;

            decimal commonChangeDelta = DeltaPeriods.ValueDecimal * countCandlesInDay;

            decimal newDelta = commonChangeDelta / CandlesCountInDay.ValueInt;

            DeltaPeriods.ValueDecimal = newDelta;
        }

        private decimal _currentDelta;

        public override void UpDateCandle(DateTime time, decimal price, decimal volume, bool canPushUp, Side side)
        {
            // Формула кумулятивной дельты 
            //Delta= ∑_i▒vBuy- ∑_i▒vSell 

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

                if (CandlesAll.Count > 0)
                {
                    candle.OpenInterest = CandlesAll[^1].OpenInterest;
                }

                CandlesAll.Add(candle);

                if (canPushUp)
                {
                    UpdateChangeCandle();
                }

                _currentDelta = 0;

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

                _currentDelta = 0;

                CandlesAll.Add(candle);

                if (canPushUp)
                {
                    UpdateChangeCandle();
                }

                return;
            }


            if (side == Side.Buy)
            {
                _currentDelta += volume;
            }
            else
            {
                _currentDelta -= volume;
            }


            if (CandlesAll != null &&
                Math.Abs(_currentDelta) >= DeltaPeriods.ValueDecimal)
            {
                // если пришли данные из новой свечки

                _currentDelta = 0;

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

            if (CandlesAll != null)
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