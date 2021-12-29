/*
 *Your rights to use the code are governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 *Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Media;
using System.Threading;
using System.Threading.Tasks;
using OsEngine.Entity;
using OsEngine.Language;
using OsEngine.Logging;
using OsEngine.Market.Servers.Entity;

namespace OsEngine.Market.Servers
{
    public abstract class AServer : IServer
    {
        /// <summary>
        /// implementation of connection to the API
        /// реализация подключения к API
        /// </summary>
        public IServerRealization ServerRealization
        {
            set
            {
                _serverConnectStatus = ServerConnectStatus.Disconnect;
                _serverRealization = value;
                _serverRealization.NewTradesEvent += ServerRealization_NewTradesEvent;
                _serverRealization.ConnectEvent += _serverRealization_Connected;
                _serverRealization.DisconnectEvent += _serverRealization_Disconnected;
                _serverRealization.MarketDepthEvent += _serverRealization_MarketDepthEvent;
                _serverRealization.MyOrderEvent += _serverRealization_MyOrderEvent;
                _serverRealization.MyTradeEvent += _serverRealization_MyTradeEvent;
                _serverRealization.PortfolioEvent += _serverRealization_PortfolioEvent;
                _serverRealization.SecurityEvent += _serverRealization_SecurityEvent;
                _serverRealization.LogMessageEvent += SendLogMessage;

                CreateParameterBoolean(OsLocalization.Market.ServerParam1, false);
                _neadToSaveTicksParam = (ServerParameterBool)ServerParameters[ServerParameters.Count - 1];
                _neadToSaveTicksParam.ValueChange += SaveTradesHistoryParam_ValueChange;

                CreateParameterInt(OsLocalization.Market.ServerParam2, 5);
                _neadToSaveTicksDaysCountParam = (ServerParameterInt)ServerParameters[ServerParameters.Count - 1];
                _neadToSaveTicksDaysCountParam.ValueChange += _neadToSaveTicksDaysCountParam_ValueChange;

                CreateParameterBoolean(OsLocalization.Market.ServerParam5, true);
                _neadToSaveCandlesParam = (ServerParameterBool)ServerParameters[ServerParameters.Count - 1];
                _neadToSaveCandlesParam.ValueChange += SaveCandleHistoryParam_ValueChange;

                CreateParameterInt(OsLocalization.Market.ServerParam6, 300);
                _neadToSaveCandlesCountParam = (ServerParameterInt)ServerParameters[ServerParameters.Count - 1];

                CreateParameterBoolean(OsLocalization.Market.ServerParam7, false);
                _needToLoadBidAskInTrades = (ServerParameterBool)ServerParameters[ServerParameters.Count - 1];

                CreateParameterBoolean(OsLocalization.Market.ServerParam8, false);
                _needToRemoveTradesFromMemory = (ServerParameterBool)ServerParameters[ServerParameters.Count - 1];

                CreateParameterBoolean(OsLocalization.Market.ServerParam9, false);
                _needToRemoveCandlesFromMemory = (ServerParameterBool)ServerParameters[ServerParameters.Count - 1];

                _serverRealization.ServerParameters = ServerParameters;

                _tickStorage = new ServerTickStorage(this);
                _tickStorage.NeadToSave = _neadToSaveTicksParam.Value;
                _tickStorage.DaysToLoad = _neadToSaveTicksDaysCountParam.Value;
                _tickStorage.TickLoadedEvent += _tickStorage_TickLoadedEvent;
                _tickStorage.LogMessageEvent += SendLogMessage;
                _tickStorage.LoadTick();

                _candleStorage = new ServerCandleStorage(this);
                _candleStorage.NeadToSave = _neadToSaveCandlesParam.Value;
                _candleStorage.LogMessageEvent += SendLogMessage;

                Task task0 = new Task(ExecutorOrdersThreadArea);
                task0.Start();

                Log = new Log(_serverRealization.ServerType + "Server", StartProgram.IsOsTrader);
                Log.Listen(this);

                _serverStatusNead = ServerConnectStatus.Disconnect;

                Task task = new Task(PrimeThreadArea);
                task.Start();

                Task task2 = new Task(SenderThreadArea);
                task2.Start();

                if (PrimeSettings.PrimeSettingsMaster.ServerTestingIsActive)
                {
                    AServerTests tester = new AServerTests();
                    tester.Listen(this);
                    tester.LogMessageEvent += SendLogMessage;
                }

                Task task3 = new Task(MyTradesBeepThread);
                task3.Start();

                _serverIsStart = true;

            }
            get { return _serverRealization; }
        }

        private object trades_locker = new object();

        private double _waitTimeAfterFirstStart = 60;

        /// <summary>
        /// время ожиадания после старта сервера, по прошествии которого можно выставлять ордера
        /// </summary>
        protected double WaitTimeAfterFirstStart
        {
            set { _waitTimeAfterFirstStart = value; }
        }

        private IServerRealization _serverRealization;

        /// <summary>
        /// server type
        /// тип сервера
        /// </summary>
        public ServerType ServerType { get { return ServerRealization.ServerType; } }

        /// <summary>
        /// show settings window
        /// показать окно настроек
        /// </summary>
        public void ShowDialog()
        {
            if (_ui == null)
            {
                _ui = new AServerParameterUi(this);
                _ui.Show();
                _ui.Closing += (sender, args) => { _ui = null; };
            }
            else
            {
                _ui.Activate();
            }
        }

        /// <summary>
        /// settings window
        /// окно настроек
        /// </summary>
        private AServerParameterUi _ui;

        void _neadToSaveTicksDaysCountParam_ValueChange()
        {
            if (_tickStorage != null)
            {
                _tickStorage.DaysToLoad = _neadToSaveTicksDaysCountParam.Value;
            }
        }

        void SaveTradesHistoryParam_ValueChange()
        {
            if (_tickStorage != null)
            {
                _tickStorage.NeadToSave = _neadToSaveTicksParam.Value;
            }
        }

        void SaveCandleHistoryParam_ValueChange()
        {
            if (_candleStorage != null)
            {
                _candleStorage.NeadToSave = _neadToSaveCandlesParam.Value;
            }
        }


        // parameters / параметры

        /// <summary>
        /// shows whether the server starts working
        /// запустился ли сервер
        /// </summary>
        private bool _serverIsStart;

        /// <summary>
        /// parameter that shows whether need to save ticks for server
        /// параметр с флагом о том, нужно ли сохранять тики для сервера
        /// </summary>
        private ServerParameterBool _neadToSaveTicksParam;

        /// <summary>
        /// parameter with the number of days for saving ticks
        /// параметр с количеством дней которые нужно сохранять тики
        /// </summary>
        private ServerParameterInt _neadToSaveTicksDaysCountParam;

        private ServerParameterBool _neadToSaveCandlesParam;

        public ServerParameterInt _neadToSaveCandlesCountParam;

        private ServerParameterBool _needToLoadBidAskInTrades;

        private ServerParameterBool _needToRemoveTradesFromMemory;

        public ServerParameterBool _needToRemoveCandlesFromMemory;

        public bool NeedToHideParams = false;

        /// <summary>
        /// server parameters
        /// параметры сервера
        /// </summary>
        public List<IServerParameter> ServerParameters = new List<IServerParameter>();

        /// <summary>
        /// create STRING server parameter
        /// создать строковый параметр сервера
        /// </summary>
        public void CreateParameterString(string name, string param)
        {
            ServerParameterString newParam = new ServerParameterString();
            newParam.Name = name;
            newParam.Value = param;

            newParam = (ServerParameterString)LoadParam(newParam);
            if (_serverIsStart)
            {
                ServerParameters.Insert(ServerParameters.Count - 7, newParam);
            }
            else
            {
                ServerParameters.Add(newParam);
            }

            newParam.ValueChange += newParam_ValueChange;
        }

        /// <summary>
        /// create INT server parameter
        /// создать интовый параметр сервера
        /// </summary>
        public void CreateParameterInt(string name, int param)
        {
            ServerParameterInt newParam = new ServerParameterInt();
            newParam.Name = name;
            newParam.Value = param;

            newParam = (ServerParameterInt)LoadParam(newParam);
            if (_serverIsStart)
            {
                ServerParameters.Insert(ServerParameters.Count - 7, newParam);
            }
            else
            {
                ServerParameters.Add(newParam);
            }

            newParam.ValueChange += newParam_ValueChange;
        }

        public void CreateParameterEnum(string name, string value, List<string> collection)
        {
            ServerParameterEnum newParam = new ServerParameterEnum();
            newParam.Name = name;
            newParam.Value = value;
            newParam = (ServerParameterEnum)LoadParam(newParam);
            newParam.EnumValues = collection;

            if (_serverIsStart)
            {
                ServerParameters.Insert(ServerParameters.Count - 7, newParam);
            }
            else
            {
                ServerParameters.Add(newParam);
            }

            newParam.ValueChange += newParam_ValueChange;
        }

        /// <summary>
        /// create DECIMAL server parameter
        /// создать десимал параметр сервера
        /// </summary>
        public void CreateParameterDecimal(string name, decimal param)
        {
            ServerParameterDecimal newParam = new ServerParameterDecimal();
            newParam.Name = name;
            newParam.Value = param;

            newParam = (ServerParameterDecimal)LoadParam(newParam);
            if (_serverIsStart)
            {
                ServerParameters.Insert(ServerParameters.Count - 7, newParam);
            }
            else
            {
                ServerParameters.Add(newParam);
            }

            newParam.ValueChange += newParam_ValueChange;
        }

        /// <summary>
        /// create BOOL server parameter
        /// создать булевый параметр сервера
        /// </summary>
        public void CreateParameterBoolean(string name, bool param)
        {
            ServerParameterBool newParam = new ServerParameterBool();
            newParam.Name = name;
            newParam.Value = param;

            newParam = (ServerParameterBool)LoadParam(newParam);
            if (_serverIsStart)
            {
                ServerParameters.Insert(ServerParameters.Count - 7, newParam);
            }
            else
            {
                ServerParameters.Add(newParam);
            }


            newParam.ValueChange += newParam_ValueChange;
        }

        /// <summary>
        /// create PASSWORD server parameter
        /// создать парольный параметр сервера
        /// </summary>
        public void CreateParameterPassword(string name, string param)
        {
            ServerParameterPassword newParam = new ServerParameterPassword();
            newParam.Name = name;
            newParam.Value = param;

            newParam = (ServerParameterPassword)LoadParam(newParam);
            if (_serverIsStart)
            {
                ServerParameters.Insert(ServerParameters.Count - 7, newParam);
            }
            else
            {
                ServerParameters.Add(newParam);
            }

            newParam.ValueChange += newParam_ValueChange;
        }

        /// <summary>
        /// create PATH TO FILE server parameter
        /// создать параметр сервера для пути к папке
        /// </summary>
        public void CreateParameterPath(string name)
        {
            ServerParameterPath newParam = new ServerParameterPath();
            newParam.Name = name;

            newParam = (ServerParameterPath)LoadParam(newParam);
            if (_serverIsStart)
            {
                ServerParameters.Insert(ServerParameters.Count - 7, newParam);
            }
            else
            {
                ServerParameters.Add(newParam);
            }

            newParam.ValueChange += newParam_ValueChange;
        }

        /// <summary>
        /// create Button server parameter
        /// создать параметр сервера типа кнопка
        /// </summary>
        public void CreateParameterButton(string name)
        {
            ServerParameterButton newParam = new ServerParameterButton();
            newParam.Name = name;

            newParam = (ServerParameterButton)LoadParam(newParam);
            if (_serverIsStart)
            {
                ServerParameters.Insert(ServerParameters.Count - 7, newParam);
            }
            else
            {
                ServerParameters.Add(newParam);
            }

            newParam.ValueChange += newParam_ValueChange;
        }

        /// <summary>
        /// changed parameter state
        /// изменилось состояние параметра
        /// </summary>
        void newParam_ValueChange()
        {
            SaveParam();
        }

        /// <summary>
        /// save parameters
        /// сохранить параметры
        /// </summary>
        private void SaveParam()
        {
            try
            {
                using (StreamWriter writer = new StreamWriter(@"Engine\" + ServerType + @"Params.txt", false)
                    )
                {
                    for (int i = 0; i < ServerParameters.Count; i++)
                    {
                        writer.WriteLine(ServerParameters[i].GetStringToSave());
                    }

                    writer.Close();
                }
            }
            catch (Exception)
            {
                // send to the log / отправить в лог
            }
        }

        /// <summary>
        /// upload parameter
        /// загрузить параметр
        /// </summary>
        private IServerParameter LoadParam(IServerParameter param)
        {
            try
            {
                if (ServerType == ServerType.Binance)
                {

                }
            }
            catch (Exception)
            {
                throw new Exception("You try create Parameter befor create realization. Set CreateParam method after create ServerRealization");
            }


            if (!File.Exists(@"Engine\" + ServerType + @"Params.txt"))
            {
                return param;
            }
            try
            {
                using (StreamReader reader = new StreamReader(@"Engine\" + ServerType + @"Params.txt"))
                {
                    while (reader.EndOfStream == false)
                    {
                        string save = reader.ReadLine();

                        string[] saveAr = save.Split('^');

                        ServerParameterType type;
                        Enum.TryParse(saveAr[0], out type);

                        IServerParameter oldParam = null;

                        if (type == ServerParameterType.Enum)
                        {
                            oldParam = new ServerParameterEnum();
                        }
                        if (type == ServerParameterType.String)
                        {
                            oldParam = new ServerParameterString();
                        }
                        if (type == ServerParameterType.Decimal)
                        {
                            oldParam = new ServerParameterDecimal();
                        }
                        if (type == ServerParameterType.Int)
                        {
                            oldParam = new ServerParameterInt();
                        }
                        if (type == ServerParameterType.Bool)
                        {
                            oldParam = new ServerParameterBool();
                        }
                        if (type == ServerParameterType.Password)
                        {
                            oldParam = new ServerParameterPassword();
                        }
                        if (type == ServerParameterType.Path)
                        {
                            oldParam = new ServerParameterPath();
                        }

                        if (oldParam == null)
                        {
                            continue;
                        }

                        oldParam.LoadFromStr(save);

                        if (oldParam.Name == param.Name &&
                            oldParam.Type == param.Type)
                        {
                            return oldParam;
                        }
                    }

                    return param;
                }
            }
            catch (Exception error)
            {
                SendLogMessage(error.ToString(), LogMessageType.Error);
            }
            return param;
        }

        // connect/disconnect / подключение/отключение

        /// <summary>
        /// necessary server status. It needs to thread that listens to connectin
        /// Depending on this field manage the connection 
        /// нужный статус сервера. Нужен потоку который следит за соединением
        /// В зависимости от этого поля управляет соединением
        /// </summary>
        private ServerConnectStatus _serverStatusNead;

        /// <summary>
        /// run the server. Connect to trade system
        /// запустить сервер. Подключиться к торговой системе
        /// </summary>
        public void StartServer()
        {
            if (UserWhantConnect != null)
            {
                UserWhantConnect();
            }

            if (_serverStatusNead == ServerConnectStatus.Connect)
            {
                return;
            }

            LastStartServerTime = DateTime.Now.AddMinutes(-5);

            _serverStatusNead = ServerConnectStatus.Connect;
        }

        /// <summary>
        /// stop the server
        /// остановить сервер
        /// </summary>
        public void StopServer()
        {
            if (UserWhantDisconnect != null)
            {
                UserWhantDisconnect();
            }
            _serverStatusNead = ServerConnectStatus.Disconnect;
        }

        /// <summary>
        /// alert message from client that connection is established
        /// пришло оповещение от клиента, что соединение установлено
        /// </summary>
        void _serverRealization_Connected()
        {
            SendLogMessage(OsLocalization.Market.Message6, LogMessageType.System);
            ServerStatus = ServerConnectStatus.Connect;
        }

        // connection status /  статус соединения

        /// <summary>
        /// server status
        /// статус сервера
        /// </summary>
        public ServerConnectStatus ServerStatus
        {
            get { return _serverConnectStatus; }
            private set
            {
                if (value != _serverConnectStatus)
                {
                    _serverConnectStatus = value;
                    SendLogMessage(_serverConnectStatus + OsLocalization.Market.Message7, LogMessageType.Connect);
                    if (ConnectStatusChangeEvent != null)
                    {
                        ConnectStatusChangeEvent(_serverConnectStatus.ToString());
                    }
                }
            }
        }
        private ServerConnectStatus _serverConnectStatus;

        /// <summary>
        /// connection state has changed
        /// изменилось состояние соединения
        /// </summary>
        public event Action<string> ConnectStatusChangeEvent;

        // work of main thread / работа основного потока !!!!!!

        /// <summary>
        /// true - server is ready to work 
        /// true - сервер готов к работе
        /// </summary>
        public virtual bool IsTimeToServerWork
        {
            get { return true; }
        }

        /// <summary>
        /// the place where connection is controlled. look at data streams
        /// место в котором контролируется соединение. опрашиваются потоки данных
        /// </summary>
        [System.Runtime.ExceptionServices.HandleProcessCorruptedStateExceptionsAttribute]
        private async void PrimeThreadArea()
        {
            while (true)
            {
                //await Task.Delay(1000);
                Thread.Sleep(1000);
                try
                {
                    if (ServerRealization == null)
                    {
                        continue;
                    }

                    if (!IsTimeToServerWork)
                    {
                        continue;
                    }

                    if ((ServerRealization.ServerStatus != ServerConnectStatus.Connect)
                        && _serverStatusNead == ServerConnectStatus.Connect &&
                       LastStartServerTime.AddSeconds(300) < DateTime.Now)
                    {
                        SendLogMessage(OsLocalization.Market.Message8, LogMessageType.System);
                        ServerRealization.Dispose();

                        if (Portfolios != null &&
                            Portfolios.Count != 0)
                        {
                            Portfolios.Clear();
                        }

                        if (_candleManager != null)
                        {
                            _candleManager.Dispose();
                            _candleManager = null;
                        }

                        ServerRealization.Connect();
                        LastStartServerTime = DateTime.Now;

                        NeadToReconnectEvent?.Invoke();

                        continue;
                    }

                    if (ServerRealization.ServerStatus == ServerConnectStatus.Connect && _serverStatusNead == ServerConnectStatus.Disconnect)
                    {
                        SendLogMessage(OsLocalization.Market.Message9, LogMessageType.System);
                        ServerRealization.Dispose();

                        if (_candleManager != null)
                        {
                            _candleManager.Dispose();
                            _candleManager = null;
                        }

                        continue;
                    }

                    if (ServerRealization.ServerStatus != ServerConnectStatus.Connect)
                    {
                        continue;
                    }

                    if (_candleManager == null)
                    {
                        SendLogMessage(OsLocalization.Market.Message10, LogMessageType.System);
                        StartCandleManager();
                        continue;
                    }

                    if (_portfolios == null || _portfolios.Count == 0)
                    {
                        ServerRealization.GetPortfolios();
                    }

                    if (_securities == null || Securities.Count == 0)
                    {
                        ServerRealization.GetSecurities();
                    }
                }
                catch (Exception error)
                {
                    SendLogMessage(OsLocalization.Market.Message11, LogMessageType.Error);
                    SendLogMessage(error.ToString(), LogMessageType.Error);
                    ServerStatus = ServerConnectStatus.Disconnect;
                    ServerRealization.Dispose();
                    _candleManager = null;

                    Thread.Sleep(5000);
                    // reconnect / переподключаемся

                    Task task = new Task(PrimeThreadArea);
                    task.Start();

                    if (NeadToReconnectEvent != null)
                    {
                        NeadToReconnectEvent();
                    }

                    return;
                }
            }
        }

        /// <summary>
        /// server time of last starting
        /// время последнего старта сервера
        /// </summary>
        public DateTime LastStartServerTime { get; set; }

        /// <summary>
        /// client connection has broken
        /// соединение с клиентом разорвано
        /// </summary>
        void _serverRealization_Disconnected()
        {
            SendLogMessage(OsLocalization.Market.Message12, LogMessageType.System);
            ServerStatus = ServerConnectStatus.Disconnect;

            if (NeadToReconnectEvent != null)
            {
                NeadToReconnectEvent();
            }
        }

        /// <summary>
        /// start candle downloading
        /// запускает скачиватель свечек
        /// </summary>
        private void StartCandleManager()
        {
            if (_candleManager == null)
            {
                _candleManager = new CandleManager(this);
                _candleManager.CandleUpdateEvent += _candleManager_CandleUpdateEvent;
                _candleManager.LogMessageEvent += SendLogMessage;
            }
        }

        #region Работа потока рассылки

        /// <summary>
        /// queue of new orders
        /// очередь новых ордеров
        /// </summary>
        private ConcurrentQueue<Order> _ordersToSend = new ConcurrentQueue<Order>();

        /// <summary>
        /// queue of ticks
        /// очередь тиков
        /// </summary>
        private ConcurrentQueue<List<Trade>> _tradesToSend = new ConcurrentQueue<List<Trade>>();

        /// <summary>
        /// queue of new portfolios
        /// очередь новых портфелей
        /// </summary>
        private ConcurrentQueue<List<Portfolio>> _portfolioToSend = new ConcurrentQueue<List<Portfolio>>();

        /// <summary>
        /// queue of new securities
        /// очередь новых инструментов
        /// </summary>
        private ConcurrentQueue<List<Security>> _securitiesToSend = new ConcurrentQueue<List<Security>>();

        /// <summary>
        /// queue of my new trades
        /// очередь новых моих сделок
        /// </summary>
        private ConcurrentQueue<MyTrade> _myTradesToSend = new ConcurrentQueue<MyTrade>();

        /// <summary>
        /// queue of new time
        /// очередь нового времени
        /// </summary>
        private ConcurrentQueue<DateTime> _newServerTime = new ConcurrentQueue<DateTime>();

        /// <summary>
        /// queue of updated candles series
        /// очередь обновлённых серий свечек
        /// </summary>
        private ConcurrentQueue<CandleSeries> _candleSeriesToSend = new ConcurrentQueue<CandleSeries>();

        /// <summary>
        /// queue of new depths 
        /// очередь новых стаканов
        /// </summary>
        private ConcurrentQueue<MarketDepth> _marketDepthsToSend = new ConcurrentQueue<MarketDepth>();

        /// <summary>
        /// queue of updated bid and ask by security
        /// очередь обновлений бида с аска по инструментам 
        /// </summary>
        private ConcurrentQueue<BidAskSender> _bidAskToSend = new ConcurrentQueue<BidAskSender>();

        /// <summary>
        /// place where the connection is controlled
        /// место в котором контролируется соединение
        /// </summary>
        private async void SenderThreadArea()
        {
            while (true)
            {
                try
                {
                    if (!_ordersToSend.IsEmpty)
                    {
                        Order order;
                        if (_ordersToSend.TryDequeue(out order))
                        {
                            if (NewOrderIncomeEvent != null)
                            {
                                NewOrderIncomeEvent(order);
                            }
                        }
                    }
                    else if (!_myTradesToSend.IsEmpty &&
                             (_ordersToSend.IsEmpty))
                    {
                        MyTrade myTrade;

                        if (_myTradesToSend.TryDequeue(out myTrade))
                        {
                            if (NewMyTradeEvent != null)
                            {
                                NewMyTradeEvent(myTrade);
                            }
                        }
                    }
                    else if (!_tradesToSend.IsEmpty)
                    {
                        lock (trades_locker)
                        {
                            List<Trade> trades;

                            if (_tradesToSend.TryDequeue(out trades))
                            {
                                if (NewTradeEvent != null)
                                {
                                    NewTradeEvent(trades);
                                }
                                if (_needToRemoveTradesFromMemory.Value == true && _allTrades != null)

                                {
                                    foreach (var el in _allTrades)
                                    {
                                        if (el.Count > 100)
                                        {
                                            for (int i = el.Count - 100; i > 0; i--)
                                            {
                                                if (el[i] == null)
                                                {
                                                    break;
                                                }
                                                el[i] = null;
                                            }
                                        }
                                    }
                                }

                            }
                        }
                    }

                    else if (!_portfolioToSend.IsEmpty)
                    {
                        List<Portfolio> portfolio;

                        if (_portfolioToSend.TryDequeue(out portfolio))
                        {
                            if (PortfoliosChangeEvent != null)
                            {
                                PortfoliosChangeEvent(portfolio);
                            }
                        }
                    }

                    else if (!_securitiesToSend.IsEmpty)
                    {
                        List<Security> security;

                        if (_securitiesToSend.TryDequeue(out security))
                        {
                            if (SecuritiesChangeEvent != null)
                            {
                                SecuritiesChangeEvent(security);
                            }
                        }
                    }
                    else if (!_newServerTime.IsEmpty)
                    {
                        DateTime time;

                        if (_newServerTime.TryDequeue(out time))
                        {
                            if (TimeServerChangeEvent != null)
                            {
                                TimeServerChangeEvent(_serverTime);
                            }
                        }
                    }

                    else if (!_candleSeriesToSend.IsEmpty)
                    {
                        CandleSeries series;

                        if (_candleSeriesToSend.TryDequeue(out series))
                        {
                            if (NewCandleIncomeEvent != null)
                            {
                                NewCandleIncomeEvent(series);
                            }
                        }
                    }

                    else if (!_marketDepthsToSend.IsEmpty)
                    {
                        MarketDepth depth;

                        if (_marketDepthsToSend.TryDequeue(out depth))
                        {
                            if (NewMarketDepthEvent != null)
                            {
                                NewMarketDepthEvent(depth);
                            }
                        }
                    }

                    else if (!_bidAskToSend.IsEmpty)
                    {
                        BidAskSender bidAsk;

                        if (_bidAskToSend.TryDequeue(out bidAsk))
                        {
                            if (NewBidAscIncomeEvent != null)
                            {
                                NewBidAscIncomeEvent(bidAsk.Bid, bidAsk.Ask, bidAsk.Security);
                            }
                        }
                    }
                    else
                    {
                        if (MainWindow.ProccesIsWorked == false)
                        {
                            return;
                        }
                        await Task.Delay(1);
                    }
                }
                catch (Exception error)
                {
                    SendLogMessage(error.ToString(), LogMessageType.Error);
                }
            }
        }

        #endregion

        // server time / время сервера

        /// <summary>
        /// server time
        /// время сервера
        /// </summary>
        public DateTime ServerTime
        {
            get { return _serverTime; }

            private set
            {
                if (value < _serverTime)
                {
                    return;
                }

                DateTime lastTime = _serverTime;
                _serverTime = value;

                if (_serverTime != lastTime)
                {
                    _newServerTime.Enqueue(_serverTime);
                }
                ServerRealization.ServerTime = _serverTime;
            }
        }
        private DateTime _serverTime;

        /// <summary>
        /// server time changed
        /// изменилось время сервера
        /// </summary>
        public event Action<DateTime> TimeServerChangeEvent;

        // portfolios / портфели

        /// <summary>
        /// all account in the system
        /// все счета в системе
        /// </summary>
        public List<Portfolio> Portfolios
        {
            get { return _portfolios; }
        }
        private List<Portfolio> _portfolios;

        /// <summary>
        /// take portfolio by number
        /// взять портфель по номеру
        /// </summary>
        public Portfolio GetPortfolioForName(string name)
        {
            try
            {
                if (_portfolios == null)
                {
                    return null;
                }
                return _portfolios.Find(portfolio => portfolio.Number == name);
            }
            catch (Exception error)
            {
                SendLogMessage(error.ToString(), LogMessageType.Error);
                return null;
            }
        }

        /// <summary>
        /// portfolio updated
        /// обновился портфель
        /// </summary>
        private void _serverRealization_PortfolioEvent(List<Portfolio> portf)
        {
            try
            {
                if (_portfolios == null)
                {
                    _portfolios = new List<Portfolio>();
                }

                for (int i = 0; i < portf.Count; i++)
                {
                    Portfolio curPortfolio = _portfolios.Find(p => p.Number == portf[i].Number);

                    if (curPortfolio == null)
                    {
                        bool isInArray = false;

                        for (int i2 = 0; i2 < _portfolios.Count; i2++)
                        {
                            if (_portfolios[i2].Number[0] > portf[i].Number[0])
                            {
                                _portfolios.Insert(i2, portf[i]);
                                curPortfolio = portf[i];
                                isInArray = true;
                                break;
                            }
                        }

                        if (isInArray == false)
                        {
                            _portfolios.Add(portf[i]);
                            curPortfolio = portf[i];
                        }
                    }

                    curPortfolio.Profit = portf[i].Profit;
                    curPortfolio.ValueBegin = portf[i].ValueBegin;
                    curPortfolio.ValueCurrent = portf[i].ValueCurrent;
                    curPortfolio.ValueBlocked = portf[i].ValueBlocked;

                    var positions = portf[i].GetPositionOnBoard();

                    if (positions != null)
                    {
                        foreach (var positionOnBoard in positions)
                        {
                            curPortfolio.SetNewPosition(positionOnBoard);
                        }
                    }
                }

                _portfolioToSend.Enqueue(_portfolios);
            }
            catch (Exception error)
            {
                SendLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        /// <summary>
        /// portfolios changed
        /// изменились портфели
        /// </summary>
        public event Action<List<Portfolio>> PortfoliosChangeEvent;

        // instruments / инструменты

        /// <summary>
        /// all instruments in the system
        /// все инструменты в системе
        /// </summary>
        public List<Security> Securities
        {
            get { return _securities; }
        }
        private List<Security> _securities;

        /// <summary>
        /// take the instrument as a Security by name of instrument
        /// взять инструмент в виде класса Security, по имени инструмента 
        /// </summary>
        public Security GetSecurityForName(string name)
        {
            if (_securities == null)
            {
                return null;
            }
            return _securities.Find(securiti => securiti.Name == name);
        }

        /// <summary>
        /// security list updated
        /// обновился список бумаг
        /// </summary>
        void _serverRealization_SecurityEvent(List<Security> securities)
        {
            if (securities == null)
            {
                return;
            }

            if (_securities == null 
                && securities.Count > 5000)
            {
                _securities = securities;
                _securitiesToSend.Enqueue(_securities);

                 return;
            }

            if (_securities == null)
            {
                _securities = new List<Security>();
            }

            for (int i = 0; i < securities.Count; i++)
            {
                if (securities[i] == null)
                {
                    continue;
                }
                if (securities[i].NameId == null)
                {
                    SendLogMessage(OsLocalization.Market.Message13, LogMessageType.Error);
                    return;
                }

                if (_securities.Find(s =>
                        s != null &&
                        s.NameId == securities[i].NameId &&
                        s.Name == securities[i].Name) == null)
                {
                    bool isInArray = false;

                    for (int i2 = 0; i2 < _securities.Count; i2++)
                    {
                        if (_securities[i2].Name[0] > securities[i].Name[0])
                        {
                            _securities.Insert(i2, securities[i]);
                            isInArray = true;
                            break;
                        }
                    }

                    if (isInArray == false)
                    {
                        _securities.Add(securities[i]);
                    }
                }
            }

            _securitiesToSend.Enqueue(_securities);
        }

        /// <summary>
        /// instruments changed
        /// изменились инструменты
        /// </summary>
        public event Action<List<Security>> SecuritiesChangeEvent;

        /// <summary>
        /// show security
        /// показать бумаги
        /// </summary>
        public void ShowSecuritiesDialog()
        {
            SecuritiesUi ui = new SecuritiesUi(this);
            ui.ShowDialog();
        }

        // subcribe to data / Подпись на данные

        /// <summary>
        /// master of dowloading candles
        /// мастер загрузки свечек
        /// </summary>
        private CandleManager _candleManager;

        /// <summary>
        /// multithreaded access locker in StartThisSecurity
        /// объект блокирующий многопоточный доступ в StartThisSecurity
        /// </summary>
        private object _lockerStarter = new object();

        /// <summary>
        /// start downloading of this instrument
        /// начать выкачивать данный иснтрументн
        /// </summary>
        /// <param name="namePaper"> security name / название инструмента </param>
        /// <param name="timeFrameBuilder"> object that has data about needed for series timeframe / объект несущий в себе данные о ТаймФрейме нужном для серии </param>
        /// <returns> if everything is going well, CandleSeries returns generated candle object / в случае успешного запуска возвращает CandleSeries, объект генерирующий свечи </returns>
        public CandleSeries StartThisSecurity(string namePaper, TimeFrameBuilder timeFrameBuilder)
        {
            try
            {
                lock (_lockerStarter)
                {
                    if (namePaper == "")
                    {
                        return null;
                    }

                    if (Portfolios == null || Securities == null)
                    {
                        return null;
                    }

                    if (LastStartServerTime != DateTime.MinValue &&
                        LastStartServerTime.AddSeconds(15) > DateTime.Now)
                    {
                        return null;
                    }

                    if (ServerStatus != ServerConnectStatus.Connect)
                    {
                        return null;
                    }

                    if (_candleManager == null)
                    {
                        return null;
                    }

                    Security security = null;

                    for (int i = 0; _securities != null && i < _securities.Count; i++)
                    {
                        if (_securities[i] == null)
                        {
                            continue;
                        }
                        if (_securities[i].Name == namePaper)
                        {
                            security = _securities[i];
                            break;
                        }
                    }

                    if (security == null)
                    {
                        return null;
                    }

                    CandleSeries series = new CandleSeries(timeFrameBuilder, security, StartProgram.IsOsTrader);

                    ServerRealization.Subscrible(security);

                    _candleManager.StartSeries(series);

                    SendLogMessage(OsLocalization.Market.Message14 + series.Security.Name +
                                   OsLocalization.Market.Message15 + series.TimeFrame +
                                   OsLocalization.Market.Message16, LogMessageType.System);

                    if (_tickStorage != null)
                    {
                        _tickStorage.SetSecurityToSave(security);
                    }

                    _candleStorage.SetSeriesToSave(series);

                    return series;
                }
            }
            catch (Exception error)
            {
                SendLogMessage(error.ToString(), LogMessageType.Error);
                return null;
            }
        }

        /// <summary>
        /// stop the downloading of candles
        /// остановить скачивание свечек
        /// </summary>
        /// <param name="series"> candles series that need to stop / серия свечек которую надо остановить </param>
        public void StopThisSecurity(CandleSeries series)
        {
            if (series != null)
            {
                _candleManager.StopSeries(series);
            }
        }

        /// <summary>
        /// candles series changed
        /// изменились серии свечек
        /// </summary>
        private void _candleManager_CandleUpdateEvent(CandleSeries series)
        {
            if (series.IsMergedByCandlesFromFile == false)
            {
                series.IsMergedByCandlesFromFile = true;

                List<Candle> candles = _candleStorage.GetCandles(series.Specification, _neadToSaveCandlesCountParam.Value);
                series.CandlesAll.Merge(candles);
            }

            if (_needToRemoveCandlesFromMemory.Value == true
                && series.CandlesAll.Count > _neadToSaveCandlesCountParam.Value
                && _serverTime.Minute % 15 == 0
                && _serverTime.Second == 0
            )
            {
                series.CandlesAll.RemoveRange(0, series.CandlesAll.Count - 1 - _neadToSaveCandlesCountParam.Value);
            }

            _candleSeriesToSend.Enqueue(series);
        }

        /// <summary>
        /// need to reconnect server and get a new data
        /// необходимо перезаказать данные у сервера
        /// </summary>
        public event Action NeadToReconnectEvent;

        /// <summary>
        /// take the candle history for a period
        /// взять историю свечей за период
        /// </summary>
        public CandleSeries GetCandleDataToSecurity(string namePaper, TimeFrameBuilder timeFrameBuilder, DateTime startTime,
            DateTime endTime, DateTime actualTime, bool neadToUpdate)
        {
            if (Portfolios == null || Securities == null)
            {
                return null;
            }

            if (LastStartServerTime != DateTime.MinValue &&
                LastStartServerTime.AddSeconds(15) > DateTime.Now)
            {
                return null;
            }

            if (ServerStatus != ServerConnectStatus.Connect)
            {
                return null;
            }

            if (_candleManager == null)
            {
                return null;
            }

            Security security = null;

            for (int i = 0; _securities != null && i < _securities.Count; i++)
            {
                if (_securities[i].Name == namePaper ||
                    _securities[i].NameId == namePaper)
                {
                    security = _securities[i];
                    break;
                }
            }

            if (security == null)
            {
                return null;
            }

            CandleSeries series = new CandleSeries(timeFrameBuilder, security, StartProgram.IsOsTrader);

            ServerRealization.Subscrible(security);

            if (timeFrameBuilder.CandleCreateMethodType == CandleCreateMethodType.Simple)
            {
                series.CandlesAll =
                    ServerRealization.GetCandleDataToSecurity(security, timeFrameBuilder, startTime, endTime,
                        actualTime);
            }

            if (series.CandlesAll == null)
            {
                List<Trade> trades = ServerRealization.GetTickDataToSecurity(security, startTime, endTime, actualTime);
                if (trades != null &&
                    trades.Count != 0)
                {
                    series.PreLoad(trades);
                }
            }

            if (series.CandlesAll != null &&
                series.CandlesAll.Count != 0)
            {
                series.IsStarted = true;
            }

            _candleManager.StartSeries(series);

            return series;
        }

        /// <summary>
        /// take ticks data for a period
        /// взять тиковые данные за период
        /// </summary>
        public bool GetTickDataToSecurity(string namePaper, DateTime startTime, DateTime endTime, DateTime actualTime, bool neadToUpdete)
        {
            if (Portfolios == null || Securities == null)
            {
                return false;
            }

            if (LastStartServerTime != DateTime.MinValue &&
                LastStartServerTime.AddSeconds(15) > DateTime.Now)
            {
                return false;
            }

            if (actualTime == DateTime.MinValue)
            {
                actualTime = startTime;
            }

            if (ServerStatus != ServerConnectStatus.Connect)
            {
                return false;
            }

            if (_candleManager == null)
            {
                return false;
            }

            Security security = null;

            for (int i = 0; _securities != null && i < _securities.Count; i++)
            {
                if (_securities[i].Name == namePaper)
                {
                    security = _securities[i];
                    break;
                }
            }

            if (security == null)
            {
                for (int i = 0; _securities != null && i < _securities.Count; i++)
                {
                    if (_securities[i].NameId == namePaper)
                    {
                        security = _securities[i];
                        break;
                    }
                }
                if (security == null)
                {
                    return false;
                }
            }

            List<Trade> trades = ServerRealization.GetTickDataToSecurity(security, startTime, endTime, actualTime);

            if (trades == null ||
                trades.Count == 0)
            {
                return false;
            }

            if (_allTrades == null)
            {
                _allTrades = new List<Trade>[1];
                _allTrades[0] = trades;
                return true;
            }

            for (int i = 0; i < _allTrades.Length; i++)
            {
                if (_allTrades[i] != null && _allTrades[i].Count != 0 &&
                    _allTrades[i][0].SecurityNameCode == security.Name)
                {
                    _allTrades[i] = trades;
                    return true;
                }
            }

            // there is no instruments storage / хранилища для инструмента нет
            List<Trade>[] allTradesNew = new List<Trade>[_allTrades.Length + 1];

            for (int i = 0; i < _allTrades.Length; i++)
            {
                allTradesNew[i] = _allTrades[i];
            }
            allTradesNew[allTradesNew.Length - 1] = trades;

            _allTrades = allTradesNew;


            return true;
        }

        /// <summary>
        /// new cadles
        /// новые свечи
        /// </summary>
        public event Action<CandleSeries> NewCandleIncomeEvent;

        // market depth / стакан

        /// <summary>
        /// all depths
        /// все стаканы
        /// </summary>
        private List<MarketDepth> _depths = new List<MarketDepth>();

        private decimal _currentBestBid;
        private decimal _currentBestAsk;
        
        /// <summary>
        /// came a new depth
        /// пришел обновленный стакан
        /// </summary>
        /// <param name="myDepth"></param>
        void _serverRealization_MarketDepthEvent(MarketDepth myDepth)
        {
            try
            {
                if (myDepth.Time == DateTime.MinValue)
                {
                    myDepth.Time = ServerTime;
                }
                else
                {
                    ServerTime = myDepth.Time;
                }

                if (NewMarketDepthEvent != null)
                {
                    _marketDepthsToSend.Enqueue(myDepth);

                    if (myDepth.Asks.Count != 0 && myDepth.Bids.Count != 0)
                    {
                        decimal besBid = myDepth.Bids[0].Price;
                        decimal bestAsk = myDepth.Asks[0].Price;

                        if (_currentBestBid != besBid || _currentBestAsk != bestAsk)
                        {
                            Security sec = GetSecurityForName(myDepth.SecurityNameCode);
                            if (sec != null)
                            {
                                _currentBestBid = besBid;
                                _currentBestAsk = bestAsk;
                            
                                _bidAskToSend.Enqueue(new BidAskSender
                                {
                                    Bid = besBid,
                                    Ask = bestAsk,
                                    Security = sec
                                });
                            }
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
        /// best bid or ask changed for the instrument
        /// изменился лучший бид / аск по инструменту
        /// </summary>
        public event Action<decimal, decimal, Security> NewBidAscIncomeEvent;

        /// <summary>
        /// new depth in the system
        /// новый стакан в системе
        /// </summary>
        public event Action<MarketDepth> NewMarketDepthEvent;

        // ticks / тики

        /// <summary>
        /// ticks storage
        /// хранилище тиков
        /// </summary>
        private ServerTickStorage _tickStorage;

        /// <summary>
        /// хранилище свечек
        /// </summary>
        private ServerCandleStorage _candleStorage;

        /// <summary>
        /// ticks storage
        /// хранилище тиков
        /// </summary>
        /// <param name="trades"></param>
        void _tickStorage_TickLoadedEvent(List<Trade>[] trades)
        {
            _allTrades = trades;
        }

        /// <summary>
        /// all ticks
        /// все тики
        /// </summary>
        private List<Trade>[] _allTrades;

        /// <summary>
        /// take ticks history by instrument
        /// взять историю тиков по инструменту
        /// </summary>
        /// <param name="security"> instrument / инстурмент </param>
        /// <returns> trades / сделки </returns>
        public List<Trade> GetAllTradesToSecurity(Security security)
        {
            try
            {
                if (_allTrades == null)
                {
                    return null;
                }
                List<Trade> trades = new List<Trade>();

                for (int i = 0; i < _allTrades.Length; i++)
                {
                    if (_allTrades[i] != null && _allTrades[i].Count != 0 &&
                        _allTrades[i][0] != null &&
                        _allTrades[i][0].SecurityNameCode == security.Name)
                    {
                        return _allTrades[i];
                    }
                }

                return trades;
            }
            catch (Exception error)
            {
                SendLogMessage(error.ToString(), LogMessageType.Error);
                return null;
            }
        }

        /// <summary>
        /// all server ticks
        /// все тики имеющиеся у сервера
        /// </summary>
        public List<Trade>[] AllTrades { get { return _allTrades; } }

        /// <summary>
        /// came new ticks
        /// пришли новые тики
        /// </summary>
        void ServerRealization_NewTradesEvent(Trade trade)
        {
            try
            {
                if (trade.Price <= 0)
                {
                    return;
                }

                ServerTime = trade.Time;

                if (_needToLoadBidAskInTrades.Value)
                {
                    BathTradeMarketDepthData(trade);
                }

                if (trade == null)
                {
                    return;
                }

                // save / сохраняем
                if (_allTrades == null)
                {
                    _allTrades = new List<Trade>[1];
                    _allTrades[0] = new List<Trade> { trade };
                }
                else
                {
                    // sort trades by storages / сортируем сделки по хранилищам
                    List<Trade> myList = null;
                    bool isSave = false;
                    for (int i = 0; i < _allTrades.Length; i++)
                    {
                        List<Trade> curList = _allTrades[i];

                        if (curList == null || curList.Count == 0)
                        {
                            continue;
                        }

                        if (curList[0].SecurityNameCode != trade.SecurityNameCode)
                        {
                            continue;
                        }

                        if (trade.Time < curList[curList.Count - 1].Time)
                        {
                            return;
                        }

                        curList.Add(trade);
                        myList = curList;
                        isSave = true;
                        break;

                    }

                    if (isSave == false)
                    {
                        // there is no storage for instrument / хранилища для инструмента нет
                        List<Trade>[] allTradesNew = new List<Trade>[_allTrades.Length + 1];
                        for (int i = 0; i < _allTrades.Length; i++)
                        {
                            allTradesNew[i] = _allTrades[i];
                        }

                        allTradesNew[allTradesNew.Length - 1] = new List<Trade>();
                        allTradesNew[allTradesNew.Length - 1].Add(trade);
                        myList = allTradesNew[allTradesNew.Length - 1];
                        _allTrades = allTradesNew;
                    }
                    /*
                    if (_needToRemoveTradesFromMemory.Value == true &&
                        myList.Count > 100)
                    {
                        myList[myList.Count - 100] = null;
                    }
                    */

                    _tradesToSend.Enqueue(myList);
                }
            }
            catch (Exception error)
            {
                SendLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        /// <summary>
        /// upload trades by market depth data
        /// прогрузить трейды данными стакана
        /// </summary>
        private void BathTradeMarketDepthData(Trade trade)
        {
            MarketDepth depth = _depths.Find(d => d.SecurityNameCode == trade.SecurityNameCode);

            if (depth == null ||
                depth.Asks == null || depth.Asks.Count == 0 ||
                depth.Bids == null || depth.Bids.Count == 0)
            {
                return;
            }

            trade.Ask = depth.Asks[0].Price;
            trade.Bid = depth.Bids[0].Price;
            trade.BidsVolume = depth.BidSummVolume;
            trade.AsksVolume = depth.AskSummVolume;
        }

        /// <summary>
        /// new tick
        /// новый тик
        /// </summary>
        public event Action<List<Trade>> NewTradeEvent;

        // my new trade / новая моя сделка

        private List<MyTrade> _myTrades = new List<MyTrade>();

        /// <summary>
        /// my trades
        /// мои сделки
        /// </summary>
        public List<MyTrade> MyTrades
        {
            get { return _myTrades; }
        }

        /// <summary>
        /// my incoming trades from system 
        /// входящие из системы мои сделки
        /// </summary>
        void _serverRealization_MyTradeEvent(MyTrade trade)
        {
            if (trade.Time == DateTime.MinValue)
            {
                trade.Time = ServerTime;
            }

            _myTradesToSend.Enqueue(trade);
            _myTrades.Add(trade);
            _neadToBeepOnTrade = true;
        }

        /// <summary>
        /// my trade changed
        /// изменилась моя сделка
        /// </summary>
        public event Action<MyTrade> NewMyTradeEvent;

        private bool _neadToBeepOnTrade;

        private async void MyTradesBeepThread()
        {
            while (true)
            {
                await Task.Delay(2000);
                if (MainWindow.ProccesIsWorked == false)
                {
                    return;
                }

                if (_neadToBeepOnTrade == false)
                {
                    continue;
                }

                if (PrimeSettings.PrimeSettingsMaster.TransactionBeepIsActiv == false)
                {
                    continue;
                }

                _neadToBeepOnTrade = false;
                SystemSounds.Asterisk.Play();
            }
        }

        // work with orders / работа с ордерами

        /// <summary>
        /// work place of thred on the queues of ordr execution and order cancellation 
        /// место работы потока на очередях исполнения заявок и их отмены
        /// </summary>
        private async void ExecutorOrdersThreadArea()
        {
            while (true)
            {
                if (LastStartServerTime.AddSeconds(_waitTimeAfterFirstStart) > DateTime.Now)
                {
                    await Task.Delay(1000);
                    continue;
                }
                try
                {
                    await Task.Delay(20);

                    if (_ordersToExecute != null && _ordersToExecute.Count != 0)
                    {
                        OrderAserverSender order;
                        if (_ordersToExecute.TryDequeue(out order))
                        {
                            if (order.OrderSendType == OrderSendType.Execute)
                            {
                                ServerRealization.SendOrder(order.Order);
                            }
                            else if (order.OrderSendType == OrderSendType.Cancel)
                            {
                                ServerRealization.CancelOrder(order.Order);
                            }
                        }
                    }
                }
                catch (Exception error)
                {
                    SendLogMessage(error.ToString(), LogMessageType.Error);
                }
            }
        }

        private ConcurrentQueue<OrderAserverSender> _ordersToExecute = new ConcurrentQueue<OrderAserverSender>();

        /// <summary>
        /// incoming order from system
        /// входящий из системы ордер
        /// </summary>
        void _serverRealization_MyOrderEvent(Order myOrder)
        {
            if (myOrder.TimeCallBack == DateTime.MinValue)
            {
                myOrder.TimeCallBack = ServerTime;
            }
            if (myOrder.TimeCreate == DateTime.MinValue)
            {
                myOrder.TimeCreate = ServerTime;
            }
            if (myOrder.State == OrderStateType.Done &&
                myOrder.TimeDone == DateTime.MinValue)
            {
                myOrder.TimeDone = myOrder.TimeCallBack;
            }
            if (myOrder.State == OrderStateType.Cancel &&
                myOrder.TimeDone == DateTime.MinValue)
            {
                myOrder.TimeCancel = myOrder.TimeCallBack;
            }

            myOrder.ServerType = ServerType;

            _ordersToSend.Enqueue(myOrder);

            for (int i = 0; i < _myTrades.Count; i++)
            {
                if (_myTrades[i].NumberOrderParent == myOrder.NumberMarket)
                {
                    _myTradesToSend.Enqueue(_myTrades[i]);
                }
            }
        }

        /// <summary>
        /// send order for execution to the trading system
        /// выслать ордер на исполнение в торговую систему
        /// </summary>
        /// <param name="order"> order / ордер </param>
        public void ExecuteOrder(Order order)
        {
            if (UserSetOrderOnExecute != null)
            {
                UserSetOrderOnExecute(order);
            }
            if (LastStartServerTime.AddSeconds(_waitTimeAfterFirstStart) > DateTime.Now)
            {
                order.State = OrderStateType.Fail;
                _ordersToSend.Enqueue(order);

                SendLogMessage(OsLocalization.Market.Message17 + order.NumberUser +
                               OsLocalization.Market.Message18, LogMessageType.Error);
                return;
            }

            order.TimeCreate = ServerTime;

            OrderAserverSender ord = new OrderAserverSender();
            ord.Order = order;
            ord.OrderSendType = OrderSendType.Execute;

            _ordersToExecute.Enqueue(ord);

            SendLogMessage(OsLocalization.Market.Message19 + order.Price +
                           OsLocalization.Market.Message20 + order.Side +
                           OsLocalization.Market.Message21 + order.Volume +
                           OsLocalization.Market.Message22 + order.SecurityNameCode +
                           OsLocalization.Market.Message23 + order.NumberUser, LogMessageType.System);
        }

        /// <summary>
        /// cancel order from the trading system
        /// отозвать ордер из торговой системы
        /// </summary>
        /// <param name="order"> order / ордер </param>
        public void CancelOrder(Order order)
        {
            if (UserSetOrderOnCancel != null)
            {
                UserSetOrderOnCancel(order);
            }

            OrderAserverSender ord = new OrderAserverSender();
            ord.Order = order;
            ord.OrderSendType = OrderSendType.Cancel;

            _ordersToExecute.Enqueue(ord);

            SendLogMessage(OsLocalization.Market.Message24 + order.NumberUser, LogMessageType.System);
        }

        /// <summary>
        /// order changed
        /// изменился ордер
        /// </summary>
        public event Action<Order> NewOrderIncomeEvent;

        // log messages / сообщения для лога

        /// <summary>
        /// add a new message in the log
        /// добавить в лог новое сообщение
        /// </summary>
        private void SendLogMessage(string message, LogMessageType type)
        {
            if (LogMessageEvent != null)
            {
                LogMessageEvent(message, type);
            }
        }

        /// <summary>
        /// log manager
        /// менеджер лога
        /// </summary>
        public Log Log;

        /// <summary>
        /// outgoing messages for the log
        /// исходящее сообщение для лога
        /// </summary>
        public event Action<string, LogMessageType> LogMessageEvent;

        // outgoing events for automatic testing
        // исходящие события для автоматического тестирования

        /// <summary>
        /// external systems requested order execution
        /// внешние системы запросили исполнение ордера
        /// </summary>
        public event Action<Order> UserSetOrderOnExecute;

        /// <summary>
        /// external systems requested order cancellation
        /// внешние системы запросили отзыв ордера
        /// </summary>
        public event Action<Order> UserSetOrderOnCancel;

        /// <summary>
        /// user requested connect to the API
        /// пользователь запросил подключение к АПИ
        /// </summary>
        public event Action UserWhantConnect;

        /// <summary>
        /// user requested disconnect from the API
        /// пользователь запросил отключение от АПИ
        /// </summary>
        public event Action UserWhantDisconnect;
    }

    public class OrderAserverSender
    {
        public Order Order;

        public OrderSendType OrderSendType;
    }

    public enum OrderSendType
    {
        Execute,
        Cancel
    }
}