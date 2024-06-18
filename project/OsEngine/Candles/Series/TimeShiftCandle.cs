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
    [Candle("TimeShiftCandle")]
    public class TimeShiftCandle : ACandlesSeriesRealization
    {
        public CandlesParameterString TimeFrameParameter;

        public CandlesParameterInt SecondsShift;

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

                SecondsShift = CreateParameterInt("SecondsShift", OsLocalization.Market.Label123, -3);

                TimeFrame = TimeFrame.Min30;
                CreateCandlesTimes();
            }
            else if (state == CandleSeriesState.ParametersChange)
            {
                TimeFrame newTf;

                Enum.TryParse(TimeFrameParameter.ValueString, out newTf);
                {
                    TimeFrame = newTf;
                }

                if (CandlesAll != null)
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

                        CreateCandlesTimes();
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

        List<DateTime> _timesStartCandles = new List<DateTime>();

        private void CreateCandlesTimes()
        {
            _timesStartCandles = new List<DateTime>();

            DateTime firstCandleTime = new DateTime(2022, 1, 1, 0, 0, 0);

            _timesStartCandles.Add(firstCandleTime);

            DateTime nextCandleTime = firstCandleTime.Add(_timeFrameSpan);

            while (true)
            {
                if(nextCandleTime.Day != firstCandleTime.Day)
                {
                    break;
                }

                _timesStartCandles.Add(nextCandleTime);
                nextCandleTime = nextCandleTime.Add(_timeFrameSpan);
            }

            for(int i = 0;i < _timesStartCandles.Count;i++)
            {
                _timesStartCandles[i] = _timesStartCandles[i].AddSeconds(SecondsShift.ValueInt);
            }
        }

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

                for(int i = 0;i < _timesStartCandles.Count-1;i++)
                {
                    DateTime curTime = _timesStartCandles[i];
                    DateTime nextTime = _timesStartCandles[i+1];

                    if(time.TimeOfDay >= curTime.TimeOfDay &&
                        time.TimeOfDay < nextTime.TimeOfDay)
                    {
                        timeNextCandle = curTime;
                        break;
                    }
                }

                timeNextCandle = new DateTime(time.Year, time.Month, time.Day,
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

            if (
                  CandlesAll != null
                  && CandlesAll[CandlesAll.Count - 1].TimeStart.Add(TimeFrameSpan) <= time
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

                for (int i = 0; i < _timesStartCandles.Count - 1; i++)
                {
                    DateTime curTime = _timesStartCandles[i];
                    DateTime nextTime = _timesStartCandles[i+1];

                    if (time.TimeOfDay >= curTime.TimeOfDay &&
                        time.TimeOfDay < nextTime.TimeOfDay)
                    {
                        timeNextCandle = curTime;
                        break;
                    }
                }

                timeNextCandle = new DateTime(time.Year, time.Month, time.Day,
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
    }
}