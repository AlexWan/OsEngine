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
    [Candle("TimeSpanCandle")]
    public class TimeSpanCandle : ACandlesSeriesRealization
    {
        public CandlesParameterInt Hours;

        public CandlesParameterInt Minutes;

        public CandlesParameterInt Seconds;

        public CandlesParameterString ForcedStartFromZero;

        public override void OnStateChange(CandleSeriesState state)
        {
            if (state == CandleSeriesState.Configure)
            {
                List<string> workRegimes = new List<string>();

                workRegimes.Add("Every day");
                workRegimes.Add("Every hour");
                workRegimes.Add("Every minute");
                workRegimes.Add("Off");

                ForcedStartFromZero
                    = CreateParameterStringCollection("ForcedStartFromZero",
                   OsLocalization.Market.Label124, "Every hour", workRegimes);

                Hours = CreateParameterInt("Hours", "Hours", 0);
                Minutes = CreateParameterInt("Minutes", "Minutes", 59);
                Seconds = CreateParameterInt("Seconds", "Seconds", 40);

                _timeFrameSpan = new TimeSpan(Hours.ValueInt, Minutes.ValueInt, Seconds.ValueInt);
            }
            else if (state == CandleSeriesState.ParametersChange)
            {
                _timeFrameSpan = new TimeSpan(Hours.ValueInt,Minutes.ValueInt,Seconds.ValueInt);

                if (CandlesAll != null)
                {
                    CandlesAll.Clear();
                }
            }
        }

        public TimeSpan TimeFrameSpan
        {
            get { return _timeFrameSpan; }
        }
        private TimeSpan _timeFrameSpan;

        public override void UpDateCandle(DateTime time, decimal price, decimal volume, bool canPushUp, Side side)
        {
            if (CandlesAll != null
                                   && CandlesAll.Count > 0
                                   && CandlesAll[CandlesAll.Count - 1] != null
                                   &&
                                   CandlesAll[CandlesAll.Count - 1].TimeStart > time)
            {
                // если пришли старые данные
                return;
            }

            if (CandlesAll == null ||
                CandlesAll.Count == 0)
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

                return;
            }

            if (
                (
                  CandlesAll != null &&
                  CandlesAll[CandlesAll.Count - 1].TimeStart < time &&
                  CandlesAll[CandlesAll.Count - 1].TimeStart.Add(TimeFrameSpan) <= time
                )
                ||
                (
                  ForcedStartFromZero.ValueString == "Every day" &&
                  CandlesAll[CandlesAll.Count - 1].TimeStart.Day != time.Day
                )
                ||
                (
                  ForcedStartFromZero.ValueString == "Every hour" &&
                  CandlesAll[CandlesAll.Count - 1].TimeStart.Hour != time.Hour
                )
                ||
                (
                  ForcedStartFromZero.ValueString == "Every minute" &&
                  CandlesAll[CandlesAll.Count - 1].TimeStart.Minute != time.Minute
                )
                )
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
                CandlesAll[CandlesAll.Count - 1].TimeStart <= time &&
                CandlesAll[CandlesAll.Count - 1].TimeStart.Add(TimeFrameSpan) > time)
            {
                // если пришли данные внутри свечи

                if (CandlesAll[CandlesAll.Count - 1].State == CandleState.Finished)
                {
                    CandlesAll[CandlesAll.Count - 1].State = CandleState.Started;
                }

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