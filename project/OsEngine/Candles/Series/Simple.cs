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

    [Candle("Simple")] // Japan
    public class Simple : ACandlesSeriesRealization
    {
        public CandlesParameterString TimeFrameParameter;

        public CandlesParameterBool BuildNonTradingCandles;

        public override void OnStateChange(CandleSeriesState state)
        {
            if (state == CandleSeriesState.Configure)
            {
                List<string> allTimeFrames = new List<string>();

                allTimeFrames.Add(TimeFrame.Sec1.ToString());
                allTimeFrames.Add(TimeFrame.Sec2.ToString());
                allTimeFrames.Add(TimeFrame.Sec5.ToString());
                allTimeFrames.Add(TimeFrame.Sec10.ToString());
                allTimeFrames.Add(TimeFrame.Sec15.ToString());
                allTimeFrames.Add(TimeFrame.Sec20.ToString());
                allTimeFrames.Add(TimeFrame.Sec30.ToString());
                allTimeFrames.Add(TimeFrame.Min1.ToString());
                allTimeFrames.Add(TimeFrame.Min2.ToString());
                allTimeFrames.Add(TimeFrame.Min3.ToString());
                allTimeFrames.Add(TimeFrame.Min5.ToString());
                allTimeFrames.Add(TimeFrame.Min10.ToString());
                allTimeFrames.Add(TimeFrame.Min15.ToString());
                allTimeFrames.Add(TimeFrame.Min20.ToString());
                allTimeFrames.Add(TimeFrame.Min30.ToString());
                allTimeFrames.Add(TimeFrame.Min45.ToString());
                allTimeFrames.Add(TimeFrame.Hour1.ToString());
                allTimeFrames.Add(TimeFrame.Hour2.ToString());
                allTimeFrames.Add(TimeFrame.Hour4.ToString());
                allTimeFrames.Add(TimeFrame.Day.ToString());

                TimeFrameParameter
                    = CreateParameterStringCollection("TimeFrame",
                    OsLocalization.Market.Label10, TimeFrame.Min30.ToString(), allTimeFrames);

                BuildNonTradingCandles
                    = CreateParameterBool("NonTradingCandles", OsLocalization.Market.Label12, false);

                TimeFrame = TimeFrame.Min30;
            }
            else if (state == CandleSeriesState.ParametersChange)
            {
                TimeFrame newTf;

                Enum.TryParse(TimeFrameParameter.ValueString, out newTf);
                {
                    TimeFrame = newTf;
                }

                if(CandlesAll != null)
                {
                    CandlesAll.Clear();
                }
               
            }
        }

        public TimeFrame TimeFrame
        {
            get { return _timeFrame; }
            set
            {
                try
                {
                    if (value != _timeFrame)
                    {
                        _timeFrame = value;
                        if (value == TimeFrame.Sec1)
                        {
                            _timeFrameSpan = new TimeSpan(0, 0, 0, 1);
                        }
                        else if (value == TimeFrame.Sec2)
                        {
                            _timeFrameSpan = new TimeSpan(0, 0, 0, 2);
                        }
                        else if (value == TimeFrame.Sec5)
                        {
                            _timeFrameSpan = new TimeSpan(0, 0, 0, 5);
                        }
                        else if (value == TimeFrame.Sec10)
                        {
                            _timeFrameSpan = new TimeSpan(0, 0, 0, 10);
                        }
                        else if (value == TimeFrame.Sec15)
                        {
                            _timeFrameSpan = new TimeSpan(0, 0, 0, 15);
                        }
                        else if (value == TimeFrame.Sec20)
                        {
                            _timeFrameSpan = new TimeSpan(0, 0, 0, 20);
                        }
                        else if (value == TimeFrame.Sec30)
                        {
                            _timeFrameSpan = new TimeSpan(0, 0, 0, 30);
                        }
                        else if (value == TimeFrame.Min1)
                        {
                            _timeFrameSpan = new TimeSpan(0, 0, 1, 0);
                        }
                        else if (value == TimeFrame.Min2)
                        {
                            _timeFrameSpan = new TimeSpan(0, 0, 2, 0);
                        }
                        else if (value == TimeFrame.Min3)
                        {
                            _timeFrameSpan = new TimeSpan(0, 0, 3, 0);
                        }
                        else if (value == TimeFrame.Min5)
                        {
                            _timeFrameSpan = new TimeSpan(0, 0, 5, 0);
                        }
                        else if (value == TimeFrame.Min10)
                        {
                            _timeFrameSpan = new TimeSpan(0, 0, 10, 0);
                        }
                        else if (value == TimeFrame.Min15)
                        {
                            _timeFrameSpan = new TimeSpan(0, 0, 15, 0);
                        }
                        else if (value == TimeFrame.Min20)
                        {
                            _timeFrameSpan = new TimeSpan(0, 0, 20, 0);
                        }
                        else if (value == TimeFrame.Min30)
                        {
                            _timeFrameSpan = new TimeSpan(0, 0, 30, 0);
                        }
                        else if (value == TimeFrame.Min45)
                        {
                            _timeFrameSpan = new TimeSpan(0, 0, 45, 0);
                        }
                        else if (value == TimeFrame.Hour1)
                        {
                            _timeFrameSpan = new TimeSpan(0, 1, 0, 0);
                        }
                        else if (value == TimeFrame.Hour2)
                        {
                            _timeFrameSpan = new TimeSpan(0, 2, 0, 0);
                        }
                        else if (value == TimeFrame.Hour4)
                        {
                            _timeFrameSpan = new TimeSpan(0, 4, 0, 0);
                        }
                        else if (value == TimeFrame.Day)
                        {
                            _timeFrameSpan = new TimeSpan(0, 24, 0, 0);
                        }
                    }
                }
                catch
                {
                    // ignore
                }
            }
        }
        private TimeFrame _timeFrame;

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

                if (TimeFrameSpan.TotalMinutes >= 1)
                {
                    timeNextCandle = time.AddSeconds(-time.Second);

                    while (timeNextCandle.Minute % TimeFrameSpan.TotalMinutes != 0)
                    {
                        timeNextCandle = timeNextCandle.AddMinutes(-1);
                    }

                    while (timeNextCandle.Second != 0)
                    {
                        timeNextCandle = timeNextCandle.AddSeconds(-1);
                    }
                }
                else
                {
                    while (timeNextCandle.Second % TimeFrameSpan.TotalSeconds != 0)
                    {
                        timeNextCandle = timeNextCandle.AddSeconds(-1);
                    }
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

                CandlesAll.Add(candle);

                if (canPushUp)
                {
                    UpdateChangeCandle();
                }

                return;
            }

            if (CandlesAll[CandlesAll.Count - 1].TimeStart.Add(TimeFrameSpan + TimeFrameSpan) <= time
                && BuildNonTradingCandles.ValueBool)
            {
                // произошёл пропуск данных в результате клиринга или перерыва в торгах
                SetForeign(time);
            }

            if (
                (
                  CandlesAll != null &&
                  CandlesAll[CandlesAll.Count - 1].TimeStart < time &&
                  CandlesAll[CandlesAll.Count - 1].TimeStart.Add(TimeFrameSpan) <= time
                )
                ||
                (
                  TimeFrame == TimeFrame.Day &&
                  CandlesAll[CandlesAll.Count - 1].TimeStart.Date < time.Date
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

                if (TimeFrameSpan.TotalMinutes >= 1)
                {
                    timeNextCandle = time.AddSeconds(-time.Second);

                    while (timeNextCandle.Minute % TimeFrameSpan.TotalMinutes != 0 &&
                        TimeFrame != TimeFrame.Min45 && TimeFrame != TimeFrame.Min3)
                    {
                        timeNextCandle = timeNextCandle.AddMinutes(-1);
                    }

                    while (timeNextCandle.Second != 0)
                    {
                        timeNextCandle = timeNextCandle.AddSeconds(-1);
                    }
                }
                else
                {
                    while (timeNextCandle.Second % TimeFrameSpan.TotalSeconds != 0)
                    {
                        timeNextCandle = timeNextCandle.AddSeconds(-1);
                    }
                }

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

        private void SetForeign(DateTime now)
        {
            if (CandlesAll == null ||
                CandlesAll.Count == 1)
            {
                return;
            }

            for (int i = 0; i < CandlesAll.Count; i++)
            {
                if ((i + 1 < CandlesAll.Count &&
                     CandlesAll[i].TimeStart.Add(TimeFrameSpan) < CandlesAll[i + 1].TimeStart)
                    ||
                    (i + 1 == CandlesAll.Count &&
                     CandlesAll[i].TimeStart.Add(TimeFrameSpan) < now))
                {
                    Candle candle = new Candle();
                    candle.TimeStart = CandlesAll[i].TimeStart.Add(TimeFrameSpan);
                    candle.High = CandlesAll[i].Close;
                    candle.Open = CandlesAll[i].Close;
                    candle.Low = CandlesAll[i].Close;
                    candle.Close = CandlesAll[i].Close;
                    candle.Volume = 1;

                    CandlesAll.Insert(i + 1, candle);
                }
            }
        }

    }
}