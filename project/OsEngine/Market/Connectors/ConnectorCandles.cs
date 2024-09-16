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
    /// класс предоставляющий универсальный интерфейс для подключения к серверам биржи для роботов, 
    /// терминалов и вкладок, которые могут торговать
    /// </summary>
    public class ConnectorCandles
    {
        #region Service code

        /// <summary>
        /// constructor
        /// конструктор
        /// </summary>
        /// <param name="name"> bot name / имя робота </param>
        /// <param name="startProgram"> program that created the bot which created this connection / программа создавшая робота который создал это подключение </param>
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
                Task.Run(Subscrable);
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
        /// программа создавшая робота который создал это подключение
        /// </summary>
        public StartProgram StartProgram;

        /// <summary>
        /// shows whether it is possible to save settings
        /// можно ли сохранять настройки
        /// </summary>
        private bool _canSave;

        /// <summary>
        /// upload
        /// загрузить
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
        /// save object settings in file
        /// сохранить настройки объекта в файл
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
        /// delete object settings
        /// удалить настройки объекта
        /// </summary>
        public void Delete()
        {
            if (StartProgram != StartProgram.IsOsOptimizer)
            {
                TimeFrameBuilder.Delete();

                if (File.Exists(@"Engine\" + _name + @"ConnectorPrime.txt"))
                {
                    File.Delete(@"Engine\" + _name + @"ConnectorPrime.txt");
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
                _myServer.NeadToReconnectEvent -= _myServer_NeadToReconnectEvent;
                _myServer = null;
            }

            _neadToStopThread = true;
        }

        /// <summary>
        /// show settings window
        /// показать окно настроек
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
        /// имя робота которому принадлежит коннектор
        /// </summary>
        public string UniqName
        {
            get { return _name; }
        }
        private string _name;

        /// <summary>
        /// trade server
        /// сервер через который идёт торговля
        /// </summary>
        public IServer MyServer
        {
            get { return _myServer; }
        }
        private IServer _myServer;

        /// <summary>
        /// connector's server type 
        /// тип сервера на который подписан коннектор
        /// </summary>
        public ServerType ServerType;

        public int ServerUid;

        /// <summary>
        /// shows whether connector is connected to download data 
        /// подключен ли коннектор на скачивание данных
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
        /// connector is ready to send Orders / 
        /// готов ли коннектор к выставленю заявок
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
        /// connector's account number
        /// номер счёта к которому подключен коннектор
        /// </summary>
        public string PortfolioName;

        /// <summary>
        /// connector's account
        /// счёт к которому подключен коннектор
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
        /// Название бумаги к которой подключен коннектор
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
        /// connector's security class name
        /// класс бумаги к которой подключен коннектор
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
        /// connector's security
        /// бумага к которой подключен коннектор
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
        /// Does the server support market orders
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
        /// Does the server support order price change
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
        /// shows whether execution of trades in emulation mode is enabled
        /// включено ли исполнение сделок в режиме эмуляции
        /// </summary>
        public bool EmulatorIsOn;

        /// <summary>
        /// emulator. It's for order execution in the emulation mode 
        /// эмулятор. В нём исполняются ордера в режиме эмуляции
        /// </summary>
        private readonly OrderExecutionEmulator _emulator;

        /// <summary>
        /// включена ли подача событий на верх или нет
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
        /// тип комиссии для позиций
        /// </summary>
        public ComissionType ComissionType;

        /// <summary>
        /// размер комиссии
        /// </summary>
        public decimal ComissionValue;

        #endregion

        #region Candle series settings

        public CandleSeries CandleSeries
        {
            get { return _mySeries; }
        }

        /// <summary>
        /// candle series that collects candles  
        /// серия свечек которая собирает для нас свечки
        /// </summary>
        private CandleSeries _mySeries;

        /// <summary>
        /// object preserving settings for building candles
        /// объект сохраняющий в себе настройки для построения свечек
        /// </summary>
        public TimeFrameBuilder TimeFrameBuilder;

        /// <summary>
        /// method of creating candles: from ticks or from depths 
        /// способ создания свечей: из тиков или из стаканов
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
                    NeadToLoadServerData = true;
                }

                Reconnect();
            }
            get { return TimeFrameBuilder.CandleMarketDataType; }
        }

        /// <summary>
        /// method of creating candles: from ticks or from depths 
        /// способ создания свечей: из тиков или из стаканов
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
        /// cadles timeframe on which the connector is subscribed
        /// ТаймФрейм свечек на который подписан коннектор
        /// </summary>
        public TimeFrame TimeFrame
        {
            get { return TimeFrameBuilder.TimeFrame; }
            set
            {
                try
                {
                    if (value != TimeFrameBuilder.TimeFrame)
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
        /// ТаймФрейм свечек в виде TimeSpan на который подписан коннектор
        /// </summary>
        public TimeSpan TimeFrameTimeSpan
        {
            get { return TimeFrameBuilder.TimeFrameTimeSpan; }
        }

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
                    Task.Run(Subscrable);

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

        private bool _neadToStopThread;

        private object _myServerLocker = new object();

        private static int _aliveTasks = 0;

        private static string _aliveTasksArrayLocker = "aliveTasksArrayLocker";

        private bool _alreadCheckedInAliveTasksArray = false;

        private static int _tasksCountOnSubscruble = 0;

        private static string _tasksCountLocker = "_tasksCountOnLocker";

        private async void Subscrable()
        {
            try
            {
                _alreadCheckedInAliveTasksArray = false;

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
                            if (_alreadCheckedInAliveTasksArray == false)
                            {
                                _aliveTasks++;
                                _alreadCheckedInAliveTasksArray = true;
                            }

                            if (millisecondsToDelay < 500)
                            {
                                millisecondsToDelay = 500;
                            }
                        }

                        await Task.Delay(millisecondsToDelay);
                    }

                    if (_neadToStopThread)
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

                    SubscribleOnServer(_myServer);

                    if (_myServer.ServerType == ServerType.Tester)
                    {
                        ((TesterServer)_myServer).TestingEndEvent -= ConnectorReal_TestingEndEvent;
                        ((TesterServer)_myServer).TestingEndEvent += ConnectorReal_TestingEndEvent;
                    }

                    if (_mySeries == null)
                    {
                        while (_mySeries == null)
                        {
                            if (_neadToStopThread)
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

                            if (_tasksCountOnSubscruble > 20)
                            {
                                continue;
                            }

                            lock (_tasksCountLocker)
                            {
                                _tasksCountOnSubscruble++;
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
                                _tasksCountOnSubscruble--;
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
                                        UnSubscribleOnServer(_myServer);
                                        _myServer = servers[i];
                                        SubscribleOnServer(_myServer);
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

        private void UnSubscribleOnServer(IServer server)
        {
            server.NewBidAscIncomeEvent -= ConnectorBotNewBidAscIncomeEvent;
            server.NewMyTradeEvent -= ConnectorBot_NewMyTradeEvent;
            server.NewOrderIncomeEvent -= ConnectorBot_NewOrderIncomeEvent;
            server.NewMarketDepthEvent -= ConnectorBot_NewMarketDepthEvent;
            server.NewTradeEvent -= ConnectorBot_NewTradeEvent;
            server.TimeServerChangeEvent -= myServer_TimeServerChangeEvent;
            server.NeadToReconnectEvent -= _myServer_NeadToReconnectEvent;
            server.PortfoliosChangeEvent -= Server_PortfoliosChangeEvent;
        }

        private void SubscribleOnServer(IServer server)
        {
            server.NewBidAscIncomeEvent -= ConnectorBotNewBidAscIncomeEvent;
            server.NewMyTradeEvent -= ConnectorBot_NewMyTradeEvent;
            server.NewOrderIncomeEvent -= ConnectorBot_NewOrderIncomeEvent;
            server.NewMarketDepthEvent -= ConnectorBot_NewMarketDepthEvent;
            server.NewTradeEvent -= ConnectorBot_NewTradeEvent;
            server.TimeServerChangeEvent -= myServer_TimeServerChangeEvent;
            server.NeadToReconnectEvent -= _myServer_NeadToReconnectEvent;
            server.PortfoliosChangeEvent -= Server_PortfoliosChangeEvent;

            if (NeadToLoadServerData)
            {
                server.NewMarketDepthEvent += ConnectorBot_NewMarketDepthEvent;
                server.NewBidAscIncomeEvent += ConnectorBotNewBidAscIncomeEvent;
                server.NewTradeEvent += ConnectorBot_NewTradeEvent;
                server.TimeServerChangeEvent += myServer_TimeServerChangeEvent;
                server.NewMyTradeEvent += ConnectorBot_NewMyTradeEvent;
                server.NewOrderIncomeEvent += ConnectorBot_NewOrderIncomeEvent;
                server.PortfoliosChangeEvent += Server_PortfoliosChangeEvent;
            }

            server.NeadToReconnectEvent += _myServer_NeadToReconnectEvent;
        }

        public bool NeadToLoadServerData = true;

        void _myServer_NeadToReconnectEvent()
        {
            Reconnect();
        }

        #endregion

        #region Incoming data

        /// <summary>
        /// test finished
        /// тест завершился
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

        DateTime _timeLastEndCandle = DateTime.MinValue;

        /// <summary>
        /// the candle has just ended
        /// свеча только что завершилась
        /// </summary>
        void MySeries_СandleFinishedEvent(CandleSeries candleSeries)
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
        /// свеча обновилась
        /// </summary>
        void MySeries_СandleUpdeteEvent(CandleSeries candleSeries)
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
        /// входящий ордер
        /// </summary>
        void ConnectorBot_NewOrderIncomeEvent(Order order)
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
        /// входящая моя сделка
        /// </summary>
        void ConnectorBot_NewMyTradeEvent(MyTrade trade)
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
        /// входящие лучшие бид с аском
        /// </summary>
        void ConnectorBotNewBidAscIncomeEvent(decimal bestBid, decimal bestAsk, Security namePaper)
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
        /// входящий стакан
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
        /// входящие трейды
        /// </summary>
        void ConnectorBot_NewTradeEvent(List<Trade> tradesList)
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
        /// incoming night server time
        /// входящее новое время сервера
        /// </summary>
        void myServer_TimeServerChangeEvent(DateTime time)
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
        /// conector's ticks
        /// тики коннектора
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
        /// свечи коннектора
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
        /// take the best price of seller in the depth
        /// взять лучшую цену продавца в стакане
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
        /// take the best price of buyer in the depth
        /// взять лучшую цену покупателя в стакане
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
        /// take server time
        /// взять время сервера
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
        /// исполнить ордер
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
        /// отменить ордер
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

        public void LoadOrderInOrderStorage(Order order)
        {
            ServerMaster.InsertOrder(order);
        }

        #endregion

        #region Events

        /// <summary>
        /// orders are changed
        /// изменились Ордера
        /// </summary>
        public event Action<Order> OrderChangeEvent;

        /// <summary>
        /// candles are changed
        /// изменились Свечки
        /// </summary>
        public event Action<List<Candle>> NewCandlesChangeEvent;

        /// <summary>
        /// изменились Свечки
        /// </summary>
        public event Action<List<Candle>> LastCandlesChangeEvent;

        /// <summary>
        /// depth is changed
        /// изменился Стакан
        /// </summary>
        public event Action<MarketDepth> GlassChangeEvent;

        /// <summary>
        /// my trades are changed
        /// изменились мои сделки
        /// </summary>
        public event Action<MyTrade> MyTradeEvent;

        /// <summary>
        /// tick is changed
        /// изменился тик
        /// </summary>
        public event Action<List<Trade>> TickChangeEvent;

        /// <summary>
        /// bid or ask is changed
        /// изменился бид с аском
        /// </summary>
        public event Action<decimal, decimal> BestBidAskChangeEvent;

        /// <summary>
        /// testing finished
        /// завершилось тестирование
        /// </summary>
        public event Action TestOverEvent;

        /// <summary>
        /// server time is changed
        /// изменилось время сервера
        /// </summary>
        public event Action<DateTime> TimeChangeEvent;

        /// <summary>
        /// connector is starting to reconnect
        /// коннектор начинает процедуру переподключения
        /// </summary>
        public event Action<string, TimeFrame, TimeSpan, string, ServerType> ConnectorStartedReconnectEvent;

        /// <summary>
        /// security for connector defined
        /// бумага для коннектора определена
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
        /// выслать новое сообщение на верх
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
        /// исходящее сообщение для лога
        /// </summary>
        public event Action<string, LogMessageType> LogMessageEvent;

        #endregion
    }

    /// <summary>
    /// connector work type
    /// тип работы коннектора
    /// </summary>
    public enum ConnectorWorkType
    {
        /// <summary>
        /// real connection
        /// реальное подключение
        /// </summary>
        Real,

        /// <summary>
        /// test trading
        /// тестовая торговля
        /// </summary>
        Tester
    }
}