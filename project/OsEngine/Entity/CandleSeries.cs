/*
 *Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System;
using System.Collections.Generic;
using System.Linq;
using OsEngine.Market.Servers;
using OsEngine.Market.Servers.Tester;

namespace OsEngine.Entity
{

    /// <summary>
    /// серия свечек. Объект в котором из входящих данных собираются свечи
    /// </summary>
    public class CandleSeries
    {

// сервис

        /// <summary>
        /// конструктор
        /// </summary>
        /// <param name="timeFrameBuilder">объект несущий в себе данные о таймФрейме</param>
        /// <param name="security">бумага на которою мы подписаны</param>
        public CandleSeries(TimeFrameBuilder timeFrameBuilder, Security security)
        {
            _timeFrameBuilder = timeFrameBuilder;
            Security = security;
        }

        /// <summary>
        /// блокируем пустой конструктор
        /// </summary>
        private CandleSeries()
        {

        }

        /// <summary>
        /// данные из которых собираем свечи: из тиков или из стаканов
        /// </summary>
        public CandleMarketDataType CandleMarketDataType
        {
            get { return _timeFrameBuilder.CandleMarketDataType; }
        }

        /// <summary>
        /// тип сборки свечей: обычный, ренко, дельта, 
        /// </summary>
        public CandleCreateMethodType CandleCreateMethodType
        {
            get { return _timeFrameBuilder.CandleCreateMethodType; }
        }

        private readonly TimeFrameBuilder _timeFrameBuilder;

        /// <summary>
        /// бумага по которой собираются свечи
        /// </summary>
        public Security Security;

        /// <summary>
        /// таймФрейм собираемых свечей в виде TimeSpan
        /// </summary>
        public TimeSpan TimeFrameSpan
        {
            get { return _timeFrameBuilder.TimeFrameTimeSpan; }
        }

        /// <summary>
        /// таймфрейм в виде перечисления
        /// </summary>
        public TimeFrame TimeFrame
        {
            get { return _timeFrameBuilder.TimeFrame; }
            set
            {
                _timeFrameBuilder.TimeFrame = value;
            }
        }

        /// <summary>
        /// все собранные свечи в этой серии
        /// </summary>
        public List<Candle> CandlesAll;

        public List<Candle> CandlesOnlyReady
        {
            get
            {
                if (CandlesAll == null)
                {
                    return null;
                }

                List<Candle> history = CandlesAll;

                if (CandlesAll == null ||
                    CandlesAll.Count == 0)
                {
                    return null;
                }

                if (CandlesAll[CandlesAll.Count - 1].State != CandleState.Finished)
                {
                    return CandlesAll.GetRange(0, CandlesAll.Count - 1); 
                }

                return history;
            }
        }

        /// <summary>
        /// флаг. Прогружена ли серия первичными данными
        /// </summary>
        public bool IsStarted
        {
            get { return _isStarted; }
            set { _isStarted = value; }
        }

        private bool _isStarted;

        /// <summary>
        /// нужно ли продолжать прогружать объект
        /// </summary>
        private bool _isStoped;

        /// <summary>
        /// остановить расчёт серии
        /// </summary>
        public void Stop()
        {
            _isStoped = true;
        }

        /// <summary>
        /// очистить
        /// </summary>
        public void Clear()
        {
            _lastTradeIndex = 0;
            CandlesAll = null;
        }

// приём изменившегося времени

        /// <summary>
        /// добавить в серию новое время сервера
        /// </summary>
        /// <param name="time">новое время</param>
        public void SetNewTime(DateTime time)
        {
            if (_isStoped || _isStarted == false)
            {
                return;
            }

            if (CandlesAll == null || CandlesAll.Count == 0)
            {
                return;
            }

            if (ServerMaster.StartProgram != ServerStartProgramm.IsOsTrader)
            {
                return;
            }

            if (_timeFrameBuilder.CandleCreateMethodType != CandleCreateMethodType.Simple)
            {
                return;
            }

            if ((CandlesAll[CandlesAll.Count - 1].TimeStart.Add(TimeFrameSpan) < time)
                ||
                (TimeFrame == TimeFrame.Day && CandlesAll[CandlesAll.Count - 1].TimeStart.Date < time.Date)
                )
            {
                // пришло время закрыть свечу
                CandlesAll[CandlesAll.Count - 1].State = CandleState.Finished;

                UpdateFinishCandle();
            }
        }

// сбор свечек из тиков

        /// <summary>
        /// индекс тика на последней итерации
        /// </summary>
        private int _lastTradeIndex;

        /// <summary>
        /// добавить в серию новые тики
        /// </summary>
        /// <param name="trades">новые тики</param>
        public void SetNewTicks(List<Trade> trades)
        {
            if (_isStoped || _isStarted == false)
            {
                return;
            }
            if (_timeFrameBuilder.CandleMarketDataType == CandleMarketDataType.MarketDepth)
            {
                return;
            }

            if (ServerMaster.StartProgram != ServerStartProgramm.IsOsTrader &&
                ServerMaster.StartProgram != ServerStartProgramm.IsOsData &&
                ServerMaster.StartProgram != ServerStartProgramm.IsOsConverter &&
                TypeTesterData == TesterDataType.Candle)
            {
                return;
            }

            if (_lastTradeIndex >= trades.Count)
            {
                return;
            }

            // обновилось неизвесное кол-во тиков
            for (int i = _lastTradeIndex; i < trades.Count; i++)
            {
                if (trades[i] == null)
                {
                    continue;
                }
                UpDateCandle(trades[i].Time, trades[i].Price, trades[i].Volume, true, trades[i].Side);

                if (ServerMaster.StartProgram == ServerStartProgramm.IsOsData)
                {
                    continue;
                }

                if (CandlesAll[CandlesAll.Count - 1].Trades == null)
                { CandlesAll[CandlesAll.Count - 1].Trades = new List<Trade>(); }

                CandlesAll[CandlesAll.Count - 1].Trades.Add(trades[i]);
            }

            _lastTradeIndex = trades.Count;
        }

        /// <summary>
        /// добавить в серию новые тики, но свечи прогрузить только один раз, в конце
        /// </summary>
        /// <param name="trades">тики</param>
        public void PreLoad(List<Trade> trades)
        {
            if (_timeFrameBuilder.CandleMarketDataType == CandleMarketDataType.MarketDepth)
            {
                return;
            }
            if (trades == null || trades.Count == 0)
            {
                _isStarted = true;
                return;
            }

            for (int i = 0; i < trades.Count; i++)
            {
                UpDateCandle(trades[i].Time, trades[i].Price, trades[i].Volume, false, trades[i].Side);

                if (CandlesAll[CandlesAll.Count - 1].Trades == null)
                {
                    CandlesAll[CandlesAll.Count-1].Trades = new List<Trade>();
                }
                CandlesAll[CandlesAll.Count-1].Trades.Add(trades[i]);
            }
            UpdateChangeCandle();

            _isStarted = true;
        }

        /// <summary>
        /// прогрузить свечу новыми данными
        /// </summary>
        /// <param name="time">время новых данных</param>
        /// <param name="price">цена</param>
        /// <param name="volume">объём</param>
        /// <param name="canPushUp">можно ли передовать сведения о свечках выше</param>
        /// <param name="side">сторона в которую прошла последняя сделка</param>
        private void UpDateCandle(DateTime time, decimal price, decimal volume, bool canPushUp, Side side)
        {
            if (_timeFrameBuilder.CandleCreateMethodType == CandleCreateMethodType.Simple)
            {
                UpDateSimpleTimeFrame(time, price, volume, canPushUp);
            }
            else if (_timeFrameBuilder.CandleCreateMethodType == CandleCreateMethodType.Delta)
            {
                UpDateDeltaTimeFrame(time, price, volume, canPushUp,side);
            }
            else if (_timeFrameBuilder.CandleCreateMethodType == CandleCreateMethodType.Ticks)
            {
                UpDateTickTimeFrame(time, price, volume, canPushUp);
            }
            else if (_timeFrameBuilder.CandleCreateMethodType == CandleCreateMethodType.Volume)
            {
                UpDateVolumeTimeFrame(time, price, volume, canPushUp);
            }
            else if (_timeFrameBuilder.CandleCreateMethodType == CandleCreateMethodType.Renko)
            {
                UpDateRencoTimeFrame(time, price, volume, canPushUp);
            }
            else if (_timeFrameBuilder.CandleCreateMethodType == CandleCreateMethodType.HeikenAshi)
            {
                UpDateHeikenAshiCandle(time, price, volume, canPushUp);
            }
        }

        /// <summary>
        /// обновить свечи с обычным ТФ
        /// </summary>
        private void UpDateHeikenAshiCandle(DateTime time, decimal price, decimal volume, bool canPushUp)
        {
            if (CandlesAll != null && CandlesAll.Count > 0 && CandlesAll[CandlesAll.Count - 1] != null &&
                CandlesAll[CandlesAll.Count - 1].TimeStart > time)
            {
                // если пришли старые данные
                return;
            }

            if (CandlesAll == null)
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

            if (CandlesAll[CandlesAll.Count - 1].TimeStart.Add(TimeFrameSpan + TimeFrameSpan) <= time &&
                _timeFrameBuilder.SetForeign)
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
                CandlesAll[CandlesAll.Count - 1].Close = (CandlesAll[CandlesAll.Count - 1].Open +
                                                          CandlesAll[CandlesAll.Count - 1].High +
                                                          CandlesAll[CandlesAll.Count - 1].Low +
                                                          CandlesAll[CandlesAll.Count - 1].Close)/4;

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


                Candle newCandle = new Candle()
                {
                    Close = price,
                    High = price,
                    Low = price,
                    Open = (CandlesAll[CandlesAll.Count - 1].Open +
                            CandlesAll[CandlesAll.Count - 1].Close)/2,
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

        /// <summary>
        /// обновить свечи с обычным ТФ
        /// </summary>
        private void UpDateSimpleTimeFrame(DateTime time, decimal price, decimal volume, bool canPushUp)
        {
            //if (From > trade.Time)
            //{
            //     return;
            // }

            if (CandlesAll != null && CandlesAll.Count > 0 && CandlesAll[CandlesAll.Count - 1] != null &&
                CandlesAll[CandlesAll.Count - 1].TimeStart > time)
            {
                // если пришли старые данные
                return;
            }

            if (CandlesAll == null)
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

            if (CandlesAll[CandlesAll.Count - 1].TimeStart.Add(TimeFrameSpan + TimeFrameSpan) <= time &&
                _timeFrameBuilder.SetForeign)
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

        /// <summary>
        /// текущее накопление дельты
        /// </summary>
        private decimal _currentDelta;

        /// <summary>
        /// обновить свечи с дельтой
        /// </summary>
        private void UpDateDeltaTimeFrame(DateTime time, decimal price, decimal volume, bool canPushUp, Side side)
        {
            // Формула кумулятивной дельты 
            //Delta= ∑_i▒vBuy- ∑_i▒vSell 

            if (CandlesAll != null && CandlesAll.Count > 0 && CandlesAll[CandlesAll.Count - 1] != null &&
               CandlesAll[CandlesAll.Count - 1].TimeStart > time)
            {// если пришли старые данные
                return;
            }

            if (CandlesAll == null)
            {
                // пришла первая сделка
                CandlesAll = new List<Candle>();

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

            if (side == Side.Buy)
            {
                _currentDelta += volume;
            }
            else
            {
                _currentDelta -= volume;
            }


            if (CandlesAll != null &&
                Math.Abs(_currentDelta) >= _timeFrameBuilder.DeltaPeriods)
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

        /// <summary>
        /// сколько у нас в последней строящейся свечке тиков
        /// </summary>
        private int _lastCandleTickCount;

        /// <summary>
        /// обновить свечи по кол-ву обезличенных сделок в свече
        /// </summary>
        private void UpDateTickTimeFrame(DateTime time, decimal price, decimal volume, bool canPushUp)
        {
            if (CandlesAll != null && CandlesAll.Count > 0 && CandlesAll[CandlesAll.Count - 1] != null &&
               CandlesAll[CandlesAll.Count - 1].TimeStart > time)
            {// если пришли старые данные
                return;
            }

            if (CandlesAll == null)
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

            if (CandlesAll != null &&
                _lastCandleTickCount >= _timeFrameBuilder.TradeCount)
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
                 _lastCandleTickCount < _timeFrameBuilder.TradeCount)
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

        /// <summary>
        /// обновить свечи по объёму в свече
        /// </summary>
        private void UpDateVolumeTimeFrame(DateTime time, decimal price, decimal volume, bool canPushUp)
        {
            if (CandlesAll != null && CandlesAll.Count > 0 && CandlesAll[CandlesAll.Count - 1] != null &&
               CandlesAll[CandlesAll.Count - 1].TimeStart > time)
            {// если пришли старые данные
                return;
            }

            if (CandlesAll == null)
            {
                // пришла первая сделка
                CandlesAll = new List<Candle>();

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

            if (CandlesAll != null &&
                CandlesAll[CandlesAll.Count - 1].Volume >= _timeFrameBuilder.VolumeToCloseCandleInVolumeType)
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

        private decimal _rencoStartPrice;

        private Side _rencoLastSide;

        private void UpDateRencoTimeFrame(DateTime time, decimal price, decimal volume, bool canPushUp)
        {
            if (CandlesAll != null && CandlesAll.Count > 0 && CandlesAll[CandlesAll.Count - 1] != null &&
               CandlesAll[CandlesAll.Count - 1].TimeStart > time)
            {// если пришли старые данные
                return;
            }

            if (CandlesAll == null)
            {
                _rencoStartPrice = price;
                _rencoLastSide = Side.UnKnown;
                // пришла первая сделка
                CandlesAll = new List<Candle>();

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

            decimal renDist = _timeFrameBuilder.RencoPunktsToCloseCandleInRencoType;

            if (
                (_rencoLastSide == Side.UnKnown && Math.Abs(_rencoStartPrice - price) >= renDist)
                ||
                (_rencoLastSide == Side.Buy && price - _rencoStartPrice >= renDist)
                ||
                (_rencoLastSide == Side.Buy && _rencoStartPrice - price >= renDist * 2)
                ||
                (_rencoLastSide == Side.Sell && _rencoStartPrice - price >= renDist)
                ||
                (_rencoLastSide == Side.Sell && price - _rencoStartPrice >= renDist * 2)
                )
            {
                // если пришли данные из новой свечки

                Candle lastCandle = CandlesAll[CandlesAll.Count - 1];
                


                if (
                    (_rencoLastSide == Side.UnKnown && price - _rencoStartPrice  >= renDist)
                    ||
                    (_rencoLastSide == Side.Buy && price - _rencoStartPrice >= renDist)
                    )
                {
                    _rencoLastSide = Side.Buy;
                    _rencoStartPrice = _rencoStartPrice + renDist;
                    lastCandle.High = _rencoStartPrice;
                }
                else if (
                (_rencoLastSide == Side.UnKnown && _rencoStartPrice - price >= renDist)
                ||
                (_rencoLastSide == Side.Sell && _rencoStartPrice - price >= renDist)
                )
                {
                    _rencoLastSide = Side.Sell;
                    _rencoStartPrice = _rencoStartPrice - renDist;
                    lastCandle.Low = _rencoStartPrice;
                }
                else if (
                    _rencoLastSide == Side.Buy && _rencoStartPrice - price >= renDist*2 )
                {
                    _rencoLastSide = Side.Sell;
                    lastCandle.Open = _rencoStartPrice - renDist;
                    _rencoStartPrice = _rencoStartPrice - renDist*2;
                    lastCandle.Low = _rencoStartPrice;
                }
                else if (
                    _rencoLastSide == Side.Sell && price - _rencoStartPrice >= renDist * 2)
                {
                    _rencoLastSide = Side.Buy;
                    lastCandle.Open = _rencoStartPrice + renDist;
                    _rencoStartPrice = _rencoStartPrice + renDist * 2;
                    lastCandle.High = _rencoStartPrice;
                }

                lastCandle.Close = _rencoStartPrice;

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
                    Close = _rencoStartPrice,
                    High = _rencoStartPrice,
                    Low = _rencoStartPrice,
                    Open = _rencoStartPrice,
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

        /// <summary>
        /// заставляет выслать на верх все имеющиеся свечки
        /// </summary>
        public void UpdateAllCandles()
        {
            if (CandlesAll == null)
            {
                return;
            }

            UpdateChangeCandle();

        }

        /// <summary>
        /// метод пересылающий готовые свечи выше
        /// </summary>
        private void UpdateChangeCandle()
        {
            if (ServerMaster.StartProgram == ServerStartProgramm.IsTester &&
                (TypeTesterData == TesterDataType.MarketDepthOnlyReadyCandle ||
                TypeTesterData == TesterDataType.TickOnlyReadyCandle))
            {
                return;
            }
            if (СandleUpdeteEvent != null)
            {
                СandleUpdeteEvent(this);
            }
        }

        private DateTime _lastNewCandleFinish = DateTime.MinValue;

        private void UpdateFinishCandle()
        {
            if (ServerMaster.StartProgram == ServerStartProgramm.IsOsTrader)
            {
                if (DateTime.Now < _lastNewCandleFinish.AddSeconds(TimeFrameSpan.TotalSeconds/2))
                {
                    return;
                }
                _lastNewCandleFinish = DateTime.Now;
            }

            if (СandleFinishedEvent != null)
            {
                СandleFinishedEvent(this);
            }
        }

        /// <summary>
        /// добавить свечи в неторговые периоды
        /// </summary>
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

// прямая загрузка серии из свечек

        /// <summary>
        /// загрузить в серию новую свечку
        /// </summary>
        /// <param name="candle"></param>
        public void SetNewCandleInArray(Candle candle)
        {
            if (_timeFrameBuilder.CandleMarketDataType == CandleMarketDataType.MarketDepth)
            {
                return;
            }

            if (CandlesAll == null)
            {
                CandlesAll = new List<Candle>();
            }

            if (CandlesAll.Count == 0)
            {
                CandlesAll.Add(candle);
                return;
            }

            if (CandlesAll[CandlesAll.Count - 1].TimeStart > candle.TimeStart)
            {
                return;
            }

            if (CandlesAll[CandlesAll.Count - 1].TimeStart == candle.TimeStart)
            {
                CandlesAll[CandlesAll.Count - 1] = candle;

                UpdateFinishCandle();
                return;
            }

            CandlesAll.Add(candle);

            UpdateFinishCandle();

        }

        /// <summary>
        /// в серии изменилась последняя свеча
        /// </summary>
        public event Action<CandleSeries> СandleUpdeteEvent;

        /// <summary>
        /// в серии завершилась последняя свеча
        /// </summary>
        public event Action<CandleSeries> СandleFinishedEvent;

// создание свечек из Стакана

        public void SetNewMarketDepth(MarketDepth marketDepth)
        {
            if (_isStarted == false)
            {
                return;
            }

            if (_isStoped)
            {
                return;
            }

            if (_timeFrameBuilder.CandleMarketDataType != CandleMarketDataType.MarketDepth)
            {
                return;
            }


            if (marketDepth.Bids == null ||
                marketDepth.Bids.Count == 0 ||
                marketDepth.Bids[0].Price == 0)
            {
                return;
            }

            if (marketDepth.Asks == null ||
                marketDepth.Asks.Count == 0 ||
                marketDepth.Asks[0].Price == 0)
            {
                return;
            }

            decimal price = marketDepth.Bids[0].Price + (marketDepth.Asks[0].Price - marketDepth.Bids[0].Price)/2;

            UpDateCandle(marketDepth.Time, price, 1, true, Side.UnKnown);
        }


// для тестера

        public TesterDataType TypeTesterData;
    }

    /// <summary>
    /// тип данных для рассчёта свечек в серии свечей
    /// </summary>
    public enum CandleMarketDataType
    {
        /// <summary>
        /// создание свечей из тиков
        /// </summary>
        Tick,

        /// <summary>
        /// создание свечей из центра стакана
        /// </summary>
        MarketDepth
    }

    /// <summary>
    /// метод создания свечей
    /// </summary>
    public enum CandleCreateMethodType
    {
        /// <summary>
        /// свечи с обычным ТФ от 1 секунды и выше
        /// </summary>
        Simple,

        /// <summary>
        /// свечи ренко
        /// </summary>
        Renko,

        /// <summary>
        /// свечи набираются из объёма
        /// </summary>
        Volume,

        /// <summary>
        /// свечи набираемые из тиков
        /// </summary>
        Ticks,

        /// <summary>
        /// свечи завершением которых служит изменение дельты на N открытого интереса
        /// </summary>
        Delta,

        /// <summary>
        /// свечи хейкен аши
        /// </summary>
        HeikenAshi
    }
}
