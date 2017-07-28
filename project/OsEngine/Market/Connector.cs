/*
 *Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using System.Windows.Forms.Integration;
using OsEngine.Alerts;
using OsEngine.Entity;
using OsEngine.Logging;
using OsEngine.Market.Servers;
using OsEngine.Market.Servers.AstsBridge;
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

            TimeFrameBuilder = new TimeFrameBuilder(_name);
            ServerType = ServerType.Unknown;
            Load();
            _canSave = true;

            CreateGlass();

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
            TimeFrameBuilder.Delete();
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

            if (_subscrabler != null)
            {
                try
                {
                    if (_subscrabler.IsAlive)
                    {
                        _subscrabler.Abort();
                    }
                }
                catch 
                {
                    //SendNewLogMessage(error.ToString(), LogMessageType.Error);
                }
            }
        }

        /// <summary>
        /// показать окно настроек
        /// </summary>
        public void ShowDialog()
        {
            try
            {
                if (ServerMaster.GetServers() == null)
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

        public TimeFrameBuilder TimeFrameBuilder;

        /// <summary>
        /// способ создания свечей: из тиков или из стаканов
        /// </summary>
        public CandleSeriesCreateDataType CandleCreateType
        {
            set
            {
                if (value == TimeFrameBuilder.CandleCreateType)
                {
                    return;
                }
                TimeFrameBuilder.CandleCreateType = value;
                Reconnect();
            }
            get { return TimeFrameBuilder.CandleCreateType; }
        }

        /// <summary>
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
        /// ТаймФрейм свечек в виде TimeSpan на который подписан коннектор
        /// </summary>
        public TimeSpan TimeFrameTimeSpan
        {
            get { return TimeFrameBuilder.TimeFrameTimeSpan; }
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
        /// мой сервер
        /// </summary>
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
        /// эмулятор. В нём исполняются ордера в режиме эмуляции
        /// </summary>
        private readonly OrderExecutionEmulator _emulator;

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

                    _mySeries = null;
                }

                Save();

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

        /// <summary>
        /// подписаться на получение свечек
        /// </summary>
        private void Subscrable()
        {
            try
            {
                while (true)
                {
                    Thread.Sleep(500);

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

                    _myServer = servers.Find(server => server.ServerType == ServerType);

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

                        if (ServerMaster.IsTester)
                        {
                            ((TesterServer)_myServer).TestingEndEvent -= ConnectorReal_TestingEndEvent;
                            ((TesterServer)_myServer).TestingEndEvent += ConnectorReal_TestingEndEvent;
                        }
                    }

                    Thread.Sleep(500);

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
                                Thread.Sleep(5000);
                                _mySeries = _myServer.StartThisSecurity(_namePaper, TimeFrameBuilder);
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
                PaintBidAsk(bestBid, bestAsk);
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

                PaintMarketDepth(glass);
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
                if (tradesList == null || tradesList.Count == 0 || tradesList[tradesList.Count - 1] == null ||
                    tradesList[tradesList.Count - 1].SecurityNameCode != NamePaper)
                {
                    return;
                }

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

// прорисовка элементов управления

        /// <summary>
        /// область для размещения стакана
        /// </summary>
        private WindowsFormsHost _hostGlass;

        /// <summary>
        /// таблица стакана
        /// </summary>
        DataGridView _glassBox;

        /// <summary>
        /// элемент для отрисовки выбранной пользователем цены
        /// </summary>
        private System.Windows.Controls.TextBox _textBoxLimitPrice;

        /// <summary>
        /// последняя выбранная пользователем цена
        /// </summary>
        private decimal _lastSelectPrice;

        /// <summary>
        /// загрузить контролы в коннектор
        /// </summary>
        public void CreateGlass()
        {
            try
            {
                _glassBox = new DataGridView();
                _glassBox.AllowUserToOrderColumns = false;
                _glassBox.AllowUserToResizeRows = false;
                _glassBox.AllowUserToDeleteRows = false;
                _glassBox.AllowUserToAddRows = false;
                _glassBox.RowHeadersVisible = false;
                _glassBox.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
                _glassBox.MultiSelect = false;
                _glassBox.SelectionChanged += _glassBox_SelectionChanged;

                DataGridViewCellStyle style = new DataGridViewCellStyle();
                style.Alignment = DataGridViewContentAlignment.BottomRight;

                DataGridViewTextBoxCell cell0 = new DataGridViewTextBoxCell();
                cell0.Style = style;

                DataGridViewColumn column0 = new DataGridViewColumn();
                column0.CellTemplate = cell0;
                column0.HeaderText = @"Сумма";
                column0.ReadOnly = true;
                column0.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;

                _glassBox.Columns.Add(column0);

                DataGridViewTextBoxCell cell = new DataGridViewTextBoxCell();
                DataGridViewColumn column = new DataGridViewColumn();
                column.CellTemplate = cell;
                column.HeaderText = @"Объём";
                column.ReadOnly = true;
                column.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;

                _glassBox.Columns.Add(column);

                DataGridViewTextBoxCell cell2 = new DataGridViewTextBoxCell();
                DataGridViewColumn column1 = new DataGridViewColumn();
                column1.CellTemplate = cell2;
                column1.HeaderText = @"Цена";
                column1.ReadOnly = true;
                column1.Width = 90;

                _glassBox.Columns.Add(column1);


                DataGridViewTextBoxCell cell3 = new DataGridViewTextBoxCell();
                DataGridViewColumn column3 = new DataGridViewColumn();
                column3.CellTemplate = cell3;
                column3.HeaderText = @"Объём";
                column3.ReadOnly = true;
                column3.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;

                _glassBox.Columns.Add(column3);

                DataGridViewCellStyle styleRed = new DataGridViewCellStyle();
                styleRed.Alignment = DataGridViewContentAlignment.MiddleRight;
                styleRed.ForeColor = Color.Black;
                styleRed.Font = new Font("Stencil", 4);


                for (int i = 0; i < 25; i++)
                {
                    _glassBox.Rows.Add(null, null, null);
                    _glassBox.Rows[_glassBox.Rows.Count - 1].DefaultCellStyle.BackColor = Color.Gainsboro;
                    _glassBox.Rows[_glassBox.Rows.Count - 1].DefaultCellStyle.ForeColor = Color.Black;
                    _glassBox.Rows[_glassBox.Rows.Count - 1].DefaultCellStyle.Font = new Font("New Times Roman", 10);
                    _glassBox.Rows[_glassBox.Rows.Count - 1].Cells[0].Style = styleRed;
                    _glassBox.Rows[_glassBox.Rows.Count - 1].Cells[1].Style = styleRed;
                }

                DataGridViewCellStyle styleBlue = new DataGridViewCellStyle();
                styleBlue.Alignment = DataGridViewContentAlignment.MiddleRight;
                styleBlue.ForeColor = Color.DarkOrange;
                styleBlue.Font = new Font("Stencil", 4);

                for (int i = 0; i < 25; i++)
                {
                    _glassBox.Rows.Add(null, null, null);
                    _glassBox.Rows[_glassBox.Rows.Count - 1].DefaultCellStyle.BackColor = Color.Black;
                    _glassBox.Rows[_glassBox.Rows.Count - 1].DefaultCellStyle.ForeColor = Color.DarkOrange;
                    _glassBox.Rows[_glassBox.Rows.Count - 1].DefaultCellStyle.Font = new Font("New Times Roman", 10);
                    _glassBox.Rows[_glassBox.Rows.Count - 1].Cells[0].Style = styleBlue;
                    _glassBox.Rows[_glassBox.Rows.Count - 1].Cells[1].Style = styleBlue;
                }

                _glassBox.Rows[22].Cells[0].Selected = true;
                _glassBox.Rows[22].Cells[0].Selected = false;
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        /// <summary>
        /// изменился текст в поле лимитной цены рядом со стаканом
        /// </summary>
        void _textBoxLimitPrice_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            try
            {
                if (!_isPaint)
                {
                    return;
                }

                try
                {
                    if (Convert.ToDecimal(_textBoxLimitPrice.Text) < 0)
                    {
                        throw new Exception();
                    }
                }
                catch (Exception)
                {
                    _textBoxLimitPrice.Text = _lastSelectPrice.ToString(new CultureInfo("RU-ru"));
                }

                _lastSelectPrice = Convert.ToDecimal(_textBoxLimitPrice.Text);
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        /// <summary>
        /// пользователь щёлкнул по стакану
        /// </summary>
        void _glassBox_SelectionChanged(object sender, EventArgs e)
        {
            try
            {
                if (_glassBox.InvokeRequired)
                {
                    _glassBox.Invoke(new Action<object, EventArgs>(_glassBox_SelectionChanged), sender, e);
                    return;
                }

                decimal price;
                try
                {
                    if (_glassBox.CurrentCell == null)
                    {
                        return;
                    }
                    price = Convert.ToDecimal(_glassBox.Rows[_glassBox.CurrentCell.RowIndex].Cells[2].Value);
                }
                catch (Exception)
                {
                    return;
                }

                if (price == 0)
                {
                    return;
                }
                if (_isPaint)
                {
                    _lastSelectPrice = price;
                    _textBoxLimitPrice.Text = Convert.ToDouble(_lastSelectPrice).ToString(new CultureInfo("RU-ru"));
                }
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        /// <summary>
        /// начать прорисовывать элементы коннектора
        /// </summary>
        public void StartPaint(WindowsFormsHost glass, System.Windows.Controls.TextBox textBoxLimitPrice)
        {
            try
            {
                if (_glassBox == null)
                {
                    return;
                }
                if (_glassBox.InvokeRequired)
                {
                    _glassBox.Invoke(new Action<WindowsFormsHost, System.Windows.Controls.TextBox>(StartPaint),glass,textBoxLimitPrice);
                    return;
                }

                _textBoxLimitPrice = textBoxLimitPrice;
                _textBoxLimitPrice.TextChanged += _textBoxLimitPrice_TextChanged;
                _hostGlass = glass;

                _isPaint = true;
                PaintBidAsk(_bestAsk, _bestBid);
                _hostGlass.Child = _glassBox;
                _hostGlass.Child.Refresh();

                _textBoxLimitPrice.Text = Convert.ToDouble(_lastSelectPrice).ToString(new CultureInfo("RU-ru"));

                PaintMarketDepth(_depth);
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        /// <summary>
        /// остановить прорисовывание элементов коннектора
        /// </summary>
        public void StopPaint()
        {
            try
            {
                if (_glassBox != null && _glassBox.InvokeRequired)
                {
                    _glassBox.Invoke(new Action(StopPaint));
                    return;
                }

                if (_textBoxLimitPrice != null)
                {
                    _textBoxLimitPrice.TextChanged -= _textBoxLimitPrice_TextChanged;
                    _textBoxLimitPrice = null;
                }

                if (_hostGlass != null)
                {
                    _hostGlass.Child = null;
                    _hostGlass = null;
                }

                _isPaint = false;

            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        /// <summary>
        /// включена ли прорисовка
        /// </summary>
        private bool _isPaint;

        /// <summary>
        /// прорисовать Бид с Аском
        /// </summary>
        private void PaintBidAsk(decimal bid, decimal ask)
        {
            try
            {
                if (_isPaint == false)
                {
                    return;
                }
                if (_glassBox.InvokeRequired)
                {
                    _glassBox.Invoke(new Action<decimal, decimal>(PaintBidAsk), bid, ask);
                    return;
                }

                if (ask != 0 && bid != 0)
                {
                    _glassBox.Rows[25].Cells[2].Value = ask;
                    _glassBox.Rows[24].Cells[2].Value = bid;
                }
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        /// <summary>
        /// стакан
        /// </summary>
        private MarketDepth _depth;

        /// <summary>
        /// время последнего обновления стакана
        /// </summary>
        private DateTime _lastGlassUpdete = DateTime.MinValue;

        /// <summary>
        /// прорисовать стакан
        /// </summary>
        private void PaintMarketDepth(MarketDepth depth)
        {
            try
            {
                _depth = depth;

                if (_isPaint == false || depth == null)
                {
                    return;
                }

                if (_glassBox.InvokeRequired)
                {
                    _glassBox.Invoke(new Action<MarketDepth>(PaintMarketDepth), depth);
                    return;
                }
                if (_lastGlassUpdete != DateTime.MinValue &&
                    _lastGlassUpdete.AddMilliseconds(300) > DateTime.Now)
                {
                    return;
                }

                _lastGlassUpdete = DateTime.Now;

                if (depth.Bids[0].Bid == 0 ||
                    depth.Asks[0].Ask == 0)
                {
                    return;
                }

                decimal maxVol = 0;

                decimal allBid = 0;

                decimal allAsk = 0;

                for (int i = 0; depth.Bids != null && i < 25; i++)
                {
                    if (i < depth.Bids.Count)
                    {
                        _glassBox.Rows[25 + i].Cells[2].Value = depth.Bids[i].Price;
                        _glassBox.Rows[25 + i].Cells[3].Value = depth.Bids[i].Bid;
                        if (depth.Bids[i].Bid > maxVol)
                        {
                            maxVol = depth.Bids[i].Bid;
                        }
                        allAsk += depth.Bids[i].Bid;
                    }
                    else if (_glassBox.Rows[25 + i].Cells[2].Value != null)
                    {
                        _glassBox.Rows[25 + i].Cells[0].Value = null;
                        _glassBox.Rows[25 + i].Cells[1].Value = null;
                        _glassBox.Rows[25 + i].Cells[2].Value = null;
                        _glassBox.Rows[25 + i].Cells[3].Value = null;
                    }
                }


                for (int i = 0; depth.Asks != null && i < 25; i++)
                {
                    if (i < depth.Asks.Count)
                    {
                        _glassBox.Rows[24 - i].Cells[2].Value = depth.Asks[i].Price;
                        _glassBox.Rows[24 - i].Cells[3].Value = depth.Asks[i].Ask;

                        if (depth.Asks[i].Ask > maxVol)
                        {
                            maxVol = depth.Asks[i].Ask;
                        }

                        allBid += depth.Asks[i].Ask;
                    }
                    else if (_glassBox.Rows[24 - i].Cells[2].Value != null)
                    {
                        _glassBox.Rows[24 - i].Cells[2].Value = null;
                        _glassBox.Rows[24 - i].Cells[3].Value = null;
                        _glassBox.Rows[24 - i].Cells[0].Value = null;
                        _glassBox.Rows[24 - i].Cells[1].Value = null;
                    }

                }

                // объём в палках для аска
                for (int i = 0; depth.Bids != null && i < 25 && i < depth.Bids.Count; i++)
                {
                    int percentFromMax = Convert.ToInt32(depth.Bids[i].Bid / maxVol * 50);

                    if (percentFromMax == 0)
                    {
                        percentFromMax = 1;
                    }

                    StringBuilder builder = new StringBuilder(percentFromMax);
                    for (int i2 = 0; i2 < percentFromMax; i2++)
                    {
                        builder.Append('|');
                    }

                    _glassBox.Rows[25 + i].Cells[1].Value = builder;

                }

                // объём в палках для бида
                for (int i = 0; depth.Asks != null && i < 25 && i < depth.Asks.Count; i++)
                {
                    int percentFromMax = Convert.ToInt32(depth.Asks[i].Ask / maxVol * 50);

                    if (percentFromMax == 0)
                    {
                        percentFromMax = 1;
                    }

                    StringBuilder builder = new StringBuilder(percentFromMax);

                    for (int i2 = 0; i2 < percentFromMax; i2++)
                    {
                        builder.Append('|');
                    }

                    _glassBox.Rows[24 - i].Cells[1].Value = builder;
                }

                decimal maxSeries;

                if (allAsk > allBid)
                {
                    maxSeries = allAsk;
                }
                else
                {
                    maxSeries = allBid;
                }

                // объём комулятивный для аска
                decimal summ = 0;
                for (int i = 0; depth.Bids != null && i < 25 && i < depth.Bids.Count; i++)
                {
                    summ += depth.Bids[i].Bid;

                    int percentFromMax = Convert.ToInt32(summ / maxSeries * 50);

                    if (percentFromMax == 0)
                    {
                        percentFromMax = 1;
                    }

                    StringBuilder builder = new StringBuilder(percentFromMax);
                    for (int i2 = 0; i2 < percentFromMax; i2++)
                    {
                        builder.Append('|');
                    }

                    _glassBox.Rows[25 + i].Cells[0].Value = builder;

                }

                // объём комулятивный для бида
                summ = 0;
                for (int i = 0; depth.Asks != null && i < 25 && i < depth.Asks.Count; i++)
                {
                    summ += depth.Asks[i].Ask;

                    int percentFromMax = Convert.ToInt32(summ / maxSeries * 50);

                    if (percentFromMax == 0)
                    {
                        percentFromMax = 1;
                    }

                    StringBuilder builder = new StringBuilder(percentFromMax);
                    for (int i2 = 0; i2 < percentFromMax; i2++)
                    {
                        builder.Append('|');
                    }

                    _glassBox.Rows[24 - i].Cells[0].Value = builder;

                }
                _glassBox.Refresh();
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

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

