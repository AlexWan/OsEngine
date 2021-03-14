/*
 *Your rights to use the code are governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 *Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net;
using System.Net.NetworkInformation;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows;
using OsEngine.Entity;
using OsEngine.Language;
using OsEngine.Logging;
using OsEngine.Market.Servers.Entity;
using Action = System.Action;

namespace OsEngine.Market.Servers.Finam
{

    /// <summary>
    /// class-server for connection to Finam
    /// класс - сервер для подключения к Финам
    /// </summary>
    public class FinamServer : IServer
    {

        /// <summary>
        /// constructor
        /// конструктор
        /// </summary>
        public FinamServer()
        {
            if (!Directory.Exists(@"Data\Temp\"))
            {
                Directory.CreateDirectory(@"Data\Temp\");
            }

            ServerAdress = "export.finam.ru";
            ServerStatus = ServerConnectStatus.Disconnect;
            ServerType = ServerType.Finam;

            Load();

            _logMaster = new Log("FinamServer", StartProgram.IsOsData);
            _logMaster.Listen(this);

            _serverStatusNead = ServerConnectStatus.Disconnect;

            _threadPrime = new Thread(PrimeThreadArea);
            _threadPrime.CurrentCulture = new CultureInfo("ru-RU");
            _threadPrime.IsBackground = true;
            _threadPrime.Start();


            Thread threadDataSender = new Thread(SenderThreadArea);
            threadDataSender.CurrentCulture = new CultureInfo("ru-RU");
            threadDataSender.IsBackground = true;
            threadDataSender.Start();

            _tradesToSend = new ConcurrentQueue<List<Trade>>();
            _securitiesToSend = new ConcurrentQueue<List<Security>>();
            _candleSeriesToSend = new ConcurrentQueue<CandleSeries>();
            _marketDepthsToSend = new ConcurrentQueue<MarketDepth>();
            _portfolioToSend = new ConcurrentQueue<List<Portfolio>>();
            _bidAskToSend = new ConcurrentQueue<BidAskSender>();

            Thread worker = new Thread(ThreadDownLoaderArea);
            worker.Name = "ThinamLoaderThread";
            worker.IsBackground = true;
            worker.Start();
        }

        /// <summary>
        /// take server type
        /// взять тип сервера
        /// </summary>
        /// <returns></returns>
        public ServerType ServerType { get; set; }

        /// <summary>
        /// show settings
        /// показать настройки
        /// </summary>
        public void ShowDialog()
        {
            if (_ui == null)
            {
                _ui = new FinamServerUi(this, _logMaster);
                _ui.Show();
                _ui.Closing += (sender, args) => { _ui = null; };
            }
            else
            {
                _ui.Activate();
            }

        }

        /// <summary>
        /// item control window
        /// окно управления элемента
        /// </summary>
        private FinamServerUi _ui;

        /// <summary>
        /// server address to connect to server
        /// адрес сервера по которому нужно соединяться с сервером
        /// </summary>
        public string ServerAdress;

        /// <summary>
        /// take server settings from file
        /// загрузить настройки сервера из файла
        /// </summary>
        public void Load()
        {
            if (!File.Exists(@"Engine\" + @"FinamServer.txt"))
            {
                return;
            }

            try
            {
                using (StreamReader reader = new StreamReader(@"Engine\" + @"FinamServer.txt"))
                {
                    ServerAdress = reader.ReadLine();

                    reader.Close();
                }
            }
            catch (Exception)
            {
                // ignored
            }
        }

        /// <summary>
        /// save server settings in file
        /// сохранить настройки сервера в файл
        /// </summary>
        public void Save()
        {
            try
            {
                using (StreamWriter writer = new StreamWriter(@"Engine\" + @"FinamServer.txt", false))
                {
                    writer.WriteLine(ServerAdress);

                    writer.Close();
                }
            }
            catch (Exception)
            {
                // ignored
            }
        }

        // server status
        // статус сервера

        private ServerConnectStatus _serverConnectStatus;

        /// <summary>
        /// server status
        /// статус сервера
        /// </summary>
        public ServerConnectStatus ServerStatus
        {
            get { return _serverConnectStatus; }
            private set
            {
                if (value != _serverConnectStatus)
                {
                    _serverConnectStatus = value;
                    SendLogMessage(_serverConnectStatus + OsLocalization.Market.Message7, LogMessageType.Connect);
                    if (ConnectStatusChangeEvent != null)
                    {
                        ConnectStatusChangeEvent(_serverConnectStatus.ToString());
                    }
                }
            }
        }

        /// <summary>
        /// called when connection status changed
        /// вызывается когда статус соединения изменяется
        /// </summary>
        public event Action<string> ConnectStatusChangeEvent;

        public int CountDaysTickNeadToSave { get; set; }

        public bool NeadToSaveTicks { get; set; }

        // connection / disconnection
        // подключение / отключение

        /// <summary>
        /// start server
        /// запустить сервер
        /// </summary>
        public void StartServer()
        {
            if (ServerStatus == ServerConnectStatus.Connect)
            {
                SendLogMessage(OsLocalization.Market.Message2, LogMessageType.System);
                return;
            }
            _serverStatusNead = ServerConnectStatus.Connect;
        }

        /// <summary>
        /// stop server
        /// остановить сервер
        /// </summary>
        public void StopServer()
        {
            _serverStatusNead = ServerConnectStatus.Disconnect;
        }

        /// <summary>
        /// needed server status. Need a thread that monitors the connection. Depending on this field controls the connection
        /// нужный статус сервера. Нужен потоку который следит за соединением
        /// В зависимости от этого поля управляет соединением
        /// </summary>
        private ServerConnectStatus _serverStatusNead;

        // main thread work !!!!!!
        // работа основного потока !!!!!!

        /// <summary>
        /// main thread that controls connection, downloading portfolios and securities, sending to up
        /// основной поток, следящий за подключением, загрузкой портфелей и бумаг, пересылкой данных на верх
        /// </summary>
        private Thread _threadPrime;

        /// <summary>
        /// the place where the connection is controlled. listen to data streams
        /// место в котором контролируется соединение. опрашиваются потоки данных
        /// </summary>
        [System.Runtime.ExceptionServices.HandleProcessCorruptedStateExceptionsAttribute]
        private void PrimeThreadArea()
        {
            while (true)
            {
                Thread.Sleep(1000);
                try
                {
                    if (_serverStatusNead == ServerConnectStatus.Connect &&
                        _serverConnectStatus == ServerConnectStatus.Disconnect)
                    {
                        SendLogMessage(OsLocalization.Market.Message8, LogMessageType.System);
                        CheckServer();
                        continue;
                    }
                    if (_serverConnectStatus == ServerConnectStatus.Disconnect)
                    {
                        continue;
                    }

                    if (_serverStatusNead == ServerConnectStatus.Disconnect &&
                        _serverConnectStatus == ServerConnectStatus.Connect)
                    {
                        ServerStatus = ServerConnectStatus.Disconnect;
                        continue;
                    }

                    if (_getSecurities == false)
                    {
                        SendLogMessage(OsLocalization.Market.Message50, LogMessageType.System);
                        GetSecurities();
                        CreatePortfolio();
                        _getSecurities = true;
                        continue;
                    }

                    if (Securities == null)
                    {
                        _getSecurities = false;
                    }
                }
                catch (Exception error)
                {
                    SendLogMessage(error.ToString(), LogMessageType.Error);
                    ServerStatus = ServerConnectStatus.Disconnect;

                    Thread.Sleep(5000);
                    // reconect / переподключаемся
                    _threadPrime = new Thread(PrimeThreadArea);
                    _threadPrime.CurrentCulture = new CultureInfo("ru-RU");
                    _threadPrime.IsBackground = true;
                    _threadPrime.Start();

                    if (NeadToReconnectEvent != null)
                    {
                        NeadToReconnectEvent();
                    }

                    return;
                }
            }
        }

        /// <summary>
        /// server time of last starting
        /// время последнего старта сервера
        /// </summary>
        public DateTime LastStartServerTime { get; set; }

        /// <summary>
        /// shows whether portfolios and securities downloaded
        /// скачаны ли портфели и бумаги
        /// </summary>
        private bool _getSecurities;

        /// <summary>
        /// start connection
        /// начать процесс подключения
        /// </summary>
        private void CheckPing()
        {
            Ping ping = new Ping();

            PingReply pingReply = ping.Send(ServerAdress);

            if (pingReply == null || pingReply.Status != IPStatus.Success ||
                pingReply.RoundtripTime > 1000)
            { // if something is wrong, we exit / если что-то не так - выходим
                SendLogMessage("Server response fail, ping is " + pingReply.Status + ". wrong address or internet fail", LogMessageType.Error);
                return;
            }

            ServerStatus = ServerConnectStatus.Connect;

            Thread.Sleep(10000);
        }

        /// <summary>
        /// check server page availability
        /// проверка доступности страницы сервера
        /// </summary>
        private void CheckServer()
        {
            String pageContent = GetPage("http://" + ServerAdress);

            if (pageContent.Length == 0)
            { // if there is no content, we exit / если нет контента - выходим
                SendLogMessage(OsLocalization.Market.Message51, LogMessageType.Error);
                return;
            }

            ServerStatus = ServerConnectStatus.Connect;

            Thread.Sleep(10000);
        }

        public static string GetPage(string uri)
        {
            if (ServicePointManager.SecurityProtocol != SecurityProtocolType.Tls12)
            {
                ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
            }

            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(uri);

            HttpWebResponse response = (HttpWebResponse)request.GetResponse();

            string resultPage = "";

            using (StreamReader sr = new StreamReader(response.GetResponseStream(), Encoding.Default, true))
            {
                resultPage = sr.ReadToEnd();
                sr.Close();
            }
            return resultPage;
        }

        /// <summary>
        /// get path to the latest cashed version of icharts.js
        /// получить путь к последней кешированной версии icharts.js
        /// </summary>
        public static string GetIchartsPath()
        {
            var response = GetPage("https://www.finam.ru/profile/moex-akcii/gazprom/export/");
            return Regex.Match(response, @"\/cache\/.*\/icharts\/icharts\.js", RegexOptions.IgnoreCase).Value;
        }

        /// <summary>
        /// includes loading of tools and portfolios
        /// включает загрузку инструментов и портфелей
        /// </summary>
        private void GetSecurities()
        {
            var response = GetPage($"https://www.finam.ru{GetIchartsPath()}");

            string[] arraySets = response.Split('=');
            string[] arrayIds = arraySets[1].Split('[')[1].Split(']')[0].Split(',');

            string names = arraySets[2].Split('[')[1].Split(']')[0];

            List<string> arrayNames = new List<string>();

            string name = "";

            for (int i = 1; i < names.Length; i++)
            {
                if ((names[i] == '\'' && i + 1 == names.Length)
                    ||
                    (names[i] == '\'' && names[i + 1] == ',' && names[i + 2] == '\''))
                {
                    arrayNames.Add(name);
                    name = "";
                    i += 2;
                }
                else
                {
                    name += names[i];
                }
            }
            string[] arrayCodes = arraySets[3].Split('[')[1].Split(']')[0].Split(',');
            string[] arrayMarkets = arraySets[4].Split('[')[1].Split(']')[0].Split(',');
            string[] arrayDecp = arraySets[5].Split('{')[1].Split('}')[0].Split(',');
            string[] arrayFormatStrs = arraySets[6].Split('[')[1].Split(']')[0].Split(',');
            string[] arrayEmitentChild = arraySets[7].Split('[')[1].Split(']')[0].Split(',');
            string[] arrayEmitentUrls = arraySets[8].Split('{')[1].Split('}')[0].Split(',');

            _finamSecurities = new List<FinamSecurity>();

            for (int i = 0; i < arrayIds.Length; i++)
            {
                _finamSecurities.Add(new FinamSecurity());

                _finamSecurities[i].Code = arrayCodes[i].TrimStart('\'').TrimEnd('\'');
                _finamSecurities[i].Decp = arrayDecp[i].Split(':')[1];
                _finamSecurities[i].EmitentChild = arrayEmitentChild[i];
                _finamSecurities[i].Id = arrayIds[i];
                _finamSecurities[i].Name = arrayNames[i];
                _finamSecurities[i].Url = arrayEmitentUrls[i].Split(':')[1];

                _finamSecurities[i].MarketId = arrayMarkets[i];

                if (_finamSecurities[i].MarketId == "7")
                {
                    _finamSecurities[i].Name =
                        _finamSecurities[i].Name.Replace("*", "")
                            .Replace("-", "")
                            .Replace("_", "")
                            .ToUpper();

                    _finamSecurities[i].Code = _finamSecurities[i].Name;

                    if (_finamSecurities[i].Name == "MINI D&JFUT")
                    {
                        _finamSecurities[i].Code = "DANDI.MINIFUT";
                    }

                }

                if (Convert.ToInt32(arrayMarkets[i]) == 200)
                {
                    _finamSecurities[i].Market = "МосБиржа топ";
                }
                else if (Convert.ToInt32(arrayMarkets[i]) == 1)
                {
                    _finamSecurities[i].Market = "МосБиржа акции";
                }
                else if (Convert.ToInt32(arrayMarkets[i]) == 14)
                {
                    _finamSecurities[i].Market = "МосБиржа фьючерсы";
                }
                else if (Convert.ToInt32(arrayMarkets[i]) == 41)
                {
                    _finamSecurities[i].Market = "Курс рубля";
                }
                else if (Convert.ToInt32(arrayMarkets[i]) == 45)
                {
                    _finamSecurities[i].Market = "МосБиржа валютный рынок";
                }
                else if (Convert.ToInt32(arrayMarkets[i]) == 2)
                {
                    _finamSecurities[i].Market = "МосБиржа облигации";
                }
                else if (Convert.ToInt32(arrayMarkets[i]) == 12)
                {
                    _finamSecurities[i].Market = "МосБиржа внесписочные облигации";
                }
                else if (Convert.ToInt32(arrayMarkets[i]) == 29)
                {
                    _finamSecurities[i].Market = "МосБиржа пифы";
                }
                else if (Convert.ToInt32(arrayMarkets[i]) == 515)
                {
                    _finamSecurities[i].Market = "Мосбиржа ETF";
                }
                else if (Convert.ToInt32(arrayMarkets[i]) == 8)
                {
                    _finamSecurities[i].Market = "Расписки";
                }
                else if (Convert.ToInt32(arrayMarkets[i]) == 519)
                {
                    _finamSecurities[i].Market = "Еврооблигации";
                }
                else if (Convert.ToInt32(arrayMarkets[i]) == 517)
                {
                    _finamSecurities[i].Market = "Санкт-Петербургская биржа";
                }
                else if (Convert.ToInt32(arrayMarkets[i]) == 6)
                {
                    _finamSecurities[i].Market = "Мировые индексы";
                }
                else if (Convert.ToInt32(arrayMarkets[i]) == 24)
                {
                    _finamSecurities[i].Market = "Товары";
                }
                else if (Convert.ToInt32(arrayMarkets[i]) == 5)
                {
                    _finamSecurities[i].Market = "Мировые валюты";
                }
                else if (Convert.ToInt32(arrayMarkets[i]) == 25)
                {
                    _finamSecurities[i].Market = "Акции США(BATS)";
                }
                else if (Convert.ToInt32(arrayMarkets[i]) == 7)
                {
                    _finamSecurities[i].Market = "Фьючерсы США";
                }
                else if (Convert.ToInt32(arrayMarkets[i]) == 27)
                {
                    _finamSecurities[i].Market = "Отрасли экономики США";
                }
                else if (Convert.ToInt32(arrayMarkets[i]) == 26)
                {
                    _finamSecurities[i].Market = "Гособлигации США";
                }
                else if (Convert.ToInt32(arrayMarkets[i]) == 28)
                {
                    _finamSecurities[i].Market = "ETF";
                }
                else if (Convert.ToInt32(arrayMarkets[i]) == 30)
                {
                    _finamSecurities[i].Market = "Индексы мировой экономики";
                }
                else if (Convert.ToInt32(arrayMarkets[i]) == 91)
                {
                    _finamSecurities[i].Market = "Российские индексы";
                }
                else if (Convert.ToInt32(arrayMarkets[i]) == 3)
                {
                    _finamSecurities[i].Market = "РТС";
                }
                else if (Convert.ToInt32(arrayMarkets[i]) == 20)
                {
                    _finamSecurities[i].Market = "RTS Board";
                }
                else if (Convert.ToInt32(arrayMarkets[i]) == 10)
                {
                    _finamSecurities[i].Market = "РТС-GAZ";
                }
                else if (Convert.ToInt32(arrayMarkets[i]) == 17)
                {
                    _finamSecurities[i].Market = "ФОРТС Архив";
                }
                else if (Convert.ToInt32(arrayMarkets[i]) == 31)
                {
                    _finamSecurities[i].Market = "Сырье Архив";
                }
                else if (Convert.ToInt32(arrayMarkets[i]) == 38)
                {
                    _finamSecurities[i].Market = "RTS Standard Архив";
                }
                else if (Convert.ToInt32(arrayMarkets[i]) == 16)
                {
                    _finamSecurities[i].Market = "ММВБ Архив";
                }
                else if (Convert.ToInt32(arrayMarkets[i]) == 18)
                {
                    _finamSecurities[i].Market = "РТС Архив";
                }
                else if (Convert.ToInt32(arrayMarkets[i]) == 9)
                {
                    _finamSecurities[i].Market = "СПФБ Архив";
                }
                else if (Convert.ToInt32(arrayMarkets[i]) == 32)
                {
                    _finamSecurities[i].Market = "РТС-BOARD Архив";
                }
                else if (Convert.ToInt32(arrayMarkets[i]) == 39)
                {
                    _finamSecurities[i].Market = "Расписки Архив";
                }
                else if (Convert.ToInt32(arrayMarkets[i]) == -1)
                {
                    _finamSecurities[i].Market = "Отрасли";
                }
                else if (Convert.ToInt32(arrayMarkets[i]) == 520)
                {
                    _finamSecurities[i].Market = "Криптовалюты";
                }
            }

            _securities = new List<Security>();

            for (int i = 0; i < _finamSecurities.Count; i++)
            {
                if (_finamSecurities[i].Name == "")
                {
                    continue;
                }

                Security sec = new Security();
                sec.NameFull = _finamSecurities[i].Code;
                sec.Name = _finamSecurities[i].Name;
                sec.NameId = _finamSecurities[i].Id;
                sec.NameClass = _finamSecurities[i].Market;
                sec.PriceStep = 1;
                sec.PriceStepCost = 1;

                _securities.Add(sec);
            }

            _securitiesToSend.Enqueue(_securities);

            SendLogMessage(OsLocalization.Market.Message52 + _securities.Count, LogMessageType.System);
        }

        private void CreatePortfolio()
        {
            if (Portfolios != null && Portfolios.Count != 0)
            {
                return;
            }
            _portfolios = new List<Portfolio>();
            Portfolio fakePortfolio = new Portfolio();
            fakePortfolio.Number = "FakeFinamPortfolio";
            fakePortfolio.ValueBegin = 1000000;
            _portfolios.Add(fakePortfolio);

            SendLogMessage(OsLocalization.Market.Message53 + fakePortfolio.Number, LogMessageType.System);

            _portfolioToSend.Enqueue(Portfolios);
        }

        // work of sending thread
        // работа потока рассылки !!!!!

        /// <summary>
        /// queue of ticks
        /// очередь тиков
        /// </summary>
        private ConcurrentQueue<List<Trade>> _tradesToSend;

        /// <summary>
        /// queue of new instruments
        /// очередь новых инструментов
        /// </summary>
        private ConcurrentQueue<List<Security>> _securitiesToSend;

        /// <summary>
        /// queue of upsated candle series
        /// очередь обновлённых серий свечек
        /// </summary>
        private ConcurrentQueue<CandleSeries> _candleSeriesToSend;

        /// <summary>
        /// queue of new depths
        /// очередь новых стаканов
        /// </summary>
        private ConcurrentQueue<MarketDepth> _marketDepthsToSend;

        /// <summary>
        /// queue of new portfolios
        /// очередь новых портфелей
        /// </summary>
        private ConcurrentQueue<List<Portfolio>> _portfolioToSend;

        /// <summary>
        /// queue of updated bid/ask on instruments
        /// очередь обновлений бида с аска по инструментам 
        /// </summary>
        private ConcurrentQueue<BidAskSender> _bidAskToSend;

        /// <summary>
        /// place where the connection is controlled
        /// место в котором контролируется соединение
        /// </summary>
        private void SenderThreadArea()
        {
            while (true)
            {
                try
                {
                    if (_tradesToSend != null && _tradesToSend.Count != 0)
                    {
                        List<Trade> trades;

                        if (_tradesToSend.TryDequeue(out trades))
                        {
                            if (NewTradeEvent != null)
                            {
                                NewTradeEvent(trades);
                            }
                        }
                    }
                    else if (_securitiesToSend != null && _securitiesToSend.Count != 0)
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
                    else if (_candleSeriesToSend != null && _candleSeriesToSend.Count != 0)
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
                    else if (_marketDepthsToSend != null && _marketDepthsToSend.Count != 0)
                    {
                        MarketDepth depth;

                        if (_marketDepthsToSend.TryDequeue(out depth))
                        {
                            if (NewMarketDepthEvent != null)
                            {
                                NewMarketDepthEvent(depth);
                            }
                        }
                    }
                    else if (_portfolioToSend != null && _portfolioToSend.Count != 0)
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
                    else if (_bidAskToSend != null && _bidAskToSend.Count != 0)
                    {
                        BidAskSender bidAsk;

                        if (_bidAskToSend.TryDequeue(out bidAsk))
                        {
                            if (NewBidAscIncomeEvent != null)
                            {
                                NewBidAscIncomeEvent(bidAsk.Bid, bidAsk.Ask, bidAsk.Security);
                            }
                        }
                    }
                    else
                    {
                        Thread.Sleep(1);
                    }
                }
                catch (Exception error)
                {
                    SendLogMessage(error.ToString(), LogMessageType.Error);
                }
            }
        }

        // security / бумаги
        private List<FinamSecurity> _finamSecurities;

        private List<Security> _securities;

        /// <summary>
        /// all instruments in the system
        /// все инструменты в системе
        /// </summary>
        public List<Security> Securities
        {
            get { return _securities; }
        }

        /// <summary>
        /// take the instrument as a Security class by the name of the tool
        /// взять инструмент в виде класса Security, по имени инструмента 
        /// </summary>
        public Security GetSecurityForName(string name)
        {
            if (_securities == null)
            {
                return null;
            }
            return _securities.Find(securiti => securiti.Name == name || securiti.NameClass == name);
        }

        /// <summary>
        /// called when new tools appear
        /// вызывается при появлении новых инструментов
        /// </summary>
        public event Action<List<Security>> SecuritiesChangeEvent;

        /// <summary>
        /// show instruments
        /// показать инструменты 
        /// </summary>
        public void ShowSecuritiesDialog()
        {
            SecuritiesUi ui = new SecuritiesUi(this);
            ui.ShowDialog();
        }

        // portfolios. It is sent for trading in the emulator on Finam server
        // портфели. Рассылается для торговли в эмуляторе на сервере финам

        private List<Portfolio> _portfolios;

        /// <summary>
        /// all accounts in the system
        /// все счета в системе
        /// </summary>
        public List<Portfolio> Portfolios
        {
            get { return _portfolios; }
        }

        /// <summary>
        /// take portfolio by his number/name
        /// взять портфель по его номеру/имени
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
        /// called when new portfolios appear
        /// вызывается когда в системе появляются новые портфели
        /// </summary>
        public event Action<List<Portfolio>> PortfoliosChangeEvent;

        // Subscribe to data
        // Подпись на данные

        private List<FinamDataSeries> _finamDataSeries;

        /// <summary>
        /// multi-threaded access locker in StartThisSecurity
        /// объект блокирующий многопоточный доступ в StartThisSecurity
        /// </summary>
        private object _lockerStarter = new object();

        /// <summary>
        /// start downloading data on instrument
        /// Начать выгрузку данных по инструменту. 
        /// </summary>
        /// <param name="namePaper">security name for running / имя бумаги которую будем запускать</param>
        /// <param name="timeFrameBuilder">object with timeframe / объект несущий в себе таймфрейм</param>
        /// <returns>In case of luck, returns CandleSeries / В случае удачи возвращает CandleSeries
        /// in case of failure null / в случае неудачи null</returns>
        public CandleSeries StartThisSecurity(string namePaper, TimeFrameBuilder timeFrameBuilder)
        {
            return null;
        }

        void series_СandleFinishedEvent(CandleSeries series)
        {
            _candleSeriesToSend.Enqueue(series);

            List<Candle> candles = series.CandlesAll;

            if (candles == null || candles.Count == 0)
            {
                return;
            }

            MarketDepth depth = new MarketDepth();
            depth.SecurityNameCode = series.Security.Name;
            MarketDepthLevel ask = new MarketDepthLevel();
            ask.Bid = 10;
            ask.Price = candles[candles.Count - 1].Close;

            depth.Bids = new List<MarketDepthLevel>();
            depth.Bids.Add(ask);


            MarketDepthLevel bid = new MarketDepthLevel();
            bid.Ask = 10;
            bid.Price = candles[candles.Count - 1].Close;

            depth.Asks = new List<MarketDepthLevel>();
            depth.Asks.Add(bid);

            _marketDepthsToSend.Enqueue(depth);

            Trade newtTrade = new Trade();
            newtTrade.Id = "0";
            newtTrade.Price = candles[candles.Count - 1].Close;
            newtTrade.Time = candles[candles.Count - 1].TimeStart.Add(series.TimeFrameSpan);
            newtTrade.SecurityNameCode = series.Security.NameFull;

            List<Trade> tradeList = new List<Trade>();
            tradeList.Add(newtTrade);

            TradesUpdateEvent(tradeList);

            ServerTime = newtTrade.Time;

            BidAskSender bidAskSender = new BidAskSender();
            bidAskSender.Ask = ask.Price;
            bidAskSender.Bid = bid.Price;
            bidAskSender.Security = series.Security;

            _bidAskToSend.Enqueue(bidAskSender);
        }

        /// <summary>
        /// Start uploading data on the instrument
        /// Начать выгрузку данных по инструменту. 
        /// </summary>
        /// <param name="namePaper">security id/айди бумаги</param>
        /// <param name="timeFrameBuilder">object with timeframe / объект несущий в себе данные по таймФреймам</param>
        /// <param name="startTime">start downloading time / время начала загрузки</param>
        /// <param name="endTime">finish downloading time /время завершения работы</param>
        /// <param name="actualTime">time of the last data load / время последней загрузки данных</param>
        /// <param name="neadToUpdate">whether to automatically update / нужно ли автоматически обновлять</param>
        /// <returns>In case of luck, returns CandleSeries / В случае удачи возвращает CandleSeries
        /// in case of failure null / в случае неудачи null</returns>
        public CandleSeries GetCandleDataToSecurity(string namePaper, TimeFrameBuilder timeFrameBuilder, DateTime startTime, DateTime endTime, DateTime actualTime, bool neadToUpdate)
        {
            try
            {
                if (LastStartServerTime.AddSeconds(5) > DateTime.Now)
                {
                    return null;
                }

                // one by one / дальше по одному
                lock (_lockerStarter)
                {
                    if (namePaper == null)
                    {
                        return null;
                    }
                    // need to start the server if it is still disabled / надо запустить сервер если он ещё отключен
                    if (ServerStatus != ServerConnectStatus.Connect)
                    {
                        //MessageBox.Show("Сервер не запущен. Скачивание данных прервано. Инструмент: " + namePaper);
                        return null;
                    }

                    if (_securities == null)
                    {
                        Thread.Sleep(5000);
                        return null;
                    }
                    if (LastStartServerTime != DateTime.MinValue &&
                        LastStartServerTime.AddSeconds(15) > DateTime.Now)
                    {
                        return null;
                    }

                    Security security = null;

                    for (int i = 0; _securities != null && i < _securities.Count; i++)
                    {
                        if (_securities[i].NameId == namePaper)
                        {
                            security = _securities[i];
                            break;
                        }
                    }

                    if (security == null)
                    {
                        return null;
                    }

                    _candles = null;

                    CandleSeries series = new CandleSeries(timeFrameBuilder, security, StartProgram.IsOsData)
                    {
                        CandlesAll = _candles,
                        IsStarted = true
                    };

                    FinamDataSeries finamDataSeries = new FinamDataSeries();

                    finamDataSeries.ServerPrefics = "http://" + ServerAdress;
                    finamDataSeries.TimeActual = actualTime;
                    finamDataSeries.Security = security;
                    finamDataSeries.SecurityFinam = _finamSecurities.Find(s => s.Id == security.NameId);
                    finamDataSeries.Series = series;
                    finamDataSeries.TimeEnd = endTime;
                    finamDataSeries.TimeStart = startTime;
                    finamDataSeries.TimeFrame = timeFrameBuilder.TimeFrame;
                    finamDataSeries.LogMessageEvent += SendLogMessage;
                    finamDataSeries.NeadToUpdeate = neadToUpdate;


                    if (_finamDataSeries == null)
                    {
                        _finamDataSeries = new List<FinamDataSeries>();
                    }

                    _finamDataSeries.Add(finamDataSeries);

                    Thread.Sleep(2000);

                    SendLogMessage(OsLocalization.Market.Label7 + series.Security.Name +
                                   OsLocalization.Market.Label10 + series.TimeFrame +
                                   OsLocalization.Market.Message16,
                        LogMessageType.System);

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
        /// take ticks data on instrument for period
        /// взять тиковые данные по инструменту за определённый период
        /// </summary>
        /// <returns></returns>
        public bool GetTickDataToSecurity(string namePaper, DateTime startTime, DateTime endTime, DateTime actualTime, bool neadToUpdete)
        {
            try
            {
                if (LastStartServerTime.AddSeconds(5) > DateTime.Now)
                {
                    return false;
                }

                // one by one / дальше по одному
                lock (_lockerStarter)
                {
                    if (namePaper == null)
                    {
                        return false;
                    }
                    // need to start the server if it is still disabled / надо запустить сервер если он ещё отключен
                    if (ServerStatus != ServerConnectStatus.Connect)
                    {
                        //MessageBox.Show("Сервер не запущен. Скачивание данных прервано. Инструмент: " + namePaper);
                        return false;
                    }

                    if (_securities == null)
                    {
                        Thread.Sleep(5000);
                        return false;
                    }
                    if (LastStartServerTime != DateTime.MinValue &&
                        LastStartServerTime.AddSeconds(15) > DateTime.Now)
                    {
                        return false;
                    }

                    Security security = null;


                    for (int i = 0; _securities != null && i < _securities.Count; i++)
                    {
                        if (_securities[i].NameId == namePaper)
                        {
                            security = _securities[i];
                            break;
                        }
                    }

                    if (security == null)
                    {
                        return false;
                    }


                    FinamDataSeries finamDataSeries = new FinamDataSeries();

                    finamDataSeries.ServerPrefics = "http://" + ServerAdress;
                    finamDataSeries.TimeActual = actualTime;
                    finamDataSeries.Security = security;
                    finamDataSeries.SecurityFinam = _finamSecurities.Find(s => s.Id == security.NameId);
                    finamDataSeries.TimeEnd = endTime;
                    finamDataSeries.TimeStart = startTime;
                    finamDataSeries.IsTick = true;
                    finamDataSeries.TradesUpdateEvent += finamDataSecies_TradesFilesUpdateEvent;
                    finamDataSeries.NeadToUpdeate = neadToUpdete;
                    finamDataSeries.LogMessageEvent += SendLogMessage;

                    if (_finamDataSeries == null)
                    {
                        _finamDataSeries = new List<FinamDataSeries>();
                    }

                    _finamDataSeries.Add(finamDataSeries);

                    Thread.Sleep(2000);

                    SendLogMessage(OsLocalization.Market.Label7 + security.Name +
                                   OsLocalization.Market.Label10 + " Tick" +
                                   OsLocalization.Market.Message16,
                        LogMessageType.System);

                    return true;
                }
            }
            catch (Exception error)
            {
                SendLogMessage(error.ToString(), LogMessageType.Error);
                return false;
            }
        }

        /// <summary>
        /// start depth downloading on instrument 
        /// запустить скачивание стакана по инструменту
        /// </summary>
        /// <returns></returns>
        public bool StartMarketDepthDataToSecurity(string namePaper)
        {
            return true;
        }

        /// <summary>
        /// stop downloading on instrument
        /// остановить скачивание инструмента
        /// </summary>
        public void StopThisSecurity(CandleSeries series)
        {
            if (series == null)
            {
                return;
            }

            for (int i = 0; i < _finamDataSeries.Count; i++)
            {
                if (_finamDataSeries[i].Series == series)
                {
                    _finamDataSeries.Remove(_finamDataSeries[i]);
                    break;
                }
            }
        }

        // depth. Sent for trading in the emulator on Finam server
        // стакан. Рассылается для торговли в эмуляторе на сервере Финам

        /// <summary>
        /// called when bid or ask changes
        /// вызывается когда изменяется бид или аск по инструменту
        /// </summary>
        public event Action<decimal, decimal, Security> NewBidAscIncomeEvent;

        /// <summary>
        /// called when depth changes
        /// вызывается когда изменяется стакан
        /// </summary>
        public event Action<MarketDepth> NewMarketDepthEvent;

        // candle downloading / выгрузка свечей

        private void ThreadDownLoaderArea()
        {
            while (true)
            {
                Thread.Sleep(5000);

                if (LastStartServerTime.AddSeconds(15) > DateTime.Now)
                {
                    continue;
                }

                for (int i = 0; _finamDataSeries != null && i < _finamDataSeries.Count; i++)
                {
                    if (_finamDataSeries[i].NeadToUpdeate == false &&
                        _finamDataSeries[i].LoadedOnce == true)
                    {
                        continue;
                    }
                    _finamDataSeries[i].Process();
                    Thread.Sleep(10000);
                }
            }
        }

        /// <summary>
        /// called at the time of changing candle series
        /// вызывается в момент изменения серий свечек
        /// </summary>
        public event Action<CandleSeries> NewCandleIncomeEvent;

        /// <summary>
        /// connectors connected to server need to reload data
        /// коннекторам подключеным к серверу необходимо перезаказать данные
        /// </summary>
        public event Action NeadToReconnectEvent;

        /// <summary>
        /// candles downloading from method GetSmartComCandleHistory
        /// свечи скаченные из метода GetSmartComCandleHistory
        /// </summary>
        private List<Candle> _candles;

        // ticks / тики

        /// <summary>
        /// file names downloaded from Finam with trades
        /// имена файлов загруженных из финам с трейдами
        /// </summary>
        private List<List<string>> _downLoadFilesWhithTrades;

        private void finamDataSecies_TradesFilesUpdateEvent(List<string> files)
        {
            if (_downLoadFilesWhithTrades == null)
            {
                _downLoadFilesWhithTrades = new List<List<string>>();
            }

            for (int i = 0; i < files.Count; i++)
            {
                if (files[i] == null)
                {
                    continue;
                }
                string name = files[i].Split('_')[0];

                List<string> myList = _downLoadFilesWhithTrades.Find(f => f[0].Split('_')[0] == name);

                if (myList == null)
                {
                    myList = new List<string>();
                    _downLoadFilesWhithTrades.Add(myList);
                }
                myList.Add(files[i]);
            }
        }

        private void TradesUpdateEvent(List<Trade> tradesNew)
        {
            if (_allTrades == null)
            {
                _allTrades = new List<Trade>[1];
                _allTrades[0] = new List<Trade>(tradesNew);
            }
            else
            {
                // sort trades by storages / сортируем сделки по хранилищам
                for (int indTrade = 0; indTrade < tradesNew.Count; indTrade++)
                {
                    Trade trade = tradesNew[indTrade];
                    bool isSave = false;
                    for (int i = 0; i < _allTrades.Length; i++)
                    {
                        if (_allTrades[i] != null && _allTrades[i].Count != 0 &&
                            _allTrades[i][0].SecurityNameCode == trade.SecurityNameCode)
                        {
                            // if there is already a storage for this instrument, we save and everything / если для этого инструметна уже есть хранилище, сохраняем и всё
                            isSave = true;
                            if (_allTrades[i][_allTrades[i].Count - 1].Time > trade.Time)
                            {
                                break;
                            }
                            _allTrades[i].Add(trade);
                            break;
                        }
                    }
                    if (isSave == false)
                    {
                        // there is no storage for the instrument / хранилища для инструмента нет
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
            }

            foreach (var trades in _allTrades)
            {
                if (tradesNew[0].SecurityNameCode == trades[0].SecurityNameCode)
                {
                    _tradesToSend.Enqueue(trades);
                    break;
                }
            }

        }

        /// <summary>
        /// all ticks
        /// все тики
        /// </summary>
        private List<Trade>[] _allTrades;

        /// <summary>
        /// all server ticks
        /// все тики имеющиеся у сервера
        /// </summary>
        public List<Trade>[] AllTrades
        {
            get { return _allTrades; }
        }

        /// <summary>
        /// take ticks by instruments
        /// взять тики по инструменту
        /// </summary>
        public List<Trade> GetAllTradesToSecurity(Security security)
        {
            if (_allTrades != null)
            {
                foreach (var tradesList in _allTrades)
                {
                    if (tradesList.Count > 1 &&
                        tradesList[0] != null &&
                        tradesList[0].SecurityNameCode == security.NameFull)
                    {
                        return tradesList;
                    }
                }
            }

            return new List<Trade>();
        }

        public List<string> GetAllFilesWhithTradeToSecurity(string security)
        {
            if (_downLoadFilesWhithTrades == null)
            {
                _downLoadFilesWhithTrades = new List<List<string>>();
            }

            return _downLoadFilesWhithTrades.Find(s => s[0].Split('_')[0] == @"Data\Temp\" + security);
        }

        /// <summary>
        /// called at the time of appearance of new trades on instrument
        /// вызывается в момет появления новых трейдов по инструменту
        /// </summary>
        public event Action<List<Trade>> NewTradeEvent;

        // log messages
        // обработка лога

        /// <summary>
        /// add a new log message
        /// добавить в лог новое сообщение
        /// </summary>
        private void SendLogMessage(string message, LogMessageType type)
        {
            if (LogMessageEvent != null)
            {
                LogMessageEvent(message, type);
            }
        }

        /// <summary>
        /// log manager
        /// менеджер лога
        /// </summary>
        private Log _logMaster;

        /// <summary>
        /// outgoing message for log
        /// исходящее сообщение для лога
        /// </summary>
        public event Action<string, LogMessageType> LogMessageEvent;


        #region the rest of the server interface is not implemented, because Finam isn't a full server /остальное из интерфейса сервера не реализовано, т.к. Финам не полный сервер

        // my trades / мои сделки

        /// <summary>
        /// my trades
        /// мои сделки
        /// </summary>
        public List<MyTrade> MyTrades
        {
            get { return null; }
        }

        /// <summary>
        /// called when my new deal comes
        /// вызывается когда приходит новая моя сделка
        /// </summary>
        public event Action<MyTrade> NewMyTradeEvent;

        // work with orders
        // работа с ордерами

        /// <summary>
        /// execute order
        /// исполнить ордер
        /// </summary>
        public void ExecuteOrder(Order order)
        {

        }

        /// <summary>
        /// cancel order
        /// отменить ордер
        /// </summary>
        public void CancelOrder(Order order)
        {

        }

        /// <summary>
        /// called when new order appear in the system
        /// вызывается когда в системе появляется новый ордер
        /// </summary>
        public event Action<Order> NewOrderIncomeEvent;

        private DateTime _serverTime;

        /// <summary>
        /// server time
        /// время сервера
        /// </summary>
        public DateTime ServerTime
        {
            get { return _serverTime; }
            set
            {
                if (_serverTime > value)
                {
                    return;
                }
                _serverTime = value;

            }
        }

        /// <summary>
        /// called when server time is changed
        /// вызывается когда изменяется время сервера
        /// </summary>
        public event Action<DateTime> TimeServerChangeEvent;

        #endregion

    }

    /// <summary>
    /// data series for the load history at Finam
    /// серия данных для подгрузки истории в финам
    /// </summary>
    public class FinamDataSeries
    {

        /// <summary>
        /// prefix for the server address
        /// префикс для адреса сервера
        /// </summary>
        public string ServerPrefics;

        /// <summary>
        /// security in the Finam specification
        /// контракт в финам спецификации
        /// </summary>
        public FinamSecurity SecurityFinam;

        /// <summary>
        /// security in Os.Engine format
        /// контракт в формате Os.Engine
        /// </summary>
        public Security Security;

        /// <summary>
        /// timeframe
        /// таймФрейм
        /// </summary>
        public TimeFrame TimeFrame
        {
            get { return _timeFrame; }
            set
            {
                _timeFrame = value;

                if (_timeFrame == TimeFrame.Day)
                {
                    _timeFrameFinam = 8.ToString();
                    _timeFrameSpan = new TimeSpan(24, 0, 0, 0);
                }
                else if (_timeFrame == TimeFrame.Hour1)
                {
                    _timeFrameFinam = 7.ToString();
                    _timeFrameSpan = new TimeSpan(0, 1, 0, 0);
                }
                else if (_timeFrame == TimeFrame.Min30)
                {
                    _timeFrameFinam = 6.ToString();
                    _timeFrameSpan = new TimeSpan(0, 0, 30, 0);
                }
                else if (_timeFrame == TimeFrame.Min15)
                {
                    _timeFrameFinam = 5.ToString();
                    _timeFrameSpan = new TimeSpan(0, 0, 15, 0);
                }
                else if (_timeFrame == TimeFrame.Min10)
                {
                    _timeFrameFinam = 4.ToString();
                    _timeFrameSpan = new TimeSpan(0, 0, 10, 0);
                }
                else if (_timeFrame == TimeFrame.Min5)
                {
                    _timeFrameFinam = 3.ToString();
                    _timeFrameSpan = new TimeSpan(0, 0, 5, 0);
                }
                else if (_timeFrame == TimeFrame.Min1)
                {
                    _timeFrameFinam = 2.ToString();
                    _timeFrameSpan = new TimeSpan(0, 0, 1, 0);
                }
            }
        }
        private TimeFrame _timeFrame;

        /// <summary>
        /// timeframe in Finam format
        /// таймфрейм в формате финам
        /// </summary>
        private string _timeFrameFinam;

        /// <summary>
        /// timeframe in TimeSpan format
        /// таймфрейм в формате TimeSpan
        /// </summary>
        private TimeSpan _timeFrameSpan;

        /// <summary>
        /// candle series
        /// серия свечек
        /// </summary>
        public CandleSeries Series;

        /// <summary>
        /// start time of the download
        /// время начала скачивания
        /// </summary>
        public DateTime TimeStart;

        /// <summary>
        /// finish time of the download
        /// время завершения скачивания
        /// </summary>
        public DateTime TimeEnd;

        /// <summary>
        /// current time
        /// актуальное время
        /// </summary>
        public DateTime TimeActual;

        /// <summary>
        /// candle updated
        /// обновились свечи
        /// </summary>
        public event Action<CandleSeries> CandleUpdateEvent;

        /// <summary>
        /// trades updated
        /// обновились трейды
        /// </summary>
        public event Action<List<string>> TradesUpdateEvent;

        /// <summary>
        /// is current object a downloading tick
        /// является ли текущий объект скачивающим тики
        /// </summary>
        public bool IsTick;

        /// <summary>
        /// update data
        /// обновить данные
        /// </summary>
        public void Process()
        {
            try
            {
                if (NeadToUpdeate == false &&
                    LoadedOnce)
                {
                    return;
                }

                if (IsTick == false)
                {
                    if (Series == null)
                    {
                        return;
                    }

                    Series.IsStarted = true;
                }

                LoadedOnce = true;

                SendLogMessage(SecurityFinam.Name + OsLocalization.Market.Message54 + _timeFrame, LogMessageType.System);

                if (IsTick == false)
                {
                    if (TimeFrame == TimeFrame.Sec1 ||
                        TimeFrame == TimeFrame.Sec10 ||
                        TimeFrame == TimeFrame.Sec15 ||
                        TimeFrame == TimeFrame.Sec2 ||
                        TimeFrame == TimeFrame.Sec20 ||
                        TimeFrame == TimeFrame.Sec30 ||
                        TimeFrame == TimeFrame.Sec5)
                    {
                        List<string> trades = GetTrades();

                        List<Trade> listTrades = new List<Trade>();
                        Trade newTrade = new Trade();

                        for (int i = 0; trades != null && i < trades.Count; i++)
                        {
                            if (trades[i] == null)
                            {
                                continue;
                            }
                            StreamReader reader = new StreamReader(trades[i]);

                            while (!reader.EndOfStream)
                            {
                                try
                                {
                                    newTrade.SetTradeFromString(reader.ReadLine());

                                    if (newTrade.Time.Hour < 10)
                                    {
                                        continue;
                                    }
                                    listTrades.Add(newTrade);
                                    Series.SetNewTicks(listTrades);
                                    TimeActual = newTrade.Time;
                                }
                                catch
                                {
                                    // ignore
                                }

                            }
                            reader.Close();
                        }
                        listTrades.Clear();
                    }
                    else
                    {
                        List<Candle> candles = GetCandles();

                        for (int i = 0; candles != null && i < candles.Count; i++)
                        {
                            Series.SetNewCandleInArray(candles[i]);
                        }
                    }

                    if (CandleUpdateEvent != null)
                    {
                        CandleUpdateEvent(Series);
                    }
                }
                else //if (IsTick == true)
                {
                    List<string> trades = GetTrades();

                    if (TradesUpdateEvent != null)
                    {
                        TradesUpdateEvent(trades);
                    }
                }
            }
            catch (Exception error)
            {
                SendLogMessage(error.ToString(), LogMessageType.Error);
            }
            SendLogMessage(SecurityFinam.Name + OsLocalization.Market.Message55 + _timeFrame, LogMessageType.System);
        }

        /// <summary>
        /// whether need to update the series automatically
        /// нужно ли обновлять серию автоматически
        /// </summary>
        public bool NeadToUpdeate;

        /// <summary>
        /// set had once loaded
        /// сет уже один раз подгружался
        /// </summary>
        public bool LoadedOnce;

        /// <summary>
        /// update trades
        /// обновить трейды
        /// </summary>
        /// <returns></returns>
        private List<string> GetTrades()
        {
            DateTime timeStart = TimeStart;

            DateTime timeEnd = TimeEnd;

            if (timeEnd.Date > DateTime.Now.Date)
            {
                timeEnd = DateTime.Now;
            }

            if (TimeActual != DateTime.MinValue)
            {
                timeStart = TimeActual;
            }

            List<string> trades = new List<string>();

            while (timeStart.Date != timeEnd.Date)
            {
                string tradesOneDay = GetTrades(timeStart.Date, timeStart.Date);
                timeStart = timeStart.AddDays(1);

                if (tradesOneDay != null)
                {
                    trades.Add(tradesOneDay);
                }
                GC.Collect();
                GC.WaitForPendingFinalizers();
            }

            string tradesToday = GetTrades(timeStart.Date, timeStart.Date);

            if (tradesToday != null)
            {
                trades.Add(tradesToday);
            }

            return trades;
        }

        /// <summary>
        /// take trade for period
        /// взять трейды за период
        /// </summary>
        /// <param name="timeStart"></param>
        /// <param name="timeEnd"></param>
        /// <returns></returns>
        private string GetTrades(DateTime timeStart, DateTime timeEnd)
        {
            SendLogMessage(OsLocalization.Market.Message56 + SecurityFinam.Name +
                           OsLocalization.Market.Message57 + timeStart.Date, LogMessageType.System);
            //http://195.128.78.52/GBPUSD_141201_141206.csv?market=5&em=86&code=GBPUSD&df=1&mf=11&yf=2014&from=01.12.2014&dt=6&mt=11&yt=2014&to=06.12.2014&
            //p=2&f=GBPUSD_141201_141206&e=.csv&cn=GBPUSD&dtf=1&tmf=3&MSOR=1&mstime=on&mstimever=1&sep=3&sep2=1&datf=5&at=1

            string monthStart = "";
            string dayStart = "";

            if (timeStart.Month.ToString().Length == 1)
            {
                monthStart += "0" + timeStart.Month;
            }
            else
            {
                monthStart += timeStart.Month;
            }

            if (TimeStart.Day.ToString().Length == 1)
            {
                dayStart += "0" + TimeStart.Day;
            }
            else
            {
                dayStart += TimeStart.Day;
            }


            string timeStartInStrToName =
                timeStart.Year.ToString()[2].ToString()
                + timeStart.Year.ToString()[3].ToString()
                + monthStart + dayStart;

            string monthEnd = "";
            string dayEnd = "";

            if (timeEnd.Month.ToString().Length == 1)
            {
                monthEnd += "0" + timeEnd.Month;
            }
            else
            {
                monthEnd += timeEnd.Month;
            }

            if (timeEnd.Day.ToString().Length == 1)
            {
                dayEnd += "0" + timeEnd.Day;
            }
            else
            {
                dayEnd += timeEnd.Day;
            }

            string timeEndInStrToName = timeEnd.Year.ToString()[2].ToString()
                                        + timeEnd.Year.ToString()[3].ToString()
                                        + monthEnd
                                        + dayEnd;

            string timeFrom = timeStart.ToShortDateString();
            string timeTo = timeEnd.ToShortDateString();

            string urlToSec = SecurityFinam.Name + "_" + timeStartInStrToName + "_" + timeEndInStrToName;

            string url = ServerPrefics + "/" + urlToSec + ".txt?";

            url += "market=" + SecurityFinam.MarketId + "&";
            url += "em=" + SecurityFinam.Id + "&";
            url += "code=" + SecurityFinam.Code + "&";
            url += "df=" + (timeStart.Day) + "&";
            url += "mf=" + (timeStart.Month - 1) + "&";
            url += "yf=" + (timeStart.Year) + "&";
            url += "from=" + timeFrom + "&";

            url += "dt=" + (timeEnd.Day) + "&";
            url += "mt=" + (timeEnd.Month - 1) + "&";
            url += "yt=" + (timeEnd.Year) + "&";
            url += "to=" + timeTo + "&";

            url += "p=" + 1 + "&";
            url += "f=" + urlToSec + "&";
            url += "e=" + ".txt" + "&";
            url += "cn=" + SecurityFinam.Name + "&";
            url += "dtf=" + 1 + "&";
            url += "tmf=" + 1 + "&";
            url += "MSOR=" + 1 + "&";
            url += "mstime=" + "on" + "&";
            url += "mstimever=" + "1" + "&";
            url += "sep=" + "1" + "&";
            url += "sep2=" + "1" + "&";
            url += "datf=" + "12" + "&";
            url += "at=" + "0";

            // if we have already downloaded this trades series, try to get it from the general storage
            // если мы уже эту серию трейдов качали, пробуем достать её из общего хранилища

            string secName = SecurityFinam.Name;

            if (secName.Contains("/"))
            {
                secName = Extensions.RemoveExcessFromSecurityName(secName);
            }

            string fileName = @"Data\Temp\" + secName + "_" + timeStart.ToShortDateString() + ".txt";

            if (timeStart.Date != DateTime.Now.Date &&
                File.Exists(fileName))
            {
                return fileName;
            }

            // request data
            // запрашиваем данные

            WebClient wb = new WebClient();

            try
            {
                _tickLoaded = false;
                wb.DownloadFileAsync(new Uri(url, UriKind.Absolute), fileName);
                wb.DownloadFileCompleted += wb_DownloadFileCompleted;
            }
            catch (Exception)
            {
                wb.Dispose();
                return null;
            }

            while (true)
            {
                Thread.Sleep(1000);
                if (_tickLoaded)
                {
                    break;
                }
            }
            wb.Dispose();

            if (!File.Exists(fileName))
            { // file is not uploaded / файл не загружен
                return null;
            }

            StringBuilder list = new StringBuilder();

            StreamReader reader = new StreamReader(fileName);

            while (!reader.EndOfStream)
            {
                string[] s = reader.ReadLine().Split(',');

                StringBuilder builder = new StringBuilder();

                builder.Append(s[0] + ",");
                builder.Append(s[1] + ",");
                builder.Append(s[2] + ",");
                builder.Append(s[3] + ",");

                if (s[5] == "S")
                {
                    builder.Append("Sell");
                }
                else
                {
                    builder.Append("Buy");
                }

                list.Append(builder + "\r\n");
            }

            reader.Close();

            StreamWriter writer = new StreamWriter(fileName);
            writer.Write(list);
            writer.Close();

            return fileName;
        }

        void wb_DownloadFileCompleted(object sender, System.ComponentModel.AsyncCompletedEventArgs e)
        {
            _tickLoaded = true;
        }

        private bool _tickLoaded;

        /// <summary>
        /// update candles
        /// обновить свечи
        /// </summary>
        /// <returns></returns>
        private List<Candle> GetCandles()
        {

            DateTime timeStart = TimeStart;

            DateTime timeEnd = TimeEnd;

            if (timeEnd.Date > DateTime.Now.Date)
            {
                timeEnd = DateTime.Now;
            }

            if (TimeActual != DateTime.MinValue)
            {
                timeStart = TimeActual;
            }

            List<Candle> candles = new List<Candle>();

            while (timeStart.Year != timeEnd.Year)
            {
                List<Candle> candlesOneDay = GetCandles(timeStart.Date, timeStart.AddMonths(12 - timeStart.Month).AddDays(31 - timeStart.Day).Date);

                timeStart = timeStart.AddMonths(12 - timeStart.Month).AddDays(32 - timeStart.Day).Date;

                if (candlesOneDay != null)
                {
                    candles.AddRange(candlesOneDay);
                }
                Thread.Sleep(5000);
            }
            List<Candle> candlesToday = GetCandles(timeStart, timeEnd);

            if (candlesToday != null)
            {
                candles.AddRange(candlesToday);
            }

            if (candles.Count != 0)
            {
                TimeActual = candles[candles.Count - 1].TimeStart;
            }

            return candles;
        }

        /// <summary>
        /// take candles for period
        /// взять свечи за период
        /// </summary>
        /// <param name="timeStart"></param>
        /// <param name="timeEnd"></param>
        /// <returns></returns>
        private List<Candle> GetCandles(DateTime timeStart, DateTime timeEnd)
        {
            SendLogMessage(OsLocalization.Market.Message58 + SecurityFinam.Name +
                           OsLocalization.Market.Label10 + TimeFrame +
                           OsLocalization.Market.Label26 + timeStart.Date +
                           OsLocalization.Market.Label27 + timeEnd.Date, LogMessageType.System);
            //http://195.128.78.52/GBPUSD_141201_141206.csv?market=5&em=86&code=GBPUSD&df=1&mf=11&yf=2014&from=01.12.2014&dt=6&mt=11&yt=2014&to=06.12.2014&
            //p=2&f=GBPUSD_141201_141206&e=.csv&cn=GBPUSD&dtf=1&tmf=3&MSOR=1&mstime=on&mstimever=1&sep=3&sep2=1&datf=5&at=1

            if (string.IsNullOrEmpty(_timeFrameFinam))
            {
                return null;
            }

            string timeStartInStrToName = timeStart.Year.ToString()[2].ToString() + timeStart.Year.ToString()[3].ToString() + timeStart.Month + TimeStart.Day;
            string timeEndInStrToName = timeEnd.Year.ToString()[2].ToString() + timeEnd.Year.ToString()[3].ToString() + timeEnd.Month + timeEnd.Day;

            string timeFrom = timeStart.ToShortDateString();
            string timeTo = timeEnd.ToShortDateString();

            string fileName = SecurityFinam.Name + "_" + timeStartInStrToName + "_" + timeEndInStrToName;

            string url = ServerPrefics + "/" + fileName + ".txt?";

            url += "market=" + SecurityFinam.MarketId + "&";
            url += "em=" + SecurityFinam.Id + "&";
            url += "code=" + SecurityFinam.Code + "&";
            url += "df=" + (timeStart.Day) + "&";
            url += "mf=" + (timeStart.Month - 1) + "&";
            url += "yf=" + (timeStart.Year) + "&";
            url += "from=" + timeFrom + "&";

            url += "dt=" + (timeEnd.Day) + "&";
            url += "mt=" + (timeEnd.Month - 1) + "&";
            url += "yt=" + (timeEnd.Year) + "&";
            url += "to=" + timeTo + "&";

            url += "p=" + _timeFrameFinam + "&";
            url += "f=" + fileName + "&";
            url += "e=" + ".txt" + "&";
            url += "cn=" + SecurityFinam.Name + "&";
            url += "dtf=" + 1 + "&";
            url += "tmf=" + 1 + "&";
            url += "MSOR=" + 1 + "&";
            url += "mstime=" + "on" + "&";
            url += "mstimever=" + "1" + "&";
            url += "sep=" + "1" + "&";
            url += "sep2=" + "1" + "&";
            url += "datf=" + "5" + "&";
            url += "at=" + "0";

            WebClient wb = new WebClient();

            string response = wb.DownloadString(url);

            if (response != "")
            {
                List<Candle> candles = new List<Candle>();

                response = response.Replace("\r\n", "&");

                string[] tradesInStr = response.Split('&');

                if (tradesInStr.Length == 1)
                {
                    return null;
                }

                for (int i = 0; i < tradesInStr.Length; i++)
                {
                    if (tradesInStr[i] == "")
                    {
                        continue;
                    }
                    candles.Add(new Candle());
                    candles[candles.Count - 1].SetCandleFromString(tradesInStr[i]);
                    candles[candles.Count - 1].TimeStart = candles[candles.Count - 1].TimeStart.Add(-_timeFrameSpan);
                }
                return candles;
            }

            return null;
        }

        /// <summary>
        /// add a new log message
        /// добавить в лог новое сообщение
        /// </summary>
        private void SendLogMessage(string message, LogMessageType type)
        {
            if (LogMessageEvent != null)
            {
                LogMessageEvent(message, type);
            }
            else
            {
                MessageBox.Show(message);
            }
        }

        /// <summary>
        /// outgoin log message
        /// исходящее сообщение для лога
        /// </summary>
        public event Action<string, LogMessageType> LogMessageEvent;
    }

    /// <summary>
    /// security in Finam specification
    /// контракт в спецификации финам
    /// </summary>
    public class FinamSecurity
    {
        /// <summary>
        /// unique number
        /// уникальный номер
        /// </summary>
        public string Id;

        /// <summary>
        /// name
        /// имя
        /// </summary>
        public string Name;

        /// <summary>
        /// код контракта
        /// </summary>
        public string Code;

        /// <summary>
        /// name of market
        /// название рынка 
        /// </summary>
        public string Market;

        /// <summary>
        /// name of market as a number
        /// название рынка в виде цифры
        /// </summary>
        public string MarketId;

        /// <summary>
        /// хз
        /// </summary>
        public string Decp;

        /// <summary>
        /// хз
        /// </summary>
        public string EmitentChild;

        /// <summary>
        /// web-site adress
        /// адрес на сайте
        /// </summary>
        public string Url;
    }
}
