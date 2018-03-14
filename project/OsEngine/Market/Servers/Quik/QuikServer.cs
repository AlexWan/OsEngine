/*
 *Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using OsEngine.Entity;
using OsEngine.Logging;

namespace OsEngine.Market.Servers.Quik
{
    /// <summary>
    /// класс - сервер для подключения к Квик
    /// </summary>
    public class QuikServer: IServer
    {

        /// <summary>
        /// конструктор
        /// </summary>
        /// <param name="neadToLoadTicks">нужно ли подгружать тиковые данные для работы</param>
        public QuikServer(bool neadToLoadTicks)
        {
            ServerName = "OSA_DDE";
            NumberQuikInSystem = 1;
            _neadToSaveTicks = true;
            _countDaysTickNeadToSave = 2;
            _status = ServerConnectStatus.Disconnect;
            _ddeStatus = ServerConnectStatus.Disconnect;
            _transe2QuikStatus = ServerConnectStatus.Disconnect;
            _tradesStatus = ServerConnectStatus.Disconnect;

            Load();

            Thread worker = new Thread(CloseOrderThreadHome);
            worker.CurrentCulture = new CultureInfo("ru-RU");
            worker.IsBackground = true;
            worker.Start();

            _tickStorage = new ServerTickStorage(this);
            _tickStorage.NeadToSave = NeadToSaveTicks;
            _tickStorage.DaysToLoad = CountDaysTickNeadToSave;
            _tickStorage.TickLoadedEvent += _tickStorage_TickLoadedEvent;
            _tickStorage.LogMessageEvent += SendLogMessage;

            if (neadToLoadTicks)
            {
                _tickStorage.LoadTick();
            }

            _ordersToExecute = new ConcurrentQueue<Order>();
            _ordersToCansel = new ConcurrentQueue<Order>();

            Thread ordersExecutor = new Thread(ExecutorOrdersThreadArea);
            ordersExecutor.IsBackground = true;
            ordersExecutor.CurrentCulture = new CultureInfo("ru-RU");
            ordersExecutor.Start();

            _logMaster = new Log("QuikServer");
            _logMaster.Listen(this);
            ServerType = ServerType.QuikDde;

            Thread statusWatcher = new Thread(StatusWatcherArea);
            statusWatcher.IsBackground = true;
            statusWatcher.CurrentCulture = new CultureInfo("ru-RU");
            statusWatcher.Start();
        }

//сервис

        /// <summary>
        /// путь к Квик
        /// </summary>
        public string PathToQuik;

        /// <summary>
        /// название сервера ДДЕ, который будет разворачивать робот
        /// </summary>
        public string ServerName;

        /// <summary>
        /// номер Квик в системе
        /// </summary>
        public int NumberQuikInSystem;

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
            }
        }

        /// <summary>
        /// взять тип сервера
        /// </summary>
        public ServerType ServerType 
        { get; set; }

        /// <summary>
        /// вызвать окно настроек
        /// </summary>
        public void ShowDialog() 
        {
            QuikServerUi ui = new QuikServerUi(this,_logMaster);
            ui.Show();
        }

        /// <summary>
        /// загрузить настройки
        /// </summary>
        private void Load()
        {
            if (!File.Exists(@"Engine\" + @"QuikServer.txt"))
            {
                return;
            }

            try
            {
                using (StreamReader reader = new StreamReader(@"Engine\" + @"QuikServer.txt"))
                {
                    PathToQuik = reader.ReadLine();
                    ServerName = reader.ReadLine();
                    NumberQuikInSystem = Convert.ToInt32(reader.ReadLine());
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

        /// <summary>
        /// сохранить настройки
        /// </summary>
        public void Save()
        {
            try
            {
                using (StreamWriter writer = new StreamWriter(@"Engine\" + @"QuikServer.txt", false))
                {
                    writer.WriteLine(PathToQuik);
                    writer.WriteLine(ServerName);
                    writer.WriteLine(NumberQuikInSystem);
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

// подключение/отключение

        /// <summary>
        /// время последнего старта сервера
        /// </summary>
        private DateTime _lastStartServerTime = DateTime.MinValue; 

        /// <summary>
        /// мастер загрузки свечек
        /// </summary>
        private CandleManager _candleManager;

        /// <summary>
        /// ДДЕ сервер
        /// </summary>
        private QuikDde _serverDde; 

        /// <summary>
        /// подключится к квик
        /// </summary>
        public void StartServer() 
        {
            Thread worker = new Thread(Connecting);
            worker.IsBackground = true;
            worker.CurrentCulture = new CultureInfo("ru-RU");
            worker.Start();
        }

        private bool _weTried;

        /// <summary>
        /// подключиться к квик. Вызывается новым потоком из StartServer
        /// </summary>
        private void Connecting()
        {
            if (_weTried)
            {
                SendLogMessage("Данный коннектор не поддерживает динамическое переподключение. Нужно перезапустить Os.Enigine и попробовать ещё раз. ", LogMessageType.Error);
                return;
            }

            _weTried = true;

            try
            {
                SendLogMessage("Запущена процедура активации подключения",LogMessageType.System);

                if (string.IsNullOrWhiteSpace(PathToQuik))
                {
                    SendLogMessage("Ошибка. Необходимо указать местоположение Quik", LogMessageType.Error);
                    return;
                }

                if (_status == ServerConnectStatus.Connect)
                {
                    SendLogMessage("Ошибка. Соединение уже установлено", LogMessageType.Error);
                    return;
                }

                _lastStartServerTime = DateTime.Now;

                if (_transe2QuikStatus != ServerConnectStatus.Connect)
                {
                    int error;
                    var msg = new StringBuilder(256);

                    Trans2Quik.QuikResult result = Trans2Quik.SET_CONNECTION_STATUS_CALLBACK(StatusCallback, out error, msg,
                        msg.Capacity);

                    if (result != Trans2Quik.QuikResult.SUCCESS)
                    {
                        SendLogMessage("Ошибка. Trans2Quik не хочет подключаться " + error, LogMessageType.Error);
                        return;
                    }

                    result = Trans2Quik.CONNECT(PathToQuik, out error, msg, msg.Capacity);

                    if (result != Trans2Quik.QuikResult.SUCCESS && result != Trans2Quik.QuikResult.ALREADY_CONNECTED_TO_QUIK)
                    {
                        SendLogMessage("Ошибка при попытке подклчиться через Transe2Quik." + msg, LogMessageType.Error);
                        return;
                    }



                    Trans2Quik.SET_TRANSACTIONS_REPLY_CALLBACK(TransactionReplyCallback, out error, msg,
                         msg.Capacity);

                    result = Trans2Quik.SUBSCRIBE_ORDERS("", "");
                    while(result != Trans2Quik.QuikResult.SUCCESS)
                    {
                        Thread.Sleep(5000);
                        result = Trans2Quik.SUBSCRIBE_ORDERS("", "");
                        SendLogMessage("Повторно пытаемся включить поток сделок" + msg, LogMessageType.Error);
                    }

                    result = Trans2Quik.START_ORDERS((PfnOrderStatusCallback));
                    while (result != Trans2Quik.QuikResult.SUCCESS)
                    {
                        Thread.Sleep(5000);
                        result = Trans2Quik.START_ORDERS((PfnOrderStatusCallback));
                        SendLogMessage("Повторно пытаемся включить поток сделок" + msg, LogMessageType.Error);
                    }

                    result = Trans2Quik.SUBSCRIBE_TRADES("", "");
                    result = Trans2Quik.START_TRADES(PfnTradeStatusCallback);

                    Trans2Quik.GetTradeAccount(0);
                }

                if (_candleManager == null)
                {
                    _candleManager = new CandleManager(this);
                    _candleManager.CandleUpdateEvent += _candleManager_CandleUpdateEvent;
                    _candleManager.LogMessageEvent += SendLogMessage;
                }

                if (_serverDde == null)
                {
                    _serverDde = new QuikDde(ServerName);
                    _serverDde.StatusChangeEvent += _serverDde_StatusChangeEvent;
                    //_serverDde.UpdateMyTrade += _serverDde_UpdateMyTrade;
                    //_serverDde.UpdateOrders += _serverDde_UpdateOrders;
                    _serverDde.UpdatePortfolios += _serverDde_UpdatePortfolios;
                    _serverDde.UpdateSecurity += _serverDde_UpdateSecurity;
                    _serverDde.UpdateTrade += _serverDde_UpdateTrade;
                    _serverDde.UpdateGlass += _serverDde_UpdateGlass;
                    _serverDde.UpdateTimeSecurity += _serverDde_UpdateTimeSecurity;
                    _serverDde.LogMessageEvent += _serverDde_LogMessageEvent;
                }

                Thread.Sleep(1000);

                if (!_serverDde.IsRegistered)
                {
                    _serverDde.StartServer();
                }
                else
                {
                    _ddeStatus = ServerConnectStatus.Connect;
                }
            }
            catch (Exception erorr)
            {
                SendLogMessage(erorr.ToString(), LogMessageType.Error);
            }
        }

        /// <summary>
        /// выключить
        /// </summary>
        public void StopServer()
        {
            SendLogMessage("Запущена процедура оключения от Квик", LogMessageType.System);

            
            if (_serverDde != null && _serverDde.IsRegistered)
            {
                _serverDde.StopDdeInQuik();
                _serverDde = null;
            }

            try
            {
                int error;
                var msg = new StringBuilder(256);
                Trans2Quik.DISCONNECT(out error, msg, msg.Capacity);
            }
            catch (Exception erorr)
            {
                SendLogMessage(erorr.ToString(), LogMessageType.Error);
            }
        }

// статус соединения

        /// <summary>
        /// в этом месте живёт поток следящий за готовностью сервера
        /// </summary>
        private void StatusWatcherArea()
        {
            while (true)
            {
                Thread.Sleep(300);
                if (_ddeStatus == ServerConnectStatus.Connect &&
                    _transe2QuikStatus == ServerConnectStatus.Connect &&
                    _tradesStatus == ServerConnectStatus.Connect &&
                     _connectToBrokerStatus == ServerConnectStatus.Connect &&
                    _status == ServerConnectStatus.Disconnect)
                {
                    if (_lastStartServerTime.AddSeconds(40) > DateTime.Now)
                    {
                        continue;
                    }
                    _status = ServerConnectStatus.Connect;
                    SendLogMessage("Основной статус сервера изменён на Connect", LogMessageType.Connect);
                    if (ConnectStatusChangeEvent != null)
                    {
                        new Action<string>(ConnectStatusChangeEvent)(_status.ToString());
                    }
                }

                if ((_ddeStatus == ServerConnectStatus.Disconnect ||
                    _transe2QuikStatus == ServerConnectStatus.Disconnect ||
                     _connectToBrokerStatus== ServerConnectStatus.Disconnect || 
                     _tradesStatus == ServerConnectStatus.Disconnect) &&
                    _status == ServerConnectStatus.Connect)
                {
                    _status = ServerConnectStatus.Disconnect;
                    SendLogMessage("Основной статус сервера изменён на Disconnect", LogMessageType.Connect);
                    if (ConnectStatusChangeEvent != null)
                    {
                        new Action<string>(ConnectStatusChangeEvent)(_status.ToString());
                    }
                }

            }
        }

        /// <summary>
        /// статус ДДЕ сервера
        /// </summary>
        private ServerConnectStatus _ddeStatus;

        /// <summary>
        /// статус трансТуКвик библиотеки
        /// </summary>
        private ServerConnectStatus _transe2QuikStatus;

        /// <summary>
        /// подключены ли к серверу
        /// </summary>
        private ServerConnectStatus _connectToBrokerStatus;

        /// <summary>
        /// статус подгрузки тиков
        /// </summary>
        private ServerConnectStatus _tradesStatus;
       
        /// <summary>
        /// основной статус сервера. Connect если верхние три тоже коннект
        /// </summary>
        private ServerConnectStatus _status;

        /// <summary>
        /// статус сервера
        /// </summary>
        public ServerConnectStatus ServerStatus { get { return _status; } }

        /// <summary>
        /// входящее сообщение об изменении состояния подключения к Квик
        /// </summary>
        private void StatusCallback(Trans2Quik.QuikResult connectionEvent, 
            int extendedErrorCode, string infoMessage)
        {
            try
            {
                if (connectionEvent == Trans2Quik.QuikResult.DLL_CONNECTED)
                {
                    _transe2QuikStatus = ServerConnectStatus.Connect;
                    SendLogMessage("Transe2Quik изменение статуса " + _transe2QuikStatus, LogMessageType.System);
                }
                if (connectionEvent == Trans2Quik.QuikResult.DLL_DISCONNECTED)
                {
                    _transe2QuikStatus = ServerConnectStatus.Disconnect;
                    SendLogMessage("Transe2Quik изменение статуса " + _transe2QuikStatus, LogMessageType.System);
                }
                if (connectionEvent == Trans2Quik.QuikResult.QUIK_CONNECTED)
                {
                    _connectToBrokerStatus = ServerConnectStatus.Connect;
                    SendLogMessage("Соединение Квик с сервером брокера установлено. ", LogMessageType.System);
                }
                if (connectionEvent == Trans2Quik.QuikResult.QUIK_DISCONNECTED)
                {
                    _connectToBrokerStatus = ServerConnectStatus.Disconnect;
                    SendLogMessage("Соединение Квик с сервером брокера разорвано. ", LogMessageType.System);
                }
            }
            catch (Exception erorr)
            {
                SendLogMessage(erorr.ToString(), LogMessageType.Error);
            }
        }

        /// <summary>
        /// входящее сообщение об изменении состояния ДДЕ сервера
        /// </summary>
        void _serverDde_StatusChangeEvent(DdeServerStatus status)
        {
            try
            {
                if (status == DdeServerStatus.Connected)
                {
                    _ddeStatus = ServerConnectStatus.Connect;
                    SendLogMessage("DDE Server изменение статуса " + _ddeStatus, LogMessageType.System);
                }
                if (status == DdeServerStatus.Disconnected)
                {
                    _ddeStatus = ServerConnectStatus.Disconnect;
                    SendLogMessage("DDE Server изменение статуса " + _ddeStatus, LogMessageType.System);
                }
            }
            catch (Exception erorr)
            {
                SendLogMessage(erorr.ToString(), LogMessageType.Error);
            }
            
        }

        /// <summary>
        /// вызывается, когда изменилось состояние соединения
        /// </summary>
        public event Action<string> ConnectStatusChangeEvent;

// время сервера

        
        private DateTime _serverTime;

        /// <summary>
        /// время сервера
        /// </summary>
        public DateTime ServerTime
        {
            get { return _serverTime; }

            private set
            {
                if (value != _serverTime)
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
        }

        void _serverDde_UpdateTimeSecurity(DateTime timeSecurity)
        {
            ServerTime = timeSecurity;
        }

        /// <summary>
        /// изменилось время сервера
        /// </summary>
        public event Action<DateTime> TimeServerChangeEvent;

// портфели

        private List<Portfolio> _portfolios; 

        /// <summary>
        /// все портфели доступные сейчас для торгов
        /// </summary>
        public List<Portfolio> Portfolios
        {
            get { return _portfolios; }
        }

        /// <summary>
        /// взять объект Счёта по его имени
        /// </summary>
        public Portfolio GetPortfolioForName(string namePortfolio) 
        {
            if (Portfolios == null)
            {
                return null;
            }

            for (int i = 0; i < _portfolios.Count; i++)
            {
                if (_portfolios[i].Number == namePortfolio ||
                    _portfolios[i].Number.Split('@')[0] == namePortfolio)
                {
                    return _portfolios[i];
                }
            }

            return null;
        }

        /// <summary>
        /// входящие новые портфели из ДДЕ сервера
        /// </summary>
        void _serverDde_UpdatePortfolios(List<Portfolio> portfoliosNew)
        {
            if (_portfolios == null)
            {
                for (int i = 0; i < portfoliosNew.Count;i++)
                {
                    SendLogMessage(portfoliosNew[i].Number + " Создан новый портфель.", LogMessageType.System);
                }

                _portfolios = portfoliosNew;
            }
            else
            {
                for (int i = 0; i < portfoliosNew.Count; i++)
                {
                    if (string.IsNullOrWhiteSpace(portfoliosNew[i].Number))
                    {
                        continue;
                    }
                    Portfolio portfolioLock = _portfolios.Find(portfolio1 => portfolio1.Number == portfoliosNew[i].Number);

                    if (portfolioLock == null)
                    {
                        SendLogMessage(portfoliosNew[i].Number + " Создан новый портфель.", LogMessageType.System);

                        _portfolios.Add(portfoliosNew[i]);
                    }
                    else
                    {
                        break;
                    }
                }
            }

            if (PortfoliosChangeEvent != null)
            {
                PortfoliosChangeEvent(_portfolios);
            }
        }

        /// <summary>
        /// вызывается когда изменился счёт
        /// </summary>
        public event Action<List<Portfolio>> PortfoliosChangeEvent; 

// инструменты

        private List<Security> _securities; 

        /// <summary>
        /// все инструменты доступные сейчас для торгов
        /// </summary>
        public List<Security> Securities
        {
            get { return _securities; }
        }

        /// <summary>
        /// взять бумагу по названию
        /// </summary>
        public Security GetSecurityForName(string namePaper)
        {
            if (string.IsNullOrWhiteSpace(namePaper))
            {
                return null;
            }

            if(_securities == null)
            {
                return null;
            }

            Security mySecurity = null;

            for (int i = 0; i < _securities.Count; i++)
            {
                if (_securities[i].Name == namePaper)
                {
                    mySecurity = _securities[i];
                }
            }

            return mySecurity;
        }

        /// <summary>
        /// входящие бумаги из ДДЕ сервера
        /// </summary>
        void _serverDde_UpdateSecurity(Security security, decimal bestBid, decimal bestAsk)
        {
            bool newSecurity = false;

            if (_securities == null)
            {
                _securities = new List<Security>();
            }

            if (_securities.Find(s => s.Name == security.Name) == null)
            {
                _securities.Add(security);
                newSecurity = true;
            }

            if (SecuritiesChangeEvent != null && newSecurity)
            {
                SecuritiesChangeEvent(_securities);
            }

            if (NewBidAscIncomeEvent != null)
            {
                NewBidAscIncomeEvent(bestBid, bestAsk, security);
            }
        }

        /// <summary>
        /// изменился состав инструментов
        /// </summary>
        public event Action<List<Security>> SecuritiesChangeEvent;

        /// <summary>
        /// новые лучший бид и аск
        /// </summary>
        public event Action<decimal, decimal, Security> NewBidAscIncomeEvent;

        /// <summary>
        /// показать инструменты 
        /// </summary>
        public void ShowSecuritiesDialog()
        {
            SecuritiesUi ui = new SecuritiesUi(this);
            ui.ShowDialog();
        }

// Заказ инструмента на скачивание

        private object _lockerStartThisSecurity = new object();

        /// <summary>
        /// Начать выгрузку данных по инструменту. 
        /// </summary>
        /// <param name="namePaper">имя бумаги которую будем запускать</param>
        /// <param name="timeFrameBuilder">объект несущий в себе данные о таймФрейме</param>
        /// <returns>В случае удачи возвращает CandleSeries
        /// в случае неудачи null</returns>
        public CandleSeries StartThisSecurity(string namePaper, TimeFrameBuilder timeFrameBuilder)
        {
            if (_lastStartServerTime != DateTime.MinValue &&
                _lastStartServerTime.AddSeconds(15) > DateTime.Now)
            {
                return null;
            }
            // дальше по одному

            lock (_lockerStartThisSecurity)
            {
                if (namePaper == "")
                {
                    return null;
                }
                // надо запустить сервер если он ещё отключен
                if (_status != ServerConnectStatus.Connect)
                {
                    return null;
                }

                if (_securities == null || _portfolios == null)
                {
                    Thread.Sleep(5000);
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

                CandleSeries series = new CandleSeries(timeFrameBuilder, security);

                _candleManager.StartSeries(series);

                SendLogMessage("Инструмент " + series.Security.Name + "ТаймФрейм " + series.TimeFrame +
                               " успешно подключен на получение данных и прослушивание свечек", LogMessageType.System);

                if (_tickStorage != null)
                {
                    _tickStorage.SetSecurityToSave(security);
                }
               
                return series;
            }
        }

        /// <summary>
        /// остановить приём и рассылку данных по этому инструменту и ТФ
        /// </summary>
        public void StopThisSecurity(CandleSeries series) 
        {
            if (series != null)
            {
                _candleManager.StopSeries(series);
            }
        }

        /// <summary>
        /// входящие свечки из CandleManager
        /// </summary>
        void _candleManager_CandleUpdateEvent(CandleSeries series) 
        {
            if (NewCandleIncomeEvent != null)
            {
                NewCandleIncomeEvent(series);
            }
        }

        /// <summary>
        /// вызывается когда изменяется свечка
        /// </summary>
        public event Action<CandleSeries> NewCandleIncomeEvent;

        /// <summary>
        /// коннекторам подключеным к серверу необходимо перезаказать данные
        /// </summary>
        public event Action NeadToReconnectEvent;

// таблица всех сделок

        private ServerTickStorage _tickStorage;

        /// <summary>
        /// все сделки в хранилище
        /// </summary>
        private List<Trade>[] _allTrades;

        void _tickStorage_TickLoadedEvent(List<Trade>[] trades)
        {
            _allTrades = trades;
        }

        /// <summary>
        /// пришли новые сделки из ДДЕ сервера
        /// </summary>
        private void _serverDde_UpdateTrade(List<Trade> tradesNew)
        {
            if (tradesNew == null ||
                tradesNew.Count == 0)
            {
                return;
            }

            if (AllTradesTableChangeEvent != null)
            {
                AllTradesTableChangeEvent(tradesNew);
            }

            BathTradeMarketDepthData(tradesNew);

// сортируем сделки по хранилищам

            if (_allTrades == null)
            {
                _allTrades = new List<Trade>[0];
                _allTrades[0].Add(tradesNew[0]);
            }

            for (int indTrade = 0; indTrade < tradesNew.Count; indTrade++)
            {
                Trade trade = tradesNew[indTrade];

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

                        Trade lastTrade = _allTrades[i][_allTrades[i].Count - 1];

                        if (lastTrade.Time.Hour == 23 && trade.Time.Hour == 23 &&
                            lastTrade.Time.Day != trade.Time.Day)
                        {
                            // иногда из квик приходит трейд в конце сессии с другой датой
                            break;
                        }

                        _allTrades[i].Add(trade);
                        break;
                    }
                }
                if (isSave == false)
                {
                    // хранилища для инструмента нет
                    List<Trade>[] allTradesNew = new List<Trade>[_allTrades.Length + 1];
                    for (int i = 0; i < _allTrades.Length; i++)
                    {
                        allTradesNew[i] = _allTrades[i];
                    }
                    allTradesNew[allTradesNew.Length - 1] = new List<Trade>();
                    allTradesNew[allTradesNew.Length - 1].Add(trade);
                    _allTrades = allTradesNew;
                }
            }

            // перегружаем последним временем тика время сервера
            ServerTime = tradesNew[tradesNew.Count - 1].Time;

            if (_tradesStatus == ServerConnectStatus.Connect &&
                tradesNew[tradesNew.Count - 1].Time.AddSeconds(20) < _serverTime)
            {

                _tradesStatus = ServerConnectStatus.Disconnect;
                SendLogMessage("Зафиксирован вход устаревших тиков. Запущена процедура перезагрузки сервера",
                    LogMessageType.System);
                _status = ServerConnectStatus.Disconnect;
                Thread.Sleep(300);
            }
            if (_tradesStatus == ServerConnectStatus.Disconnect &&
                tradesNew[tradesNew.Count - 1].Time.AddSeconds(10) > _serverTime)
            {
                CheckTrades();
                _tradesStatus = ServerConnectStatus.Connect;
                SendLogMessage("Зафиксирован переход тиков в онЛайн трансляцию. ", LogMessageType.System);
            }

            if (NewTradeEvent != null)
            {
                foreach (var trades in _allTrades)
                {
                    if (tradesNew[0].SecurityNameCode == trades[0].SecurityNameCode)
                    {
                        NewTradeEvent(trades);
                        break;
                    }
                }
            }
        }

        /// <summary>
        /// взять все сделки по инструменту
        /// </summary>
        public List<Trade> GetAllTradesToSecurity(Security security) 
        {
            for (int i = 0;_allTrades != null && i < _allTrades.Length; i++)
            {
                if (_allTrades[i] != null && _allTrades[i].Count != 0 &&
                    _allTrades[i][0].SecurityNameCode == security.Name)
                {
                    return _allTrades[i];
                }
            }
            return null;
        }

        /// <summary>
        /// все тики имеющиеся у сервера
        /// </summary>
        public List<Trade>[] AllTrades { get { return _allTrades; }}

        /// <summary>
        /// проверяем валидность хранилища тиков
        /// </summary>
        private void CheckTrades()
        {
            // это костыль от передачи тиков из квик в смешанном виде
            // когда разные тикеры попадают в одно хранилище

            if (_allTrades == null||
                _allTrades.Length == 0)
            {
                return;
            }

            for (int indArray = 0; indArray < _allTrades.Length; indArray ++)
            {
                List<Trade> trades = _allTrades[indArray];
                for (int i = trades.Count-1; i > 0; i--)
                {
                    decimal persent = trades[i].Price/trades[i - 1].Price;

                    if (persent > 3m ||
                        persent < 0.1m)
                    {
                        trades.Remove(trades[i - 1]);
                    }
                }
            }
        }

        /// <summary>
        /// вызывается когда по инструменту приходят новые сделки
        /// </summary>
        public event Action<List<Trade>> NewTradeEvent;

        /// <summary>
        /// изменилась таблица всех сделок
        /// </summary>
        public event Action<List<Trade>> AllTradesTableChangeEvent;

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
        private void BathTradeMarketDepthData(List<Trade> trades)
        {
            MarketDepth depth = null;

            for (int i = 0; i < trades.Count; i++)
            {
                if (i != 0 && depth == null &&
                    trades[i - 1].SecurityNameCode == trades[i].SecurityNameCode)
                {
                    continue;
                }

                if (depth == null ||
                    depth.SecurityNameCode != trades[i].SecurityNameCode)
                {
                    depth = _marketDepths.Find(d => d.SecurityNameCode == trades[i].SecurityNameCode);
                }

                if (depth == null ||
                    depth.Asks == null || depth.Asks.Count == 0 ||
                    depth.Bids == null || depth.Bids.Count == 0)
                {
                    return;
                }

                trades[i].Ask = depth.Asks[0].Price;
                trades[i].Bid = depth.Bids[0].Price;
                trades[i].BidsVolume = depth.BidSummVolume;
                trades[i].AsksVolume = depth.AskSummVolume;
            }
        }

        /// <summary>
        /// пришёл новый стакан
        /// </summary>
        void _serverDde_UpdateGlass(MarketDepth marketDepth)
        {
            marketDepth.Time = DateTime.Now;

            if (NewMarketDepthEvent != null)
            {
                NewMarketDepthEvent(marketDepth);
            }

            // грузим стаканы в хранилище
            for (int i = 0; i < _marketDepths.Count; i++)
            {
                if (_marketDepths[i].SecurityNameCode == marketDepth.SecurityNameCode)
                {
                    _marketDepths[i] = marketDepth;
                    return;
                }
            }
            _marketDepths.Add(marketDepth);
        }

        /// <summary>
        /// вызывается когда пришёл новый стакан
        /// </summary>
        public event Action<MarketDepth> NewMarketDepthEvent;

// новая моя сделка

        private void PfnTradeStatusCallback(int nMode, ulong nNumber, ulong nOrderNumber, string classCode,
        string secCode, double dPrice, long nQty, double dValue, int nIsSell, IntPtr pTradeDescriptor)
        {
            try
            {

                MyTrade trade = new MyTrade();
                trade.NumberTrade = nNumber.ToString();
                trade.NumberOrderParent = nOrderNumber.ToString();
                trade.Price = (decimal)dPrice;
                trade.SecurityNameCode = secCode;

                if (ServerTime != DateTime.MinValue)
                {
                    trade.Time = ServerTime;
                }
                else
                {
                    trade.Time = DateTime.Now;
                }

                trade.Volume = Convert.ToInt32(nQty);

                if (_myTrades == null)
                {
                    _myTrades = new List<MyTrade>();
                }

                if (nIsSell == 0)
                {
                    trade.Side = Side.Buy;
                }
                else
                {
                    trade.Side = Side.Sell;
                }

                _myTrades.Add(trade);

                if (NewMyTradeEvent != null)
                {
                    NewMyTradeEvent(trade);
                }
            }
            catch (Exception error)
            {
                SendLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        private List<MyTrade> _myTrades;

        /// <summary>
        /// мои сделки
        /// </summary>
        public List<MyTrade> MyTrades
        {
            get { return _myTrades; }
        }

        /// <summary>
        /// вызывается когда приходит новая Моя Сделка
        /// </summary>
        public event Action<MyTrade> NewMyTradeEvent; 


// работа с ордерами

        /// <summary>
        /// место работы потока отправляющего заявки на биржу
        /// </summary>
        private void ExecutorOrdersThreadArea()
        {
            while (true)
            {
                Thread.Sleep(10);
                if (_ordersToCansel != null && _ordersToCansel.Count != 0)
                {
                    Order order;
                    if (_ordersToCansel.TryDequeue(out order))
                    {
                        if (_canseledOrders == null)
                        {
                            _canseledOrders = new List<decimal[]>();
                        }

                        decimal[] record = _canseledOrders.Find(decimals => decimals[0] == order.NumberUser);

                        if (record != null)
                        {
                            record[1] += 1;
                            // если алгоритм пытается снять одну заявку пять раз, то снятие этой заявки будет проигнорировано
                            if (record[1] > 5)
                            {
                                continue;
                            }
                        }
                        else
                        {
                            record = new[] { Convert.ToDecimal(order.NumberUser), 1 };
                            _canseledOrders.Add(record);
                        }

                        string command = ConvertToKillQuikOrder(order);

                        int error;
                        var msg = new StringBuilder(256);


                        if (command == null)
                        {
                            continue;
                        }

                        Trans2Quik.QuikResult result = Trans2Quik.SEND_ASYNC_TRANSACTION(command, out error, msg, msg.Capacity);

                        SendLogMessage(result.ToString(), LogMessageType.System);
                    }
                }

                if (_ordersToExecute != null && _ordersToExecute.Count != 0)
                {
                    Order order;
                    if (_ordersToExecute.TryDequeue(out order))
                    {
                        string command = ConvertToSimpleQuikOrder(order);

                        int error;
                        var msg = new StringBuilder(256);

                        if (command == null)
                        {
                            continue;
                        }

                        order.TimeCreate = ServerTime;

                        Trans2Quik.QuikResult result = Trans2Quik.SEND_ASYNC_TRANSACTION(command, out error, msg, msg.Capacity);

                        if (msg.ToString() != "")
                        {
                            Order newOrder = new Order();
                            newOrder.NumberUser = order.NumberUser;
                            newOrder.State = OrderStateType.Fail;
                            if (NewOrderIncomeEvent != null)
                            {
                                NewOrderIncomeEvent(newOrder);
                            }
                            continue;
                        }

                        SendLogMessage(result.ToString(), LogMessageType.System);
                    }
                }
            }
        }

        /// <summary>
        /// очередь заявок на выставление
        /// </summary>
        private ConcurrentQueue<Order> _ordersToExecute;

        /// <summary>
        /// очередь заявок на отмену
        /// </summary>
        private ConcurrentQueue<Order> _ordersToCansel; 

        /// <summary>
        /// выслать Ордер на исполнение
        /// </summary>
        public void ExecuteOrder(Order order) 
        {
           _ordersToExecute.Enqueue(order);
        }

        /// <summary>
        /// снять Ордер
        /// </summary>
        public void CanselOrder(Order order) 
        {
          
            SetOrderToClose(order);
        }

        /// <summary>
        /// преобразовать ордер Os.Engine в строку выставления ордера Квик
        /// </summary>
        private string ConvertToSimpleQuikOrder(Order order)
        {
            Portfolio myPortfolio = GetPortfolioForName(order.PortfolioNumber);

            Security mySecurity = GetSecurityForName(order.SecurityNameCode);

            if (myPortfolio == null ||
                mySecurity == null)
            {
                return null;
            }


            var cookie = order.NumberUser;

            var operation = order.Side == Side.Buy ? "B" : "S";

            string price = order.Price.ToString(new CultureInfo("ru-RU"));

            string type;

            if (order.TypeOrder == OrderPriceType.Limit)
            {
                type = "L";
            }
            else
            {
                type = "M";
                price = "0";
            }

            string accaunt = "";

            string codeClient = "";

            if (myPortfolio.Number.Split('@').Length == 1)
            {
                accaunt = myPortfolio.Number;
                codeClient = "";
            }
            else
            {
                accaunt = myPortfolio.Number.Split('@')[0];
                codeClient = myPortfolio.Number.Split('@')[1];
            }

            string command = "";

            if (codeClient == "")
            {
                command =
                    "TRANS_ID=" + cookie +
                    "; ACCOUNT=" + accaunt +
                    // "; CLIENT_CODE=" + ClientCode +
                    "; SECCODE=" + mySecurity.Name +
                    "; CLASSCODE=" + mySecurity.NameClass +
                    "; TYPE=" + type +
                    "; ACTION=NEW_ORDER; OPERATION=" + operation +
                    "; PRICE=" + price +
                    "; QUANTITY=" + order.Volume +
                    ";";
            }
            else
            {
                command =
                    "TRANS_ID=" + cookie +
                    "; ACCOUNT=" + accaunt +
                    "; CLIENT_CODE=" + codeClient +
                    "; SECCODE=" + mySecurity.Name +
                    "; CLASSCODE=" + mySecurity.NameClass +
                    "; TYPE=" + type +
                    "; ACTION=NEW_ORDER; OPERATION=" + operation +
                    "; PRICE=" + price +
                    "; QUANTITY=" + order.Volume +
                    ";";
            }
            return command;
        }

        /// <summary>
        /// преобразовать ордер Os.Engine в строку отзыва ордера Квик
        /// </summary>
        private string ConvertToKillQuikOrder(Order order)
        {
            Portfolio myPortfolio = GetPortfolioForName(order.PortfolioNumber);

            Security mySecurity = GetSecurityForName(order.SecurityNameCode);

            if (myPortfolio == null ||
                mySecurity == null)
            {
                return null;
            }


            var cookie = order.NumberMarket;
            // CLASSCODE = TQBR; SECCODE = RU0009024277; TRANS_ID = 5; ACTION = KILL_ORDER; ORDER_KEY = 503983;

            string accaunt = "";

            string codeClient = "";

            if (myPortfolio.Number.Split('@').Length == 1)
            {
                accaunt = myPortfolio.Number;
                codeClient = "";
            }
            else
            {
                accaunt = myPortfolio.Number.Split('@')[0];
                codeClient = myPortfolio.Number.Split('@')[1];
            }

            string command = "";

            if (codeClient == "")
            {
                command =
                      "TRANS_ID=" + NumberGen.GetNumberOrder() +
                      "; ACCOUNT=" + accaunt +
                      "; SECCODE=" + mySecurity.Name +
                      "; CLASSCODE=" + mySecurity.NameClass +
                      "; ACTION=KILL_ORDER" +
                      "; ORDER_KEY=" + order.NumberMarket +
                      ";";
            }
            else
            {
                command =
                      "TRANS_ID=" + NumberGen.GetNumberOrder() +
                      "; ACCOUNT=" + accaunt +
                      "; CLIENT_CODE=" + codeClient +
                      "; SECCODE=" + mySecurity.Name +
                      "; CLASSCODE=" + mySecurity.NameClass +
                      "; ACTION=KILL_ORDER" +
                      "; ORDER_KEY=" + order.NumberMarket +
                      ";";

            }

            return command;
        }

// работа по повторному запросу снятия ордеров, т.к. бывает что из Квик они не с первого раза снимаются

        private List<decimal[]> _canseledOrders; 

        /// <summary>
        /// ордера которые нужно снять из ТС
        /// </summary>
        private Order[] _closedOrders; 

        /// <summary>
        /// объект блокирующий многопоточный доступ в SetOrderToClose
        /// </summary>
        private object toCloseSenderLocker = new object(); 

        /// <summary>
        /// загрузить ордер в списки на отзыв
        /// </summary>
        private void SetOrderToClose(Order newOrderToClose)
        {
            lock (toCloseSenderLocker)
            {
                if (_closedOrders != null)
                {
                    for (int i = 0; i < _closedOrders.Length; i++)
                    {
                        if (_closedOrders[i].NumberUser == newOrderToClose.NumberUser)
                        {
                            return;
                        }
                    }
                }

                if (_closedOrders == null)
                {
                    _closedOrders = new[] { newOrderToClose };
                }
                else
                {
                    Order[] newOrders = new Order[_closedOrders.Length + 1];

                    for (int i = 0; i < _closedOrders.Length; i++)
                    {
                        newOrders[i] = _closedOrders[i];
                    }
                    newOrders[newOrders.Length - 1] = newOrderToClose;
                    _closedOrders = newOrders;
                }
            }
        }

        /// <summary>
        /// место работы потока отвечающего за отзыв заявок
        /// </summary>
        private void CloseOrderThreadHome()
        {
            while (true)
            {
                try
                {
                    Thread.Sleep(2000);

                    Order[] orders = _closedOrders;

                    if (orders != null)
                    {
                        for (int i = 0; i < orders.Length; i++)
                        {
                            if (orders[i].State == OrderStateType.None)
                            {
                                if (string.IsNullOrEmpty(orders[i].NumberMarket) || orders[i].State == OrderStateType.None)
                                { // если id не успел долететь до сделки
                                    Order ord = GetOrderFromUserId(orders[i].NumberUser.ToString());
                                    if (ord != null)
                                    {
                                        orders[i] = ord;
                                    }
                                }
                            }
                            if (orders[i].State == OrderStateType.Activ)
                            {
                                _ordersToCansel.Enqueue(orders[i]);
                            }
                        }
                    }
                }
                catch (Exception)
                {
                    // ignore
                }
            }
        }

// входящие из торговой системы ордера

        /// <summary>
        /// входящие ордера по протоколам Trance2Quik
        /// </summary>
        private void TransactionReplyCallback(int nTransactionResult, int transactionExtendedErrorCode,
           int transactionReplyCode, uint dwTransId, ulong dOrderNum, string transactionReplyMessage, IntPtr pTransReplyDescriptor)
        {
            try
            {
                Order order = GetOrderFromUserId(dwTransId.ToString());

                if (order == null)
                {
                    order = new Order();
                    order.NumberUser = Convert.ToInt32(dwTransId);
                }

                if (dOrderNum != 0)
                {
                    order.NumberMarket = dOrderNum.ToString(new CultureInfo("ru-RU"));
                }

                if (transactionReplyCode == 6)
                {
                    order.State = OrderStateType.Fail;

                    SendLogMessage(dwTransId + " Ошибка выставления заявки " + transactionReplyMessage, LogMessageType.System);

                    if (NewOrderIncomeEvent != null)
                    {
                        NewOrderIncomeEvent(order);
                    }
                }
                else
                {
                    if (order.NumberMarket == "0" || dOrderNum == 0)
                    {
                        return;
                    }
                    order.State = OrderStateType.Activ;
                    if (NewOrderIncomeEvent != null)
                    {
                        NewOrderIncomeEvent(order);
                    }
                }
            }
            catch (Exception erorr)
            {
                SendLogMessage(erorr.ToString(), LogMessageType.Error);
            }
        }

        // из трансТуКвик пришло оповещение об ордере
        private void PfnOrderStatusCallback(int nMode, int dwTransId, ulong nOrderNum, string classCode, string secCode,
            double dPrice, long l, double dValue, int nIsSell, int nStatus, IntPtr pOrderDescriptor)
        {
            try
            {
                if (dwTransId == 0)
                {
                    return;
                }
                Order order = new Order();

                order.SecurityNameCode = secCode;
                order.ServerType = ServerType.QuikDde;
                order.Price = (decimal)dPrice;
                order.Volume = (int)dValue;
                order.NumberUser = dwTransId;
                order.NumberMarket = nOrderNum.ToString();

                if (ServerTime != DateTime.MinValue)
                {
                    order.TimeCallBack = ServerTime;
                }
                else
                {
                    order.TimeCallBack = DateTime.Now;
                }

                if (nIsSell == 0)
                {
                    order.Side = Side.Buy;
                }
                else
                {
                    order.Side = Side.Sell;
                }

                if (nStatus == 1)
                {
                    order.State = OrderStateType.Activ;
                }
                else if (nStatus == 2)
                {
                    order.State = OrderStateType.Cancel;
                }
                else
                {
                    order.State = OrderStateType.Done;
                }

                SetOrder(order);

                if (NewOrderIncomeEvent != null)
                {
                    NewOrderIncomeEvent(order);
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
            }
            catch (Exception error)
            {
                SendLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        /// <summary>
        /// сохранить ордер в массив
        /// </summary>
        private void SetOrder(Order newOrder)
        {
            lock (_orderSenderLocker)
            {
                if (_allOrders != null)
                {
                    for (int i = 0; i < _allOrders.Length; i++)
                    {
                        if (_allOrders[i].NumberUser == newOrder.NumberUser)
                        {
                            _allOrders[i] = newOrder;
                            return;
                        }
                    }
                }

                if (_allOrders == null)
                {
                    _allOrders = new[] { newOrder };
                }
                else
                {
                    Order[] newOrders = new Order[_allOrders.Length + 1];

                    for (int i = 0; i < _allOrders.Length; i++)
                    {
                        newOrders[i] = _allOrders[i];
                    }
                    newOrders[newOrders.Length - 1] = newOrder;
                    _allOrders = newOrders;
                }
            }
        }

        /// <summary>
        /// все ордера пришедшие из сервера
        /// </summary>
        private Order[] _allOrders;

        /// <summary>
        /// объект блокирующий многопоточный доступ в SetOrder
        /// </summary>
        private object _orderSenderLocker = new object();

        /// <summary>
        /// взять ордер по внутреннему Id
        /// </summary>
        private Order GetOrderFromUserId(string userId)
        {
            if (_allOrders == null)
            {
                return null;
            }

            for (int i = 0; i < _allOrders.Length; i++)
            {
                if (_allOrders[i].NumberUser.ToString() == userId)
                {
                    return _allOrders[i];
                }
            }
            return null;
        }

        /// <summary>
        ///  взять ордер по Id биржи
        /// </summary>
        private Order GetOrderFromMarketId(string marketId)
        {
            if (_allOrders == null)
            {
                return null;
            }

            for (int i = 0; i < _allOrders.Length; i++)
            {
                if (_allOrders[i].NumberMarket == marketId)
                {
                    return _allOrders[i];
                }
            }
            return null;
        }

        /// <summary>
        /// вызывается когда изменился ордер
        /// </summary>
        public event Action<Order> NewOrderIncomeEvent;

// работа с логами

        /// <summary>
        /// сообщение из ДДЕ привода
        /// </summary>
        void _serverDde_LogMessageEvent(string message, LogMessageType type)
        {
            if (LogMessageEvent != null)
            {
                LogMessageEvent(message, type);
            }
        }

        /// <summary>
        /// сохранить новую запись в лог
        /// </summary>
        private void SendLogMessage(string message, LogMessageType type)
        {
            if (LogMessageEvent != null)
            {
                LogMessageEvent(message, type);
            }
        }

        /// <summary>
        /// лог менеджер
        /// </summary>
        private Log _logMaster;

        /// <summary>
        /// вызывается когда есть новое сообщение в логе
        /// </summary>
        public event Action<string, LogMessageType> LogMessageEvent;

    }
}


