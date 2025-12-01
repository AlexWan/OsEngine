/*
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
using OsEngine.OsTrader.SystemAnalyze;
using System.Net.Sockets;
using System.Text;

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

                if (File.Exists(@"Engine\" + ServerNameUnique + @"nonTradePeriod.txt"))
                {
                    File.Delete(@"Engine\" + ServerNameUnique + @"nonTradePeriod.txt");
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
                _serverRealization.ForceCheckOrdersAfterReconnectEvent += _serverRealization_ForceCheckOrdersAfterReconnect;

                _serverRealization.NewsEvent += _serverRealization_NewsEvent;

                _serverRealization.AdditionalMarketDataEvent += _serverRealization_AdditionalMarketDataEvent;
                _serverRealization.FundingUpdateEvent += _serverRealization_FundingUpdateEvent;
                _serverRealization.Volume24hUpdateEvent += _serverRealization_Volume24hUpdateEvent;

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

                CreateParameterInt(OsLocalization.Market.ServerParam6, 900);
                _needToLoadCandlesCountParam = (ServerParameterInt)ServerParameters[ServerParameters.Count - 1];
                _needToLoadCandlesCountParam.ValueChange += _needToLoadCandlesCountParam_ValueChange;
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
                ((ServerParameterButton)ServerParameters[9]).UserClickButton += AServer_UserClickSecuritiesUiButton;

                CreateParameterButton(OsLocalization.Market.ServerParam14);
                ServerParameters[10].Comment = OsLocalization.Market.Label281;
                ((ServerParameterButton)ServerParameters[10]).UserClickButton += AServer_UserClickNonTradePeriodsUiButton;

                if (ServerPermission != null
                    && ServerPermission.IsSupports_ProxyFor_MultipleInstances)
                {
                    List<string> proxyType = new List<string>();
                    proxyType.Add("None");
                    proxyType.Add("Auto");
                    proxyType.Add("Manual");
                    CreateParameterEnum(OsLocalization.Market.Label171, "None", proxyType);
                    ServerParameters[ServerParameters.Count - 1].Comment = OsLocalization.Market.Label191;

                    CreateParameterString(OsLocalization.Market.Label172, "");
                    ServerParameters[ServerParameters.Count - 1].Comment = OsLocalization.Market.Label192;
                }

                if (ServerPermission != null
                    && ServerPermission.IsSupports_AsyncOrderSending)
                {
                    _asyncOrdersSender
                        = new AServerAsyncOrderSender(ServerPermission.AsyncOrderSending_RateGateLimitMls);
                    _asyncOrdersSender.ExecuteOrderInRealizationEvent += ExecuteOrderInRealization;
                }

                if (ServerPermission != null
                    && ServerPermission.IsSupports_CheckDataFeedLogic)
                {
                    Task task4 = new Task(CheckDataFlowThread);
                    task4.Start();

                    CreateParameterBoolean(OsLocalization.Market.Label242, false);
                    _needToCheckDataFeedOnDisconnect = (ServerParameterBool)ServerParameters[ServerParameters.Count - 1];
                    ServerParameters[ServerParameters.Count - 1].Comment = OsLocalization.Market.Label243;
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
                _candleStorage.CandlesSaveCount = _needToLoadCandlesCountParam.Value;
                _candleStorage.LogMessageEvent += SendLogMessage;

                Log = new Log(this.ServerNameUnique + "Server", StartProgram.IsOsTrader);
                Log.Listen(this);

                _serverStatusNeed = ServerConnectStatus.Disconnect;

                _loadDataLocker = "lockerData_" + ServerType.ToString();

                Task task0 = new Task(ExecutorOrdersThreadArea);
                task0.Start();

                Task task = new Task(PrimeThreadArea);
                task.Start();

                Task.Run(() => HighPriorityDataThreadArea());
                Task.Run(() => MediumPriorityDataThreadArea());
                Task.Run(() => LowPriorityDataThreadArea());

                Task task3 = new Task(MyTradesBeepThread);
                task3.Start();

                _serverIsCreated = true;

                _ordersHub = new AServerOrdersHub(this);
                _ordersHub.LogMessageEvent += SendLogMessage;
                _ordersHub.GetAllActiveOrdersOnReconnectEvent += _ordersHub_GetAllActiveOrdersOnReconnectEvent;
                _ordersHub.ActiveStateOrderCheckStatusEvent += _ordersHub_ActiveStateOrderCheckStatusEvent;
                _ordersHub.LostOrderEvent += _ordersHub_LostOrderEvent;
                _ordersHub.LostMyTradesEvent += _ordersHub_LostMyTradesEvent;

                _nonTradePeriods = new NonTradePeriods(ServerNameUnique);

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

        private ServerParameterBool _needToCheckDataFeedOnDisconnect;

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
        public ServerParameterInt _needToLoadCandlesCountParam;

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
        public IServerParameter GetStandardServerParameter(int index)
        {
            if (index < 0 || index >= _serverStandardParamsCount)
            {
                throw new Exception("Index out of range");
            }

            return ServerParameters[^(_serverStandardParamsCount - index)];
        }

        /// <summary>
        /// create STRING server parameter
        /// </summary>
        public ServerParameterString CreateParameterString(string name, string param)
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

            return newParam;
        }

        /// <summary>
        /// create INT server parameter
        /// </summary>
        public ServerParameterInt CreateParameterInt(string name, int param)
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

            return newParam;
        }

        /// <summary>
        /// create ENUM server parameter
        /// </summary>
        public ServerParameterEnum CreateParameterEnum(string name, string value, List<string> collection)
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

            return newParam;
        }

        /// <summary>
        /// create DECIMAL server parameter
        /// </summary>
        public ServerParameterDecimal CreateParameterDecimal(string name, decimal param)
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

            return newParam;
        }

        /// <summary>
        /// create BOOL server parameter
        /// </summary>
        public ServerParameterBool CreateParameterBoolean(string name, bool param)
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

            return newParam;
        }

        /// <summary>
        /// create PASSWORD server parameter
        /// </summary>
        public ServerParameterPassword CreateParameterPassword(string name, string param)
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

            return newParam;
        }

        /// <summary>
        /// create PATH TO FILE server parameter
        /// </summary>
        public ServerParameterPath CreateParameterPath(string name)
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

            return newParam;
        }

        /// <summary>
        /// create Button server parameter
        /// </summary>
        public ServerParameterButton CreateParameterButton(string name)
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

            return newParam;
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
        private void _needToLoadCandlesCountParam_ValueChange()
        {
            if (_needToLoadCandlesCountParam.Value > 20000)
            {
                _needToLoadCandlesCountParam.Value = 20000;
                return;
            }

            _candleStorage.CandlesSaveCount = _needToLoadCandlesCountParam.Value;
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
        /// Can do trade operations
        /// </summary>
        public bool IsReadyToTrade
        {
            get
            {
                if (ServerStatus != ServerConnectStatus.Connect)
                {
                    return false;
                }

                if (LastStartServerTime.AddSeconds(5) > DateTime.Now)
                {
                    return false;
                }

                if (LastStartServerTime.AddSeconds(this.WaitTimeToTradeAfterFirstStart) > DateTime.Now)
                {
                    return false;
                }

                if (Portfolios == null
                    || Portfolios.Count == 0)
                {
                    return false;
                }

                if (Securities == null
                 || Securities.Count == 0)
                {
                    return false;
                }

                return true;
            }
        }

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
            // OsLocalization.Market.Label171 Proxy type
            // OsLocalization.Market.Label172 Proxy

            ServerParameterEnum proxyType = null;
            ServerParameterString proxy = null;

            for (int i = 0; i < ServerParameters.Count; i++)
            {
                if (ServerParameters[i].Name == OsLocalization.Market.Label171)
                {
                    proxyType = (ServerParameterEnum)ServerParameters[i];
                }
                if (ServerParameters[i].Name == OsLocalization.Market.Label172)
                {
                    proxy = (ServerParameterString)ServerParameters[i];
                }
            }

            if (proxy == null
                || proxyType == null)
            {
                return null;
            }

            if (proxyType.Value == "None")
            {
                return null;
            }

            if (proxyType.Value == "Manual")
            {
                string proxyName = proxy.Value;

                if (string.IsNullOrEmpty(proxyName))
                {
                    return null;
                }

                return ServerMaster.GetProxyManualRegime(proxyName);
            }
            else if (proxyType.Value == "Auto")
            {

                return ServerMaster.GetProxyAutoRegime(this.ServerType, this.ServerNameAndPrefix);
            }

            return null;
        }

        #endregion

        #region Thread 1. Work with connection

        /// <summary>
        /// the place where connection is controlled. look at data streams
        /// </summary>
        private void PrimeThreadArea()
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

                        if (ServerPermission != null
                            && ServerPermission.IsSupports_ProxyFor_MultipleInstances)
                        {
                            WebProxy proxy = GetProxy();

                            if (proxy != null)
                            {
                                SendLogMessage(OsLocalization.Market.Label173 + "\n" + proxy.Address, LogMessageType.System);
                            }

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

                    GetNonTradePeriod();

                    if (_lastDateTimeServer.Date != DateTime.Now.Date)
                    {
                        HasConnectionMessageBeenSent = false;
                        HasFirstOrderMessageBeenSent = false;
                        _lastDateTimeServer = DateTime.Now.Date;
                    }

                    if (HasConnectionMessageBeenSent == false)
                    {
                        SendMessageConnectorConnectInAnalysisServer();
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

        private async void HighPriorityDataThreadArea()
        {
            while (true)
            {
                try
                {
                    bool workDone = false;

                    if (!_ordersToSend.IsEmpty)
                    {
                        workDone = true;
                        Order order;
                        while (_ordersToSend.TryDequeue(out order))
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

                    if (!_myTradesToSend.IsEmpty)
                    {
                        workDone = true;
                        MyTrade myTrade;

                        while (_myTradesToSend.TryDequeue(out myTrade))
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

                    if (!_portfolioToSend.IsEmpty)
                    {
                        workDone = true;
                        List<Portfolio> portfolio;

                        while (_portfolioToSend.TryDequeue(out portfolio))
                        {
                            if (PortfoliosChangeEvent != null)
                            {
                                PortfoliosChangeEvent(portfolio);
                            }
                        }
                    }

                    if (workDone == false)
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

        private async void MediumPriorityDataThreadArea()
        {
            while (true)
            {
                try
                {
                    bool workDone = false;

                    if (!_tradesToSend.IsEmpty)
                    {
                        workDone = true;
                        List<Trade> trades;

                        if (_tradesToSend.TryDequeue(out trades))
                        {
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
                                if (_isNonTradingPeriodNow) break;

                                if (_needToCheckDataFeedOnDisconnect != null
                                    && _needToCheckDataFeedOnDisconnect.Value)
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

                    if (!_marketDepthsToSend.IsEmpty)
                    {
                        workDone = true;
                        MarketDepth depth;

                        if (_marketDepthsToSend.TryDequeue(out depth))
                        {
                            if (_marketDepthsToSend.Count < 1000)
                            {
                                if (!_isNonTradingPeriodNow)
                                {
                                    if (NewMarketDepthEvent != null)
                                    {
                                        NewMarketDepthEvent(depth);
                                    }

                                    if (_needToCheckDataFeedOnDisconnect != null
                                        && _needToCheckDataFeedOnDisconnect.Value)
                                    {
                                        SecurityFlowTime tradeTime = new SecurityFlowTime();
                                        tradeTime.SecurityName = depth.SecurityNameCode;
                                        tradeTime.LastTimeMarketDepth = DateTime.Now;
                                        _securitiesFeedFlow.Enqueue(tradeTime);
                                    }
                                }
                            }
                            else
                            {
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
                                    if (_isNonTradingPeriodNow) break;

                                    if (_needToCheckDataFeedOnDisconnect != null
                                    && _needToCheckDataFeedOnDisconnect.Value)
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

                                SystemUsageAnalyzeMaster.MarketDepthClearingCount += 1;
                            }
                        }
                    }

                    if (!_bidAskToSend.IsEmpty)
                    {
                        workDone = true;
                        BidAskSender bidAsk;

                        if (_bidAskToSend.TryDequeue(out bidAsk))
                        {
                            if (_bidAskToSend.Count < 1000)
                            {
                                if (!_isNonTradingPeriodNow && NewBidAskIncomeEvent != null)
                                {
                                    NewBidAskIncomeEvent(bidAsk.Bid, bidAsk.Ask, bidAsk.Security);
                                }
                            }
                            else
                            {
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
                                    if (_isNonTradingPeriodNow) break;

                                    if (NewBidAskIncomeEvent != null)
                                    {
                                        NewBidAskIncomeEvent(list[i].Bid, list[i].Ask, list[i].Security);
                                    }
                                }

                                SystemUsageAnalyzeMaster.BidAskClearingCount += 1;
                            }
                        }
                    }

                    if (workDone == false)
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

        private async void LowPriorityDataThreadArea()
        {
            while (true)
            {
                try
                {
                    bool workDone = false;

                    if (!_securitiesToSend.IsEmpty)
                    {
                        workDone = true;
                        List<Security> security;

                        while (_securitiesToSend.TryDequeue(out security))
                        {
                            if (SecuritiesChangeEvent != null)
                            {
                                SecuritiesChangeEvent(security);
                            }
                        }
                    }
                    if (!_newServerTime.IsEmpty)
                    {
                        workDone = true;
                        DateTime time = DateTime.MinValue;
                        DateTime newTime;

                        while (_newServerTime.TryDequeue(out newTime))
                        {
                            time = newTime;
                        }

                        if (time != DateTime.MinValue)
                        {
                            if (TimeServerChangeEvent != null)
                            {
                                TimeServerChangeEvent(_serverTime);
                            }
                        }
                    }

                    if (!_candleSeriesToSend.IsEmpty)
                    {
                        workDone = true;
                        CandleSeries series;

                        while (_candleSeriesToSend.TryDequeue(out series))
                        {
                            if (NewCandleIncomeEvent != null)
                            {
                                NewCandleIncomeEvent(series);
                            }
                        }
                    }

                    if (!_newsToSend.IsEmpty)
                    {
                        workDone = true;
                        News news;

                        while (_newsToSend.TryDequeue(out news))
                        {
                            if (NewsEvent != null)
                            {
                                NewsEvent(news);
                            }
                        }
                    }

                    if (!_additionalMarketDataToSend.IsEmpty)
                    {
                        workDone = true;

                        if (_additionalMarketDataToSend.Count < 1000)
                        {
                            OptionMarketDataForConnector data;
                            while (_additionalMarketDataToSend.TryDequeue(out data))
                            {
                                ConvertableMarketData(data);
                            }
                        }
                        else
                        {
                            Dictionary<string, OptionMarketDataForConnector> lastData = new Dictionary<string, OptionMarketDataForConnector>();

                            OptionMarketDataForConnector data;
                            while (_additionalMarketDataToSend.TryDequeue(out data))
                            {
                                if (lastData.ContainsKey(data.SecurityName) == false)
                                {
                                    lastData.Add(data.SecurityName, data);
                                }
                                else
                                {
                                    lastData[data.SecurityName] = data;
                                }
                            }
                            foreach (var val in lastData.Values)
                            {
                                ConvertableMarketData(val);
                            }
                        }
                    }

                    if (!_fundingToSend.IsEmpty)
                    {
                        workDone = true;

                        if (_fundingToSend.Count < 1000)
                        {
                            Funding data;
                            while (_fundingToSend.TryDequeue(out data))
                            {
                                if (NewFundingEvent != null)
                                {
                                    NewFundingEvent(data);
                                }
                            }
                        }
                        else
                        {
                            Dictionary<string, Funding> lastData = new Dictionary<string, Funding>();

                            Funding data;
                            while (_fundingToSend.TryDequeue(out data))
                            {
                                if (lastData.ContainsKey(data.SecurityNameCode) == false)
                                {
                                    lastData.Add(data.SecurityNameCode, data);
                                }
                                else
                                {
                                    lastData[data.SecurityNameCode] = data;
                                }
                            }
                            foreach (var val in lastData.Values)
                            {
                                if (NewFundingEvent != null)
                                {
                                    NewFundingEvent(val);
                                }
                            }
                        }
                    }

                    if (!_securityVolumesToSend.IsEmpty)
                    {
                        workDone = true;
                        if (_securityVolumesToSend.Count < 1000)
                        {
                            SecurityVolumes data;
                            while (_securityVolumesToSend.TryDequeue(out data))
                            {
                                if (NewVolume24hUpdateEvent != null)
                                {
                                    NewVolume24hUpdateEvent(data);
                                }
                            }
                        }
                        else
                        {
                            Dictionary<string, SecurityVolumes> lastData = new Dictionary<string, SecurityVolumes>();

                            SecurityVolumes data;
                            while (_securityVolumesToSend.TryDequeue(out data))
                            {
                                if (lastData.ContainsKey(data.SecurityNameCode) == false)
                                {
                                    lastData.Add(data.SecurityNameCode, data);
                                }
                                else
                                {
                                    lastData[data.SecurityNameCode] = data;
                                }
                            }
                            foreach (var val in lastData.Values)
                            {
                                if (NewVolume24hUpdateEvent != null)
                                {
                                    NewVolume24hUpdateEvent(val);
                                }
                            }
                        }
                    }

                    if (workDone == false)
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

        /// <summary>
        /// queue for Funding
        /// </summary>
        private ConcurrentQueue<Funding> _fundingToSend = new ConcurrentQueue<Funding>();

        /// <summary>
        /// queue for Volume24H
        /// </summary>
        private ConcurrentQueue<SecurityVolumes> _securityVolumesToSend = new ConcurrentQueue<SecurityVolumes>();

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

                    if (portf[i].ServerUniqueName != this.ServerNameAndPrefix)
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
        /// Request securities from server again.
        /// </summary>
        public void ReloadSecurities()
        {
            ServerRealization.GetSecurities();
        }

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

                    Security sec = _securities.Find(s =>
                            s != null &&
                            s.NameId == securities[i].NameId &&
                            s.Name == securities[i].Name &&
                            s.NameClass == securities[i].NameClass);

                    if (sec == null)
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
                    else
                    {
                        sec.Lot = securities[i].Lot;
                        sec.PriceStepCost = securities[i].PriceStepCost;
                        sec.PriceStep = securities[i].PriceStep;
                        sec.VolumeStep = securities[i].VolumeStep;
                        sec.DecimalsVolume = securities[i].DecimalsVolume;
                        sec.Decimals = securities[i].Decimals;
                        sec.Strike = securities[i].Strike;
                        sec.State = securities[i].State;
                        sec.Expiration = securities[i].Expiration;
                        sec.MarginBuy = securities[i].MarginBuy;
                        sec.MarginSell = securities[i].MarginSell;
                        sec.MinTradeAmount = securities[i].MinTradeAmount;
                        sec.PriceLimitHigh = securities[i].PriceLimitHigh;
                        sec.PriceLimitLow = securities[i].PriceLimitLow;
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

        private void AServer_UserClickSecuritiesUiButton()
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
                            securities[j].MarginBuy = curSaveSec.MarginBuy;
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
                        Security securityCurrent = _securities[i];

                        if (securityCurrent == null)
                        {
                            continue;
                        }
                        if (securityCurrent.Name == securityName &&
                            (securityCurrent.NameClass == securityClass))
                        {
                            security = securityCurrent;
                            break;
                        }
                        if (securityCurrent.Name == securityName &&
                            (securityClass == null))
                        {
                            security = securityCurrent;
                            break;
                        }
                    }

                    if (security == null)
                    {
                        return null;
                    }

                    CandleSeries series = new CandleSeries(timeFrameBuilder, security, StartProgram.IsOsTrader);

                    ServerRealization.Subscribe(security);

                    _candleManager.StartSeries(series);

                    SendLogMessage(OsLocalization.Market.Message14 + series.Security.Name + " " +
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
            try
            {
                if (ServerStatus != ServerConnectStatus.Connect)
                {
                    return;
                }

                if (series != null && _candleManager != null)
                {
                    _candleManager.StopSeries(series);
                }

                if (_candleStorage != null)
                {
                    _candleStorage.RemoveSeries(series);
                }

                Security security = series.Security;

                if (_candleManager != null &&
                    _candleManager.IsSafeToUnsubscribeFromSecurityUpdates(security))
                {
                    ServerRealization.Unsubscribe(security);
                    RemoveSecurityFromSubscribed(security.Name, security.NameClass);
                }
            }
            catch (Exception ex)
            {
                SendLogMessage(ex.ToString(), LogMessageType.Error);
            }
        }

        /// <summary>
        /// candles series changed
        /// </summary>
        private void _candleManager_CandleUpdateEvent(CandleSeries series)
        {
            if (series.IsMergedByCandlesFromFile == false 
                && series.CandleCreateMethodType == "TimeShiftCandle")
            {
                series.IsMergedByCandlesFromFile = true;
            }

            if (series.IsMergedByCandlesFromFile == false)
            {
                series.IsMergedByCandlesFromFile = true;

                if (_needToSaveCandlesParam.Value == true)
                {
                    List<Candle> candlesStorage = _candleStorage.GetCandles(series.Specification, _needToLoadCandlesCountParam.Value);

                    if(series.TimeFrameBuilder.CandleMarketDataType == CandleMarketDataType.MarketDepth)
                    {
                        // нужно вставками прогружать каждую свечу по отдельности. 
                        series.CandlesAll = series.CandlesAll.Merge(candlesStorage);

                        for(int i = 0; candlesStorage != null && i < candlesStorage.Count;i++)
                        {
                            Candle candle = candlesStorage[i];

                            bool isInArray = false;

                            for(int j = 0;j < series.CandlesAll.Count;j++)
                            {
                                if (series.CandlesAll[j].TimeStart == candle.TimeStart)
                                {
                                    series.CandlesAll[j] = candle;
                                    isInArray = true;
                                    break;
                                }
                                else if (j == 0
                                   && candle.TimeStart < series.CandlesAll[j].TimeStart)
                                {
                                    series.CandlesAll.Insert(j, candle);
                                    isInArray = true;
                                    break;
                                }
                                else if (j != 0
                                    && candle.TimeStart > series.CandlesAll[j-1].TimeStart
                                    && candle.TimeStart < series.CandlesAll[j].TimeStart)
                                {
                                    series.CandlesAll.Insert(j, candle);
                                    isInArray = true;
                                    break;
                                }
                            }

                            if(isInArray == false)
                            {
                                series.CandlesAll.Add(candle);
                            }
                        }

                        if(series.CandlesAll.Count > _needToLoadCandlesCountParam.Value)
                        {
                            series.CandlesAll = 
                                series.CandlesAll.GetRange(
                                    series.CandlesAll.Count - _needToLoadCandlesCountParam.Value, 
                                    _needToLoadCandlesCountParam.Value);
                        }

                    }
                    else
                    {
                        series.CandlesAll = series.CandlesAll.Merge(candlesStorage);
                    }

                    List<Candle> candlesAll = series.CandlesAll;

                    if (candlesStorage != null
                        && candlesStorage.Count > 0
                        && candlesAll != null)
                    {
                        // копируем в новый массив данные по открытому интересу
                        for (int i = 0, j = 0; i < candlesStorage.Count && j < candlesAll.Count; i++, j++)
                        {
                            Candle candleStorage = candlesStorage[i];
                            Candle candleAll = candlesAll[j];

                            if (candleStorage.TimeStart == candleAll.TimeStart)
                            {
                                if (candleStorage.OpenInterest > candleAll.OpenInterest)
                                {
                                    candleAll.OpenInterest = candleStorage.OpenInterest;
                                }
                            }
                            else if (candleStorage.TimeStart > candleAll.TimeStart)
                            {
                                i--;
                            }
                            else if (candleStorage.TimeStart < candleAll.TimeStart)
                            {
                                j--;
                            }
                        }
                    }
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
                && series.CandlesAll.Count > _needToLoadCandlesCountParam.Value
                && _serverTime.Minute % 15 == 0
                && _serverTime.Second == 0
            )
            {
                series.CandlesAll.RemoveRange(0, series.CandlesAll.Count - 1 - _needToLoadCandlesCountParam.Value);
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
            if (ServerPermission != null)
            {
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

        private void RemoveSecurityFromSubscribed(string securityName, string securityClass)
        {
            // remove security from subscribed list
            if (_subscribeSecurities == null || _subscribeSecurities.Count == 0)
            {
                return;
            }

            if (securityName == null || securityClass == null)
            {
                return;
            }

            for (int i = 0; i < _subscribeSecurities.Count; i++)
            {
                if (_subscribeSecurities[i].SecurityName == securityName
                    && _subscribeSecurities[i].SecurityClass == securityClass)
                {
                    _subscribeSecurities.RemoveAt(i);
                    return;
                }
            }
        }

        private void CheckDataFlowThread()
        {
            while (true)
            {
                try
                {
                    Thread.Sleep(1000);

                    if (MainWindow.ProccesIsWorked == false)
                    {
                        return;
                    }

                    if (this.ServerStatus != ServerConnectStatus.Connect)
                    {
                        continue;
                    }

                    if (_needToCheckDataFeedOnDisconnect.Value == false)
                    {
                        continue;
                    }

                    SecurityFlowTime securityFlowTime = null;

                    while (_securitiesFeedFlow.Count > 15000)
                    {
                        _securitiesFeedFlow.TryDequeue(out securityFlowTime);
                    }

                    // 1 разбираем очередь с обновлением данных с сервера

                    while (_securitiesFeedFlow.Count > 0)
                    {
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
            if (NewBidAskIncomeEvent == null)
            {
                return;
            }

            decimal bestBid = 0;
            if (newMarketDepth.Bids != null &&
                newMarketDepth.Bids.Count > 0)
            {
                bestBid = newMarketDepth.Bids[0].Price.ToDecimal();
            }

            decimal bestAsk = 0;
            if (newMarketDepth.Asks != null &&
                newMarketDepth.Asks.Count > 0)
            {
                bestAsk = newMarketDepth.Asks[0].Price.ToDecimal();
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
        public event Action<decimal, decimal, Security> NewBidAskIncomeEvent;

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
            if (trades == null)
            {
                return;
            }
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
                trade.Ask = depth.Asks[0].Price.ToDecimal();
            }

            if (depth.Bids != null &&
                depth.Bids.Count > 0)
            {
                trade.Bid = depth.Bids[0].Price.ToDecimal();
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
        private async void ExecutorOrdersThreadArea()
        {
            while (true)
            {
                try
                {
                    if (_ordersToExecute.IsEmpty == true)
                    {
                        await Task.Delay(1);
                        continue;
                    }

                    if (LastStartServerTime.AddSeconds(WaitTimeToTradeAfterFirstStart) > DateTime.Now)
                    {
                        await Task.Delay(1000);
                        continue;
                    }

                    if (_ordersToExecute != null && _ordersToExecute.Count != 0)
                    {
                        OrderAserverSender order;

                        SystemUsageAnalyzeMaster.OrdersInQueue = _ordersToExecute.Count;

                        if (_ordersToExecute.TryDequeue(out order))
                        {
                            if (_asyncOrdersSender != null)
                            {
                                _asyncOrdersSender.ExecuteAsync(order);
                            }
                            else
                            {
                                ExecuteOrderInRealization(order);
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

        private AServerAsyncOrderSender _asyncOrdersSender;

        private void ExecuteOrderInRealization(OrderAserverSender order)
        {
            try
            {
                if (order.OrderSendType == OrderSendType.Execute)
                {
                    ServerRealization.SendOrder(order.Order);
                }
                else if (order.OrderSendType == OrderSendType.Cancel)
                {
                    //if (IsAlreadyCancelled(order.Order) == false
                    //    || order.Order.CancellingTryCount < 5)
                    //{
                    if (ServerRealization.CancelOrder(order.Order) == false)
                    {
                        if (CancelOrderFailEvent != null)
                        {
                            CancelOrderFailEvent(order.Order);
                        }
                    }
                    /*else
                    {
                        if (string.IsNullOrEmpty(order.Order.NumberMarket) == false)
                        {
                            lock (_cancelledOrdersNumbersLocker)
                            {
                                _cancelledOrdersNumbers.Add(order.Order.NumberMarket);

                                if (_cancelledOrdersNumbers.Count > 150)
                                {
                                    _cancelledOrdersNumbers.RemoveAt(0);
                                }
                            }
                        }
                    }*/
                    //}
                }
                else if (order.OrderSendType == OrderSendType.ChangePrice
                    && IsCanChangeOrderPrice)
                {
                    ServerRealization.ChangeOrderPrice(order.Order, order.NewPrice);
                }
            }
            catch (Exception error)
            {
                SendLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        private bool IsAlreadyCancelled(Order order)
        {
            if (string.IsNullOrEmpty(order.NumberMarket))
            {
                return false;
            }
            bool isCancelled = false;

            lock (_cancelledOrdersNumbersLocker)
            {
                if (_cancelledOrdersNumbers.Find(o => o == order.NumberMarket) != null)
                {
                    isCancelled = true;
                }
            }

            return isCancelled;
        }

        private List<string> _cancelledOrdersNumbers = new List<string>();

        private string _cancelledOrdersNumbersLocker = "_cancelledOrdersNumbersLocker";

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
                if (string.IsNullOrEmpty(order.ServerName))
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

                if (HasFirstOrderMessageBeenSent == false)
                {
                    SendMessageFirstOrderInAnalysisServer();
                }

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

                lock (_cancelOrdersLocker)
                {
                    order.IsSendToCancel = true;
                    order.CancellingTryCount++;
                    order.LastCancelTryLocalTime = DateTime.Now;

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

                        if (_canceledOrders.Count > 100)
                        {
                            _canceledOrders.RemoveAt(0);
                        }
                    }
                }

                saveOrder.NumberOfCalls++;

                if (saveOrder.NumberOfCalls >= 5)
                {
                    saveOrder.NumberOfErrors++;

                    /*if (saveOrder.NumberOfErrors <= 5)
                    {
                        SendLogMessage(
                        "AServer Error. You can't cancel order. There have already been five attempts to cancel order. "
                         + "NumberUser: " + order.NumberUser
                         + " NumberMarket: " + order.NumberMarket
                         , LogMessageType.Error);
                    }*/

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

        private string _cancelOrdersLocker = "_cancelOrdersLocker";

        /// <summary>
        /// cancel all orders from trading system
        /// </summary>
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

            if (myOrder.State == OrderStateType.None)
            {
                SendLogMessage(ServerNameAndPrefix + " Order in state None.", LogMessageType.Error);
                return;
            }

            myOrder.ServerType = ServerType;
            myOrder.ServerName = ServerNameUnique;

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

        /// <summary>
        /// An attempt to revoke the order ended in an error
        /// </summary>
        public event Action<Order> CancelOrderFailEvent;

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

        private void _serverRealization_ForceCheckOrdersAfterReconnect()
        {
            try
            {
                _ordersHub.ForceCheckOrdersAfterReconnect();
            }
            catch
            {
                // ignore
            }
        }

        #endregion

        #region Orders private data interface 

        /// <summary>
        /// returns a list of active orders. Starting from the first order and up to 100 orders
        /// </summary>
        public List<Order> GetActiveOrders()
        {
            return GetActiveOrders(0, 100);
        }

        /// <summary>
        /// returns a list of active orders. Starting from the startIndex order and up to count
        /// </summary>
        /// <param name="startIndex">index 0 - the newest orders </param>
        /// <param name="count">number of orders in the request. Maximum 100</param>
        public List<Order> GetActiveOrders(int startIndex, int count)
        {
            try
            {
                if (count > 100)
                {
                    count = 100;
                }

                return ServerRealization.GetActiveOrders(startIndex, count);
            }
            catch (Exception ex)
            {
                SendLogMessage(ex.ToString(), LogMessageType.Error);
            }

            return null;
        }

        /// <summary>
        /// returns a list of historical orders. Starting from the first order and up to 100 orders
        /// </summary>
        public List<Order> GetHistoricalOrders()
        {
            return GetHistoricalOrders(0, 100);
        }

        /// <summary>
        /// returns a list of historical orders. Starting from the startIndex order and up to count
        /// </summary>
        /// <param name="startIndex">index 0 - the newest orders </param>
        /// <param name="count">number of orders in the request. Maximum 100</param>
        public List<Order> GetHistoricalOrders(int startIndex, int count)
        {
            try
            {
                if (count > 100)
                {
                    count = 100;
                }

                return ServerRealization.GetHistoricalOrders(startIndex, count);
            }
            catch (Exception ex)
            {
                SendLogMessage(ex.ToString(), LogMessageType.Error);
            }

            return null;
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
                    _dictAdditionalMarketData[data.SecurityName].UnderlyingPrice = data.UnderlyingPrice.ToDouble();
                }
                if (!string.IsNullOrEmpty(data.MarkPrice) &&
                    _dictAdditionalMarketData[data.SecurityName].MarkPrice.ToString() != data.MarkPrice)
                {
                    _dictAdditionalMarketData[data.SecurityName].MarkPrice = data.MarkPrice.ToDouble();
                }
                if (!string.IsNullOrEmpty(data.MarkIV) &&
                    _dictAdditionalMarketData[data.SecurityName].MarkIV.ToString() != data.MarkIV)
                {
                    _dictAdditionalMarketData[data.SecurityName].MarkIV = data.MarkIV.ToDouble();
                }
                if (!string.IsNullOrEmpty(data.BidIV) &&
                    _dictAdditionalMarketData[data.SecurityName].BidIV.ToString() != data.BidIV)
                {
                    _dictAdditionalMarketData[data.SecurityName].BidIV = data.BidIV.ToDouble();
                }
                if (!string.IsNullOrEmpty(data.AskIV) &&
                    _dictAdditionalMarketData[data.SecurityName].AskIV.ToString() != data.AskIV)
                {
                    _dictAdditionalMarketData[data.SecurityName].AskIV = data.AskIV.ToDouble();
                }
                if (!string.IsNullOrEmpty(data.Delta) &&
                    _dictAdditionalMarketData[data.SecurityName].Delta.ToString() != data.Delta)
                {
                    _dictAdditionalMarketData[data.SecurityName].Delta = data.Delta.ToDouble();
                }
                if (!string.IsNullOrEmpty(data.Gamma) &&
                    _dictAdditionalMarketData[data.SecurityName].Gamma.ToString() != data.Gamma)
                {
                    _dictAdditionalMarketData[data.SecurityName].Gamma = data.Gamma.ToDouble();
                }
                if (!string.IsNullOrEmpty(data.Vega) &&
                    _dictAdditionalMarketData[data.SecurityName].Vega.ToString() != data.Vega)
                {
                    _dictAdditionalMarketData[data.SecurityName].Vega = data.Vega.ToDouble();
                }
                if (!string.IsNullOrEmpty(data.Theta) &&
                    _dictAdditionalMarketData[data.SecurityName].Theta.ToString() != data.Theta)
                {
                    _dictAdditionalMarketData[data.SecurityName].Theta = data.Theta.ToDouble();
                }
                if (!string.IsNullOrEmpty(data.Rho) &&
                    _dictAdditionalMarketData[data.SecurityName].Rho.ToString() != data.Rho)
                {
                    _dictAdditionalMarketData[data.SecurityName].Rho = data.Rho.ToDouble();
                }
                if (!string.IsNullOrEmpty(data.OpenInterest) &&
                    _dictAdditionalMarketData[data.SecurityName].OpenInterest.ToString() != data.OpenInterest)
                {
                    _dictAdditionalMarketData[data.SecurityName].OpenInterest = data.OpenInterest.ToDouble();
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

        private void _serverRealization_FundingUpdateEvent(Funding obj)
        {
            try
            {
                if (obj == null)
                {
                    return;
                }

                if (string.IsNullOrEmpty(obj.SecurityNameCode))
                {
                    return;
                }

                _fundingToSend.Enqueue(obj);
            }
            catch (Exception ex)
            {
                SendLogMessage(ex.ToString(), LogMessageType.Error);
            }
        }

        /// <summary>
        /// new Funding data
        /// </summary>
        public event Action<Funding> NewFundingEvent;

        private void _serverRealization_Volume24hUpdateEvent(SecurityVolumes obj)
        {
            try
            {
                if (obj == null)
                {
                    return;
                }

                if (string.IsNullOrEmpty(obj.SecurityNameCode))
                {
                    return;
                }

                _securityVolumesToSend.Enqueue(obj);
            }
            catch (Exception ex)
            {
                SendLogMessage(ex.ToString(), LogMessageType.Error);
            }
        }

        /// <summary>
        /// new Volumes 24h data
        /// </summary>
        public event Action<SecurityVolumes> NewVolume24hUpdateEvent;

        #endregion

        #region SendMessageAnalysisServer

        private bool HasConnectionMessageBeenSent = false;

        private bool HasFirstOrderMessageBeenSent = false;

        private string _messageFirstConnect;

        private string _messageFirstOrder;

        private DateTime _lastDateTimeServer = DateTime.MinValue;

        private void SendMessageConnectorConnectInAnalysisServer()
        {
            try
            {
                _messageFirstConnect = $"{this.ServerNameUnique}%Openings";

                Thread thread = new Thread(SendMessageConnectorConnect);
                thread.Start();

                HasConnectionMessageBeenSent = true;
            }
            catch
            {
                // ignore
            }
        }

        private void SendMessageConnectorConnect()
        {
            try
            {
                TcpClient newClient = new TcpClient();
                newClient.Connect("45.137.152.144", 11100);
                NetworkStream tcpStream = newClient.GetStream();
                byte[] sendBytes = Encoding.UTF8.GetBytes(_messageFirstConnect);
                tcpStream.Write(sendBytes, 0, sendBytes.Length);
                newClient.Close();
            }
            catch
            {
                // игнор
            }
        }

        private void SendMessageFirstOrderInAnalysisServer()
        {
            try
            {
                _messageFirstOrder = $"{this.ServerNameUnique}%Orders";

                Thread thread = new Thread(SendMessageFirstOrder);
                thread.Start();

                HasFirstOrderMessageBeenSent = true;
            }
            catch
            {
                // ignore
            }
        }

        private void SendMessageFirstOrder()
        {
            try
            {
                TcpClient newClient = new TcpClient();
                newClient.Connect("45.137.152.144", 11100);
                NetworkStream tcpStream = newClient.GetStream();
                byte[] sendBytes = Encoding.UTF8.GetBytes(_messageFirstOrder);
                tcpStream.Write(sendBytes, 0, sendBytes.Length);
                newClient.Close();
            }
            catch
            {
                // игнор
            }
        }

        #endregion

        #region Non trade periods

        private NonTradePeriods _nonTradePeriods;

        private bool _isNonTradingPeriodNow;

        private void AServer_UserClickNonTradePeriodsUiButton()
        {
            if (_nonTradePeriods != null)
            {
                _nonTradePeriods.ShowDialog();
            }
        }

        private void GetNonTradePeriod()
        {
            try
            {
                if (_nonTradePeriods == null)
                {
                    return;
                }

                if (!_nonTradePeriods.CanTradeThisTime(DateTime.Now))
                {
                    _isNonTradingPeriodNow = true;
                }
                else
                {
                    _isNonTradingPeriodNow = false;
                }
            }
            catch (Exception ex)
            {
                SendLogMessage(ex.ToString(), LogMessageType.Error);
            }
        }

        public bool IsNonTradePeriod
        {
            get 
            {
                return _isNonTradingPeriodNow; 
            }
        }

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
