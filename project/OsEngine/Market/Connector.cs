/*
 *Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Threading;
using OsEngine.Alerts;
using OsEngine.Entity;
using OsEngine.Logging;
using OsEngine.Market.Servers;
using OsEngine.Market.Servers.Tester;

namespace OsEngine.Market
{

    /// <summary>
    /// класс предоставляющий универсальный интерфейс для подключения к серверам биржи
    /// </summary>
    public class Connector
    {
// сервис

        /// <summary>
        /// конструктор
        /// </summary>
        /// <param name="name">имя робота</param>
        public Connector(string name)
        {
            _name = name;

            _timeFrameBuilder = new TimeFrameBuilder(_name);
            ServerType = ServerType.Unknown;
            Load();
            _canSave = true;

            if (!string.IsNullOrWhiteSpace(NamePaper))
            {
                _subscrabler = new Thread(Subscrable);
                _subscrabler.CurrentCulture = new CultureInfo("ru-RU");
                _subscrabler.Name = "ConnectorSubscrableThread_" + UniqName; 
                _subscrabler.IsBackground = true;
                _subscrabler.Start();
            }

            _emulator = new OrderExecutionEmulator();
            _emulator.MyTradeEvent += ConnectorBot_NewMyTradeEvent;
            _emulator.OrderChangeEvent += ConnectorBot_NewOrderIncomeEvent;
        }

        /// <summary>
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
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(),LogMessageType.Error);
            }
        }

        /// <summary>
        /// можно ли сохранять настройки
        /// </summary>
        private bool _canSave;

        /// <summary>
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
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        /// <summary>
        /// удалить настройки объекта
        /// </summary>
        public void Delete()
        {
            _timeFrameBuilder.Delete();
            if (File.Exists(@"Engine\" + _name + @"ConnectorPrime.txt"))
            {
                File.Delete(@"Engine\" + _name + @"ConnectorPrime.txt");
            }

            if (_mySeries != null)
            {
                _mySeries.Stop();
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
        /// показать окно настроек
        /// </summary>
        public void ShowDialog()
        {
            try
            {
                if (ServerMaster.GetServers() == null||
                    ServerMaster.GetServers().Count == 0)
                {
                    AlertMessageSimpleUi uiMessage = new AlertMessageSimpleUi("Ни одного соединения с биржей не найдено! " +
                                                        " Нажмите на кнопку ^Сервер^ ");
                    uiMessage.Show();
                    return;
                }

                ConnectorUi ui = new ConnectorUi(this);
                ui.LogMessageEvent += SendNewLogMessage;
                ui.ShowDialog();
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        /// <summary>
        /// имя робота которому принадлежит коннектор
        /// </summary>
        private string _name;

        public string UniqName
        {
            get { return _name; }
        }

        /// <summary>
        /// номер счёта к которому подключен коннектор
        /// </summary>
        public string PortfolioName;

        /// <summary>
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
        /// Название бумаги к которой подключен коннектор
        /// </summary>
        public string NamePaper
        {
            get { return _namePaper; }
            set
            { 
                if ( value != _namePaper)
                {
                    _namePaper = value;
                    Save();
                    Reconnect();
                }
            }
        }
        private string _namePaper;

        /// <summary>
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
        /// объект сохраняющий в себе настройки для построения свечек
        /// </summary>
        private TimeFrameBuilder _timeFrameBuilder;

        /// <summary>
        /// способ создания свечей: из тиков или из стаканов
        /// </summary>
        public CandleMarketDataType CandleMarketDataType
        {
            set
            {
                if (value == _timeFrameBuilder.CandleMarketDataType)
                {
                    return;
                }
                _timeFrameBuilder.CandleMarketDataType = value;
                Reconnect();
            }
            get { return _timeFrameBuilder.CandleMarketDataType; }
        }

        /// <summary>
        /// способ создания свечей: из тиков или из стаканов
        /// </summary>
        public CandleCreateMethodType CandleCreateMethodType
        {
            set
            {
                if (value == _timeFrameBuilder.CandleCreateMethodType)
                {
                    return;
                }
                _timeFrameBuilder.CandleCreateMethodType = value;
                Reconnect();
            }
            get { return _timeFrameBuilder.CandleCreateMethodType; }
        }

        /// <summary>
        /// ТаймФрейм свечек на который подписан коннектор
        /// </summary>
        public TimeFrame TimeFrame
        {
            get { return _timeFrameBuilder.TimeFrame; }
            set
            {
                try
                {
                    if (value != _timeFrameBuilder.TimeFrame)
                    {
                        _timeFrameBuilder.TimeFrame = value;
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
        /// хранилище переодов для дельты
        /// </summary>
        public decimal DeltaPeriods
        {
            get { return _timeFrameBuilder.DeltaPeriods; }
            set
            {
                if (value == _timeFrameBuilder.DeltaPeriods)
                {
                    return;
                }
                _timeFrameBuilder.DeltaPeriods = value;
                Reconnect();
            }
        }

        /// <summary>
        /// движение необходимое для закрытия свечи, когда выбран режим свечей ренко
        /// </summary>
        public decimal RencoPunktsToCloseCandleInRencoType
        {
            get { return _timeFrameBuilder.RencoPunktsToCloseCandleInRencoType; }
            set
            {
                if (value != _timeFrameBuilder.RencoPunktsToCloseCandleInRencoType)
                {
                    _timeFrameBuilder.RencoPunktsToCloseCandleInRencoType = value;
                    Reconnect();
                }
            }
        }

        /// <summary>
        /// ТаймФрейм свечек в виде TimeSpan на который подписан коннектор
        /// </summary>
        public TimeSpan TimeFrameTimeSpan
        {
            get { return _timeFrameBuilder.TimeFrameTimeSpan; }
        }

        /// <summary>
        /// серия свечек которая собирает для нас свечки
        /// </summary>
        private CandleSeries _mySeries;

        /// <summary>
        /// тип сервера на который подписан коннектор
        /// </summary>
        public ServerType ServerType;

        /// <summary>
        /// сервер через который идёт торговля
        /// </summary>
        public IServer MyServer
        {
            get { return _myServer; }
        }
        private IServer _myServer;

        /// <summary>
        /// включено ли исполнение сделок в режиме эмуляции
        /// </summary>
        public bool EmulatorIsOn;

        /// <summary>
        /// нужно ли запрашивать неторговые интервалы
        /// </summary>
        public bool SetForeign
        {
            get { return _timeFrameBuilder.SetForeign; }
            set
            {
                if (_timeFrameBuilder.SetForeign == value)
                {
                    return;
                }
                _timeFrameBuilder.SetForeign = value;
                Reconnect();
            }
        }

        /// <summary>
        /// количество трейдов в свечах при таймФрейме Тики
        /// </summary>
        public int CountTradeInCandle
        {
            get { return _timeFrameBuilder.TradeCount; }
            set
            {
                if (value != _timeFrameBuilder.TradeCount)
                {
                    _timeFrameBuilder.TradeCount = value;
                    Reconnect();
                }
            }
        }

        /// <summary>
        /// объём необходимый для закрытия свечи, когда выбран режим закрытия свечи по объёму
        /// </summary>
        public decimal VolumeToCloseCandleInVolumeType
        {
            get { return _timeFrameBuilder.VolumeToCloseCandleInVolumeType; }
            set
            {
                if (value != _timeFrameBuilder.VolumeToCloseCandleInVolumeType)
                {
                    _timeFrameBuilder.VolumeToCloseCandleInVolumeType = value;
                    Reconnect();
                }
            }
        }

        /// <summary>
        /// эмулятор. В нём исполняются ордера в режиме эмуляции
        /// </summary>
        private readonly OrderExecutionEmulator _emulator;

        /// <summary>
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

// подписка на данные 

        /// <summary>
        /// переподключить скачивание свечек
        /// </summary>
        private void Reconnect()
        {
            try
            {
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
                    _subscrabler = new Thread(Subscrable);
                    _subscrabler.CurrentCulture = new CultureInfo("ru-RU");
                    _subscrabler.IsBackground = true;
                    _subscrabler.Name = "ConnectorSubscrableThread_" + UniqName; 
                    _subscrabler.Start();

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
        /// поток занимающийся подпиской на свечи
        /// </summary>
        private Thread _subscrabler;

        /// <summary>
        /// локер запрещающий многопоточный доступ к Subscrable
        /// </summary>
        private object _subscrableLocker = new object();

        private bool _neadToStopThread;

        /// <summary>
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

                    if (ServerType == ServerType.Unknown ||
                        string.IsNullOrWhiteSpace(NamePaper))
                    {
                        continue;
                    }

                    List<IServer> servers = ServerMaster.GetServers();

                    if (servers == null)
                    {
                        if (ServerType != ServerType.Unknown)
                        {
                            ServerMaster.SetNeedServer(ServerType);
                        }
                        continue;
                    }

                    try
                    {
                        _myServer = servers.Find(server => server.ServerType == ServerType);
                    }
                    catch
                    {
                        // ignore
                        continue;
                    }

                    if (_myServer == null)
                    {
                        if (ServerType != ServerType.Unknown)
                        {
                            ServerMaster.SetNeedServer(ServerType);
                        }
                        continue;
                    }
                    else
                    {
                        _myServer.NewBidAscIncomeEvent -= ConnectorBotNewBidAscIncomeEvent;
                        _myServer.NewMyTradeEvent -= ConnectorBot_NewMyTradeEvent;
                        _myServer.NewOrderIncomeEvent -= ConnectorBot_NewOrderIncomeEvent;
                        _myServer.NewMarketDepthEvent -= ConnectorBot_NewMarketDepthEvent;
                        _myServer.NewTradeEvent -= ConnectorBot_NewTradeEvent;
                        _myServer.TimeServerChangeEvent -= myServer_TimeServerChangeEvent;
                        _myServer.NeadToReconnectEvent -= _myServer_NeadToReconnectEvent;

                        _myServer.NewBidAscIncomeEvent += ConnectorBotNewBidAscIncomeEvent;
                        _myServer.NewMyTradeEvent += ConnectorBot_NewMyTradeEvent;
                        _myServer.NewOrderIncomeEvent += ConnectorBot_NewOrderIncomeEvent;
                        _myServer.NewMarketDepthEvent += ConnectorBot_NewMarketDepthEvent;
                        _myServer.NewTradeEvent += ConnectorBot_NewTradeEvent;
                        _myServer.TimeServerChangeEvent += myServer_TimeServerChangeEvent;
                        _myServer.NeadToReconnectEvent += _myServer_NeadToReconnectEvent;

                        if (ServerMaster.StartProgram == ServerStartProgramm.IsTester)
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
                                _mySeries = _myServer.StartThisSecurity(_namePaper, _timeFrameBuilder);
                            }


                            _mySeries.СandleUpdeteEvent += MySeries_СandleUpdeteEvent;
                            _mySeries.СandleFinishedEvent += MySeries_СandleFinishedEvent;
                            _subscrabler = null;
                        }
                    }

                    _subscrabler = null;
                    return;
                }
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        void _myServer_NeadToReconnectEvent()
        {

            Reconnect();
        }

// входящие данные

        /// <summary>
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

                _bestAsk = bestBid;
                _bestBid = bestAsk;

                if (EmulatorIsOn || ServerType == ServerType.Finam)
                {
                    _emulator.ProcessBidAsc(_bestAsk, _bestBid, MarketTime);
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
        /// входящие трейды
        /// </summary>
        void ConnectorBot_NewTradeEvent(List<Trade> tradesList)
        {
            try
            {
                if (NamePaper == null ||
                    tradesList == null ||
                    tradesList.Count == 0 ||
                    tradesList[tradesList.Count - 1] == null ||
                    tradesList[tradesList.Count - 1].SecurityNameCode != NamePaper)
                {
                    return;
                }
            }
            catch
            {
                // ошибка сдесь трудноуловимая. Кто поймёт что не так - молодец
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

// хранящиеся данные

        /// <summary>
        /// лучшая цена покупателя
        /// </summary>
        private decimal _bestBid;

        /// <summary>
        ///  лучшая цена продавца
        /// </summary>
        private decimal _bestAsk;

// внешний интерфейс доступа к данным

        /// <summary>
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
                if(onlyReady)
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

 // Пересылка ордеров

        /// <summary>
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
                    SendNewLogMessage("Попытка выставить ордер при выключенном соединении",LogMessageType.Error);
                    return;
                }

                order.SecurityNameCode = NamePaper;
                order.PortfolioNumber = PortfolioName;
                order.ServerType = ServerType;

                if (EmulatorIsOn || _myServer.ServerType == ServerType.Finam)
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
                    _myServer.CanselOrder(order);
                }
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

// Исходящие события

        /// <summary>
        ///  изменились Ордера
        /// </summary>
        public event Action<Order> OrderChangeEvent;

        /// <summary>
        /// изменились Свечки
        /// </summary>
        public event Action<List<Candle>> NewCandlesChangeEvent;

        /// <summary>
        /// изменились Свечки
        /// </summary>
        public event Action<List<Candle>> LastCandlesChangeEvent;

        /// <summary>
        /// изменился Стакан
        /// </summary>
        public event Action<MarketDepth> GlassChangeEvent;

        /// <summary>
        /// изменились мои сделки
        /// </summary>
        public event Action<MyTrade> MyTradeEvent;

        /// <summary>
        /// изменился тик
        /// </summary>
        public event Action<List<Trade>> TickChangeEvent;

        /// <summary>
        /// изменился бид с аском
        /// </summary>
        public event Action<decimal, decimal> BestBidAskChangeEvent;

        /// <summary>
        /// завершилось тестирование
        /// </summary>
        public event Action TestOverEvent;

        /// <summary>
        /// изменилось время сервера
        /// </summary>
        public event Action<DateTime> TimeChangeEvent;

        /// <summary>
        /// коннектор начинает процедуру переподключения
        /// </summary>
        public event Action<string, TimeFrame, TimeSpan, string, ServerType> ConnectorStartedReconnectEvent;

// сообщения в лог 

        /// <summary>
        /// выслать новое сообщение на верх
        /// </summary>
        private void SendNewLogMessage(string message, LogMessageType type)
        {
            if (LogMessageEvent != null)
            {
                LogMessageEvent(message, type);
            }
            else if (type == LogMessageType.Error)
            { // если на нас никто не подписан и в логе ошибка
                System.Windows.MessageBox.Show(message);
            }
        }

        /// <summary>
        /// исходящее сообщение для лога
        /// </summary>
        public event Action<string, LogMessageType> LogMessageEvent;
    }

    /// <summary>
    /// тип работы коннектора
    /// </summary>
    public enum ConnectorWorkType
    {
        /// <summary>
        /// реальное подключение
        /// </summary>
        Real,

        /// <summary>
        /// тестовая торговля
        /// </summary>
        Tester
    }
}

