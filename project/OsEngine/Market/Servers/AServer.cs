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

                CreateParameterButton(OsLocalization.Market.ServerParam12);
                ServerParameters[9].Comment = OsLocalization.Market.Label131;
                ((ServerParameterButton)ServerParameters[9]).UserClickButton += AServer_UserClickButton;

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

                Log = new Log(_serverRealization.ServerType + "Server", StartProgram.IsOsTrader);
                Log.Listen(this);

                _serverStatusNeed = ServerConnectStatus.Disconnect;

                _loadDataLocker = "lockerData_" + ServerType.ToString();

                Task task0 = new Task(ExecutorOrdersThreadArea);
                task0.Start();

                Task task = new Task(PrimeThreadArea);
                task.Start();

                Task task2 = new Task(SenderThreadArea);
                task2.Start();

                Task task3 = new Task(MyTradesBeepThread);
                task3.Start();

                _serverIsCreated = true;

                _ordersHub = new AServerOrdersHub(this);
                _ordersHub.LogMessageEvent += SendLogMessage;
                _ordersHub.GetAllActivOrdersOnReconnectEvent += _ordersHub_GetAllActivOrdersOnReconnectEvent;
                _ordersHub.ActivStateOrderCheckStatusEvent += _ordersHub_ActivStateOrderCheckStatusEvent;
                _ordersHub.LostOrderEvent += _ordersHub_LostOrderEvent;
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

        /// <summary>
        /// settings window
        /// </summary>
        private AServerParameterUi _ui;

        /// <summary>
        /// user has closed the server settings window
        /// </summary>
        private void _ui_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            _ui.Closing -= _ui_Closing;
            _ui = null;
        }

        #endregion

        #region Parameters

        /// <summary>
        /// whether a server object has been created
        /// </summary>
        private bool _serverIsCreated;

        /// <summary>
        /// whether to save the current session's trades to the file system
        /// </summary>
        private ServerParameterBool _neadToSaveTicksParam;

        /// <summary>
        /// parameter with the number of days for saving ticks
        /// </summary>
        private ServerParameterInt _neadToSaveTicksDaysCountParam;

        /// <summary>
        /// whether candles should be saved to the file system
        /// </summary>
        private ServerParameterBool _neadToSaveCandlesParam;

        /// <summary>
        /// number of candles for which trades should be loaded at the start of the connector
        /// </summary>
        public ServerParameterInt _neadToSaveCandlesCountParam;

        /// <summary>
        /// whether trades should be filled with data on the best bid and ask.
        /// </summary>
        private ServerParameterBool _needToLoadBidAskInTrades;

        /// <summary>
        /// whether to delete the transaction feed from memory
        /// </summary>
        private ServerParameterBool _needToRemoveTradesFromMemory;

        /// <summary>
        /// whether the candles should be removed from the memory
        /// </summary>
        public ServerParameterBool _needToRemoveCandlesFromMemory;

        /// <summary>
        /// whether we use the full stack of market depth or only bid and ask.
        /// </summary>
        public ServerParameterBool _needToUseFullMarketDepth;

        /// <summary>
        /// only trades with a new price are submitted to the top.
        /// </summary>
        public ServerParameterBool _needToUpdateOnlyTradesWithNewPrice;

        /// <summary>
        /// blocks the display of the default server settings in the settings window. 
        /// </summary>
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
                ServerParameters.Insert(ServerParameters.Count - 10, newParam);
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
                ServerParameters.Insert(ServerParameters.Count - 10, newParam);
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
                ServerParameters.Insert(ServerParameters.Count - 10, newParam);
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
                ServerParameters.Insert(ServerParameters.Count - 10, newParam);
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
                ServerParameters.Insert(ServerParameters.Count - 10, newParam);
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
                ServerParameters.Insert(ServerParameters.Count - 10, newParam);
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
                ServerParameters.Insert(ServerParameters.Count - 10, newParam);
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
                ServerParameters.Insert(ServerParameters.Count - 10, newParam);
            }
            else
            {
                ServerParameters.Add(newParam);
            }

            newParam.ValueChange += userChangeParameter_ValueChange;
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

        /// <summary>
        /// user has changed the value of the parameter
        /// </summary>
        private void userChangeParameter_ValueChange()
        {
            SaveParam();
        }

        /// <summary>
        /// user has changed the value of the parameter
        /// </summary>
        private void _neadToSaveCandlesCountParam_ValueChange()
        {
            _candleStorage.CandlesSaveCount = _neadToSaveCandlesCountParam.Value;
        }

        /// <summary>
        /// user has changed the value of the parameter
        /// </summary>
        private void _neadToSaveTicksDaysCountParam_ValueChange()
        {
            if (_tickStorage != null)
            {
                _tickStorage.DaysToLoad = _neadToSaveTicksDaysCountParam.Value;
            }
        }

        /// <summary>
        /// user has changed the value of the parameter
        /// </summary>
        private void SaveTradesHistoryParam_ValueChange()
        {
            if (_tickStorage != null)
            {
                _tickStorage.NeadToSave = _neadToSaveTicksParam.Value;
            }
        }

        /// <summary>
        /// user has changed the value of the parameter
        /// </summary>
        private void SaveCandleHistoryParam_ValueChange()
        {
            if (_candleStorage != null)
            {
                _candleStorage.NeadToSave = _neadToSaveCandlesParam.Value;
            }
        }

        #endregion

        #region Start / Stop server - user direction

        /// <summary>
        /// necessary server status. It needs to thread that listens to connectin
        /// Depending on this field manage the connection 
        /// </summary>
        private ServerConnectStatus _serverStatusNeed;

        /// <summary>
        /// run the server. Connect to trade system
        /// </summary>
        public void StartServer()
        {
            if (UserWhantConnect != null)
            {
                UserWhantConnect();
            }

            if (_serverStatusNeed == ServerConnectStatus.Connect)
            {
                return;
            }

            LastStartServerTime = DateTime.Now.AddSeconds(-300);

            _serverStatusNeed = ServerConnectStatus.Connect;
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
            _serverStatusNeed = ServerConnectStatus.Disconnect;
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

        #region Thread 1. Work with connection

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
                        && _serverStatusNeed == ServerConnectStatus.Connect &&
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

                    if (ServerRealization.ServerStatus == ServerConnectStatus.Connect && _serverStatusNeed == ServerConnectStatus.Disconnect)
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

                    try
                    {
                        ServerRealization.Dispose();
                    }
                    catch(Exception ex)
                    {
                        SendLogMessage(ex.ToString(), LogMessageType.Error);
                    }

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
        /// start a candle-collecting device
        /// </summary>
        private void StartCandleManager()
        {
            if (_candleManager == null)
            {
                _candleManager = new CandleManager(this, StartProgram.IsOsTrader);
                _candleManager.CandleUpdateEvent += _candleManager_CandleUpdateEvent;
                _candleManager.LogMessageEvent += SendLogMessage;
            }
        }

        /// <summary>
        /// dispose a candle-collecting device
        /// </summary>
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

        #region Thread 2. Data forwarding operations

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
                            if (TestValue_CanSendOrdersUp)
                            {
                                if (NewOrderIncomeEvent != null)
                                {
                                    NewOrderIncomeEvent(order);
                                }

                                _ordersHub.SetOrderFromApi(order);

                                for (int i = 0; i < _myTrades.Count; i++)
                                {
                                    if (_myTrades[i].NumberOrderParent == order.NumberMarket)
                                    {
                                        _myTradesToSend.Enqueue(_myTrades[i]);
                                    }
                                }
                            }
                        }
                    }
                    else if (!_myTradesToSend.IsEmpty &&
                             (_ordersToSend.IsEmpty))
                    {
                        MyTrade myTrade;

                        if (_myTradesToSend.TryDequeue(out myTrade))
                        {
                            if (TestValue_CanSendOrdersUp)
                            {
                                if (NewMyTradeEvent != null)
                                {
                                    NewMyTradeEvent(myTrade);
                                }

                                bool isInArray = false;

                                for(int i = 0;i < _myTrades.Count;i++)
                                {
                                    if (_myTrades[i].NumberTrade == myTrade.NumberTrade)
                                    {
                                        isInArray = true;
                                        break;
                                    }
                                }

                                if(isInArray == false)
                                {
                                    _myTrades.Add(myTrade);
                                }
                                
                                while(_myTrades.Count > 1000)
                                {
                                    _myTrades.RemoveAt(0);
                                }

                                _neadToBeepOnTrade = true;
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

        public bool TestValue_CanSendOrdersUp = true;

        /// <summary>
        /// queue of ticks
        /// </summary>
        private ConcurrentQueue<List<Trade>> _tradesToSend = new ConcurrentQueue<List<Trade>>();

        /// <summary>
        /// queue of new or updated portfolios
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
        /// server time changed event
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
                        for (int i2 = 0; i2 < positions.Count; i2++)
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
        /// portfolios changed event
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

        /// <summary>
        /// often used securities. optimizes access to securities
        /// </summary>
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
                if (_securities[i].Name == securityName &&
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
            try
            {
                if (securities == null
                    || securities.Count == 0)
                {
                    return;
                }

                TryUpdateSecuritiesUserSettings(securities);

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
                    if (string.IsNullOrEmpty(securities[i].NameId))
                    {
                        SendLogMessage(OsLocalization.Market.Message13, LogMessageType.Error);
                        continue;
                    }
                    if (string.IsNullOrEmpty(securities[i].Name))
                    {
                        SendLogMessage(OsLocalization.Market.Message98, LogMessageType.Error);
                        continue;
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
            catch (Exception ex)
            {
                SendLogMessage("AServer Error. _serverRealization_SecurityEvent  " + ex.ToString(), LogMessageType.Error);
            }
        }

        /// <summary>
        /// instruments changed
        /// </summary>
        public event Action<List<Security>> SecuritiesChangeEvent;

        SecuritiesUi _securitiesUi;

        private void AServer_UserClickButton()
        {
            if(_securitiesUi == null)
            {
                _securitiesUi = new SecuritiesUi(this);
                _securitiesUi.Show();
                _securitiesUi.Closed += _securitiesUi_Closed;
            }
            else
            {
                _securitiesUi.Activate();
            }
        }

        private void _securitiesUi_Closed(object sender, EventArgs e)
        {
            _securitiesUi.Closed -= _securitiesUi_Closed;
            _securitiesUi = null;
        }

        private List<Security> _savedSecurities;

        private void TryUpdateSecuritiesUserSettings(List<Security> securities)
        {
            try
            {
                if (_savedSecurities == null)
                {
                    _savedSecurities = LoadSavedSecurities();
                }

                for (int i = 0; i < _savedSecurities.Count; i++)
                {
                    Security curSaveSec = _savedSecurities[i];

                    for (int j = 0; j < securities.Count; j++)
                    {
                        if (securities[j].Name == curSaveSec.Name
                            && securities[j].NameId == curSaveSec.NameId
                            && securities[j].SecurityType == curSaveSec.SecurityType
                            && securities[j].NameClass == curSaveSec.NameClass)
                        {
                            securities[j].Lot = curSaveSec.Lot;
                            securities[j].PriceStep = curSaveSec.PriceStep;
                            securities[j].PriceStepCost = curSaveSec.PriceStepCost;
                            securities[j].Decimals = curSaveSec.Decimals;
                            securities[j].DecimalsVolume = curSaveSec.DecimalsVolume;
                            securities[j].MinTradeAmount = curSaveSec.MinTradeAmount;
                            //securities[j].PriceLimitHigh = curSaveSec.PriceLimitHigh;
                            //securities[j].PriceLimitLow = curSaveSec.PriceLimitLow;
                            //securities[j].Go = curSaveSec.Go;
                            securities[j].Strike = curSaveSec.Strike;


                            break;
                        }
                    }
                }
            }
            catch(Exception ex)
            {
                SendLogMessage(ex.ToString(),LogMessageType.Error);
            }
        }

        private List<Security> LoadSavedSecurities()
        {
            List<Security> securities = new List<Security>();

            try
            {
                if (Directory.Exists(@"Engine\ServerDopSettings") == false)
                {
                    return securities;
                }

                if (Directory.Exists(@"Engine\ServerDopSettings\" + ServerType) == false)
                {
                    return securities;
                }

                string[] paths = Directory.GetFiles(@"Engine\ServerDopSettings\" + ServerType);

                for (int i = 0; paths != null && i < paths.Length; i++)
                {
                    string curPath = paths[i];

                    using (StreamReader reader = new StreamReader(curPath))
                    {
                        string secInStr = reader.ReadToEnd();

                        Security newSecurity = new Security();
                        newSecurity.LoadFromString(secInStr);
                        securities.Add(newSecurity);
                    }
                }

                return securities;
            }
            catch(Exception ex)
            {
                SendLogMessage(ex.ToString(),LogMessageType.Error);
                return securities;
            }
        }

        #endregion

        #region  Subscribe to data

        /// <summary>
        /// master of dowloading candles
        /// </summary>
        private CandleManager _candleManager;

        /// <summary>
        /// object for accessing candle storage in the file system
        /// </summary>
        private ServerCandleStorage _candleStorage;

        /// <summary>
        /// multithreaded access locker in StartThisSecurity
        /// </summary>
        private string _lockerStarter = "lockerStarterAserver";

        /// <summary>
        /// start uploading data on instrument
        /// </summary>
        /// <param name="securityName"> security name for running</param>
        /// <param name="timeFrameBuilder"> object that has data about timeframe</param>
        /// <param name="securityClass"> security class for running</param>
        /// <returns> returns CandleSeries if successful else null</returns>
        public CandleSeries StartThisSecurity(string securityName, TimeFrameBuilder timeFrameBuilder, string securityClass)
        {
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

            if (_candleStorage != null)
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

            if (series.IsMergedByTradesFromFile == false)
            {
                series.IsMergedByTradesFromFile = true;

                if (_neadToSaveTicksParam.Value == true
                    && series.TimeFrameBuilder.SaveTradesInCandles)
                {
                    List<Trade> trades = GetAllTradesToSecurity(series.Security);

                    if (trades != null && trades.Count > 0)
                    {
                        series.LoadTradesInCandles(trades);
                    }
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

        /// <summary>
        /// blocker of data request methods from multithreaded access
        /// </summary>
        private string _loadDataLocker;

        /// <summary>
        /// interface for getting the last candlesticks for a security. 
        /// Used to activate candlestick series in live trades
        /// </summary>
        [System.Runtime.ExceptionServices.HandleProcessCorruptedStateExceptionsAttribute]
        public List<Candle> GetLastCandleHistory(Security security, TimeFrameBuilder timeFrameBuilder, int candleCount)
        {
            try
            {
                if (ServerStatus != ServerConnectStatus.Connect)
                {
                    return null;
                }

                if (ServerRealization == null)
                {
                    return null;
                }

                return ServerRealization.GetLastCandleHistory(security, timeFrameBuilder, candleCount);
            }
            catch (Exception ex)
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
        [System.Runtime.ExceptionServices.HandleProcessCorruptedStateExceptionsAttribute]
        public List<Candle> GetCandleDataToSecurity(string securityName, string securityClass, TimeFrameBuilder timeFrameBuilder,
            DateTime startTime, DateTime endTime, DateTime actualTime, bool neadToUpdate)
        {
            try
            {
                if (Securities == null)
                {
                    return null;
                }

                if (LastStartServerTime != DateTime.MinValue &&
                    LastStartServerTime.AddSeconds(5) > DateTime.Now)
                {
                    return null;
                }

                if (ServerStatus != ServerConnectStatus.Connect)
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

                for (int i = 0; _securities != null && i < _securities.Count; i++)
                {
                    if (_securities[i].NameClass == securityClass &&
                        string.IsNullOrEmpty(_securities[i].NameId) == false &&
                            _securities[i].NameId == securityName)
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

                List<Candle> candles = null;

                if (timeFrameBuilder.CandleCreateMethodType == "Simple")
                {
                    lock (_loadDataLocker)
                    {
                        candles =
                        ServerRealization.GetCandleDataToSecurity(security, timeFrameBuilder, startTime, endTime,
                        actualTime);
                    }
                }

                return candles;

            }
            catch (Exception ex)
            {
                SendLogMessage(
                    "AServer. GetCandleDataToSecurity method error: " + ex.ToString(),
                    LogMessageType.Error);

                return null;
            }
        }

        /// <summary>
        /// take ticks data for a period
        /// </summary>
        [System.Runtime.ExceptionServices.HandleProcessCorruptedStateExceptionsAttribute]
        public List<Trade> GetTickDataToSecurity(string securityName, string securityClass, DateTime startTime, DateTime endTime, DateTime actualTime, bool neadToUpdete)
        {
            try
            {
                if (Securities == null)
                {
                    return null;
                }

                if (LastStartServerTime != DateTime.MinValue &&
                    LastStartServerTime.AddSeconds(5) > DateTime.Now)
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
            catch (Exception ex)
            {
                SendLogMessage(
                    "AServer. GetTickDataToSecurity method error: " + ex.ToString(),
                    LogMessageType.Error);

                return null;
            }
        }

        #endregion

        #region Market depth

        /// <summary>
        /// last market depths by securities
        /// </summary>
        private List<MarketDepth> _depths = new List<MarketDepth>();

        /// <summary>
        /// array blocker with market depths against multithreaded access
        /// </summary>
        private string _depthsArrayLocker = "depthsLocker";

        /// <summary>
        /// last bid and ask values by securities
        /// </summary>
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

                if ((myDepth.Asks == null ||
                      myDepth.Asks.Count == 0)
                     &&
                     ( myDepth.Bids == null ||
                    myDepth.Bids.Count == 0))
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

        /// <summary>
        /// send the incoming market depth to the top
        /// </summary>
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

            if (_needToLoadBidAskInTrades.Value)
            {
                bool isInArray = false;

                for (int i = 0; i < _depths.Count; i++)
                {
                    if (_depths[i].SecurityNameCode == newMarketDepth.SecurityNameCode)
                    {
                        _depths[i] = newMarketDepth;
                        isInArray = true;
                    }
                }

                if (isInArray == false)
                {
                    lock (_depthsArrayLocker)
                    {
                        _depths.Add(newMarketDepth);
                    }
                }
            }
        }

        /// <summary>
        /// send the incoming bid ask values to the top
        /// </summary>
        private void TrySendBidAsk(MarketDepth newMarketDepth)
        {
            if (NewBidAscIncomeEvent == null)
            {
                return;
            }

            decimal bestBid = 0;
            if(newMarketDepth.Bids != null &&
                newMarketDepth.Bids.Count > 0)
            {
                bestBid = newMarketDepth.Bids[0].Price;
            }

            decimal bestAsk = 0;
            if(newMarketDepth.Asks != null &&
                newMarketDepth.Asks.Count > 0)
            {
                bestAsk = newMarketDepth.Asks[0].Price;
            }
           
            if (bestBid == 0 &&
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
        /// object for accessing trades storage in the file system
        /// </summary>
        private ServerTickStorage _tickStorage;

        /// <summary>
        /// all server trades
        /// </summary>
        public List<Trade>[] AllTrades { get { return _allTrades; } }
        private List<Trade>[] _allTrades;

        /// <summary>
        /// array blocker with trades against multithreaded access
        /// </summary>
        private string _newTradesLocker = "tradesLocker";

        /// <summary>
        /// get trade history by security
        /// </summary>
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
        /// storage load trades from file system
        /// </summary>
        void _tickStorage_TickLoadedEvent(List<Trade>[] trades)
        {
            _allTrades = trades;
        }

        /// <summary>
        /// new trade event from ServerRealization
        /// </summary>
        void ServerRealization_NewTradesEvent(Trade trade)
        {
            try
            {
                if (trade == null)
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

                lock (_newTradesLocker)
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

                            if (_needToUpdateOnlyTradesWithNewPrice.Value == true)
                            {
                                Trade lastTrade = curList[curList.Count - 1];

                                if (lastTrade == null
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

            if (depth == null)
            {
                return;
            }

            if(depth.Asks != null &&
                depth.Asks.Count > 0)
            {
                trade.Ask = depth.Asks[0].Price;
            }
          
            if(depth.Bids != null &&
                depth.Bids.Count > 0)
            {
                trade.Bid = depth.Bids[0].Price;
            }
            
            trade.BidsVolume = depth.BidSummVolume;
            trade.AsksVolume = depth.AskSummVolume;
        }

        /// <summary>
        /// new trade event
        /// </summary>
        public event Action<List<Trade>> NewTradeEvent;

        #endregion

        #region MyTrade

        /// <summary>
        /// my trades array
        /// </summary>
        public List<MyTrade> MyTrades
        {
            get { return _myTrades; }
        }
        private List<MyTrade> _myTrades = new List<MyTrade>();

        /// <summary>
        /// whether a sound must be emitted during a new my trade
        /// </summary>
        private bool _neadToBeepOnTrade;

        /// <summary>
        /// buzzer mechanism 
        /// </summary>
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

        /// <summary>
        /// my trades incoming from IServerRealization
        /// </summary>
        void _serverRealization_MyTradeEvent(MyTrade trade)
        {
            if (trade.Time == DateTime.MinValue)
            {
                trade.Time = ServerTime;
            }

            _myTradesToSend.Enqueue(trade);
        }

        /// <summary>
        /// my trade changed event
        /// </summary>
        public event Action<MyTrade> NewMyTradeEvent;

        #endregion

        #region Thread 3. Work with orders

        /// <summary>
        /// work place of thred on the queues of ordr execution and order cancellation 
        /// </summary>
        [System.Runtime.ExceptionServices.HandleProcessCorruptedStateExceptionsAttribute]
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
                    if (_ordersToExecute.IsEmpty == true)
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
                            else if (order.OrderSendType == OrderSendType.ChangePrice
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

        /// <summary>
        /// array for storing orders to be sent to the exchange
        /// </summary>
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
        /// send order for execution to the trading system
        /// </summary>
        public void ExecuteOrder(Order order)
        {
            try
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

                if (ServerRealization.ServerStatus == ServerConnectStatus.Disconnect)
                {
                    SendLogMessage("AServer Error. You can't Execute order when server status Disconnect "
                        + order.NumberUser, LogMessageType.Error);
                    order.State = OrderStateType.Fail;
                    _ordersToSend.Enqueue(order);

                    return;
                }

                if (_portfolios == null ||
                    _portfolios.Count == 0)
                {
                    SendLogMessage("AServer Error. You can't Execute order when Portfolious is null "
                       + order.NumberUser, LogMessageType.Error);
                    order.State = OrderStateType.Fail;
                    _ordersToSend.Enqueue(order);

                    return;
                }

                if (string.IsNullOrEmpty(order.PortfolioNumber) == true)
                {
                    SendLogMessage("AServer Error. You can't Execute order without specifying his portfolio "
                         + order.NumberUser, LogMessageType.Error);
                    order.State = OrderStateType.Fail;
                    _ordersToSend.Enqueue(order);

                    return;
                }

                Portfolio myPortfolio = null;

                for (int i = 0; i < _portfolios.Count; i++)
                {
                    if (_portfolios[i].Number == order.PortfolioNumber)
                    {
                        myPortfolio = _portfolios[i];
                        break;
                    }
                }

                if (myPortfolio == null)
                {
                    SendLogMessage("AServer Error. You can't Execute order. Error portfolio name: "
                         + order.PortfolioNumber, LogMessageType.Error);
                    order.State = OrderStateType.Fail;
                    _ordersToSend.Enqueue(order);

                    return;
                }

                order.TimeCreate = ServerTime;

                OrderAserverSender ord = new OrderAserverSender();
                ord.Order = order;
                ord.OrderSendType = OrderSendType.Execute;

                _ordersHub.SetOrderFromOsEngine(order);

                _ordersToExecute.Enqueue(ord);

                SendLogMessage(OsLocalization.Market.Message19 + order.Price +
                               OsLocalization.Market.Message20 + order.Side +
                               OsLocalization.Market.Message21 + order.Volume +
                               OsLocalization.Market.Message22 + order.SecurityNameCode +
                               OsLocalization.Market.Message23 + order.NumberUser, LogMessageType.System);

            }
            catch (Exception error)
            {
                SendLogMessage(error.ToString(), LogMessageType.Error);
            }
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
                    SendLogMessage("AServer Error. You can't change order price an order without a stock exchange number "
                        + order.NumberUser, LogMessageType.Error);
                    return;
                }

                if (ServerRealization.ServerStatus == ServerConnectStatus.Disconnect)
                {
                    SendLogMessage("AServer Error. You can't change order price when server status Disconnect "
                        + order.NumberUser, LogMessageType.Error);
                    return;
                }

                if (order.Price == newPrice)
                {
                    return;
                }

                OrderAserverSender ord = new OrderAserverSender();
                ord.Order = order;
                ord.OrderSendType = OrderSendType.ChangePrice;
                ord.NewPrice = newPrice;

                _ordersToExecute.Enqueue(ord);

                SendLogMessage(OsLocalization.Market.Label120 + order.NumberUser, LogMessageType.System);

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
            try
            {
                if (UserSetOrderOnCancel != null)
                {
                    UserSetOrderOnCancel(order);
                }

                if (string.IsNullOrEmpty(order.NumberMarket))
                {
                    SendLogMessage("AServer Error. You can't cancel an order without a stock exchange number "
                        + order.NumberUser, LogMessageType.Error);
                    return;
                }

                if (ServerRealization.ServerStatus == ServerConnectStatus.Disconnect)
                {
                    SendLogMessage("AServer Error. You can't cancel order when server status Disconnect "
                        + order.NumberUser, LogMessageType.Error);
                    return;
                }

                OrderCounter saveOrder = null;

                for (int i = 0; i < _canceledOrders.Count; i++)
                {
                    if (_canceledOrders[i].NumberMarket == order.NumberMarket)
                    {
                        saveOrder = _canceledOrders[i];
                        break;
                    }
                }

                if (saveOrder == null)
                {
                    saveOrder = new OrderCounter();
                    saveOrder.NumberMarket = order.NumberMarket;
                    _canceledOrders.Add(saveOrder);

                    if (_canceledOrders.Count > 50)
                    {
                        _canceledOrders.RemoveAt(0);
                    }
                }

                saveOrder.NumberOfCalls++;

                if (saveOrder.NumberOfCalls >= 5)
                {
                    saveOrder.NumberOfErrors++;

                    if (saveOrder.NumberOfErrors <= 3)
                    {
                        SendLogMessage(
                        "AServer Error. You can't cancel order. There have already been five attempts to cancel order. "
                         + "NumberUser: " + order.NumberUser
                         + " NumberMarket: " + order.NumberMarket
                         , LogMessageType.Error);
                    }

                    return;
                }

                OrderAserverSender ord = new OrderAserverSender();
                ord.Order = order;
                ord.OrderSendType = OrderSendType.Cancel;

                _ordersToExecute.Enqueue(ord);

                SendLogMessage(OsLocalization.Market.Message24 + order.NumberUser, LogMessageType.System);

            }
            catch (Exception error)
            {
                SendLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        List<OrderCounter> _canceledOrders = new List<OrderCounter>();

        /// <summary>
        /// cancel all orders from trading system
        /// </summary>
        [System.Runtime.ExceptionServices.HandleProcessCorruptedStateExceptionsAttribute]
        public void CancelAllOrders()
        {
            try
            {
                if (ServerStatus == ServerConnectStatus.Disconnect)
                {
                    SendLogMessage("AServer Error. You can't cancel all orders when server status Disconnect "
                        , LogMessageType.Error);
                    return;
                }

                ServerRealization.CancelAllOrders();
            }
            catch (Exception ex)
            {
                SendLogMessage(
                    "AServer. CancelAllOrders method error: " + ex.ToString(),
                    LogMessageType.Error);
            }
        }

        /// <summary>
        /// cancel all orders from trading system to security
        /// </summary>
        [System.Runtime.ExceptionServices.HandleProcessCorruptedStateExceptionsAttribute]
        public void CancelAllOrdersToSecurity(Security security)
        {
            try
            {
                if (ServerStatus == ServerConnectStatus.Disconnect)
                {
                    SendLogMessage("AServer Error. You can't cancel orders to Security when server status Disconnect "
                    , LogMessageType.Error);
                    return;
                }

                ServerRealization.CancelAllOrdersToSecurity(security);
            }
            catch (Exception ex)
            {
                SendLogMessage(
                    "AServer. CancelAllOrdersToSecurity method error: " + ex.ToString(),
                    LogMessageType.Error);
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

        #region Orders Hub

        AServerOrdersHub _ordersHub;

        private void _ordersHub_GetAllActivOrdersOnReconnectEvent()
        {
            try
            {
                if (ServerStatus == ServerConnectStatus.Disconnect)
                {
                    return;
                }

                _serverRealization.GetAllActivOrders();
            }
            catch (Exception ex)
            {
                SendLogMessage(ex.ToString(), LogMessageType.Error);
            }
        }

        private void _ordersHub_LostOrderEvent(Order order)
        {
            string message = "ORDER LOST!!! Five times we've requested his status. There's no answer! \n";

            message += "Security: " + order.SecurityNameCode + "\n";
            message += "Class: " + order.SecurityClassCode + "\n";
            message += "NumberUser: " + order.NumberUser + "\n";
            message += "NumberMarket: " + order.NumberMarket + "\n";

            SendLogMessage(message, LogMessageType.Error);
        }

        private void _ordersHub_ActivStateOrderCheckStatusEvent(Order order)
        {
            try
            {
                if (ServerStatus == ServerConnectStatus.Disconnect)
                {
                    return;
                }

                _serverRealization.GetOrderStatus(order);
            }
            catch (Exception ex)
            {
                SendLogMessage(ex.ToString(), LogMessageType.Error);
            }
        }

        #endregion

        #region Log messages

        /// <summary>
        /// log manager
        /// </summary>
        public Log Log;

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
        /// outgoing messages for the log event
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

    public class OrderCounter
    {
        public string NumberMarket;

        public int NumberOfCalls;

        public int NumberOfErrors;

    }
}