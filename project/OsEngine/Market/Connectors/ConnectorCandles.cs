﻿/*
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
            _needToStopThread = true;

            if (StartProgram != StartProgram.IsOsOptimizer)
            {
                TimeFrameBuilder.Delete();

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
                _mySeries.Stop();
                _mySeries.Clear();
                _mySeries.СandleUpdeteEvent -= MySeries_СandleUpdeteEvent;
                _mySeries.СandleFinishedEvent -= MySeries_СandleFinishedEvent;

                if (_myServer != null)
                {
                    _myServer.StopThisSecurity(_mySeries);
                }
                _mySeries = null;
            }

            if (_emulator != null)
            {
                _emulator.MyTradeEvent -= ConnectorBot_NewMyTradeEvent;
                _emulator.OrderChangeEvent -= ConnectorBot_NewOrderIncomeEvent;
            }

            if (_myServer != null)
            {
                _myServer.NewBidAscIncomeEvent -= ConnectorBotNewBidAscIncomeEvent;
                _myServer.NewMyTradeEvent -= ConnectorBot_NewMyTradeEvent;
                _myServer.NewOrderIncomeEvent -= ConnectorBot_NewOrderIncomeEvent;
                _myServer.NewMarketDepthEvent -= ConnectorBot_NewMarketDepthEvent;
                _myServer.NewTradeEvent -= ConnectorBot_NewTradeEvent;
                _myServer.TimeServerChangeEvent -= myServer_TimeServerChangeEvent;
                _myServer.NeedToReconnectEvent -= _myServer_NeedToReconnectEvent;
                _myServer = null;
            }
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

                ConnectorCandlesUi ui = new ConnectorCandlesUi(this);
                ui.IsCanChangeSaveTradesInCandles(canChangeSettingsSaveCandlesIn);
                ui.LogMessageEvent += SendNewLogMessage;
                ui.ShowDialog();
                ui.LogMessageEvent -= SendNewLogMessage;
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

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
                if (ServerType == ServerType.Lmax ||
                    ServerType == ServerType.Tester ||
                    ServerType == ServerType.BitMex)
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
        public ComissionType CommissionType;

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
                            ConnectorStartedReconnectEvent(SecurityName, TimeFrame, TimeFrameTimeSpan, PortfolioName, ServerType);
                        }
                        return;
                    }
                    _lastReconnectTime = DateTime.Now;
                }


                if (_mySeries != null)
                {
                    _mySeries.Stop();
                    _mySeries.Clear();
                    _mySeries.СandleUpdeteEvent -= MySeries_СandleUpdeteEvent;
                    _mySeries.СandleFinishedEvent -= MySeries_СandleFinishedEvent;

                    if (_myServer != null)
                    {
                        _myServer.StopThisSecurity(_mySeries);
                    }
                    _mySeries = null;
                }

                Save();

                if (ConnectorStartedReconnectEvent != null)
                {
                    ConnectorStartedReconnectEvent(SecurityName, TimeFrame, TimeFrameTimeSpan, PortfolioName, ServerType);
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
                _mySeries.СandleUpdeteEvent -= MySeries_СandleUpdeteEvent;
                _mySeries.СandleFinishedEvent -= MySeries_СandleFinishedEvent;

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
                            ServerMaster.SetServerToAutoConnection(ServerType);
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
                            _myServer = servers.Find(server => server.ServerType == ServerType);
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
                            ServerMaster.SetServerToAutoConnection(ServerType);
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
                        ((TesterServer)_myServer).TestingEndEvent -= ConnectorReal_TestingEndEvent;
                        ((TesterServer)_myServer).TestingEndEvent += ConnectorReal_TestingEndEvent;
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

                        _mySeries.СandleUpdeteEvent += MySeries_СandleUpdeteEvent;
                        _mySeries.СandleFinishedEvent += MySeries_СandleFinishedEvent;
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
            server.NewBidAscIncomeEvent -= ConnectorBotNewBidAscIncomeEvent;
            server.NewMyTradeEvent -= ConnectorBot_NewMyTradeEvent;
            server.NewOrderIncomeEvent -= ConnectorBot_NewOrderIncomeEvent;
            server.NewMarketDepthEvent -= ConnectorBot_NewMarketDepthEvent;
            server.NewTradeEvent -= ConnectorBot_NewTradeEvent;
            server.TimeServerChangeEvent -= myServer_TimeServerChangeEvent;
            server.NeedToReconnectEvent -= _myServer_NeedToReconnectEvent;
            server.PortfoliosChangeEvent -= Server_PortfoliosChangeEvent;
        }

        private void SubscribeOnServer(IServer server)
        {
            server.NewBidAscIncomeEvent -= ConnectorBotNewBidAscIncomeEvent;
            server.NewMyTradeEvent -= ConnectorBot_NewMyTradeEvent;
            server.NewOrderIncomeEvent -= ConnectorBot_NewOrderIncomeEvent;
            server.NewMarketDepthEvent -= ConnectorBot_NewMarketDepthEvent;
            server.NewTradeEvent -= ConnectorBot_NewTradeEvent;
            server.TimeServerChangeEvent -= myServer_TimeServerChangeEvent;
            server.NeedToReconnectEvent -= _myServer_NeedToReconnectEvent;
            server.PortfoliosChangeEvent -= Server_PortfoliosChangeEvent;

            if (NeedToLoadServerData)
            {
                server.NewMarketDepthEvent += ConnectorBot_NewMarketDepthEvent;
                server.NewBidAscIncomeEvent += ConnectorBotNewBidAscIncomeEvent;
                server.NewTradeEvent += ConnectorBot_NewTradeEvent;
                server.TimeServerChangeEvent += myServer_TimeServerChangeEvent;
                server.NewMyTradeEvent += ConnectorBot_NewMyTradeEvent;
                server.NewOrderIncomeEvent += ConnectorBot_NewOrderIncomeEvent;
                server.PortfoliosChangeEvent += Server_PortfoliosChangeEvent;
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
        void ConnectorReal_TestingEndEvent()
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

        /// <summary>
        /// time of the last completed candle
        /// </summary>
        private DateTime _timeLastEndCandle = DateTime.MinValue;

        /// <summary>
        /// the candle has just ended
        /// </summary>
        private void MySeries_СandleFinishedEvent(CandleSeries candleSeries)
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

                if (timeLastCandle == _timeLastEndCandle)
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
        private void MySeries_СandleUpdeteEvent(CandleSeries candleSeries)
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

        /// <summary>
        /// incoming my trade
        /// </summary>
        private void ConnectorBot_NewMyTradeEvent(MyTrade trade)
        {
            if (_myServer.ServerStatus != ServerConnectStatus.Connect)
            {
                return;
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
        private void ConnectorBotNewBidAscIncomeEvent(decimal bestBid, decimal bestAsk, Security namePaper)
        {
            try
            {
                if (namePaper == null ||
                    namePaper.Name != _securityName)
                {
                    return;
                }

                if (_bestBid == bestBid
                    && _bestAsk == bestAsk)
                {
                    return;
                }

                _bestBid = bestBid;
                _bestAsk = bestAsk;

                if (EmulatorIsOn || ServerType == ServerType.Finam)
                {
                    if (_emulator != null)
                    {
                        _emulator.ProcessBidAsc(_bestBid, _bestAsk);
                    }
                }

                if (BestBidAskChangeEvent != null && EventsIsOn == true)
                {
                    BestBidAskChangeEvent(bestBid, bestAsk);
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
                    bestBid = glass.Bids[0].Price;
                }

                decimal bestAsk = 0;
                
                if(glass.Asks!= null &&
                    glass.Asks.Count > 0)
                {
                    bestAsk = glass.Asks[0].Price;
                }
              

                if (EmulatorIsOn)
                {
                    if (_emulator != null)
                    {
                        _emulator.ProcessBidAsc(bestAsk, bestBid);
                    }
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

                if (_securityName == null || tradesList == null || tradesList.Count == 0)
                {
                    return;
                }
                else
                {
                    int count = tradesList.Count;
                    if (tradesList[count - 1] == null ||
                        tradesList[count - 1].SecurityNameCode != _securityName)
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

        #endregion

        #region Events

        /// <summary>
        /// order are changed
        /// </summary>
        public event Action<Order> OrderChangeEvent;

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
        /// server time is changed
        /// </summary>
        public event Action<DateTime> TimeChangeEvent;

        /// <summary>
        /// connector is starting to reconnect
        /// </summary>
        public event Action<string, TimeFrame, TimeSpan, string, ServerType> ConnectorStartedReconnectEvent;

        /// <summary>
        /// security for connector defined
        /// </summary>
        public event Action<Security> SecuritySubscribeEvent;

        /// <summary>
        /// portfolio on exchange changed
        /// </summary>
        public event Action<Portfolio> PortfolioOnExchangeChangedEvent;

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