/*
 *Your rights to use the code are governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 *Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System;
using System.Collections.Generic;
using System.IO;
using OsEngine.Entity;
using OsEngine.Language;
using OsEngine.Logging;
using OsEngine.Market.Servers;
using OsEngine.Market.Servers.Optimizer;
using OsEngine.Market.Servers.Tester;
using System.Threading.Tasks;

namespace OsEngine.Market.Connectors
{

    /// <summary>
    /// class that provides a universal interface for connecting to the servers of the exchange for bots
    /// terminals and tabs that can trade
    /// </summary>
    public class ConnectorCandles
    {
        #region Service code

        /// <summary>
        /// constructor
        /// </summary>
        /// <param name="name"> bot name</param>
        /// <param name="startProgram"> program that created the bot which created this connection</param>
        public ConnectorCandles(string name, StartProgram startProgram, bool createEmulator)
        {
            _name = name;
            StartProgram = startProgram;

            TimeFrameBuilder = new TimeFrameBuilder(_name, startProgram);
            ServerType = ServerType.None;


            if (StartProgram != StartProgram.IsOsOptimizer)
            {
                _canSave = true;
                Load();
                ServerMaster.RevokeOrderToEmulatorEvent += ServerMaster_RevokeOrderToEmulatorEvent;
            }

            if (createEmulator == true && startProgram != StartProgram.IsOsOptimizer)
            {
                _emulator = new OrderExecutionEmulator();
                _emulator.MyTradeEvent += ConnectorBot_NewMyTradeEvent;
                _emulator.OrderChangeEvent += ConnectorBot_NewOrderIncomeEvent;

            }

            if (!string.IsNullOrWhiteSpace(SecurityName))
            {
                _taskIsDead = false;
                Task.Run(Subscribe);
            }
            else
            {
                _taskIsDead = true;
            }

            if (StartProgram == StartProgram.IsTester)
            {
                PortfolioName = "GodMode";
            }
        }

        /// <summary>
        /// program that created the bot which created this connection
        /// </summary>
        public StartProgram StartProgram;

        /// <summary>
        /// shows whether it is possible to save settings
        /// </summary>
        private bool _canSave;

        /// <summary>
        /// upload settings
        /// </summary>
        private void Load()
        {
            if (!File.Exists(@"Engine\" + _name + @"ConnectorPrime.txt"))
            {
                return;
            }
            try
            {
                using (StreamReader reader = new StreamReader(@"Engine\" + _name + @"ConnectorPrime.txt"))
                {

                    PortfolioName = reader.ReadLine();
                    EmulatorIsOn = Convert.ToBoolean(reader.ReadLine());
                    _securityName = reader.ReadLine();
                    Enum.TryParse(reader.ReadLine(), true, out ServerType);
                    _securityClass = reader.ReadLine();

                    if (reader.EndOfStream == false)
                    {
                        _eventsIsOn = Convert.ToBoolean(reader.ReadLine());
                    }
                    else
                    {
                        _eventsIsOn = true;
                    }

                    if (reader.EndOfStream == false)
                    {
                        ServerFullName = reader.ReadLine();
                    }
                    else
                    {
                        ServerFullName = ServerType.ToString();
                    }

                    reader.Close();
                }
            }
            catch
            {
                _eventsIsOn = true;
                // ignore
            }
        }

        /// <summary>
        /// save settings in file
        /// </summary>
        public void Save()
        {
            if (_canSave == false)
            {
                return;
            }
            if (StartProgram == StartProgram.IsOsOptimizer)
            {
                return;
            }
            try
            {
                using (StreamWriter writer = new StreamWriter(@"Engine\" + _name + @"ConnectorPrime.txt", false))
                {
                    writer.WriteLine(PortfolioName);
                    writer.WriteLine(EmulatorIsOn);
                    writer.WriteLine(SecurityName);
                    writer.WriteLine(ServerType);
                    writer.WriteLine(SecurityClass);
                    writer.WriteLine(EventsIsOn);
                    writer.WriteLine(ServerFullName);

                    writer.Close();
                }
            }
            catch
            {
                // ignore
            }
        }

        /// <summary>
        /// delete object and clear memory
        /// </summary>
        public void Delete()
        {
            if (_ui != null)
            {
                _ui.Close();
            }

            _needToStopThread = true;

            if (StartProgram != StartProgram.IsOsOptimizer)
            {
                try
                {
                    if (File.Exists(@"Engine\" + _name + @"ConnectorPrime.txt"))
                    {
                        File.Delete(@"Engine\" + _name + @"ConnectorPrime.txt");
                    }
                }
                catch
                {
                    // ignore
                }

                ServerMaster.RevokeOrderToEmulatorEvent -= ServerMaster_RevokeOrderToEmulatorEvent;
            }

            if (_mySeries != null)
            {
                if (_myServer != null)
                {
                    _myServer.StopThisSecurity(_mySeries);
                }

                _mySeries.CandleUpdateEvent -= MySeries_CandleUpdateEvent;
                _mySeries.CandleFinishedEvent -= MySeries_CandleFinishedEvent;
                _mySeries.Stop();
                _mySeries.Clear();
                _mySeries = null;
            }

            if (_emulator != null)
            {
                _emulator.MyTradeEvent -= ConnectorBot_NewMyTradeEvent;
                _emulator.OrderChangeEvent -= ConnectorBot_NewOrderIncomeEvent;
            }

            if (_myServer != null)
            {
                if (_myServer.ServerType == ServerType.Tester)
                {
                    ((TesterServer)_myServer).TestingEndEvent -= Connector_TestingEndEvent;
                    ((TesterServer)_myServer).TestingStartEvent -= Connector_TestingStartEvent;
                }

                UnSubscribeOnServer(_myServer);
                _myServer = null;
            }

            if(TimeFrameBuilder != null)
            {
                TimeFrameBuilder = null;
            }

            _securityName = null;
            _optionMarketData = null;
            _funding = null;
            _securityVolumes = null;

        }

        /// <summary>
        /// show settings window
        /// </summary>
        public void ShowDialog(bool canChangeSettingsSaveCandlesIn)
        {
            try
            {
                if (ServerMaster.GetServers() == null ||
                    ServerMaster.GetServers().Count == 0)
                {
                    SendNewLogMessage(OsLocalization.Market.Message1, LogMessageType.Error);
                    return;
                }

                if (_ui == null)
                {
                    _ui = new ConnectorCandlesUi(this);
                    _ui.IsCanChangeSaveTradesInCandles(canChangeSettingsSaveCandlesIn);
                    _ui.LogMessageEvent += SendNewLogMessage;
                    _ui.Closed += _ui_Closed;
                    _ui.Show();
                }
                else
                {
                    _ui.Activate();
                }
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        private void _ui_Closed(object sender, EventArgs e)
        {
            try
            {
                _ui.LogMessageEvent -= SendNewLogMessage;
                _ui.Closed -= _ui_Closed;
                _ui = null;

                if (DialogClosed != null)
                {
                    DialogClosed();
                }
            }
            catch
            {
                // ignore
            }
        }

        public event Action DialogClosed;

        private ConnectorCandlesUi _ui;

        #endregion

        #region Settings and properties

        /// <summary>
        /// name of bot that owns the connector
        /// </summary>
        public string UniqueName
        {
            get { return _name; }
        }
        private string _name;

        /// <summary>
        /// trade server
        /// </summary>
        public IServer MyServer
        {
            get { return _myServer; }
        }
        private IServer _myServer;

        /// <summary>
        /// connector's server type 
        /// </summary>
        public ServerType ServerType;

        /// <summary>
        /// connector`s server full name
        /// </summary>
        public string ServerFullName;

        /// <summary>
        /// unique server number. Service data for the optimizer
        /// </summary>
        public int ServerUid;

        /// <summary>
        /// whether the object is connected to the server
        /// </summary>
        public bool IsConnected
        {
            get
            {
                if (_mySeries != null)
                {
                    return true;
                }
                return false;
            }
        }

        /// <summary>
        /// connector is ready to send Orders it true
        /// </summary>
        public bool IsReadyToTrade
        {
            get
            {
                if (_myServer == null)
                {
                    return false;
                }

                if (_myServer.ServerStatus != ServerConnectStatus.Connect)
                {
                    return false;
                }

                if (StartProgram != StartProgram.IsOsTrader)
                { // в тестере и оптимизаторе дальше не проверяем
                    return true;
                }

                if (_myServer.LastStartServerTime.AddSeconds(5) > DateTime.Now)
                {
                    return false;
                }

                if (_myServer.GetType().BaseType == typeof(AServer))
                {
                    AServer aServer = (AServer)_myServer;
                    if (aServer.LastStartServerTime.AddSeconds(aServer.WaitTimeToTradeAfterFirstStart) > DateTime.Now)
                    {
                        return false;
                    }
                }

                return true;
            }
        }

        /// <summary>
        /// connector's portfolio number
        /// </summary>
        public string PortfolioName;

        /// <summary>
        /// connector's portfolio object
        /// </summary>
        public Portfolio Portfolio
        {
            get
            {
                try
                {
                    if (_myServer != null)
                    {
                        return _myServer.GetPortfolioForName(PortfolioName);
                    }
                    return null;
                }
                catch (Exception error)
                {
                    SendNewLogMessage(error.ToString(), LogMessageType.Error);
                }

                return null;
            }
        }

        /// <summary>
        /// connector's security name
        /// </summary>
        public string SecurityName
        {
            get { return _securityName; }
            set
            {
                if (value != _securityName)
                {
                    _securityName = value;
                    Save();
                    Reconnect();
                }
            }
        }
        private string _securityName;

        /// <summary>
        /// connector's security class
        /// </summary>
        public string SecurityClass
        {
            get { return _securityClass; }
            set
            {
                if (value != _securityClass)
                {
                    _securityClass = value;
                    Save();
                    Reconnect();
                }
            }
        }
        private string _securityClass;

        /// <summary>
        /// connector's security object
        /// </summary>
        public Security Security
        {
            get
            {
                try
                {
                    if (_myServer != null)
                    {
                        return _myServer.GetSecurityForName(_securityName, _securityClass);
                    }
                }
                catch (Exception error)
                {
                    SendNewLogMessage(error.ToString(), LogMessageType.Error);
                }

                return null;
            }
        }

        /// <summary>
        /// does the server supports Market type orders. Support = true
        /// </summary>
        public bool MarketOrdersIsSupport
        {
            get
            {
                if (ServerType == ServerType.Tester ||
                     ServerType == ServerType.Optimizer)
                {
                    return true;
                }

                if (ServerType == ServerType.None)
                {
                    return false;
                }

                IServerPermission serverPermision = ServerMaster.GetServerPermission(ServerType);

                if (serverPermision == null)
                {
                    return false;
                }

                return serverPermision.MarketOrdersIsSupport;
            }
        }

        /// <summary>
        /// does the server support order price change. Support = true
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

        public MarketDepthLoadRegime MarketDepthPaintRegime
        {
            get
            {
                if (ServerType == ServerType.Tester ||
                    ServerType == ServerType.Optimizer)
                {
                    return MarketDepthLoadRegime.Unknown;
                }
                else
                {
                    IServer server = _myServer;

                    if(server == null)
                    {
                        return MarketDepthLoadRegime.All;
                    }

                    if(server.GetType().BaseType.Name == "AServer")
                    {
                        AServer Aserver = (AServer)server;

                        if(Aserver._needToUseFullMarketDepth.Value == true)
                        {
                            return MarketDepthLoadRegime.All;
                        }
                        else
                        {
                            return MarketDepthLoadRegime.BidAsk;
                        }

                    }
                    else
                    {
                        return MarketDepthLoadRegime.All;
                    }
                }
            }
        }

        /// <summary>
        /// shows whether execution of orders in emulation mode is enabled
        /// </summary>
        public bool EmulatorIsOn;

        /// <summary>
        /// emulator. Object for order execution in the emulation mode 
        /// </summary>
        private readonly OrderExecutionEmulator _emulator;

        /// <summary>
        /// whether event feeding is enabled in the robot
        /// </summary>
        public bool EventsIsOn
        {
            get
            {
                return _eventsIsOn;
            }
            set
            {
                if (_eventsIsOn == value)
                {
                    return;
                }
                _eventsIsOn = value;
                Save();
            }
        }
        private bool _eventsIsOn = true;

        /// <summary>
        /// commission type for positions
        /// </summary>
        public CommissionType CommissionType;

        /// <summary>
        /// commission rate
        /// </summary>
        public decimal CommissionValue;

        #endregion

        #region Candle series settings

        /// <summary>
        /// candle series that collects candles  
        /// </summary>
        public CandleSeries CandleSeries
        {
            get { return _mySeries; }
        }
        private CandleSeries _mySeries;

        /// <summary>
        /// object preserving settings for building candles
        /// </summary>
        public TimeFrameBuilder TimeFrameBuilder;

        /// <summary>
        /// method of creating candles: from ticks or from depths 
        /// </summary>
        public CandleMarketDataType CandleMarketDataType
        {
            set
            {
                if (value == TimeFrameBuilder.CandleMarketDataType)
                {
                    return;
                }
                TimeFrameBuilder.CandleMarketDataType = value;

                if (value == CandleMarketDataType.MarketDepth)
                {
                    NeedToLoadServerData = true;
                }

                Reconnect();
            }
            get { return TimeFrameBuilder.CandleMarketDataType; }
        }

        /// <summary>
        /// method of creating candles: Simple / Volume / Range / etc
        /// </summary>
        public string CandleCreateMethodType
        {
            set
            {
                if (value == TimeFrameBuilder.CandleCreateMethodType)
                {
                    return;
                }
                TimeFrameBuilder.CandleCreateMethodType = value;
                Reconnect();
            }
            get { return TimeFrameBuilder.CandleCreateMethodType; }
        }

        /// <summary>
        /// candles timeframe on which the connector is subscribed
        /// </summary>
        public TimeFrame TimeFrame
        {
            get { return TimeFrameBuilder.TimeFrame; }
            set
            {
                try
                {
                    if (value != TimeFrameBuilder.TimeFrame
                        || (value == TimeFrame.Sec1 &&
                        TimeFrameBuilder.TimeFrameTimeSpan.TotalSeconds == 0))
                    {
                        TimeFrameBuilder.TimeFrame = value;
                        Reconnect();
                    }
                }
                catch (Exception error)
                {
                    SendNewLogMessage(error.ToString(), LogMessageType.Error);
                }
            }
        }

        /// <summary>
        /// candle timeframe in the form of connector' s Timespan
        /// </summary>
        public TimeSpan TimeFrameTimeSpan
        {
            get { return TimeFrameBuilder.TimeFrameTimeSpan; }
        }

        /// <summary>
        /// whether the trades tape is saved inside the candles
        /// </summary>
        public bool SaveTradesInCandles
        {
            get { return TimeFrameBuilder.SaveTradesInCandles; }
            set
            {
                if (value == TimeFrameBuilder.SaveTradesInCandles)
                {
                    return;
                }
                TimeFrameBuilder.SaveTradesInCandles = value;
                Reconnect();
            }
        }

        #endregion

        #region Data subscription

        private DateTime _lastReconnectTime;

        private object _reconnectLocker = new object();

        private void Reconnect()
        {
            try
            {
                lock (_reconnectLocker)
                {
                    if (_lastReconnectTime.AddSeconds(1) > DateTime.Now)
                    {
                        if (ConnectorStartedReconnectEvent != null)
                        {
                            ConnectorStartedReconnectEvent(SecurityName, TimeFrame, TimeFrameTimeSpan, PortfolioName, ServerFullName);
                        }
                        return;
                    }
                    _lastReconnectTime = DateTime.Now;
                }


                if (_mySeries != null)
                {
                    _mySeries.Stop();
                    _mySeries.Clear();
                    _mySeries.CandleUpdateEvent -= MySeries_CandleUpdateEvent;
                    _mySeries.CandleFinishedEvent -= MySeries_CandleFinishedEvent;

                    if (_myServer != null)
                    {
                        _myServer.StopThisSecurity(_mySeries);
                    }
                    _mySeries = null;
                }

                Save();

                if (ConnectorStartedReconnectEvent != null)
                {
                    ConnectorStartedReconnectEvent(SecurityName, TimeFrame, TimeFrameTimeSpan, PortfolioName, ServerFullName);
                }

                if (_taskIsDead == true)
                {
                    _taskIsDead = false;
                    Task.Run(Subscribe);

                    if (NewCandlesChangeEvent != null)
                    {
                        NewCandlesChangeEvent(null);
                    }
                }
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        private bool _lastHardReconnectOver = true;

        public void ReconnectHard()
        {
            if (_lastHardReconnectOver == false)
            {
                return;
            }

            _lastHardReconnectOver = false;

            DateTime timestart = DateTime.Now;

            if (_mySeries != null)
            {
                _mySeries.Stop();
                _mySeries.Clear();
                _mySeries.CandleUpdateEvent -= MySeries_CandleUpdateEvent;
                _mySeries.CandleFinishedEvent -= MySeries_CandleFinishedEvent;

                if (_myServer != null)
                {
                    _myServer.StopThisSecurity(_mySeries);
                }
                _mySeries = null;
            }

            Reconnect();

            _lastHardReconnectOver = true;
        }

        private bool _taskIsDead;

        private bool _needToStopThread;

        private object _myServerLocker = new object();

        private static int _aliveTasks = 0;

        private static string _aliveTasksArrayLocker = "aliveTasksArrayLocker";

        private bool _alreadyCheckedInAliveTasksArray = false;

        private static int _tasksCountOnSubscribe = 0;

        private static string _tasksCountLocker = "_tasksCountOnLocker";

        private async void Subscribe()
        {
            try
            {
                _alreadyCheckedInAliveTasksArray = false;

                while (true)
                {
                    if (ServerType == ServerType.Optimizer)
                    {
                        await Task.Delay(1);
                    }
                    else if (ServerType == ServerType.Tester)
                    {
                        await Task.Delay(10);
                    }
                    else
                    {
                        int millisecondsToDelay = _aliveTasks * 5;

                        lock (_aliveTasksArrayLocker)
                        {
                            if (_alreadyCheckedInAliveTasksArray == false)
                            {
                                _aliveTasks++;
                                _alreadyCheckedInAliveTasksArray = true;
                            }

                            if (millisecondsToDelay < 500)
                            {
                                millisecondsToDelay = 500;
                            }
                        }

                        await Task.Delay(millisecondsToDelay);
                    }

                    if (_needToStopThread)
                    {
                        lock (_aliveTasksArrayLocker)
                        {
                            if (_aliveTasks > 0)
                            {
                                _aliveTasks--;
                            }
                        }
                        return;
                    }

                    if (ServerType == ServerType.None ||
                        string.IsNullOrWhiteSpace(SecurityName))
                    {
                        continue;
                    }

                    List<IServer> servers = ServerMaster.GetServers();

                    if (servers == null)
                    {
                        if (ServerType != ServerType.None)
                        {
                            ServerMaster.SetServerToAutoConnection(ServerType,ServerFullName);
                        }
                        continue;
                    }

                    try
                    {
                        if (ServerType == ServerType.Optimizer &&
                            this.ServerUid != 0)
                        {
                            for (int i = 0; i < servers.Count; i++)
                            {
                                if (servers[i] == null)
                                {
                                    servers.RemoveAt(i);
                                    i--;
                                    continue;
                                }
                                if (servers[i].ServerType == ServerType.Optimizer &&
                                    ((OptimizerServer)servers[i]).NumberServer == this.ServerUid)
                                {
                                    _myServer = servers[i];
                                    break;
                                }

                            }
                        }
                        else
                        {
                            for (int i = 0; i < servers.Count; i++)
                            {
                                if (servers[i].ServerType == ServerType)
                                {
                                    if(string.IsNullOrEmpty(ServerFullName) == true
                                        || servers[i].ServerNameAndPrefix.StartsWith(ServerFullName))
                                    {
                                        _myServer = servers[i];
                                        break;
                                    }
                                }
                                else if (string.IsNullOrEmpty(ServerFullName) &&
                                    servers[i].ServerType == ServerType)
                                {
                                    _myServer = servers[i];
                                    break;
                                }
                            }
                        }
                    }
                    catch
                    {
                        // ignore
                        continue;
                    }

                    if (_myServer == null)
                    {
                        if (ServerType != ServerType.None)
                        {
                            ServerMaster.SetServerToAutoConnection(ServerType,ServerFullName);
                        }
                        continue;
                    }

                    ServerConnectStatus stat = _myServer.ServerStatus;

                    if (stat != ServerConnectStatus.Connect)
                    {
                        continue;
                    }

                    SubscribeOnServer(_myServer);

                    if (_myServer.ServerType == ServerType.Tester)
                    {
                        ((TesterServer)_myServer).TestingEndEvent -= Connector_TestingEndEvent;
                        ((TesterServer)_myServer).TestingEndEvent += Connector_TestingEndEvent;

                        ((TesterServer)_myServer).TestingStartEvent -= Connector_TestingStartEvent;
                        ((TesterServer)_myServer).TestingStartEvent += Connector_TestingStartEvent;
                    }

                    if (_mySeries == null)
                    {
                        while (_mySeries == null)
                        {
                            if (_needToStopThread)
                            {
                                lock (_aliveTasksArrayLocker)
                                {
                                    if (_aliveTasks > 0)
                                    {
                                        _aliveTasks--;
                                    }
                                }
                                return;
                            }
                            if (_myServer == null)
                            {
                                continue;
                            }

                            if (StartProgram == StartProgram.IsOsTrader ||
                                StartProgram == StartProgram.IsOsData)
                            {
                                int millisecondsToDelay = _aliveTasks * 5;

                                if (millisecondsToDelay < 500)
                                {
                                    millisecondsToDelay = 500;
                                }

                                if(millisecondsToDelay > 1000)
                                {
                                    millisecondsToDelay = 1000;
                                }

                                await Task.Delay(millisecondsToDelay);
                            }
                            else
                            {
                                await Task.Delay(1);
                            }

                            if (_tasksCountOnSubscribe > 20)
                            {
                                continue;
                            }

                            lock (_tasksCountLocker)
                            {
                                _tasksCountOnSubscribe++;
                            }

                            lock (_myServerLocker)
                            {
                                if (_myServer != null)
                                {
                                    _mySeries = _myServer.StartThisSecurity(_securityName, TimeFrameBuilder, _securityClass);
                                }
                            }

                            lock (_tasksCountLocker)
                            {
                                _tasksCountOnSubscribe--;
                            }

                            OptimizerServer myOptimizerServer = _myServer as OptimizerServer;
                            if (_mySeries == null &&
                                myOptimizerServer != null &&
                                myOptimizerServer.ServerType == ServerType.Optimizer &&
                                myOptimizerServer.NumberServer != ServerUid)
                            {
                                for (int i = 0; i < servers.Count; i++)
                                {
                                    if (servers[i].ServerType == ServerType.Optimizer &&
                                        ((OptimizerServer)servers[i]).NumberServer == this.ServerUid)
                                    {
                                        UnSubscribeOnServer(_myServer);
                                        _myServer = servers[i];
                                        SubscribeOnServer(_myServer);
                                        break;
                                    }
                                }
                            }
                        }

                        _mySeries.CandleUpdateEvent += MySeries_CandleUpdateEvent;
                        _mySeries.CandleFinishedEvent += MySeries_CandleFinishedEvent;
                        _taskIsDead = true;
                    }


                    _taskIsDead = true;

                    if (SecuritySubscribeEvent != null)
                    {
                        SecuritySubscribeEvent(Security);
                    }

                    lock (_aliveTasksArrayLocker)
                    {
                        if (_aliveTasks > 0)
                        {
                            _aliveTasks--;
                        }
                    }

                    return;
                }
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        private void UnSubscribeOnServer(IServer server)
        {
            server.NewBidAskIncomeEvent -= ConnectorBotNewBidAskIncomeEvent;
            server.NewMyTradeEvent -= ConnectorBot_NewMyTradeEvent;
            server.CancelOrderFailEvent -= _myServer_CancelOrderFailEvent;
            server.NewOrderIncomeEvent -= ConnectorBot_NewOrderIncomeEvent;
            server.NewMarketDepthEvent -= ConnectorBot_NewMarketDepthEvent;
            server.NewTradeEvent -= ConnectorBot_NewTradeEvent;
            server.TimeServerChangeEvent -= myServer_TimeServerChangeEvent;
            server.NeedToReconnectEvent -= _myServer_NeedToReconnectEvent;
            server.PortfoliosChangeEvent -= Server_PortfoliosChangeEvent;
            server.NewAdditionalMarketDataEvent -= Server_NewAdditionalMarketDataEvent;
            server.NewFundingEvent -= Server_NewFundingEvent;
            server.NewVolume24hUpdateEvent -= Server_NewVolume24hUpdateEvent;
        }

        private void SubscribeOnServer(IServer server)
        {
            server.NewBidAskIncomeEvent -= ConnectorBotNewBidAskIncomeEvent;
            server.NewMyTradeEvent -= ConnectorBot_NewMyTradeEvent;
            server.NewOrderIncomeEvent -= ConnectorBot_NewOrderIncomeEvent;
            server.CancelOrderFailEvent -= _myServer_CancelOrderFailEvent;
            server.NewMarketDepthEvent -= ConnectorBot_NewMarketDepthEvent;
            server.NewTradeEvent -= ConnectorBot_NewTradeEvent;
            server.TimeServerChangeEvent -= myServer_TimeServerChangeEvent;
            server.NeedToReconnectEvent -= _myServer_NeedToReconnectEvent;
            server.PortfoliosChangeEvent -= Server_PortfoliosChangeEvent;
            server.NewAdditionalMarketDataEvent -= Server_NewAdditionalMarketDataEvent;
            server.NewFundingEvent -= Server_NewFundingEvent;
            server.NewVolume24hUpdateEvent -= Server_NewVolume24hUpdateEvent;

            if (NeedToLoadServerData)
            {
                server.NewMarketDepthEvent += ConnectorBot_NewMarketDepthEvent;
                server.NewBidAskIncomeEvent += ConnectorBotNewBidAskIncomeEvent;
                server.NewTradeEvent += ConnectorBot_NewTradeEvent;
                server.TimeServerChangeEvent += myServer_TimeServerChangeEvent;
                server.NewMyTradeEvent += ConnectorBot_NewMyTradeEvent;
                server.NewOrderIncomeEvent += ConnectorBot_NewOrderIncomeEvent;
                server.CancelOrderFailEvent += _myServer_CancelOrderFailEvent;
                server.PortfoliosChangeEvent += Server_PortfoliosChangeEvent;
                server.NewAdditionalMarketDataEvent += Server_NewAdditionalMarketDataEvent;
                server.NewFundingEvent += Server_NewFundingEvent;
                server.NewVolume24hUpdateEvent += Server_NewVolume24hUpdateEvent;
            }

            server.NeedToReconnectEvent += _myServer_NeedToReconnectEvent;
        }

        public bool NeedToLoadServerData = true;

        private void _myServer_NeedToReconnectEvent()
        {
            Reconnect();
        }

        #endregion

        #region Incoming data

        /// <summary>
        /// test finished. Event from tester
        /// </summary>
        private void Connector_TestingEndEvent()
        {
            try
            {
                if (TestOverEvent != null)
                {
                    TestOverEvent();
                }
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        private void Connector_TestingStartEvent()
        {
            try
            {
                if (TestStartEvent != null)
                {
                    TestStartEvent();
                }
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        /// <summary>
        /// time of the last completed candle
        /// </summary>
        private DateTime _timeLastEndCandle = DateTime.MinValue;

        /// <summary>
        /// the candle has just ended
        /// </summary>
        private void MySeries_CandleFinishedEvent(CandleSeries candleSeries)
        {
            try
            {
                if (EventsIsOn == false)
                {
                    return;
                }

                List<Candle> candles = Candles(true);

                if (candles == null || candles.Count == 0)
                {
                    return;
                }

                DateTime timeLastCandle = candles[candles.Count - 1].TimeStart;

                if (timeLastCandle == _timeLastEndCandle
                    && CandleCreateMethodType == "Simple")
                {
                    return;
                }

                _timeLastEndCandle = timeLastCandle;

                if (NewCandlesChangeEvent != null)
                {
                    NewCandlesChangeEvent(candles);
                }
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        /// <summary>
        /// the candle updated
        /// </summary>
        private void MySeries_CandleUpdateEvent(CandleSeries candleSeries)
        {
            try
            {
                if (LastCandlesChangeEvent != null && EventsIsOn == true)
                {
                    LastCandlesChangeEvent(Candles(false));
                }
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        /// <summary>
        /// incoming order
        /// </summary>
        private void ConnectorBot_NewOrderIncomeEvent(Order order)
        {
            try
            {
                if (StartProgram != StartProgram.IsOsTrader)
                {// tester or optimizer
                    if (order.SecurityNameCode != this.SecurityName)
                    {
                        return;
                    }
                }

                if (string.IsNullOrEmpty(order.ServerName))
                {
                    order.ServerName = this.ServerFullName;
                }

                if (OrderChangeEvent != null)
                {
                    OrderChangeEvent(order);
                }

                ServerMaster.InsertOrder(order);
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        private void _myServer_CancelOrderFailEvent(Order order)
        {
            try
            {
                if (StartProgram != StartProgram.IsOsTrader)
                {// tester or optimizer
                    if (order.SecurityNameCode != this.SecurityName)
                    {
                        return;
                    }
                }

                if (CancelOrderFailEvent != null)
                {
                    CancelOrderFailEvent(order);
                }
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        /// <summary>
        /// incoming my trade
        /// </summary>
        private void ConnectorBot_NewMyTradeEvent(MyTrade trade)
        {
            if (_myServer.ServerStatus != ServerConnectStatus.Connect)
            {
                return;
            }

            if (StartProgram != StartProgram.IsOsTrader)
            {// tester or optimizer
                if (trade.SecurityNameCode != this.SecurityName)
                {
                    return;
                }
            }

            try
            {
                if (MyTradeEvent != null)
                {
                    MyTradeEvent(trade);
                }
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        /// <summary>
        /// incoming best bid with ask
        /// </summary>
        private void ConnectorBotNewBidAskIncomeEvent(decimal bestBid, decimal bestAsk, Security security)
        {
            try
            {
                if (security == null ||
                    security.Name != _securityName)
                {
                    return;
                }

                _bestBid = bestBid;
                _bestAsk = bestAsk;

                if (StartProgram == StartProgram.IsOsTrader)
                {
                    if (EmulatorIsOn || ServerType == ServerType.Finam)
                    {
                        if (_emulator != null)
                        {
                            _emulator.ProcessBidAsk((decimal)_bestBid, (decimal)_bestAsk);
                        }
                    }
                    if (BestBidAskChangeEvent != null
                        && EventsIsOn == true)
                    {
                        BestBidAskChangeEvent(bestBid, bestAsk);
                    }
                }
                else
                {// Tester or Optimizer
                    if (BestBidAskChangeEvent != null)
                    {
                        BestBidAskChangeEvent(bestBid, bestAsk);
                    }
                }
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        /// <summary>
        /// incoming depth
        /// </summary>
        private void ConnectorBot_NewMarketDepthEvent(MarketDepth glass)
        {
            try
            {
                if (_securityName == null)
                {
                    return;
                }

                if (_securityName != glass.SecurityNameCode)
                {
                    return;
                }

                if (GlassChangeEvent != null && EventsIsOn == true)
                {
                    GlassChangeEvent(glass);
                }

                decimal bestBid = 0;

                if (glass.Bids != null &&
                     glass.Bids.Count > 0)
                {
                    bestBid = glass.Bids[0].Price.ToDecimal();
                }

                decimal bestAsk = 0;

                if (glass.Asks != null &&
                    glass.Asks.Count > 0)
                {
                    bestAsk = glass.Asks[0].Price.ToDecimal();
                }

                if (EmulatorIsOn)
                {
                    if (_emulator != null)
                    {
                        _emulator.ProcessBidAsk(bestAsk, bestBid);
                    }
                }

                if (bestAsk != 0)
                {
                    _bestAsk = bestAsk;
                }
                if (bestBid != 0)
                {
                    _bestBid = bestBid;
                }
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        /// <summary>
        /// incoming trades
        /// </summary>
        private void ConnectorBot_NewTradeEvent(List<Trade> tradesList)
        {
            try
            {
                if (_securityName == null
                    || tradesList == null
                    || tradesList.Count == 0)
                {
                    return;
                }
                else
                {
                    int count = tradesList.Count - 1;

                    if (tradesList[count] == null ||
                        tradesList[count].SecurityNameCode != _securityName)
                    {
                        return;
                    }
                }
            }
            catch
            {
                // it's hard to catch the error here. Who will understand what is wrong - well done 
                // ошибка здесь трудноуловимая. Кто понял что не так - молодец
                return;
            }

            try
            {
                if (TickChangeEvent != null && EventsIsOn == true)
                {
                    TickChangeEvent(tradesList);
                }
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        /// <summary>
        /// incoming server time
        /// </summary>
        private void myServer_TimeServerChangeEvent(DateTime time)
        {
            try
            {
                if (TimeChangeEvent != null && EventsIsOn == true)
                {
                    TimeChangeEvent(time);
                }
                if (EmulatorIsOn == true)
                {
                    if (_emulator != null)
                    {
                        _emulator.ProcessTime(time);
                    }
                }
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        /// <summary>
        /// on the stock market has changed the state of the portfolio
        /// </summary>
        private void Server_PortfoliosChangeEvent(List<Portfolio> portfolios)
        {
            try
            {
                Portfolio myPortfolio = null;

                for (int i = 0; i < portfolios.Count; i++)
                {
                    if (PortfolioName == portfolios[i].Number)
                    {
                        myPortfolio = portfolios[i];
                        break;
                    }
                }

                if (myPortfolio != null &&
                    PortfolioOnExchangeChangedEvent != null)
                {
                    PortfolioOnExchangeChangedEvent(myPortfolio);
                }
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        private void Server_NewAdditionalMarketDataEvent(OptionMarketData data)
        {
            try
            {
                if (_securityName != data.SecurityName)
                {
                    return;
                }

                _optionMarketData.SecurityName = data.SecurityName;
                _optionMarketData.UnderlyingAsset = data.UnderlyingAsset;
                _optionMarketData.UnderlyingPrice = data.UnderlyingPrice;
                _optionMarketData.MarkPrice = data.MarkPrice;
                _optionMarketData.MarkIV = data.MarkIV;
                _optionMarketData.BidIV = data.BidIV;
                _optionMarketData.AskIV = data.AskIV;
                _optionMarketData.Delta = data.Delta;
                _optionMarketData.Gamma = data.Gamma;
                _optionMarketData.Vega = data.Vega;
                _optionMarketData.Theta = data.Theta;
                _optionMarketData.Rho = data.Rho;
                _optionMarketData.OpenInterest = data.OpenInterest;
                _optionMarketData.TimeCreate = data.TimeCreate;

                AdditionalDataEvent?.Invoke(_optionMarketData);
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        private void Server_NewVolume24hUpdateEvent(SecurityVolumes data)
        {
            if (_securityName != data.SecurityNameCode)
            {
                return;
            }

            _securityVolumes.SecurityNameCode = data.SecurityNameCode;

            bool isChange = false;

            if (data.Volume24h != 0 && _securityVolumes.Volume24h != data.Volume24h)
            {
                _securityVolumes.Volume24h = data.Volume24h;
                isChange = true;
            }

            if (data.Volume24hUSDT != 0 && _securityVolumes.Volume24hUSDT != data.Volume24hUSDT)
            {
                _securityVolumes.Volume24hUSDT = data.Volume24hUSDT;
                isChange = true;
            }

            if (isChange)
            {
                if (data.TimeUpdate != new DateTime(1970, 1, 1, 0, 0, 0) && _securityVolumes.TimeUpdate != data.TimeUpdate)
                {
                    _securityVolumes.TimeUpdate = data.TimeUpdate;
                }

                SecurityVolumes marketData = new SecurityVolumes();

                marketData.SecurityNameCode = _securityVolumes.SecurityNameCode;
                marketData.Volume24h = _securityVolumes.Volume24h;
                marketData.Volume24hUSDT = _securityVolumes.Volume24hUSDT;
                marketData.TimeUpdate = _securityVolumes.TimeUpdate;

                NewVolume24hChangedEvent?.Invoke(marketData);
            }
        }

        private void Server_NewFundingEvent(Funding data)
        {
            if (_securityName != data.SecurityNameCode)
            {
                return;
            }

            _funding.SecurityNameCode = data.SecurityNameCode;

            bool isChange = false;

            if (data.CurrentValue != 0 && _funding.CurrentValue != data.CurrentValue)
            {
                _funding.CurrentValue = data.CurrentValue;
                isChange = true;
            }

            if (data.NextFundingTime != new DateTime(1970, 1, 1, 0, 0, 0) && _funding.NextFundingTime != data.NextFundingTime)
            {
                _funding.NextFundingTime = data.NextFundingTime;
                isChange = true;
            }

            if (data.PreviousValue != 0 && _funding.PreviousValue != data.PreviousValue)
            {
                _funding.PreviousValue = data.PreviousValue;
                isChange = true;
            }

            if (data.PreviousFundingTime != new DateTime(1970, 1, 1, 0, 0, 0) && _funding.PreviousFundingTime != data.PreviousFundingTime)
            {
                _funding.PreviousFundingTime = data.PreviousFundingTime;
                isChange = true;
            }

            if (data.MaxFundingRate != 0 && _funding.MaxFundingRate != data.MaxFundingRate)
            {
                _funding.MaxFundingRate = data.MaxFundingRate;
                isChange = true;
            }

            if (data.MinFundingRate != 0 && _funding.MinFundingRate != data.MinFundingRate)
            {
                _funding.MinFundingRate = data.MinFundingRate;
                isChange = true;
            }
                        
            if (_funding.NextFundingTime > new DateTime(1970, 1, 1, 0, 0, 0) &&
                _funding.PreviousFundingTime > new DateTime(1970, 1, 1, 0, 0, 0) &&
                _funding.FundingIntervalHours == 0)
            {
                _funding.NextFundingTime = _funding.NextFundingTime.AddMilliseconds(-_funding.NextFundingTime.Millisecond);
                _funding.PreviousFundingTime = _funding.PreviousFundingTime.AddMilliseconds(-_funding.PreviousFundingTime.Millisecond);

                _funding.FundingIntervalHours = (_funding.NextFundingTime - _funding.PreviousFundingTime).Hours;
                isChange = true;
            }

            if (_funding.FundingIntervalHours == 0 && data.FundingIntervalHours != 0)
            {
                _funding.FundingIntervalHours = data.FundingIntervalHours;
                isChange = true;
            }

            if (isChange)
            {
                if (data.TimeUpdate != new DateTime(1970, 1, 1, 0, 0, 0) && _funding.TimeUpdate != data.TimeUpdate)
                {
                    _funding.TimeUpdate = data.TimeUpdate;
                }

                Funding marketData = new Funding();

                marketData.SecurityNameCode = _funding.SecurityNameCode;
                marketData.CurrentValue = _funding.CurrentValue;
                marketData.NextFundingTime = _funding.NextFundingTime;
                marketData.FundingIntervalHours = _funding.FundingIntervalHours;
                marketData.MaxFundingRate = _funding.MaxFundingRate;
                marketData.MinFundingRate = _funding.MinFundingRate;
                marketData.TimeUpdate = _funding.TimeUpdate;               

                FundingChangedEvent?.Invoke(marketData);
            }
        }

        #endregion

        #region Trade data access interface

        /// <summary>
        /// trades feed
        /// </summary>
        public List<Trade> Trades
        {
            get
            {
                try
                {
                    if (_myServer != null)
                    {
                        return _myServer.GetAllTradesToSecurity(_myServer.GetSecurityForName(_securityName, _securityClass));
                    }
                }
                catch (Exception error)
                {
                    SendNewLogMessage(error.ToString(), LogMessageType.Error);
                }

                return null;
            }
        }

        /// <summary>
        /// connector's candles
        /// </summary>
        public List<Candle> Candles(bool onlyReady)
        {
            try
            {
                if (_mySeries == null ||
                    _mySeries.CandlesAll == null)
                {
                    return null;
                }
                if (onlyReady)
                {
                    return _mySeries.CandlesOnlyReady;
                }
                else
                {
                    return _mySeries.CandlesAll;
                }

            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }


            return null;
        }

        /// <summary>
        /// best price of seller in the depth
        /// </summary>
        public decimal BestAsk
        {
            get
            {
                return _bestAsk;
            }
        }
        private decimal _bestAsk;

        /// <summary>
        /// best price of buyer in the depth
        /// </summary>
        public decimal BestBid
        {
            get
            {
                return _bestBid;
            }
        }
        private decimal _bestBid;

        /// <summary>
        /// server time
        /// </summary>
        public DateTime MarketTime
        {
            get
            {
                try
                {
                    if (_myServer == null)
                    {
                        return DateTime.Now;
                    }
                    return _myServer.ServerTime;
                }
                catch (Exception error)
                {
                    SendNewLogMessage(error.ToString(), LogMessageType.Error);
                }
                return DateTime.Now;
            }
        }

        /// <summary>
        /// Data of Options
        /// </summary>
        public OptionMarketData OptionMarketData
        {
            get { return _optionMarketData; }
        }

        private OptionMarketData _optionMarketData = new OptionMarketData();

        /// <summary>
        /// Data of Funding
        /// </summary>
        public Funding Funding
        {
            get { return _funding; }
        }

        private Funding _funding = new Funding();

        /// <summary>
        /// Volume24h
        /// </summary>
        public SecurityVolumes SecurityVolumes
        {
            get { return _securityVolumes; }
        }

        private SecurityVolumes _securityVolumes = new SecurityVolumes();
              
        #endregion

        #region Orders

        /// <summary>
        /// execute order
        /// </summary>
        public void OrderExecute(Order order, bool isEmulator = false)
        {
            try
            {
                if (_myServer == null)
                {
                    return;
                }

                if (string.IsNullOrEmpty(order.ServerName))
                {
                    order.ServerName = this.ServerFullName;
                }

                if (_myServer.ServerStatus == ServerConnectStatus.Disconnect)
                {
                    SendNewLogMessage(OsLocalization.Market.Message2, LogMessageType.Error);
                    return;
                }

                if (StartProgram == StartProgram.IsTester ||
                    StartProgram == StartProgram.IsOsOptimizer)
                {
                    order.TimeFrameInTester = TimeFrameBuilder.TimeFrame;
                }

                order.SecurityNameCode = SecurityName;
                order.SecurityClassCode = SecurityClass;
                order.PortfolioNumber = PortfolioName;
                order.ServerType = ServerType;
                order.TimeCreate = MarketTime;

                if (StartProgram != StartProgram.IsTester &&
                    StartProgram != StartProgram.IsOsOptimizer &&
                    (EmulatorIsOn
                    || _myServer.ServerType == ServerType.Finam
                    || isEmulator))
                {
                    if (_emulator != null)
                    {
                        _emulator.OrderExecute(order);
                    }
                }
                else
                {
                    _myServer.ExecuteOrder(order);
                }
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        /// <summary>
        /// cancel order
        /// </summary>
        public void OrderCancel(Order order)
        {
            try
            {
                if (_myServer == null)
                {
                    return;
                }
                if (string.IsNullOrEmpty(order.SecurityNameCode))
                {
                    order.SecurityNameCode = SecurityName;
                }

                if (string.IsNullOrEmpty(order.PortfolioNumber))
                {
                    order.PortfolioNumber = PortfolioName;
                }

                if (_myServer.ServerStatus == ServerConnectStatus.Disconnect)
                {
                    SendNewLogMessage(OsLocalization.Market.Message99, LogMessageType.Error);
                    return;
                }

                if (EmulatorIsOn
                    || _myServer.ServerType == ServerType.Finam
                    || order.SecurityNameCode == SecurityName + " TestPaper")
                {
                    if (_emulator != null)
                    {
                        _emulator.OrderCancel(order);
                    }
                }
                else
                {
                    _myServer.CancelOrder(order);
                }
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        /// <summary>
        /// Order price change
        /// </summary>
        /// <param name="order">An order that will have a new price</param>
        /// <param name="newPrice">New price</param>
        public void ChangeOrderPrice(Order order, decimal newPrice)
        {
            if (order == null)
            {
                return;
            }

            try
            {
                if (_myServer == null)
                {
                    return;
                }

                if (_myServer.ServerStatus == ServerConnectStatus.Disconnect)
                {
                    SendNewLogMessage(OsLocalization.Market.Message2, LogMessageType.Error);
                    return;
                }

                if (order.Volume == order.VolumeExecute
                    || order.State == OrderStateType.Done
                    || order.State == OrderStateType.Fail)
                {
                    return;
                }

                if (EmulatorIsOn)
                {
                    if (_emulator.ChangeOrderPrice(order, newPrice))
                    {
                        if (OrderChangeEvent != null)
                        {
                            OrderChangeEvent(order);
                        }
                    }
                }
                else
                {
                    if (IsCanChangeOrderPrice == false)
                    {
                        SendNewLogMessage(OsLocalization.Trader.Label373, LogMessageType.Error);
                        return;
                    }

                    _myServer.ChangeOrderPrice(order, newPrice);
                }
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        /// <summary>
        /// incoming event: need to cancel the order
        /// </summary>
        private void ServerMaster_RevokeOrderToEmulatorEvent(Order order)
        {
            if (order.SecurityNameCode != SecurityName + " TestPaper"
                && order.SecurityNameCode != SecurityName)
            {
                return;
            }

            if (IsConnected == false
               || IsReadyToTrade == false)
            {
                SendNewLogMessage(OsLocalization.Trader.Label191, LogMessageType.Error);
                return;
            }

            OrderCancel(order);
        }

        /// <summary>
        /// upload the order to the repository
        /// </summary>
        /// <param name="order"></param>
        public void LoadOrderInOrderStorage(Order order)
        {
            ServerMaster.InsertOrder(order);
        }

        public void CheckEmulatorExecution(decimal price)
        {
            if(EmulatorIsOn == false)
            {
                return;
            }

            _emulator.ProcessBidAsk(price, price);
        }

        #endregion

        #region Events

        /// <summary>
        /// order are changed
        /// </summary>
        public event Action<Order> OrderChangeEvent;

        /// <summary>
        /// an attempt to revoke the order ended in an error
        /// </summary>
        public event Action<Order> CancelOrderFailEvent;

        /// <summary>
        /// another candle has closed
        /// </summary>
        public event Action<List<Candle>> NewCandlesChangeEvent;

        /// <summary>
        /// candles are changed
        /// </summary>
        public event Action<List<Candle>> LastCandlesChangeEvent;

        /// <summary>
        /// market depth is changed
        /// </summary>
        public event Action<MarketDepth> GlassChangeEvent;

        /// <summary>
        /// myTrade are changed
        /// </summary>
        public event Action<MyTrade> MyTradeEvent;

        /// <summary>
        /// new additional market data event
        /// </summary>
        public event Action<OptionMarketData> AdditionalDataEvent;

        /// <summary>
        /// new trade in the trades feed
        /// </summary>
        public event Action<List<Trade>> TickChangeEvent;

        /// <summary>
        /// bid or ask is changed
        /// </summary>
        public event Action<decimal, decimal> BestBidAskChangeEvent;

        /// <summary>
        /// testing finished
        /// </summary>
        public event Action TestOverEvent;

        /// <summary>
        /// testing started
        /// </summary>
        public event Action TestStartEvent;

        /// <summary>
        /// server time is changed
        /// </summary>
        public event Action<DateTime> TimeChangeEvent;

        /// <summary>
        /// connector is starting to reconnect
        /// </summary>
        public event Action<string, TimeFrame, TimeSpan, string, string> ConnectorStartedReconnectEvent;

        /// <summary>
        /// security for connector defined
        /// </summary>
        public event Action<Security> SecuritySubscribeEvent;

        /// <summary>
        /// portfolio on exchange changed
        /// </summary>
        public event Action<Portfolio> PortfolioOnExchangeChangedEvent;

        /// <summary>
        /// funding data is changed
        /// </summary>
        public event Action<Funding> FundingChangedEvent;

        /// <summary>
        /// volumes 24h data is changed
        /// </summary>
        public event Action<SecurityVolumes> NewVolume24hChangedEvent;

        #endregion

        #region Log

        /// <summary>
        /// send new message to up
        /// </summary>
        public void SendNewLogMessage(string message, LogMessageType type)
        {
            if (LogMessageEvent != null)
            {
                LogMessageEvent(message, type);
            }
            else if (type == LogMessageType.Error)
            { // if nobody is subscribed to us and there is an error in the log / если на нас никто не подписан и в логе ошибка
                System.Windows.MessageBox.Show(message);
            }
        }

        /// <summary>
        /// outgoing log message
        /// </summary>
        public event Action<string, LogMessageType> LogMessageEvent;

        #endregion
    }

    /// <summary>
    /// connector work type
    /// </summary>
    public enum ConnectorWorkType
    {
        /// <summary>
        /// real connection
        /// </summary>
        Real,

        /// <summary>
        /// test trading
        /// </summary>
        Tester
    }
}