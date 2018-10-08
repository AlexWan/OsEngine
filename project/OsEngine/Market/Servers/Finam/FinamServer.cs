/*
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
using System.Threading;
using System.Windows;
using OsEngine.Entity;
using OsEngine.Logging;
using OsEngine.Market.Servers.Entity;
using Action = System.Action;

namespace OsEngine.Market.Servers.Finam
{

    /// <summary>
    /// класс - сервер для подключения к Финам
    /// </summary>
    public class FinamServer : IServer
    {

        /// <summary>
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

            _logMaster = new Log("FinamServer");
            _logMaster.Listen(this);

            _serverStatusNead = ServerConnectStatus.Disconnect;

            _threadPrime = new Thread(PrimeThreadArea);
            _threadPrime.CurrentCulture = new CultureInfo("ru-RU");
            _threadPrime.IsBackground = true;
            _threadPrime.Start();

            if (ServerMaster.StartProgram != ServerStartProgramm.IsOsData)
            {
                Thread threadDataSender = new Thread(SenderThreadArea);
                threadDataSender.CurrentCulture = new CultureInfo("ru-RU");
                threadDataSender.IsBackground = true;
                threadDataSender.Start();
            }

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
        /// взять тип сервера
        /// </summary>
        /// <returns></returns>
        public ServerType ServerType { get; set; }

        /// <summary>
        /// показать окно настроект
        /// </summary>
        public void ShowDialog()
        {
            FinamServerUi ui = new FinamServerUi(this, _logMaster);
            ui.ShowDialog();
        }

        /// <summary>
        /// адрес сервера по которому нужно соединяться с сервером
        /// </summary>
        public string ServerAdress;

        /// <summary>
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
                if (value != _serverConnectStatus)
                {
                    _serverConnectStatus = value;
                    SendLogMessage(_serverConnectStatus + " Изменилось состояние соединения", LogMessageType.Connect);
                    if (ConnectStatusChangeEvent != null)
                    {
                        ConnectStatusChangeEvent(_serverConnectStatus.ToString());
                    }
                }
            }
        }

        /// <summary>
        /// вызывается когда статус соединения изменяется
        /// </summary>
        public event Action<string> ConnectStatusChangeEvent;

        // подключение / отключение

        /// <summary>
        /// запустить сервер
        /// </summary>
        public void StartServer()
        {
            if (ServerStatus == ServerConnectStatus.Connect)
            {
                SendLogMessage("Перехвачена попытка запустить сервер, со статусом Connect", LogMessageType.System);
                return;
            }
            _serverStatusNead = ServerConnectStatus.Connect;
        }

        /// <summary>
        /// остановить сервер СмартКом
        /// </summary>
        public void StopServer()
        {
            if (ServerStatus == ServerConnectStatus.Disconnect)
            {
                SendLogMessage("Перехвачена попытка остановить сервер, со статусом Disconnect", LogMessageType.System);
                return;
            }
            _serverStatusNead = ServerConnectStatus.Disconnect;
        }

        /// <summary>
        /// нужный статус сервера. Нужен потоку который следит за соединением
        /// В зависимости от этого поля управляет соединением
        /// </summary>
        private ServerConnectStatus _serverStatusNead;

        // работа основного потока !!!!!!

        /// <summary>
        /// основной поток, следящий за подключением, загрузкой портфелей и бумаг, пересылкой данных на верх
        /// </summary>
        private Thread _threadPrime;

        /// <summary>
        /// место в котором контролируется соединение.
        /// опрашиваются потоки данных
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
                        SendLogMessage("Запущена процедура активации подключения", LogMessageType.System);
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
                        SendLogMessage("Скачиваем бумаги", LogMessageType.System);
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
                    SendLogMessage("КРИТИЧЕСКАЯ ОШИБКА. Реконнект", LogMessageType.Error);
                    SendLogMessage(error.ToString(), LogMessageType.Error);
                    ServerStatus = ServerConnectStatus.Disconnect;

                    Thread.Sleep(5000);
                    // переподключаемся
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
        /// время последнего старта сервера
        /// </summary>
        private DateTime _lastStartServerTime = DateTime.MinValue;

        /// <summary>
        /// скачаны ли портфели и бумаги
        /// </summary>
        private bool _getSecurities;

        /// <summary>
        /// начать процесс подключения
        /// </summary>
        private void CheckPing()
        {
            Ping ping = new Ping();

            PingReply pingReply = ping.Send(ServerAdress);

            if (pingReply == null || pingReply.Status != IPStatus.Success ||
                pingReply.RoundtripTime > 1000)
            { // если что-то не так - выходим
                SendLogMessage("Ошибка доступа к серверу: ping is " + pingReply.Status + ". Не верный адрес или отсутствует интернет соединение", LogMessageType.Error);
                return;
            }

            ServerStatus = ServerConnectStatus.Connect;

            Thread.Sleep(10000);
        }

        /// <summary>
        /// проверка доступности страницы сервера
        /// </summary>
        private void CheckServer()
        {
            String pageContent = GetPage("http://" + ServerAdress);

            if (pageContent.Length == 0)
            { // если нет контента - выходим
                SendLogMessage("Ошибка доступа к серверу. Не верный адрес или отсутствует интернет соединение", LogMessageType.Error);
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
        /// включает загрузку инструментов и портфелей
        /// </summary>
        private void GetSecurities()
        {
            var response = GetPage("https://www.finam.ru/cache/icharts/icharts.js");

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
                _finamSecurities[i].Code = arrayCodes[i]; 
                _finamSecurities[i].Decp = arrayDecp[i].Split(':')[1];
                _finamSecurities[i].EmitentChild = arrayEmitentChild[i];
                _finamSecurities[i].Id = arrayIds[i];
                _finamSecurities[i].Name = arrayNames[i];
                _finamSecurities[i].Url = arrayEmitentUrls[i].Split(':')[1];

                _finamSecurities[i].MarketId = arrayMarkets[i];

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
                    _finamSecurities[i].Market = "Мировые Индексы";
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
            }

            _finamSecurities.AddRange(Ge3tCryptoSec());

            _securities = new List<Security>();

            for (int i = 0; i < _finamSecurities.Count; i++)
            {
                Security sec = new Security();
                sec.Name = _finamSecurities[i].Name;
                sec.NameFull = _finamSecurities[i].Code;
                sec.NameId = _finamSecurities[i].Id;
                sec.NameClass = _finamSecurities[i].Market;
                sec.PriceStep = 1;
                sec.PriceStepCost = 1;

                _securities.Add(sec);
            }

            _securitiesToSend.Enqueue(_securities);

            SendLogMessage("Доступно " +  _securities.Count + " бумаг.", LogMessageType.System);
        }

        private List<FinamSecurity> Ge3tCryptoSec()
        {
          List<FinamSecurity> crypto = new List<FinamSecurity>();

            for (int i = 0; i < 13; i++)
            {
                crypto.Add(new FinamSecurity());
                crypto[i].Market = "Криптовалюты";
                crypto[i].MarketId = 520.ToString();
            }

            crypto[0].Id = 484427.ToString();
            crypto[0].Code = "GDAX.ETH-BTC";
            crypto[0].Url = "cryptocurrencies/eth-btc";
            crypto[0].Name = "ETH-BTC";
            crypto[0].Decp = "5";

            crypto[1].Id = 491809.ToString();
            crypto[1].Code = "GDAX.BCH-USD";
            crypto[1].Url = "cryptocurrencies/bch-usd";
            crypto[1].Name = "BCH-USD";
            crypto[1].Decp = "2";

            crypto[2].Id = 484425.ToString();
            crypto[2].Code = "GDAX.BTC-EUR";
            crypto[2].Url = "cryptocurrencies/btc-eur";
            crypto[2].Name = "BTC-EUR";
            crypto[2].Decp = "2";

            crypto[3].Id = 484424.ToString();
            crypto[3].Code = "GDAX.BTC-GBP";
            crypto[3].Url = "cryptocurrencies/btc-gbp";
            crypto[3].Name = "BTC-GBP";
            crypto[3].Decp = "2";

            crypto[4].Id = 484429.ToString();
            crypto[4].Code = "GDAX.BTC-USD";
            crypto[4].Url = "cryptocurrencies/btc-usd";
            crypto[4].Name = "BTC-USD";
            crypto[4].Decp = "2";

            crypto[5].Id = 484427.ToString();
            crypto[5].Code = "GDAX.ETH-BTC";
            crypto[5].Url = "cryptocurrencies/eth-btc";
            crypto[5].Name = "ETH-BTC";
            crypto[5].Decp = "5";

            crypto[6].Id = 484426.ToString();
            crypto[6].Code = "GDAX.ETH-EUR";
            crypto[6].Url = "cryptocurrencies/eth-eur";
            crypto[6].Name = "ETH-EUR";
            crypto[6].Decp = "2";

            crypto[7].Id = 484430.ToString();
            crypto[7].Code = "GDAX.ETH-USD";
            crypto[7].Url = "cryptocurrencies/eth-usd";
            crypto[7].Name = "ETH-USD";
            crypto[7].Decp = "2";

            crypto[8].Id = 484423.ToString();
            crypto[8].Code = "GDAX.LTC-BTC";
            crypto[8].Url = "cryptocurrencies/ltc-btc";
            crypto[8].Name = "LTC-BTC";
            crypto[8].Decp = "5";

            crypto[9].Id = 484422.ToString();
            crypto[9].Code = "GDAX.LTC-EUR";
            crypto[9].Url = "cryptocurrencies/ltc-eur";
            crypto[9].Name = "LTC-EUR";
            crypto[9].Decp = "2";

            crypto[10].Id = 484428.ToString();
            crypto[10].Code = "GDAX.LTC-USD";
            crypto[10].Url = "cryptocurrencies/ltc-usd";
            crypto[10].Name = "LTC-USD";
            crypto[10].Decp = "2";

            crypto[11].Id = 491575.ToString();
            crypto[11].Code = "BTSM.ETH/EUR@milli";
            crypto[11].Url = "cryptocurrencies/ether-euro-milli";
            crypto[11].Name = "Ether / Euro milli";
            crypto[11].Decp = "2";

            crypto[12].Id = 491576.ToString();
            crypto[12].Code = "BTSP.ETH/USD@milli";
            crypto[12].Url = "cryptocurrencies/ether-usd-milli";
            crypto[12].Name = "Ether / U.S. dollar milli";
            crypto[12].Decp = "2";

            return crypto;
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

            SendLogMessage("Создан портфель для торговли в эмуляторе " + fakePortfolio.Number, LogMessageType.System);

            _portfolioToSend.Enqueue(Portfolios);
        }

        // работа потока рассылки !!!!!

        /// <summary>
        /// очередь тиков
        /// </summary>
        private ConcurrentQueue<List<Trade>> _tradesToSend;

        /// <summary>
        /// очередь новых инструментов
        /// </summary>
        private ConcurrentQueue<List<Security>> _securitiesToSend;

        /// <summary>
        /// очередь обновлённых серий свечек
        /// </summary>
        private ConcurrentQueue<CandleSeries> _candleSeriesToSend;

        /// <summary>
        /// очередь новых стаканов
        /// </summary>
        private ConcurrentQueue<MarketDepth> _marketDepthsToSend;

        /// <summary>
        /// очередь новых портфелей
        /// </summary>
        private ConcurrentQueue<List<Portfolio>> _portfolioToSend;

        /// <summary>
        /// очередь обновлений бида с аска по инструментам 
        /// </summary>
        private ConcurrentQueue<BidAskSender> _bidAskToSend;

        /// <summary>
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

        // бумаги
        private List<FinamSecurity> _finamSecurities;

        private List<Security> _securities;

        /// <summary>
        /// все инструменты в системе
        /// </summary>
        public List<Security> Securities
        {
            get { return _securities; }
        }

        /// <summary>
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
        /// вызывается при появлении новых инструментов
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

// портфели. Рассылается для торговли в эмуляторе на сервере финам

        private List<Portfolio> _portfolios;

        /// <summary>
        /// все счета в системе
        /// </summary>
        public List<Portfolio> Portfolios
        {
            get { return _portfolios; }
        }

        /// <summary>
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
        /// вызывается когда в системе появляются новые портфели
        /// </summary>
        public event Action<List<Portfolio>> PortfoliosChangeEvent;

        // Подпись на данные

        private List<FinamDataSeries> _finamDataSeries;

        /// <summary>
        /// объект блокирующий многопоточный доступ в StartThisSecurity
        /// </summary>
        private object _lockerStarter = new object();

        /// <summary>
        /// Начать выгрузку данных по инструменту. 
        /// </summary>
        /// <param name="namePaper">имя бумаги которую будем запускать</param>
        /// <param name="timeFrameBuilder">объект несущий в себе таймфрейм</param>
        /// <returns>В случае удачи возвращает CandleSeries
        /// в случае неудачи null</returns>
        public CandleSeries StartThisSecurity(string namePaper, TimeFrameBuilder timeFrameBuilder)
        {
            return null;
        }

        /// <summary>
        /// Начать выгрузку данных по инструменту. 
        /// </summary>
        /// <param name="securityId">айди бумаги</param>
        /// <param name="timeFrameBuilder">объект несущий в себе данные по таймФреймам</param>
        /// <param name="startTime">время начала загрузки</param>
        /// <param name="endTime">время завершения работы</param>
        /// <param name="actualTime">время последней загрузки данных</param>
        /// <param name="neadToUpdate">нужно ли автоматически обновлять</param>
        /// <returns>В случае удачи возвращает CandleSeries
        /// в случае неудачи null</returns>
        public CandleSeries StartThisSecurity(string securityId, TimeFrameBuilder timeFrameBuilder, DateTime startTime, DateTime endTime, DateTime actualTime, bool neadToUpdate)
        {
            try
            {
                if (_lastStartServerTime.AddSeconds(5) > DateTime.Now)
                {
                    return null;
                }

                // дальше по одному
                lock (_lockerStarter)
                {
                    if (securityId == null)
                    {
                        return null;
                    }
                    // надо запустить сервер если он ещё отключен
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
                    if (_lastStartServerTime != DateTime.MinValue &&
                        _lastStartServerTime.AddSeconds(15) > DateTime.Now)
                    {
                        return null;
                    }

                    Security security = null;

                    for (int i = 0; _securities != null && i < _securities.Count; i++)
                    {
                        if (_securities[i].NameId == securityId)
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

                    CandleSeries series = new CandleSeries(timeFrameBuilder, security)
                    {
                        CandlesAll = _candles,
                        IsStarted = true
                    };

                    if (ServerMaster.StartProgram != ServerStartProgramm.IsOsData)
                    {
                        series.СandleFinishedEvent += series_СandleFinishedEvent;
                        series.СandleUpdeteEvent += series_СandleFinishedEvent;
                    }


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

                    SendLogMessage("Инструмент " + series.Security.Name + "ТаймФрейм" + series.TimeFrame +
                                   " успешно подключен на получение данных и прослушивание свечек",
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

        public bool StartTickToSecurity(string id, DateTime startTime, DateTime endTime, DateTime actualTime, bool neadToUpdete)
        {
            try
            {
                if (_lastStartServerTime.AddSeconds(5) > DateTime.Now)
                {
                    return false;
                }

                // дальше по одному
                lock (_lockerStarter)
                {
                    if (id == null)
                    {
                        return false;
                    }
                    // надо запустить сервер если он ещё отключен
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
                    if (_lastStartServerTime != DateTime.MinValue &&
                        _lastStartServerTime.AddSeconds(15) > DateTime.Now)
                    {
                        return false;
                    }

                    Security security = null;


                    for (int i = 0; _securities != null && i < _securities.Count; i++)
                    {
                        if (_securities[i].NameId == id)
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

                    SendLogMessage("Инструмент " + security.Name + "ТаймФрейм Tick"+
                                   " успешно подключен на получение данных и прослушивание свечек",
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

// стакан. Рассылается для торговли в эмуляторе на сервере Финам

        /// <summary>
        /// вызывается когда изменяется бид или аск по инструменту
        /// </summary>
        public event Action<decimal, decimal, Security> NewBidAscIncomeEvent;

        /// <summary>
        /// вызывается когда изменяется стакан
        /// </summary>
        public event Action<MarketDepth> NewMarketDepthEvent;

        // выгрузка свечей

        private void ThreadDownLoaderArea()
        {
            while (true)
            {
                Thread.Sleep(5000);

                if (_lastStartServerTime.AddSeconds(15) > DateTime.Now)
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
        /// вызывается в момент изменения серий свечек
        /// </summary>
        public event Action<CandleSeries> NewCandleIncomeEvent;

        /// <summary>
        /// коннекторам подключеным к серверу необходимо перезаказать данные
        /// </summary>
        public event Action NeadToReconnectEvent;

        /// <summary>
        /// свечи скаченные из метода GetSmartComCandleHistory
        /// </summary>
        private List<Candle> _candles;

        // тики

        /// <summary>
        /// имена файлов загруженных из финам с трейдами
        /// </summary>
        private List<List<string>> _downLoadFilesWhithTrades;  

        private void finamDataSecies_TradesFilesUpdateEvent(List<string> files)
        {
            if (_downLoadFilesWhithTrades == null)
            {
                _downLoadFilesWhithTrades = new List<List<string>>();
            }

            for(int i =  0;i < files.Count;i++)
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
                // сортируем сделки по хранилищам
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
        /// все тики
        /// </summary>
        private List<Trade>[] _allTrades;

        /// <summary>
        /// все тики имеющиеся у сервера
        /// </summary>
        public List<Trade>[] AllTrades
        {
            get { return _allTrades; }
        }

        /// <summary>
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
        /// вызывается в момет появления новых трейдов по инструменту
        /// </summary>
        public event Action<List<Trade>> NewTradeEvent;

        // обработка лога

        /// <summary>
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
        /// менеджер лога
        /// </summary>
        private Log _logMaster;

        /// <summary>
        /// исходящее сообщение для лога
        /// </summary>
        public event Action<string, LogMessageType> LogMessageEvent;


        #region остальное из интерфейса сервера не реализовано, т.к. Финам не полный сервер

   
        // мои сделки

        /// <summary>
        /// мои сделки
        /// </summary>
        public List<MyTrade> MyTrades
        {
            get { return null; }
        }

        /// <summary>
        /// вызывается когда приходит новая моя сделка
        /// </summary>
        public event Action<MyTrade> NewMyTradeEvent;

        // работа с ордерами

        /// <summary>
        /// исполнить ордер
        /// </summary>
        public void ExecuteOrder(Order order)
        {

        }

        /// <summary>
        /// отменить ордер
        /// </summary>
        public void CanselOrder(Order order)
        {

        }

        /// <summary>
        /// вызывается когда в системе появляется новый ордер
        /// </summary>
        public event Action<Order> NewOrderIncomeEvent;

        private DateTime _serverTime;

        /// <summary>
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
        /// вызывается когда изменяется время сервера
        /// </summary>
        public event Action<DateTime> TimeServerChangeEvent;

        #endregion

    }

    /// <summary>
    /// серия данных для подгрузки истории в финам
    /// </summary>
    public class FinamDataSeries
    {

        /// <summary>
        /// префикс для адреса сервера
        /// </summary>
        public string ServerPrefics;

        /// <summary>
        /// контракт в финам спецификации
        /// </summary>
        public FinamSecurity SecurityFinam;

        /// <summary>
        /// контракт в формате Os.Engine
        /// </summary>
        public Security Security;

        /// <summary>
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
        /// таймфрейм в формате финам
        /// </summary>
        private string _timeFrameFinam;

        /// <summary>
        /// таймфрейм в формате TimeSpan
        /// </summary>
        private TimeSpan _timeFrameSpan;

        /// <summary>
        /// серия свечек
        /// </summary>
        public CandleSeries Series;

        /// <summary>
        /// время начала скачивания
        /// </summary>
        public DateTime TimeStart;

        /// <summary>
        /// время завершения скачивания
        /// </summary>
        public DateTime TimeEnd;

        /// <summary>
        /// актуальное время
        /// </summary>
        public DateTime TimeActual;

        /// <summary>
        /// обновились свечи
        /// </summary>
        public event Action<CandleSeries> CandleUpdateEvent;

        /// <summary>
        /// обновились трейды
        /// </summary>
        public event Action<List<string>> TradesUpdateEvent;

        /// <summary>
        /// является ли текущий объект скачивающим тики
        /// </summary>
        public bool IsTick;

        /// <summary>
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

                SendLogMessage(SecurityFinam.Name + " Старт скачивания данных. ТФ " + _timeFrame, LogMessageType.System);

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

                        for (int i = 0; trades!= null && i < trades.Count; i++)
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
            SendLogMessage(SecurityFinam.Name + " Закончили скачивание данных. ТФ " + _timeFrame, LogMessageType.System);
        }

        /// <summary>
        /// нужно ли обновлять серию автоматически
        /// </summary>
        public bool NeadToUpdeate;

        /// <summary>
        /// сет уже один раз подгружался
        /// </summary>
        public bool LoadedOnce;

        /// <summary>
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
        /// взять трейды за период
        /// </summary>
        /// <param name="timeStart"></param>
        /// <param name="timeEnd"></param>
        /// <returns></returns>
        private string GetTrades(DateTime timeStart, DateTime timeEnd)
        {
            SendLogMessage("Обновляем данные по трейдам для бумаги " + SecurityFinam.Name + " за " + timeStart.Date, LogMessageType.System);
            //http://195.128.78.52/GBPUSD_141201_141206.csv?market=5&em=86&code=GBPUSD&df=1&mf=11&yf=2014&from=01.12.2014&dt=6&mt=11&yt=2014&to=06.12.2014&
            //p=2&f=GBPUSD_141201_141206&e=.csv&cn=GBPUSD&dtf=1&tmf=3&MSOR=1&mstime=on&mstimever=1&sep=3&sep2=1&datf=5&at=1

            string timeStartInStrToName = timeStart.Year.ToString()[2].ToString() + timeStart.Year.ToString()[3].ToString() + timeStart.Month + TimeStart.Day;
            string timeEndInStrToName = timeEnd.Year.ToString()[2].ToString() + timeEnd.Year.ToString()[3].ToString() + timeEnd.Month + TimeStart.Day;

            string timeFrom = timeStart.ToShortDateString();
            string timeTo = timeEnd.ToShortDateString();

            string urlToSec = SecurityFinam.Name + "_" + timeStartInStrToName + "_" + timeEndInStrToName;

            string url = ServerPrefics + "/" +urlToSec+ ".txt?";

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
            url += "f=" + urlToSec+ "&";
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
            url += "at=" + "0" ;

// если мы уже эту серию трейдов качали, пробуем достать её из общего хранилища

            string fileName = @"Data\Temp\" + SecurityFinam.Name + "_" + timeStart.ToShortDateString() + ".txt";

            if (timeStart.Date != DateTime.Now.Date &&
                File.Exists(fileName))
            {
                return fileName;
            }


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
            { // файл не загружен
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

                if(s[5] == "S")
                {
                    builder.Append("Sell" );
                }
                else
                {
                    builder.Append("Buy" );
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
        /// взять свечи за период
        /// </summary>
        /// <param name="timeStart"></param>
        /// <param name="timeEnd"></param>
        /// <returns></returns>
        private List<Candle> GetCandles(DateTime timeStart, DateTime timeEnd)
        {
            SendLogMessage("Обновляем данные по свечам для бумаги " + SecurityFinam.Name + ". ТаймФрейм: " + TimeFrame + ". C " + timeStart.Date + " по " + timeEnd.Date, LogMessageType.System);
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
        /// исходящее сообщение для лога
        /// </summary>
        public event Action<string, LogMessageType> LogMessageEvent;
    }

    /// <summary>
    /// контракт в спецификации финам
    /// </summary>
    public class FinamSecurity
    {
        /// <summary>
        /// уникальный номер
        /// </summary>
        public string Id;

        /// <summary>
        /// имя
        /// </summary>
        public string Name;

        /// <summary>
        /// код контракта
        /// </summary>
        public string Code;

        /// <summary>
        /// название рынка 
        /// </summary>
        public string Market;

        /// <summary>
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
        /// адрес на сайте
        /// </summary>
        public string Url;
    }
}
