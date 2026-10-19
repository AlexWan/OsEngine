/*
 *Your rights to use the code are governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 *Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using OsEngine.Entity;
using OsEngine.Entity.WebSocketOsEngine;
using OsEngine.Language;
using OsEngine.Logging;
using OsEngine.Market.Servers.BCS.Entity;
using OsEngine.Market.Servers.Entity;
using RestSharp;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Authentication;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace OsEngine.Market.Servers.BCS
{
    public class BcsServer : AServer
    {
        public BcsServer(int uniqueNumber)
        {
            ServerNum = uniqueNumber;
            BcsServerRealization realization = new BcsServerRealization();
            ServerRealization = realization;

            ServerParameterPassword token = CreateParameterPassword(OsLocalization.Market.ServerParamToken, "");
            token.Comment = OsLocalization.Market.ServerParamBCSTokenDescription;

            ServerParameterBool useStock = CreateParameterBoolean(OsLocalization.Market.UseStock, true);
            ServerParameterBool useFutures = CreateParameterBoolean(OsLocalization.Market.UseFutures, false);
            ServerParameterBool useCurrency = CreateParameterBoolean(OsLocalization.Market.UseCurrency, false);
            ServerParameterBool useBonds = CreateParameterBoolean(OsLocalization.Market.UseBonds, false);
            ServerParameterBool useFunds = CreateParameterBoolean(OsLocalization.Market.UseFunds, false);
            ServerParameterBool useOptions = CreateParameterBoolean(OsLocalization.Market.UseOptions, false);
            ServerParameterBool useOther = CreateParameterBoolean(OsLocalization.Market.UseOther, false);
            useStock.Comment = OsLocalization.Market.UseStockDescription;
            useFutures.Comment = OsLocalization.Market.UseFuturesDescription;
            useCurrency.Comment = OsLocalization.Market.UseCurrencyDescription;
            useBonds.Comment = OsLocalization.Market.UseBondsDescription;
            useFunds.Comment = OsLocalization.Market.UseFundsDescription;
            useOptions.Comment = OsLocalization.Market.UseOptionsDescription.Split('.')[0];
            useOther.Comment = OsLocalization.Market.UseOtherDescription2;

            ServerParameterEnum depthLevels = CreateParameterEnum(OsLocalization.Market.ServerParam13, "10", new List<string> { "1", "10", "20" });
            depthLevels.Comment = OsLocalization.Market.SetDepthLevelsDescription;

            ServerParameterBool ignoreMorningAuction = CreateParameterBoolean(OsLocalization.Market.IgnoreMorningAuctionTrades, false);
            ignoreMorningAuction.Comment = OsLocalization.Market.IgnoreMorningAuctionTradesDescription;
        }
    }

    public class BcsServerRealization : IServerRealization
    {
        #region 1 Constructor, Status, Connection

        public BcsServerRealization()
        {
            Thread worker = new Thread(CheckLifetimeToken);
            worker.Name = "BcsCheckToken";
            worker.IsBackground = true;
            worker.Start();

            Thread worker2 = new Thread(DataMessageReader);
            worker2.Name = "BcsDataMessageReader";
            worker2.IsBackground = true;
            worker2.Start();

            Thread worker3 = new Thread(PortfolioMessageReader);
            worker3.Name = "BcsPortfolioMessageReader";
            worker3.IsBackground = true;
            worker3.Start();

            Thread worker4 = new Thread(OrderStateMessageReader);
            worker4.Name = "BcsOrdersMessageReader";
            worker4.IsBackground = true;
            worker4.Start();

            Thread worker5 = new Thread(MyTradesMessageReader);
            worker5.Name = "BcsMyTradesMessageReader";
            worker5.IsBackground = true;
            worker5.Start();

            Thread worker6 = new Thread(CheckSubscriptionsConfirmations);
            worker6.Name = "BcsCheckSubscriptionsConfirmations";
            worker6.IsBackground = true;
            worker6.Start();

            Thread worker7 = new Thread(MarketDataWatchdog);
            worker7.Name = "BcsMarketDataWatchdog";
            worker7.IsBackground = true;
            worker7.Start();

            Thread worker8 = new Thread(CheckAlivePrivateSockets);
            worker8.Name = "BcsCheckAlivePrivateSockets";
            worker8.IsBackground = true;
            worker8.Start();
        }

        private WebProxy _myProxy;

        public void Connect(WebProxy proxy = null)
        {
            try
            {
                _myProxy = proxy;

                SendLogMessage("Start Bcs Connection", LogMessageType.System);

                _apiTokenRefresh = ((ServerParameterPassword)ServerParameters[0]).Value;
                _ignoreMorningAuctionTrades = ((ServerParameterBool)ServerParameters[9]).Value;

                if (string.IsNullOrEmpty(_apiTokenRefresh))
                {
                    SendLogMessage("Connection terminated. You must specify the api token. You can get it on the Bcs website",
                        LogMessageType.Error);
                    return;
                }

                if (GetAccess24HToken() == false)
                {
                    SendLogMessage("Authorization Error. Probably an invalid token is specified. You can see it on the Bcs website.",
                        LogMessageType.Error);
                    return;
                }

                _lastReconnectTime = DateTime.Now;

                _privateSocketReconnectAllowed = true;
                _marketDataReconnectAllowed = true;

                ConfigureHandler();

                _clientForSocket = new HttpClient(_handler);

                CreatePublicWebSocketConnect();
                CreatePrivateWebSocketConnect();
            }
            catch (Exception ex)
            {
                SendLogMessage(ex.Message.ToString(), LogMessageType.Error);
            }
        }

        private void CreateHttpClient()
        {
            _httpClient = new HttpClient(_handler)
            {
                BaseAddress = new Uri(_baseUrl),
                Timeout = TimeSpan.FromSeconds(30)
            };

            _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        }

        private void ConfigureHandler()
        {
            _handler = new HttpClientHandler();

            if (_myProxy != null)
            {
                _handler.Proxy = _myProxy;
                _handler.UseProxy = true;
            }
            else
            {
                _handler.UseProxy = false;
            }

            _handler.SslProtocols = SslProtocols.Tls12 | SslProtocols.Tls13;
            _handler.CheckCertificateRevocationList = false;
        }

        private DateTime _lastReconnectTime = DateTime.MinValue;

        // перевыпуск токена из 401-обработчика и фонового потока одновременно не допускаем
        private string _tokenRefreshLocker = "tokenRefreshLockerBcs";

        private void CheckLifetimeToken()
        {
            while (true)
            {
                try
                {
                    Thread.Sleep(60000);

                    if (ServerStatus != ServerConnectStatus.Connect || _accessTokenExpireTime == DateTime.MinValue)
                    {
                        continue;
                    }

                    // плановое утреннее переподключение до начала дневной сессии:
                    // чистые сокеты + свежий токен на весь день. Строго в окне 06:30-09:30
                    // и не чаще раза в сутки: кулдаун 12ч в старой логике дрейфовал
                    // и переподключал коннектор посреди дневной/вечерней сессии
                    TimeSpan timeOfDay = DateTime.Now.TimeOfDay;

                    if (timeOfDay >= TimeSpan.FromHours(6.5)
                        && timeOfDay < TimeSpan.FromHours(9.5)
                        && _lastReconnectTime.Date < DateTime.Today)
                    {
                        SendLogMessage("BCS: плановое утреннее переподключение", LogMessageType.System);
                        _lastReconnectTime = DateTime.Now;
                        ForceDisconnect();
                        continue;
                    }

                    if (DateTime.Now > _accessTokenExpireTime.AddMinutes(-30)) // истекает срок токена
                    {
                        DateTime oldExpiry = _accessTokenExpireTime;

                        if (GetAccess24HToken() == false) // перевыпускаем токен
                        {
                            if (ServerStatus == ServerConnectStatus.Connect)
                            {
                                ServerStatus = ServerConnectStatus.Disconnect;
                                DisconnectEvent();
                            }
                        }
                        else
                        {
                            // сокеты авторизованы старым токеном при рукопожатии, переавторизовать живой сокет нельзя.
                            // Немедленный реконнект - только если токен уже истёк или не доживёт до планового окна,
                            // иначе переподключение сделает плановый блок в 06:30
                            DateTime nextMorning = DateTime.Today.AddHours(6.5);

                            if (nextMorning <= DateTime.Now)
                            {
                                nextMorning = nextMorning.AddDays(1);
                            }

                            if (oldExpiry <= DateTime.Now || nextMorning > oldExpiry.AddMinutes(30))
                            {
                                SendLogMessage("BCS: access token refreshed, reconnecting sockets with new token", LogMessageType.System);
                                ForceDisconnect();
                            }
                            else
                            {
                                SendLogMessage("BCS: access token refreshed, переподключение отложено до планового (06:30)", LogMessageType.System);
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    SendLogMessage(ex.ToString(), LogMessageType.Error);
                    Thread.Sleep(5000);
                }
            }
        }

        private void ForceDisconnect()
        {
            if (ServerStatus != ServerConnectStatus.Disconnect)
            {
                ServerStatus = ServerConnectStatus.Disconnect;
                DisconnectEvent();
            }
        }

        #region Private sockets seamless reconnect

        // Единственный поток, пересоздающий приватные сокеты (single writer, паттерн TInvest/OKX).
        // Портфельный сокет мёртв, если закрыт ИЛИ молчит более 30 секунд
        // (БКС шлёт полный снапшот портфеля каждые ~5 секунд, проверено зондом).
        // Ордерный сокет мёртв только по ReadyState - прикладного heartbeat у него нет.
        private void CheckAlivePrivateSockets()
        {
            Thread.Sleep(10000);

            while (true)
            {
                try
                {
                    Thread.Sleep(10000);

                    if (IsCompletelyDeleted == true)
                    {
                        return;
                    }

                    if (ServerStatus != ServerConnectStatus.Connect
                        || _privateSocketReconnectAllowed == false)
                    {
                        continue;
                    }

                    WebSocket portfolio = _webSocketPortfolio;

                    bool portfolioDead = portfolio == null
                        || portfolio.ReadyState != WebSocketState.Open
                        || (_lastPortfolioMessageTime != DateTime.MinValue
                            && _lastPortfolioMessageTime.AddSeconds(30) < DateTime.Now);

                    if (portfolioDead)
                    {
                        ReconnectPortfolioSocket();
                    }

                    WebSocket orders = _webSocketOrders;

                    if (orders == null
                        || orders.ReadyState != WebSocketState.Open)
                    {
                        ReconnectOrdersSocket();
                    }
                }
                catch (Exception ex)
                {
                    SendLogMessage(ex.ToString(), LogMessageType.Error);
                    Thread.Sleep(5000);
                }
            }
        }

        private void ReconnectPortfolioSocket()
        {
            try
            {
                if (ServerStatus != ServerConnectStatus.Connect
                    || _privateSocketReconnectAllowed == false)
                {
                    return;
                }

                if (_lastPortfolioReconnectTime != DateTime.MinValue
                    && _lastPortfolioReconnectTime.AddSeconds(30) > DateTime.Now)
                {
                    // throttled: не чаще одного реконнекта в 30 секунд
                    return;
                }

                _lastPortfolioReconnectTime = DateTime.Now;
                _portfolioReconnectAttempts++;

                SendLogMessage($"BCS WebSocket Portfolio reconnect attempt {_portfolioReconnectAttempts}/3.", LogMessageType.System);

                WebSocket oldSocket = _webSocketPortfolio;
                _webSocketPortfolio = null;

                if (oldSocket != null)
                {
                    // порядок важен: сначала закрываем старый, потом открываем новый -
                    // у портфельного сокета лимит 2 одновременных соединения
                    DetachPortfolioSocketEvents(oldSocket);

                    // закрываем безусловно: недооткрывшийся сокет тоже занимает слот сервера
                    oldSocket.CloseAsync();
                }

                WebSocket newSocket = CreatePortfolioSocket();

                if (newSocket == null)
                {
                    HandlePortfolioReconnectFailure();
                    return;
                }

                DateTime timeEnd = DateTime.Now.AddSeconds(10);

                while (newSocket.ReadyState != WebSocketState.Open)
                {
                    Thread.Sleep(1000);

                    if (timeEnd < DateTime.Now)
                    {
                        break;
                    }
                }

                if (newSocket.ReadyState != WebSocketState.Open)
                {
                    DetachPortfolioSocketEvents(newSocket);
                    newSocket.CloseAsync();

                    HandlePortfolioReconnectFailure();
                    return;
                }

                // Dispose мог пройти, пока ждали открытия: сокет не нужен, закрываем
                if (_privateSocketReconnectAllowed == false
                    || ServerStatus != ServerConnectStatus.Connect)
                {
                    DetachPortfolioSocketEvents(newSocket);
                    newSocket.CloseAsync();
                    return;
                }

                _webSocketPortfolio = newSocket;
                // grace-окно: если за 30 секунд снапшот не придёт,
                // watchdog сочтёт сокет мёртвым и пересоздаст ещё раз
                _lastPortfolioMessageTime = DateTime.Now;
                _portfolioReconnectAttempts = 0;

                SendLogMessage("BCS WebSocket Portfolio reconnected. Snapshot will arrive from the socket.", LogMessageType.System);
            }
            catch (Exception ex)
            {
                SendLogMessage($"{ex.Message} {ex.StackTrace}", LogMessageType.Error);
            }
        }

        private void HandlePortfolioReconnectFailure()
        {
            if (_portfolioReconnectAttempts >= 3)
            {
                SendLogMessage("BCS WebSocket Portfolio reconnect failed after maximum attempts. Restarting the connector.", LogMessageType.Error);
                ForceDisconnect();
            }
        }

        private void ReconnectOrdersSocket()
        {
            try
            {
                if (ServerStatus != ServerConnectStatus.Connect
                    || _privateSocketReconnectAllowed == false)
                {
                    return;
                }

                if (_webSocketOrders != null
                    && _webSocketOrders.ReadyState == WebSocketState.Open)
                {
                    return;
                }

                if (_lastOrdersReconnectTime != DateTime.MinValue
                    && _lastOrdersReconnectTime.AddSeconds(30) > DateTime.Now)
                {
                    // throttled: не чаще одного реконнекта в 30 секунд
                    return;
                }

                _lastOrdersReconnectTime = DateTime.Now;
                _ordersReconnectAttempts++;

                SendLogMessage($"BCS WebSocket Orders reconnect attempt {_ordersReconnectAttempts}/3.", LogMessageType.System);

                WebSocket oldSocket = _webSocketOrders;
                _webSocketOrders = null;

                if (oldSocket != null)
                {
                    DetachOrdersSocketEvents(oldSocket);

                    // закрываем безусловно: недооткрывшийся сокет тоже занимает слот сервера
                    oldSocket.CloseAsync();
                }

                WebSocket newSocket = CreateOrdersSocket();

                if (newSocket == null)
                {
                    HandleOrdersReconnectFailure();
                    return;
                }

                DateTime timeEnd = DateTime.Now.AddSeconds(10);

                while (newSocket.ReadyState != WebSocketState.Open)
                {
                    Thread.Sleep(1000);

                    if (timeEnd < DateTime.Now)
                    {
                        break;
                    }
                }

                if (newSocket.ReadyState != WebSocketState.Open)
                {
                    DetachOrdersSocketEvents(newSocket);
                    newSocket.CloseAsync();

                    HandleOrdersReconnectFailure();
                    return;
                }

                // Dispose мог пройти, пока ждали открытия: сокет не нужен, закрываем
                if (_privateSocketReconnectAllowed == false
                    || ServerStatus != ServerConnectStatus.Connect)
                {
                    DetachOrdersSocketEvents(newSocket);
                    newSocket.CloseAsync();
                    return;
                }

                _webSocketOrders = newSocket;
                _ordersReconnectAttempts = 0;

                SendLogMessage("BCS WebSocket Orders reconnected. Aligning orders from REST.", LogMessageType.System);

                AlignOrdersAfterReconnect();
            }
            catch (Exception ex)
            {
                SendLogMessage($"{ex.Message} {ex.StackTrace}", LogMessageType.Error);
            }
        }

        private void HandleOrdersReconnectFailure()
        {
            if (_ordersReconnectAttempts >= 3)
            {
                SendLogMessage("BCS WebSocket Orders reconnect failed after maximum attempts. Restarting the connector.", LogMessageType.Error);
                ForceDisconnect();
            }
        }

        // У ордерного сокета БКС нет снапшота и реюза событий (проверено зондом):
        // за окно обрыва события теряются, поэтому выравниваемся по REST.
        // Активные ордера - полным списком, финальные (исполненные/отменённые) - только те,
        // что изменились после потери соединения. Сделки добираются через trades/search
        // с дедупликацией по tradeNum.
        private void AlignOrdersAfterReconnect()
        {
            try
            {
                DateTime lostTime = _ordersConnectionLostTime;
                _ordersConnectionLostTime = DateTime.MinValue;

                List<Order> activeOrders = GetAllActiveOrdersFromExchange();

                for (int i = 0; i < activeOrders.Count; i++)
                {
                    if (IsMyOrderEventDuplicate(activeOrders[i]) == false)
                    {
                        MyOrderEvent?.Invoke(activeOrders[i]);
                    }

                    if (activeOrders[i].VolumeExecute > 0)
                    {
                        EnqueueMyTradeFetch(activeOrders[i]);
                    }
                }

                List<Order> recentFinalOrders = GetRecentFinalOrders();

                if (recentFinalOrders != null)
                {
                    for (int i = 0; i < recentFinalOrders.Count; i++)
                    {
                        Order order = recentFinalOrders[i];

                        if (lostTime != DateTime.MinValue
                            && order.TimeCallBack < lostTime.AddMinutes(-1))
                        {
                            continue;
                        }

                        if (IsMyOrderEventDuplicate(order) == false)
                        {
                            MyOrderEvent?.Invoke(order);
                        }

                        EnqueueMyTradeFetch(order);
                    }
                }

                if (ForceCheckOrdersAfterReconnectEvent != null)
                {
                    ForceCheckOrdersAfterReconnectEvent();
                }

                SendLogMessage($"BCS orders aligned after reconnect: {activeOrders.Count} active, recent final checked.", LogMessageType.System);
            }
            catch (Exception ex)
            {
                SendLogMessage($"Align orders after reconnect error: {ex.Message} {ex.StackTrace}", LogMessageType.Error);
            }
        }

        #endregion

        #region Market data sockets seamless reconnect

        // Единственный поток, пересоздающий сокеты маркет-даты (single writer, паттерн OKX/TInvest).
        // Сокет мёртв, если закрыт (ReadyState != Open). Тишина по конкретному сокету легальна
        // (зонд: у неликвида бывают паузы по 7,5 минут), поэтому отдельно проверяется только
        // глобальная тишина по всему пулу в торговые часы.
        private void MarketDataWatchdog()
        {
            Thread.Sleep(10000);

            while (true)
            {
                try
                {
                    Thread.Sleep(10000);

                    if (IsCompletelyDeleted == true)
                    {
                        return;
                    }

                    if (ServerStatus != ServerConnectStatus.Connect
                        || _marketDataReconnectAllowed == false)
                    {
                        continue;
                    }

                    List<BcsSocketWrapper> wrappers;

                    lock (_webSocketPublicLocker)
                    {
                        wrappers = new List<BcsSocketWrapper>(_webSocketPublicList);
                    }

                    for (int i = 0; i < wrappers.Count; i++)
                    {
                        BcsSocketWrapper wrapper = wrappers[i];

                        if (wrapper.Socket != null
                            && wrapper.Socket.ReadyState == WebSocketState.Open)
                        {
                            CheckCanarySilence(wrapper);
                            continue;
                        }

                        if (wrapper.Socket != null
                            && wrapper.CreatedTime != DateTime.MinValue
                            && wrapper.CreatedTime.AddSeconds(30) > DateTime.Now)
                        {
                            // сокет ещё открывается: обёртка держит ReadyState = Closed
                            // до хендшейка, поэтому мёртвым считаем только после grace 30 сек.
                            // У ConnectAsync свой таймаут 30 сек - не открылся,
                            // на следующих циклах пересоздадим уже как мёртвый
                            continue;
                        }

                        int subscriptionsCount;

                        lock (wrapper.Subscriptions)
                        {
                            subscriptionsCount = wrapper.Subscriptions.Count;
                        }

                        if (subscriptionsCount == 0)
                        {
                            // пустой мёртвый сокет не пересоздаём: бюджет 20 соединений
                            // не тратится на сокеты без подписок
                            RemovePublicSocketWrapper(wrapper);
                            continue;
                        }

                        ReconnectMarketDataSocket(wrapper);
                    }
                }
                catch (Exception ex)
                {
                    SendLogMessage(ex.ToString(), LogMessageType.Error);
                    Thread.Sleep(5000);
                }
            }
        }

        // Детектор молча мёртвого сокета: канарейка (ликвидный эталон) на живом рынке
        // молчать не может. Но тишина сама по себе не доказывает смерть фида - в выходные,
        // на пре-опене и у неликвида рынок легально молчит (логи 17-18.10.2026: старый
        // вотчдог из-за этого перезапускал здоровый коннектор каждые 10 минут).
        // Поэтому решение принимает REST-арбитр: свежая минутная свеча эталона = рынок жив,
        // а сокет молчит = фид мёртв, реконнектим. Старая свеча = рынок молчит, не трогаем.
        private void CheckCanarySilence(BcsSocketWrapper wrapper)
        {
            if (wrapper.LastMessageTime == DateTime.MinValue)
            {
                return;
            }

            // дешёвый предфильтр по часам: ночью не проверяем вовсе
            TimeSpan time = DateTime.Now.TimeOfDay;

            if (time < TimeSpan.FromMinutes(410) || time > TimeSpan.FromMinutes(1430))
            {
                return;
            }

            if (DateTime.Now - wrapper.LastMessageTime <= TimeSpan.FromMinutes(5))
            {
                return;
            }

            if (wrapper.LastArbiterTime != DateTime.MinValue
                && wrapper.LastArbiterTime.AddMinutes(10) > DateTime.Now)
            {
                // throttled: арбитраж не чаще раза в 10 минут на сокет
                return;
            }

            wrapper.LastArbiterTime = DateTime.Now;

            if (IsMarketAliveViaRest())
            {
                SendLogMessage($"BCS: канарейка {_canaryTicker} молчит более 5 минут, но рынок торгуется (REST). Сокет мёртв - реконнект.", LogMessageType.Error);
                ReconnectMarketDataSocket(wrapper);
            }
            else
            {
                SendLogMessage($"BCS: канарейка {_canaryTicker} молчит более 5 минут, по REST рынок тоже молчит (выходной/пре-опен/нет сделок). Сокет не трогаем.", LogMessageType.System);
            }
        }

        private bool IsMarketAliveViaRest()
        {
            try
            {
                Security canarySecurity = null;

                lock (_securitiesLocker)
                {
                    for (int i = 0; i < _securities.Count; i++)
                    {
                        if (_securities[i].Name == _canaryTicker
                            && GetClassCode(_securities[i]) == _canaryClassCode)
                        {
                            canarySecurity = _securities[i];
                            break;
                        }
                    }
                }

                if (canarySecurity == null)
                {
                    // бумага не загружена (её класс выключен в параметрах) -
                    // для REST-ответа хватит синтетической
                    canarySecurity = new Security();
                    canarySecurity.Name = _canaryTicker;
                    canarySecurity.NameClass = "STOCK-" + _canaryClassCode;
                    canarySecurity.Decimals = 2;
                    canarySecurity.DecimalsVolume = 0;
                }

                DateTime endTime = DateTime.Now;
                DateTime startTime = endTime.AddMinutes(-15);

                List<Candle> candles = RequestHistoryCandles(canarySecurity, _canaryClassCode, "M1", startTime, endTime);

                if (candles == null)
                {
                    // REST не ответил: неизвестность - не повод ронять сокет
                    SendLogMessage("BCS canary arbiter: REST candles request failed, socket left as is", LogMessageType.System);
                    return false;
                }

                DateTime lastCandleTime = DateTime.MinValue;

                for (int i = 0; i < candles.Count; i++)
                {
                    if (candles[i].TimeStart > lastCandleTime)
                    {
                        lastCandleTime = candles[i].TimeStart;
                    }
                }

                // свежая минутная свеча = рынок торгуется прямо сейчас
                return lastCandleTime != DateTime.MinValue
                    && lastCandleTime >= DateTime.Now.AddMinutes(-5);
            }
            catch (Exception ex)
            {
                SendLogMessage(ex.ToString(), LogMessageType.Error);
                return false;
            }
        }

        private void ReconnectMarketDataSocket(BcsSocketWrapper wrapper)
        {
            try
            {
                if (wrapper == null
                    || ServerStatus != ServerConnectStatus.Connect
                    || _marketDataReconnectAllowed == false)
                {
                    return;
                }

                if (wrapper.LastReconnectTime != DateTime.MinValue
                    && wrapper.LastReconnectTime.AddSeconds(30) > DateTime.Now)
                {
                    // throttled: не чаще одного реконнекта в 30 секунд на сокет
                    return;
                }

                wrapper.LastReconnectTime = DateTime.Now;
                wrapper.ReconnectAttempts++;

                SendLogMessage($"BCS WebSocket MarketData reconnect attempt {wrapper.ReconnectAttempts}/3.", LogMessageType.System);

                // порядок важен: сначала закрываем старый, потом открываем новый -
                // у маркет-даты жёсткий бюджет 20 одновременных соединений,
                // временное удвоение сокетов при массовом падении пула упрётся в лимит
                WebSocket oldSocket = wrapper.Socket;
                wrapper.Socket = null;
                wrapper.CanaryActive = false;

                if (oldSocket != null)
                {
                    DetachMarketDataSocketEvents(oldSocket);

                    // закрываем безусловно, не только Open: у недооткрывшегося сокета
                    // хендшейк иначе завершится на сервере и ~минуту жрёт слот
                    // из бюджета 20 соединений (бой 19.10: призрак от гонки дал 403 на #17)
                    oldSocket.CloseAsync();
                }

                // пауза между пересозданиями: при массовом падении пула
                // сокеты восстанавливаются по одному, без одновременного штурма
                Thread.Sleep(500);

                WebSocket newSocket = CreateNewSocketMarketData();

                if (newSocket == null)
                {
                    HandleMarketDataReconnectFailure(wrapper);
                    return;
                }

                // назначаем до ожидания открытия: Opened-хук найдёт wrapper по sender
                // и восстановит его подписки одним пакетом
                wrapper.Socket = newSocket;
                wrapper.CreatedTime = DateTime.Now;

                DateTime timeEnd = DateTime.Now.AddSeconds(10);

                while (newSocket.ReadyState != WebSocketState.Open)
                {
                    Thread.Sleep(1000);

                    if (timeEnd < DateTime.Now)
                    {
                        break;
                    }
                }

                if (newSocket.ReadyState != WebSocketState.Open)
                {
                    // полуоткрытый сокет бросаем сразу: он всё равно занимает слот в бюджете соединений
                    DetachMarketDataSocketEvents(newSocket);
                    newSocket.CloseAsync();

                    wrapper.Socket = null;

                    HandleMarketDataReconnectFailure(wrapper);
                    return;
                }

                // Dispose мог пройти, пока ждали открытия: сокет не нужен, закрываем
                if (_marketDataReconnectAllowed == false
                    || ServerStatus != ServerConnectStatus.Connect)
                {
                    DetachMarketDataSocketEvents(newSocket);
                    newSocket.CloseAsync();
                    wrapper.Socket = null;
                    return;
                }

                wrapper.ReconnectAttempts = 0;

                SendLogMessage("BCS WebSocket MarketData reconnected. Subscriptions restored from the Opened hook.", LogMessageType.System);
            }
            catch (Exception ex)
            {
                SendLogMessage($"{ex.Message} {ex.StackTrace}", LogMessageType.Error);
            }
        }

        private void HandleMarketDataReconnectFailure(BcsSocketWrapper wrapper)
        {
            if (wrapper.ReconnectAttempts >= 3)
            {
                SendLogMessage("BCS WebSocket MarketData reconnect failed after maximum attempts. Restarting the connector.", LogMessageType.Error);
                ForceDisconnect();
            }
        }

        private BcsSocketWrapper FindPublicSocketWrapper(WebSocket socket)
        {
            if (socket == null)
            {
                return null;
            }

            lock (_webSocketPublicLocker)
            {
                for (int i = 0; i < _webSocketPublicList.Count; i++)
                {
                    if (ReferenceEquals(_webSocketPublicList[i].Socket, socket))
                    {
                        return _webSocketPublicList[i];
                    }
                }
            }

            return null;
        }

        private void RemovePublicSocketWrapper(BcsSocketWrapper wrapper)
        {
            lock (_webSocketPublicLocker)
            {
                _webSocketPublicList.Remove(wrapper);

                // чистим маппинги бумаг, указывающие на удалённый wrapper
                List<string> keysToRemove = null;

                foreach (KeyValuePair<string, BcsSocketWrapper> pair in _wrapperBySecurityName)
                {
                    if (ReferenceEquals(pair.Value, wrapper))
                    {
                        if (keysToRemove == null)
                        {
                            keysToRemove = new List<string>();
                        }

                        keysToRemove.Add(pair.Key);
                    }
                }

                if (keysToRemove != null)
                {
                    for (int i = 0; i < keysToRemove.Count; i++)
                    {
                        _wrapperBySecurityName.Remove(keysToRemove[i]);
                    }
                }
            }

            WebSocket socket = wrapper.Socket;
            wrapper.Socket = null;

            if (socket != null)
            {
                DetachMarketDataSocketEvents(socket);
                socket.CloseAsync();
            }
        }

        #endregion

        private bool GetAccess24HToken()
        {
            try
            {
                string endPoint = "/trade-api-keycloak/realms/tradeapi/protocol/openid-connect/token";
                RestRequest requestRest = new RestRequest(endPoint, Method.POST);
                RestClient client = new RestClient(_baseUrl);

                requestRest.AddHeader("Content-Type", "application/x-www-form-urlencoded");
                requestRest.AddHeader("Accept", "application/json");
                requestRest.AddParameter("grant_type", "refresh_token");
                requestRest.AddParameter("client_id", "trade-api-write");
                requestRest.AddParameter("refresh_token", _apiTokenRefresh);

                if (_myProxy != null)
                {
                    client.Proxy = _myProxy;
                }

                IRestResponse response = client.Execute(requestRest);

                if (response.StatusCode == HttpStatusCode.OK)
                {
                    BcsAuthResponse tokenResp = JsonConvert.DeserializeAnonymousType(response.Content, new BcsAuthResponse());

                    if (tokenResp != null)
                    {
                        _apiAccessToken = tokenResp.access_token;
                        _accessTokenExpireTime = DateTime.Now.AddSeconds(Convert.ToInt32(tokenResp.expires_in));
                        _refreshTokenExpireTime = DateTime.Now.AddSeconds(Convert.ToInt32(tokenResp.refresh_expires_in));

                        if (DateTime.Now.AddDays(5) > _refreshTokenExpireTime) // Refresh-токен имеет срок жизни 90 суток
                        {
                            SendLogMessage("Attention! The token's lifetime is less than 5 days.", LogMessageType.Error);
                        }

                        return true;
                    }
                    else
                    {
                        SendLogMessage($"Token request error: {response.Content}", LogMessageType.Error);
                        return false;
                    }
                }
                else
                {
                    SendLogMessage($"Token request error: code: {response.StatusCode} > msg: {response.Content}", LogMessageType.Error);
                    return false;
                }
            }
            catch (Exception exception)
            {
                SendLogMessage("Token request error: " + exception.ToString(), LogMessageType.Error);
                return false;
            }
        }

        public void Dispose()
        {
            // сначала запрещаем бесшовный реконнект: Closed-события закрываемых ниже сокетов
            // не должны запускать пересоздание
            _privateSocketReconnectAllowed = false;
            _marketDataReconnectAllowed = false;

            _portfolioReconnectAttempts = 0;
            _ordersReconnectAttempts = 0;
            _lastPortfolioReconnectTime = DateTime.MinValue;
            _lastOrdersReconnectTime = DateTime.MinValue;
            _lastPortfolioMessageTime = DateTime.MinValue;
            _ordersConnectionLostTime = DateTime.MinValue;

            _canaryIsUserSubscribed = false;

            _myPortfolios.Clear();
            _securitiesLots.Clear();
            _accessTokenExpireTime = DateTime.MinValue;
            _hasLimitReached = false;

            lock (_depthSubscribeLocker)
            {
                _tradesSubscribeTimes.Clear();
                _tradesSubscribeAttempts.Clear();
                _depthSubscribeTimes.Clear();
                _depthSubscribeAttempts.Clear();
                _loggedEmptyDepth.Clear();
                _lastDepthConfirmTime = DateTime.MinValue;
            }

            lock (_pendingMyTradeFetchLocker)
            {
                _pendingMyTradeFetchOrderNumbers.Clear();
            }

            _lastTradeTime.Clear();

            lock (_sentMyTradesLocker)
            {
                _sentMyTradesNumbers.Clear();
            }

            lock (_sentMyOrderEventsLocker)
            {
                _sentMyOrderEvents.Clear();
            }

            UnsubscribeAllSecurities();
            _subscribedSecurities.Clear();
            DeleteWebSocketConnection();

            WebSocketDataMessage = new ConcurrentQueue<string>();
            WebSocketPortfolioMessage = new ConcurrentQueue<string>();
            WebSocketMyOrdersAndTradesMessage = new ConcurrentQueue<string>();
            MyTradesToFetchQueue = new ConcurrentQueue<MyTradeFetchRequest>();

            _lastMdTime = DateTime.MinValue;

            _httpClient?.Dispose();
            _httpClient = null;

            _clientForSocket?.Dispose();
            _clientForSocket = null;

            _handler?.Dispose();
            _handler = null;

            if (ServerStatus != ServerConnectStatus.Disconnect)
            {
                ServerStatus = ServerConnectStatus.Disconnect;
                DisconnectEvent();
            }
        }

        public DateTime ServerTime { get; set; }

        public ServerType ServerType => ServerType.BCS;

        public ServerConnectStatus ServerStatus { get; set; } = ServerConnectStatus.Disconnect;

        public List<IServerParameter> ServerParameters { get; set; }

        public event Action ConnectEvent;

        public event Action DisconnectEvent;

        public event Action ForceCheckOrdersAfterReconnectEvent;

        public bool IsCompletelyDeleted { get; set; }

        #endregion

        #region 2 Properties

        private readonly string _baseUrl = "https://be.broker.ru";

        private HttpClient _httpClient;
        private HttpClient _clientForSocket;
        private HttpClientHandler _handler;

        private bool _useStock = false;
        private bool _useFutures = false;
        private bool _useOptions = false;
        private bool _useCurrency = false;
        private bool _useBonds = false;
        private bool _useFunds = false;
        private bool _useOther = false;
        private bool _ignoreMorningAuctionTrades = true; // ignore trades before 7:00 MSK


        private string _apiTokenRefresh;
        private string _apiAccessToken; // life time 24 h

        private DateTime _accessTokenExpireTime = DateTime.MinValue;
        private DateTime _refreshTokenExpireTime = DateTime.MinValue;
        TimeZoneInfo _moscowTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Russian Standard Time");

        // market data socket pool: each wrapper remembers its subscriptions,
        // a dead socket is reconnected and resubscribed on its own (OKX/TInvest pattern).
        // BCS limits: 20 simultaneous market data connections, 100 instruments per connection
        private List<BcsSocketWrapper> _webSocketPublicList = new List<BcsSocketWrapper>();

        // all accesses to the public sockets pool go through this locker:
        // the watchdog thread iterates it, Subscribe adds sockets, Dispose clears it
        private string _webSocketPublicLocker = "_webSocketPublicLockerBcs";

        // security name -> wrapper holding its subscription: ReSubscribeTrades
        // must send to the socket the security actually lives on, not to the last one
        private Dictionary<string, BcsSocketWrapper> _wrapperBySecurityName = new Dictionary<string, BcsSocketWrapper>();

        // canary heartbeat: every market data socket carries a trades subscription on a liquid
        // benchmark, so a silently dead socket is visible per-socket.
        // Canary events are not passed to the engine unless the user subscribed the instrument
        private readonly string _canaryTicker = "SBER";
        private readonly string _canaryClassCode = "TQBR";
        private bool _canaryIsUserSubscribed;

        private WebSocket _webSocketPortfolio;
        private WebSocket _webSocketOrders;
        private bool _socketPortfolioIsActive;
        private bool _socketOrdersIsActive;
        private string _activationLocker = "activationLocker";

        // seamless reconnect of private sockets (portfolio + orders):
        // a dead socket is recreated by the CheckAlivePrivateSockets thread,
        // the connector stays connected (OKX/TInvest pattern)
        private bool _privateSocketReconnectAllowed = true;
        private DateTime _lastPortfolioMessageTime = DateTime.MinValue;
        private DateTime _lastPortfolioReconnectTime = DateTime.MinValue;
        private int _portfolioReconnectAttempts;
        private DateTime _lastOrdersReconnectTime = DateTime.MinValue;
        private int _ordersReconnectAttempts;
        private DateTime _ordersConnectionLostTime = DateTime.MinValue;

        // seamless reconnect of market data sockets: a dead socket is recreated
        // by the MarketDataWatchdog thread, the connector stays connected
        private bool _marketDataReconnectAllowed = true;

        #endregion

        #region 3 Securities

        private List<Security> _securities = new List<Security>();

        private RateGate _rateGateSecurity = new RateGate(1, TimeSpan.FromMilliseconds(100));

        public void GetSecurities()
        {
            if (_securities.Count > 0)
                _securities.Clear();

            _useStock = ((ServerParameterBool)ServerParameters[1]).Value;
            _useFutures = ((ServerParameterBool)ServerParameters[2]).Value;
            _useCurrency = ((ServerParameterBool)ServerParameters[3]).Value;
            _useBonds = ((ServerParameterBool)ServerParameters[4]).Value;
            _useFunds = ((ServerParameterBool)ServerParameters[5]).Value;
            _useOptions = ((ServerParameterBool)ServerParameters[6]).Value;
            _useOther = ((ServerParameterBool)ServerParameters[7]).Value;

            if (_useStock)
            {
                List<BcsSecurity> securities = GetSecuritiesByType("STOCK");

                if (securities != null && securities.Count > 0)
                    UpdateSecuritiesFromServer(securities);
            }

            if (_useCurrency)
            {
                List<BcsSecurity> securities = GetSecuritiesByType("CURRENCY");

                if (securities != null && securities.Count > 0)
                    UpdateSecuritiesFromServer(securities);
            }

            if (_useFutures)
            {
                List<BcsSecurity> securities = GetSecuritiesByType("FUTURES");

                if (securities != null && securities.Count > 0)
                    UpdateSecuritiesFromServer(securities);
            }

            if (_useBonds)
            {
                List<BcsSecurity> securities = GetSecuritiesByType("BONDS");

                if (securities != null && securities.Count > 0)
                    UpdateSecuritiesFromServer(securities);
            }

            if (_useFunds)
            {
                List<BcsSecurity> securities = GetSecuritiesByType("MUTUAL_FUNDS");

                if (securities != null && securities.Count > 0)
                    UpdateSecuritiesFromServer(securities);

                securities = GetSecuritiesByType("ETF");

                if (securities != null && securities.Count > 0)
                    UpdateSecuritiesFromServer(securities);
            }

            if (_useOptions)
            {
                string[] baseAssetsForOptions = new string[] {"VTBR", "SBER", "T", "GAZP", "LKOH", "NVTK", "AFLT", "PLZL", "GMKN", "YNDX", "PIKK", "SMLT", "SNGSP",
                    "CHMF", "NLMK", "SVCB", "ROSN", "AFKS", "VKCO", "MTLR", "TATN", "MGNT", "ALRS", "MAGN", "MOEX", "SBERP", "SNGS", "MTSS", "RTKM", "IRAO",
                    "FEES", "POSI", "TATNP", "GLDRUB_TOM", "CNY000SMALL", "EUR000SMALL", "USD000SMALL" };

                for (int i = 0; i < baseAssetsForOptions.Length; i++)
                {
                    List<BcsSecurity> securities = GetSecuritiesByType("OPTIONS", baseAssetsForOptions[i]);

                    if (securities != null && securities.Count > 0)
                        UpdateSecuritiesFromServer(securities);
                }
            }

            if (_useOther)
            {
                List<BcsSecurity> securities = GetSecuritiesByType("INDICES");

                if (securities != null && securities.Count > 0)
                    UpdateSecuritiesFromServer(securities);

                securities = GetSecuritiesByType("GOODS");

                if (securities != null && securities.Count > 0)
                    UpdateSecuritiesFromServer(securities);
            }

            if (_securities.Count > 0)
            {
                SendLogMessage("Securities loaded. Count: " + _securities.Count, LogMessageType.System);

                SecurityEvent?.Invoke(_securities);

                Task.Run(() => TryLoadMarginAndLimitsFromMoex());
            }
        }

        private List<BcsSecurity> GetSecuritiesByType(string secType, string baseAsset = null)
        {
            string apiEndpoint = "/trade-api-information-service/api/v1/instruments/by-type?";

            string type = $"type={secType}";

            string size = "size=100";

            int page = -1;

            List<BcsSecurity> securitiesResp = [];
            List<BcsSecurity> bcsSec = [];

            // счётчик идёт по последовательным ошибкам и сбрасывается после успеха:
            // в старой логике после первой ошибки пагинация шла до пустой страницы,
            // а 5 ошибок обнуляли ВЕСЬ скачанный класс бумаг
            int failCount = 0;

            do
            {
                page++;

                string path = baseAsset == null ? apiEndpoint + type + "&" + size + "&" + "page=" + page : apiEndpoint + type + "&" + size + "&" + "page=" + page + "&" + "baseAssetTicker=" + baseAsset;

                _rateGateSecurity.WaitToProceed();

                HttpResponseMessage response = CreateHttpRequestAsync(path, HttpMethod.Get, null).Result;

                if (response.StatusCode == HttpStatusCode.OK)
                {
                    failCount = 0;

                    try
                    {
                        string responseMsg = response.Content.ReadAsStringAsync().Result;
                        bcsSec = JsonConvert.DeserializeAnonymousType(responseMsg, new List<BcsSecurity>());

                        if (bcsSec != null && bcsSec.Count > 0)
                        {
                            securitiesResp.AddRange(bcsSec);
                        }
                        else
                        {
                            return securitiesResp;
                        }
                    }
                    catch (Exception ex)
                    {
                        SendLogMessage($"Error parsing securities: {ex}", LogMessageType.Error);
                        return securitiesResp;
                    }
                }
                else
                {
                    failCount++;

                    string errorMsg = response.Content.ReadAsStringAsync().Result;

                    SendLogMessage($"Securities request error. Status: {response.StatusCode}-{response.ReasonPhrase}\n" +
                        $"Msg: {errorMsg}\n" +
                        $"Try: {failCount} downloading securities {type}", LogMessageType.Error);

                    page--;

                    continue;
                }

            } while ((bcsSec.Count == 100 || failCount > 0) && failCount < 5);

            if (failCount >= 5)
            {
                // класс не теряем: возвращаем всё, что успели скачать
                SendLogMessage($"There are too many attempts to download securities {type} type. Возвращаю частичный список: {securitiesResp.Count} бумаг.", LogMessageType.Error);
            }

            return securitiesResp;
        }

        private void UpdateSecuritiesFromServer(List<BcsSecurity> securities)
        {
            for (int i = 0; i < securities.Count; i++)
            {
                try
                {
                    BcsSecurity item = securities[i];

                    SecurityType instrumentType = GetSecurityType(item.instrumentType);

                    if (instrumentType == SecurityType.None)
                    {
                        continue;
                    }

                    if ((instrumentType == SecurityType.Stock || instrumentType == SecurityType.Fund) && item.boards[0].classCode != "TQBR")
                    {
                        continue;
                    }

                    if (instrumentType == SecurityType.Stock
                        && item.bcsScore.ToDecimal() == 0
                        && item.priceChangeMonth.ToDecimal() == 0)
                    {
                        continue;
                    }

                    Security newSecurity = new Security();
                    newSecurity.SecurityType = instrumentType;
                    newSecurity.Exchange = item.boards[0].exchange;
                    newSecurity.DecimalsVolume = 0;
                    newSecurity.VolumeStep = 1;
                    newSecurity.Name = item.ticker;
                    newSecurity.NameFull = item.displayName;
                    newSecurity.Lot = instrumentType == SecurityType.Futures || instrumentType == SecurityType.Option ? 1.0m : item.lotSize.ToDecimal();
                    newSecurity.NameId = string.IsNullOrEmpty(item.isin) ? i + "-" + item.ticker + "-" + item.boards[0].exchange : item.isin;

                    decimal minStep = item.minimumStep.ToDecimal();

                    if (minStep <= 0)
                    {
                        // шаг цены не пришёл: без дефолта свечи молча округлятся до целых
                        SendLogMessage($"Security {item.ticker}: minimumStep не задан API, принят шаг цены 1.", LogMessageType.System);
                        minStep = 1;
                    }

                    newSecurity.Decimals = GetDecimals(minStep);
                    newSecurity.PriceStep = minStep;
                    newSecurity.PriceStepCost = newSecurity.PriceStep;

                    if (instrumentType == SecurityType.Futures || instrumentType == SecurityType.Option)
                    {
                        decimal stepPrice = item.stepPrice.ToDecimal();

                        if (stepPrice > 0)
                        {
                            newSecurity.PriceStepCost = stepPrice;
                        }
                    }
                    newSecurity.State = SecurityStateType.Activ;

                    string instrType = item.instrumentType.StartsWith("MUT") ? item.instrumentType.Split('_')[1] : item.instrumentType;
                    newSecurity.NameClass = instrType + "-" + item.boards[0].classCode;

                    if (_useBonds && newSecurity.SecurityType == SecurityType.Bond)
                    {
                        newSecurity.AciValue = item.accruedInt.ToDecimal();
                        newSecurity.NominalCurrent = item.faceValue.ToDecimal();
                        newSecurity.NominalInitial = item.faceValue.ToDecimal();

                        DateTime emissionDate = ParseBcsDate(item.emissionDate);

                        if (emissionDate != DateTime.MinValue)
                        {
                            newSecurity.PlacementDate = emissionDate;
                        }
                    }

                    DateTime matDate = ParseBcsDate(item.maturityDate);

                    if (matDate != DateTime.MinValue)
                    {
                        if (newSecurity.SecurityType == SecurityType.Futures)
                        {
                            newSecurity.Expiration = matDate;
                            newSecurity.UsePriceStepCostToCalculateVolume = true;
                        }
                        else if (newSecurity.SecurityType == SecurityType.Bond)
                        {
                            newSecurity.MaturityDate = matDate;
                        }
                        else if (newSecurity.SecurityType == SecurityType.Option)
                        {
                            newSecurity.Expiration = matDate;
                            newSecurity.UsePriceStepCostToCalculateVolume = true;
                            newSecurity.Strike = item.strike.ToDecimal();
                            newSecurity.OptionType = item.type == "put" ? OptionType.Put : OptionType.Call;
                            newSecurity.UnderlyingAsset = item.baseAssetSecuritySecCode;
                        }
                    }
                    _securities.Add(newSecurity);
                }
                catch (Exception ex)
                {
                    // одна битая бумага не должна ронять всю пачку
                    SendLogMessage($"Security parsing error ({securities[i].ticker}):\n {ex.Message} - {ex.StackTrace}", LogMessageType.Error);
                }
            }
        }

        private SecurityType GetSecurityType(string secType)
        {
            if (secType.Equals("FUTURES"))
            {
                return SecurityType.Futures;
            }
            else if (secType.Equals("OPTIONS"))
            {
                return SecurityType.Option;
            }
            else if (secType.Equals("STOCK"))
            {
                return SecurityType.Stock;
            }
            else if (secType.Equals("BONDS"))
            {
                return SecurityType.Bond;
            }
            else if (secType.Equals("MUTUAL_FUNDS") || secType.Equals("ETF"))
            {
                return SecurityType.Fund;
            }
            else if (secType.Equals("INDICES"))
            {
                return SecurityType.Index;
            }
            else if (secType.Equals("CURRENCY"))
            {
                return SecurityType.CurrencyPair;
            }
            else if (secType.Equals("GOODS"))
            {
                return SecurityType.Commodities;
            }

            return SecurityType.None;
        }

        private int GetDecimals(decimal x)
        {
            int precision = 0;
            while (x * (decimal)Math.Pow(10, precision) != Math.Round(x * (decimal)Math.Pow(10, precision)))
                precision++;
            return precision;
        }

        private void TryLoadMarginAndLimitsFromMoex()
        {
            try
            {
                // догружаем ГО и ценовые лимиты по фьючерсам с MOEX ISS

                string url = "https://iss.moex.com/iss/engines/futures/markets/forts/securities.json" +
                    "?securities.columns=SECID,INITIALMARGIN,HIGHLIMIT,LOWLIMIT&iss.meta=off&iss.only=securities&securities.limit=1000";

                string json;

                HttpClientHandler moexHandler = new HttpClientHandler();

                if (_myProxy != null)
                {
                    moexHandler.Proxy = _myProxy;
                    moexHandler.UseProxy = true;
                }
                else
                {
                    moexHandler.UseProxy = false;
                }

                using (HttpClient client = new HttpClient(moexHandler))
                {
                    client.Timeout = TimeSpan.FromSeconds(15);

                    json = client.GetStringAsync(url).Result;
                }

                if (string.IsNullOrEmpty(json))
                {
                    return;
                }

                JObject response = JObject.Parse(json);

                JArray columns = response["securities"]?["columns"] as JArray;
                JArray data = response["securities"]?["data"] as JArray;

                if (columns == null || data == null || data.Count == 0)
                {
                    return;
                }

                int indexSecId = -1;
                int indexMargin = -1;
                int indexHighLimit = -1;
                int indexLowLimit = -1;

                for (int i = 0; i < columns.Count; i++)
                {
                    string column = columns[i].ToString();

                    if (column == "SECID") indexSecId = i;
                    else if (column == "INITIALMARGIN") indexMargin = i;
                    else if (column == "HIGHLIMIT") indexHighLimit = i;
                    else if (column == "LOWLIMIT") indexLowLimit = i;
                }

                if (indexSecId < 0 || indexMargin < 0 || indexHighLimit < 0 || indexLowLimit < 0)
                {
                    return;
                }

                bool isUpdated = false;

                for (int i = 0; i < data.Count; i++)
                {
                    JArray row = data[i] as JArray;

                    if (row == null || row.Count <= indexSecId)
                    {
                        continue;
                    }

                    string secId = row[indexSecId].ToString();

                    for (int j = 0; j < _securities.Count; j++)
                    {
                        Security security = _securities[j];

                        if (security.SecurityType != SecurityType.Futures
                            || security.Name != secId)
                        {
                            continue;
                        }

                        if (row[indexMargin].Type != JTokenType.Null)
                        {
                            decimal margin = row[indexMargin].ToObject<decimal>();
                            security.MarginBuy = margin;
                            security.MarginSell = margin;
                        }

                        if (row[indexHighLimit].Type != JTokenType.Null)
                        {
                            security.PriceLimitHigh = row[indexHighLimit].ToObject<decimal>();
                        }

                        if (row[indexLowLimit].Type != JTokenType.Null)
                        {
                            security.PriceLimitLow = row[indexLowLimit].ToObject<decimal>();
                        }

                        isUpdated = true;
                    }
                }

                if (isUpdated)
                {
                    SecurityEvent?.Invoke(_securities);
                }
            }
            catch (Exception ex)
            {
                // функция вспомогательная - не пугаем пользователя ошибкой, но след в логе оставляем
                SendLogMessage("MOEX ISS недоступен: гарантийное обеспечение и ценовые лимиты фьючерсов не загружены (на торговлю не влияет). " + ex.Message, LogMessageType.System);
            }
        }

        public event Action<List<Security>> SecurityEvent;

        #endregion

        #region 4 Portfolios

        private List<Portfolio> _myPortfolios = new List<Portfolio>();
        private Dictionary<string, decimal> _securitiesLots = [];

        public void GetPortfolios()
        {
            try
            {
                string path = "/trade-api-bff-portfolio/api/v1/portfolio";

                HttpResponseMessage portfolioResponse = CreateHttpRequestAsync(path, HttpMethod.Get, null).Result;

                if (portfolioResponse.StatusCode == HttpStatusCode.OK)
                {
                    string responseMsg = portfolioResponse.Content.ReadAsStringAsync().Result;
                    List<BcsPortfolio> bcsPortfolios = JsonConvert.DeserializeAnonymousType(responseMsg, new List<BcsPortfolio>());

                    if (bcsPortfolios != null && bcsPortfolios.Count > 0)
                    {
                        UpdateMyPortfolio(bcsPortfolios);
                    }
                }
                else
                {
                    // получаем через вебсокет
                }
            }
            catch (Exception ex)
            {
                SendLogMessage($"Portfolio request error: {ex.Message}", LogMessageType.Error);
            }
        }

        private void UpdateMyPortfolio(List<BcsPortfolio> bcsPortfolios)
        {
            if (_securities.Count == 0)
                return;

            List<string> accounts = GetAllAccounts(bcsPortfolios);

            for (int i = 0; i < accounts.Count; i++)
            {
                Portfolio portfolio = _myPortfolios.Find(p => p.Number == accounts[i]);

                if (portfolio == null)
                {
                    portfolio = new Portfolio();
                    portfolio.Number = accounts[i];

                    _myPortfolios.Add(portfolio);
                }

                List<BcsPortfolio> portfT365term = bcsPortfolios.FindAll(p => p.account == accounts[i]
                    && p.type != "futuresLimit" && p.type != "otcLimit"
                    && (p.type == "futuresHolding" || p.term == "T365"));

                if (portfT365term.Count > 0)
                {
                    // закрытые позиции убираем по составу всегда, независимо от количества:
                    // guard по Count пропускал случай "одна закрылась, другая открылась"
                    if (portfolio.PositionOnBoard != null && portfolio.PositionOnBoard.Count > 0)
                    {
                        for (int k = 0; k < portfolio.PositionOnBoard.Count; k++)
                        {
                            if (portfT365term.Find(p => p.ticker == portfolio.PositionOnBoard[k].SecurityNameCode) == null)
                            {
                                _securitiesLots.Remove(portfolio.PositionOnBoard[k].SecurityNameCode);
                                portfolio.PositionOnBoard.Remove(portfolio.PositionOnBoard[k]);
                                k--;
                            }
                        }
                    }

                    decimal totalBalance = 0;
                    decimal totalBlocked = 0;
                    decimal totalUnrealizedPl = 0;

                    for (int j = 0; j < portfT365term.Count; j++)
                    {
                        decimal quantity = portfT365term[j].quantity.ToDecimal();
                        decimal currPrice = portfT365term[j].currentPrice.ToDecimal();
                        decimal currBlocked = portfT365term[j].locked.ToDecimal();
                        decimal currUnrealizedPl = quantity == 0 ? 0 : portfT365term[j].unrealizedPL.ToDecimal(); // после закрытия позиции фьючерса нереализованная прибыль отображается до клиринга

                        // рублевая оценка из API: у облигаций цена в % от номинала, у валютных позиций цена не в рублях -
                        // quantity * currentPrice для них некорректно
                        decimal posCurrBalance = portfT365term[j].currentValueRub.ToDecimal();

                        if (posCurrBalance == 0 && quantity != 0)
                        {
                            posCurrBalance = quantity * currPrice;
                        }

                        totalBalance += posCurrBalance;

                        // блокировка портфеля - в рублях (как ValueCurrent), не в штуках:
                        // пропорциональная доля рублёвой оценки позиции
                        if (quantity > 0 && currBlocked > 0)
                        {
                            totalBlocked += posCurrBalance * (currBlocked / quantity);
                        }

                        totalUnrealizedPl += currUnrealizedPl;

                        PositionOnBoard posPortf = null;

                        if (portfolio.PositionOnBoard != null)
                        {
                            posPortf = portfolio.PositionOnBoard.Find(p => p.SecurityNameCode == portfT365term[j].ticker);
                        }

                        if (posPortf == null)
                        {
                            posPortf = new PositionOnBoard();
                            posPortf.SecurityNameCode = portfT365term[j].ticker;

                            Security security = _securities.Find(s => s.Name == portfT365term[j].ticker);

                            if (security != null)
                            {
                                _securitiesLots[security.Name] = security.Lot;

                                decimal lot = security.Lot > 0 ? security.Lot : 1;
                                posPortf.ValueCurrent = quantity / lot;
                                posPortf.ValueBegin = quantity / lot;
                                posPortf.ValueBlocked = currBlocked / lot;
                            }
                            else
                            {
                                posPortf.ValueCurrent = quantity;
                                posPortf.ValueBegin = quantity;
                                posPortf.ValueBlocked = currBlocked;
                            }

                            posPortf.PortfolioName = portfT365term[j].account;
                            posPortf.UnrealizedPnl = currUnrealizedPl;


                            portfolio.SetNewPosition(posPortf);
                        }
                        else
                        {

                            if (_securitiesLots.TryGetValue(posPortf.SecurityNameCode, out decimal lot))
                            {
                                decimal safeLot = lot > 0 ? lot : 1;
                                posPortf.ValueCurrent = quantity / safeLot;
                                posPortf.ValueBlocked = currBlocked / safeLot;
                            }
                            else
                            {
                                posPortf.ValueCurrent = quantity;
                                posPortf.ValueBlocked = currBlocked;
                            }

                            posPortf.UnrealizedPnl = currUnrealizedPl;
                        }
                    }

                    if (portfolio.ValueBegin == 0)
                        portfolio.ValueBegin = totalBalance;

                    portfolio.ValueCurrent = totalBalance;
                    portfolio.ValueBlocked = totalBlocked;
                    portfolio.UnrealizedPnl = totalUnrealizedPl;

                }
                else
                {
                    // счёт полностью закрыт: снапшот не содержит T365-позиций -
                    // чистим доску и обнуляем итоги, иначе последние позиции
                    // и старые значения останутся на доске навсегда
                    if (portfolio.PositionOnBoard != null && portfolio.PositionOnBoard.Count > 0)
                    {
                        for (int k = 0; k < portfolio.PositionOnBoard.Count; k++)
                        {
                            _securitiesLots.Remove(portfolio.PositionOnBoard[k].SecurityNameCode);
                        }

                        portfolio.PositionOnBoard.Clear();
                    }

                    portfolio.ValueCurrent = 0;
                    portfolio.ValueBlocked = 0;
                    portfolio.UnrealizedPnl = 0;
                }

                if (_myPortfolios.Count != 0)
                {
                    PortfolioEvent?.Invoke(_myPortfolios);
                }
            }
        }

        private List<string> GetAllAccounts(List<BcsPortfolio> bcsPortfolios)
        {
            List<string> accounts = [];

            for (int i = 0; i < bcsPortfolios.Count; i++)
            {
                if (accounts.Find(a => a == bcsPortfolios[i].account) == null)
                {
                    accounts.Add(bcsPortfolios[i].account);
                }
            }

            return accounts;
        }

        public event Action<List<Portfolio>> PortfolioEvent;

        #endregion

        #region 5 Data

        public List<Candle> GetLastCandleHistory(Security security, TimeFrameBuilder timeFrameBuilder, int candleCount)
        {
            if (candleCount <= 0)
            {
                return null;
            }

            DateTime endTime = DateTime.Now;
            DateTime startTime = endTime.AddTicks(-(timeFrameBuilder.TimeFrameTimeSpan.Ticks * candleCount));

            List<Candle> candles = GetHistoryCandles(security, timeFrameBuilder, startTime, endTime);

            if (candles == null)
            {
                return null;
            }

            while (candles.Count > candleCount)
            {
                candles.RemoveAt(0);
            }

            return candles;
        }

        public List<Candle> GetCandleDataToSecurity(Security security, TimeFrameBuilder timeFrameBuilder, DateTime startTime, DateTime endTime, DateTime actualTime)
        {

            if (!CheckTime(startTime, endTime, actualTime))
            {
                return null;
            }
            DateTime requestedStartTime = startTime;

            List<Candle> candles = GetHistoryCandles(security, timeFrameBuilder, requestedStartTime, endTime);

            if (candles == null)
            {
                return null;
            }

            return candles;
        }

        private readonly RateGate _rateGateCandles = new RateGate(1, TimeSpan.FromMilliseconds(500));

        private List<Candle> GetHistoryCandles(Security security, TimeFrameBuilder timeFrameBuilder, DateTime startTime, DateTime endTime)
        {
            string timeFrame = GetCandleTimeFrame(timeFrameBuilder.TimeFrameTimeSpan);

            if (timeFrame == null)
            {
                return null;
            }

            if (timeFrame == "D" && endTime.Date > DateTime.Now.Date.AddDays(-1))
            {
                // отсекаем незакрытую дневную свечу текущего дня: её TimeStart равен полуночи, поэтому -1 мс
                endTime = new DateTime(DateTime.Now.Year, DateTime.Now.Month, DateTime.Now.Day, 0, 0, 0).AddMilliseconds(-1);
            }

            string classCode = GetClassCode(security);

            if (string.IsNullOrEmpty(classCode))
            {
                SendLogMessage($"BCS candles request error. Empty class code for {security.Name}", LogMessageType.Error);
                return null;
            }

            List<Candle> candles = new List<Candle>();
            TimeSpan requestTimeSpan = TimeSpan.FromTicks(timeFrameBuilder.TimeFrameTimeSpan.Ticks * 1440);
            DateTime requestStartTime = startTime;

            while (requestStartTime < endTime)
            {
                DateTime requestEndTime = requestStartTime.Add(requestTimeSpan);

                if (requestEndTime > endTime)
                {
                    requestEndTime = endTime;
                }

                List<Candle> newCandles = RequestHistoryCandles(security, classCode, timeFrame, requestStartTime, requestEndTime);

                if (newCandles == null)
                {
                    return candles.Count == 0 ? null : candles;
                }

                if (newCandles.Count > 0)
                {
                    if (candles.Count > 0 &&
                        candles[candles.Count - 1].TimeStart == newCandles[0].TimeStart)
                    {
                        newCandles.RemoveAt(0);
                    }

                    candles.AddRange(newCandles);
                }

                if (requestEndTime == endTime)
                {
                    break;
                }

                requestStartTime = requestEndTime;
            }

            for (int i = 0; i < candles.Count; i++)
            {
                if (candles[i].TimeStart < startTime ||
                    candles[i].TimeStart > endTime)
                {
                    candles.RemoveAt(i);
                    i--;
                }
            }

            return candles;
        }

        private List<Candle> RequestHistoryCandles(Security security, string classCode, string timeFrame, DateTime startTime, DateTime endTime)
        {
            try
            {
                _rateGateCandles.WaitToProceed();

                // сервер трактует время в запросе как UTC (суффикс Z), внутреннее время коннектора — московское
                // SpecifyKind: движок присылает Kind=Local, ConvertTimeToUtc требует Unspecified
                DateTime startUnspec = DateTime.SpecifyKind(startTime, DateTimeKind.Unspecified);
                DateTime endUnspec = DateTime.SpecifyKind(endTime, DateTimeKind.Unspecified);

                string startUtc = TimeZoneInfo.ConvertTimeToUtc(startUnspec, _moscowTimeZone).ToString("yyyy-MM-ddTHH:mm:00Z");
                string endUtc = TimeZoneInfo.ConvertTimeToUtc(endUnspec, _moscowTimeZone).ToString("yyyy-MM-ddTHH:mm:00Z");

                string path = "/trade-api-market-data-connector/api/v1/candles-chart?"
                    + "classCode=" + Uri.EscapeDataString(classCode) + "&ticker=" + Uri.EscapeDataString(security.Name)
                    + "&startDate=" + Uri.EscapeDataString(startUtc)
                    + "&endDate=" + Uri.EscapeDataString(endUtc)
                    + "&timeFrame=" + Uri.EscapeDataString(timeFrame);

                // "/trade-api-market-data-connector/api/v1/candles-chart?classCode=TQBR&ticker=ALRS&startDate=2026-07-01T00%3A00%3A00Z&endDate=2026-07-10T00%3A00%3A00Z&timeFrame=H1";

                HttpResponseMessage response = CreateHttpRequestAsync(path, HttpMethod.Get, null).Result;

                if (response == null)
                {
                    return null;
                }

                if (response.StatusCode != HttpStatusCode.OK)
                {
                    string errorMsg = response.Content.ReadAsStringAsync().Result;
                    SendLogMessage($"BCS candles request error. Code: {response.StatusCode} || msg: {errorMsg}", LogMessageType.Error);
                    return null;
                }

                string responseMsg = response.Content.ReadAsStringAsync().Result;
                BcsCandles candleResponse = JsonConvert.DeserializeObject<BcsCandles>(responseMsg);

                if (candleResponse == null ||
                    candleResponse.bars == null)
                {
                    return new List<Candle>();
                }

                List<Candle> converted = ConvertToOsEngineCandles(candleResponse.bars, security);

                return converted;
            }
            catch (Exception ex)
            {
                SendLogMessage($"BCS candles request error: {ex.Message}", LogMessageType.Error);
                return null;
            }
        }

        private List<Candle> ConvertToOsEngineCandles(Bar[] bars, Security security)
        {
            List<Candle> candles = new List<Candle>();

            for (int i = bars.Length - 1; i >= 0; i--)
            {
                Bar bar = bars[i];

                Candle candle = new Candle();

                DateTime startTimeUtc = DateTime.Parse(bar.time, null, DateTimeStyles.RoundtripKind);

                candle.TimeStart = TimeZoneInfo.ConvertTimeFromUtc(startTimeUtc, _moscowTimeZone);
                candle.Open = bar.open.ToDecimal();
                candle.High = Math.Round(bar.high.ToDecimal(), security.Decimals);
                candle.Low = Math.Round(bar.low.ToDecimal(), security.Decimals);
                candle.Close = Math.Round(bar.close.ToDecimal(), security.Decimals);

                // у БКС volume в свечах в рублях (проверено зондом 19.10.2026),
                // в конвенции OsEngine объём - в штуках: пересчёт по цене закрытия
                decimal closePrice = bar.close.ToDecimal();
                candle.Volume = closePrice > 0
                    ? Math.Round(bar.volume.ToDecimal() / closePrice, security.DecimalsVolume)
                    : 0;

                candle.State = CandleState.Finished;
                candles.Add(candle);
            }

            return candles;
        }

        private bool CheckTime(DateTime startTime, DateTime endTime, DateTime actualTime)
        {
            if (startTime >= endTime ||
                startTime >= DateTime.Now ||
                actualTime > endTime ||
                actualTime > DateTime.Now)
            {
                return false;
            }

            return true;
        }

        // classCode из NameClass формата "TYPE-classCode": последний сегмент,
        // единая точка парсинга (раньше в части мест был Split[1] - расходилось при дефисе в classCode)
        private string GetClassCodeByNameClass(string nameClass)
        {
            if (string.IsNullOrEmpty(nameClass))
            {
                return nameClass;
            }

            string[] splitNameClass = nameClass.Split('-');

            return splitNameClass[splitNameClass.Length - 1];
        }

        private string GetClassCode(Security security)
        {
            if (security.NameClass == null)
            {
                return null;
            }

            string[] splitNameClass = security.NameClass.Split('-');

            if (splitNameClass.Length < 2)
            {
                return security.NameClass;
            }

            return splitNameClass[splitNameClass.Length - 1];
        }

        private string GetCandleTimeFrame(TimeSpan timeFrame)
        {
            if (timeFrame.TotalMinutes == 1)
            {
                return "M1";
            }
            else if (timeFrame.TotalMinutes == 5)
            {
                return "M5";
            }
            else if (timeFrame.TotalMinutes == 15)
            {
                return "M15";
            }
            else if (timeFrame.TotalMinutes == 30)
            {
                return "M30";
            }
            else if (timeFrame.TotalHours == 1)
            {
                return "H1";
            }
            else if (timeFrame.TotalHours == 4)
            {
                return "H4";
            }
            else if (timeFrame.TotalDays == 1)
            {
                return "D";
            }

            return null;
        }

        public List<Trade> GetTickDataToSecurity(Security security, DateTime startTime, DateTime endTime, DateTime actualTime)
        {
            return null;
        }

        #endregion

        #region 6 WebSocket creation

        private readonly string _wsHostMarketData = "wss://ws.broker.ru/trade-api-market-data-connector/api/v1/market-data/ws";
        private readonly string _wsHostPortfolio = "wss://ws.broker.ru/trade-api-bff-portfolio/api/v1/portfolio/ws";
        private readonly string _wsHostOrders = "wss://ws.broker.ru/trade-api-bff-operations/api/v1/orders/events/ws";

        private string _socketLocker = "webSocketLockerBcs";

        private void CreatePublicWebSocketConnect()
        {
            try
            {
                if (WebSocketDataMessage == null)
                {
                    WebSocketDataMessage = new ConcurrentQueue<string>();
                }

                // сокет маркет-даты не создаётся до первой подписки (ленивое создание):
                // бюджет 20 соединений не тратится на пустой сокет
            }
            catch (Exception ex)
            {
                SendLogMessage($"{ex.Message} {ex.StackTrace}", LogMessageType.Error);
            }
        }

        private WebSocket CreateNewSocketMarketData()
        {
            try
            {
                WebSocket webSocketMarketData = new WebSocket(_wsHostMarketData);
                webSocketMarketData.SetHeader("Authorization", "Bearer " + _apiAccessToken);

                // EmitOnPing не включаем: heartbeat у БКС на протокольном уровне (WS ping-фреймы),
                // ClientWebSocket отвечает на них сам, текстового ping/pong в API нет (проверено зондом)
                webSocketMarketData.KeepAliveInterval = TimeSpan.FromSeconds(30);
                webSocketMarketData.KeepAliveTimeout = TimeSpan.FromMinutes(2);
                webSocketMarketData.OnOpen += WebSocketData_Opened;
                webSocketMarketData.OnClose += WebSocketData_Closed;
                webSocketMarketData.OnMessage += WebSocketData_MessageReceived;
                webSocketMarketData.OnError += WebSocketData_Error;

                if (_myProxy != null)
                {
                    webSocketMarketData.SetProxy(_myProxy);
                }

                webSocketMarketData.ConnectAsync(TimeSpan.FromSeconds(30), _clientForSocket);

                return webSocketMarketData;
            }
            catch (Exception ex)
            {
                SendLogMessage("Create socket market data error: " + ex.ToString(), LogMessageType.Error);
                return null;
            }
        }

        private void DetachMarketDataSocketEvents(WebSocket webSocketMarketData)
        {
            webSocketMarketData.OnOpen -= WebSocketData_Opened;
            webSocketMarketData.OnClose -= WebSocketData_Closed;
            webSocketMarketData.OnMessage -= WebSocketData_MessageReceived;
            webSocketMarketData.OnError -= WebSocketData_Error;
        }

        private void CreatePrivateWebSocketConnect()
        {
            try
            {
                _socketPortfolioIsActive = false;
                _socketOrdersIsActive = false;

                lock (_socketLocker)
                {
                    WebSocketPortfolioMessage = new ConcurrentQueue<string>();
                    WebSocketMyOrdersAndTradesMessage = new ConcurrentQueue<string>();
                    MyTradesToFetchQueue = new ConcurrentQueue<MyTradeFetchRequest>();

                    _webSocketPortfolio = CreatePortfolioSocket();
                    _webSocketOrders = CreateOrdersSocket();
                }
            }
            catch (Exception ex)
            {
                SendLogMessage("Create private sockets error" + ex.ToString(), LogMessageType.Error);
            }
        }

        private WebSocket CreatePortfolioSocket()
        {
            try
            {
                WebSocket webSocketPortfolio = new WebSocket(_wsHostPortfolio);
                webSocketPortfolio.SetHeader("Authorization", "Bearer " + _apiAccessToken);
                webSocketPortfolio.EmitOnPing = true;
                webSocketPortfolio.KeepAliveInterval = TimeSpan.FromSeconds(30);
                webSocketPortfolio.KeepAliveTimeout = TimeSpan.FromMinutes(2);
                webSocketPortfolio.OnOpen += WebSocketPortfolio_Opened;
                webSocketPortfolio.OnClose += WebSocketPortfolio_Closed;
                webSocketPortfolio.OnMessage += WebSocketPortfolio_MessageReceived;
                webSocketPortfolio.OnError += WebSocketPortfolio_Error;

                if (_myProxy != null)
                {
                    webSocketPortfolio.SetProxy(_myProxy);
                }

                webSocketPortfolio.ConnectAsync(TimeSpan.FromSeconds(30), _clientForSocket);

                return webSocketPortfolio;
            }
            catch (Exception ex)
            {
                SendLogMessage("Create portfolio socket error: " + ex.ToString(), LogMessageType.Error);
                return null;
            }
        }

        private WebSocket CreateOrdersSocket()
        {
            try
            {
                WebSocket webSocketOrders = new WebSocket(_wsHostOrders);
                webSocketOrders.SetHeader("Authorization", "Bearer " + _apiAccessToken);
                webSocketOrders.EmitOnPing = true;
                webSocketOrders.KeepAliveInterval = TimeSpan.FromSeconds(30);
                webSocketOrders.KeepAliveTimeout = TimeSpan.FromMinutes(2);
                webSocketOrders.OnOpen += WebSocketOrders_Opened;
                webSocketOrders.OnClose += WebSocketOrders_Closed;
                webSocketOrders.OnMessage += WebSocketOrders_MessageReceived;
                webSocketOrders.OnError += WebSocketOrders_Error;

                if (_myProxy != null)
                {
                    webSocketOrders.SetProxy(_myProxy);
                }

                webSocketOrders.ConnectAsync(TimeSpan.FromSeconds(30), _clientForSocket);

                return webSocketOrders;
            }
            catch (Exception ex)
            {
                SendLogMessage("Create orders socket error: " + ex.ToString(), LogMessageType.Error);
                return null;
            }
        }

        private void DetachPortfolioSocketEvents(WebSocket webSocketPortfolio)
        {
            webSocketPortfolio.OnOpen -= WebSocketPortfolio_Opened;
            webSocketPortfolio.OnClose -= WebSocketPortfolio_Closed;
            webSocketPortfolio.OnMessage -= WebSocketPortfolio_MessageReceived;
            webSocketPortfolio.OnError -= WebSocketPortfolio_Error;
        }

        private void DetachOrdersSocketEvents(WebSocket webSocketOrders)
        {
            webSocketOrders.OnOpen -= WebSocketOrders_Opened;
            webSocketOrders.OnClose -= WebSocketOrders_Closed;
            webSocketOrders.OnMessage -= WebSocketOrders_MessageReceived;
            webSocketOrders.OnError -= WebSocketOrders_Error;
        }
        private void DeleteWebSocketConnection()
        {
            try
            {
                lock (_socketLocker)
                {
                    if (_webSocketPublicList != null)
                    {
                        List<BcsSocketWrapper> wrappers;

                        lock (_webSocketPublicLocker)
                        {
                            wrappers = new List<BcsSocketWrapper>(_webSocketPublicList);
                            _webSocketPublicList.Clear();
                            _wrapperBySecurityName.Clear();
                        }

                        for (int i = 0; i < wrappers.Count; i++)
                        {
                            WebSocket webSocketPublic = wrappers[i].Socket;

                            if (webSocketPublic == null)
                            {
                                continue;
                            }

                            try
                            {
                                DetachMarketDataSocketEvents(webSocketPublic);

                                if (webSocketPublic.ReadyState == WebSocketState.Open)
                                {
                                    webSocketPublic.CloseAsync();
                                }
                            }
                            catch
                            {
                                // ignore
                            }

                            wrappers[i].Socket = null;
                        }
                    }

                    if (_webSocketPortfolio != null)
                    {
                        try
                        {
                            _webSocketPortfolio.OnOpen -= WebSocketPortfolio_Opened;
                            _webSocketPortfolio.OnClose -= WebSocketPortfolio_Closed;
                            _webSocketPortfolio.OnMessage -= WebSocketPortfolio_MessageReceived;
                            _webSocketPortfolio.OnError -= WebSocketPortfolio_Error;
                            _webSocketPortfolio.CloseAsync();
                        }
                        catch
                        {
                            // ignore
                        }
                    }

                    if (_webSocketOrders != null)
                    {
                        try
                        {
                            _webSocketOrders.OnOpen -= WebSocketOrders_Opened;
                            _webSocketOrders.OnClose -= WebSocketOrders_Closed;
                            _webSocketOrders.OnMessage -= WebSocketOrders_MessageReceived;
                            _webSocketOrders.OnError -= WebSocketOrders_Error;
                            _webSocketOrders.CloseAsync();
                        }
                        catch
                        {
                            // ignore
                        }
                    }
                }
            }
            catch
            {

            }
            finally
            {
                _webSocketPortfolio = null;
                _webSocketOrders = null;
            }
        }

        private void CheckActivationSockets()
        {
            // сокет маркет-даты в активации не участвует: он создаётся лениво
            // первой подпиской и дальше пересоздаётся бесшовно

            if (_socketPortfolioIsActive == false)
            {
                return;
            }

            if (_socketOrdersIsActive == false)
            {
                return;
            }

            try
            {
                lock (_activationLocker)
                {
                    if (ServerStatus != ServerConnectStatus.Connect)
                    {
                        if (_httpClient == null)
                            CreateHttpClient();

                        SendLogMessage("All sockets activated. Connect State", LogMessageType.System);
                        ServerStatus = ServerConnectStatus.Connect;
                        ConnectEvent();
                    }
                }
            }
            catch (Exception ex)
            {
                SendLogMessage(ex.ToString(), LogMessageType.Error);
            }
        }

        #endregion

        #region 7 WebSocket events

        private void WebSocketData_Opened(object sender, EventArgs e)
        {
            SendLogMessage("Socket market data activated", LogMessageType.System);
            CheckActivationSockets();

            // первичное открытие после ленивого создания и бесшовный реконнект
            // проходят один путь: подписки сокета восстанавливаются одним пакетом
            WebSocket openedSocket = sender as WebSocket;
            BcsSocketWrapper wrapper = FindPublicSocketWrapper(openedSocket);

            if (wrapper != null)
            {
                // grace-окно для канареечного детектора: после открытия
                // подписки и первые сообщения придут за несколько секунд
                wrapper.LastMessageTime = DateTime.Now;

                SendWrapperSubscriptions(wrapper, openedSocket);
            }
        }

        private void WebSocketData_Closed(object sender, CloseEventArgs e)
        {
            try
            {
                if (ServerStatus == ServerConnectStatus.Disconnect
                    || _marketDataReconnectAllowed == false)
                {
                    return;
                }

                WebSocket deadSocket = sender as WebSocket;
                BcsSocketWrapper wrapper = FindPublicSocketWrapper(deadSocket);

                if (wrapper == null)
                {
                    // сокет уже заменён бесшовным реконнектом и больше не отслеживается
                    return;
                }

                // только логируем: сокет пересоздаст поток MarketDataWatchdog,
                // коннектор остаётся в Connect
                SendLogMessage($"BCS WebSocket MarketData connection closed (code {e.Code} {e.Reason}). The socket will be reconnected.", LogMessageType.System);

                if (ReferenceEquals(wrapper.Socket, deadSocket))
                {
                    wrapper.Socket = null;
                    wrapper.CanaryActive = false;
                }

                DetachMarketDataSocketEvents(deadSocket);
            }
            catch (Exception ex)
            {
                SendLogMessage(ex.ToString(), LogMessageType.Error);
            }
        }

        private void WebSocketData_Error(object sender, ErrorEventArgs e)
        {
            try
            {
                if (ServerStatus == ServerConnectStatus.Disconnect)
                {
                    return;
                }

                if (e.Exception != null)
                {
                    string message = e.Exception.ToString();

                    if (message.Contains("status code '403'"))
                    {
                        lock (_subscribeLimitLocker)
                        {
                            if (!_hasLimitReached)
                            {
                                _hasLimitReached = true;
                                SendLogMessage("BCS достигнут лимит соединений WebSocket. Новые подписки остановлены.",
                                    LogMessageType.Error);
                            }
                        }
                    }
                    else if (message.Contains("The remote party closed the WebSocket connection"))
                    {
                        // ignore
                    }
                    else
                    {
                        SendLogMessage(e.Exception.ToString(), LogMessageType.Error);
                    }
                }
            }
            catch (Exception ex)
            {
                SendLogMessage("Data socket error" + ex.ToString(), LogMessageType.Error);
            }
        }

        private void WebSocketData_MessageReceived(object sender, MessageEventArgs e)
        {
            try
            {
                if (e == null)
                {
                    return;
                }

                if (string.IsNullOrEmpty(e.Data))
                {
                    return;
                }

                // пер-сокетный heartbeat: канарейка на сокете делает молчание видимым
                BcsSocketWrapper wrapper = FindPublicSocketWrapper(sender as WebSocket);

                if (wrapper != null)
                {
                    wrapper.LastMessageTime = DateTime.Now;
                }

                if (WebSocketDataMessage == null)
                {
                    return;
                }

                if (ServerStatus == ServerConnectStatus.Disconnect)
                {
                    return;
                }

                WebSocketDataMessage.Enqueue(e.Data);
            }
            catch (Exception error)
            {
                SendLogMessage("Market data socket error. " + error.ToString(), LogMessageType.Error);
            }
        }

        // Portfolio socket
        private void WebSocketPortfolio_Opened(object sender, EventArgs e)
        {
            SendLogMessage("Socket Portfolio activated", LogMessageType.System);
            _socketPortfolioIsActive = true;
            CheckActivationSockets();
        }

        private void WebSocketPortfolio_Closed(object sender, CloseEventArgs e)
        {
            try
            {
                if (ServerStatus == ServerConnectStatus.Disconnect
                    || _privateSocketReconnectAllowed == false)
                {
                    return;
                }

                // только логируем: сокет пересоздаст поток CheckAlivePrivateSockets,
                // коннектор остаётся в Connect
                SendLogMessage($"BCS WebSocket Portfolio connection closed (code {e.Code} {e.Reason}). The socket will be reconnected.", LogMessageType.System);
            }
            catch (Exception ex)
            {
                SendLogMessage(ex.ToString(), LogMessageType.Error);
            }
        }

        private void WebSocketPortfolio_Error(object sender, ErrorEventArgs e)
        {
            try
            {
                if (ServerStatus == ServerConnectStatus.Disconnect)
                {
                    return;
                }

                if (e.Exception != null)
                {
                    string message = e.Exception.ToString();

                    if (message.Contains("The remote party closed the WebSocket connection"))
                    {
                        // ignore
                    }
                    else
                    {
                        SendLogMessage(e.Exception.ToString(), LogMessageType.Error);
                    }
                }
            }
            catch (Exception ex)
            {
                SendLogMessage("Portfolio socket error" + ex.ToString(), LogMessageType.Error);
            }
        }

        private void WebSocketPortfolio_MessageReceived(object sender, MessageEventArgs e)
        {
            try
            {
                if (e == null)
                {
                    return;
                }

                if (string.IsNullOrEmpty(e.Data))
                {
                    return;
                }

                _lastPortfolioMessageTime = DateTime.Now;

                if (WebSocketPortfolioMessage == null)
                {
                    return;
                }

                if (ServerStatus == ServerConnectStatus.Disconnect)
                {
                    return;
                }

                WebSocketPortfolioMessage.Enqueue(e.Data);
            }
            catch (Exception error)
            {
                SendLogMessage("Portfolio socket error. " + error.ToString(), LogMessageType.Error);
            }
        }

        // Orders socket
        private void WebSocketOrders_Opened(object sender, EventArgs e)
        {
            SendLogMessage("Socket Orders activated", LogMessageType.System);
            _socketOrdersIsActive = true;
            CheckActivationSockets();
        }

        private void WebSocketOrders_Closed(object sender, CloseEventArgs e)
        {
            try
            {
                if (ServerStatus == ServerConnectStatus.Disconnect
                    || _privateSocketReconnectAllowed == false)
                {
                    return;
                }

                if (_ordersConnectionLostTime == DateTime.MinValue)
                {
                    _ordersConnectionLostTime = DateTime.Now;
                }

                // только логируем: сокет пересоздаст поток CheckAlivePrivateSockets
                // с последующим REST-выравниванием ордеров
                SendLogMessage($"BCS WebSocket Orders connection closed (code {e.Code} {e.Reason}). The socket will be reconnected.", LogMessageType.System);
            }
            catch (Exception ex)
            {
                SendLogMessage(ex.ToString(), LogMessageType.Error);
            }
        }

        private void WebSocketOrders_Error(object sender, ErrorEventArgs e)
        {
            try
            {
                if (ServerStatus == ServerConnectStatus.Disconnect)
                {
                    return;
                }

                if (e.Exception != null)
                {
                    string message = e.Exception.ToString();

                    if (message.Contains("The remote party closed the WebSocket connection"))
                    {
                        // ignore
                    }
                    else
                    {
                        SendLogMessage(e.Exception.ToString(), LogMessageType.Error);
                    }
                }
            }
            catch (Exception ex)
            {
                SendLogMessage("My orders socket error" + ex.ToString(), LogMessageType.Error);
            }
        }

        private void WebSocketOrders_MessageReceived(object sender, MessageEventArgs e)
        {
            try
            {
                if (e == null)
                {
                    return;
                }
                if (string.IsNullOrEmpty(e.Data))
                {
                    return;
                }

                if (WebSocketMyOrdersAndTradesMessage == null)
                {
                    return;
                }

                if (ServerStatus == ServerConnectStatus.Disconnect)
                {
                    return;
                }

                WebSocketMyOrdersAndTradesMessage.Enqueue(e.Data);
            }
            catch (Exception error)
            {
                SendLogMessage("My orders socket error. " + error.ToString(), LogMessageType.Error);
            }
        }

        #endregion

        #region 8 WebSocket Security subscribe

        private RateGate _rateGateSubscribe = new RateGate(1, TimeSpan.FromMilliseconds(220));

        private readonly object _depthSubscribeLocker = new object();
        private readonly Dictionary<string, DateTime> _tradesSubscribeTimes = new Dictionary<string, DateTime>();
        private readonly Dictionary<string, int> _tradesSubscribeAttempts = new Dictionary<string, int>();

        // стакан и лента сделок - две независимые подписки (dataType 0 и 2),
        // подтверждения приходят раздельно и с разной задержкой: следим за обоими каналами
        private readonly Dictionary<string, DateTime> _depthSubscribeTimes = new Dictionary<string, DateTime>();
        private readonly Dictionary<string, int> _depthSubscribeAttempts = new Dictionary<string, int>();

        // тики подтверждаются мгновенно, стаканы сервер обрабатывает пакетно:
        // при массовой подписке OrderBookSuccess идут с задержкой до ~2 минут,
        // первый детект должен быть больше этой очереди
        private readonly TimeSpan _tradesConfirmTimeout = TimeSpan.FromSeconds(90);
        private readonly TimeSpan _depthConfirmFirstTimeout = TimeSpan.FromSeconds(240);

        // а вот ретраи - быстрые: если канал мёртв, ждать 4 минуты между попытками
        // смысла нет (окно деградации depth-регистрации у БКС 19.10 длилось ~25 мин,
        // быстрый ретрай сократил бы видимую деградацию втрое)
        private readonly TimeSpan _depthConfirmRetryTimeout = TimeSpan.FromSeconds(90);
        private readonly HashSet<string> _loggedEmptyDepth = new HashSet<string>();

        // время последнего OrderBookSuccess: пока подтверждения стаканов активно приходят,
        // сервер живо разбирает очередь и ре-подписывать ничего не нужно
        private DateTime _lastDepthConfirmTime = DateTime.MinValue;

        List<Security> _subscribedSecurities = new List<Security>();

        public void Subscribe(Security security)
        {
            try
            {
                if (_hasLimitReached)
                {
                    return;
                }

                for (int i = 0; i < _subscribedSecurities.Count; i++)
                {
                    if (_subscribedSecurities[i].Name == security.Name)
                    {
                        return;
                    }
                }

                _rateGateSubscribe.WaitToProceed();

                _subscribedSecurities.Add(security);

                BcsSocketWrapper wrapper = GetOrCreateWrapperForSubscribe();

                if (wrapper == null)
                {
                    // бюджет из 20 соединений исчерпан, подписка отклонена
                    _subscribedSecurities.Remove(security);
                    return;
                }

                lock (wrapper.Subscriptions)
                {
                    wrapper.Subscriptions.Add(security);
                }

                lock (_webSocketPublicLocker)
                {
                    _wrapperBySecurityName[security.Name] = wrapper;
                }

                if (security.Name == _canaryTicker)
                {
                    // пользователь подписался на инструмент-канарейку: его события идут в движок
                    _canaryIsUserSubscribed = true;
                }

                WebSocket socket = wrapper.Socket;

                if (socket != null
                    && socket.ReadyState == WebSocketState.Open)
                {
                    string depth = GetMarketDepthValue();

                    // канареечная trades-подписка на этом сокете уже даёт ленту по этому инструменту:
                    // дублирующую trades-подписку не шлём, иначе события задвоятся
                    bool tradesAlreadyLive = wrapper.CanaryActive
                        && security.Name == _canaryTicker;

                    if (tradesAlreadyLive == false)
                    {
                        socket.SendAsync($"{{\"subscribeType\": 0,\"dataType\": 2,\"instruments\": [{{\"ticker\": \"{security.Name}\",\"classCode\": \"{GetClassCode(security)}\"}}]}}");

                        lock (_depthSubscribeLocker)
                        {
                            _tradesSubscribeTimes[security.Name] = DateTime.Now;
                            _tradesSubscribeAttempts[security.Name] = 0;
                        }
                    }

                    socket.SendAsync($"{{\"subscribeType\": 0,\"dataType\": 0,\"depth\": {depth},\"instruments\": [{{\"ticker\": \"{security.Name}\",\"classCode\": \"{GetClassCode(security)}\"}}]}}");

                    lock (_depthSubscribeLocker)
                    {
                        _depthSubscribeTimes[security.Name] = DateTime.Now;
                        _depthSubscribeAttempts[security.Name] = 0;
                    }
                }
                // если сокет ещё не открыт (ленивое создание) или мёртв - подписка записана
                // в wrapper и уйдёт одним пакетом из Opened-хука / после бесшовного реконнекта
            }
            catch (Exception exception)
            {
                SendLogMessage($"Subscribe error {security.Name} " + exception.ToString(), LogMessageType.Error);
            }
        }

        // лимиты БКС: 100 подписок в соединении, 20 одновременных соединений маркет-даты.
        // Сервер считает подписки, а не бумаги: каждая бумага = 2 подписки (стакан + лента),
        // реальный потолок 50 инструментов на сокет. 45 - с запасом на канарейку и ре-подписки
        // (проверено боем: на 51 бумаге (102 подписки) сервер шлёт "Превышен лимит")
        private const int _maxUserSubscriptionsPerSocket = 45;
        private const int _maxMarketDataSockets = 20;

        private BcsSocketWrapper GetOrCreateWrapperForSubscribe()
        {
            lock (_webSocketPublicLocker)
            {
                // последний сокет пула со свободным местом
                for (int i = _webSocketPublicList.Count - 1; i >= 0; i--)
                {
                    BcsSocketWrapper wrapper = _webSocketPublicList[i];

                    int count;

                    lock (wrapper.Subscriptions)
                    {
                        count = wrapper.Subscriptions.Count;
                    }

                    if (count < _maxUserSubscriptionsPerSocket)
                    {
                        return wrapper;
                    }
                }

                // все сокеты полные - нужен новый, но бюджет соединений жёсткий
                if (_webSocketPublicList.Count >= _maxMarketDataSockets)
                {
                    lock (_subscribeLimitLocker)
                    {
                        if (!_hasLimitReached)
                        {
                            _hasLimitReached = true;
                            SendLogMessage("BCS исчерпан бюджет из 20 одновременных WebSocket соединений маркет-даты (~900 инструментов по 45 на сокет). Новые подписки остановлены.",
                                LogMessageType.Error);
                        }
                    }

                    return null;
                }

                BcsSocketWrapper newWrapper = new BcsSocketWrapper();
                newWrapper.Socket = CreateNewSocketMarketData();
                newWrapper.CreatedTime = DateTime.Now;
                _webSocketPublicList.Add(newWrapper);

                SendLogMessage($"BCS market data: создан сокет пула #{_webSocketPublicList.Count}. Подписки уйдут пакетом после открытия.", LogMessageType.System);

                return newWrapper;
            }
        }

        private string GetMarketDepthValue()
        {
            // [17] = стандартный ServerParam10 (полный стакан): 10 кастомных параметров + 7 по таблице
            if (((ServerParameterBool)ServerParameters[17]).Value == false)
            {
                return "1";
            }

            // [8] = кастомный параметр глубины стакана
            return ((ServerParameterEnum)ServerParameters[8]).Value;
        }

        // восстанавливает все подписки сокета одним пакетом:
        // вызывается из Opened-хука при первичном открытии и после бесшовного реконнекта
        private void SendWrapperSubscriptions(BcsSocketWrapper wrapper, WebSocket socket)
        {
            try
            {
                if (socket == null
                    || socket.ReadyState != WebSocketState.Open)
                {
                    return;
                }

                List<Security> securities;

                lock (wrapper.Subscriptions)
                {
                    securities = new List<Security>(wrapper.Subscriptions);
                }

                if (securities.Count == 0)
                {
                    return;
                }

                string depth = GetMarketDepthValue();

                // пакеты по 50 инструментов: лимит БКС 8 КБ на сообщение
                for (int start = 0; start < securities.Count; start += 50)
                {
                    // гейт на каждый пакет: лимит 10 сообщений/сек от клиента
                    _rateGateSubscribe.WaitToProceed();

                    List<string> argsList = new List<string>();

                    int end = Math.Min(start + 50, securities.Count);

                    for (int i = start; i < end; i++)
                    {
                        argsList.Add($"{{\"ticker\": \"{securities[i].Name}\",\"classCode\": \"{GetClassCode(securities[i])}\"}}");
                    }

                    string instruments = string.Join(",", argsList);

                    socket.SendAsync($"{{\"subscribeType\": 0,\"dataType\": 2,\"instruments\": [{instruments}]}}");
                    socket.SendAsync($"{{\"subscribeType\": 0,\"dataType\": 0,\"depth\": {depth},\"instruments\": [{instruments}]}}");
                }

                // подтверждения подписок должны прийти заново, следим за обоими каналами
                lock (_depthSubscribeLocker)
                {
                    for (int i = 0; i < securities.Count; i++)
                    {
                        _tradesSubscribeTimes[securities[i].Name] = DateTime.Now;
                        _tradesSubscribeAttempts[securities[i].Name] = 0;
                        _depthSubscribeTimes[securities[i].Name] = DateTime.Now;
                        _depthSubscribeAttempts[securities[i].Name] = 0;
                    }
                }

                SendCanaryTradesFrame(wrapper, socket, securities);

                SendLogMessage($"BCS market data: восстановлено {securities.Count} подписок на сокете пула.", LogMessageType.System);
            }
            catch (Exception ex)
            {
                SendLogMessage(ex.ToString(), LogMessageType.Error);
            }
        }

        // канарейка: trades-подписка на ликвидный эталон отдельным фреймом на каждом сокете.
        // У маркет-даты БКС нет прикладного heartbeat, поэтому молчание сокета иначе не видно.
        // Если пользователь сам подписан на этот инструмент на этом сокете -
        // его подписка и есть канарейка, дубль не нужен
        private void SendCanaryTradesFrame(BcsSocketWrapper wrapper, WebSocket socket, List<Security> userSecurities)
        {
            if (wrapper.CanaryActive)
            {
                return;
            }

            for (int i = 0; i < userSecurities.Count; i++)
            {
                if (userSecurities[i].Name == _canaryTicker)
                {
                    wrapper.CanaryActive = true;
                    return;
                }
            }

            socket.SendAsync($"{{\"subscribeType\": 0,\"dataType\": 2,\"instruments\": [{{\"ticker\": \"{_canaryTicker}\",\"classCode\": \"{_canaryClassCode}\"}}]}}");

            lock (_webSocketPublicLocker)
            {
                _wrapperBySecurityName[_canaryTicker] = wrapper;
            }

            lock (_depthSubscribeLocker)
            {
                _tradesSubscribeTimes[_canaryTicker] = DateTime.Now;
                _tradesSubscribeAttempts[_canaryTicker] = 0;
            }

            wrapper.CanaryActive = true;
        }

        private void CheckSubscriptionsConfirmations()
        {
            while (true)
            {
                Thread.Sleep(10000);

                try
                {
                    if (ServerStatus != ServerConnectStatus.Connect)
                    {
                        continue;
                    }

                    List<string> tradesToResubscribe = new List<string>();
                    List<string> tradesToStop = new List<string>();
                    List<string> depthToResubscribe = new List<string>();
                    List<string> depthToStop = new List<string>();

                    lock (_depthSubscribeLocker)
                    {
                        CollectExpiredConfirmations(_tradesSubscribeTimes, _tradesSubscribeAttempts,
                            _tradesConfirmTimeout, _tradesConfirmTimeout, tradesToResubscribe, tradesToStop);

                        if (_lastDepthConfirmTime.AddSeconds(60) < DateTime.Now)
                        {
                            CollectExpiredConfirmations(_depthSubscribeTimes, _depthSubscribeAttempts,
                                _depthConfirmFirstTimeout, _depthConfirmRetryTimeout, depthToResubscribe, depthToStop);
                        }
                        // иначе сервер прямо сейчас присылает OrderBookSuccess пачками:
                        // очередь разбирается, ре-подписка только добавит нагрузку
                    }

                    for (int i = 0; i < tradesToResubscribe.Count; i++)
                    {
                        ReSubscribeTrades(tradesToResubscribe[i]);
                    }

                    for (int i = 0; i < tradesToStop.Count; i++)
                    {
                        lock (_depthSubscribeLocker)
                        {
                            _tradesSubscribeTimes.Remove(tradesToStop[i]);
                            _tradesSubscribeAttempts.Remove(tradesToStop[i]);
                        }

                        SendLogMessage($"BCS 3 попытки исчерпаны, лента сделок по инструменту не найдена: {tradesToStop[i]}", LogMessageType.System);
                    }

                    for (int i = 0; i < depthToResubscribe.Count; i++)
                    {
                        ReSubscribeDepth(depthToResubscribe[i]);
                    }

                    if (depthToStop.Count > 0)
                    {
                        EscalateDeadDepthChannelToSocketReconnect(depthToStop);
                    }
                }
                catch (Exception ex)
                {
                    SendLogMessage(ex.ToString(), LogMessageType.Error);
                }
            }
        }

        // depth-канал сокета мёртв на сервере: 3 ре-подписки не подтвердились.
        // Бумага тут ни при чём - мёртв канал на конкретном соединении, и лечится
        // только новым соединением (бой 19.10: depth-канал сокета молчал 5+ минут,
        // ре-подписки не подтверждались, после реконнекта подтвердились мгновенно).
        // Канарейка это не ловит: она на trades-канале, а он жив.
        private void EscalateDeadDepthChannelToSocketReconnect(List<string> depthToStop)
        {
            List<BcsSocketWrapper> wrappersToReconnect = new List<BcsSocketWrapper>();

            lock (_webSocketPublicLocker)
            {
                for (int i = 0; i < depthToStop.Count; i++)
                {
                    BcsSocketWrapper wrapper;

                    if (_wrapperBySecurityName.TryGetValue(depthToStop[i], out wrapper)
                        && wrappersToReconnect.Contains(wrapper) == false)
                    {
                        wrappersToReconnect.Add(wrapper);
                    }
                }
            }

            lock (_depthSubscribeLocker)
            {
                for (int i = 0; i < depthToStop.Count; i++)
                {
                    _depthSubscribeTimes.Remove(depthToStop[i]);
                    _depthSubscribeAttempts.Remove(depthToStop[i]);
                }
                // записи сняты: после реконнекта SendWrapperSubscriptions
                // перевзведёт подтверждения всего сокета с нуля
            }

            for (int i = 0; i < wrappersToReconnect.Count; i++)
            {
                if (wrappersToReconnect[i].Socket == null
                    || wrappersToReconnect[i].Socket.ReadyState != WebSocketState.Open)
                {
                    // сокет и так не жив: его пересоздаст обычный watchdog-цикл
                    continue;
                }

                SendLogMessage($"BCS depth-канал сокета не подтверждает подписки после 3 попыток ({depthToStop.Count} инструментов). Реконнект сокета.", LogMessageType.Error);

                // 30-сек троттлинг внутри: массовое срабатывание по многим бумагам
                // того же сокета свернётся в один реконнект
                ReconnectMarketDataSocket(wrappersToReconnect[i]);
            }
        }

        // вызывается под _depthSubscribeLocker
        private void CollectExpiredConfirmations(Dictionary<string, DateTime> times, Dictionary<string, int> attempts,
            TimeSpan firstTimeout, TimeSpan retryTimeout, List<string> toResubscribe, List<string> toStop)
        {
            foreach (KeyValuePair<string, DateTime> pair in times)
            {
                int attemptCount;
                attempts.TryGetValue(pair.Key, out attemptCount);

                // первая проверка терпеливая (серверные очереди), ретраи быстрые
                TimeSpan timeout = attemptCount == 0 ? firstTimeout : retryTimeout;

                if (DateTime.Now - pair.Value > timeout)
                {
                    if (attemptCount < 3)
                    {
                        toResubscribe.Add(pair.Key);
                    }
                    else
                    {
                        toStop.Add(pair.Key);
                    }
                }
            }
        }

        private void ReSubscribeTrades(string securityName)
        {
            if (_hasLimitReached)
            {
                return;
            }

            BcsSocketWrapper wrapper;

            lock (_webSocketPublicLocker)
            {
                if (!_wrapperBySecurityName.TryGetValue(securityName, out wrapper))
                {
                    return;
                }
            }

            WebSocket socket = wrapper.Socket;

            if (socket == null || socket.ReadyState != WebSocketState.Open)
            {
                // сокет мёртв: подписка восстановится пакетом после бесшовного реконнекта
                return;
            }

            string classCode = null;

            lock (wrapper.Subscriptions)
            {
                Security security = wrapper.Subscriptions.Find(s => s.Name == securityName);

                if (security != null)
                {
                    classCode = GetClassCode(security);
                }
            }

            if (classCode == null)
            {
                // канарейка не живёт в Subscriptions - у неё свои тикер и класс
                if (securityName == _canaryTicker)
                {
                    classCode = _canaryClassCode;
                }
                else
                {
                    return;
                }
            }

            _rateGateSubscribe.WaitToProceed();

            string tradesMessage = $"{{\"subscribeType\": 0,\"dataType\": 2,\"instruments\": [{{\"ticker\": \"{securityName}\",\"classCode\": \"{classCode}\"}}]}}";

            SendLogMessage($"BCS re-subscribe trades {securityName}: {tradesMessage}", LogMessageType.System);

            socket.SendAsync(tradesMessage);

            lock (_depthSubscribeLocker)
            {
                _tradesSubscribeTimes[securityName] = DateTime.Now;

                int attempts;
                _tradesSubscribeAttempts.TryGetValue(securityName, out attempts);
                _tradesSubscribeAttempts[securityName] = attempts + 1;
            }
        }

        private void ReSubscribeDepth(string securityName)
        {
            if (_hasLimitReached)
            {
                return;
            }

            BcsSocketWrapper wrapper;

            lock (_webSocketPublicLocker)
            {
                if (!_wrapperBySecurityName.TryGetValue(securityName, out wrapper))
                {
                    return;
                }
            }

            WebSocket socket = wrapper.Socket;

            if (socket == null || socket.ReadyState != WebSocketState.Open)
            {
                // сокет мёртв: подписка восстановится пакетом после бесшовного реконнекта
                return;
            }

            Security security = null;

            lock (wrapper.Subscriptions)
            {
                security = wrapper.Subscriptions.Find(s => s.Name == securityName);
            }

            if (security == null)
            {
                return;
            }

            _rateGateSubscribe.WaitToProceed();

            string depthMessage = $"{{\"subscribeType\": 0,\"dataType\": 0,\"depth\": {GetMarketDepthValue()},\"instruments\": [{{\"ticker\": \"{security.Name}\",\"classCode\": \"{GetClassCode(security)}\"}}]}}";

            SendLogMessage($"BCS re-subscribe depth {security.Name}: {depthMessage}", LogMessageType.System);

            socket.SendAsync(depthMessage);

            lock (_depthSubscribeLocker)
            {
                _depthSubscribeTimes[securityName] = DateTime.Now;

                int attempts;
                _depthSubscribeAttempts.TryGetValue(securityName, out attempts);
                _depthSubscribeAttempts[securityName] = attempts + 1;
            }
        }

        private void UnsubscribeAllSecurities()
        {
            try
            {
                List<BcsSocketWrapper> wrappers;

                lock (_webSocketPublicLocker)
                {
                    wrappers = new List<BcsSocketWrapper>(_webSocketPublicList);
                }

                if (wrappers.Count == 0)
                {
                    return;
                }

                string depth = GetMarketDepthValue();

                for (int i = 0; i < wrappers.Count; i++)
                {
                    WebSocket socket = wrappers[i].Socket;

                    if (socket == null
                        || socket.ReadyState != WebSocketState.Open)
                    {
                        continue;
                    }

                    List<Security> securities;

                    lock (wrappers[i].Subscriptions)
                    {
                        securities = new List<Security>(wrappers[i].Subscriptions);
                    }

                    if (securities.Count == 0)
                    {
                        continue;
                    }

                    // отписка по каждому сокету только от тех инструментов,
                    // которые реально на нём подписаны; пакеты по 50 - лимит 8 КБ на сообщение
                    for (int start = 0; start < securities.Count; start += 50)
                    {
                        _rateGateSubscribe.WaitToProceed();

                        List<string> argsList = new List<string>();

                        int end = Math.Min(start + 50, securities.Count);

                        for (int j = start; j < end; j++)
                        {
                            argsList.Add($"{{\"ticker\": \"{securities[j].Name}\",\"classCode\": \"{GetClassCode(securities[j])}\"}}");
                        }

                        string instruments = string.Join(",", argsList);

                        socket.SendAsync($"{{\"subscribeType\": 1,\"dataType\": 0,\"depth\": {depth},\"instruments\":[{instruments}]}}");
                        socket.SendAsync($"{{\"subscribeType\": 1,\"dataType\": 2,\"instruments\":[{instruments}]}}");
                    }
                }
            }
            catch (Exception exception)
            {
                SendLogMessage($"Unsubscribe error: " + exception.ToString(), LogMessageType.Error);
            }
        }

        public bool SubscribeNews()
        {
            return false;
        }

        public event Action<News> NewsEvent { add { } remove { } }

        #endregion

        #region 9 WebSocket parsing the messages

        private ConcurrentQueue<string> WebSocketDataMessage = new ConcurrentQueue<string>();
        private ConcurrentQueue<string> WebSocketPortfolioMessage = new ConcurrentQueue<string>();
        private ConcurrentQueue<string> WebSocketMyOrdersAndTradesMessage = new ConcurrentQueue<string>();
        private string _subscribeLimitLocker = "limitLocker";
        private bool _hasLimitReached = false;

        private ConcurrentQueue<MyTradeFetchRequest> MyTradesToFetchQueue = new ConcurrentQueue<MyTradeFetchRequest>();
        private RateGate _rateGateMyTrades = new RateGate(1, TimeSpan.FromMilliseconds(100));
        private List<string> _sentMyTradesNumbers = new List<string>();
        private string _sentMyTradesLocker = "sentMyTradesLocker";
        private readonly string _tradesSearchPath = "/trade-api-bff-trade-details/api/v1/trades/search";

        private void DataMessageReader()
        {
            Thread.Sleep(1000);

            while (true)
            {
                try
                {
                    if (WebSocketDataMessage.IsEmpty)
                    {
                        Thread.Sleep(1);
                        continue;
                    }

                    string message;

                    WebSocketDataMessage.TryDequeue(out message);

                    if (message == null)
                    {
                        continue;
                    }

                    if (message.StartsWith("{\"dis"))
                    {
                        WarningSocketMessage warning = JsonConvert.DeserializeAnonymousType(message, new WarningSocketMessage());

                        if (warning != null)
                        {
                            if (warning.displayOptions.text.StartsWith("Превышен лимит"))
                            {
                                lock (_subscribeLimitLocker)
                                {
                                    if (!_hasLimitReached)
                                    {
                                        _hasLimitReached = true;
                                        SendLogMessage($"Внимание! {warning.displayOptions.text}.\n Подписано инструментов: {_subscribedSecurities.Count}", LogMessageType.Error);
                                    }
                                }
                            }
                            else
                            {
                                SendLogMessage($"BCS market data warning: {warning.displayOptions.text}", LogMessageType.System);
                            }

                            Thread.Sleep(500);
                            continue;
                        }
                    }

                    if (message.StartsWith("{\"err"))
                    {
                        SendLogMessage($"Ошибка чтения рыночных данных! {message}", LogMessageType.Error);
                        Thread.Sleep(2000);
                        continue;
                    }

                    PublicMarketDataResponse response = JsonConvert.DeserializeAnonymousType(message, new PublicMarketDataResponse());

                    if (response.responseType != null)
                    {
                        if (response.responseType.Equals("LastTrades"))
                        {
                            if (response.errors != null && response.errors.Count > 0)
                            {
                                for (int i = 0; i < response.errors.Count; i++)
                                {
                                    Error error = response.errors[i];
                                    SendLogMessage($"Ошибка: {error.message} (Код: {error.code})", LogMessageType.Error);

                                    if (error.code == "NOT_FOUND")
                                    {
                                        RemoveSubscriptionAfterNotFound(response.ticker);
                                    }
                                }
                            }
                            else
                            {
                                UpdateTrade(response);
                            }
                        }
                        if (response.responseType.Equals("OrderBook"))
                        {
                            if (response.errors != null && response.errors.Count > 0)
                            {
                                for (int j = 0; j < response.errors.Count; j++)
                                {
                                    Error error = response.errors[j];
                                    SendLogMessage($"Ошибка: {error.message} (Код: {error.code})", LogMessageType.Error);

                                    if (error.code == "NOT_FOUND")
                                    {
                                        RemoveSubscriptionAfterNotFound(response.ticker);
                                    }
                                }
                            }
                            else
                            {
                                if (response.bids != null && response.asks != null
                                    && response.bids.Count == 0 && response.asks.Count == 0)
                                {
                                    lock (_depthSubscribeLocker)
                                    {
                                        if (_loggedEmptyDepth.Add(response.ticker))
                                        {
                                            SendLogMessage($"BCS стакан пустой: {response.ticker} (инструмент не торгуется или требует тест/квалификацию)", LogMessageType.System);
                                        }
                                    }
                                }

                                UpdateMarketDepth(response);
                            }
                        }
                        if (response.responseType.Equals("OrderBookSuccess"))
                        {
                            lock (_depthSubscribeLocker)
                            {
                                _depthSubscribeTimes.Remove(response.ticker);
                                _depthSubscribeAttempts.Remove(response.ticker);
                                _lastDepthConfirmTime = DateTime.Now;
                            }

                            SendLogMessage($"BCS market data: подписка подтверждена {response.responseType} {response.ticker} {response.classCode}", LogMessageType.System);
                        }
                        else if (response.responseType.Equals("LastTradesSuccess"))
                        {
                            lock (_depthSubscribeLocker)
                            {
                                _tradesSubscribeTimes.Remove(response.ticker);
                                _tradesSubscribeAttempts.Remove(response.ticker);
                            }

                            SendLogMessage($"BCS market data: подписка подтверждена {response.responseType} {response.ticker} {response.classCode}", LogMessageType.System);
                        }
                    }
                    else
                    {
                        // диагностика: сообщение не распознано как данные — проверяем ошибку подписки
                        ErrorSubscribeSocket errorSocket = JsonConvert.DeserializeAnonymousType(message, new ErrorSubscribeSocket());

                        if (errorSocket != null && errorSocket.errors != null)
                        {
                            for (int k = 0; k < errorSocket.errors.Length; k++)
                            {
                                Error error = errorSocket.errors[k];
                                SendLogMessage($"BCS ошибка подписки: {error.message} (Код: {error.code})", LogMessageType.Error);
                            }
                        }
                        else
                        {
                            SendLogMessage($"BCS неизвестное сообщение market data: {message}", LogMessageType.System);
                        }
                    }
                }
                catch (Exception ex)
                {
                    SendLogMessage(ex.ToString(), LogMessageType.Error);
                    Thread.Sleep(5000);
                }
            }
        }

        private Dictionary<string, DateTime> _lastTradeTime = new Dictionary<string, DateTime>();

        // инструмент не существует или недоступен аккаунту: re-подписки бессмысленны,
        // а 3 попытки confirmation-вотчдога уронили бы эскалацией здоровый сокет
        // с десятками живых подписок. Снимаем бумагу со всех учётов подписок
        private void RemoveSubscriptionAfterNotFound(string ticker)
        {
            if (string.IsNullOrEmpty(ticker))
            {
                return;
            }

            lock (_depthSubscribeLocker)
            {
                _tradesSubscribeTimes.Remove(ticker);
                _tradesSubscribeAttempts.Remove(ticker);
                _depthSubscribeTimes.Remove(ticker);
                _depthSubscribeAttempts.Remove(ticker);
            }

            lock (_webSocketPublicLocker)
            {
                _wrapperBySecurityName.Remove(ticker);

                for (int i = 0; i < _webSocketPublicList.Count; i++)
                {
                    lock (_webSocketPublicList[i].Subscriptions)
                    {
                        _webSocketPublicList[i].Subscriptions.RemoveAll(s => s.Name == ticker);
                    }
                }
            }

            SendLogMessage($"BCS инструмент недоступен (NOT_FOUND), подписка на него снята: {ticker}", LogMessageType.System);
        }

        private void UpdateTrade(PublicMarketDataResponse tradeData)
        {
            // тики канарейки не уходят в движок, если пользователь сам на неё не подписан
            if (_canaryIsUserSubscribed == false
                && tradeData.ticker == _canaryTicker)
            {
                return;
            }

            Trade trade = new Trade();
            trade.SecurityNameCode = tradeData.ticker;
            trade.Time = ConvertUtsStringToDateTimeRu(tradeData.dateTime);

            if (_ignoreMorningAuctionTrades)
            {
                // утренний аукцион: акции отсекаем до 7:00 мск, фьючерсы и опционы - до 9:00 мск
                Security security = GetSecurityByName(tradeData.ticker, tradeData.classCode);

                int auctionHour = security != null
                    && (security.SecurityType == SecurityType.Futures || security.SecurityType == SecurityType.Option)
                    ? 9 : 7;

                if (trade.Time.Hour < auctionHour)
                {
                    return;
                }
            }

            // биржа может прислать тики с перевёрнутым порядком меток, время делаем монотонным по бумаге
            if (_lastTradeTime.TryGetValue(trade.SecurityNameCode, out DateTime lastTime)
                && trade.Time <= lastTime)
            {
                trade.Time = lastTime.AddTicks(1);
            }

            _lastTradeTime[trade.SecurityNameCode] = trade.Time;

            trade.Price = tradeData.price.ToDecimal();
            trade.Side = tradeData.side == "BUY" ? Side.Buy : Side.Sell;
            trade.Volume = tradeData.quantity.ToDecimal();

            trade.Id = trade.Time.Ticks.ToString();

            NewTradesEvent?.Invoke(trade);
        }

        private void UpdateMarketDepth(PublicMarketDataResponse depthData)
        {
            if (depthData.bids == null ||
                depthData.asks == null)
            {
                return;
            }

            if (depthData.bids.Count == 0 &&
                depthData.asks.Count == 0)
            {
                return;
            }

            MarketDepth depth = new MarketDepth();
            depth.SecurityNameCode = depthData.ticker;

            depth.Time = ConvertUtsStringToDateTimeRu(depthData.dateTime);

            for (int i = 0; i < depthData.bids.Count; i++)
            {
                MarketDepthLevel newBid = new MarketDepthLevel();
                newBid.Price = depthData.bids[i].price.ToDouble();
                newBid.Bid = depthData.bids[i].quantity.ToDouble();
                depth.Bids.Add(newBid);
            }

            for (int i = 0; i < depthData.asks.Count; i++)
            {
                MarketDepthLevel newAsk = new MarketDepthLevel();
                newAsk.Price = depthData.asks[i].price.ToDouble();
                newAsk.Ask = depthData.asks[i].quantity.ToDouble();
                depth.Asks.Add(newAsk);
            }

            if (_lastMdTime != DateTime.MinValue &&
                _lastMdTime >= depth.Time)
            {
                depth.Time = _lastMdTime.AddTicks(1);
            }

            _lastMdTime = depth.Time;

            MarketDepthEvent?.Invoke(depth);
        }

        private DateTime _lastMdTime = DateTime.MinValue;

        public event Action<Trade> NewTradesEvent;

        public event Action<MarketDepth> MarketDepthEvent;

        private void PortfolioMessageReader()
        {
            Thread.Sleep(1000);

            while (true)
            {
                try
                {
                    if (WebSocketPortfolioMessage.IsEmpty)
                    {
                        Thread.Sleep(1);
                        continue;
                    }

                    string message;

                    WebSocketPortfolioMessage.TryDequeue(out message);

                    if (message == null)
                    {
                        continue;
                    }

                    List<BcsPortfolio> bcsPortfolios = JsonConvert.DeserializeAnonymousType(message, new List<BcsPortfolio>());

                    if (bcsPortfolios != null && bcsPortfolios.Count > 0)
                    {
                        UpdateMyPortfolio(bcsPortfolios);
                    }
                }
                catch (Exception exception)
                {
                    SendLogMessage(exception.ToString(), LogMessageType.Error);
                    Thread.Sleep(5000);
                }
            }
        }

        private void OrderStateMessageReader()
        {
            Thread.Sleep(1000);

            while (true)
            {
                try
                {
                    if (WebSocketMyOrdersAndTradesMessage.IsEmpty)
                    {
                        Thread.Sleep(1);
                        continue;
                    }

                    string message;

                    WebSocketMyOrdersAndTradesMessage.TryDequeue(out message);

                    if (message == null)
                    {
                        continue;
                    }

                    BcsOrdersResponse orderResponse = JsonConvert.DeserializeAnonymousType(message, new BcsOrdersResponse());

                    if (orderResponse != null && orderResponse.data != null)
                    {
                        // строковые коды от биржи могут приходить с пробелами (исторический баг " 1")
                        orderResponse.data.messageType = orderResponse.data.messageType?.Trim();
                        orderResponse.data.orderStatus = orderResponse.data.orderStatus?.Trim();
                        orderResponse.data.executionType = orderResponse.data.executionType?.Trim();
                        orderResponse.data.side = orderResponse.data.side?.Trim();
                        orderResponse.data.orderType = orderResponse.data.orderType?.Trim();

                        if (orderResponse.data.messageType == "9")
                        {
                            SendLogMessage("Приостановка размещения ордера:\n" + orderResponse.data.rejectReason, LogMessageType.Error);
                            continue;
                        }

                        if (orderResponse.data.orderStatus == "5") // изменение ордера
                        {
                            //на бирже номер изменился, фиксируем последовательно в список
                            string oldNumber = orderResponse.data.orderNumber;
                            string newNumber = orderResponse.data.orderId.Split('-')[2];

                            if (_changedOrderNumsMarket.Count > 0)
                            {
                                // Проверяем менялся ли уже этот ордер
                                bool hasOrderChanged = false;

                                for (int i = 0; i < _changedOrderNumsMarket.Count; i++)
                                {
                                    List<string> nums = _changedOrderNumsMarket[i];

                                    if (nums[^1] == oldNumber)
                                    {
                                        nums.Add(newNumber);
                                        hasOrderChanged = true;
                                        break;
                                    }
                                }

                                if (!hasOrderChanged)
                                {
                                    _changedOrderNumsMarket.Add([oldNumber, newNumber]);
                                }
                            }
                            else
                            {
                                _changedOrderNumsMarket.Add([oldNumber, newNumber]);
                            }

                            continue;
                        }

                        if (orderResponse.data.orderStatus == "6" || orderResponse.data.orderStatus == "9") //  в процессе отмены или замены
                        {
                            continue;
                        }

                        UpdateMyOrder(orderResponse);
                    }
                }
                catch (Exception exception)
                {
                    SendLogMessage(exception.ToString(), LogMessageType.Error);
                    Thread.Sleep(5000);
                }
            }
        }

        private void UpdateMyOrder(BcsOrdersResponse orderEvent, bool forceEvent = false)
        {
            try
            {
                if (orderEvent.data.orderStatus == "6" || orderEvent.data.orderStatus == "9" || orderEvent.data.orderStatus == "5")
                {
                    return;
                }

                OrderStateType stateType = GetOrderState(orderEvent.data.orderStatus);

                bool isMyOrder = false;
                int orderUserNumber = 0;

                lock (_orderNumbersLocker)
                {
                    // сначала исходный id ордера (стабилен от выставления до конца жизни),
                    // затем id текущей операции (отмена/изменение присылают новый clientOrderId)
                    if (Guid.TryParse(orderEvent.originalClientOrderId, out Guid originalId)
                        && _numberByGuidOrders.TryGetValue(originalId, out orderUserNumber))
                    {
                        isMyOrder = true;
                    }
                    else if (Guid.TryParse(orderEvent.clientOrderId, out Guid currentId)
                        && _numberByGuidOrders.TryGetValue(currentId, out orderUserNumber))
                    {
                        isMyOrder = true;
                    }
                }

                if (stateType == OrderStateType.Fail && isMyOrder)
                {
                    SendLogMessage("Ордер отклонён!\n" + orderEvent.data.rejectReason, LogMessageType.Error);
                }

                if (stateType == OrderStateType.Active && orderEvent.data.orderType.Equals("1")) // игнор размещения маркет ордера
                {
                    return;
                }

                Order newOrder = new Order();

                Security security = GetSecurityByName(orderEvent.data.ticker, orderEvent.data.classCode);

                if (security != null)
                {
                    // lot=0 недопустим: без защиты здесь было DivideByZero на первом событии
                    decimal lot = security.Lot > 0 ? security.Lot : 1;

                    newOrder.SecurityNameCode = security.Name;
                    newOrder.SecurityClassCode = security.NameClass;
                    newOrder.Volume = orderEvent.data.orderQuantity.ToDecimal() / lot;

                    // исполненный объём обязателен: ChangeOrderPrice считает остаток как Volume-VolumeExecute
                    newOrder.VolumeExecute = orderEvent.data.executedQuantity.ToDecimal() / lot;
                }
                else
                {
                    newOrder.SecurityNameCode = orderEvent.data.ticker;
                    newOrder.SecurityClassCode = orderEvent.data.classCode;
                    newOrder.Volume = orderEvent.data.orderQuantity.ToDecimal();
                    newOrder.VolumeExecute = orderEvent.data.executedQuantity.ToDecimal();
                }

                newOrder.TimeCallBack = ConvertUtsStringToDateTimeRu(orderEvent.data.transactionTime);

                if (stateType == OrderStateType.Done)
                {
                    newOrder.TimeDone = ConvertUtsStringToDateTimeRu(orderEvent.data.transactionTime);
                }
                else if (stateType == OrderStateType.Active)
                {
                    newOrder.TimeCreate = ConvertUtsStringToDateTimeRu(orderEvent.data.transactionTime);
                }
                else if (stateType == OrderStateType.Cancel)
                {
                    newOrder.TimeCancel = ConvertUtsStringToDateTimeRu(orderEvent.data.transactionTime);
                }

                if (isMyOrder)
                {
                    newOrder.NumberUser = orderUserNumber;

                    AddOrderIdAndUserNum(orderEvent.data.orderId, orderUserNumber);
                }

                newOrder.NumberMarket = orderEvent.data.orderNumber;

                if (stateType == OrderStateType.Done || stateType == OrderStateType.Partial || stateType == OrderStateType.Cancel)
                {
                    // если ордер менялся отчет придет с новым номером ордера, а надо указать первоначальный NumberMarket чтобы в системе обновился статус позиции

                    if (_changedOrderNumsMarket.Count > 0)
                    {
                        for (int i = 0; i < _changedOrderNumsMarket.Count; i++)
                        {
                            List<string> nums = _changedOrderNumsMarket[i];

                            if (nums[^1] == orderEvent.data.orderNumber)
                            {
                                // нашли список с номерами ордера, которому меняли цену
                                newOrder.NumberMarket = nums[0];

                                // последний номер сохраняем: сделки в trades/search живут под ним,
                                // а цепочку ниже удалим
                                _latestChangedOrderNumber[nums[0]] = nums[^1];

                                _changedOrderNumsMarket.Remove(nums);

                                break;
                            }
                        }
                    }
                }

                newOrder.Side = orderEvent.data.side.Equals("1") ? Side.Buy : Side.Sell;
                newOrder.State = stateType;
                newOrder.TypeOrder = orderEvent.data.orderType.Equals("1") ? OrderPriceType.Market : OrderPriceType.Limit;
                newOrder.Price = newOrder.TypeOrder == OrderPriceType.Limit ? orderEvent.data.price.ToDecimal() : orderEvent.data.averagePrice.ToDecimal();
                newOrder.ServerType = ServerType.BCS;

                if (_myPortfolios.Count == 1)
                {
                    newOrder.PortfolioNumber = _myPortfolios[0].Number;
                }

                string orderString = JsonConvert.SerializeObject(newOrder);

                if (forceEvent)
                {
                    // GetOrderStatus - путь восстановления: хаб ждёт событие, дедуп не должен его съесть.
                    // Отпечаток регистрируем, чтобы последующий дубль из сокета не прошёл
                    IsMyOrderEventDuplicate(newOrder);
                    MyOrderEvent?.Invoke(newOrder);
                }
                else if (IsMyOrderEventDuplicate(newOrder) == false)
                {
                    MyOrderEvent?.Invoke(newOrder);
                }

                if (orderEvent.data.executionType == "11") // сделка
                {
                    TryCreateMyTradeFromOrderEvent(orderEvent, newOrder);
                }
            }
            catch (Exception ex)
            {
                SendLogMessage($" Update my order error: {ex.Message} {ex.StackTrace}", LogMessageType.Error);
            }
        }

        private void TryCreateMyTradeFromOrderEvent(BcsOrdersResponse orderEvent, Order order)
        {
            // В событии сделки нет точной цены (averagePrice - средняя по заявке,
            // при многослойном исполнении она врёт для конкретной сделки).
            // Поэтому MyTrade всегда добираем через trades/search:
            // точная цена + единый дедуп по tradeNum вместо двух пространств имён
            EnqueueMyTradeFetch(orderEvent, order);
        }

        private readonly HashSet<string> _pendingMyTradeFetchOrderNumbers = new HashSet<string>();

        private readonly string _pendingMyTradeFetchLocker = "bcsPendingMyTradeFetchLocker";

        private void EnqueueMyTradeFetch(BcsOrdersResponse orderEvent, Order order, bool force = false)
        {
            try
            {
                lock (_pendingMyTradeFetchLocker)
                {
                    // серия частичных исполнений по ордеру даёт серию событий - один фетч заберёт все сделки сразу
                    // force-запрос (восстановление через GetOrderStatus) ставим всегда: обычный фетч мог уже
                    // пометить tradeNum как отправленный, пока хаб был заглушен
                    if (force == false
                        && _pendingMyTradeFetchOrderNumbers.Add(orderEvent.data.orderNumber) == false)
                    {
                        return;
                    }
                }

                MyTradeFetchRequest request = new MyTradeFetchRequest();
                request.Ticker = orderEvent.data.ticker;
                request.ClassCode = orderEvent.data.classCode;
                request.Side = orderEvent.data.side;
                request.OrderNumber = orderEvent.data.orderNumber;
                request.NumberOrderParent = string.IsNullOrEmpty(order.NumberMarket)
                    ? orderEvent.data.orderNumber
                    : order.NumberMarket;
                request.Force = force;

                if (MyTradesToFetchQueue != null)
                {
                    MyTradesToFetchQueue.Enqueue(request);
                }
            }
            catch (Exception ex)
            {
                SendLogMessage($"Enqueue my trades fetch error: {ex.Message} {ex.StackTrace}", LogMessageType.Error);
            }
        }

        // перегрузка для REST-выравнивания после реконнекта ордерного сокета:
        // добирает сделки по ордерам, исполненным за окно обрыва
        private void EnqueueMyTradeFetch(Order order)
        {
            try
            {
                if (string.IsNullOrEmpty(order.NumberMarket))
                {
                    return;
                }

                lock (_pendingMyTradeFetchLocker)
                {
                    if (_pendingMyTradeFetchOrderNumbers.Add(order.NumberMarket) == false)
                    {
                        return;
                    }
                }

                string classCode = GetClassCodeByNameClass(order.SecurityClassCode);

                MyTradeFetchRequest request = new MyTradeFetchRequest();
                request.Ticker = order.SecurityNameCode;
                request.ClassCode = classCode;
                request.Side = order.Side == Side.Buy ? "1" : "2";
                // сделки ищем по последнему биржевому номеру, родителем остаётся первоначальный
                request.OrderNumber = GetLatestChangedOrderNumber(order.NumberMarket);
                request.NumberOrderParent = order.NumberMarket;

                if (MyTradesToFetchQueue != null)
                {
                    MyTradesToFetchQueue.Enqueue(request);
                }
            }
            catch (Exception ex)
            {
                SendLogMessage($"Enqueue my trades fetch (order) error: {ex.Message} {ex.StackTrace}", LogMessageType.Error);
            }
        }

        private void MyTradesMessageReader()
        {
            Thread.Sleep(1000);

            while (true)
            {
                try
                {
                    if (MyTradesToFetchQueue == null || MyTradesToFetchQueue.IsEmpty)
                    {
                        Thread.Sleep(1);
                        continue;
                    }

                    MyTradeFetchRequest request;

                    if (MyTradesToFetchQueue.TryDequeue(out request) == false)
                    {
                        continue;
                    }

                    lock (_pendingMyTradeFetchLocker)
                    {
                        _pendingMyTradeFetchOrderNumbers.Remove(request.OrderNumber);
                    }

                    FetchMyTrades(request);
                }
                catch (Exception ex)
                {
                    SendLogMessage("My trades reader error: " + ex.ToString(), LogMessageType.Error);
                    Thread.Sleep(5000);
                }
            }
        }

        private void FetchMyTrades(MyTradeFetchRequest request)
        {
            try
            {
                int page = -1;
                int totalPages = 0;

                do
                {
                    _rateGateMyTrades.WaitToProceed();

                    page++;

                    string path = _tradesSearchPath + $"?page={page}&size=100&sort=tradeDateTime,desc";

                    // окно от начала дня: этот фетч - запасной путь, сделка могла случиться задолго до него
                    // (задержка очереди, потеря событий, восстановление через GetOrderStatus)
                    Dictionary<string, dynamic> jsonContent = new Dictionary<string, dynamic>
                    {
                        { "tickers", new string[] { request.Ticker } },
                        { "classCodes", new string[] { request.ClassCode } },
                        { "side", request.Side },
                        // минус сутки: ночная сессия 00:00-03:00 МСК попадает в предыдущие UTC-сутки
                        { "startDateTime", DateTime.UtcNow.Date.AddDays(-1).ToString("yyyy-MM-ddTHH:mm:ss.fffZ") },
                        { "endDateTime", DateTime.UtcNow.AddMinutes(1).ToString("yyyy-MM-ddTHH:mm:ss.fffZ") }
                    };

                    string jsonRequest = JsonConvert.SerializeObject(jsonContent);

                    HttpResponseMessage response = CreateHttpRequestAsync(path, HttpMethod.Post, jsonRequest).Result;

                    if (response == null || response.StatusCode != HttpStatusCode.OK)
                    {
                        return;
                    }

                    string responseMsg = response.Content.ReadAsStringAsync().Result;
                    BcsTradesListResponse tradesResponse = JsonConvert.DeserializeAnonymousType(responseMsg, new BcsTradesListResponse());

                    if (tradesResponse == null || tradesResponse.records == null || tradesResponse.records.Length == 0)
                    {
                        break;
                    }

                    if (int.TryParse(tradesResponse.totalPages, out int pages))
                    {
                        totalPages = pages;
                    }

                    for (int i = 0; i < tradesResponse.records.Length; i++)
                    {
                        BcsTrade tradeRecord = tradesResponse.records[i];

                        if (string.IsNullOrEmpty(tradeRecord.orderNum) || tradeRecord.orderNum != request.OrderNumber)
                        {
                            continue;
                        }

                        string numberTrade = tradeRecord.tradeNum;

                        if (string.IsNullOrEmpty(numberTrade))
                        {
                            continue;
                        }

                        if (request.Force == false
                            && IsMyTradeAlreadySent(numberTrade))
                        {
                            continue;
                        }

                        MyTrade trade = new MyTrade();
                        trade.SecurityNameCode = tradeRecord.ticker;
                        trade.Price = tradeRecord.price.ToDecimal();
                        trade.Volume = tradeRecord.tradeQuantityLots.ToDecimal();
                        trade.NumberOrderParent = request.NumberOrderParent;
                        trade.NumberTrade = numberTrade;
                        trade.Time = ConvertUtsStringToDateTimeRu(tradeRecord.tradeDateTime);
                        trade.Side = tradeRecord.side == "1" ? Side.Buy : Side.Sell;

                        MarkMyTradeSent(numberTrade);

                        MyTradeEvent?.Invoke(trade);
                    }

                } while (page + 1 < totalPages);
            }
            catch (Exception ex)
            {
                SendLogMessage("Fetch my trades error: " + ex.ToString(), LogMessageType.Error);
            }
        }

        // после смены цены ордер живёт на бирже под новым номером, сделки в trades/search -
        // под ним же. NumberMarket в движке при этом первоначальный: фильтр по нему
        // отсекал бы все сделки изменённого ордера
        private string GetLatestChangedOrderNumber(string originalNumber)
        {
            if (_latestChangedOrderNumber.TryGetValue(originalNumber, out string latest))
            {
                return latest;
            }

            for (int i = 0; i < _changedOrderNumsMarket.Count; i++)
            {
                if (_changedOrderNumsMarket[i][0] == originalNumber)
                {
                    return _changedOrderNumsMarket[i][^1];
                }
            }

            return originalNumber;
        }

        private bool IsMyTradeAlreadySent(string numberTrade)
        {
            lock (_sentMyTradesLocker)
            {
                return _sentMyTradesNumbers.Contains(numberTrade);
            }
        }

        private void MarkMyTradeSent(string numberTrade)
        {
            lock (_sentMyTradesLocker)
            {
                if (_sentMyTradesNumbers.Contains(numberTrade))
                {
                    return;
                }

                _sentMyTradesNumbers.Add(numberTrade);

                while (_sentMyTradesNumbers.Count > 1000)
                {
                    _sentMyTradesNumbers.RemoveAt(0);
                }
            }
        }

        // дедуп событий ордеров по биржевому отпечатку NumberMarket+State+TimeCallBack:
        // выравнивание по REST после бесшовного реконнекта не должно повторять
        // события, уже доставленные сокетом до обрыва
        private List<string> _sentMyOrderEvents = new List<string>();
        private string _sentMyOrderEventsLocker = "sentMyOrderEventsLockerBcs";

        private bool IsMyOrderEventDuplicate(Order order)
        {
            string key = order.NumberMarket + "|" + order.State + "|" + order.TimeCallBack.Ticks;

            lock (_sentMyOrderEventsLocker)
            {
                if (_sentMyOrderEvents.Contains(key))
                {
                    return true;
                }

                _sentMyOrderEvents.Add(key);

                while (_sentMyOrderEvents.Count > 1000)
                {
                    _sentMyOrderEvents.RemoveAt(0);
                }

                return false;
            }
        }

        private class MyTradeFetchRequest
        {
            public string Ticker;
            public string ClassCode;
            public string Side;
            public string OrderNumber;
            public string NumberOrderParent;
            // true только из GetOrderStatus: хаб ждёт MyTrade, дедуп не должен съесть повторную отправку
            public bool Force;
        }

        private OrderStateType GetOrderState(string status)
        {
            OrderStateType stateType;

            // защита от пробелов в строковых кодах (исторический баг " 1")
            status = status?.Trim();

            if (status == "0" || status == "5")
            {
                stateType = OrderStateType.Active;
            }
            else if (status == "6" || status == "9")
            {
                // транзитные состояния: в процессе отмены / изменения - ордер жив на бирже
                stateType = OrderStateType.Active;
            }
            else if (status == "8")
            {
                stateType = OrderStateType.Fail;
            }
            else if (status == "1")
            {
                stateType = OrderStateType.Partial;
            }
            else if (status == "2")
            {
                stateType = OrderStateType.Done;
            }
            else if (status == "4")
            {
                stateType = OrderStateType.Cancel;
            }
            else if (status == "10")
            {
                stateType = OrderStateType.Pending;
            }
            else
            {
                stateType = OrderStateType.None;
            }

            return stateType;
        }

        public event Action<MyTrade> MyTradeEvent;
        public event Action<Order> MyOrderEvent;

        #endregion

        #region 10 Trade

        // лимит БКС 10 RPS на сервис "Заявки" СУММАРНО (restrictions.txt):
        // один общий гейт на все операции с заявками, иначе три независимых
        // давали до 30 RPS и штурм 429
        private RateGate _rateGateOrdersOperations = new RateGate(1, TimeSpan.FromMilliseconds(100));

        // лимит БКС 200000 заявок в сутки на счёт: считаем свои отправки
        // (SendOrder и ChangeOrderPrice - редактирование порождает новую заявку)
        private int _ordersSentToday;
        private DateTime _ordersDayMarker = DateTime.MinValue;
        private bool _ordersLimitWarned;
        private string _orderNumbersLocker = "orderNumbersLocker";
        private Dictionary<int, Guid> _guidByNumberOrders = new Dictionary<int, Guid>();
        private Dictionary<Guid, int> _numberByGuidOrders = new Dictionary<Guid, int>();
        private Queue<int> _orderQueue = new Queue<int>();
        private List<List<string>> _changedOrderNumsMarket = [];
        private Dictionary<string, int> _userNumberByOrderId = new Dictionary<string, int>();

        // первоначальный номер -> последний номер после смены цены (постоянный,
        // в отличие от цепочек _changedOrderNumsMarket, которые удаляются на финале)
        private Dictionary<string, string> _latestChangedOrderNumber = new Dictionary<string, string>();

        // дневной лимит заявок: предупреждение на 180k (одноразовое), стоп на 200k -
        // биржа при превышении всё равно ответит 429, но лучше не доводить
        private bool CheckDailyOrdersLimit()
        {
            DateTime today = DateTime.Now.Date;

            if (_ordersDayMarker != today)
            {
                _ordersDayMarker = today;
                _ordersSentToday = 0;
                _ordersLimitWarned = false;
            }

            _ordersSentToday++;

            if (_ordersSentToday >= 180000 && _ordersLimitWarned == false)
            {
                _ordersLimitWarned = true;
                SendLogMessage($"BCS: отправлено {_ordersSentToday} заявок за сутки, приближение к дневному лимиту 200000.", LogMessageType.Error);
            }

            return _ordersSentToday < 200000;
        }

        public void SendOrder(Order order)
        {
            _rateGateOrdersOperations.WaitToProceed();

            if (CheckDailyOrdersLimit() == false)
            {
                CreateOrderFail(order);
                SendLogMessage("Order fail. Достигнут дневной лимит БКС 200000 заявок. Ордер не отправлен.", LogMessageType.Error);
                return;
            }

            try
            {
                string endPoint = "/trade-api-bff-operations/api/v1/orders";

                Dictionary<string, dynamic> jsonContent = new Dictionary<string, dynamic>();

                Guid orderId = Guid.NewGuid();
                string side = order.Side == Side.Buy ? "1" : "2";
                string type = order.TypeOrder == OrderPriceType.Market ? "1" : "2";

                decimal quantity = 0;
                Security security = GetSecurityForOrder(order.SecurityNameCode, order.SecurityClassCode);

                if (security != null)
                {
                    // если лот неизвестен, считаем что объём уже задан в штуках
                    decimal lot = security.Lot > 0 ? security.Lot : 1;
                    quantity = order.Volume * lot;
                }

                if (security == null
                    || quantity <= 0)
                {
                    CreateOrderFail(order);
                    SendLogMessage($"Order fail. Security {order.SecurityNameCode} / {order.SecurityClassCode} not found in securities lists or quantity is zero. Order not sended.", LogMessageType.Error);
                    return;
                }

                jsonContent.Add("clientOrderId", orderId.ToString());
                jsonContent.Add("side", side);
                jsonContent.Add("orderType", type);
                jsonContent.Add("orderQuantity", quantity);
                jsonContent.Add("ticker", order.SecurityNameCode);
                jsonContent.Add("classCode", GetClassCodeByNameClass(order.SecurityClassCode));

                if (order.TypeOrder == OrderPriceType.Limit)
                {
                    jsonContent.Add("price", order.Price);
                }

                AddOrderIds(order.NumberUser, orderId);

                string jsonRequest = JsonConvert.SerializeObject(jsonContent);

                HttpResponseMessage sendOrderResponse = CreateHttpRequestAsync(endPoint, HttpMethod.Post, jsonRequest).Result;

                if (sendOrderResponse.StatusCode == HttpStatusCode.OK)
                {
                    string responseMsg = sendOrderResponse.Content.ReadAsStringAsync().Result;
                    BcsOrderResponse response = JsonConvert.DeserializeAnonymousType(responseMsg, new BcsOrderResponse());

                    if (response.status == "OK")
                    {
                        // ignore
                    }
                    else
                    {
                        CreateOrderFail(order);
                        SendLogMessage($"Order Id: {response.clientOrderId} fail. Status: {response.status}", LogMessageType.Error);
                    }
                }
                else if (sendOrderResponse.StatusCode == HttpStatusCode.Forbidden)
                {
                    string responseMsg = sendOrderResponse.Content.ReadAsStringAsync().Result;

                    CreateOrderFail(order);
                    SendLogMessage($"Order Fail. Status: {sendOrderResponse.StatusCode} || msg:{responseMsg}", LogMessageType.Error);
                }
                else
                {
                    CreateOrderFail(order);
                    string responseMsg = sendOrderResponse.Content.ReadAsStringAsync().Result;
                    SendLogMessage($"Order Fail. Status: {sendOrderResponse.StatusCode} || msg:{responseMsg}", LogMessageType.Error);
                }
            }
            catch (Exception exception)
            {
                CreateOrderFail(order);
                SendLogMessage("Order send error " + exception.ToString(), LogMessageType.Error);
            }
        }

        private void CreateOrderFail(Order order)
        {
            order.State = OrderStateType.Fail;

            MyOrderEvent?.Invoke(order);
        }

        public void ChangeOrderPrice(Order order, decimal newPrice)
        {
            _rateGateOrdersOperations.WaitToProceed();

            if (CheckDailyOrdersLimit() == false)
            {
                SendLogMessage("Order change price fail. Достигнут дневной лимит БКС 200000 заявок.", LogMessageType.Error);
                return;
            }

            try
            {
                string endPoint = "trade-api-bff-operations/api/v1/orders/edit";

                Dictionary<string, dynamic> jsonContent = new Dictionary<string, dynamic>();

                Guid clientOrderId = Guid.NewGuid();
                string type = order.TypeOrder == OrderPriceType.Market ? "1" : "2";

                decimal quantity = 0;
                Security security = GetSecurityForOrder(order.SecurityNameCode, order.SecurityClassCode);

                if (security != null)
                {
                    // если лот неизвестен, считаем что объём уже задан в штуках
                    decimal lot = security.Lot > 0 ? security.Lot : 1;
                    quantity = (order.Volume - order.VolumeExecute) * lot;
                }

                if (security == null
                    || quantity <= 0)
                {
                    // orderQuantity по спеке минимум 1 - нулевой остаток на биржу не отправляем
                    SendLogMessage($"Change order price skipped. Security {order.SecurityNameCode} / {order.SecurityClassCode} not found in securities lists or remaining quantity is zero. Order not changed.", LogMessageType.Error);
                    return;
                }

                string orderId = "";

                Guid guid = GetClientOrderId(order.NumberUser);

                if (guid == Guid.Empty)
                {
                    SendLogMessage($"Change order price skipped. NumberUser: {order.NumberUser} not in mapping.", LogMessageType.System);
                    return;
                }

                orderId = guid.ToString();

                jsonContent.Add("orderIdType", 1);
                jsonContent.Add("orderId", orderId);
                jsonContent.Add("clientOrderId", clientOrderId.ToString());
                jsonContent.Add("orderType", type);

                if (order.TypeOrder == OrderPriceType.Limit)
                {
                    jsonContent.Add("price", newPrice);
                }

                jsonContent.Add("orderQuantity", quantity);

                string jsonRequest = JsonConvert.SerializeObject(jsonContent);

                HttpResponseMessage changeOrderResponse = CreateHttpRequestAsync(endPoint, HttpMethod.Post, jsonRequest).Result;

                if (changeOrderResponse.StatusCode == HttpStatusCode.OK)
                {
                    string responseMsg = changeOrderResponse.Content.ReadAsStringAsync().Result;
                    BcsOrderResponse response = JsonConvert.DeserializeAnonymousType(responseMsg, new BcsOrderResponse());

                    if (response.status == "OK")
                    {
                        AddReverseOrderId(clientOrderId, order.NumberUser);
                        order.Price = newPrice;
                        MyOrderEvent?.Invoke(order);
                    }
                    else
                    {
                        SendLogMessage($"Order Id: {response.clientOrderId} change price error. Status: {response.status}", LogMessageType.Error);
                    }
                }
                else if (changeOrderResponse.StatusCode == HttpStatusCode.Forbidden)
                {
                    string responseMsg = changeOrderResponse.Content.ReadAsStringAsync().Result;
                    SendLogMessage($"Change price order forbidden. Status: {changeOrderResponse.StatusCode} || msg:{responseMsg}", LogMessageType.Error);
                }
                else
                {
                    string responseMsg = changeOrderResponse.Content.ReadAsStringAsync().Result;
                    SendLogMessage($"Change price order error. Body: {jsonRequest}\n Status: {changeOrderResponse.StatusCode} || msg:{responseMsg}", LogMessageType.Error);
                }
            }
            catch (Exception exception)
            {
                SendLogMessage("Change price order request error: " + exception.ToString(), LogMessageType.Error);
            }
        }

        public bool CancelOrder(Order order)
        {
            _rateGateOrdersOperations.WaitToProceed();

            try
            {
                string endPoint = "/trade-api-bff-operations/api/v1/orders/cancel";

                Dictionary<string, dynamic> jsonContent = new Dictionary<string, dynamic>();

                Guid clientOrderId = Guid.NewGuid();

                string orderId = "";

                Guid guid = GetClientOrderId(order.NumberUser);

                if (guid == Guid.Empty)
                {
                    SendLogMessage($"Order cancel skipped. NumberUser: {order.NumberUser} not in mapping.", LogMessageType.System);
                    return false;
                }

                orderId = guid.ToString();

                jsonContent.Add("orderIdType", 1);
                jsonContent.Add("orderId", orderId);
                jsonContent.Add("clientOrderId", clientOrderId.ToString());

                string jsonRequest = JsonConvert.SerializeObject(jsonContent);
                HttpResponseMessage cancelOrderResponse = CreateHttpRequestAsync(endPoint, HttpMethod.Post, jsonRequest).Result;

                if (cancelOrderResponse.StatusCode == HttpStatusCode.OK)
                {
                    string responseMsg = cancelOrderResponse.Content.ReadAsStringAsync().Result;
                    BcsOrderResponse response = JsonConvert.DeserializeAnonymousType(responseMsg, new BcsOrderResponse());

                    if (response.status == "OK")
                    {
                        AddReverseOrderId(clientOrderId, order.NumberUser);
                        return true;
                    }
                    else
                    {
                        SendLogMessage($"Order Id: {response.clientOrderId} cancel error. Status: {response.status}", LogMessageType.Error);
                    }
                }
                else if (cancelOrderResponse.StatusCode == HttpStatusCode.Forbidden)
                {
                    string responseMsg = cancelOrderResponse.Content.ReadAsStringAsync().Result;
                    SendLogMessage($"Cancel order forbidden. Status: {cancelOrderResponse.StatusCode} || msg:{responseMsg}", LogMessageType.Error);
                }
                else
                {
                    SendLogMessage($"Order cancel request error. Body: {jsonRequest}\n Status: {cancelOrderResponse.StatusCode} || msg:{cancelOrderResponse.ReasonPhrase}", LogMessageType.Error);
                }
            }
            catch (Exception exception)
            {
                SendLogMessage("Order cancel error: " + exception.ToString(), LogMessageType.Error);
            }
            return false;
        }

        public void CancelAllOrders()
        {
            List<Order> activeOrders = GetAllActiveOrdersFromExchange();

            if (activeOrders.Count > 0)
            {
                for (int i = 0; i < activeOrders.Count; i++)
                {
                    CancelOrder(activeOrders[i]);
                }
            }
        }

        public void CancelAllOrdersToSecurity(Security security)
        {
            List<Order> activeOrders = GetAllActiveOrdersFromExchange();

            if (activeOrders.Count > 0)
            {
                List<Order> ordersBySec = activeOrders.FindAll(o => o.SecurityNameCode == security.Name && o.SecurityClassCode == security.NameClass);

                if (ordersBySec.Count > 0)
                {
                    for (int i = 0; i < ordersBySec.Count; i++)
                    {
                        CancelOrder(ordersBySec[i]);
                    }
                }
            }
        }

        public OrderStateType GetOrderStatus(Order order)
        {
            _rateGateOrdersOperations.WaitToProceed();

            try
            {
                string orderId = "";

                Guid guid = GetClientOrderId(order.NumberUser);

                if (guid == Guid.Empty)
                {
                    return OrderStateType.None;
                }

                orderId = guid.ToString();

                string endPoint = $"/trade-api-bff-operations/api/v1/orders?orderIdType=1&orderId={orderId}";

                HttpResponseMessage statusOrderResponse = CreateHttpRequestAsync(endPoint, HttpMethod.Get, null).Result;

                if (statusOrderResponse.StatusCode == HttpStatusCode.OK)
                {
                    string responseMsg = statusOrderResponse.Content.ReadAsStringAsync().Result;
                    BcsOrdersResponse statusResponse = JsonConvert.DeserializeAnonymousType(responseMsg, new BcsOrdersResponse());

                    OrderStateType stateType = GetOrderState(statusResponse.data.orderStatus);

                    if (stateType == OrderStateType.Fail)
                    {
                        SendLogMessage("Order fail! Reason: " + statusResponse.data.rejectReason, LogMessageType.Error);
                    }

                    UpdateMyOrder(statusResponse, true);

                    if (stateType == OrderStateType.Done || stateType == OrderStateType.Partial)
                    {
                        // восстановление сделок по исполненному ордеру, когда события ордера были потеряны
                        EnqueueMyTradeFetch(statusResponse, order, true);
                    }

                    return stateType;
                }
                else
                {
                    string errorMsg = statusOrderResponse.Content.ReadAsStringAsync().Result;

                    if (statusOrderResponse.StatusCode == HttpStatusCode.NotFound)
                    {
                        // заявка ещё не видна сервису статусов БКС — регистрация на бирже идёт асинхронно. Не ошибка.
                        SendLogMessage($"Order status not found yet. ClientOrderId: {orderId}. The hub will retry.", LogMessageType.System);
                    }
                    else
                    {
                        SendLogMessage($"Order status request error: {statusOrderResponse.StatusCode} || msg:{errorMsg}", LogMessageType.Error);
                    }
                }
            }
            catch (Exception ex)
            {
                SendLogMessage("Order status getting error: " + ex.ToString(), LogMessageType.Error);
            }

            return OrderStateType.None;
        }

        public void GetAllActivOrders()
        {
            List<Order> ordersResult = GetAllActiveOrdersFromExchange();

            if (ordersResult.Count > 0)
            {
                for (int i = 0; i < ordersResult.Count; i++)
                {
                    MyOrderEvent?.Invoke(ordersResult[i]);
                }
            }
        }

        public List<Order> GetActiveOrders(int startIndex, int count)
        {
            try
            {
                List<Order> ordersResult = GetAllActiveOrdersFromExchange();

                if (ordersResult.Count != 0 && startIndex < ordersResult.Count)
                {
                    if (startIndex + count < ordersResult.Count)
                    {
                        return ordersResult.GetRange(startIndex, count);
                    }
                    else
                    {
                        return ordersResult.GetRange(startIndex, ordersResult.Count - startIndex);
                    }
                }
            }
            catch (Exception ex)
            {
                SendLogMessage("Active orders getting error: " + ex.ToString(), LogMessageType.Error);
            }

            return null;
        }

        private List<Order> GetAllActiveOrdersFromExchange()
        {
            List<Order> ordersResult = [];
            try
            {
                List<Order> activeOrdersBuy = GetOrdersFromExchangeByStatus([3], 1);
                List<Order> activeOrdersSell = GetOrdersFromExchangeByStatus([3], 2);

                if (activeOrdersBuy != null && activeOrdersBuy.Count > 0)
                {
                    ordersResult.AddRange(activeOrdersBuy);
                }

                if (activeOrdersSell != null && activeOrdersSell.Count > 0)
                {
                    ordersResult.AddRange(activeOrdersSell);
                }

                if (ordersResult.Count > 0)
                {
                    ordersResult.Sort((a, b) => b.TimeCallBack.CompareTo(a.TimeCallBack));
                    return ordersResult;
                }
            }
            catch (Exception ex)
            {
                SendLogMessage("Active orders getting error: " + ex.ToString(), LogMessageType.Error);
                return ordersResult;
            }

            return ordersResult;
        }

        public List<Order> GetHistoricalOrders(int startIndex, int count)
        {
            try
            {
                List<Order> ordersResult = [];

                List<Order> historyOrdersBuy = GetOrdersFromExchangeByStatus([1, 2], 1);
                List<Order> historyOrdersSell = GetOrdersFromExchangeByStatus([1, 2], 2);

                if (historyOrdersBuy != null && historyOrdersBuy.Count > 0)
                {
                    ordersResult.AddRange(historyOrdersBuy);
                }

                if (historyOrdersSell != null && historyOrdersSell.Count > 0)
                {
                    ordersResult.AddRange(historyOrdersSell);
                }

                if (ordersResult.Count != 0 && startIndex < ordersResult.Count)
                {
                    if (startIndex + count < ordersResult.Count)
                    {
                        ordersResult.Sort((a, b) => b.TimeCallBack.CompareTo(a.TimeCallBack));
                        return ordersResult.GetRange(startIndex, count);
                    }
                    else
                    {
                        ordersResult.Sort((a, b) => b.TimeCallBack.CompareTo(a.TimeCallBack));
                        return ordersResult.GetRange(startIndex, ordersResult.Count - startIndex);
                    }
                }
            }
            catch (Exception ex)
            {
                SendLogMessage("History orders getting error: " + ex.ToString(), LogMessageType.Error);
            }

            return null;
        }

        // лёгкая версия GetHistoricalOrders для выравнивания после реконнекта:
        // одна страница (100 записей на сторону), окно - последние сутки,
        // без пагинации за всю историю
        private List<Order> GetRecentFinalOrders()
        {
            List<Order> orders = new List<Order>();

            try
            {
                for (int side = 1; side <= 2; side++)
                {
                    _rateGateOrdersOperations.WaitToProceed();

                    string endPoint = "/trade-api-bff-order-details/api/v1/orders/search?page=0&size=100&sort=orderDateTime,desc";

                    Dictionary<string, dynamic> jsonContent = new Dictionary<string, dynamic>
                    {
                        { "startDateTime", DateTime.UtcNow.AddDays(-1).ToString("yyyy-MM-ddTHH:mm:ss.fffZ") },
                        { "endDateTime", DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ") },
                        { "side", side },
                        { "orderStatus", new int[] { 1, 2 } },
                        { "orderTypes", new int[] { 1, 2, 3, 10 } },
                        { "tickers", Array.Empty<string>() },
                        { "classCodes", Array.Empty<string>() }
                    };

                    string jsonRequest = JsonConvert.SerializeObject(jsonContent);

                    HttpResponseMessage orderListResponse = CreateHttpRequestAsync(endPoint, HttpMethod.Post, jsonRequest).Result;

                    if (orderListResponse.StatusCode != HttpStatusCode.OK)
                    {
                        string errorMsg = orderListResponse.Content.ReadAsStringAsync().Result;
                        SendLogMessage($"Recent orders getting error: {orderListResponse.StatusCode} || msg:{errorMsg}", LogMessageType.Error);
                        continue;
                    }

                    string responseMsg = orderListResponse.Content.ReadAsStringAsync().Result;
                    BcsOrdersListResponse ordersResponse = JsonConvert.DeserializeAnonymousType(responseMsg, new BcsOrdersListResponse());

                    if (ordersResponse == null || ordersResponse.records == null)
                    {
                        continue;
                    }

                    for (int i = 0; i < ordersResponse.records.Length; i++)
                    {
                        Order order = ConvertToOsEngineOrder(ordersResponse.records[i]);

                        if (order != null)
                        {
                            orders.Add(order);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                SendLogMessage("Recent orders getting error: " + ex.ToString(), LogMessageType.Error);
            }

            return orders;
        }

        private List<Order> GetOrdersFromExchangeByStatus(int[] orderStatuses, int side)
        {
            List<Order> orders = new List<Order>();
            int page = -1;
            int totalPages = 0;

            try
            {
                do
                {
                    _rateGateOrdersOperations.WaitToProceed();

                    page++;

                    string endPoint = $"/trade-api-bff-order-details/api/v1/orders/search?page={page}&size=100&sort=orderDateTime,desc";

                    Dictionary<string, dynamic> jsonContent = new Dictionary<string, dynamic>
                {
                    { "startDateTime", DateTime.UtcNow.AddMonths(-3).ToString("yyyy-MM-ddTHH:mm:ss.fffZ") },
                    { "endDateTime", DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ") },
                    {"side",  side },
                    {"orderStatus", orderStatuses},
                    {"orderTypes", new int[]{1, 2, 3, 10} },
                    {"tickers",  Array.Empty<string>() },
                    {"classCodes", Array.Empty<string>() }
                };

                    string jsonRequest = JsonConvert.SerializeObject(jsonContent);

                    HttpResponseMessage orderListResponse = CreateHttpRequestAsync(endPoint, HttpMethod.Post, jsonRequest).Result;

                    if (orderListResponse.StatusCode == HttpStatusCode.OK)
                    {
                        string responseMsg = orderListResponse.Content.ReadAsStringAsync().Result;
                        BcsOrdersListResponse ordersResponse = JsonConvert.DeserializeAnonymousType(responseMsg, new BcsOrdersListResponse());

                        if (ordersResponse != null && ordersResponse.records != null && ordersResponse.records.Length > 0)
                        {
                            if (int.TryParse(ordersResponse.totalPages, out int pages))
                            {
                                totalPages = pages;
                            }

                            for (int i = 0; i < ordersResponse.records.Length; i++)
                            {
                                Order order = ConvertToOsEngineOrder(ordersResponse.records[i]);

                                if (order == null)
                                    continue;

                                orders.Add(order);
                            }
                        }
                        else
                        {
                            break;
                        }
                    }
                    else
                    {
                        string errorMsg = orderListResponse.Content.ReadAsStringAsync().Result;
                        SendLogMessage($"Order list getting error: {orderListResponse.StatusCode} || msg:{errorMsg}", LogMessageType.Error);
                        return null;
                    }

                } while (page + 1 < totalPages);

                return orders;
            }
            catch (Exception ex)
            {
                SendLogMessage("Orders getting error: " + ex.ToString(), LogMessageType.Error);
                return null;
            }
        }

        private Order ConvertToOsEngineOrder(Record record)
        {
            try
            {
                Order order = new Order();

                // строковые коды от биржи могут приходить с пробелами (исторический баг " 1")
                string recordStatus = record.orderStatus?.Trim();
                string recordType = record.orderType?.Trim();
                string recordSide = record.side?.Trim();

                Security security = GetSecurityByName(record.ticker, record.classCode);

                if (security != null)
                {
                    order.SecurityNameCode = security.Name;
                    order.SecurityClassCode = security.NameClass;
                }
                else
                {
                    order.SecurityNameCode = record.ticker;
                    order.SecurityClassCode = record.classCode;
                }

                if (_userNumberByOrderId.TryGetValue(record.orderId, out int userNumber))
                {
                    order.NumberUser = userNumber;
                }

                order.NumberMarket = record.orderNum;
                order.TimeCallBack = ConvertUtsStringToDateTimeRu(record.updateDateTime);
                order.Price = record.price.ToDecimal();

                if (order.Price == 0)
                {
                    // у рыночных заявок price приходит нулевой, реальная цена исполнения в averagePrice
                    order.Price = record.averagePrice.ToDecimal();
                }

                order.Volume = record.orderQuantityLots.ToDecimal();
                order.VolumeExecute = record.executedQuantityLots.ToDecimal();
                order.Side = recordSide == "1" ? Side.Buy : Side.Sell;

                if (_myPortfolios.Count == 1)
                {
                    order.PortfolioNumber = _myPortfolios[0].Number;
                }

                if (recordStatus == "1")
                {
                    order.State = OrderStateType.Cancel;
                    order.TimeCancel = ConvertUtsStringToDateTimeRu(record.updateDateTime);
                }
                else if (recordStatus == "2")
                {
                    order.State = OrderStateType.Done;
                    order.TimeDone = ConvertUtsStringToDateTimeRu(record.updateDateTime);
                }
                else
                {
                    order.State = OrderStateType.Active;
                    order.TimeCreate = ConvertUtsStringToDateTimeRu(record.updateDateTime);
                }

                if (recordType == "1")
                    order.TypeOrder = OrderPriceType.Market;
                else if (recordType == "3")
                    order.TypeOrder = OrderPriceType.Iceberg;
                else
                    order.TypeOrder = OrderPriceType.Limit; // типы 2 и 10 — лимитные, 4-7 и 11 — стоп/тейк, порождающие лимитные

                order.ServerType = ServerType.BCS;

                if (order.State == OrderStateType.Active && _changedOrderNumsMarket.Count > 0)
                {
                    for (int i = 0; i < _changedOrderNumsMarket.Count; i++)
                    {
                        List<string> nums = _changedOrderNumsMarket[i];

                        if (nums[^1] == record.orderNum)
                        {
                            order.NumberMarket = nums[0];
                            break;
                        }
                    }
                }

                return order;
            }
            catch (Exception ex)
            {
                SendLogMessage("Order convert error: " + ex.ToString(), LogMessageType.Error);
                return null;
            }
        }

        #endregion

        #region 11 Queries

        private async Task<HttpResponseMessage> CreateHttpRequestAsync(string path, HttpMethod method, string body)
        {
            try
            {
                int maxRetries = 3;

                for (int i = 0; i < maxRetries; i++)
                {
                    using HttpRequestMessage request = new HttpRequestMessage(method, path);

                    // заголовок ставим на каждый запрос: фоновый поток перевыпускает access-токен раз в сутки,
                    // дефолтный заголовок HttpClient после перевыпуска остаётся со старым токеном
                    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _apiAccessToken);

                    if (method == HttpMethod.Post && !string.IsNullOrEmpty(body))
                    {
                        request.Content = new StringContent(body, Encoding.UTF8, "application/json");
                    }

                    try
                    {
                        if (_httpClient != null)
                        {
                            HttpResponseMessage response = await _httpClient.SendAsync(request);

                            if (response.StatusCode == HttpStatusCode.Unauthorized && i < maxRetries - 1)
                            {
                                // токен умер раньше срока (отзыв/смена секрета на стороне БКС):
                                // перевыпускаем и повторяем запрос - заголовок ставится на каждый запрос
                                response.Dispose();

                                SendLogMessage($"Ответ 401 по пути: <<{path}>>, перевыпуск токена и повтор", LogMessageType.System);

                                bool refreshed;

                                lock (_tokenRefreshLocker)
                                {
                                    refreshed = GetAccess24HToken();
                                }

                                if (refreshed == false)
                                {
                                    SendLogMessage("BCS: 401 и перевыпуск токена не удался. Переподключение коннектора.", LogMessageType.Error);
                                    ForceDisconnect();
                                    return null;
                                }

                                continue;
                            }

                            if (response.StatusCode == HttpStatusCode.TooManyRequests && i < maxRetries - 1)
                            {
                                int delayMs = 1000 * (1 << i);

                                response.Dispose();

                                SendLogMessage($"Ответ 429 по пути: <<{path}>>, повтор через {delayMs}мс", LogMessageType.System);

                                await Task.Delay(delayMs);

                                continue;
                            }

                            return response;
                        }
                        else
                        {
                            if (_handler == null)
                                ConfigureHandler();

                            CreateHttpClient();

                            await Task.Delay(1000);
                            i--;
                            continue;
                        }
                    }
                    catch (HttpRequestException ex) when (ex.InnerException is System.IO.IOException)
                    {
                        if (i == maxRetries - 1)
                            throw;

                        SendLogMessage($"Попытка {i + 1} запросить путь: <<{path}>> не удалась, повтор через {1000 * (i + 1)}мс", LogMessageType.System);
                        await Task.Delay(1000 * (i + 1));
                    }
                }

                throw new InvalidOperationException("Запрос не удался после всех попыток");
            }
            catch (TaskCanceledException cancelEx)
            {
                if (cancelEx.InnerException is TimeoutException)
                {
                    SendLogMessage($"Таймаут запроса (30с) по пути: <<{path}>>", LogMessageType.Error);
                }

                // иначе это штатная отмена при StopServer/Dispose, не ошибка
                return null;
            }
            catch (Exception ex)
            {
                SendLogMessage($"Create http request error {ex.Message} {ex.StackTrace}", LogMessageType.Error);
                return null;
            }
        }

        #endregion

        #region 12 Helpers

        public DateTime ConvertUtsStringToDateTimeRu(string timeString)
        {
            DateTime startTimeUtc = DateTime.Parse(timeString, null, DateTimeStyles.RoundtripKind);
            return TimeZoneInfo.ConvertTimeFromUtc(startTimeUtc, _moscowTimeZone);
        }

        private DateTime ParseBcsDate(string value)
        {
            if (string.IsNullOrEmpty(value) || value == "0")
            {
                return DateTime.MinValue;
            }

            // встречающиеся у БКС форматы: 20261217, 2026-12-17, 2026-12-17T00:00:00.000Z
            if (DateTime.TryParseExact(value, new[] { "yyyyMMdd", "yyyy-MM-dd" },
                    CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime result))
            {
                return result;
            }

            if (DateTime.TryParse(value, null, DateTimeStyles.RoundtripKind, out result))
            {
                return result;
            }

            return DateTime.MinValue;
        }

        private Guid GetClientOrderId(int key)
        {
            lock (_orderNumbersLocker)
            {
                if (_guidByNumberOrders.TryGetValue(key, out Guid value))
                {
                    return value;
                }
            }

            return Guid.Empty;
        }

        private void AddOrderIds(int userNumber, Guid clientOrderId)
        {
            lock (_orderNumbersLocker)
            {
                _guidByNumberOrders[userNumber] = clientOrderId;
                _numberByGuidOrders[clientOrderId] = userNumber;
                _orderQueue.Enqueue(userNumber);

                if (_guidByNumberOrders.Count >= 500)
                {
                    RemoveFirstElementsQueue(50);
                }
            }
        }

        private void AddReverseOrderId(Guid clientOrderId, int userNumber)
        {
            lock (_orderNumbersLocker)
            {
                _numberByGuidOrders[clientOrderId] = userNumber;
            }
        }

        private void AddOrderIdAndUserNum(string orderId, int userNumber)
        {
            lock (_orderNumbersLocker)
            {
                _userNumberByOrderId.TryAdd(orderId, userNumber);
            }
        }

        private void RemoveFirstElementsQueue(int count)
        {
            List<string> keys = [];

            for (int i = 0; i < count && _orderQueue.Count > 0; i++)
            {
                int key = _orderQueue.Dequeue();

                if (_guidByNumberOrders.TryGetValue(key, out Guid value))
                {
                    _numberByGuidOrders.Remove(value);
                    _guidByNumberOrders.Remove(key);

                    Dictionary<string, int>.Enumerator entor = _userNumberByOrderId.GetEnumerator();

                    while (entor.MoveNext())
                    {
                        if (entor.Current.Value == key)
                        {
                            keys.Add(entor.Current.Key);
                        }
                    }
                }
            }

            for (int j = 0; j < keys.Count; j++)
            {
                _userNumberByOrderId.Remove(keys[j]);
            }
        }

        private string _securitiesLocker = "securitiesLocker";

        private Security GetSecurityForOrder(string name, string className)
        {
            lock (_securitiesLocker)
            {
                // сначала ищем среди подписанных бумаг
                Security security = _subscribedSecurities.Find(s => s.Name == name && s.NameClass == className);

                if (security != null)
                {
                    return security;
                }

                // бумага могла не быть подписана (например, закрытие позиции из портфеля) —
                // ищем в общем списке бумаг
                security = _securities.Find(s => s.Name == name && s.NameClass == className);

                if (security != null)
                {
                    return security;
                }

                // на крайний случай — по имени без класса
                return _securities.Find(s => s.Name == name);
            }
        }

        private Security GetSecurityByName(string name, string className)
        {
            lock (_securitiesLocker)
            {
                return _subscribedSecurities.Find(s => s.Name == name && s.NameClass.Contains(className));
            }
        }

        public void SetLeverage(Security security, decimal leverage) { }

        #endregion

        #region 13 Log

        private void SendLogMessage(string message, LogMessageType messageType)
        {
            LogMessageEvent?.Invoke(message, messageType);
        }

        public event Action<string, LogMessageType> LogMessageEvent;

        #endregion

        public event Action<Funding> FundingUpdateEvent { add { } remove { } }
        public event Action<OptionMarketDataForConnector> AdditionalMarketDataEvent { add { } remove { } }
        public event Action<SecurityVolumes> Volume24hUpdateEvent { add { } remove { } }
    }
}
