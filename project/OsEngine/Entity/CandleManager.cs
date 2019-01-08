/*
 *Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using System.Windows;
using OsEngine.Logging;
using OsEngine.Market;
using OsEngine.Market.Servers;
using OsEngine.Market.Servers.Binance;
using OsEngine.Market.Servers.Bitfinex;
using OsEngine.Market.Servers.BitMex;
using OsEngine.Market.Servers.Kraken;
using OsEngine.Market.Servers.QuikLua;
using OsEngine.Market.Servers.SmartCom;
using OsEngine.Market.Servers.Tester;

namespace OsEngine.Entity
{
    /// <summary>
    /// хранитель серий свечек. Создаётся в сервере и участвует в процессе подписки на свечки. 
    /// Хранит в себе серии свечек, отвечает за их прогрузку тиками, чтобы в них формировались свечи
    /// </summary>
    public class CandleManager
    {
        /// <summary>
        /// конструктор
        /// </summary>
        /// <param name="server">сервер из которго будут идти данные для создания свечек</param>
        /// <param name="startProgram">программа которая создала объект класса</param>
        public CandleManager(IServer server)
        {
            _server = server;
            _server.NewTradeEvent += server_NewTradeEvent;
            _server.TimeServerChangeEvent += _server_TimeServerChangeEvent;
            _server.NewMarketDepthEvent += _server_NewMarketDepthEvent;
            _candleSeriesNeadToStart = new Queue<CandleSeries>();

            Thread worker = new Thread(CandleStarter);
            worker.CurrentCulture = new CultureInfo("ru-RU");
            worker.IsBackground = true;
            worker.Name = "CandleStarter";
            worker.Start();

            TypeTesterData = TesterDataType.Unknown;
        }

        /// <summary>
        /// время сервера изменилось. Входящее событие
        /// </summary>
        /// <param name="dateTime">новое время сервера</param>
        private void _server_TimeServerChangeEvent(DateTime dateTime)
        {
            try
            {
                if (_activSeries == null)
                {
                    return;
                }

                for (int i = 0; i < _activSeries.Count; i++)
                {
                    _activSeries[i].SetNewTime(dateTime);
                }
            }
            catch (Exception error)
            {
                SendLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        /// <summary>
        /// в сервере появился новый тик. Входящее событие
        /// </summary>
        /// <param name="trades">новый тик</param>
        private void server_NewTradeEvent(List<Trade> trades)
        {
            if (_server.ServerType == ServerType.Tester &&
                TypeTesterData == TesterDataType.Candle)
            {
                return;
            }
            try
            {
                if (_activSeries == null)
                {
                    return;
                }

                for (int i = 0; i < _activSeries.Count; i++)
                {
                    if (_activSeries[i].CandleMarketDataType == CandleMarketDataType.Tick &&
                        _activSeries[i].Security.Name == trades[0].SecurityNameCode)
                    {
                        _activSeries[i].SetNewTicks(trades);
                    }
                }
            }
            catch (Exception error)
            {
                SendLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        /// <summary>
        /// из сервера пришол новый стакан
        /// </summary>
        /// <param name="marketDepth"></param>
        void _server_NewMarketDepthEvent(MarketDepth marketDepth)
        {
            if (_server.ServerType == ServerType.Tester &&
                TypeTesterData == TesterDataType.Candle)
            {
                return;
            }

            try
            {
                if (_activSeries == null)
                {
                    return;
                }

                for (int i = 0; i < _activSeries.Count; i++)
                {
                    if (_activSeries[i].CandleMarketDataType == CandleMarketDataType.MarketDepth &&
                        _activSeries[i].Security.Name == marketDepth.SecurityNameCode)
                    {
                        _activSeries[i].SetNewMarketDepth(marketDepth);
                    }
                }
            }
            catch (Exception error)
            {
                SendLogMessage(error.ToString(), LogMessageType.Error);
            }
        }


        /// <summary>
        /// начать создавать свечи в новой серии свечек
        /// </summary>
        /// <param name="series">CandleSeries который нужно запустить</param>
        public void StartSeries(CandleSeries series)
        {
            try
            {
                if (_server.ServerType != ServerType.Tester &&
                    _server.ServerType != ServerType.Optimizer &&
                    _server.ServerType != ServerType.Miner)
                {
                    series.СandleUpdeteEvent += series_СandleUpdeteEvent; 
                }

                series.TypeTesterData = _typeTesterData;
                series.СandleFinishedEvent += series_СandleFinishedEvent;

                if (_activSeries == null)
                {
                    _activSeries = new List<CandleSeries>();
                }

                _activSeries.Add(series);

                _candleSeriesNeadToStart.Enqueue(series);
            }
            catch (Exception error)
            {
                SendLogMessage(error.ToString(), LogMessageType.Error);
            }
           
        }

        /// <summary>
        /// очередь серий свечек которую нужно подгрузить
        /// </summary>
        private Queue<CandleSeries> _candleSeriesNeadToStart;

        /// <summary>
        /// метод, в котором работает поток обрабатывающий очередь _candleSeriesNeadToStart
        /// </summary>
        private void CandleStarter()
        {
            try
            {
                while (true)
                {

                    Thread.Sleep(50);

                    if (_candleSeriesNeadToStart.Count != 0)
                    {
                        CandleSeries series = _candleSeriesNeadToStart.Dequeue();

                        if (series == null)
                        {
                            continue;
                        }

                        ServerType serverType = _server.ServerType;

                        if (serverType == ServerType.Tester)
                        {
                            series.IsStarted = true;
                        }

                        else if (serverType == ServerType.Plaza ||
                                 serverType == ServerType.QuikDde ||
                                 serverType == ServerType.AstsBridge ||
                                 serverType == ServerType.NinjaTrader ||
                                 serverType == ServerType.Lmax ||

                                 (serverType == ServerType.InteractivBrokers
                                  && (series.CandlesAll == null || series.CandlesAll.Count == 0)))
                        {
                            series.CandlesAll = null;
                            // далее, пытаемся пробуем прогрузить свечи при помощи тиков
                            List<Trade> allTrades = _server.GetAllTradesToSecurity(series.Security);

                            series.PreLoad(allTrades);

                            // если на сервере есть предзагрузка свечек и что-то скачалось 
                            series.UpdateAllCandles();

                            series.IsStarted = true;
                        }
                        else if (serverType == ServerType.SmartCom)
                        {
                            SmartComServer smart = (SmartComServer) _server;

                            if (series.CandleCreateMethodType != CandleCreateMethodType.Simple ||
                                series.TimeFrameSpan.TotalMinutes < 1)
                            {
                                List<Trade> allTrades = _server.GetAllTradesToSecurity(series.Security);

                                series.PreLoad(allTrades);
                            }
                            else
                            {
                                List<Candle> candles = smart.GetSmartComCandleHistory(series.Security.Name,
                                    series.TimeFrameSpan, 170);
                                if (candles != null)
                                {
                                    candles.Reverse();
                                    series.CandlesAll = candles;
                                }
                            }
                            series.UpdateAllCandles();
                            series.IsStarted = true;
                        }
                        else if (serverType == ServerType.Tester ||
                                 serverType == ServerType.InteractivBrokers ||
                                 serverType == ServerType.Optimizer ||
                                 serverType == ServerType.Oanda||
                                 serverType == ServerType.BitStamp
                            )
                        {
                            series.IsStarted = true;
                        }
                        else if (serverType == ServerType.QuikLua)
                        {
                            QuikLuaServer luaServ = (QuikLuaServer)_server;
                            if (series.CandleCreateMethodType != CandleCreateMethodType.Simple || 
                                series.TimeFrameSpan.TotalMinutes < 1)
                            {
                                List<Trade> allTrades = _server.GetAllTradesToSecurity(series.Security);

                                series.PreLoad(allTrades);
                            }
                            else
                            {
                                List<Candle> candles = luaServ.GetQuikLuaCandleHistory(series.Security.Name,
                                    series.TimeFrameSpan);
                                if (candles != null)
                                {
                                    //candles.Reverse();
                                    series.CandlesAll = candles;
                                }
                            }
                            series.UpdateAllCandles();
                            series.IsStarted = true;
                        }
                        else if (serverType == ServerType.BitMex)
                        {
                            BitMexServer bitMex = (BitMexServer)_server;
                            if (series.CandleCreateMethodType != CandleCreateMethodType.Simple || 
                                series.TimeFrameSpan.TotalMinutes < 1)
                            {
                                List<Trade> allTrades = _server.GetAllTradesToSecurity(series.Security);
                                series.PreLoad(allTrades);
                            }
                            else
                            {
                                List<Candle> candles = bitMex.GetBitMexCandleHistory(series.Security.Name,
                                    series.TimeFrameSpan);
                                if (candles != null)
                                {
                                    series.CandlesAll = candles;
                                }
                            }
                            series.UpdateAllCandles();
                            series.IsStarted = true;
                        }
                        else if (serverType == ServerType.Kraken)
                        {
                            KrakenServer kraken = (KrakenServer)_server;

                            if (series.CandleCreateMethodType != CandleCreateMethodType.Simple || 
                                series.TimeFrameSpan.TotalMinutes < 1)
                            {
                                List<Trade> allTrades = _server.GetAllTradesToSecurity(series.Security);
                                series.PreLoad(allTrades);
                            }
                            else
                            {
                                List<Candle> candles = kraken.GetHistory(series.Security.Name,
                                    Convert.ToInt32(series.TimeFrameSpan.TotalMinutes));
                                if (candles != null)
                                {
                                    series.CandlesAll = candles;
                                }
                            }
                            series.UpdateAllCandles();
                            series.IsStarted = true;
                        }
                        else if (serverType == ServerType.Binance)
                        {
                            BinanceServer binance = (BinanceServer)_server;
                            if (series.CandleCreateMethodType != CandleCreateMethodType.Simple || 
                                series.TimeFrameSpan.TotalMinutes < 1)
                            {
                                List<Trade> allTrades = _server.GetAllTradesToSecurity(series.Security);
                                series.PreLoad(allTrades);
                            }
                            else
                            {
                                List<Candle> candles = binance.GetCandleHistory(series.Security.Name,
                                    series.TimeFrameSpan);
                                if (candles != null)
                                {
                                    series.CandlesAll = candles;
                                }
                            }
                            series.UpdateAllCandles();
                            series.IsStarted = true;
                        }
                        else if (serverType == ServerType.Bitfinex)
                        {
                            BitfinexServer bitfinex = (BitfinexServer)_server;
                            if (series.CandleCreateMethodType != CandleCreateMethodType.Simple || 
                                series.TimeFrameSpan.TotalMinutes < 1)
                            {
                                List<Trade> allTrades = _server.GetAllTradesToSecurity(series.Security);
                                series.PreLoad(allTrades);
                            }
                            else
                            {
                                List<Candle> candles = bitfinex.GetCandleHistory(series.Security.Name,
                                    series.TimeFrameSpan);
                                if (candles != null)
                                {
                                    series.CandlesAll = candles;
                                }
                            }
                            series.UpdateAllCandles();
                            series.IsStarted = true;
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
        /// прекратить загрузку свечек по серии
        /// </summary>
        /// <param name="series">серия свечек которую нужно остановить</param>
        public void StopSeries(CandleSeries series)
        {
            try
            {
                if (_activSeries == null)
                {
                    return;
                }

                _activSeries.Remove(series);
            }
            catch (Exception error)
            {
                SendLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        /// <summary>
        /// сервер подключения к бирже
        /// </summary>
        private IServer _server;

// Для ТЕСТЕРА

        /// <summary>
        /// для тестера и Interactiv Brokers. Подгрузка новой свечи в серии
        /// </summary>
        public void SetNewCandleInSeries(Candle candle, string nameSecurity, TimeSpan timeFrame)
        {
            if (_activSeries == null || _activSeries.Count == 0)
            {
                return;
            }

            for (int i = 0; i < _activSeries.Count; i++)
            {
                if (_activSeries[i].Security.Name == nameSecurity && _activSeries[i].TimeFrameSpan == timeFrame)
                {
                    _activSeries[i].SetNewCandleInArray(candle);
                }
            }
        }

        /// <summary>
        /// для тестера. Очистить серии от старых данных
        /// </summary>
        public void Clear()
        {
            if (_activSeries == null)
            {
                return;
            }

            for (int i = 0; i < _activSeries.Count; i++)
            {
                _activSeries[i].Clear();
            }
        }

        /// <summary>
        /// для тестера. Синхронизировать получаемые данные
        /// </summary>
        public void SynhSeries(List<string> nameSecurities)
        {
            if (nameSecurities == null || nameSecurities.Count == 0 ||
               _activSeries == null || _activSeries.Count == 0)
            {
                return;
            }

            List<CandleSeries> mySeries = new List<CandleSeries>();

            for (int i = 0; i < _activSeries.Count; i++)
            {
                if (nameSecurities.Find(nameSec => nameSec == _activSeries[i].Security.Name) != null)
                {
                    mySeries.Add(_activSeries[i]);
                }
            }
            _activSeries = mySeries;
        }

        private TesterDataType _typeTesterData;
        /// <summary>
        /// тип данных которые заказал тестер
        /// </summary>
        public TesterDataType TypeTesterData
        {
            get { return _typeTesterData; }
            set
            {
                _typeTesterData = value;
                for (int i = 0;_activSeries != null && i < _activSeries.Count; i++)
                {
                    _activSeries[i].TypeTesterData = value;
                }
            }
            
        }


        /// <summary>
        /// активные серии
        /// </summary>
        private List<CandleSeries> _activSeries;

        /// <summary>
        /// в одной из серий обновились свечки. Входящее событие
        /// </summary>
        /// <param name="series">серия по которой прошло обновление</param>
        void series_СandleUpdeteEvent(CandleSeries series)
        {
            if (CandleUpdateEvent != null)
            {
                CandleUpdateEvent( series);
            }
        }

        void series_СandleFinishedEvent(CandleSeries series)
        {
            if (CandleUpdateEvent != null)
            {
                CandleUpdateEvent(series);
            }
        }

        /// <summary>
        /// обновилась свечка
        /// </summary>
        public event Action<CandleSeries> CandleUpdateEvent;

// Отправка сообщений на верх

        private void SendLogMessage(string message,LogMessageType type)
        {
            if (LogMessageEvent != null)
            {
                LogMessageEvent(message, type);
            }
            else if (type == LogMessageType.Error)
            {
                MessageBox.Show(message);
            }
        }

        public event Action<string, LogMessageType> LogMessageEvent;
    }
}
