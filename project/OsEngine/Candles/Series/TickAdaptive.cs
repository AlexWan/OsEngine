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
    [Candle("TickAdaptive")]
    public class TickAdaptive : ACandlesSeriesRealization
    {
        public CandlesParameterInt TradeCount;

        public CandlesParameterInt CandlesCountInDay;

        public CandlesParameterInt DaysLookBack;

        public override void OnStateChange(CandleSeriesState state)
        {
            if (state == CandleSeriesState.Configure)
            {
                TradeCount = CreateParameterInt("TradeCount", OsLocalization.Market.Label11, 1000);
                CandlesCountInDay = CreateParameterInt("CandlesCountInDay", OsLocalization.Market.Label125, 100);
                DaysLookBack = CreateParameterInt("DaysLookBack", OsLocalization.Market.Label126, 1);
            }
            else if (state == CandleSeriesState.ParametersChange)
            {
                if (DaysLookBack.ValueInt <= 0)
                {
                    DaysLookBack.ValueInt = 1;
                }
                if (CandlesCountInDay.ValueInt <= 0)
                {
                    CandlesCountInDay.ValueInt = 1;
                }
                if (TradeCount.ValueInt <= 0)
                {
                    TradeCount.ValueInt = 1;
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

            int daysLookBack = DaysLookBack.ValueInt;

            if((CandlesAll[CandlesAll.Count - 1].Trades == null ||
                CandlesAll[CandlesAll.Count - 1].Trades.Count == 0)
                && daysLookBack > 2)
            {
                daysLookBack = 2;
            }

            decimal candlesCount = 0;

            DateTime date = CandlesAll[CandlesAll.Count - 1].TimeStart.Date;

            int days = 0;

            int tradesCount = 0;

            for (int i = CandlesAll.Count - 1; i >= 0; i--)
            {
                Candle curCandle = CandlesAll[i];

                if (curCandle.TimeStart.Date < date)
                {
                    date = curCandle.TimeStart.Date;
                    days++;
                }

                if (days >= daysLookBack)
                {
                    break;
                }

                if(curCandle.Trades != null)
                {
                    tradesCount += curCandle.Trades.Count;
                }

                candlesCount++;

                if(i == 0)
                {
                    days++;
                    break;
                }
            }

            if (candlesCount == 0)
            {
                return;
            }

            if(tradesCount == 0)
            {
                decimal countCandlesInDay = candlesCount / days;
                decimal commonTradesCount = TradeCount.ValueInt * countCandlesInDay;
                decimal newTradesCount = commonTradesCount / CandlesCountInDay.ValueInt;
                TradeCount.ValueInt = Convert.ToInt32(newTradesCount);
            }
            else
            {
                decimal newTradesCount = tradesCount / days / CandlesCountInDay.ValueInt;
                TradeCount.ValueInt = Convert.ToInt32(newTradesCount);
            }
        }

        private int _lastCandleTickCount;

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

                _lastCandleTickCount = 1;
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

                _lastCandleTickCount = 1;

                CandlesAll.Add(candle);

                if (canPushUp)
                {
                    UpdateChangeCandle();
                }

                return;
            }

            if (CandlesAll != null &&
                _lastCandleTickCount >= TradeCount.ValueInt)
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

                _lastCandleTickCount = 1;

                return;
            }

            if (CandlesAll != null &&
                 _lastCandleTickCount < TradeCount.ValueInt)
            {
                // если пришли данные внутри свечи
                _lastCandleTickCount++;

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