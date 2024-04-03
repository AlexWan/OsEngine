/*
 * Your rights to use code governed by this license http://o-s-a.net/doc/license_simple_engine.pdf
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System;
using System.Collections.Generic;
using OsEngine.Market.Servers.Tester;

namespace OsEngine.Entity
{

    /// <summary>
    /// a series of candles. The object in which the incoming data is collected candles
    /// </summary>
    public class CandleSeries
    {
        #region Service and properties

        /// <summary>
        /// constructor
        /// </summary>
        /// <param name="timeFrameBuilder">/object that carries timeframe data</param>
        /// <param name="security">security we are subscribed to</param>
        /// <param name="startProgram">the program that created the object</param>
        public CandleSeries(TimeFrameBuilder timeFrameBuilder, Security security, StartProgram startProgram)
        {
            TimeFrameBuilder = timeFrameBuilder;
            Security = security;
            _startProgram = startProgram;

            UID = Guid.NewGuid();
        }

        public Guid UID;

        /// <summary>
        /// blocking empty constructor
        /// </summary>
        private CandleSeries()
        {

        }

        /// <summary>
        /// program that created the object
        /// </summary>
        private StartProgram _startProgram;

        /// <summary>
        /// data from which to collect candles: from ticks or from glasses
        /// </summary>
        public CandleMarketDataType CandleMarketDataType
        {
            get { return TimeFrameBuilder.CandleMarketDataType; }
        }

        /// <summary>
        /// candle assembly type: regular, renko, delta
        /// </summary>
        public CandleCreateMethodType CandleCreateMethodType
        {
            get { return TimeFrameBuilder.CandleCreateMethodType; }
        }

        /// <summary>
        /// time frame settings storage
        /// </summary>
        public TimeFrameBuilder TimeFrameBuilder;

        /// <summary>
        /// unique data accessory string
        /// </summary>
        public string Specification
        {
            get
            {
                string _specification 
                    = Security.NameFull + "_" + TimeFrameBuilder.Specification;

                _specification =
                    _specification.Replace("(", "")
                        .Replace(")", "")
                        .Replace(" ", "")
                        .Replace("\"", "")
                        .Replace("\\", "")
                        .Replace(";", "")
                        .Replace(":", "")
                        .Replace("/", "");


                return _specification;
            }
        }

        /// <summary>
        /// whether the data series has been merged with data from the file system
        /// </summary>
        public bool IsMergedByCandlesFromFile;

        /// <summary>
        /// whether the data series has been merged with trades data from the file system
        /// </summary>
        public bool IsMergedByTradesFromFile;

        /// <summary>
        /// security on which the candles are assembled
        /// </summary>
        public Security Security;

        /// <summary>
        /// timeFrame of collected candlesticks in the form of TimeSpan
        /// </summary>
        public TimeSpan TimeFrameSpan
        {
            get { return TimeFrameBuilder.TimeFrameTimeSpan; }
        }

        /// <summary>
        /// timeframe as an enumeration
        /// </summary>
        public TimeFrame TimeFrame
        {
            get { return TimeFrameBuilder.TimeFrame; }
            set
            {
                TimeFrameBuilder.TimeFrame = value;
            }
        }

        /// <summary>
        /// all assembled candles in this series with All statuses
        /// </summary>
        public List<Candle> CandlesAll;

        /// <summary>
        /// all assembled candles in this series with Finished status
        /// </summary>
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
        /// flag. Whether the series is loaded with primary data
        /// </summary>
        public bool IsStarted
        {
            get { return _isStarted; }
            set { _isStarted = value; }
        }
        private bool _isStarted;

        /// <summary>
        /// whether to keep loading the object
        /// </summary>
        private bool _isStoped;

        /// <summary>
        /// batch calculation series
        /// </summary>
        public void Stop()
        {
            _isStoped = true;
        }

        /// <summary>
        /// clear data
        /// </summary>
        public void Clear()
        {
            _lastTradeIndex = 0;

            if(CandlesAll != null)
            {
                CandlesAll.Clear();
                CandlesAll = null;
            }
        }

        /// <summary>
        /// add new ticks to the series, but load the candles only once, call at the start series
        /// </summary>
        public void PreLoad(List<Trade> trades)
        {
            if (TimeFrameBuilder.CandleMarketDataType == CandleMarketDataType.MarketDepth)
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
                if (trades[i] == null)
                {
                    continue;
                }
                UpDateCandle(trades[i].Time, trades[i].Price, trades[i].Volume, false, trades[i].Side);

                List<Trade> tradesInCandle = CandlesAll[CandlesAll.Count - 1].Trades;

                tradesInCandle.Add(trades[i]);

                CandlesAll[CandlesAll.Count - 1].Trades = tradesInCandle;
            }
            UpdateChangeCandle();

            _isStarted = true;
        }

        /// <summary>
        /// Load trades into existing candles
        /// </summary>
        public void LoadTradesInCandles(List<Trade> trades)
        {
            if(CandlesAll == null ||
                CandlesAll.Count < 2)
            {
                return;
            }

            int candleIndex = 0;

            for(int i = 0;i < trades.Count;i++)
            {
                Trade trade = trades[i];

                if(trade == null)
                {
                    continue;
                }

                for(int j = candleIndex; j < CandlesAll.Count;j++)
                {
                    candleIndex = j;

                    Candle candle = CandlesAll[j];

                    if(j+1 == CandlesAll.Count)
                    {
                        candle.Trades.Add(trade);
                        break;
                    }

                    Candle nextCandle = CandlesAll[j + 1];

                    if(candle == null ||
                        nextCandle == null)
                    {
                        continue;
                    }

                    if(trade.Time >= candle.TimeStart &&
                       trade.Time < nextCandle.TimeStart)
                    {
                        candle.Trades.Add(trade);
                        break;
                    }
                    else
                    {

                    }
                }
            }
        }

        #endregion

        #region Candles building

        /// <summary>
        /// add a new server time to the series
        /// </summary>
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

            if (_startProgram != StartProgram.IsOsTrader)
            {
                return;
            }

            if (TimeFrameBuilder.CandleCreateMethodType != CandleCreateMethodType.Simple)
            {
                return;
            }

            Candle lastCandle = CandlesAll[CandlesAll.Count - 1];

            if(lastCandle.State == CandleState.Finished)
            {
                return;
            }

            if (lastCandle.TimeStart.Add(TimeFrameSpan) < time.AddSeconds(-5))
            {
                // пришло время закрыть свечу
                lastCandle.State = CandleState.Finished;

                UpdateFinishCandle();
            }
        }

        /// <summary>
        /// add new ticks to the series
        /// </summary>
        public void SetNewTicks(List<Trade> trades)
        {
            if (_isStoped || _isStarted == false)
            {
                return;
            }
            if (TimeFrameBuilder.CandleMarketDataType == CandleMarketDataType.MarketDepth)
            {
                return;
            }

            if (_startProgram != StartProgram.IsOsTrader &&
                _startProgram != StartProgram.IsOsData &&
                _startProgram != StartProgram.IsOsConverter &&
                TypeTesterData == TesterDataType.Candle)
            {
                return;
            }

            try
            {
                Trade lastTrade = trades[trades.Count - 1];

                if(lastTrade == null)
                {
                    return;
                }

                if (_lastTradeTime > lastTrade.Time)
                {
                    return;
                }
            }
            catch
            {
                return;
            }

            List<Trade> newTrades = GetActualTrades(trades);

            if(newTrades == null)
            {
                return;
            }

            // обновилось неизвесное кол-во тиков
            for (int i = 0; i < newTrades.Count; i++)
            {
                Trade trade = newTrades[i];

                if (trade == null)
                {
                    continue;
                }

                if (CandlesAll != null &&
                    CandlesAll.Count > 0 &&
                    CandlesAll[CandlesAll.Count - 1].TimeStart > trade.Time)
                {
                    continue;
                }

                if(TimeFrameBuilder.CandleCreateMethodType == CandleCreateMethodType.Simple)
                { // при классической сборке свечек. Когда мы точно знаем когда у свечи закрытие
                    bool saveInNextCandle = true;

                    if (CandlesAll != null &&
                        CandlesAll.Count > 0 &&
                        TimeFrameBuilder.SaveTradesInCandles &&
                        CandlesAll[CandlesAll.Count - 1].TimeStart.Add(TimeFrameSpan) > trade.Time)
                    {
                        CandlesAll[CandlesAll.Count - 1].Trades.Add(trade);
                        saveInNextCandle = false;
                    }

                    UpDateCandle(trade.Time, trade.Price, trade.Volume, true, trade.Side);

                    if (TimeFrameBuilder.SaveTradesInCandles
                        && saveInNextCandle)
                    {
                        CandlesAll[CandlesAll.Count - 1].Trades.Add(trade);
                    }
                }
                else
                { // при любым другом виде свечек

                    UpDateCandle(trade.Time, trade.Price, trade.Volume, true, trade.Side);

                    if (TimeFrameBuilder.SaveTradesInCandles)
                    {
                        CandlesAll[CandlesAll.Count - 1].Trades.Add(trade);
                    }
                }
            }
        }

        /// <summary>
        /// add new market depth to the series
        /// </summary>
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

            if (TimeFrameBuilder.CandleMarketDataType != CandleMarketDataType.MarketDepth)
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

            decimal price = marketDepth.Bids[0].Price + (marketDepth.Asks[0].Price - marketDepth.Bids[0].Price) / 2;

            UpDateCandle(marketDepth.Time, price, 1, true, Side.None);
        }

        private List<Trade> GetActualTrades(List<Trade> trades)
        {

            List<Trade> newTrades = new List<Trade>();

            if (trades.Count > 1000)
            { // если удаление трейдов из системы выключено

                int newTradesCount = trades.Count - _lastTradeIndex;

                if (newTradesCount <= 0)
                {
                    return null;
                }

                newTrades = trades.GetRange(_lastTradeIndex, newTradesCount);
            }
            else
            {
                if (_lastTradeTime == DateTime.MinValue)
                {
                    newTrades = trades;
                }
                else
                {
                    bool isNewTradesFurther = false;

                    for (int i = 0; i < trades.Count; i++)
                    {
                        try
                        {
                            if (trades[i] == null)
                            {
                                continue;
                            }
                            if (trades[i].Time < _lastTradeTime)
                            {
                                continue;
                            }
                            if (trades[i].Time == _lastTradeTime)
                            {
                                if (string.IsNullOrEmpty(trades[i].Id))
                                {
                                    // если IDшников нет - просто игнорируем трейды с идентичным временем
                                    continue;
                                }
                                else if (isNewTradesFurther == false)
                                {
                                    if (IsInArrayTradeIds(trades[i].Id))
                                    {// если IDшник в последних 100 трейдах
                                        continue;
                                    }
                                    // дальше по массиву точно идут новые трейды.
                                    // 1) они новые 2) текущий уже не лежит в старых трейдах
                                    isNewTradesFurther = true;
                                }
                            }

                            newTrades.Add(trades[i]);
                        }
                        catch
                        {
                            continue;
                        }
                    }
                }
            }

            _lastTradeIndex = trades.Count;

            if (newTrades.Count == 0)
            {
                return null;
            }

            _lastTradeTime = newTrades[newTrades.Count - 1].Time;

            for (int i2 = 0; i2 < newTrades.Count; i2++)
            {
                if (newTrades[i2] == null)
                {
                    newTrades.RemoveAt(i2);
                    i2--;
                }
                if (string.IsNullOrEmpty(newTrades[i2].Id) == false)
                {
                    AddInListTradeIds(newTrades[i2].Id, newTrades[i2].Time);
                }
            }

            return newTrades;
        }

        private int _lastTradeIndex;
        private DateTime _lastTradeTime;
        List<string> _lastTradeIds = new List<string>();
        DateTime _idsTime = DateTime.MinValue;

        private void AddInListTradeIds(string id, DateTime _timeNow)
        {
            if(_idsTime.Second != _timeNow.Second 
                && _idsTime < _timeNow)
            {
                _lastTradeIds.Clear();
                _idsTime = _timeNow;
            }

            _lastTradeIds.Add(id);
        }

        private bool IsInArrayTradeIds(string id)
        {
            bool isInArray = false;

            for(int i = 0;i < _lastTradeIds.Count;i++)
            {
                if(_lastTradeIds[i].Equals(id))
                {
                    return true;
                }
            }

            return isInArray;
        }

        /// <summary>
        /// update candle series with new data
        /// </summary>
        /// <param name="time">new data time</param>
        /// <param name="price">price new data</param>
        /// <param name="volume">volume new data</param>
        /// <param name="canPushUp">can the candles be shared with the architects above?</param>
        /// <param name="side">last trade side</param>
        private void UpDateCandle(DateTime time, decimal price, decimal volume, bool canPushUp, Side side)
        {
            if (TimeFrameBuilder.CandleCreateMethodType == CandleCreateMethodType.Simple)
            {
                UpDateSimpleTimeFrame(time, price, volume, canPushUp);
            }
            else if (TimeFrameBuilder.CandleCreateMethodType == CandleCreateMethodType.Delta)
            {
                UpDateDeltaTimeFrame(time, price, volume, canPushUp, side);
            }
            else if (TimeFrameBuilder.CandleCreateMethodType == CandleCreateMethodType.Ticks)
            {
                UpDateTickTimeFrame(time, price, volume, canPushUp);
            }
            else if (TimeFrameBuilder.CandleCreateMethodType == CandleCreateMethodType.Volume)
            {
                UpDateVolumeTimeFrame(time, price, volume, canPushUp);
            }
            else if (TimeFrameBuilder.CandleCreateMethodType == CandleCreateMethodType.Renko)
            {
                UpDateRencoTimeFrame(time, price, volume, canPushUp);
            }
            else if (TimeFrameBuilder.CandleCreateMethodType == CandleCreateMethodType.HeikenAshi)
            {
                UpDateHeikenAshiCandle(time, price, volume, canPushUp);
            }
            else if (TimeFrameBuilder.CandleCreateMethodType == CandleCreateMethodType.Range)
            {
                UpDateRangeCandles(time, price, volume, canPushUp);
            }
            else if (TimeFrameBuilder.CandleCreateMethodType == CandleCreateMethodType.Rеvers)
            {
                UpDateReversCandles(time, price, volume, canPushUp);
            }
        }

        private void UpDateRangeCandles(DateTime time, decimal price, decimal volume, bool canPushUp)
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



            if (CandlesAll != null &&
                CandlesAll[CandlesAll.Count - 1].High - CandlesAll[CandlesAll.Count - 1].Low >= TimeFrameBuilder.RangeCandlesPunkts)
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

            if (CandlesAll != null &&
                CandlesAll[CandlesAll.Count - 1].High - CandlesAll[CandlesAll.Count - 1].Low < TimeFrameBuilder.RangeCandlesPunkts)
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

        private void UpDateReversCandles(DateTime time, decimal price, decimal volume, bool canPushUp)
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

            bool candleReady = false;

            Candle lastCandle = CandlesAll[CandlesAll.Count - 1];

            if (lastCandle.High - lastCandle.Open >= TimeFrameBuilder.ReversCandlesPunktsMinMove && //движение нужное есть
                lastCandle.High - lastCandle.Close >= TimeFrameBuilder.ReversCandlesPunktsBackMove) // откат имеется
            { // есть откат от хая
                candleReady = true;
            }

            if (lastCandle.Open - lastCandle.Low >= TimeFrameBuilder.ReversCandlesPunktsMinMove && //движение нужное есть
                lastCandle.Close - lastCandle.Low >= TimeFrameBuilder.ReversCandlesPunktsBackMove) // откат имеется
            { // есть откат от лоя
                candleReady = true;
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

            if (CandlesAll[CandlesAll.Count - 1].TimeStart.Add(TimeFrameSpan + TimeFrameSpan) <= time &&
                TimeFrameBuilder.SetForeign)
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
                CandlesAll[CandlesAll.Count - 1].Close = Math.Round((CandlesAll[CandlesAll.Count - 1].Open +
                                                          CandlesAll[CandlesAll.Count - 1].High +
                                                          CandlesAll[CandlesAll.Count - 1].Low +
                                                          CandlesAll[CandlesAll.Count - 1].Close) / 4, Security.Decimals);

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
                    Open = Math.Round((CandlesAll[CandlesAll.Count - 1].Open +
                            CandlesAll[CandlesAll.Count - 1].Close) / 2, Security.Decimals),
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

        private void UpDateSimpleTimeFrame(DateTime time, decimal price, decimal volume, bool canPushUp)
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

            if (CandlesAll[CandlesAll.Count - 1].TimeStart.Add(TimeFrameSpan + TimeFrameSpan) <= time &&
                TimeFrameBuilder.SetForeign)
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

        private decimal _currentDelta;

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

            if (side == Side.Buy)
            {
                _currentDelta += volume;
            }
            else
            {
                _currentDelta -= volume;
            }


            if (CandlesAll != null &&
                Math.Abs(_currentDelta) >= TimeFrameBuilder.DeltaPeriods)
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

        private int _lastCandleTickCount;

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
                _lastCandleTickCount >= TimeFrameBuilder.TradeCount)
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
                 _lastCandleTickCount < TimeFrameBuilder.TradeCount)
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
                CandlesAll[CandlesAll.Count - 1].Volume >= TimeFrameBuilder.VolumeToCloseCandleInVolumeType)
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

        private bool _rencoIsBuildShadows;

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
                _rencoLastSide = Side.None;
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

            decimal renDist = TimeFrameBuilder.RencoPunktsToCloseCandleInRencoType;

            if (
                (_rencoLastSide == Side.None && Math.Abs(_rencoStartPrice - price) >= renDist)
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
                    (_rencoLastSide == Side.None && price - _rencoStartPrice >= renDist)
                    ||
                    (_rencoLastSide == Side.Buy && price - _rencoStartPrice >= renDist)
                    )
                {
                    _rencoLastSide = Side.Buy;
                    _rencoStartPrice = _rencoStartPrice + renDist;
                    lastCandle.High = _rencoStartPrice;
                }
                else if (
                (_rencoLastSide == Side.None && _rencoStartPrice - price >= renDist)
                ||
                (_rencoLastSide == Side.Sell && _rencoStartPrice - price >= renDist)
                )
                {
                    _rencoLastSide = Side.Sell;
                    _rencoStartPrice = _rencoStartPrice - renDist;
                    lastCandle.Low = _rencoStartPrice;
                }
                else if (
                    _rencoLastSide == Side.Buy && _rencoStartPrice - price >= renDist * 2)
                {
                    _rencoLastSide = Side.Sell;
                    lastCandle.Open = _rencoStartPrice - renDist;
                    _rencoStartPrice = _rencoStartPrice - renDist * 2;
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

                if (TimeFrameBuilder.RencoIsBuildShadows == false)
                {
                    if (lastCandle.IsUp)
                    {
                        lastCandle.Low = lastCandle.Open;
                        lastCandle.High = lastCandle.Close;
                    }
                    else
                    {
                        lastCandle.High = lastCandle.Open;
                        lastCandle.Low = lastCandle.Close;
                    }
                }

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

        public void UpdateAllCandles()
        {
            if (CandlesAll == null)
            {
                return;
            }

            UpdateChangeCandle();

        }

        private void UpdateChangeCandle()
        {
            if (_startProgram == StartProgram.IsTester &&
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

        private void UpdateFinishCandle()
        {
            if (СandleFinishedEvent != null)
            {
                СandleFinishedEvent(this);
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

        /// <summary>
        /// the last candle in the series has changed
        /// </summary>
        public event Action<CandleSeries> СandleUpdeteEvent;

        /// <summary>
        /// the last candle in the series has finished
        /// </summary>
        public event Action<CandleSeries> СandleFinishedEvent;

        #endregion

        #region Tester

        public TesterDataType TypeTesterData;

        /// <summary>
        /// load a new candle into the series in the tester or optimizer
        /// </summary>
        public void SetNewCandleInArray(Candle candle)
        {
            if (TimeFrameBuilder.CandleMarketDataType == CandleMarketDataType.MarketDepth)
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

        #endregion

    }

    /// <summary>
    /// data type for calculating candlesticks in a candlestick series
    /// </summary>
    public enum CandleMarketDataType
    {
        /// <summary>
        /// creating candles from trades
        /// </summary>
        Tick,

        /// <summary>
        /// creating candles from market depth center
        /// </summary>
        MarketDepth
    }

    /// <summary>
    /// Candles type
    /// </summary>
    public enum CandleCreateMethodType
    {
        Simple,

        Renko,

        Volume,

        Ticks,

        Delta,

        HeikenAshi,

        Rеvers,

        Range
    }
}