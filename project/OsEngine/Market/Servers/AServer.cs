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
        #region Instead of a constructor

        /// <summary>
        /// implementation of connection to the API
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
                ServerParameters[0].Comment = OsLocalization.Market.Label87;

                CreateParameterInt(OsLocalization.Market.ServerParam2, 5);
                _neadToSaveTicksDaysCountParam = (ServerParameterInt)ServerParameters[ServerParameters.Count - 1];
                _neadToSaveTicksDaysCountParam.ValueChange += _neadToSaveTicksDaysCountParam_ValueChange;
                ServerParameters[1].Comment = OsLocalization.Market.Label88;

                CreateParameterBoolean(OsLocalization.Market.ServerParam5, true);
                _neadToSaveCandlesParam = (ServerParameterBool)ServerParameters[ServerParameters.Count - 1];
                _neadToSaveCandlesParam.ValueChange += SaveCandleHistoryParam_ValueChange;
                ServerParameters[2].Comment = OsLocalization.Market.Label89;

                CreateParameterInt(OsLocalization.Market.ServerParam6, 300);
                _neadToSaveCandlesCountParam = (ServerParameterInt)ServerParameters[ServerParameters.Count - 1];
                _neadToSaveCandlesCountParam.ValueChange += _neadToSaveCandlesCountParam_ValueChange;
                ServerParameters[3].Comment = OsLocalization.Market.Label90;

                CreateParameterBoolean(OsLocalization.Market.ServerParam7, false);
                _needToLoadBidAskInTrades = (ServerParameterBool)ServerParameters[ServerParameters.Count - 1];
                ServerParameters[4].Comment = OsLocalization.Market.Label91;

                CreateParameterBoolean(OsLocalization.Market.ServerParam8, true);
                _needToRemoveTradesFromMemory = (ServerParameterBool)ServerParameters[ServerParameters.Count - 1];
                ServerParameters[5].Comment = OsLocalization.Market.Label92;

                CreateParameterBoolean(OsLocalization.Market.ServerParam9, false);
                _needToRemoveCandlesFromMemory = (ServerParameterBool)ServerParameters[ServerParameters.Count - 1];
                ServerParameters[6].Comment = OsLocalization.Market.Label93;

                CreateParameterBoolean(OsLocalization.Market.ServerParam10, true);
                _needToUseFullMarketDepth = (ServerParameterBool)ServerParameters[ServerParameters.Count - 1];
                ServerParameters[7].Comment = OsLocalization.Market.Label94;

                CreateParameterBoolean(OsLocalization.Market.ServerParam11, true);
                _needToUpdateOnlyTradesWithNewPrice = (ServerParameterBool)ServerParameters[ServerParameters.Count - 1];
                ServerParameters[8].Comment = OsLocalization.Market.Label95;

                _serverRealization.ServerParameters = ServerParameters;

                _tickStorage = new ServerTickStorage(this);
                _tickStorage.NeadToSave = _neadToSaveTicksParam.Value;
                _tickStorage.DaysToLoad = _neadToSaveTicksDaysCountParam.Value;
                _tickStorage.TickLoadedEvent += _tickStorage_TickLoadedEvent;
                _tickStorage.LogMessageEvent += SendLogMessage;
                _tickStorage.LoadTick();

                _candleStorage = new ServerCandleStorage(this);
                _candleStorage.NeadToSave = _neadToSaveCandlesParam.Value;
                _candleStorage.CandlesSaveCount = _neadToSaveCandlesCountParam.Value;
                _candleStorage.LogMessageEvent += SendLogMessage;

                Task task0 = new Task(ExecutorOrdersThreadArea);
                task0.Start();

                Log = new Log(_serverRealization.ServerType + "Server", StartProgram.IsOsTrader);
                Log.Listen(this);

                _serverStatusNead = ServerConnectStatus.Disconnect;

                _loadDataLocker = "lockerData_" + ServerType.ToString();

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

                _serverIsCreated = true;

            }
            get { return _serverRealization; }
        }
        private IServerRealization _serverRealization;

        #endregion

        #region GUI

        /// <summary>
        /// show settings window
        /// </summary>
        public void ShowDialog()
        {
            if (_ui == null)
            {
                _ui = new AServerParameterUi(this);
                _ui.Show();
                _ui.Closing += _ui_Closing;
            }
            else
            {
                _ui.Activate();
            }
        }

        private void _ui_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            _ui.Closing -= _ui_Closing;
            _ui = null;
        }

        /// <summary>
        /// settings window
        /// </summary>
        private AServerParameterUi _ui;

        #endregion

        #region Parameters

        /// <summary>
        /// whether a server object has been created
        /// </summary>
        private bool _serverIsCreated;

        /// <summary>
        /// parameter that shows whether need to save ticks for server
        /// </summary>
        private ServerParameterBool _neadToSaveTicksParam;

        /// <summary>
        /// parameter with the number of days for saving ticks
        /// </summary>
        private ServerParameterInt _neadToSaveTicksDaysCountParam;

        private ServerParameterBool _neadToSaveCandlesParam;

        public ServerParameterInt _neadToSaveCandlesCountParam;

        private ServerParameterBool _needToLoadBidAskInTrades;

        private ServerParameterBool _needToRemoveTradesFromMemory;

        public ServerParameterBool _needToRemoveCandlesFromMemory;

        public ServerParameterBool _needToUseFullMarketDepth;

        public ServerParameterBool _needToUpdateOnlyTradesWithNewPrice;

        public bool NeedToHideParams = false;

        /// <summary>
        /// server parameters
        /// </summary>
        public List<IServerParameter> ServerParameters = new List<IServerParameter>();

        /// <summary>
        /// create STRING server parameter
        /// </summary>
        public void CreateParameterString(string name, string param)
        {
            ServerParameterString newParam = new ServerParameterString();
            newParam.Name = name;
            newParam.Value = param;

            newParam = (ServerParameterString)LoadParam(newParam);
            if (_serverIsCreated)
            {
                ServerParameters.Insert(ServerParameters.Count - 9, newParam);
            }
            else
            {
                ServerParameters.Add(newParam);
            }

            newParam.ValueChange += userChangeParameter_ValueChange;
        }

        /// <summary>
        /// create INT server parameter
        /// </summary>
        public void CreateParameterInt(string name, int param)
        {
            ServerParameterInt newParam = new ServerParameterInt();
            newParam.Name = name;
            newParam.Value = param;

            newParam = (ServerParameterInt)LoadParam(newParam);
            if (_serverIsCreated)
            {
                ServerParameters.Insert(ServerParameters.Count - 9, newParam);
            }
            else
            {
                ServerParameters.Add(newParam);
            }

            newParam.ValueChange += userChangeParameter_ValueChange;
        }

        /// <summary>
        /// create ENUM server parameter
        /// </summary>
        public void CreateParameterEnum(string name, string value, List<string> collection)
        {
            ServerParameterEnum newParam = new ServerParameterEnum();
            newParam.Name = name;
            newParam.Value = value;
            newParam = (ServerParameterEnum)LoadParam(newParam);
            newParam.EnumValues = collection;

            if (_serverIsCreated)
            {
                ServerParameters.Insert(ServerParameters.Count - 9, newParam);
            }
            else
            {
                ServerParameters.Add(newParam);
            }

            newParam.ValueChange += userChangeParameter_ValueChange;
        }

        /// <summary>
        /// create DECIMAL server parameter
        /// </summary>
        public void CreateParameterDecimal(string name, decimal param)
        {
            ServerParameterDecimal newParam = new ServerParameterDecimal();
            newParam.Name = name;
            newParam.Value = param;

            newParam = (ServerParameterDecimal)LoadParam(newParam);
            if (_serverIsCreated)
            {
                ServerParameters.Insert(ServerParameters.Count - 9, newParam);
            }
            else
            {
                ServerParameters.Add(newParam);
            }

            newParam.ValueChange += userChangeParameter_ValueChange;
        }

        /// <summary>
        /// create BOOL server parameter
        /// </summary>
        public void CreateParameterBoolean(string name, bool param)
        {
            ServerParameterBool newParam = new ServerParameterBool();
            newParam.Name = name;
            newParam.Value = param;

            newParam = (ServerParameterBool)LoadParam(newParam);
            if (_serverIsCreated)
            {
                ServerParameters.Insert(ServerParameters.Count - 9, newParam);
            }
            else
            {
                ServerParameters.Add(newParam);
            }


            newParam.ValueChange += userChangeParameter_ValueChange;
        }

        /// <summary>
        /// create PASSWORD server parameter
        /// </summary>
        public void CreateParameterPassword(string name, string param)
        {
            ServerParameterPassword newParam = new ServerParameterPassword();
            newParam.Name = name;
            newParam.Value = param;

            newParam = (ServerParameterPassword)LoadParam(newParam);
            if (_serverIsCreated)
            {
                ServerParameters.Insert(ServerParameters.Count - 9, newParam);
            }
            else
            {
                ServerParameters.Add(newParam);
            }

            newParam.ValueChange += userChangeParameter_ValueChange;
        }

        /// <summary>
        /// create PATH TO FILE server parameter
        /// </summary>
        public void CreateParameterPath(string name)
        {
            ServerParameterPath newParam = new ServerParameterPath();
            newParam.Name = name;

            newParam = (ServerParameterPath)LoadParam(newParam);
            if (_serverIsCreated)
            {
                ServerParameters.Insert(ServerParameters.Count - 9, newParam);
            }
            else
            {
                ServerParameters.Add(newParam);
            }

            newParam.ValueChange += userChangeParameter_ValueChange;
        }

        /// <summary>
        /// create Button server parameter
        /// </summary>
        public void CreateParameterButton(string name)
        {
            ServerParameterButton newParam = new ServerParameterButton();
            newParam.Name = name;

            newParam = (ServerParameterButton)LoadParam(newParam);
            if (_serverIsCreated)
            {
                ServerParameters.Insert(ServerParameters.Count - 9, newParam);
            }
            else
            {
                ServerParameters.Add(newParam);
            }

            newParam.ValueChange += userChangeParameter_ValueChange;
        }

        /// <summary>
        /// changed parameter state
        /// </summary>
        void userChangeParameter_ValueChange()
        {
            SaveParam();
        }

        private void _neadToSaveCandlesCountParam_ValueChange()
        {
            _candleStorage.CandlesSaveCount = _neadToSaveCandlesCountParam.Value;
        }

        private void _neadToSaveTicksDaysCountParam_ValueChange()
        {
            if (_tickStorage != null)
            {
                _tickStorage.DaysToLoad = _neadToSaveTicksDaysCountParam.Value;
            }
        }

        private void SaveTradesHistoryParam_ValueChange()
        {
            if (_tickStorage != null)
            {
                _tickStorage.NeadToSave = _neadToSaveTicksParam.Value;
            }
        }

        private void SaveCandleHistoryParam_ValueChange()
        {
            if (_candleStorage != null)
            {
                _candleStorage.NeadToSave = _neadToSaveCandlesParam.Value;
            }
        }

        /// <summary>
        /// save parameters
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

        #endregion

        #region Start / Stop server - user direction

        /// <summary>
        /// necessary server status. It needs to thread that listens to connectin
        /// Depending on this field manage the connection 
        /// </summary>
        private ServerConnectStatus _serverStatusNead;

        /// <summary>
        /// run the server. Connect to trade system
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

            LastStartServerTime = DateTime.Now.AddSeconds(-300);

            _serverStatusNead = ServerConnectStatus.Connect;
        }

        /// <summary>
        /// stop the server
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
        /// user requested connect to the API
        /// </summary>
        public event Action UserWhantConnect;

        /// <summary>
        /// user requested disconnect from the API
        /// </summary>
        public event Action UserWhantDisconnect;

        #endregion

        #region Server status

        /// <summary>
        /// server status
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
        /// server type
        /// </summary>
        public ServerType ServerType { get { return ServerRealization.ServerType; } }

        /// <summary>
        /// alert message from client that connection is established
        /// </summary>
        void _serverRealization_Connected()
        {
            SendLogMessage(OsLocalization.Market.Message6, LogMessageType.System);
            ServerStatus = ServerConnectStatus.Connect;
        }

        /// <summary>
        /// client connection has broken
        /// </summary>
        void _serverRealization_Disconnected()
        {
            if (ServerStatus == ServerConnectStatus.Disconnect)
            {
                return;
            }
            SendLogMessage(OsLocalization.Market.Message12, LogMessageType.System);
            ServerStatus = ServerConnectStatus.Disconnect;

            if (NeadToReconnectEvent != null)
            {
                NeadToReconnectEvent();
            }
        }

        /// <summary>
        /// connection state has changed
        /// </summary>
        public event Action<string> ConnectStatusChangeEvent;

        /// <summary>
        /// need to reconnect server and get a new data
        /// </summary>
        public event Action NeadToReconnectEvent;

        #endregion

        #region Work of main thread

        /// <summary>
        /// the place where connection is controlled. look at data streams
        /// </summary>
        [System.Runtime.ExceptionServices.HandleProcessCorruptedStateExceptionsAttribute]
        private async void PrimeThreadArea()
        {
            while (true)
            {
                Thread.Sleep(1000);
                try
                {
                    if (ServerRealization == null)
                    {
                        continue;
                    }

                    if ((ServerRealization.ServerStatus != ServerConnectStatus.Connect)
                        && _serverStatusNead == ServerConnectStatus.Connect &&
                       LastStartServerTime.AddSeconds(100) < DateTime.Now)
                    {
                        SendLogMessage(OsLocalization.Market.Message8, LogMessageType.System);
                        ServerRealization.Dispose();

                        if (Portfolios != null &&
                            Portfolios.Count != 0)
                        {
                            Portfolios.Clear();
                        }

                        DeleteCandleManager();
 
                        ServerRealization.Connect();
                        LastStartServerTime = DateTime.Now;

                        NeadToReconnectEvent?.Invoke();

                        continue;
                    }

                    if (ServerRealization.ServerStatus == ServerConnectStatus.Connect && _serverStatusNead == ServerConnectStatus.Disconnect)
                    {
                        SendLogMessage(OsLocalization.Market.Message9, LogMessageType.System);
                        ServerRealization.Dispose();

                        DeleteCandleManager();

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

                    DeleteCandleManager();

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
        /// </summary>
        public DateTime LastStartServerTime { get; set; }

        /// <summary>
        /// start candle downloading
        /// </summary>
        private void StartCandleManager()
        {
            if (_candleManager == null)
            {
                _candleManager = new CandleManager(this,StartProgram.IsOsTrader);
                _candleManager.CandleUpdateEvent += _candleManager_CandleUpdateEvent;
                _candleManager.LogMessageEvent += SendLogMessage;
            }
        }

        private void DeleteCandleManager()
        {
            if (_candleManager != null)
            {
                _candleManager.CandleUpdateEvent -= _candleManager_CandleUpdateEvent;
                _candleManager.LogMessageEvent -= SendLogMessage;
                _candleManager.Dispose();
                _candleManager = null;
            }
        }

        #endregion

        #region Data forwarding flow operation

        /// <summary>
        /// workplace of the thread sending data to the top
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
                        List<Trade> trades;

                        if (_tradesToSend.TryDequeue(out trades))
                        {
                            if (NewTradeEvent != null)
                            {
                                NewTradeEvent(trades);
                            }
                            if (_needToRemoveTradesFromMemory.Value == true && _allTrades != null)

                            {
                                for (int i = 0; i < _allTrades.Length; i++)
                                {
                                    List<Trade> curTrades = _allTrades[i];

                                    if (curTrades.Count > 100)
                                    {
                                        curTrades = curTrades.GetRange(curTrades.Count - 101, 100);
                                        _allTrades[i] = curTrades;
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

        /// <summary>
        /// queue of new orders
        /// </summary>
        private ConcurrentQueue<Order> _ordersToSend = new ConcurrentQueue<Order>();

        /// <summary>
        /// queue of ticks
        /// </summary>
        private ConcurrentQueue<List<Trade>> _tradesToSend = new ConcurrentQueue<List<Trade>>();

        /// <summary>
        /// queue of new portfolios
        /// </summary>
        private ConcurrentQueue<List<Portfolio>> _portfolioToSend = new ConcurrentQueue<List<Portfolio>>();

        /// <summary>
        /// queue of new securities
        /// </summary>
        private ConcurrentQueue<List<Security>> _securitiesToSend = new ConcurrentQueue<List<Security>>();

        /// <summary>
        /// queue of my new trades
        /// </summary>
        private ConcurrentQueue<MyTrade> _myTradesToSend = new ConcurrentQueue<MyTrade>();

        /// <summary>
        /// queue of new time
        /// </summary>
        private ConcurrentQueue<DateTime> _newServerTime = new ConcurrentQueue<DateTime>();

        /// <summary>
        /// queue of updated candles series
        /// </summary>
        private ConcurrentQueue<CandleSeries> _candleSeriesToSend = new ConcurrentQueue<CandleSeries>();

        /// <summary>
        /// queue of new depths 
        /// </summary>
        private ConcurrentQueue<MarketDepth> _marketDepthsToSend = new ConcurrentQueue<MarketDepth>();

        /// <summary>
        /// queue of updated bid and ask by security
        /// </summary>
        private ConcurrentQueue<BidAskSender> _bidAskToSend = new ConcurrentQueue<BidAskSender>();

        #endregion

        #region Server time

        /// <summary>
        /// server time
        /// </summary>
        public DateTime ServerTime
        {
            get { return _serverTime; }

            private set
            {
                if (value <= _serverTime)
                {
                    return;
                }

                _serverTime = value;

                if (_newServerTime.IsEmpty == true)
                {
                    _newServerTime.Enqueue(_serverTime);
                }
                ServerRealization.ServerTime = _serverTime;
            }
        }
        private DateTime _serverTime;

        /// <summary>
        /// server time changed
        /// </summary>
        public event Action<DateTime> TimeServerChangeEvent;

        #endregion

        #region Portfolios

        /// <summary>
        /// all account in the system
        /// </summary>
        public List<Portfolio> Portfolios
        {
            get { return _portfolios; }
        }
        private List<Portfolio> _portfolios;

        /// <summary>
        /// take portfolio by number
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
                        for(int i2 = 0;i2 < positions.Count;i2++)
                        {
                            curPortfolio.SetNewPosition(positions[i2]);
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
        /// </summary>
        public event Action<List<Portfolio>> PortfoliosChangeEvent;

        #endregion

        #region  Securities

        /// <summary>
        /// all instruments in the system
        /// </summary>
        public List<Security> Securities
        {
            get { return _securities; }
        }
        private List<Security> _securities = new List<Security>();

        private List<Security> _frequentlyUsedSecurities = new List<Security>();

        /// <summary>
        /// take the instrument as a Security by name of instrument
        /// </summary>
        public Security GetSecurityForName(string securityName, string securityClass)
        {
            if (_securities == null)
            {
                return null;
            }

            for (int i = 0; i < _frequentlyUsedSecurities.Count; i++)
            {
                if (_frequentlyUsedSecurities[i].Name == securityName &&
                    _frequentlyUsedSecurities[i].NameClass == securityClass)
                {
                    return _frequentlyUsedSecurities[i];
                }
            }

            for (int i = 0; i < _securities.Count; i++)
            {
                if(_securities[i].Name == securityName &&
                    _securities[i].NameClass == securityClass)
                {
                    _frequentlyUsedSecurities.Add(_securities[i]);
                    return _securities[i];
                }
            }

            for (int i = 0; i < _securities.Count; i++)
            {
                if (_securities[i].Name == securityName)
                {
                    return _securities[i];
                }
            }

            return null;
        }

        /// <summary>
        /// show security
        /// </summary>
        public void ShowSecuritiesDialog()
        {
            SecuritiesUi ui = new SecuritiesUi(this);
            ui.ShowDialog();
        }

        /// <summary>
        /// security list updated
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
        /// </summary>
        public event Action<List<Security>> SecuritiesChangeEvent;

        #endregion

        #region  Subcribe to data

        /// <summary>
        /// master of dowloading candles
        /// </summary>
        private CandleManager _candleManager;

        /// <summary>
        /// multithreaded access locker in StartThisSecurity
        /// </summary>
        private string _lockerStarter = "lockerStarterAserver";

        private string _lockerStarterByTime = "lockerStarterByTimeAserver";

        private DateTime _lastTrySubCandle = DateTime.MinValue;

        /// <summary>
        /// start uploading data on instrument
        /// </summary>
        /// <param name="securityName"> security name for running</param>
        /// <param name="timeFrameBuilder"> object that has data about timeframe</param>
        /// <param name="securityClass"> security class for running</param>
        /// <returns> returns CandleSeries if successful else null</returns>
        public CandleSeries StartThisSecurity(string securityName, TimeFrameBuilder timeFrameBuilder, string securityClass)
        {
            lock(_lockerStarterByTime)
            {
                if(_lastTrySubCandle.AddMilliseconds(100) > DateTime.Now)
                {
                    return null;
                }

                _lastTrySubCandle = DateTime.Now;
            }

            try
            {
                lock (_lockerStarter)
                {
                    if (securityName == "")
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
                        if (_securities[i].Name == securityName &&
                            (_securities[i].NameClass == securityClass))
                        {
                            security = _securities[i];
                            break;
                        }
                        if (_securities[i].Name == securityName &&
                            (securityClass == null))
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
        /// </summary>
        /// <param name="series"> candles series that need to stop</param>
        public void StopThisSecurity(CandleSeries series)
        {
            if (series != null && _candleManager != null)
            {
                _candleManager.StopSeries(series);
            }

            if(_candleStorage != null)
            {
                _candleStorage.RemoveSeries(series);
            }
        }

        /// <summary>
        /// candles series changed
        /// </summary>
        private void _candleManager_CandleUpdateEvent(CandleSeries series)
        {
            if (series.IsMergedByCandlesFromFile == false)
            {
                series.IsMergedByCandlesFromFile = true;

                if (_neadToSaveCandlesParam.Value == true)
                {
                    List<Candle> candles = _candleStorage.GetCandles(series.Specification, _neadToSaveCandlesCountParam.Value);
                    series.CandlesAll = series.CandlesAll.Merge(candles);
                }
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
        /// new candles event
        /// </summary>
        public event Action<CandleSeries> NewCandleIncomeEvent;

        #endregion

        #region Data upload

        private string _loadDataLocker;

        /// <summary>
        /// interface for getting the last candlesticks for a security. Used to activate candlestick series in live trades
        /// </summary>
        public List<Candle> GetLastCandleHistory(Security security, TimeFrameBuilder timeFrameBuilder, int candleCount)
        {
            try
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

                if (ServerRealization == null)
                {
                    return null;
                }

                return ServerRealization.GetLastCandleHistory(security, timeFrameBuilder, candleCount);
            }
            catch(Exception ex)
            {
                SendLogMessage(
                    "AServer. GetLastCandleHistory method error: " + ex.ToString(), 
                    LogMessageType.Error);

                return null;
            }
        }

        /// <summary>
        /// take the candle history for a period
        /// </summary>
        public List<Candle> GetCandleDataToSecurity(string securityName, string securityClass, TimeFrameBuilder timeFrameBuilder,
            DateTime startTime, DateTime endTime, DateTime actualTime, bool neadToUpdate)
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
                if(_securities[i].Name == securityName &&
                    _securities[i].NameClass == securityClass)
                {
                    security = _securities[i];
                    break;
                }
            }

            if (security == null)
            {
                for (int i = 0; _securities != null && i < _securities.Count; i++)
                {
                    if (string.IsNullOrEmpty(_securities[i].NameId) == false &&
                        _securities[i].NameId == securityName)
                    {
                        security = _securities[i];
                        break;
                    }
                }
            }

            if (security == null)
            {
                return null;
            }

            CandleSeries series = new CandleSeries(timeFrameBuilder, security, StartProgram.IsOsTrader);

            //ServerRealization.Subscrible(security);

            if (timeFrameBuilder.CandleCreateMethodType == CandleCreateMethodType.Simple)
            {
                lock(_loadDataLocker)
                {
                    series.CandlesAll =
                    ServerRealization.GetCandleDataToSecurity(security, timeFrameBuilder, startTime, endTime,
                    actualTime);
                }
            }

           /* if (series.CandlesAll == null)
            {
                List<Trade> trades = ServerRealization.GetTickDataToSecurity(security, startTime, endTime, actualTime);
                if (trades != null &&
                    trades.Count != 0)
                {
                    series.PreLoad(trades);
                }
            }*/

            if (series.CandlesAll != null &&
                series.CandlesAll.Count != 0)
            {
                series.IsStarted = true;
            }

            // _candleManager.StartSeries(series);

            return series.CandlesAll;
        }

        /// <summary>
        /// take ticks data for a period
        /// </summary>
        public List<Trade> GetTickDataToSecurity(string securityName, string securityClass, DateTime startTime, DateTime endTime, DateTime actualTime, bool neadToUpdete)
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

            if (actualTime == DateTime.MinValue)
            {
                actualTime = startTime;
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
                if (_securities[i].Name == securityName &&
                    _securities[i].NameClass == securityClass)
                {
                    security = _securities[i];
                    break;
                }
            }

            if (security == null)
            {
                for (int i = 0; _securities != null && i < _securities.Count; i++)
                {
                    if (string.IsNullOrEmpty(_securities[i].NameId) == false &&
                        _securities[i].NameId == securityName)
                    {
                        security = _securities[i];
                        break;
                    }
                }
                if (security == null)
                {
                    return null;
                }
            }
            List<Trade> trades = null;

            lock (_loadDataLocker)
            {
                trades = ServerRealization.GetTickDataToSecurity(security, startTime, endTime, actualTime);
            }
            return trades;
        }

        #endregion

        #region Market depth

        /// <summary>
        /// last market depths by securities
        /// </summary>
        private List<MarketDepth> _depths = new List<MarketDepth>();

        private string _depthsArrayLocker = "depthsLocker";
        
        private List<BidAskSender> _lastBidAskValues = new List<BidAskSender>();

        /// <summary>
        /// new depth event
        /// </summary>
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

                if (myDepth.Asks.Count == 0 && myDepth.Bids.Count == 0)
                {
                    return;
                }

                TrySendMarketDepthEvent(myDepth);
                TrySendBidAsk(myDepth);

            }
            catch (Exception error)
            {
                SendLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        private void TrySendMarketDepthEvent(MarketDepth newMarketDepth)
        {
            if (NewMarketDepthEvent == null)
            {
                return;
            }

            if (_needToUseFullMarketDepth.Value == false)
            {
                return;
            }

            _marketDepthsToSend.Enqueue(newMarketDepth);

            if(_needToLoadBidAskInTrades.Value)
            {
                bool isInArray = false;

                for(int i = 0;i < _depths.Count;i++)
                {
                    if (_depths[i].SecurityNameCode == newMarketDepth.SecurityNameCode)
                    {
                        _depths[i] = newMarketDepth;
                        isInArray = true;
                    }
                }

                if(isInArray == false)
                {
                    lock (_depthsArrayLocker)
                    {
                        _depths.Add(newMarketDepth);
                    }
                }
            }
        }

        private void TrySendBidAsk(MarketDepth newMarketDepth)
        {
            if (NewBidAscIncomeEvent == null)
            {
                return;
            }

            decimal bestBid = newMarketDepth.Bids[0].Price;
            decimal bestAsk = newMarketDepth.Asks[0].Price;

            if (bestBid == 0 ||
                bestAsk == 0)
            {
                return;
            }

            Security sec = GetSecurityForName(newMarketDepth.SecurityNameCode, "");

            if (sec == null)
            {
                return;
            }

            for (int i = 0; i < _lastBidAskValues.Count; i++)
            {
                if (_lastBidAskValues[i].Security.Name == sec.Name)
                {
                    if (_lastBidAskValues[i].Bid == bestBid &&
                        _lastBidAskValues[i].Ask == bestAsk)
                    {
                        return;
                    }
                }
            }

            BidAskSender newSender = new BidAskSender();
            newSender.Bid = bestBid;
            newSender.Ask = bestAsk;
            newSender.Security = sec;

            _bidAskToSend.Enqueue(newSender);

            bool isInArray = false;

            for (int i = 0; i < _lastBidAskValues.Count; i++)
            {
                if (_lastBidAskValues[i].Security.Name == sec.Name)
                {
                    _lastBidAskValues[i] = newSender;
                    isInArray = true;
                    break;
                }
            }

            if (isInArray == false)
            {
                _lastBidAskValues.Add(newSender);
            }
        }

        /// <summary>
        /// best bid or ask changed for the instrument
        /// </summary>
        public event Action<decimal, decimal, Security> NewBidAscIncomeEvent;

        /// <summary>
        /// new depth in the system
        /// </summary>
        public event Action<MarketDepth> NewMarketDepthEvent;

        #endregion

        #region Trades

        /// <summary>
        /// ticks storage
        /// </summary>
        private ServerTickStorage _tickStorage;

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

        private string _newTradesLocker = "tradesLocker";

        /// <summary>
        /// came new ticks
        /// </summary>
        void ServerRealization_NewTradesEvent(Trade trade)
        {
            try
            {
                if(trade == null)
                {
                    return;
                }

                if (trade.Price <= 0)
                {
                    return;
                }

                ServerTime = trade.Time;

                if (_needToLoadBidAskInTrades.Value)
                {
                    BathTradeMarketDepthData(trade);
                }

                lock(_newTradesLocker)
                {
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

                            if (curList == null
                                || curList.Count == 0
                                || curList[0] == null)
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

                            if(_needToUpdateOnlyTradesWithNewPrice.Value == true)
                            {
                                Trade lastTrade = curList[curList.Count - 1];

                                if(lastTrade == null 
                                    || lastTrade.Price == trade.Price)
                                {
                                    return;
                                }
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

                        _tradesToSend.Enqueue(myList);
                    }
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
            MarketDepth depth = null;

            lock (_depthsArrayLocker)
            {
                for (int i = 0; i < _depths.Count; i++)
                {
                    if (_depths[i].SecurityNameCode == trade.SecurityNameCode)
                    {
                        depth = _depths[i];
                        break;
                    }
                }
            }

            if(depth == null)
            {
                return;
            }

            if (depth.Asks == null || depth.Asks.Count == 0 ||
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

        #endregion

        #region MyTrade

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

        #endregion

        #region Work with orders

        /// <summary>
        /// work place of thred on the queues of ordr execution and order cancellation 
        /// место работы потока на очередях исполнения заявок и их отмены
        /// </summary>
        private async void ExecutorOrdersThreadArea()
        {
            while (true)
            {
                if (LastStartServerTime.AddSeconds(WaitTimeToTradeAfterFirstStart) > DateTime.Now)
                {
                    await Task.Delay(1000);
                    continue;
                }

                try
                {
                    if(_ordersToExecute.IsEmpty == true)
                    {
                        await Task.Delay(1);
                        continue;
                    }

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
                            else if(order.OrderSendType == OrderSendType.ChangePrice 
                                && IsCanChangeOrderPrice)
                            {
                                ServerRealization.ChangeOrderPrice(order.Order, order.NewPrice);
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
        /// waiting time after server start, after which it is possible to place orders
        /// </summary>
        public double WaitTimeToTradeAfterFirstStart
        {
            set { _waitTimeAfterFirstStart = value; }

            get
            {
                if (_alreadyLoadAwaitInfoFromServerPermission == false)
                {
                    _alreadyLoadAwaitInfoFromServerPermission = true;

                    IServerPermission permission = ServerMaster.GetServerPermission(this.ServerType);

                    if (permission != null)
                    {
                        _waitTimeAfterFirstStart = permission.WaitTimeSecondsAfterFirstStartToSendOrders;
                    }
                }

                return _waitTimeAfterFirstStart;
            }
        }
        private double _waitTimeAfterFirstStart = 60;
        private bool _alreadyLoadAwaitInfoFromServerPermission = false;

        /// <summary>
        /// does the server support order price change
        /// </summary>
        public bool IsCanChangeOrderPrice
        {
            get
            {
                if (ServerType == ServerType.None)
                {
                    return false;
                }

                IServerPermission serverPermision = ServerMaster.GetServerPermission(ServerType);

                if (serverPermision == null)
                {
                    return false;
                }

                return serverPermision.IsCanChangeOrderPrice;
            }
        }

        /// <summary>
        /// incoming order from system
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
        /// </summary>
        public void ExecuteOrder(Order order)
        {
            if (UserSetOrderOnExecute != null)
            {
                UserSetOrderOnExecute(order);
            }
            if (LastStartServerTime.AddSeconds(WaitTimeToTradeAfterFirstStart) > DateTime.Now)
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
        /// Order price change
        /// </summary>
        /// <param name="order">An order that will have a new price</param>
        /// <param name="newPrice">New price</param>
        public void ChangeOrderPrice(Order order, decimal newPrice)
        {
            try
            {
                if (string.IsNullOrEmpty(order.NumberMarket))
                {
                    SendLogMessage("You can't change order price an order without a stock exchange number " + order.NumberUser, LogMessageType.System);
                    return;
                }

                if(ServerRealization.ServerStatus == ServerConnectStatus.Disconnect)
                {
                    SendLogMessage("You can't change order price when server status Disconnect " + order.NumberUser, LogMessageType.System);
                    return;
                }

                if(order.Price == newPrice)
                {
                    return;
                }

                OrderAserverSender ord = new OrderAserverSender();
                ord.Order = order;
                ord.OrderSendType = OrderSendType.ChangePrice;
                ord.NewPrice = newPrice;

                _ordersToExecute.Enqueue(ord);

                SendLogMessage(OsLocalization.Market.Message116 + order.NumberUser, LogMessageType.System);

            }
            catch (Exception error)
            {
                SendLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        /// <summary>
        /// cancel order from the trading system
        /// </summary>
        public void CancelOrder(Order order)
        {
            if (UserSetOrderOnCancel != null)
            {
                UserSetOrderOnCancel(order);
            }

            if(string.IsNullOrEmpty(order.NumberMarket))
            {
                SendLogMessage("You can't revoke an order without a stock exchange number " + order.NumberUser, LogMessageType.System);
                return;
            }

            OrderAserverSender ord = new OrderAserverSender();
            ord.Order = order;
            ord.OrderSendType = OrderSendType.Cancel;

            _ordersToExecute.Enqueue(ord);

            SendLogMessage(OsLocalization.Market.Message24 + order.NumberUser, LogMessageType.System);
        }

        /// <summary>
        /// cancel all orders from trading system
        /// </summary>
        public void CancelAllOrders()
        {
            if(ServerStatus == ServerConnectStatus.Disconnect)
            {
                return;
            }

            ServerRealization.CancelAllOrders();
        }

        /// <summary>
        /// cancel all orders from trading system to security
        /// </summary>
        public void CancelAllOrdersToSecurity(Security security)
        {
            if (ServerStatus == ServerConnectStatus.Disconnect)
            {
                return;
            }

            ServerRealization.CancelAllOrdersToSecurity(security);
        }

        /// <summary>
        /// order changed
        /// </summary>
        public event Action<Order> NewOrderIncomeEvent;

        /// <summary>
        /// external systems requested order execution
        /// </summary>
        public event Action<Order> UserSetOrderOnExecute;

        /// <summary>
        /// external systems requested order cancellation
        /// </summary>
        public event Action<Order> UserSetOrderOnCancel;

        #endregion

        #region Log messages

        /// <summary>
        /// add a new message in the log
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
        /// </summary>
        public Log Log;

        /// <summary>
        /// outgoing messages for the log
        /// </summary>
        public event Action<string, LogMessageType> LogMessageEvent;

        #endregion
    }

    public class OrderAserverSender
    {
        public Order Order;

        public OrderSendType OrderSendType;

        public decimal NewPrice;
    }

    public enum OrderSendType
    {
        Execute,
        Cancel,
        ChangePrice
    }
}