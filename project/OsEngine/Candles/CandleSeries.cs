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
            timeFrameBuilder.CandleSeriesRealization.Security = security;
            timeFrameBuilder.CandleUpdateEvent += TimeFrameBuilder_CandleUpdateEvent;
            timeFrameBuilder.CandleFinishedEvent += TimeFrameBuilder_CandleFinishedEvent;
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
        /// data from which to collect candles: from ticks or from market depth
        /// </summary>
        public CandleMarketDataType CandleMarketDataType
        {
            get { return TimeFrameBuilder.CandleMarketDataType; }
        }

        /// <summary>
        /// candle assembly type: simple, renko, delta, volume, etc
        /// </summary>
        public string CandleCreateMethodType
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
        /// timeframe
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
        public List<Candle> CandlesAll
        {
            get
            {
                if (TimeFrameBuilder == null
                    || TimeFrameBuilder.CandleSeriesRealization == null)
                {
                    return null;
                }

                return TimeFrameBuilder.CandleSeriesRealization.CandlesAll;
            }
            set
            {
                if(TimeFrameBuilder.CandleSeriesRealization == null)
                {
                    return;
                }
                
                TimeFrameBuilder.CandleSeriesRealization.CandlesAll = value;
            }
        }

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

                if (history == null ||
                    history.Count == 0)
                {
                    return null;
                }

                if (history[history.Count - 1].State != CandleState.Finished)
                {
                    return history.GetRange(0, history.Count - 1);
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

        public bool IsSendInStandardStarter;

        /// <summary>
        /// whether to keep loading the object
        /// </summary>
        private bool _isStoped;

        /// <summary>
        /// stop calculation series
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

            if (CandlesAll != null)
            {
                CandlesAll.Clear();
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
            if (CandlesAll == null ||
                CandlesAll.Count < 2)
            {
                return;
            }

            int candleIndex = 0;

            for (int i = 0; i < trades.Count; i++)
            {
                Trade trade = trades[i];

                if (trade == null)
                {
                    continue;
                }

                List<Candle> candlesAll = CandlesAll;

                for (int j = candleIndex; j < candlesAll.Count; j++)
                {
                    candleIndex = j;

                    Candle candle = candlesAll[j];

                    if (j + 1 == candlesAll.Count)
                    {
                        candle.Trades.Add(trade);
                        break;
                    }

                    Candle nextCandle = candlesAll[j + 1];

                    if (candle == null ||
                        nextCandle == null)
                    {
                        continue;
                    }

                    if (trade.Time >= candle.TimeStart &&
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

        public void SetNewTime(DateTime time)
        {
            if (_isStoped || _isStarted == false)
            {
                return;
            }

            if (CandlesAll == null 
                || CandlesAll.Count == 0)
            {
                return;
            }

            if (_startProgram != StartProgram.IsOsTrader)
            {
                return;
            }

            if (TimeFrameBuilder.CandleCreateMethodType != "Simple")
            {
                return;
            }

            Candle lastCandle = null;
            
            try
            {
                lastCandle = CandlesAll[CandlesAll.Count - 1];
            }
            catch
            {
                return;
            }
            

            if (lastCandle == null
                || lastCandle.State == CandleState.Finished)
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

                if (lastTrade == null)
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

            if (newTrades == null)
            {
                return;
            }

            // обновилось неизвестное кол-во тиков
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

                if (TimeFrameBuilder.CandleCreateMethodType == "Simple")
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

                    if(trade.OpenInterest != 0)
                    {
                        CandlesAll[CandlesAll.Count - 1].OpenInterest = trade.OpenInterest;
                    }
                }
                else
                { // при любым другом виде свечек

                    UpDateCandle(trade.Time, trade.Price, trade.Volume, true, trade.Side);

                    if (TimeFrameBuilder.SaveTradesInCandles)
                    {
                        CandlesAll[CandlesAll.Count - 1].Trades.Add(trade);
                    }

                    if (trade.OpenInterest != 0)
                    {
                        CandlesAll[CandlesAll.Count - 1].OpenInterest = trade.OpenInterest;
                    }
                }
            }
        }

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

            decimal bid = marketDepth.Bids[0].Price.ToDecimal();
            decimal ask = marketDepth.Asks[0].Price.ToDecimal();

            if (TimeFrameBuilder.MarketDepthBuildMaxSpreadIsOn
                && TimeFrameBuilder.MarketDepthBuildMaxSpread > 0)
            {
                decimal spread = ask - bid;

                if(spread > 0)
                {
                    decimal spreadPercent = spread / (bid / 100);
                    
                    if(spreadPercent > TimeFrameBuilder.MarketDepthBuildMaxSpread)
                    {
                        return;
                    }
                }
            }

            decimal price = bid + (ask - bid) / 2;

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
                    _lastTradeTime = trades[trades.Count - 1].Time;
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
                                if (string.IsNullOrEmpty(trades[i].Id) &&
                                    trades[i].IdInTester == 0)
                                {
                                    // если IDшников нет - просто игнорируем трейды с идентичным временем
                                    continue;
                                }
                                else if (trades[i].IdInTester != 0)
                                {
                                    if (trades[i].IdInTester <= _lastTradeIndexInTester)
                                    {
                                        continue;
                                    }
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

            _lastTradeIndexInTester = newTrades[newTrades.Count - 1].IdInTester;

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

        private long _lastTradeIndexInTester;

        private DateTime _lastTradeTime;

        private List<string> _lastTradeIds = new List<string>();

        private DateTime _idsTime = DateTime.MinValue;

        private void AddInListTradeIds(string id, DateTime _timeNow)
        {
            if (_idsTime.Second != _timeNow.Second
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

            for (int i = 0; i < _lastTradeIds.Count; i++)
            {
                if (_lastTradeIds[i].Equals(id))
                {
                    return true;
                }
            }

            return isInArray;
        }

        private void UpDateCandle(DateTime time, decimal price, decimal volume, bool canPushUp, Side side)
        {
            TimeFrameBuilder.CandleSeriesRealization.UpDateCandle(time, price, volume, canPushUp, side);
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
            if ((_startProgram == StartProgram.IsTester 
                || _startProgram == StartProgram.IsOsOptimizer) 
                &&
                (TypeTesterData == TesterDataType.MarketDepthOnlyReadyCandle ||
                TypeTesterData == TesterDataType.TickOnlyReadyCandle))
            {
                return;
            }
            if (CandleUpdateEvent != null)
            {
                CandleUpdateEvent(this);
            }
        }

        private void UpdateFinishCandle()
        {
            if (CandleFinishedEvent != null)
            {
                CandleFinishedEvent(this);
            }
        }

        private void TimeFrameBuilder_CandleUpdateEvent(List<Candle> candles)
        {
            UpdateChangeCandle();
        }

        private void TimeFrameBuilder_CandleFinishedEvent(List<Candle> candles)
        {
            UpdateFinishCandle();
        }

        public event Action<CandleSeries> CandleUpdateEvent;

        public event Action<CandleSeries> CandleFinishedEvent;

        #endregion

        #region Tester / Optimizer

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