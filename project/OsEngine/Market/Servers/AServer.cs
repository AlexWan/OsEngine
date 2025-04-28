﻿/*
 *Your rights to use the code are governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 *Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Media;
using System.Net;
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
        protected AServer()
        {
            // do nothin
        }

        protected AServer(int uniqueNumber)
        {
            this.ServerNum = uniqueNumber;
        }

        public void Delete()
        {
            try
            {
                if (File.Exists(@"Engine\" + ServerNameUnique + @"Params.txt"))
                {
                    File.Delete(@"Engine\" + ServerNameUnique + @"Params.txt");
                }

                if (File.Exists(@"Engine\" + ServerNameUnique + @"ServerSettings.txt"))
                {
                    File.Delete(@"Engine\" + ServerNameUnique + @"ServerSettings.txt");
                }

                ServerRealization.Dispose();
            }
            catch
            {
                // ignore
            }
        }

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

                _serverRealization.NewsEvent += _serverRealization_NewsEvent;

                _serverRealization.AdditionalMarketDataEvent += _serverRealization_AdditionalMarketDataEvent;

                Load();

                CreateParameterBoolean(OsLocalization.Market.ServerParam1, false);
                _needToSaveTicksParam = (ServerParameterBool)ServerParameters[ServerParameters.Count - 1];
                _needToSaveTicksParam.ValueChange += SaveTradesHistoryParam_ValueChange;
                ServerParameters[0].Comment = OsLocalization.Market.Label87;

                CreateParameterInt(OsLocalization.Market.ServerParam2, 5);
                _needToSaveTicksDaysCountParam = (ServerParameterInt)ServerParameters[ServerParameters.Count - 1];
                _needToSaveTicksDaysCountParam.ValueChange += _needToSaveTicksDaysCountParam_ValueChange;
                ServerParameters[1].Comment = OsLocalization.Market.Label88;

                CreateParameterBoolean(OsLocalization.Market.ServerParam5, true);
                _needToSaveCandlesParam = (ServerParameterBool)ServerParameters[ServerParameters.Count - 1];
                _needToSaveCandlesParam.ValueChange += SaveCandleHistoryParam_ValueChange;
                ServerParameters[2].Comment = OsLocalization.Market.Label89;

                CreateParameterInt(OsLocalization.Market.ServerParam6, 300);
                _needToSaveCandlesCountParam = (ServerParameterInt)ServerParameters[ServerParameters.Count - 1];
                _needToSaveCandlesCountParam.ValueChange += _needToSaveCandlesCountParam_ValueChange;
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

                if (ServerPermission != null
                    && ServerPermission.IsSupports_ProxyFor_MultipleInstances)
                {
                    List<string> proxyType = new List<string>();
                    proxyType.Add("None");
                    proxyType.Add("Auto");
                    proxyType.Add("Manual");
                    CreateParameterEnum(OsLocalization.Market.Label171, "None", proxyType);

                    CreateParameterString(OsLocalization.Market.Label172, "");
                }

                _serverStandardParamsCount = ServerParameters.Count;

                _serverRealization.ServerParameters = ServerParameters;

                _tickStorage = new ServerTickStorage(this);
                _tickStorage.NeedToSave = _needToSaveTicksParam.Value;
                _tickStorage.DaysToLoad = _needToSaveTicksDaysCountParam.Value;
                _tickStorage.TickLoadedEvent += _tickStorage_TickLoadedEvent;
                _tickStorage.LogMessageEvent += SendLogMessage;
                _tickStorage.LoadTick();

                _candleStorage = new ServerCandleStorage(this);
                _candleStorage.NeedToSave = _needToSaveCandlesParam.Value;
                _candleStorage.CandlesSaveCount = _needToSaveCandlesCountParam.Value;
                _candleStorage.LogMessageEvent += SendLogMessage;

                Log = new Log(this.ServerNameUnique + "Server", StartProgram.IsOsTrader);
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

                if (ServerPermission != null
                    && ServerPermission.IsSupports_CheckDataFeedLogic)
                {
                    _checkDataFlowIsOn = true;
                    Task task4 = new Task(CheckDataFlowThread);
                    task4.Start();
                }

                _serverIsCreated = true;

                _ordersHub = new AServerOrdersHub(this);
                _ordersHub.LogMessageEvent += SendLogMessage;
                _ordersHub.GetAllActiveOrdersOnReconnectEvent += _ordersHub_GetAllActiveOrdersOnReconnectEvent;
                _ordersHub.ActiveStateOrderCheckStatusEvent += _ordersHub_ActiveStateOrderCheckStatusEvent;
                _ordersHub.LostOrderEvent += _ordersHub_LostOrderEvent;
                _ordersHub.LostMyTradesEvent += _ordersHub_LostMyTradesEvent;

                ComparePositionsModule = new ComparePositionsModule(this);
                ComparePositionsModule.LogMessageEvent += SendLogMessage;
            }
            get { return _serverRealization; }
        }

        private IServerRealization _serverRealization;

        #endregion

        #region GUI

        /// <summary>
        /// show settings window
        /// </summary>
        public void ShowDialog(int num = 0)
        {
            if (_ui == null)
            {
                List<AServer> allServersThisType = new List<AServer>();

                List<IServer> serversFromServerMaster = ServerMaster.GetServers();

                for (int i = 0; i < serversFromServerMaster.Count; i++)
                {
                    if (serversFromServerMaster[i].ServerType == this.ServerType)
                    {
                        allServersThisType.Add((AServer)serversFromServerMaster[i]);
                    }
                }

                _ui = new AServerParameterUi(allServersThisType, num);
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
        private ServerParameterBool _needToSaveTicksParam;

        /// <summary>
        /// parameter with the number of days for saving ticks
        /// </summary>
        private ServerParameterInt _needToSaveTicksDaysCountParam;

        /// <summary>
        /// whether candles should be saved to the file system
        /// </summary>
        private ServerParameterBool _needToSaveCandlesParam;

        /// <summary>
        /// number of candles for which trades should be loaded at the start of the connector
        /// </summary>
        public ServerParameterInt _needToSaveCandlesCountParam;

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
        public bool NeedToHideParameters = false;

        public bool CanDoMultipleConnections
        {
            get
            {
                IServerPermission permission = ServerPermission;

                if (permission != null)
                {
                    return permission.IsSupports_MultipleInstances;
                }

                return false;
            }
        }

        /// <summary>
        /// server parameters
        /// </summary>
        public List<IServerParameter> ServerParameters = new List<IServerParameter>();

        private int _serverStandardParamsCount = 12;

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
                ServerParameters.Insert(ServerParameters.Count - _serverStandardParamsCount, newParam);
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
                ServerParameters.Insert(ServerParameters.Count - _serverStandardParamsCount, newParam);
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
                ServerParameters.Insert(ServerParameters.Count - _serverStandardParamsCount, newParam);
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
                ServerParameters.Insert(ServerParameters.Count - _serverStandardParamsCount, newParam);
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
                ServerParameters.Insert(ServerParameters.Count - _serverStandardParamsCount, newParam);
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
                ServerParameters.Insert(ServerParameters.Count - _serverStandardParamsCount, newParam);
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
                ServerParameters.Insert(ServerParameters.Count - _serverStandardParamsCount, newParam);
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
                ServerParameters.Insert(ServerParameters.Count - _serverStandardParamsCount, newParam);
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
                using (StreamWriter writer = new StreamWriter(@"Engine\" + ServerNameUnique + @"Params.txt", false)
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


            if (!File.Exists(@"Engine\" + ServerNameUnique + @"Params.txt"))
            {
                return param;
            }
            try
            {
                using (StreamReader reader = new StreamReader(@"Engine\" + ServerNameUnique + @"Params.txt"))
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
        private void _needToSaveCandlesCountParam_ValueChange()
        {
            _candleStorage.CandlesSaveCount = _needToSaveCandlesCountParam.Value;
        }

        /// <summary>
        /// user has changed the value of the parameter
        /// </summary>
        private void _needToSaveTicksDaysCountParam_ValueChange()
        {
            if (_tickStorage != null)
            {
                _tickStorage.DaysToLoad = _needToSaveTicksDaysCountParam.Value;
            }
        }

        /// <summary>
        /// user has changed the value of the parameter
        /// </summary>
        private void SaveTradesHistoryParam_ValueChange()
        {
            if (_tickStorage != null)
            {
                _tickStorage.NeedToSave = _needToSaveTicksParam.Value;
            }
        }

        /// <summary>
        /// user has changed the value of the parameter
        /// </summary>
        private void SaveCandleHistoryParam_ValueChange()
        {
            if (_candleStorage != null)
            {
                _candleStorage.NeedToSave = _needToSaveCandlesParam.Value;
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
            if (UserWantsConnect != null)
            {
                UserWantsConnect();
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
            if (UserWantsDisconnect != null)
            {
                UserWantsDisconnect();
            }
            _serverStatusNeed = ServerConnectStatus.Disconnect;
        }

        /// <summary>
        /// user requested connect to the API
        /// </summary>
        public event Action UserWantsConnect;

        /// <summary>
        /// user requested disconnect from the API
        /// </summary>
        public event Action UserWantsDisconnect;

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
                    SendLogMessage(_serverConnectStatus + " " + OsLocalization.Market.Message7, LogMessageType.Connect);
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

        public int ServerNum;

        public string ServerPrefix
        {
            get
            {
                return _serverPrefix;
            }
            set
            {
                if (value == _serverPrefix)
                {
                    return;
                }

                _serverPrefix = value;
                Save();
            }
        }
        private string _serverPrefix;

        public string ServerNameUnique
        {
            get
            {
                string result = ServerType.ToString();

                if (ServerNum == 0)
                {
                    return result;
                }

                result = result + "_" + ServerNum;

                return result;
            }
        }

        public string ServerNameAndPrefix
        {
            get
            {
                if (ServerNum == 0
                    || string.IsNullOrEmpty(ServerPrefix))
                {
                    return ServerNameUnique;
                }

                string result = ServerNameUnique + "_" + ServerPrefix;

                return result;
            }
        }

        /// <summary>
        /// upload settings
        /// </summary>
        private void Load()
        {
            if (!File.Exists(@"Engine\" + ServerNameUnique + @"ServerSettings.txt"))
            {
                return;
            }
            try
            {
                using (StreamReader reader = new StreamReader(@"Engine\" + ServerNameUnique + @"ServerSettings.txt"))
                {
                    _serverPrefix = reader.ReadLine();

                    reader.Close();
                }
            }
            catch
            {
                // ignore
            }
        }

        /// <summary>
        /// save settings in file
        /// </summary>
        public void Save()
        {
            try
            {
                using (StreamWriter writer = new StreamWriter(@"Engine\" + ServerNameUnique + @"ServerSettings.txt", false))
                {
                    writer.WriteLine(_serverPrefix);
                    writer.Close();
                }
            }
            catch
            {
                // ignore
            }
        }

        /// <summary>
        /// server realization permissions
        /// </summary>
        public IServerPermission ServerPermission
        {
            get
            {
                if (this.ServerType == ServerType.None)
                {
                    return null;
                }

                return ServerMaster.GetServerPermission(this.ServerType);

            }
        }

        private bool _checkDataFlowIsOn;

        /// <summary>
        /// alert message from client that connection is established
        /// </summary>
        private void _serverRealization_Connected()
        {
            SendLogMessage(OsLocalization.Market.Message6, LogMessageType.System);
            ServerStatus = ServerConnectStatus.Connect;
        }

        /// <summary>
        /// client connection has broken
        /// </summary>
        private void _serverRealization_Disconnected()
        {
            if (ServerStatus == ServerConnectStatus.Disconnect)
            {
                return;
            }
            SendLogMessage(OsLocalization.Market.Message12, LogMessageType.System);
            ServerStatus = ServerConnectStatus.Disconnect;

            if (_serverRealization.ServerStatus != ServerConnectStatus.Disconnect)
            {
                _serverRealization.ServerStatus = ServerConnectStatus.Disconnect;
            }

            if (NeedToReconnectEvent != null)
            {
                NeedToReconnectEvent();
            }
        }

        /// <summary>
        /// connection state has changed
        /// </summary>
        public event Action<string> ConnectStatusChangeEvent;

        /// <summary>
        /// need to reconnect server and get a new data
        /// </summary>
        public event Action NeedToReconnectEvent;

        #endregion

        #region Proxy

        private WebProxy GetProxy()
        {

            return null;
        }


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
                        _subscribeSecurities.Clear();

                        if (Portfolios != null &&
                            Portfolios.Count != 0)
                        {
                            Portfolios.Clear();
                        }

                        DeleteCandleManager();

                        if(ServerPermission != null 
                            && ServerPermission.IsSupports_ProxyFor_MultipleInstances)
                        {
                            WebProxy proxy = GetProxy();

                            ServerRealization.Connect(proxy);
                        }
                        else
                        {
                            ServerRealization.Connect(null);
                        }

                        LastStartServerTime = DateTime.Now;

                        NeedToReconnectEvent?.Invoke();

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
                    catch (Exception ex)
                    {
                        SendLogMessage(ex.ToString(), LogMessageType.Error);
                    }

                    DeleteCandleManager();

                    Thread.Sleep(5000);
                    // reconnect / переподключаемся

                    Task task = new Task(PrimeThreadArea);
                    task.Start();

                    if (NeedToReconnectEvent != null)
                    {
                        NeedToReconnectEvent();
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
                            if (TestValue_CanSendOrdersUp
                                && TestValue_CanSendMyTradesUp)
                            {
                                if (NewMyTradeEvent != null)
                                {
                                    NewMyTradeEvent(myTrade);
                                }

                                _ordersHub.SetMyTradeFromApi(myTrade);

                                bool isInArray = false;

                                for (int i = 0; i < _myTrades.Count; i++)
                                {
                                    if (_myTrades[i].NumberTrade == myTrade.NumberTrade)
                                    {
                                        isInArray = true;
                                        break;
                                    }
                                }

                                if (isInArray == false)
                                {
                                    _myTrades.Add(myTrade);
                                }

                                while (_myTrades.Count > 1000)
                                {
                                    _myTrades.RemoveAt(0);
                                }

                                _needToBeepOnTrade = true;
                            }
                        }
                    }
                    else if (!_tradesToSend.IsEmpty)
                    {
                        List<Trade> trades;

                        if (_tradesToSend.TryDequeue(out trades))
                        {// разбираем всю очередь. Отправляем массивы для каждого инструмента один раз
                            List<List<Trade>> list = new List<List<Trade>>();
                            list.Add(trades);

                            while (_tradesToSend.Count != 0)
                            {
                                List<Trade> newTrades = null;

                                if (_tradesToSend.TryDequeue(out newTrades))
                                {
                                    bool isInArray = false;

                                    for (int i = 0; i < list.Count; i++)
                                    {
                                        if (list[i][0].SecurityNameCode == newTrades[0].SecurityNameCode)
                                        {
                                            list[i] = newTrades;
                                            isInArray = true;
                                        }
                                    }

                                    if (isInArray == false)
                                    {
                                        list.Add(newTrades);
                                    }
                                }
                            }

                            for (int i = 0; i < list.Count; i++)
                            {
                                if (_checkDataFlowIsOn)
                                {
                                    SecurityFlowTime tradeTime = new SecurityFlowTime();
                                    tradeTime.SecurityName = list[i][0].SecurityNameCode;
                                    tradeTime.LastTimeTrade = DateTime.Now;
                                    _securitiesFeedFlow.Enqueue(tradeTime);
                                }

                                if (NewTradeEvent != null)
                                {
                                    NewTradeEvent(list[i]);
                                }
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
                            if (_marketDepthsToSend.Count < 1000)
                            {
                                if (NewMarketDepthEvent != null)
                                {
                                    NewMarketDepthEvent(depth);
                                }

                                if (_checkDataFlowIsOn)
                                {
                                    SecurityFlowTime tradeTime = new SecurityFlowTime();
                                    tradeTime.SecurityName = depth.SecurityNameCode;
                                    tradeTime.LastTimeMarketDepth = DateTime.Now;
                                    _securitiesFeedFlow.Enqueue(tradeTime);
                                }
                            }
                            else
                            {
                                // Копится очередь. ЦП не справляется
                                // Отсылаем на верх по последнему стакану для каждого инструмента
                                // Промежуточные срезы - игнорируем

                                List<MarketDepth> list = new List<MarketDepth>();

                                list.Add(depth);

                                while (_marketDepthsToSend.Count != 0)
                                {
                                    MarketDepth newDepth = null;

                                    if (_marketDepthsToSend.TryDequeue(out newDepth))
                                    {
                                        bool isInArray = false;

                                        for (int i = 0; i < list.Count; i++)
                                        {
                                            if (list[i].SecurityNameCode == newDepth.SecurityNameCode)
                                            {
                                                list[i] = newDepth;
                                                isInArray = true;
                                            }
                                        }

                                        if (isInArray == false)
                                        {
                                            list.Add(newDepth);
                                        }
                                    }
                                }

                                for (int i = 0; i < list.Count; i++)
                                {
                                    if (_checkDataFlowIsOn)
                                    {
                                        SecurityFlowTime tradeTime = new SecurityFlowTime();
                                        tradeTime.SecurityName = list[i].SecurityNameCode;
                                        tradeTime.LastTimeMarketDepth = DateTime.Now;
                                        _securitiesFeedFlow.Enqueue(tradeTime);
                                    }

                                    if (NewMarketDepthEvent != null)
                                    {
                                        NewMarketDepthEvent(list[i]);
                                    }
                                }
                            }
                        }
                    }

                    else if (!_bidAskToSend.IsEmpty)
                    {
                        BidAskSender bidAsk;

                        if (_bidAskToSend.TryDequeue(out bidAsk))
                        {
                            if (_bidAskToSend.Count < 1000)
                            {
                                if (NewBidAscIncomeEvent != null)
                                {
                                    NewBidAscIncomeEvent(bidAsk.Bid, bidAsk.Ask, bidAsk.Security);
                                }
                            }
                            else
                            {   // Копится очередь. ЦП не справляется
                                // Отсылаем на верх по последнему bid/Ask для каждого инструмента
                                // Промежуточные срезы - игнорируем

                                List<BidAskSender> list = new List<BidAskSender>();
                                list.Add(bidAsk);

                                while (_bidAskToSend.Count != 0)
                                {
                                    BidAskSender newBidAsk = null;

                                    if (_bidAskToSend.TryDequeue(out newBidAsk))
                                    {
                                        bool isInArray = false;

                                        for (int i = 0; i < list.Count; i++)
                                        {
                                            if (list[i].Security.Name == newBidAsk.Security.Name)
                                            {
                                                list[i] = newBidAsk;
                                                isInArray = true;
                                            }
                                        }

                                        if (isInArray == false)
                                        {
                                            list.Add(newBidAsk);
                                        }
                                    }
                                }

                                for (int i = 0; i < list.Count; i++)
                                {
                                    if (NewBidAscIncomeEvent != null)
                                    {
                                        NewBidAscIncomeEvent(list[i].Bid, list[i].Ask, list[i].Security);
                                    }
                                }
                            }
                        }
                    }

                    else if (!_newsToSend.IsEmpty)
                    {
                        News news;

                        if (_newsToSend.TryDequeue(out news))
                        {
                            if (NewsEvent != null)
                            {
                                NewsEvent(news);
                            }
                        }
                    }
                    else if (!_additionalMarketDataToSend.IsEmpty)
                    {
                        OptionMarketDataForConnector data;

                        if (_additionalMarketDataToSend.TryDequeue(out data))
                        {
                            ConvertableMarketData(data);
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

        public bool TestValue_CanSendMyTradesUp = true;

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

        /// <summary>
        /// queue for new news
        /// </summary>
        private ConcurrentQueue<News> _newsToSend = new ConcurrentQueue<News>();

        /// <summary>
        /// queue for Additional Market Data
        /// </summary>
        private ConcurrentQueue<OptionMarketDataForConnector> _additionalMarketDataToSend = new ConcurrentQueue<OptionMarketDataForConnector>();

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
                    if (portf[i].ServerType == ServerType.None)
                    {
                        portf[i].ServerType = this.ServerType;
                    }

                    if (string.IsNullOrEmpty(portf[i].ServerUniqueName))
                    {
                        portf[i].ServerUniqueName = this.ServerNameAndPrefix;
                    }

                    if(portf[i].ServerUniqueName != this.ServerNameAndPrefix)
                    {
                        portf[i].ServerUniqueName = this.ServerNameAndPrefix;
                    }

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

                    curPortfolio.UnrealizedPnl = portf[i].UnrealizedPnl;
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

            if (string.IsNullOrEmpty(securityClass) == false)
            {
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
        private void _serverRealization_SecurityEvent(List<Security> securities)
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
                            s.Name == securities[i].Name &&
                            s.NameClass == securities[i].NameClass) == null)
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

        private SecuritiesUi _securitiesUi;

        private void AServer_UserClickButton()
        {
            if (_securitiesUi == null)
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
                            securities[j].MinTradeAmountType = curSaveSec.MinTradeAmountType;
                            securities[j].VolumeStep = curSaveSec.VolumeStep;
                            securities[j].PriceLimitHigh = curSaveSec.PriceLimitHigh;
                            securities[j].PriceLimitLow = curSaveSec.PriceLimitLow;
                            securities[j].Go = curSaveSec.Go;
                            securities[j].Strike = curSaveSec.Strike;

                            break;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                SendLogMessage(ex.ToString(), LogMessageType.Error);
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
            catch (Exception ex)
            {
                SendLogMessage(ex.ToString(), LogMessageType.Error);
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

                    SetSecurityInSubscribed(securityName, securityClass);

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

                if (_needToSaveCandlesParam.Value == true)
                {
                    List<Candle> candles = _candleStorage.GetCandles(series.Specification, _needToSaveCandlesCountParam.Value);
                    series.CandlesAll = series.CandlesAll.Merge(candles);
                }
            }

            if (series.IsMergedByTradesFromFile == false)
            {
                series.IsMergedByTradesFromFile = true;

                if (_needToSaveTicksParam.Value == true
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
                && series.CandlesAll.Count > _needToSaveCandlesCountParam.Value
                && _serverTime.Minute % 15 == 0
                && _serverTime.Second == 0
            )
            {
                series.CandlesAll.RemoveRange(0, series.CandlesAll.Count - 1 - _needToSaveCandlesCountParam.Value);
            }

            _candleSeriesToSend.Enqueue(series);
        }

        private void _serverRealization_NewsEvent(News news)
        {
            _newsToSend.Enqueue(news);
        }

        private string _lockerStartNews = "lockerStartNews";

        /// <summary>
        /// subscribe to news
        /// </summary>
        public bool SubscribeNews()
        {
            lock (_lockerStartNews)
            {
                try
                {
                    if (Portfolios == null || Securities == null)
                    {
                        return false;
                    }

                    if (LastStartServerTime != DateTime.MinValue &&
                        LastStartServerTime.AddSeconds(10) > DateTime.Now)
                    {
                        return false;
                    }

                    if (ServerStatus != ServerConnectStatus.Connect)
                    {
                        return false;
                    }

                    IServerPermission permission = ServerMaster.GetServerPermission(this.ServerType);

                    if (permission == null
                        || permission.IsNewsServer == false)
                    {
                        SendLogMessage(ServerType + " Aserver. News Subscribe method error. No permission on News in Server", LogMessageType.Error);
                        return true;
                    }

                    return _serverRealization.SubscribeNews();
                }
                catch (Exception ex)
                {
                    SendLogMessage("Aserver. News Subscribe method error: " + ex.ToString(), LogMessageType.Error);
                }
                return false;
            }
        }

        /// <summary>
        /// the news has come out
        /// </summary>
        public event Action<News> NewsEvent;

        /// <summary>
        /// new candles event
        /// </summary>
        public event Action<CandleSeries> NewCandleIncomeEvent;

        #endregion

        #region Checking data streams subscribed to

        private List<SecurityFlowTime> _subscribeSecurities = new List<SecurityFlowTime>();

        private ConcurrentQueue<SecurityFlowTime> _securitiesFeedFlow = new ConcurrentQueue<SecurityFlowTime>();

        private void SetSecurityInSubscribed(string securityName, string securityClass)
        {
            if (_checkDataFlowIsOn == false)
            {
                return;
            }

            string[] ignoreClasses = ServerPermission.CheckDataFeedLogic_ExceptionSecuritiesClass;

            if (ignoreClasses != null)
            {
                for (int i = 0; i < ignoreClasses.Length; i++)
                {
                    if (ignoreClasses[i].Equals(securityClass))
                    {
                        return;
                    }
                }
            }

            for (int i = 0; i < _subscribeSecurities.Count; i++)
            {
                if (_subscribeSecurities[i].SecurityName == securityName
                    && _subscribeSecurities[i].SecurityClass == securityClass)
                {
                    return;
                }
            }

            SecurityFlowTime newSubscribeSecurity = new SecurityFlowTime();

            newSubscribeSecurity.SecurityName = securityName;
            newSubscribeSecurity.SecurityClass = securityClass;

            _subscribeSecurities.Add(newSubscribeSecurity);
        }

        private void CheckDataFlowThread()
        {
            while (true)
            {
                try
                {
                    Thread.Sleep(3000);

                    if (MainWindow.ProccesIsWorked == false)
                    {
                        return;
                    }

                    if (this.ServerStatus != ServerConnectStatus.Connect)
                    {
                        continue;
                    }

                    // 1 разбираем очередь с обновлением данных с сервера

                    while (_securitiesFeedFlow.Count > 0)
                    {
                        SecurityFlowTime securityFlowTime = null;

                        if (_securitiesFeedFlow.TryDequeue(out securityFlowTime))
                        {
                            if (securityFlowTime.LastTimeMarketDepth != DateTime.MinValue)
                            {// пришло обновление стакана

                                for (int i = 0; i < _subscribeSecurities.Count; i++)
                                {
                                    if (_subscribeSecurities[i].SecurityName == securityFlowTime.SecurityName)
                                    {
                                        if (securityFlowTime.LastTimeMarketDepth > _subscribeSecurities[i].LastTimeMarketDepth)
                                        {
                                            _subscribeSecurities[i].LastTimeMarketDepth = securityFlowTime.LastTimeMarketDepth;
                                            break;
                                        }
                                    }
                                }
                            }
                            else if (securityFlowTime.LastTimeTrade != DateTime.MinValue)
                            {// пришло обновление в ленте сделок

                                for (int i = 0; i < _subscribeSecurities.Count; i++)
                                {
                                    if (_subscribeSecurities[i].SecurityName == securityFlowTime.SecurityName)
                                    {
                                        if (securityFlowTime.LastTimeTrade > _subscribeSecurities[i].LastTimeTrade)
                                        {
                                            _subscribeSecurities[i].LastTimeTrade = securityFlowTime.LastTimeTrade;
                                            break;
                                        }
                                    }
                                }
                            }
                        }
                    }

                    // 2 смотрим, есть ли отставание по какой-то бумаге

                    SecurityFlowTime maxDataDelayMarketDepth = null;
                    SecurityFlowTime maxDataDelayTrade = null;

                    for (int i = 0; i < _subscribeSecurities.Count; i++)
                    {
                        if (_subscribeSecurities[i].LastTimeTrade != DateTime.MinValue)
                        {
                            if (maxDataDelayTrade == null ||
                                maxDataDelayTrade.LastTimeTrade > _subscribeSecurities[i].LastTimeTrade)
                            {
                                maxDataDelayTrade = _subscribeSecurities[i];
                            }
                        }

                        if (_subscribeSecurities[i].LastTimeMarketDepth != DateTime.MinValue)
                        {
                            if (maxDataDelayMarketDepth == null ||
                                maxDataDelayMarketDepth.LastTimeMarketDepth > _subscribeSecurities[i].LastTimeMarketDepth)
                            {
                                maxDataDelayMarketDepth = _subscribeSecurities[i];
                            }
                        }
                    }

                    // 3 смотрим, не пора ли перезапускать коннектор

                    bool needToReconnect = false;

                    if (maxDataDelayMarketDepth != null
                        && maxDataDelayMarketDepth.LastTimeMarketDepth.AddMinutes(ServerPermission.CheckDataFeedLogic_NoDataMinutesToDisconnect)
                        < DateTime.Now)
                    { // перезагружаем т.к. нет стаканов уже N минут
                        string messageToLog = "ERROR data feed. No MarketDepth. CheckDataFlowThread in Aserver. \n";
                        messageToLog += "Connector: " + this.ServerType + "\n";
                        messageToLog += "Security: " + maxDataDelayMarketDepth.SecurityName + "\n";
                        messageToLog += "No data time: " + (DateTime.Now - maxDataDelayMarketDepth.LastTimeMarketDepth).ToString() + "\n";
                        messageToLog += "Reconnect activated";
                        SendLogMessage(messageToLog, LogMessageType.Error);
                        needToReconnect = true;
                    }
                    if (maxDataDelayTrade != null
                        && maxDataDelayTrade.LastTimeTrade.AddMinutes(ServerPermission.CheckDataFeedLogic_NoDataMinutesToDisconnect * 3)
                        < DateTime.Now)
                    { // перезагружаем т.к. нет трейдов уже N минут

                        string messageToLog = "ERROR data feed. No Trades. CheckDataFlowThread in Aserver. \n";
                        messageToLog += "Connector: " + this.ServerType + "\n";
                        messageToLog += "Security: " + maxDataDelayTrade.SecurityName + "\n";
                        messageToLog += "No data time: " + (DateTime.Now - maxDataDelayTrade.LastTimeTrade).ToString() + "\n";
                        messageToLog += "Reconnect activated";
                        SendLogMessage(messageToLog, LogMessageType.Error);
                        needToReconnect = true;
                    }

                    if (needToReconnect)
                    {
                        _serverRealization_Disconnected();
                    }
                }
                catch (Exception ex)
                {
                    SendLogMessage(ex.ToString(), LogMessageType.Error);
                    Thread.Sleep(15000);
                }
            }
        }

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
            DateTime startTime, DateTime endTime, DateTime actualTime, bool needToUpdate)
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
        public List<Trade> GetTickDataToSecurity(string securityName, string securityClass, DateTime startTime, DateTime endTime, DateTime actualTime, bool needToUpdete)
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
        private void _serverRealization_MarketDepthEvent(MarketDepth myDepth)
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
                     (myDepth.Bids == null ||
                    myDepth.Bids.Count == 0))
                {
                    return;
                }

                if (myDepth.SecurityNameCode == "LQDT")
                {

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
            if (newMarketDepth.Bids != null &&
                newMarketDepth.Bids.Count > 0)
            {
                bestBid = newMarketDepth.Bids[0].Price;
            }

            decimal bestAsk = 0;
            if (newMarketDepth.Asks != null &&
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
        private void _tickStorage_TickLoadedEvent(List<Trade>[] trades)
        {
            _allTrades = trades;
        }

        /// <summary>
        /// new trade event from ServerRealization
        /// </summary>
        private void ServerRealization_NewTradesEvent(Trade trade)
        {
            try
            {
                if (trade == null)
                {
                    return;
                }

                if (trade.Price == 0)
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

            if (depth.Asks != null &&
                depth.Asks.Count > 0)
            {
                trade.Ask = depth.Asks[0].Price;
            }

            if (depth.Bids != null &&
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
        private bool _needToBeepOnTrade;

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

                if (_needToBeepOnTrade == false)
                {
                    continue;
                }

                if (PrimeSettings.PrimeSettingsMaster.TransactionBeepIsActive == false)
                {
                    continue;
                }

                _needToBeepOnTrade = false;
                SystemSounds.Asterisk.Play();
            }
        }

        /// <summary>
        /// my trades incoming from IServerRealization
        /// </summary>
        private void _serverRealization_MyTradeEvent(MyTrade trade)
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
                try
                {
                    if (LastStartServerTime.AddSeconds(WaitTimeToTradeAfterFirstStart) > DateTime.Now)
                    {
                        await Task.Delay(1000);
                        continue;
                    }

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
                if(string.IsNullOrEmpty(order.ServerName))
                {
                    order.ServerName = this.ServerNameAndPrefix;
                }

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
        private void _serverRealization_MyOrderEvent(Order myOrder)
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

        private void _ordersHub_GetAllActiveOrdersOnReconnectEvent()
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

        private void _ordersHub_LostMyTradesEvent(Order order)
        {
            string message = "MYTRADES LOST!!! Five times we've requested his status. There's no answer! \n";

            message += "Security: " + order.SecurityNameCode + "\n";
            message += "Class: " + order.SecurityClassCode + "\n";
            message += "NumberUser: " + order.NumberUser + "\n";
            message += "NumberMarket: " + order.NumberMarket + "\n";
            message += "If you are trading on the cryptocurrency spot market, ignore message. That's because MyTrades doesn't have the same volume after commission deduction.";

            SendLogMessage(message, LogMessageType.System);
        }

        private void _ordersHub_ActiveStateOrderCheckStatusEvent(Order order)
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

        #region Compare positions module

        public ComparePositionsModule ComparePositionsModule;

        public void ShowComparePositionsModuleDialog(string portfolioName)
        {
            ComparePositionsModuleUi myUi = null;

            for (int i = 0; i < _comparePositionsModuleUi.Count; i++)
            {
                if (_comparePositionsModuleUi[i].PortfolioName == portfolioName)
                {
                    myUi = _comparePositionsModuleUi[i];
                    break;
                }
            }

            if (myUi == null)
            {
                myUi = new ComparePositionsModuleUi(ComparePositionsModule, portfolioName);
                myUi.GuiClosed += MyUi_GuiClosed;
                _comparePositionsModuleUi.Add(myUi);
                myUi.Show();
            }
            else
            {
                myUi.Activate();
            }
        }

        private void MyUi_GuiClosed(string portfolioName)
        {
            for (int i = 0; i < _comparePositionsModuleUi.Count; i++)
            {
                if (_comparePositionsModuleUi[i].PortfolioName == portfolioName)
                {
                    _comparePositionsModuleUi[i].GuiClosed -= MyUi_GuiClosed;
                    _comparePositionsModuleUi.RemoveAt(i);
                    break;
                }
            }
        }

        private List<ComparePositionsModuleUi> _comparePositionsModuleUi = new List<ComparePositionsModuleUi>();

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
            if (CanDoMultipleConnections)
            {
                message = this.ServerNameUnique + " " + message;
            }

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

        #region Additional Market Data

        private void _serverRealization_AdditionalMarketDataEvent(OptionMarketDataForConnector obj)
        {
            try
            {
                if (obj == null)
                {
                    return;
                }

                if (string.IsNullOrEmpty(obj.SecurityName))
                {
                    return;
                }

                _additionalMarketDataToSend.Enqueue(obj);
            }
            catch (Exception ex)
            {
                SendLogMessage(ex.ToString(), LogMessageType.Error);
            }
        }

        private Dictionary<string, OptionMarketData> _dictAdditionalMarketData = new Dictionary<string, OptionMarketData>();

        private void ConvertableMarketData(OptionMarketDataForConnector data)
        {
            try
            {
                if (string.IsNullOrEmpty(data.SecurityName))
                {
                    return;
                }

                if (!_dictAdditionalMarketData.ContainsKey(data.SecurityName))
                {
                    OptionMarketData additionalMarketData = new OptionMarketData();
                    _dictAdditionalMarketData.Add(data.SecurityName, additionalMarketData);
                }

                if (!string.IsNullOrEmpty(data.SecurityName) &&
                    _dictAdditionalMarketData[data.SecurityName].SecurityName != data.SecurityName)
                {
                    _dictAdditionalMarketData[data.SecurityName].SecurityName = data.SecurityName;
                }
                if (!string.IsNullOrEmpty(data.UnderlyingAsset) &&
                    _dictAdditionalMarketData[data.SecurityName].UnderlyingAsset != data.UnderlyingAsset)
                {
                    _dictAdditionalMarketData[data.SecurityName].UnderlyingAsset = data.UnderlyingAsset;
                }
                if (!string.IsNullOrEmpty(data.UnderlyingPrice) &&
                    _dictAdditionalMarketData[data.SecurityName].UnderlyingPrice.ToString() != data.UnderlyingPrice)
                {
                    _dictAdditionalMarketData[data.SecurityName].UnderlyingPrice = data.UnderlyingPrice.ToDecimal();
                }
                if (!string.IsNullOrEmpty(data.MarkPrice) &&
                    _dictAdditionalMarketData[data.SecurityName].MarkPrice.ToString() != data.MarkPrice)
                {
                    _dictAdditionalMarketData[data.SecurityName].MarkPrice = data.MarkPrice.ToDecimal();
                }
                if (!string.IsNullOrEmpty(data.MarkIV) &&
                    _dictAdditionalMarketData[data.SecurityName].MarkIV.ToString() != data.MarkIV)
                {
                    _dictAdditionalMarketData[data.SecurityName].MarkIV = data.MarkIV.ToDecimal();
                }
                if (!string.IsNullOrEmpty(data.BidIV) &&
                    _dictAdditionalMarketData[data.SecurityName].BidIV.ToString() != data.BidIV)
                {
                    _dictAdditionalMarketData[data.SecurityName].BidIV = data.BidIV.ToDecimal();
                }
                if (!string.IsNullOrEmpty(data.AskIV) &&
                    _dictAdditionalMarketData[data.SecurityName].AskIV.ToString() != data.AskIV)
                {
                    _dictAdditionalMarketData[data.SecurityName].AskIV = data.AskIV.ToDecimal();
                }
                if (!string.IsNullOrEmpty(data.Delta) &&
                    _dictAdditionalMarketData[data.SecurityName].Delta.ToString() != data.Delta)
                {
                    _dictAdditionalMarketData[data.SecurityName].Delta = data.Delta.ToDecimal();
                }
                if (!string.IsNullOrEmpty(data.Gamma) &&
                    _dictAdditionalMarketData[data.SecurityName].Gamma.ToString() != data.Gamma)
                {
                    _dictAdditionalMarketData[data.SecurityName].Gamma = data.Gamma.ToDecimal();
                }
                if (!string.IsNullOrEmpty(data.Vega) &&
                    _dictAdditionalMarketData[data.SecurityName].Vega.ToString() != data.Vega)
                {
                    _dictAdditionalMarketData[data.SecurityName].Vega = data.Vega.ToDecimal();
                }
                if (!string.IsNullOrEmpty(data.Theta) &&
                    _dictAdditionalMarketData[data.SecurityName].Theta.ToString() != data.Theta)
                {
                    _dictAdditionalMarketData[data.SecurityName].Theta = data.Theta.ToDecimal();
                }
                if (!string.IsNullOrEmpty(data.Rho) &&
                    _dictAdditionalMarketData[data.SecurityName].Rho.ToString() != data.Rho)
                {
                    _dictAdditionalMarketData[data.SecurityName].Rho = data.Rho.ToDecimal();
                }
                if (!string.IsNullOrEmpty(data.OpenInterest) &&
                    _dictAdditionalMarketData[data.SecurityName].OpenInterest.ToString() != data.OpenInterest)
                {
                    _dictAdditionalMarketData[data.SecurityName].OpenInterest = data.OpenInterest.ToDecimal();
                }
                if (!string.IsNullOrEmpty(data.TimeCreate) &&
                    _dictAdditionalMarketData[data.SecurityName].TimeCreate.ToString() != data.TimeCreate)
                {
                    _dictAdditionalMarketData[data.SecurityName].TimeCreate = TimeManager.GetDateTimeFromTimeStamp(Convert.ToInt64(data.TimeCreate));
                }

                if (NewAdditionalMarketDataEvent != null)
                {
                    NewAdditionalMarketDataEvent(_dictAdditionalMarketData[data.SecurityName]);
                }
            }
            catch (Exception ex)
            {
                SendLogMessage(ex.ToString(), LogMessageType.Error);
                Thread.Sleep(5000);
            }
        }

        /// <summary>
        /// new Additional Market Data
        /// </summary>
        public event Action<OptionMarketData> NewAdditionalMarketDataEvent;

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

    public class SecurityFlowTime
    {
        public string SecurityName;

        public string SecurityClass;

        public DateTime LastTimeTrade;

        public DateTime LastTimeMarketDepth;
    }

}