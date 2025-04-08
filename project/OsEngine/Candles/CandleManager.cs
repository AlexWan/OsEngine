﻿/*
 * Your rights to use code governed by this license http://o-s-a.net/doc/license_simple_engine.pdf
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows;
using OsEngine.Logging;
using OsEngine.Market;
using OsEngine.Market.Servers;
using OsEngine.Market.Servers.BitMax;
using OsEngine.Market.Servers.Kraken;
using OsEngine.Market.Servers.Tester;
using OsEngine.Market.Servers.ZB;
using OsEngine.Market.Servers.Hitbtc;
using OsEngine.Market.Servers.InteractiveBrokers;
using OsEngine.Market.Servers.BitMaxFutures;

namespace OsEngine.Entity
{
    /// <summary>
    /// keeper of a series of candles. It is created in the server and participates in the process of subscribing to candles.
    /// Stores a series of candles, is responsible for their loading with ticks so that candles are formed in them
    /// </summary>
    public class CandleManager
    {
        #region Service

        /// <summary>
        /// constructor
        /// </summary>
        /// <param name="server">the server from which the candlestick data will go/сервер из которого будут идти данные для создания свечек</param>
        /// <param name="startProgram">the program that created the class object/программа которая создала объект класса</param>
        public CandleManager(IServer server, StartProgram startProgram)
        {
            _server = server;
            _server.NewTradeEvent += server_NewTradeEvent;
            _server.TimeServerChangeEvent += _server_TimeServerChangeEvent;
            _server.NewMarketDepthEvent += _server_NewMarketDepthEvent;
            _candleSeriesNeedToStart = new Queue<CandleSeries>();

            _startProgram = startProgram;

            if (startProgram != StartProgram.IsOsOptimizer)
            {
                Task task = new Task(CandleSeriesStarterThread);
                task.Start();
            }

            TypeTesterData = TesterDataType.Unknown;
        }

        /// <summary>
        /// exchange connection server
        /// </summary>
        private IServer _server;

        /// <summary>
        /// program to which the object CandleManager belongs
        /// </summary>
        StartProgram _startProgram;

        /// <summary>
        /// clear data from the object
        /// </summary>
        public void Dispose()
        {
            _isDisposed = true;

            try
            {
                if (_server != null)
                {
                    _server.NewTradeEvent -= server_NewTradeEvent;
                    _server.TimeServerChangeEvent -= _server_TimeServerChangeEvent;
                    _server.NewMarketDepthEvent -= _server_NewMarketDepthEvent;
                }
            }
            catch
            {
                // ignore
            }

            _server = null;

            try
            {
                for (int i = 0; _activeSeriesBasedOnTrades != null && i < _activeSeriesBasedOnTrades.Count; i++)
                {
                    _activeSeriesBasedOnTrades[i].CandleUpdateEvent -= series_CandleUpdateEvent;
                    _activeSeriesBasedOnTrades[i].CandleFinishedEvent -= series_CandleFinishedEvent;
                    _activeSeriesBasedOnTrades[i].Clear();
                    _activeSeriesBasedOnTrades[i].Stop();
                }
            }
            catch
            {
                // ignore
            }

            _activeSeriesBasedOnTrades = null;

            try
            {
                for (int i = 0; _activeSeriesBasedOnMd != null && i < _activeSeriesBasedOnMd.Count; i++)
                {
                    _activeSeriesBasedOnMd[i].CandleUpdateEvent -= series_CandleUpdateEvent;
                    _activeSeriesBasedOnMd[i].CandleFinishedEvent -= series_CandleFinishedEvent;
                    _activeSeriesBasedOnMd[i].Clear();
                    _activeSeriesBasedOnMd[i].Stop();
                }
            }
            catch
            {
                // ignore
            }

            _activeSeriesBasedOnMd = null;

            try
            {
                if (_candleSeriesNeedToStart != null
                    && _candleSeriesNeedToStart.Count != 0)
                {
                    _candleSeriesNeedToStart.Clear();
                }
            }
            catch
            {
                // ignore
            }
        }

        private bool _isDisposed;

        #endregion

        #region Candle series storing and activation

        /// <summary>
        /// method for operating the thread triggering candle series
        /// </summary>
        private async void CandleSeriesStarterThread()
        {
            try
            {
                while (true)
                {
                    if (_isDisposed == true)
                    {
                        return;
                    }

                    if (MainWindow.ProccesIsWorked == false)
                    {
                        return;
                    }

                    if (_candleSeriesNeedToStart.Count == 0)
                    {
                        await Task.Delay(20);
                    }
                    else
                    {
                        CandleSeries series = _candleSeriesNeedToStart.Dequeue();

                        if (series == null || series.IsStarted)
                        {
                            continue;
                        }

                        if (series.IsStarted)
                        {
                            if (series.CandleMarketDataType == CandleMarketDataType.MarketDepth)
                            {
                                if (_activeSeriesBasedOnMd != null)
                                {
                                    _activeSeriesBasedOnMd.Add(series);
                                }
                            }
                            else if (series.CandleMarketDataType == CandleMarketDataType.Tick)
                            {
                                if (_activeSeriesBasedOnTrades != null)
                                {
                                    _activeSeriesBasedOnTrades.Add(series);
                                }
                            }

                            continue;
                        }

                        ServerType serverType = _server.ServerType;

                        if (serverType == ServerType.Tester)
                        {
                            series.IsStarted = true;
                        }

                        IServerPermission permission = ServerMaster.GetServerPermission(serverType);

                        if (permission != null &&
                           permission.UseStandardCandlesStarter == true)
                        {
                            // !!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!
                            // NEW STANDART CANDLE SERIES START 2024
                            if (series.CandleCreateMethodType != "Simple" ||
                                series.TimeFrameSpan.TotalMinutes < 1)
                            {
                                List<Trade> allTrades = _server.GetAllTradesToSecurity(series.Security);
                                series.PreLoad(allTrades);
                            }
                            else
                            {
                                List<Candle> candles = _server.GetLastCandleHistory(series.Security, series.TimeFrameBuilder, 500);

                                if (candles != null)
                                {
                                    series.CandlesAll = candles;
                                }
                            }

                            series.UpdateAllCandles();
                            series.IsStarted = true;
                        }
                        else if (serverType == ServerType.Plaza ||
                                 serverType == ServerType.QuikDde ||
                                 serverType == ServerType.AstsBridge ||
                                 serverType == ServerType.NinjaTrader ||
                                 serverType == ServerType.Lmax ||
                                 serverType == ServerType.MoexFixFastSpot)
                        {
                            series.CandlesAll = null;
                            // further, we try to load candles with ticks
                            // далее, пытаемся пробуем прогрузить свечи при помощи тиков
                            List<Trade> allTrades = _server.GetAllTradesToSecurity(series.Security);

                            series.PreLoad(allTrades);
                            // if there is a preloading of candles on the server and something is downloaded
                            // если на сервере есть предзагрузка свечек и что-то скачалось 
                            series.UpdateAllCandles();

                            series.IsStarted = true;
                        }
                        else if (serverType == ServerType.Tester ||
                                 serverType == ServerType.Optimizer ||
                                 serverType == ServerType.BitStamp
                            )
                        {
                            series.IsStarted = true;
                        }
                        else if (serverType == ServerType.Kraken)
                        {
                            KrakenServer kraken = (KrakenServer)_server;

                            if (series.CandleCreateMethodType != "Simple" ||
                                series.TimeFrameSpan.TotalMinutes < 1)
                            {
                                List<Trade> allTrades = _server.GetAllTradesToSecurity(series.Security);
                                series.PreLoad(allTrades);
                            }
                            else
                            {
                                List<Candle> candles = kraken.GetCandleHistory(series.Security.Name,
                                    series.TimeFrameSpan);
                                if (candles != null)
                                {
                                    series.CandlesAll = candles;
                                }
                            }
                            series.UpdateAllCandles();
                            series.IsStarted = true;
                        }
                        else if (serverType == ServerType.InteractiveBrokers)
                        {
                            InteractiveBrokersServer server = (InteractiveBrokersServer)_server;
                            if (series.CandleCreateMethodType != "Simple" ||
                                series.TimeFrameSpan.TotalMinutes < 1)
                            {
                                List<Trade> allTrades = _server.GetAllTradesToSecurity(series.Security);
                                series.PreLoad(allTrades);
                            }
                            else
                            {
                                List<Candle> candles = server.GetCandleHistory(series.Security.Name,
                                    series.TimeFrame);
                                if (candles != null)
                                {
                                    series.CandlesAll = candles;
                                }
                            }
                            series.UpdateAllCandles();
                            series.IsStarted = true;
                        }
                        else if (serverType == ServerType.AscendEx_BitMax)
                        {
                            BitMaxProServer bitMax = (BitMaxProServer)_server;
                            if (series.CandleCreateMethodType != "Simple" ||
                                series.TimeFrameSpan.TotalMinutes < 1)
                            {
                                List<Trade> allTrades = _server.GetAllTradesToSecurity(series.Security);
                                series.PreLoad(allTrades);
                            }
                            else
                            {
                                List<Candle> candles = bitMax.GetCandleHistory(series.Security.Name,
                                    series.TimeFrameSpan);
                                if (candles != null)
                                {
                                    series.CandlesAll = candles;
                                }
                            }
                            series.UpdateAllCandles();
                            series.IsStarted = true;
                        }
                       
                        else if (serverType == ServerType.Exmo)
                        {
                            List<Trade> allTrades = _server.GetAllTradesToSecurity(series.Security);

                            series.PreLoad(allTrades);
                            series.UpdateAllCandles();
                            series.IsStarted = true;
                        }
                        else if (serverType == ServerType.Zb)
                        {
                            ZbServer zbServer = (ZbServer)_server;

                            if (series.CandleCreateMethodType != "Simple" ||
                                series.TimeFrameSpan.TotalMinutes < 1)
                            {
                                List<Trade> allTrades = _server.GetAllTradesToSecurity(series.Security);
                                series.PreLoad(allTrades);
                            }
                            else
                            {
                                List<Candle> candles = zbServer.GetCandleHistory(series.Security.Name, series.TimeFrameSpan);

                                if (candles != null)
                                {
                                    series.CandlesAll = candles;
                                }
                            }
                            series.UpdateAllCandles();
                            series.IsStarted = true;
                        }
                        else if (serverType == ServerType.Hitbtc)
                        {
                            HitbtcServer hitbtc = (HitbtcServer)_server;
                            if (series.CandleCreateMethodType != "Simple" ||
                                series.TimeFrameSpan.TotalMinutes < 1)
                            {
                                List<Trade> allTrades = _server.GetAllTradesToSecurity(series.Security);
                                series.PreLoad(allTrades);
                            }
                            else
                            {
                                List<Candle> candles = hitbtc.GetCandleHistory(series.Security.Name,
                                    series.TimeFrameSpan);
                                if (candles != null)
                                {
                                    series.CandlesAll = candles;
                                }
                            }
                            series.UpdateAllCandles();
                            series.IsStarted = true;
                        }
                        else if (serverType == ServerType.Bitmax_AscendexFutures)
                        {
                            if (series.CandleCreateMethodType != "Simple" ||
                                series.TimeFrameSpan.TotalMinutes < 1)
                            {
                                List<Trade> allTrades = _server.GetAllTradesToSecurity(series.Security);
                                series.PreLoad(allTrades);
                            }
                            else
                            {
                                BitMaxFuturesServer okx = (BitMaxFuturesServer)_server;
                                List<Candle> candles = okx.GetCandleHistory(series.Security.Name,
                                    series.TimeFrameSpan);
                                if (candles != null)
                                {
                                    series.CandlesAll = candles;
                                }
                            }
                            series.UpdateAllCandles();
                            series.IsStarted = true;
                        }


                        if (series.CandleMarketDataType == CandleMarketDataType.MarketDepth)
                        {
                            if (_activeSeriesBasedOnMd != null)
                                _activeSeriesBasedOnMd.Add(series);
                        }
                        else if (series.CandleMarketDataType == CandleMarketDataType.Tick)
                        {
                            if (_activeSeriesBasedOnTrades != null)
                                _activeSeriesBasedOnTrades.Add(series);
                        }

                    }
                }
            }
            catch (Exception error)
            {
                SendLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        /// <summary>
        /// start creating candles in a new series of candles
        /// </summary>
        public void StartSeries(CandleSeries series)
        {
            try
            {
                if (_server.ServerType != ServerType.Tester &&
                    _server.ServerType != ServerType.Optimizer &&
                    _server.ServerType != ServerType.Miner)
                {
                    series.CandleUpdateEvent += series_CandleUpdateEvent;
                }

                series.TypeTesterData = _typeTesterData;
                series.CandleFinishedEvent += series_CandleFinishedEvent;

                if (_activeSeriesBasedOnTrades == null)
                {
                    _activeSeriesBasedOnTrades = new List<CandleSeries>();
                }

                if (_activeSeriesBasedOnMd == null)
                {
                    _activeSeriesBasedOnMd = new List<CandleSeries>();
                }

                if (_startProgram == StartProgram.IsOsTrader)
                {
                    _candleSeriesNeedToStart.Enqueue(series);
                }
                else
                {
                    if (series.CandleMarketDataType == CandleMarketDataType.MarketDepth)
                    {
                        _activeSeriesBasedOnMd.Add(series);
                    }
                    else if (series.CandleMarketDataType == CandleMarketDataType.Tick)
                    {
                        _activeSeriesBasedOnTrades.Add(series);
                    }
                    series.IsStarted = true;
                }
            }
            catch (Exception error)
            {
                SendLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        /// <summary>
        /// stop loading candles by series
        /// </summary>
        public void StopSeries(CandleSeries series)
        {
            try
            {
                if (series == null
                    || series.UID == null)
                {
                    return;
                }

                series.CandleUpdateEvent -= series_CandleUpdateEvent;
                series.CandleFinishedEvent -= series_CandleFinishedEvent;

                for (int i = 0; _activeSeriesBasedOnTrades != null && i < _activeSeriesBasedOnTrades.Count; i++)
                {
                    CandleSeries curSeries = _activeSeriesBasedOnTrades[i];

                    if (curSeries == null ||
                        curSeries.UID == null)
                    {
                        return;
                    }

                    if (curSeries.UID == series.UID)
                    {
                        if (_activeSeriesBasedOnTrades != null)
                        {
                            _activeSeriesBasedOnTrades.RemoveAt(i);
                        }

                        break;
                    }
                }


                for (int i = 0; _activeSeriesBasedOnMd != null && i < _activeSeriesBasedOnMd.Count; i++)
                {
                    CandleSeries curSeries = _activeSeriesBasedOnMd[i];

                    if (curSeries == null ||
                        curSeries.UID == null)
                    {
                        return;
                    }

                    if (curSeries.UID == series.UID)
                    {
                        if (_activeSeriesBasedOnMd != null)
                        {
                            _activeSeriesBasedOnMd.RemoveAt(i);
                        }

                        break;
                    }
                }
            }
            catch
            {
                //SendLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        /// <summary>
        /// the turn of the series of candles to be loaded
        /// </summary>
        private Queue<CandleSeries> _candleSeriesNeedToStart;

        /// <summary>
        /// active series collecting candlesticks from the trade tape
        /// </summary>
        private List<CandleSeries> _activeSeriesBasedOnTrades;

        /// <summary>
        /// active series collecting candlesticks from the market depth
        /// </summary>
        private List<CandleSeries> _activeSeriesBasedOnMd;

        /// <summary>
        /// Number of active candleSeries
        /// </summary>
        public int ActiveSeriesCount
        {
            get
            {
                int result = 0;

                if (_activeSeriesBasedOnTrades != null)
                {
                    result += _activeSeriesBasedOnTrades.Count;
                }

                if (_activeSeriesBasedOnMd != null)
                {
                    result += _activeSeriesBasedOnMd.Count;
                }

                return result;
            }
        }

        /// <summary>
        /// request a series of candlesticks for an instrument with certain settings
        /// </summary>
        public CandleSeries GetSeries(TimeFrameBuilder timeFrameBuilder, Security security)
        {
            for (int i = 0; _activeSeriesBasedOnTrades != null && i < _activeSeriesBasedOnTrades.Count; i++)
            {
                CandleSeries curSeries = _activeSeriesBasedOnTrades[i];

                if (curSeries.Security.Name != security.Name ||
                    curSeries.Security.NameClass != security.NameClass)
                {
                    continue;
                }

                if (curSeries.TimeFrameBuilder.Specification.Equals(timeFrameBuilder.Specification) == false)
                {
                    continue;
                }

                return _activeSeriesBasedOnTrades[i];
            }

            for (int i = 0; _activeSeriesBasedOnMd != null && i < _activeSeriesBasedOnMd.Count; i++)
            {
                CandleSeries curSeries = _activeSeriesBasedOnMd[i];

                if (curSeries.Security.Name != security.Name ||
                    curSeries.Security.NameClass != security.NameClass)
                {
                    continue;
                }

                if (curSeries.TimeFrameBuilder.Specification.Equals(timeFrameBuilder.Specification) == false)
                {
                    continue;
                }

                return _activeSeriesBasedOnMd[i];
            }

            return null;
        }

        #endregion

        #region Events from server

        /// <summary>
        /// server time has changed. Inbound event
        /// </summary>
        private void _server_TimeServerChangeEvent(DateTime dateTime)
        {
            if (_isDisposed)
            {
                return;
            }

            try
            {
                for (int i = 0; _activeSeriesBasedOnTrades != null && i < _activeSeriesBasedOnTrades.Count; i++)
                {
                    _activeSeriesBasedOnTrades[i].SetNewTime(dateTime);
                }

                for (int i = 0; _activeSeriesBasedOnMd != null && i < _activeSeriesBasedOnMd.Count; i++)
                {
                    _activeSeriesBasedOnMd[i].SetNewTime(dateTime);
                }

            }
            catch (Exception error)
            {
                SendLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        /// <summary>
        /// A new tick appeared in the server. Inbound event
        /// </summary>
        private void server_NewTradeEvent(List<Trade> trades)
        {
            try
            {
                if (_isDisposed)
                {
                    return;
                }

                if ((_server.ServerType == ServerType.Tester 
                    || _server.ServerType == ServerType.Optimizer)
                    &&
                    TypeTesterData == TesterDataType.Candle)
                {
                    return;
                }

                if (trades == null ||
                    trades.Count == 0 ||
                    trades[0] == null)
                {
                    return;
                }

                if (_activeSeriesBasedOnTrades == null)
                {
                    return;
                }

                string secCode = trades[0].SecurityNameCode;

                try
                {
                    for (int i = 0; _activeSeriesBasedOnTrades != null &&
                        i < _activeSeriesBasedOnTrades.Count; i++)
                    {
                        if (_activeSeriesBasedOnTrades[i] == null ||
                            _activeSeriesBasedOnTrades[i].Security == null ||
                            _activeSeriesBasedOnTrades[i].TimeFrameBuilder.CandleSeriesRealization == null)
                        {
                            continue;
                        }
                        if (_activeSeriesBasedOnTrades[i].Security.Name == secCode)
                        {
                            _activeSeriesBasedOnTrades[i].SetNewTicks(trades);
                        }
                    }
                }
                catch
                {
                    // ignore
                }
            }
            catch (Exception error)
            {
                SendLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        /// <summary>
        /// from the server came a new market depth
        /// </summary>
        private void _server_NewMarketDepthEvent(MarketDepth marketDepth)
        {
            if (_isDisposed)
            {
                return;
            }

            if (_server == null)
            {
                return;
            }

            try
            {
                if (_server.ServerType == ServerType.Tester &&
                     TypeTesterData == TesterDataType.Candle)
                {
                    return;
                }

                if (_activeSeriesBasedOnMd == null)
                {
                    return;
                }

                for (int i = 0; i < _activeSeriesBasedOnMd.Count; i++)
                {
                    if (_activeSeriesBasedOnMd[i] == null ||
                        _activeSeriesBasedOnMd[i].Security == null)
                    {
                        continue;
                    }

                    if (_activeSeriesBasedOnMd[i].Security.Name == marketDepth.SecurityNameCode)
                    {
                        _activeSeriesBasedOnMd[i].SetNewMarketDepth(marketDepth);
                    }
                }
            }
            catch (Exception error)
            {
                SendLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        #endregion

        #region Events from candleSeries

        /// <summary>
        /// candles were updated in one of the series. Inbound event
        /// </summary>
        void series_CandleUpdateEvent(CandleSeries series)
        {
            if (_isDisposed)
            {
                return;
            }

            if (CandleUpdateEvent != null)
            {
                CandleUpdateEvent(series);
            }
        }

        void series_CandleFinishedEvent(CandleSeries series)
        {
            if (_isDisposed)
            {
                return;
            }

            if (CandleUpdateEvent != null)
            {
                CandleUpdateEvent(series);
            }
        }

        /// <summary>
        /// candle refreshed
        /// обновилась свечка
        /// </summary>
        public event Action<CandleSeries> CandleUpdateEvent;

        #endregion

        #region Tester

        /// <summary>
        /// loading a new candle in the series in the tester 
        /// </summary>
        public void SetNewCandleInSeries(Candle candle, string nameSecurity, TimeSpan timeFrame)
        {

            for (int i = 0; _activeSeriesBasedOnTrades != null && i < _activeSeriesBasedOnTrades.Count; i++)
            {
                if (_activeSeriesBasedOnTrades[i] == null ||
                    _activeSeriesBasedOnTrades[i].Security == null)
                {
                    continue;
                }

                if (_activeSeriesBasedOnTrades[i].Security.Name == nameSecurity && _activeSeriesBasedOnTrades[i].TimeFrameSpan == timeFrame)
                {
                    _activeSeriesBasedOnTrades[i].SetNewCandleInArray(candle);
                }
            }

            for (int i = 0; _activeSeriesBasedOnMd != null && i < _activeSeriesBasedOnMd.Count; i++)
            {
                if (_activeSeriesBasedOnMd[i] == null ||
                    _activeSeriesBasedOnMd[i].Security == null)
                {
                    continue;
                }
                if (_activeSeriesBasedOnMd[i].Security.Name == nameSecurity && _activeSeriesBasedOnMd[i].TimeFrameSpan == timeFrame)
                {
                    _activeSeriesBasedOnMd[i].SetNewCandleInArray(candle);
                }
            }
        }

        /// <summary>
        /// clear series from old data
        /// </summary>
        public void Clear()
        {
            try
            {
                if (_activeSeriesBasedOnTrades != null)
                {
                    for (int i = 0; i < _activeSeriesBasedOnTrades.Count; i++)
                    {
                        _activeSeriesBasedOnTrades[i].Clear();
                    }
                }
            }
            catch
            {
                // ignore
            }

            try
            {
                if (_activeSeriesBasedOnMd != null)
                {
                    for (int i = 0; i < _activeSeriesBasedOnMd.Count; i++)
                    {
                        _activeSeriesBasedOnMd[i].Clear();
                    }
                }
            }
            catch
            {
                // ignore
            }

            try
            {
                if (_candleSeriesNeedToStart != null
               && _candleSeriesNeedToStart.Count != 0)
                {
                    _candleSeriesNeedToStart.Clear();
                }
            }
            catch
            {
                // ignore
            }
        }

        /// <summary>
        /// sync received data
        /// </summary>
        public void SynhSeries(List<string> nameSecurities)
        {
            if (nameSecurities == null || nameSecurities.Count == 0)
            {
                return;
            }

            List<CandleSeries> mySeries = new List<CandleSeries>();

            for (int i = 0; _activeSeriesBasedOnTrades != null && i < _activeSeriesBasedOnTrades.Count; i++)
            {
                if (nameSecurities.Find(nameSec => nameSec == _activeSeriesBasedOnTrades[i].Security.Name) != null)
                {
                    mySeries.Add(_activeSeriesBasedOnTrades[i]);
                }
            }
            _activeSeriesBasedOnTrades = mySeries;

        }

        /// <summary>
        /// data type that tester ordered
        /// </summary>
        public TesterDataType TypeTesterData
        {
            get { return _typeTesterData; }
            set
            {
                _typeTesterData = value;
                for (int i = 0; _activeSeriesBasedOnTrades != null && i < _activeSeriesBasedOnTrades.Count; i++)
                {
                    _activeSeriesBasedOnTrades[i].TypeTesterData = value;
                }

                for (int i = 0; _activeSeriesBasedOnMd != null && i < _activeSeriesBasedOnMd.Count; i++)
                {
                    _activeSeriesBasedOnMd[i].TypeTesterData = value;
                }
            }

        }
        private TesterDataType _typeTesterData;

        #endregion

        #region Log

        private void SendLogMessage(string message, LogMessageType type)
        {
            if (LogMessageEvent != null)
            {
                LogMessageEvent(message, type);
            }
            else if (type == LogMessageType.Error
                && _isDisposed != true)
            {
                MessageBox.Show(message);
            }
        }

        public event Action<string, LogMessageType> LogMessageEvent;

        #endregion
    }
}