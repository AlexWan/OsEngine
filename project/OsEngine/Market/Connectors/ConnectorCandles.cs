/*
 *Your rights to use the code are governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 *Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Threading;
using OsEngine.Alerts;
using OsEngine.Entity;
using OsEngine.Language;
using OsEngine.Logging;
using OsEngine.Market.Servers;
using OsEngine.Market.Servers.Optimizer;
using OsEngine.Market.Servers.Tester;

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
        // service
        // сервис

        /// <summary>
        /// constructor
        /// конструктор
        /// </summary>
        /// <param name="name"> bot name / имя робота </param>
        /// <param name="startProgram"> program that created the bot which created this connection / программа создавшая робота который создал это подключение </param>
        public ConnectorCandles(string name, StartProgram startProgram)
        {
            _name = name;
            StartProgram = startProgram;

            TimeFrameBuilder = new TimeFrameBuilder(_name, startProgram);
            ServerType = ServerType.None;
           

            if (StartProgram != StartProgram.IsOsOptimizer)
            {
                _canSave = true;
                Load();
                _emulator = new OrderExecutionEmulator();
                _emulator.MyTradeEvent += ConnectorBot_NewMyTradeEvent;
                _emulator.OrderChangeEvent += ConnectorBot_NewOrderIncomeEvent;
            }

            if (!string.IsNullOrWhiteSpace(NamePaper))
            {
                _subscrabler = new Thread(Subscrable);
                _subscrabler.CurrentCulture = new CultureInfo("ru-RU");
                _subscrabler.Name = "ConnectorSubscrableThread_" + UniqName;
                _subscrabler.IsBackground = true;
                _subscrabler.Start();
            }

            if(StartProgram == StartProgram.IsTester)
            {
                PortfolioName = "GodMode";
            }
        }

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
                    _namePaper = reader.ReadLine();
                    Enum.TryParse(reader.ReadLine(), true, out ServerType);

                    reader.Close();
                }
            }
            catch
            {
                // ignore
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
        /// save object settings in file
        /// сохранить настройки объекта в файл
        /// </summary>
        public void Save()
        {
            if (_canSave == false)
            {
                return;
            }
            try
            {
                using (StreamWriter writer = new StreamWriter(@"Engine\" + _name + @"ConnectorPrime.txt", false))
                {
                    writer.WriteLine(PortfolioName);
                    writer.WriteLine(EmulatorIsOn);
                    writer.WriteLine(NamePaper);
                    writer.WriteLine(ServerType);

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
            TimeFrameBuilder.Delete();
            if (File.Exists(@"Engine\" + _name + @"ConnectorPrime.txt"))
            {
                File.Delete(@"Engine\" + _name + @"ConnectorPrime.txt");
            }

            if (_mySeries != null)
            {
                _mySeries.Stop();
                _mySeries.Clear();
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
                    AlertMessageSimpleUi uiMessage = new AlertMessageSimpleUi(OsLocalization.Market.Message1);
                    uiMessage.Show();
                    return;
                }

                ConnectorCandlesUi ui = new ConnectorCandlesUi(this);
                ui.IsCanChangeSaveTradesInCandles(canChangeSettingsSaveCandlesIn);
                ui.LogMessageEvent += SendNewLogMessage;
                ui.ShowDialog();
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        private bool _lastHardReconnectOver = true;

        /// <summary>
        /// принудительное переподключение
        /// </summary>
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
                _mySeries = null;
            }

            Reconnect();

            _lastHardReconnectOver = true;
        }

        /// <summary>
        /// name of bot that owns the connector
        /// имя робота которому принадлежит коннектор
        /// </summary>
        private string _name;

        public string UniqName
        {
            get { return _name; }
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
        public string NamePaper
        {
            get { return _namePaper; }
            set
            {
                if (value != _namePaper)
                {
                    _namePaper = value;
                    Save();
                    Reconnect();
                }
            }
        }
        private string _namePaper;

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
                        return _myServer.GetSecurityForName(_namePaper);
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
        /// object preserving settings for building candles
        /// объект сохраняющий в себе настройки для построения свечек
        /// </summary>
        public TimeFrameBuilder TimeFrameBuilder;

        /// <summary>
        /// тип комиссии для позиций
        /// </summary>
        public ComissionType ComissionType;

        /// <summary>
        /// размер комиссии
        /// </summary>
        public decimal ComissionValue;

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
                Reconnect();
            }
            get { return TimeFrameBuilder.CandleMarketDataType; }
        }

        /// <summary>
        /// method of creating candles: from ticks or from depths 
        /// способ создания свечей: из тиков или из стаканов
        /// </summary>
        public CandleCreateMethodType CandleCreateMethodType
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
        /// period storage for delta
        /// хранилище периодов для дельты
        /// </summary>
        public decimal DeltaPeriods
        {
            get { return TimeFrameBuilder.DeltaPeriods; }
            set
            {
                if (value == TimeFrameBuilder.DeltaPeriods)
                {
                    return;
                }
                TimeFrameBuilder.DeltaPeriods = value;
                Reconnect();
            }
        }

        /// <summary>
        /// movement is required to candle close when renko candle mode is selected
        /// движение необходимое для закрытия свечи, когда выбран режим свечей ренко
        /// </summary>
        public decimal RencoPunktsToCloseCandleInRencoType
        {
            get { return TimeFrameBuilder.RencoPunktsToCloseCandleInRencoType; }
            set
            {
                if (value != TimeFrameBuilder.RencoPunktsToCloseCandleInRencoType)
                {
                    TimeFrameBuilder.RencoPunktsToCloseCandleInRencoType = value;
                    Reconnect();
                }
            }
        }

        /// <summary>
        /// shows whether we are building candle shadows when renko mode is selected. true means "to build" 
        /// строим ли мы тени у свечи когда выбран ренко. true - строим
        /// </summary>
        public bool RencoIsBuildShadows
        {
            get { return TimeFrameBuilder.RencoIsBuildShadows; }
            set
            {
                TimeFrameBuilder.RencoIsBuildShadows = value;
                Reconnect();
            }
        }

        /// <summary>
        /// range value of bars
        /// величина рейдж баров
        /// </summary>
        public decimal RangeCandlesPunkts
        {
            get { return TimeFrameBuilder.RangeCandlesPunkts; }
            set
            {
                if (value != TimeFrameBuilder.RangeCandlesPunkts)
                {
                    TimeFrameBuilder.RangeCandlesPunkts = value;
                    Reconnect();
                }
            }
        }

        /// <summary>
        /// minimum movement for reversible bars
        /// минимальное движение для риверсивных баров
        /// </summary>
        public decimal ReversCandlesPunktsMinMove
        {
            get { return TimeFrameBuilder.ReversCandlesPunktsMinMove; }
            set
            {
                if (value == TimeFrameBuilder.ReversCandlesPunktsMinMove)
                {
                    return;
                }
                TimeFrameBuilder.ReversCandlesPunktsMinMove = value;
                Reconnect();
            }
        }

        /// <summary>
        /// retracement value for reversible bars
        /// величина отката для риверсивных баров
        /// </summary>
        public decimal ReversCandlesPunktsBackMove
        {
            get { return TimeFrameBuilder.ReversCandlesPunktsBackMove; }
            set
            {
                if (value == TimeFrameBuilder.ReversCandlesPunktsBackMove)
                {
                    return;
                }
                TimeFrameBuilder.ReversCandlesPunktsBackMove = value;
                Reconnect();
            }
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

        /// <summary>
        /// candle timeframe in the form of connector' s Timespan
        /// ТаймФрейм свечек в виде TimeSpan на который подписан коннектор
        /// </summary>
        public TimeSpan TimeFrameTimeSpan
        {
            get { return TimeFrameBuilder.TimeFrameTimeSpan; }
        }

        /// <summary>
        /// candle series that collects candles  
        /// серия свечек которая собирает для нас свечки
        /// </summary>
        private CandleSeries _mySeries;

        /// <summary>
        /// connector's server type 
        /// тип сервера на который подписан коннектор
        /// </summary>
        public ServerType ServerType;

        public int ServerUid;

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
        /// shows whether execution of trades in emulation mode is enabled
        /// включено ли исполнение сделок в режиме эмуляции
        /// </summary>
        public bool EmulatorIsOn;

        /// <summary>
        /// shows whether non-trading intervals are needed
        /// нужно ли запрашивать неторговые интервалы
        /// </summary>
        public bool SetForeign
        {
            get { return TimeFrameBuilder.SetForeign; }
            set
            {
                if (TimeFrameBuilder.SetForeign == value)
                {
                    return;
                }
                TimeFrameBuilder.SetForeign = value;
                Reconnect();
            }
        }

        /// <summary>
        /// count of trades in candles with timeframe Ticks
        /// количество трейдов в свечах при таймФрейме Тики
        /// </summary>
        public int CountTradeInCandle
        {
            get { return TimeFrameBuilder.TradeCount; }
            set
            {
                if (value != TimeFrameBuilder.TradeCount)
                {
                    TimeFrameBuilder.TradeCount = value;
                    Reconnect();
                }
            }
        }

        /// <summary>
        /// volume is needed for closing candle when the candle closure mode is selected "by volume"
        /// объём необходимый для закрытия свечи, когда выбран режим закрытия свечи по объёму
        /// </summary>
        public decimal VolumeToCloseCandleInVolumeType
        {
            get { return TimeFrameBuilder.VolumeToCloseCandleInVolumeType; }
            set
            {
                if (value != TimeFrameBuilder.VolumeToCloseCandleInVolumeType)
                {
                    TimeFrameBuilder.VolumeToCloseCandleInVolumeType = value;
                    Reconnect();
                }
            }
        }

        /// <summary>
        /// emulator. It's for order execution in the emulation mode 
        /// эмулятор. В нём исполняются ордера в режиме эмуляции
        /// </summary>
        private readonly OrderExecutionEmulator _emulator;

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

                if (_myServer.LastStartServerTime.AddSeconds(60) > DateTime.Now)
                {
                    return false;
                }

                return true;
            }
        }

        // data subscription
        // подписка на данные 

        private DateTime _lastReconnectTime;

        private object _reconnectLocker = new object();

        /// <summary>
        /// reconnect candle downloading
        /// переподключить скачивание свечек
        /// </summary>
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
                            ConnectorStartedReconnectEvent(NamePaper, TimeFrame, TimeFrameTimeSpan, PortfolioName, ServerType);
                        }
                        return;
                    }
                    _lastReconnectTime = DateTime.Now;
                }

                if (_mySeries != null)
                {
                    _mySeries.СandleUpdeteEvent -= MySeries_СandleUpdeteEvent;
                    _mySeries.СandleFinishedEvent -= MySeries_СandleFinishedEvent;
                    _mySeries.Stop();

                    _mySeries = null;
                }

                Save();

                if (ConnectorStartedReconnectEvent != null)
                {
                    ConnectorStartedReconnectEvent(NamePaper, TimeFrame, TimeFrameTimeSpan, PortfolioName, ServerType);
                }

                if (_subscrabler == null)
                {
                    try
                    {
                        _subscrabler = new Thread(Subscrable);
                        _subscrabler.CurrentCulture = new CultureInfo("ru-RU");
                        _subscrabler.IsBackground = true;
                        _subscrabler.Name = "ConnectorSubscrableThread_" + UniqName;
                        _subscrabler.Start();
                    }
                    catch
                    {

                    }


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

        /// <summary>
        /// thread for candle subscription
        /// поток занимающийся подпиской на свечи
        /// </summary>
        private Thread _subscrabler;

        /// <summary>
        /// locker that blocks multi-threaded access to method Subscrable
        /// локер запрещающий многопоточный доступ к Subscrable
        /// </summary>
        private object _subscrableLocker = new object();

        private bool _neadToStopThread;

        /// <summary>
        /// subscribe to receive candle
        /// подписаться на получение свечек
        /// </summary>
        private void Subscrable()
        {
            try
            {
                while (true)
                {
                    Thread.Sleep(50);

                    if (_neadToStopThread)
                    {
                        return;
                    }

                    if (ServerType == ServerType.None ||
                        string.IsNullOrWhiteSpace(NamePaper))
                    {
                        continue;
                    }

                    List<IServer> servers = ServerMaster.GetServers();

                    if (servers == null)
                    {
                        if (ServerType != ServerType.None)
                        {
                            ServerMaster.SetNeedServer(ServerType);
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
                            ServerMaster.SetNeedServer(ServerType);
                        }
                        continue;
                    }
                    else
                    {
                        SubscribleOnServer(_myServer);

                        if (_myServer.ServerType == ServerType.Tester)
                        {
                            ((TesterServer)_myServer).TestingEndEvent -= ConnectorReal_TestingEndEvent;
                            ((TesterServer)_myServer).TestingEndEvent += ConnectorReal_TestingEndEvent;
                        }
                    }

                    Thread.Sleep(50);

                    ServerConnectStatus stat = _myServer.ServerStatus;

                    if (stat != ServerConnectStatus.Connect)
                    {
                        continue;
                    }
                    lock (_subscrableLocker)
                    {
                        if (_mySeries == null)
                        {
                            while (_mySeries == null)
                            {
                                if (_neadToStopThread)
                                {
                                    return;
                                }

                                Thread.Sleep(100);
                                _mySeries = _myServer.StartThisSecurity(_namePaper, TimeFrameBuilder);

                                if (_mySeries == null &&
                                    _myServer.ServerType == ServerType.Optimizer &&
                                    ((OptimizerServer)_myServer).NumberServer != ServerUid)
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
                            _subscrabler = null;
                        }
                    }

                    _subscrabler = null;

                    if (SecuritySubscribeEvent != null)
                    {
                        SecuritySubscribeEvent(Security);
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

            server.NewBidAscIncomeEvent += ConnectorBotNewBidAscIncomeEvent;
            server.NewMyTradeEvent += ConnectorBot_NewMyTradeEvent;
            server.NewOrderIncomeEvent += ConnectorBot_NewOrderIncomeEvent;
            server.NewMarketDepthEvent += ConnectorBot_NewMarketDepthEvent;
            server.NewTradeEvent += ConnectorBot_NewTradeEvent;
            server.TimeServerChangeEvent += myServer_TimeServerChangeEvent;
            server.NeadToReconnectEvent += _myServer_NeadToReconnectEvent;
        }

        void _myServer_NeadToReconnectEvent()
        {
            Reconnect();
        }

        // incoming data
        // входящие данные

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

        /// <summary>
        /// the candle has just ended
        /// свеча только что завершилась
        /// </summary>
        void MySeries_СandleFinishedEvent(CandleSeries candleSeries)
        {
            try
            {
                if (NewCandlesChangeEvent != null)
                {
                    NewCandlesChangeEvent(Candles(true));
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
                if (LastCandlesChangeEvent != null)
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
                    namePaper.Name != NamePaper)
                {
                    return;
                }

                _bestBid = bestBid;
                _bestAsk = bestAsk;

                if (EmulatorIsOn || ServerType == ServerType.Finam)
                {
                    _emulator.ProcessBidAsc(_bestBid, _bestAsk, MarketTime);
                }

                if (BestBidAskChangeEvent != null)
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
                if (NamePaper != glass.SecurityNameCode)
                {
                    return;
                }

                if (GlassChangeEvent != null)
                {
                    GlassChangeEvent(glass);
                }

                if (glass.Bids.Count == 0 ||
                    glass.Asks.Count == 0)
                {
                    return;
                }

                _bestBid = glass.Bids[0].Price;
                _bestAsk = glass.Asks[0].Price;

                if (EmulatorIsOn)
                {
                    _emulator.ProcessBidAsc(_bestAsk, _bestBid, MarketTime);
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
                if (NamePaper == null || tradesList == null || tradesList.Count == 0)
                {
                    return;
                }
                else
                {
                    int count = tradesList.Count;
                    if (tradesList[count - 1] == null ||
                        tradesList[count - 1].SecurityNameCode != NamePaper)
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
                if (TickChangeEvent != null)
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
                if (TimeChangeEvent != null)
                {
                    TimeChangeEvent(time);
                }
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        // stored data
        // хранящиеся данные

        /// <summary>
        /// best price of buyer 
        /// лучшая цена покупателя
        /// </summary>
        private decimal _bestBid;

        /// <summary>
        /// best price of seller
        ///  лучшая цена продавца
        /// </summary>
        private decimal _bestAsk;

        // external data access interface
        // внешний интерфейс доступа к данным

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
                        return _myServer.GetAllTradesToSecurity(_myServer.GetSecurityForName(NamePaper));
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

        // forward orders
        // Пересылка ордеров

        /// <summary>
        /// execute order
        /// исполнить ордер
        /// </summary>
        public void OrderExecute(Order order)
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

                order.SecurityNameCode = NamePaper;
                order.PortfolioNumber = PortfolioName;
                order.ServerType = ServerType;
                order.TimeCreate = MarketTime;

                if (StartProgram != StartProgram.IsTester &&
                    StartProgram != StartProgram.IsOsOptimizer &&
                    (EmulatorIsOn || _myServer.ServerType == ServerType.Finam))
                {
                    _emulator.OrderExecute(order);
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
                order.SecurityNameCode = NamePaper;
                order.PortfolioNumber = PortfolioName;

                if (EmulatorIsOn || _myServer.ServerType == ServerType.Finam)
                {
                    _emulator.OrderCancel(order);
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

        // outgoing events
        // Исходящие события

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

        // message log
        // сообщения в лог 

        /// <summary>
        /// send new message to up
        /// выслать новое сообщение на верх
        /// </summary>
        private void SendNewLogMessage(string message, LogMessageType type)
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

