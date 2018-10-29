/*
 *Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Threading;
using OsEngine.Entity;
using OsEngine.Logging;
using OsEngine.Market.Servers.Entity;
using OsEngine.Market.Servers.Plaza.Internal;

namespace OsEngine.Market.Servers.Plaza
{
    /// <summary>
    /// класс - сервер для подключения к Плаза 2 CGate
    /// </summary>
    public class PlazaServer : IServer
    {
// сервис
        /// <summary>
        /// конструктор
        /// </summary>
        public PlazaServer(bool neadToLoadTicks)
        {
            _neadToSaveTicks = false;
            _countDaysTickNeadToSave = 3;
            ServerStatus = ServerConnectStatus.Disconnect;
            ServerType = ServerType.Plaza;
            _logMaster = new Log("PlazaServer",StartProgram.IsOsTrader);
            _logMaster.Listen(this);
            KeyToProggram = "11111111";
            Load();

            _tickStorage = new ServerTickStorage(this);
            _tickStorage.NeadToSave = NeadToSaveTicks;
            _tickStorage.DaysToLoad = CountDaysTickNeadToSave;
            _tickStorage.TickLoadedEvent += _tickStorage_TickLoadedEvent;
            _tickStorage.LogMessageEvent += SendNewLogMessage;

            if (neadToLoadTicks)
            {
                _tickStorage.LoadTick();
            }
        }

        /// <summary>
        /// взять тип сервера
        /// </summary>
        /// <returns>тип сервера. Квик, Смартком, Плаза</returns>
        public ServerType ServerType { get; set; }

        /// <summary>
        /// показать настройки
        /// </summary>
        public void ShowDialog()
        {
            if (_ui == null)
            {
                _ui = new PlazaServerUi(this, _logMaster);
                _ui.Show();
                _ui.Closing += (sender, args) => { _ui = null; };
            }
            else
            {
                _ui.Activate();
            }
        }

        /// <summary>
        /// окно управления элемента
        /// </summary>
        private PlazaServerUi _ui;

        /// <summary>
        /// сохранить настройки
        /// </summary>
        public void Save()
        {
            try
            {
                using (StreamWriter writer = new StreamWriter(@"Engine\" + @"PlazaServer.txt", false))
                {
                    writer.WriteLine(KeyToProggram);
                    writer.WriteLine(CountDaysTickNeadToSave);
                    writer.WriteLine(NeadToSaveTicks);
                    writer.Close();
                }
            }
            catch (Exception)
            {
                // ignored
            }
        }

        public void Load()
        {
            if (!File.Exists(@"Engine\" + @"PlazaServer.txt"))
            {
                return;
            }

            try
            {
                using (StreamReader reader = new StreamReader(@"Engine\" + @"PlazaServer.txt"))
                {
                    KeyToProggram = reader.ReadLine();
                    _countDaysTickNeadToSave = Convert.ToInt32(reader.ReadLine());
                    _neadToSaveTicks = Convert.ToBoolean(reader.ReadLine());
                    reader.Close();
                }
            }
            catch (Exception)
            {
                // ignored
            }
        }

        private int _countDaysTickNeadToSave;

        /// <summary>
        /// количество дней назад, тиковые данные по которым нужно сохранять
        /// </summary>
        public int CountDaysTickNeadToSave
        {
            get { return _countDaysTickNeadToSave; }
            set
            {
                if (_tickStorage == null)
                {
                    return;
                }
                _countDaysTickNeadToSave = value;
                _tickStorage.DaysToLoad = value;
                Save();
            }
        }

        private bool _neadToSaveTicks;

        /// <summary>
        /// нужно ли сохранять тики 
        /// </summary>
        public bool NeadToSaveTicks
        {
            get { return _neadToSaveTicks; }
            set
            {
                if (_tickStorage == null)
                {
                    return;
                }
                _neadToSaveTicks = value;
                _tickStorage.NeadToSave = value;
                Save();
            }
        }

// вкл / выкл

        /// <summary>
        /// эта штука контролирует плазу и выкачивает из неё данные
        /// </summary>
        private PlazaController _plazaController; 

        /// <summary>
        /// время последнего старта сервера
        /// </summary>
        private DateTime _lastStartServerTime = DateTime.MinValue;

        /// <summary>
        /// уникальный ключ доступа для программы, полученый при сертификации
        /// </summary>
        public string KeyToProggram;

        /// <summary>
        /// запускает сервер
        /// </summary>
        public void StartServer()
        {
            Thread worker = new Thread(Connecting);
            worker.IsBackground = true;
            worker.CurrentCulture = new CultureInfo("ru-RU");
            worker.Start();
        }

        /// <summary>
        /// останавливает сервер
        /// </summary>
        public void StopServer()
        {
            if (_plazaController != null)
            {
                _plazaController.Stop();
            }
        }

        /// <summary>
        /// внутренняя функция запускающая процесс соединения с Плазой
        /// </summary>
        private void Connecting() 
        {
            _lastStartServerTime = DateTime.Now;
            if (_plazaController == null)
            {
                _plazaController = new PlazaController(KeyToProggram);
                _plazaController.LogMessageEvent += _plazaController_LogMessageEvent;
                _plazaController.ConnectStatusChangeEvent += _plazaController_StatusChangeEvent;
                _plazaController.MarketDepthChangeEvent += _plazaController_UpdateGlass;
                _plazaController.NewMyTradeEvent += _plazaController_UpdateMyTrade;
                _plazaController.NewMyOrderEvent += _plazaController_UpdateOrders;
                _plazaController.UpdatePortfolio += _plazaController_UpdatePortfolios;
                _plazaController.UpdatePosition += _plazaController_UpdatePosition;
                _plazaController.UpdateSecurity += _plazaController_UpdateSecurity;
                _plazaController.NewTradeEvent += _plazaController_UpdateTrade;
            }

            if (_candleManager == null)
            {
                _candleManager = new CandleManager(this);
                _candleManager.CandleUpdateEvent += _candleManager_CandleUpdateEvent;
                _candleManager.LogMessageEvent += SendNewLogMessage;
            }

            _plazaController.Start();
        }

        void _plazaController_LogMessageEvent(string message)
        {
            SendNewLogMessage(message,LogMessageType.System);
        }

// статус сервера

        private ServerConnectStatus _serverConnectStatus; 

        /// <summary>
        /// статус сервера
        /// </summary>
        public ServerConnectStatus ServerStatus
        {
            get { return _serverConnectStatus; }
            private set
            {
                if (value == _serverConnectStatus)
                {
                    return;
                }
                _serverConnectStatus = value;
                if (ConnectStatusChangeEvent != null)
                {
                    ConnectStatusChangeEvent(_serverConnectStatus.ToString());
                }
            }
        }

        /// <summary>
        /// событие из плазаКонтроллера. Изменился статус
        /// </summary>
        /// <param name="status"></param>
        void _plazaController_StatusChangeEvent(ServerConnectStatus status)
        {
            ServerStatus = status;
        }

        /// <summary>
        /// происходит после изменения статуса сервера
        /// </summary>
        public event Action<string> ConnectStatusChangeEvent;

// время сервера

        private DateTime _serverTime; 

        /// <summary>
        /// последнее время сервера
        /// </summary>
        public DateTime ServerTime
        {
            get { return _serverTime; }

            private set
            {
                DateTime lastTime = _serverTime;
                _serverTime = value;

                if (_serverTime != lastTime &&
                    TimeServerChangeEvent != null)
                {
                    TimeServerChangeEvent(_serverTime);
                }
            }
        }

        /// <summary>
        /// происходит при изменении времени на сервере
        /// </summary>
        public event Action<DateTime> TimeServerChangeEvent;

// портфели

        /// <summary>
        /// все портфели в системе
        /// </summary>
        private List<Portfolio> _portfolios;

        /// <summary>
        /// взять все портфели 
        /// </summary>
        public List<Portfolio> Portfolios
        {
            get { return _portfolios; }
        }

        /// <summary>
        /// взять портфель по номеру
        /// </summary>
        /// <param name="name">название / номер портфеля</param>
        public Portfolio GetPortfolioForName(string name)
        {
            if (_portfolios == null)
            {
                return null;
            }
            return _portfolios.Find(portfolio => portfolio.Number == name);
        }

        /// <summary>
        /// новые портфели в системе
        /// </summary>
        void _plazaController_UpdatePortfolios(Portfolio portfolio)
        {
            if (_portfolios == null)
            {
                _portfolios = new List<Portfolio>();
            }

            if (_portfolios.Find(portfolio1 => portfolio1.Number == portfolio.Number) == null)
            {
                if (LogMessageEvent != null)
                {
                    LogMessageEvent(portfolio.Number + " Создан новый портфель.", LogMessageType.System);
                }
                _portfolios.Add(portfolio);

                if (PortfoliosChangeEvent != null)
                {
                    PortfoliosChangeEvent(_portfolios);
                }
            }
        }

        private object _lockerUpdatePosition = new object();

        /// <summary>
        /// новые позиции в системе
        /// </summary>
        void _plazaController_UpdatePosition(PositionOnBoard positionOnBoard)
        {
            lock (_lockerUpdatePosition)
            {
                // забиваем в название инструмента правдивое описание, т.к. до этого в этой строке некий ID

                Security security = null;

                if (_securities != null)
                {
                    security = _securities.Find(security1 =>
                        security1.NameId == positionOnBoard.SecurityNameCode
                        || security1.Name == positionOnBoard.SecurityNameCode);
                }

                if (security == null)
                {
                    PositionOnBoardSander sender = new PositionOnBoardSander();
                    sender.PositionOnBoard = positionOnBoard;
                    sender.TimeSendPortfolio += _plazaController_UpdatePosition;

                    Thread worker = new Thread(sender.Go);
                    worker.CurrentCulture = new CultureInfo("ru-RU");
                    worker.IsBackground = true;
                    worker.Start();
                    return;
                }

                positionOnBoard.SecurityNameCode = security.Name;
                Portfolio myPortfolio = null;
                if (_portfolios != null)
                {
                    myPortfolio = _portfolios.Find(portfolio => portfolio.Number == positionOnBoard.PortfolioName);
                }

                if (myPortfolio == null)
                {
                    PositionOnBoardSander sender = new PositionOnBoardSander();
                    sender.PositionOnBoard = positionOnBoard;
                    sender.TimeSendPortfolio += _plazaController_UpdatePosition;

                    Thread worker = new Thread(sender.Go);
                    worker.CurrentCulture = new CultureInfo("ru-RU");
                    worker.IsBackground = true;
                    worker.Start();
                    return;
                }

                myPortfolio.SetNewPosition(positionOnBoard);

                if (PortfoliosChangeEvent != null)
                {
                    PortfoliosChangeEvent(_portfolios);
                }
            }
        }

        /// <summary>
        /// происходит при изменении портфеля или позиции
        /// </summary>
        public event Action<List<Portfolio>> PortfoliosChangeEvent;

// бумаги

        private List<Security> _securities;

        /// <summary>
        /// Инструменты в системе
        /// </summary>
        public List<Security> Securities
        {
            get
            {
                return _securities;
            }
        }

        /// <summary>
        /// взять инструмент в виде класса Security по строке и его именем
        /// </summary>
        /// <param name="name">имя инструмента в виде строки</param>
        /// <returns>инструмент в виде класса Security. Если не найден вернёт null</returns>
        public Security GetSecurityForName(string name)
        {
            if (_securities == null)
            {
                return null;
            }
            return _securities.Find(securiti => securiti.Name == name);
        }

        /// <summary>
        /// новые инструменты из системы
        /// </summary>
        void _plazaController_UpdateSecurity(Security security)
        {
            if (_securities == null)
            {
                _securities = new List<Security>();
            }

            if (_securities.Find(security1 => security1.NameId == security.NameId) == null)
            {
                _securities.Add(security);

                if (SecuritiesChangeEvent != null)
                {
                    SecuritiesChangeEvent(_securities);
                }
            }

        }

        /// <summary>
        /// изменился список инструментов в системе
        /// </summary>
        public event Action<List<Security>> SecuritiesChangeEvent;

        /// <summary>
        /// показать инструменты 
        /// </summary>
        public void ShowSecuritiesDialog()
        {
            SecuritiesUi ui = new SecuritiesUi(this);
            ui.ShowDialog();
        }

// Подпись на данные

        /// <summary>
        /// мастер загрузки свечек
        /// </summary>
        private CandleManager _candleManager;

        /// <summary>
        /// объект - блокиратор многопоточного доступа в StartThisSecurity
        /// </summary>
        private object _lockerStarter = new object();

        /// <summary>
        /// Начать выгрузку данных по инструменту. 
        /// </summary>
        /// <param name="namePaper">имя бумаги которую будем запускать</param>
        /// <param name="timeFrameBuilder">объект несущий в себе данные о таймФрейме</param>
        /// <returns>В случае удачи возвращает CandleSeries
        /// в случае неудачи null</returns>
        public CandleSeries StartThisSecurity(string namePaper, TimeFrameBuilder timeFrameBuilder)
        {
            if (_lastStartServerTime.AddSeconds(30) > DateTime.Now)
            {
                return null;
            }

            // дальше по одному
            lock (_lockerStarter)
            {
                if (namePaper == "")
                {
                    return null;
                }
                // надо запустить сервер если он ещё отключен
                if (ServerStatus != ServerConnectStatus.Connect)
                {
                    //MessageBox.Show("Сервер не запущен. Скачивание данных прервано. Инструмент: " + namePaper);
                    return null;
                }

                if (_securities == null || _portfolios == null)
                {
                    Thread.Sleep(5000);
                    return null;
                }
                if (_lastStartServerTime != DateTime.MinValue &&
                    _lastStartServerTime.AddSeconds(15) > DateTime.Now)
                {
                    return null;
                }

                Security security = null;

                for (int i = 0; _securities != null && i < _securities.Count; i++)
                {
                    if (_securities[i].Name == namePaper)
                    {
                        security = _securities[i];
                        break;
                    }
                }

                if (security == null)
                {
                    return null;
                }

                CandleSeries series = new CandleSeries(timeFrameBuilder, security,StartProgram.IsOsTrader);

                _plazaController.StartMarketDepth(security);
                Thread.Sleep(5000);

                _candleManager.StartSeries(series);

                if (LogMessageEvent != null)
                {
                    LogMessageEvent("Инструмент " + series.Security.Name + "ТаймФрейм " + series.TimeFrame +
                                    " успешно подключен на получение данных и прослушивание свечек",
                        LogMessageType.System);
                }

                if (_tickStorage != null)
                {
                    _tickStorage.SetSecurityToSave(security);
                }
               

                return series;
            }
        }

        /// <summary>
        /// остановить скачивание данных по инструменту
        /// </summary>
        /// <param name="series">объект CandleSeries через который получаются свечи</param>
        public void StopThisSecurity(CandleSeries series)
        {
            if (series != null)
            {
                _candleManager.StopSeries(series);
            }
        }

        /// <summary>
        /// изменилась свеча в серии свечек
        /// </summary>
        void _candleManager_CandleUpdateEvent(CandleSeries series)
        {
            if (NewCandleIncomeEvent != null)
            {
                NewCandleIncomeEvent(series);
            }
        }

        /// <summary>
        /// вызывается при обновлении свечи в серии свечек
        /// </summary>
        public event Action<CandleSeries> NewCandleIncomeEvent;

        /// <summary>
        /// коннекторам подключеным к серверу необходимо перезаказать данные
        /// </summary>
        public event Action NeadToReconnectEvent;

// стакан

        /// <summary>
        /// стаканы по инструментам
        /// </summary>
        private List<MarketDepth> _marketDepths = new List<MarketDepth>();

        /// <summary>
        /// взять стакан по названию бумаги
        /// </summary>
        public MarketDepth GetMarketDepth(string securityName)
        {
            return _marketDepths.Find(m => m.SecurityNameCode == securityName);
        }

// сохранение расширенных данных по трейду

        /// <summary>
        /// прогрузить трейды данными стакана
        /// </summary>
        private void BathTradeMarketDepthData(Trade trade)
        {
            MarketDepth depth = _marketDepths.Find(d => d.SecurityNameCode == trade.SecurityNameCode);

            if (depth == null ||
                depth.Asks == null || depth.Asks.Count == 0 ||
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
        /// изменился какой-то стакан
        /// </summary>
        /// <param name="depth"></param>
        void _plazaController_UpdateGlass(MarketDepth depth)
        {
            depth.Time = DateTime.Now;

            if (NewMarketDepthEvent != null)
            {
                NewMarketDepthEvent(depth);
            }

            if (NewBidAscIncomeEvent != null)
            {
                if (depth.Asks == null || depth.Asks.Count == 0 ||
                    depth.Bids == null || depth.Bids.Count == 0)
                {
                    return;
                }
                NewBidAscIncomeEvent(depth.Asks[0].Price, depth.Bids[0].Price, GetSecurityForName(depth.SecurityNameCode));
            }

            // грузим стаканы в хранилище
            for (int i = 0; i < _marketDepths.Count; i++)
            {
                if (_marketDepths[i].SecurityNameCode == depth.SecurityNameCode)
                {
                    _marketDepths[i] = depth;
                    return;
                }
            }

            _marketDepths.Add(depth);
        }

        /// <summary>
        /// вызывается при изменении лучшего бида или аска
        /// </summary>
        public event Action<decimal, decimal, Security> NewBidAscIncomeEvent;

        /// <summary>
        /// происходит когда обновляется стакан в системе
        /// </summary>
        public event Action<MarketDepth> NewMarketDepthEvent;

// тики
        private ServerTickStorage _tickStorage;

        /// <summary>
        /// все сделки
        /// </summary>
        private List<Trade>[] _allTrades;

        /// <summary>
        /// все тики имеющиеся у сервера
        /// </summary>
        public List<Trade>[] AllTrades { get { return _allTrades; } }

        void _tickStorage_TickLoadedEvent(List<Trade>[] trades)
        {
            _allTrades = trades;
        }

        /// <summary>
        /// взять все сделки по бумаге
        /// </summary>
        /// <param name="security">бумага по которой берутся сделки</param>
        /// <returns>сделки. Если сделки по такой бумаге не найдены, возвращается null</returns>
        public List<Trade> GetAllTradesToSecurity(Security security)
        {
            if (_allTrades != null)
            {
                foreach (var tradesList in _allTrades)
                {
                    if (tradesList.Count > 1 &&
                        tradesList[0] != null &&
                        tradesList[0].SecurityNameCode == security.Name)
                    {
                        return tradesList;
                    }
                }
            }

            return new List<Trade>();
        }

        /// <summary>
        /// новый трейд из системы
        /// </summary>
        /// <param name="trade">новый трейд</param>
        /// <param name="isOnLine">является ли трейд полученный из онлайн потока</param>
        private void _plazaController_UpdateTrade(Trade trade, bool isOnLine) 
        {
            if (_securities == null)
            {
                return;
            }
            Security security = _securities.Find(security1 => security1.NameId == trade.SecurityNameCode);

            if (security == null)
            {
                return;
            }

            trade.SecurityNameCode = security.Name;

            BathTradeMarketDepthData(trade);

            if (_allTrades == null)
            {
                _allTrades = new List<Trade>[1];
                _allTrades[0] = new List<Trade> {trade};
            }
            else
            {
// сортируем сделки по хранилищам
                List<Trade> myList = null;
                bool isSave = false;
                for (int i = 0; i < _allTrades.Length; i++)
                {
                    if (_allTrades[i] != null && _allTrades[i].Count != 0 &&
                        _allTrades[i][0].SecurityNameCode == trade.SecurityNameCode)
                    {
                        // если для этого инструметна уже есть хранилище, сохраняем и всё
                        isSave = true;
                        if (_allTrades[i][_allTrades[i].Count - 1].Time > trade.Time)
                        {
                            break;
                        }

                        _allTrades[i].Add(trade);
                        myList = _allTrades[i];
                        break;
                    }
                }

                if (isSave == false)
                {
                    // хранилища для инструмента нет
                    List<Trade>[] _allTradesNew = new List<Trade>[_allTrades.Length + 1];
                    for (int i = 0; i < _allTrades.Length; i++)
                    {
                        _allTradesNew[i] = _allTrades[i];
                    }
                    _allTradesNew[_allTradesNew.Length - 1] = new List<Trade>();
                    _allTradesNew[_allTradesNew.Length - 1].Add(trade);
                    myList = _allTradesNew[_allTradesNew.Length - 1];
                    _allTrades = _allTradesNew;
                }

                if (NewTradeEvent != null && isOnLine)
                {
                    NewTradeEvent(myList);
                }
            }

            // перегружаем последним временем тика время сервера
            ServerTime = trade.Time;

        }

        /// <summary>
        /// происходит когда в систему поступает новый тик
        /// </summary>
        public event Action<List<Trade>> NewTradeEvent;

// мои сделки
        private List<MyTrade> _myTrades;

        /// <summary>
        /// мои сделки
        /// </summary>
        public List<MyTrade> MyTrades
        {
            get { return _myTrades; }
        }

        /// <summary>
        /// вызывается когда в системе появляется новая Моя сделка
        /// </summary>
        public event Action<MyTrade> NewMyTradeEvent;

        private object _lockUpdateMyTrade = new object();

        /// <summary>
        /// новая моя сделка в системе
        /// </summary>
        void _plazaController_UpdateMyTrade(MyTrade myTrade)
        {
            lock (_lockUpdateMyTrade)
            {
                if (_securities == null)
                {
                    return;
                }
                Security security = _securities.Find(security1 => security1.NameId == myTrade.SecurityNameCode);

                if (security == null)
                {
                    return;
                }

                myTrade.SecurityNameCode = security.Name;

                if (_myTrades == null)
                {
                    _myTrades = new List<MyTrade>();
                }
                _myTrades.Add(myTrade);

                if (NewMyTradeEvent != null)
                {
                    NewMyTradeEvent(myTrade);
                }
            }
        }

// исполнение ордеров

        private List<Order> _orders; 

        /// <summary>
        /// исполнить ордер
        /// </summary>
        public void ExecuteOrder(Order order)
        {
            if (_orders == null)
            {
                _orders  = new List<Order>();
            }

            _orders.Add(order);

            order.TimeCreate = ServerTime;

            _plazaController.ExecuteOrder(order);
        }

        /// <summary>
        /// отозвать ордер
        /// </summary>
        public void CanselOrder(Order order)
        {
            _plazaController.CancelOrder(order);
        }

        private object _lockerUpdateOrders = new object();

        /// <summary>
        /// в системе изменился ордер
        /// </summary>
        void _plazaController_UpdateOrders(Order order)
        {
            lock (_lockerUpdateOrders)
            {
                if (_orders != null)
                {
                    Order oldOrder = _orders.Find(order1 => order1.NumberUser == order.NumberUser);

                    if (oldOrder != null && order.Price != 0)
                    {
                        order.Volume = oldOrder.Volume;
                        order.VolumeExecute = oldOrder.Volume - order.VolumeExecute;
                    }
                }

                if (_securities == null)
                {
                    if (order.State != OrderStateType.Activ)
                    {
                        return;
                    }
                    OrderSender sender = new OrderSender();
                    sender.Order = order;
                    sender.UpdeteOrderEvent += _plazaController_UpdateOrders;
                    Thread worker = new Thread(sender.Sand);
                    worker.CurrentCulture = new CultureInfo("ru-RU");
                    worker.IsBackground = true;
                    worker.Start();
                    return;
                }

                Security security = _securities.Find(security1 => security1.NameId == order.SecurityNameCode);

                if (order.SecurityNameCode != null)
                {
                    if (security == null)
                    {
                        if (order.State != OrderStateType.Activ)
                        {
                            return;
                        }
                        OrderSender sender = new OrderSender();
                        sender.Order = order;
                        sender.UpdeteOrderEvent += _plazaController_UpdateOrders;
                        Thread worker = new Thread(sender.Sand);
                        worker.CurrentCulture = new CultureInfo("ru-RU");
                        worker.IsBackground = true;
                        worker.Start();
                        return;
                    }

                    order.SecurityNameCode = security.Name;
                }

                if (_myTrades != null &&
                    _myTrades.Count != 0)
                {
                    List<MyTrade> myTrade =
                        _myTrades.FindAll(trade => trade.NumberOrderParent == order.NumberMarket);

                    for (int tradeNum = 0; tradeNum < myTrade.Count; tradeNum++)
                    {
                        if (NewMyTradeEvent != null)
                        {
                            NewMyTradeEvent(myTrade[tradeNum]);
                        }
                    }
                }

                if (_ordersNumbers == null)
                {
                    _ordersNumbers = new List<string>();
                }

                if (_ordersNumbers.Find(orderNum => orderNum == order.NumberMarket) == null)
                {
                    _ordersNumbers.Add(order.NumberMarket);
                }

                if (NewOrderIncomeEvent != null)
                {
                    NewOrderIncomeEvent(order);
                }
            }
        }

        /// <summary>
        /// номера ордеров. Необходимо, чтобы отправлять мои сделки на второй круг, 
        /// если моя сделка пришла раньше чем ордер с биржи
        /// </summary>
        private List<string> _ordersNumbers;

        /// <summary>
        /// вызывавется когда в системе появляется новый ордер
        /// </summary>
        public event Action<Order> NewOrderIncomeEvent;

// работа с логом

        /// <summary>
        /// выслать новое сообщение на верх
        /// </summary>
        private void SendNewLogMessage(string message, LogMessageType type)
        {
            if (LogMessageEvent != null)
            {
                LogMessageEvent(message, type);
            }
        }

        /// <summary>
        /// лог мастер
        /// </summary>
        private Log _logMaster;

        /// <summary>
        /// вызывается при появлении нового сообщения
        /// </summary>
        public event Action<string, LogMessageType> LogMessageEvent;
    }


}
