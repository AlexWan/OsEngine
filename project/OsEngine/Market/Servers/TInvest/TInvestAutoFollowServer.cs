/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Grpc.Net.Client;
using Newtonsoft.Json;
using OsEngine.Entity;
using OsEngine.Language;
using OsEngine.Logging;
using OsEngine.Market.Servers.Entity;
using OsEngine.Market.Servers.TInvest.Entity;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Tinkoff.InvestApi.V1;
using Candle = OsEngine.Entity.Candle;
using Order = OsEngine.Entity.Order;
using Portfolio = OsEngine.Entity.Portfolio;
using Security = OsEngine.Entity.Security;
using Trade = OsEngine.Entity.Trade;

namespace OsEngine.Market.Servers.TInvest
{
    public class TInvestAutoFollowServer : AServer
    {
        public TInvestAutoFollowServer(int uniqueId)
        {
            ServerNum = uniqueId;

            TInvestAutoFollowServerRealization realization = new TInvestAutoFollowServerRealization();
            ServerRealization = realization;

            ServerParameterPassword token = CreateParameterPassword(OsLocalization.Market.ServerParamToken, "");
            token.Comment = OsLocalization.Market.ServerParamTokenDescription;

            CreateParameterBoolean(OsLocalization.Market.FullLogConnector, false);
        }
    }

    public class TInvestAutoFollowServerRealization : IServerRealization
    {
        #region 1 Constructor, Status, Connection

        public TInvestAutoFollowServerRealization()
        {
            Thread worker = new Thread(ConnectionCheckThread);
            worker.Name = "CheckAliveTInvestAutoFollow";
            worker.IsBackground = true;
            worker.Start();

            Thread worker2 = new Thread(PortfolioMessageReader);
            worker2.Name = "PortfolioMessageReaderTInvestAutoFollow";
            worker2.IsBackground = true;
            worker2.Start();

            Thread worker3 = new Thread(SignalsMessageReader);
            worker3.Name = "SignalsMessageReaderTInvestAutoFollow";
            worker3.IsBackground = true;
            worker3.Start();

            Thread worker4 = new Thread(PendingLimitSenderThread);
            worker4.Name = "PendingLimitSenderTInvestAutoFollow";
            worker4.IsBackground = true;
            worker4.Start();
        }

        public void Connect(WebProxy proxy)
        {
            _proxy = proxy;

            try
            {
                _token = ((ServerParameterPassword)ServerParameters[0]).Value;
                _fullLog = ((ServerParameterBool)ServerParameters[1]).Value;

                if (string.IsNullOrEmpty(_token))
                {
                    SendLogMessage("Connection terminated. No API Key. You must specify the api token. You can get it on the T-Invest website. ", LogMessageType.Error);
                    return;
                }

                SendLogMessage("Start T-Invest connection ", LogMessageType.System);

                // активные сигналы между подключениями НЕ очищаем:
                // дифф опроса посчитает дельту исполнения и не создаст дубли сделок

                CreateHttpClient();

                // проверяем токен запросом списка стратегий
                List<AfStrategy> strategies = GetStrategies();

                if (strategies == null)
                {
                    return;
                }

                RestoreActiveSignals(strategies);

                CreateStreamsConnection();
            }
            catch (Exception ex) when (IsShutdownRaceError(ex))
            {
                // штатная гонка при дисконекте: HttpClient уже закрыт в Dispose
                // или запрос отменён посередине
            }
            catch (Exception ex)
            {
                SendLogMessage("Autofollow. Error loading instruments. Reconnect the connector " + ex.ToString(), LogMessageType.Error);
            }
        }

        private bool _isDisposedNow = false;

        public void Dispose()
        {
            _isDisposedNow = true;

            try
            {
                // останавливаем чтение всех потоков рыночных данных
                if (_marketDataStreams != null)
                {
                    for (int i = 0; i < _marketDataStreams.Count; i++)
                    {
                        try
                        {
                            if (_marketDataStreams[i].StreamClient != null)
                            {
                                _marketDataStreams[i].StreamClient.RequestStream.CompleteAsync().Wait();
                                _marketDataStreams[i].StreamClient.Dispose();
                            }
                        }
                        catch
                        {
                            // ignore
                        }
                    }

                    _marketDataStreams.Clear();
                }

                if (_securityStreamMap != null)
                {
                    _securityStreamMap.Clear();
                }

                if (_cancellationTokenSource != null)
                {
                    try
                    {
                        // callback'и отмены gRPC выполняются синхронно внутри Cancel,
                        // после него Dispose безопасен
                        _cancellationTokenSource.Cancel();
                        _cancellationTokenSource.Dispose();
                        _cancellationTokenSource = null;
                    }
                    catch
                    {
                        // ignore
                    }
                }

                if (_channel != null)
                {
                    try
                    {
                        _channel.Dispose();
                        _channel = null;
                    }
                    catch
                    {
                        // ignore
                    }
                }

                if (_httpClient != null)
                {
                    try
                    {
                        _httpClient.Dispose();
                        _httpClient = null;
                    }
                    catch
                    {
                        // ignore
                    }
                }

                // локальные состояния обнуляем полностью (контракт Dispose):
                // после реконнекта стейл-лимитка не должна сработать. Оставшиеся
                // в журнале робота local_ ордера добиваются штатно: хаб пинает
                // GetOrderStatus раз в 5 минут, коннектор отвечает Cancel,
                // робот перевыставляет что нужно
                lock (_pendingLimitsLocker)
                {
                    _pendingLimitOrders.Clear();
                }

                lock (_sendingOrdersLocker)
                {
                    _sendingOrders.Clear();
                }

                lock (_completedOrdersLocker)
                {
                    _completedOrders.Clear();
                    _recentTrades.Clear();
                }

                _signalsToSendQueue = new System.Collections.Concurrent.ConcurrentQueue<Order>();

                lock (_lastPricesLocker)
                {
                    _lastMarketPrices.Clear();
                }

                lock (_mdTimeLocker)
                {
                    _lastMdTimeBySecurity.Clear();
                }

                _isFirstPositionsLoad = true;

                if (ServerStatus != ServerConnectStatus.Disconnect)
                {
                    ServerStatus = ServerConnectStatus.Disconnect;
                    DisconnectEvent();
                }
            }
            catch (Exception ex)
            {
                SendLogMessage("Error in Dispose method. " + ex.ToString(), LogMessageType.System);
            }

            _isDisposedNow = false;
        }

        private void ConnectionCheckThread()
        {
            while (true)
            {
                try
                {
                    if (ServerStatus != ServerConnectStatus.Connect)
                    {
                        Thread.Sleep(1000);
                        continue;
                    }

                    MarketDataStreamWrapper lostStream = null;

                    if (_marketDataStreams != null)
                    {
                        for (int i = 0; i < _marketDataStreams.Count; i++)
                        {
                            MarketDataStreamWrapper stream = _marketDataStreams[i];

                            if (stream.LastMessageTime.AddMinutes(3) < DateTime.UtcNow
                                || stream.IsConnected == false)
                            {
                                lostStream = stream;
                                break;
                            }
                        }
                    }

                    if (lostStream != null)
                    {
                        SendLogMessage("Autofollow. Stream is lost " + lostStream.Name, LogMessageType.System);

                        if (_isDisposedNow == true)
                        {
                            continue;
                        }

                        if (TryReconnectDataStream(lostStream))
                        {
                            SendLogMessage("Autofollow. Stream reconnected " + lostStream.Name, LogMessageType.System);
                            Thread.Sleep(2000);
                            continue;
                        }

                        if (ServerStatus == ServerConnectStatus.Connect)
                        {
                            SendLogMessage("Autofollow. Stream reconnect failed " + lostStream.Name, LogMessageType.Error);
                            ServerStatus = ServerConnectStatus.Disconnect;
                            DisconnectEvent();
                            Thread.Sleep(2000);
                        }
                    }
                    else
                    {
                        Thread.Sleep(5000);
                    }
                }
                catch (Exception ex)
                {
                    SendLogMessage(ex.ToString(), LogMessageType.System);
                    Thread.Sleep(5000);
                }
            }
        }

        public DateTime ServerTime { get; set; }

        public ServerType ServerType => ServerType.TInvestAutoFollow;

        public ServerConnectStatus ServerStatus { get; set; } = ServerConnectStatus.Disconnect;

        public List<IServerParameter> ServerParameters { get; set; }

        public event Action ConnectEvent;

        public event Action DisconnectEvent;

        // ордера опрашиваются постоянно, после реконнекта AServer сам опрашивает активные —
        // отдельное событие не нужно, заглушка чтобы убрать warning CS0067
        public event Action ForceCheckOrdersAfterReconnectEvent { add { } remove { } }

        public bool IsCompletelyDeleted { get; set; }

        #endregion

        #region 2 Properties

        private readonly TimeZoneInfo _mskTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Russian Standard Time");

        private string _token;

        private bool _fullLog;

        private WebProxy _proxy;

        // активные ордера-сигналы: ключ = signalId / stopSignalId
        private Dictionary<string, Order> _activeSignalOrders = new Dictionary<string, Order>();

        private string _signalOrdersLocker = "_signalOrdersLockerTInvestAutoFollow";

        // срок жизни стоп-сигналов (UTC): ключ = stopSignalId.
        // Нужен, чтобы отличить истечение сигнала от исполнения —
        // API отдаёт только активные, исчезновение неразличимо
        private Dictionary<string, DateTime> _signalExpireDates = new Dictionary<string, DateTime>();

        // недавно отменённые нами сигналы (UTC): кэш брокера может ещё несколько
        // секунд отдавать их в опросе — воскрешать как новые ордера нельзя,
        // иначе следующим раундом они «исчезнут» и будут зафиксированы как Done
        private Dictionary<string, DateTime> _cancelledSignalTombstones = new Dictionary<string, DateTime>();

        private static readonly TimeSpan _cancelTombstoneTtl = TimeSpan.FromMinutes(5);

        // последняя известная сумма исполнения сигнала (totalAmount): ключ = signalId.
        // По её дельте считаем точный VWAP исполнения для MyTrade
        private Dictionary<string, decimal> _signalTotalAmounts = new Dictionary<string, decimal>();

        // связь локального номера с биржевым сигналом: NumberUser → signalId.
        // NumberMarket у ордеров этой сессии остаётся local_ навсегда —
        // журналы и тесты привязывают сделки по первому номеру ордера
        private Dictionary<int, string> _signalIdByNumberUser = new Dictionary<int, string>();

        // недавно завершённые ордера и их сделки. У API автоследования нет истории —
        // храним сами ограниченное время, чтобы хаб мог восстановить пропущенное
        // финальное состояние через GetOrderStatus (§8.5)
        private class CompletedOrderInfo
        {
            public Order Order;
            public List<MyTrade> Trades = new List<MyTrade>();
            public DateTime TimeCompletedUtc;
        }

        private Dictionary<int, CompletedOrderInfo> _completedOrders = new Dictionary<int, CompletedOrderInfo>();

        private Dictionary<int, List<MyTrade>> _recentTrades = new Dictionary<int, List<MyTrade>>();

        private string _completedOrdersLocker = "_completedOrdersLockerTInvestAutoFollow";

        private bool _activeSignalsRestored = false;

        // последние рыночные цены из gRPC-потока, нужны для определения типа отложенного сигнала
        private Dictionary<string, decimal> _lastMarketPrices = new Dictionary<string, decimal>();

        private string _lastPricesLocker = "_lastPricesLockerTInvestAutoFollow";

        // время последнего стакана по бумаге (МСК, ключ — uid): биржа может прислать
        // время не новее предыдущего — сдвигаем на тик, чтобы время было монотонным
        private Dictionary<string, DateTime> _lastMdTimeBySecurity = new Dictionary<string, DateTime>();

        private string _mdTimeLocker = "_mdTimeLockerTInvestAutoFollow";

        // лимитные заявки, ожидающие цену триггера локально (API автоследования лимиток не имеет)
        private List<Order> _pendingLimitOrders = new List<Order>();

        private string _pendingLimitsLocker = "_pendingLimitsLockerTInvestAutoFollow";

        // ордера, ждущие отправки сигналом брокеру: свежие маркет/стоп-ордера
        // из SendOrder и сработавшие локальные лимитки (очередь 1 сигнал в 18 сек)
        private System.Collections.Concurrent.ConcurrentQueue<Order> _signalsToSendQueue =
            new System.Collections.Concurrent.ConcurrentQueue<Order>();

        // ордера в процессе отправки сигнала брокеру: ключ = NumberUser.
        // Локальная отмена в этом состоянии невозможна — сигнал уже на пути
        private Dictionary<int, Order> _sendingOrders = new Dictionary<int, Order>();

        private string _sendingOrdersLocker = "_sendingOrdersLockerTInvestAutoFollow";

        // первое чтение позиций после подключения: в этот момент ValueBegin = ValueCurrent
        // (входящий объём замораживается и дальше не трогается)
        private bool _isFirstPositionsLoad = true;

        // флаги шорта по бумагам из gRPC (в REST автоследования их нет):
        // uid бумаги → short разрешён. Нужны для warning'а при продаже сверх позиции
        private Dictionary<string, bool> _shortEnabledByNameId = new Dictionary<string, bool>();

        private string _shortFlagsLocker = "_shortFlagsLockerTInvestAutoFollow";

        #endregion

        #region 3 Securities

        private RateGate _rateGateRest = new RateGate(5, TimeSpan.FromSeconds(1));

        private string _getSecuritiesLocker = "_getSecuritiesLockerTInvestAutoFollow";

        public void GetSecurities()
        {
            try
            {
                if (ServerStatus != ServerConnectStatus.Connect)
                {
                    return;
                }

                lock (_getSecuritiesLocker)
                {
                    List<AfInstrument> instruments = GetInstruments();

                    if (instruments == null)
                    {
                        SendLogMessage("Autofollow. Error loading instruments. Reconnect the connector ", LogMessageType.Error);
                        ServerStatus = ServerConnectStatus.Disconnect;
                        DisconnectEvent();
                        return;
                    }

                    _securities.Clear();
                    _securitiesDictionary.Clear();

                    for (int i = 0; i < instruments.Count; i++)
                    {
                        Security newSecurity = CreateSecurityFromInstrument(instruments[i]);

                        if (newSecurity == null)
                        {
                            continue;
                        }

                        if (_securities.Find(s => s.NameId == newSecurity.NameId) == null)
                        {
                            _securities.Add(newSecurity);
                        }

                        if (_securitiesDictionary.ContainsKey(newSecurity.NameId) == false)
                        {
                            _securitiesDictionary.Add(newSecurity.NameId, newSecurity);
                        }
                    }

                    EnrichSecuritiesFromGrpc();
                }

                if (_securities.Count > 0)
                {
                    SendLogMessage("Securities loaded. Count: " + " " + _securities.Count, LogMessageType.System);

                    SecurityEvent?.Invoke(_securities);

                    GetPortfolios();
                }
                else
                {
                    SendLogMessage("Autofollow. Error loading instruments. Reconnect the connector ", LogMessageType.Error);
                }
            }
            catch (Exception ex)
            {
                SendLogMessage("Autofollow. Error loading instruments. Reconnect the connector " + ex.ToString(), LogMessageType.Error);
            }
        }

        private void EnrichSecuritiesFromGrpc()
        {
            // стоимость шага цены и флаг шорта есть только в gRPC API
            // (в REST автоследования их нет). Каждое обогащение — одним пакетным
            // вызовом: сотни унарных упираются в лимит API (ResourceExhausted)
            if (_instrumentsClient == null)
            {
                return;
            }

            EnrichPriceStepCost();
            EnrichShortFlags();
        }

        private void EnrichPriceStepCost()
        {
            // стоимость шага цены критична для фьючерсов: боты считают по ней
            // объём и профит. У опционов поля в API нет — для них, как и при сбое
            // вызова, остаётся приближение PriceStepCost = PriceStep
            try
            {
                _rateGateMarketData.WaitToProceed();

                FuturesResponse futures = _instrumentsClient.Futures(new InstrumentsRequest(), headers: _gRpcMetadata);

                if (futures == null)
                {
                    return;
                }

                Dictionary<string, decimal> costByUid = new Dictionary<string, decimal>();

                for (int i = 0; i < futures.Instruments.Count; i++)
                {
                    Future future = futures.Instruments[i];

                    if (future.MinPriceIncrementAmount != null)
                    {
                        decimal cost = GetValue(future.MinPriceIncrementAmount);

                        if (cost > 0)
                        {
                            costByUid[future.Uid] = cost;
                        }
                    }
                }

                for (int i = 0; i < _securities.Count; i++)
                {
                    Security security = _securities[i];

                    if (security.SecurityType != SecurityType.Futures)
                    {
                        continue;
                    }

                    decimal cost;

                    if (costByUid.TryGetValue(security.NameId, out cost))
                    {
                        security.PriceStepCost = cost;
                    }
                }
            }
            catch (Exception ex)
            {
                SendLogMessage("Autofollow. Failed to get price step cost from gRPC " + ex.Message,
                    LogMessageType.System);
            }
        }

        private void EnrichShortFlags()
        {
            // шорт разрешён не по всем акциям; флаг нужен для warning'а при продаже
            // сверх позиции. Фьючерсы/опционы не грузим — шорт там разрешён всегда
            try
            {
                _rateGateMarketData.WaitToProceed();

                SharesResponse shares = _instrumentsClient.Shares(new InstrumentsRequest(), headers: _gRpcMetadata);

                if (shares == null)
                {
                    return;
                }

                Dictionary<string, bool> flags = new Dictionary<string, bool>();

                for (int i = 0; i < shares.Instruments.Count; i++)
                {
                    flags[shares.Instruments[i].Uid] = shares.Instruments[i].ShortEnabledFlag;
                }

                lock (_shortFlagsLocker)
                {
                    _shortEnabledByNameId = flags;
                }
            }
            catch (Exception ex)
            {
                SendLogMessage("Autofollow. Failed to get short flags from gRPC " + ex.Message,
                    LogMessageType.System);
            }
        }

        private Security CreateSecurityFromInstrument(AfInstrument item)
        {
            SecurityType securityType = GetSecurityType(item.instrumentType);

            if (securityType == SecurityType.None)
            {
                // неизвестный тип инструмента (например sp) пропускаем
                return null;
            }

            Security newSecurity = new Security();
            newSecurity.Name = item.ticker;
            newSecurity.NameId = item.uid;
            newSecurity.NameFull = item.name;
            newSecurity.Exchange = item.exchange;
            newSecurity.SecurityType = securityType;
            newSecurity.NameClass = GetNameClass(securityType, item.currency);

            decimal priceStep = item.minPriceIncrement.ToDecimal();

            if (priceStep == 0)
            {
                priceStep = 1;
            }

            newSecurity.PriceStep = priceStep;
            newSecurity.PriceStepCost = priceStep;
            newSecurity.Decimals = GetDecimalsCount(priceStep);

            decimal lot = item.lot.ToDecimal();

            if (lot == 0)
            {
                lot = 1;
            }

            newSecurity.Lot = lot;
            newSecurity.VolumeStep = 1;
            newSecurity.State = SecurityStateType.Activ;

            if (securityType == SecurityType.Bond)
            {
                newSecurity.NominalCurrent = item.nominal.ToDecimal();
                newSecurity.NominalInitial = item.initialNominal.ToDecimal();
            }

            if (securityType == SecurityType.Futures
                || securityType == SecurityType.Option)
            {
                newSecurity.UsePriceStepCostToCalculateVolume = true;

                if (string.IsNullOrEmpty(item.expirationDate) == false)
                {
                    DateTime expiration;

                    if (DateTime.TryParse(item.expirationDate, out expiration))
                    {
                        newSecurity.Expiration = expiration;
                    }
                }
            }

            return newSecurity;
        }

        private SecurityType GetSecurityType(string instrumentType)
        {
            if (instrumentType == "share") return SecurityType.Stock;
            if (instrumentType == "bond") return SecurityType.Bond;
            if (instrumentType == "etf") return SecurityType.Fund;
            if (instrumentType == "currency") return SecurityType.CurrencyPair;
            if (instrumentType == "futures") return SecurityType.Futures;
            if (instrumentType == "option") return SecurityType.Option;

            return SecurityType.None;
        }

        private string GetNameClass(SecurityType securityType, string currency)
        {
            // соглашение по классам как у основного коннектора TInvest

            if (securityType == SecurityType.CurrencyPair)
            {
                return "Currency pair";
            }

            if (securityType == SecurityType.Futures)
            {
                return SecurityType.Futures.ToString();
            }

            if (securityType == SecurityType.Option)
            {
                return SecurityType.Option.ToString();
            }

            return securityType.ToString() + " " + currency;
        }

        private int GetDecimalsCount(decimal value)
        {
            // разрядность — по значению без хвостовых нулей:
            // тестер сверяет Decimals с PriceStep.ToStringWithNoEndZero()
            string str = value.ToString(System.Globalization.CultureInfo.InvariantCulture);

            int separatorIndex = str.IndexOf('.');

            if (separatorIndex < 0)
            {
                return 0;
            }

            return str.TrimEnd('0').Length - separatorIndex - 1;
        }

        private List<Security> _securities = new List<Security>();

        private Dictionary<string, Security> _securitiesDictionary = new Dictionary<string, Security>();

        private Security GetSecurityByIdFast(string instrumentId)
        {
            Security mySecurity = null;

            if (_securitiesDictionary.TryGetValue(instrumentId, out mySecurity))
            {
                return mySecurity;
            }

            return null;
        }

        private Security GetSecurityByOrder(Order order)
        {
            // у Order нет uid бумаги — матчим по тикеру и классу,
            // фолбэк на тикер (класс мог быть не проставлен)
            Security security = _securities.Find(s => s.Name == order.SecurityNameCode
                && s.NameClass == order.SecurityClassCode);

            if (security == null)
            {
                security = _securities.Find(s => s.Name == order.SecurityNameCode);
            }

            return security;
        }

        public event Action<List<Security>> SecurityEvent;

        #endregion

        #region 4 Portfolios

        private List<Portfolio> _myPortfolios = new List<Portfolio>();

        private string _portfoliosLocker = "_portfoliosLockerTInvestAutoFollow";

        public void GetPortfolios()
        {
            try
            {
                if (ServerStatus != ServerConnectStatus.Connect)
                {
                    return;
                }

                bool updated = UpdateStrategies();

                updated = UpdatePortfolioPositions() || updated;

                if (updated)
                {
                    PortfolioEvent?.Invoke(_myPortfolios);
                }
            }
            catch (Exception ex)
            {
                SendLogMessage(ex.ToString(), LogMessageType.Error);
            }
        }

        private bool UpdateStrategies()
        {
            // стратегия автоследования = портфель в OsEngine (Number = strategyId)

            bool updated = false;

            List<AfStrategy> strategies = GetStrategies();

            if (strategies == null)
            {
                return false;
            }

            HashSet<string> aliveStrategies = new HashSet<string>();

            for (int i = 0; i < strategies.Count; i++)
            {
                if (string.IsNullOrEmpty(strategies[i].strategyId) == false)
                {
                    aliveStrategies.Add(strategies[i].strategyId);
                }
            }

            lock (_portfoliosLocker)
            {
                for (int i = 0; i < strategies.Count; i++)
                {
                    AfStrategy strategy = strategies[i];

                    if (string.IsNullOrEmpty(strategy.strategyId))
                    {
                        continue;
                    }

                    Portfolio portfolio = _myPortfolios.Find(p => p.Number == strategy.strategyId);

                    if (portfolio == null)
                    {
                        portfolio = new Portfolio();
                        portfolio.Number = strategy.strategyId;
                        portfolio.ValueCurrent = 0;
                        portfolio.ValueBegin = 0;
                        _myPortfolios.Add(portfolio);

                        SendLogMessage("Autofollow. Strategy found " + strategy.title
                            + " " + strategy.strategyId + " " + strategy.status, LogMessageType.System);

                        updated = true;
                    }
                }

                // закрытые и удалённые стратегии убираем из списка портфелей,
                // иначе они копятся в GUI до перезапуска
                for (int i = _myPortfolios.Count - 1; i >= 0; i--)
                {
                    if (aliveStrategies.Contains(_myPortfolios[i].Number) == false)
                    {
                        SendLogMessage("Autofollow. Strategy removed from account list " + _myPortfolios[i].Number,
                            LogMessageType.System);

                        _myPortfolios.RemoveAt(i);
                        updated = true;
                    }
                }
            }

            // стратегия могла быть удалена или закрыта, пока были отключены, —
            // её сигналы больше не придут в опросе, снимаем такие ордера сами
            CancelOrdersOfDeadStrategies(aliveStrategies);

            return updated;
        }

        private void CancelOrdersOfDeadStrategies(HashSet<string> aliveStrategies)
        {
            List<Order> orphans = new List<Order>();

            lock (_signalOrdersLocker)
            {
                List<string> keys = new List<string>(_activeSignalOrders.Keys);

                for (int i = 0; i < keys.Count; i++)
                {
                    Order order = _activeSignalOrders[keys[i]];

                    if (string.IsNullOrEmpty(order.PortfolioNumber) == false
                        && aliveStrategies.Contains(order.PortfolioNumber) == false)
                    {
                        _activeSignalOrders.Remove(keys[i]);
                        _signalIdByNumberUser.Remove(order.NumberUser);
                        _signalExpireDates.Remove(keys[i]);
                        _signalTotalAmounts.Remove(keys[i]);
                        orphans.Add(order);
                    }
                }
            }

            lock (_pendingLimitsLocker)
            {
                for (int i = 0; i < _pendingLimitOrders.Count; i++)
                {
                    Order order = _pendingLimitOrders[i];

                    if (string.IsNullOrEmpty(order.PortfolioNumber) == false
                        && aliveStrategies.Contains(order.PortfolioNumber) == false)
                    {
                        _pendingLimitOrders.RemoveAt(i);
                        i--;
                        orphans.Add(order);
                    }
                }
            }

            for (int i = 0; i < orphans.Count; i++)
            {
                Order order = orphans[i];

                order.State = OrderStateType.Cancel;
                order.TimeCallBack = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, _mskTimeZone);
                order.TimeCancel = order.TimeCallBack;

                SendLogMessage("Autofollow. Strategy no longer exists. Cancelling its orders. Strategy " + order.PortfolioNumber + " "
                    + order.SecurityNameCode, LogMessageType.System);

                MyOrderEvent?.Invoke(order);
                RememberCompletedOrder(order);

                SaveActiveSignals(order.PortfolioNumber);
            }
        }

        private bool UpdatePortfolioPositions()
        {
            bool updated = false;
            bool anyLoaded = false;

            List<Portfolio> portfolios;

            lock (_portfoliosLocker)
            {
                portfolios = new List<Portfolio>(_myPortfolios);
            }

            for (int i = 0; i < portfolios.Count; i++)
            {
                Portfolio portfolio = portfolios[i];

                AfPortfolioPositionResponse data = GetPortfolioPosition(portfolio.Number);

                if (data == null)
                {
                    continue;
                }

                anyLoaded = true;
                updated = UpdatePositionsInPortfolio(portfolio, data) || updated;
            }

            // после первой успешной загрузки входящий объём замораживаем,
            // дальше его сохраняет сам SetNewPosition для уже известных позиций.
            // При failed-раунде флаг не трогаем — заморозка должна случиться
            // на первом удачном чтении, а не на первой попытке
            if (anyLoaded)
            {
                _isFirstPositionsLoad = false;
            }

            return updated;
        }

        private bool UpdatePositionsInPortfolio(Portfolio portfolio, AfPortfolioPositionResponse data)
        {
            List<PositionOnBoard> freshPositions = new List<PositionOnBoard>();

            if (data.positions != null)
            {
                // Формат ответа гибридный (проверено живыми ответами 06.10.2026):
                // акции приходят агрегированным снапшотом (одна строка, lots = итог),
                // фьючерсы — журналом движений (строка на каждое изменение, ±lots,
                // нулевой нетто строк не удаляет). Суммирование по тикеру покрывает
                // оба стиля и потому обязательно.
                // Группировка именно по тикеру, а не по instrumentUid:
                // Portfolio.SetNewPosition сливает позиции по SecurityNameCode
                // с перезаписью значения, поэтому суммировать надо до него
                Dictionary<string, PositionOnBoard> positionsByTicker = new Dictionary<string, PositionOnBoard>();

                for (int i = 0; i < data.positions.Count; i++)
                {
                    AfPosition pos = data.positions[i];

                    Security security = string.IsNullOrEmpty(pos.instrumentUid)
                        ? null
                        : GetSecurityByIdFast(pos.instrumentUid);

                    string ticker = pos.ticker;

                    if (string.IsNullOrEmpty(ticker)
                        && security != null)
                    {
                        ticker = security.Name;
                    }

                    if (string.IsNullOrEmpty(ticker))
                    {
                        continue;
                    }

                    PositionOnBoard newPos = null;

                    if (positionsByTicker.TryGetValue(ticker, out newPos) == false)
                    {
                        newPos = new PositionOnBoard();
                        newPos.PortfolioName = portfolio.Number;
                        newPos.SecurityNameCode = ticker;

                        if (security != null)
                        {
                            newPos.SecurityNameClass = security.NameClass;
                        }

                        positionsByTicker.Add(ticker, newPos);
                    }

                    newPos.ValueCurrent += pos.lots.ToDecimal();
                }

                foreach (PositionOnBoard newPos in positionsByTicker.Values)
                {
                    if (newPos.ValueCurrent == 0)
                    {
                        // позиция закрылась — не показываем
                        continue;
                    }

                    newPos.ValueBegin = _isFirstPositionsLoad ? newPos.ValueCurrent : 0;

                    freshPositions.Add(newPos);
                }
            }

            if (data.money != null)
            {
                for (int i = 0; i < data.money.Count; i++)
                {
                    AfMoney money = data.money[i];

                    PositionOnBoard newPos = new PositionOnBoard();
                    newPos.PortfolioName = portfolio.Number;
                    newPos.SecurityNameCode = money.currency;
                    newPos.ValueCurrent = money.quantity.ToDecimal();

                    if (newPos.ValueCurrent == 0)
                    {
                        // нулевой остаток валюты не показываем
                        continue;
                    }

                    newPos.ValueBegin = _isFirstPositionsLoad ? newPos.ValueCurrent : 0;

                    freshPositions.Add(newPos);
                }
            }

            lock (_portfoliosLocker)
            {
                // удаляем позиции, которых больше нет в виртуальном портфеле,
                // и нулевые — иначе они копятся в списке до перезапуска

                for (int i = 0; portfolio.PositionOnBoard != null && i < portfolio.PositionOnBoard.Count; i++)
                {
                    PositionOnBoard pos = portfolio.PositionOnBoard[i];

                    if (pos.ValueCurrent == 0
                        || freshPositions.Count == 0
                        || freshPositions.Find(p => p.SecurityNameCode == pos.SecurityNameCode) == null)
                    {
                        portfolio.PositionOnBoard.RemoveAt(i);
                        i--;
                    }
                }

                for (int i = 0; i < freshPositions.Count; i++)
                {
                    portfolio.SetNewPosition(freshPositions[i]);
                }
            }

            return true;
        }

        public event Action<List<Portfolio>> PortfolioEvent;

        #endregion

        #region 5 Data

        // https://russianinvestments.github.io/investAPI/limits/
        private RateGate _rateGateMarketData = new RateGate(600, TimeSpan.FromMinutes(1));

        public List<Candle> GetLastCandleHistory(Security security, TimeFrameBuilder timeFrameBuilder, int candleCount)
        {
            if (ServerStatus == ServerConnectStatus.Disconnect
                || candleCount <= 0)
            {
                return null;
            }

            if (candleCount > 5000)
            {
                candleCount = 5000;
            }

            DateTime timeEnd = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, _mskTimeZone); // to MSK
            DateTime timeStart = timeEnd - TimeSpan.FromMinutes(timeFrameBuilder.TimeFrameTimeSpan.TotalMinutes * (candleCount * 1.5));

            List<Candle> candles = GetCandleDataToSecurity(security, timeFrameBuilder, timeStart, timeEnd, timeStart);

            if (candles != null)
            {
                while (candles.Count > candleCount)
                {
                    candles.RemoveAt(0);
                }
            }

            return candles;
        }

        public List<Candle> GetCandleDataToSecurity(Security security, TimeFrameBuilder timeFrameBuilder,
            DateTime startTime, DateTime endTime, DateTime actualTime)
        {
            startTime = TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(startTime, DateTimeKind.Unspecified), _mskTimeZone);
            endTime = TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(endTime, DateTimeKind.Unspecified), _mskTimeZone);
            actualTime = TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(actualTime, DateTimeKind.Unspecified), _mskTimeZone);

            if (startTime != actualTime)
            {
                startTime = actualTime;
            }

            List<Candle> candles = new List<Candle>();
            TimeFrame tf = timeFrameBuilder.TimeFrame;

            int days = 1; // период, за который запрашивать свечи

            if (tf == TimeFrame.Day)
            {
                days = 500;
            }
            else if (tf == TimeFrame.Hour2 ||
                     tf == TimeFrame.Hour4)
            {
                days = 60;
            }
            else if (tf == TimeFrame.Hour1)
            {
                days = 30;
            }
            else if (tf == TimeFrame.Min30)
            {
                days = 14;
            }
            else if (tf == TimeFrame.Min5
                || tf == TimeFrame.Min10
                || tf == TimeFrame.Min15
                || tf == TimeFrame.Min20)
            {
                days = 5;
            }

            while (startTime < endTime)
            {
                DateTime endDateTime = startTime.AddDays(days);
                if (endDateTime > endTime) // не заказываем лишних данных
                    endDateTime = endTime;

                List<Candle> range = GetCandleHistoryFromDays(startTime, endDateTime, security, tf, 0);

                if (range == null) // запрошен некорректный таймфрейм
                    return null;

                candles.AddRange(range);

                startTime = endDateTime;
            }

            // под конец фильтруем одинаковые от брокера
            return FilterCorrectCandles(candles);
        }

        private List<Candle> FilterCorrectCandles(List<Candle> candles)
        {
            if (candles == null || candles.Count == 0)
                return candles;

            List<Candle> filtered = new List<Candle>();

            filtered.Add(candles[0]);
            for (int i = 1; i < candles.Count; i++)
            {
                Candle curCandle = candles[i];
                Candle prevCandle = candles[i - 1];

                if (curCandle.TimeStart == prevCandle.TimeStart)
                {
                    continue;
                }

                filtered.Add(curCandle);
            }

            return filtered;
        }

        private List<Candle> GetCandleHistoryFromDays(DateTime fromDateTime, DateTime toDateTime,
            Security security, TimeFrame tf, int tryCount)
        {
            CandleInterval requestedCandleInterval = CreateTimeFrameInterval(tf);

            if (requestedCandleInterval == CandleInterval.Unspecified)
                return null;

            Timestamp from = Timestamp.FromDateTime(fromDateTime);
            Timestamp to = Timestamp.FromDateTime(toDateTime);

            GetCandlesResponse candlesResp = null;
            int retries = 3;

            while (candlesResp == null && retries-- > 0)
            {
                _rateGateMarketData.WaitToProceed();

                try
                {
                    GetCandlesRequest getCandlesRequest = new GetCandlesRequest();
                    getCandlesRequest.InstrumentId = security.NameId;
                    getCandlesRequest.From = from;
                    getCandlesRequest.To = to;
                    getCandlesRequest.Interval = requestedCandleInterval;

                    candlesResp = _marketDataServiceClient.GetCandles(getCandlesRequest, _gRpcMetadata);
                }
                catch (RpcException ex)
                {
                    SendLogMessage("Autofollow. Error getting candles " + security.Name + " " + ex.StatusCode,
                        LogMessageType.System);
                    Thread.Sleep(300);
                }
                catch (Exception ex)
                {
                    if (ServerStatus == ServerConnectStatus.Disconnect)
                    {
                        break; // соединение разорвано до получения свечей
                    }

                    SendLogMessage("Autofollow. Error getting candles " + security.Name + " " + ex.ToString(),
                        LogMessageType.System);
                    Thread.Sleep(300);
                }
            }

            List<Candle> candles = ConvertToOsEngineCandles(candlesResp, security);

            if ((candles == null
                || candles.Count < 2)
                && tryCount < 5)
            {
                Thread.Sleep(100);
                tryCount++;
                candles = GetCandleHistoryFromDays(fromDateTime, toDateTime, security, tf, tryCount);
            }

            if (tf == TimeFrame.Day
                && candles != null)
            {
                for (int i = 0; i < candles.Count; i++)
                {
                    candles[i].TimeStart = candles[i].TimeStart.Date;
                }
            }

            return candles;
        }

        private List<Candle> ConvertToOsEngineCandles(GetCandlesResponse response, Security security)
        {
            List<Candle> candles = new List<Candle>();

            if (response == null)
                return candles;

            for (int i = 0; i < response.Candles.Count; i++)
            {
                HistoricCandle histCandle = response.Candles[i];

                Candle candle = new Candle();

                if (security.SecurityType == SecurityType.Bond
                    && security.NominalCurrent != 0)
                {
                    candle.Open = GetValue(histCandle.Open) / 100 * security.NominalCurrent;
                    candle.Close = GetValue(histCandle.Close) / 100 * security.NominalCurrent;
                    candle.High = GetValue(histCandle.High) / 100 * security.NominalCurrent;
                    candle.Low = GetValue(histCandle.Low) / 100 * security.NominalCurrent;
                }
                else
                {
                    candle.Open = GetValue(histCandle.Open);
                    candle.Close = GetValue(histCandle.Close);
                    candle.High = GetValue(histCandle.High);
                    candle.Low = GetValue(histCandle.Low);
                }

                candle.Volume = histCandle.Volume;
                candle.TimeStart = TimeZoneInfo.ConvertTimeFromUtc(histCandle.Time.ToDateTime(), _mskTimeZone);
                candle.State = CandleState.Finished;

                candles.Add(candle);
            }

            return candles;
        }

        private CandleInterval CreateTimeFrameInterval(TimeFrame tf)
        {
            if (tf == TimeFrame.Min1)
            {
                return CandleInterval._1Min;
            }
            if (tf == TimeFrame.Min2)
            {
                return CandleInterval._2Min;
            }
            if (tf == TimeFrame.Min3)
            {
                return CandleInterval._3Min;
            }
            else if (tf == TimeFrame.Min5)
            {
                return CandleInterval._5Min;
            }
            else if (tf == TimeFrame.Min10)
            {
                return CandleInterval._10Min;
            }
            else if (tf == TimeFrame.Min15)
            {
                return CandleInterval._15Min;
            }
            else if (tf == TimeFrame.Min30)
            {
                return CandleInterval._30Min;
            }
            else if (tf == TimeFrame.Hour1)
            {
                return CandleInterval.Hour;
            }
            else if (tf == TimeFrame.Hour2)
            {
                return CandleInterval._2Hour;
            }
            else if (tf == TimeFrame.Hour4)
            {
                return CandleInterval._4Hour;
            }
            else if (tf == TimeFrame.Day)
            {
                return CandleInterval.Day;
            }

            return CandleInterval.Unspecified;
        }

        public List<Trade> GetTickDataToSecurity(Security security, DateTime startTime, DateTime endTime, DateTime actualTime)
        {
            return null;
        }

        #endregion

        #region 6 gRPC streams creation

        private readonly string _gRPCHost = "https://invest-public-api.tbank.ru:443"; // prod

        private static readonly Lazy<X509Certificate2[]> _tInvestCertificates =
            new Lazy<X509Certificate2[]>(LoadTInvestCertificates);

        private Metadata _gRpcMetadata;

        private GrpcChannel _channel;

        private CancellationTokenSource _cancellationTokenSource;

        private MarketDataService.MarketDataServiceClient _marketDataServiceClient;

        private MarketDataStreamService.MarketDataStreamServiceClient _marketDataStreamClient;

        private InstrumentsService.InstrumentsServiceClient _instrumentsClient;

        private static X509Certificate2[] LoadTInvestCertificates()
        {
            // сертификаты НУЦ Минцифры лежат embedded-ресурсами в папке TInvest/Certificates

            System.Reflection.Assembly assembly = typeof(TInvestAutoFollowServer).Assembly;

            string[] resourceNames = new[]
            {
                "OsEngine.Market.Servers.TInvest.Certificates.russian_trusted_root_ca.cer",
                "OsEngine.Market.Servers.TInvest.Certificates.russian_trusted_sub_ca.cer"
            };

            List<X509Certificate2> certificates = new List<X509Certificate2>(resourceNames.Length);

            foreach (string resourceName in resourceNames)
            {
                using (Stream stream = assembly.GetManifestResourceStream(resourceName))
                {
                    if (stream == null)
                    {
                        throw new InvalidOperationException($"T-Invest certificate resource not found: {resourceName}");
                    }

                    byte[] data = new byte[stream.Length];
                    stream.ReadExactly(data, 0, data.Length);
                    certificates.Add(X509CertificateLoader.LoadCertificate(data));
                }
            }

            return certificates.ToArray();
        }

        private void CreateHttpClient()
        {
            // API Т-Банка предъявляет сертификат в цепочке НУЦ Минцифры РФ,
            // которого нет в системном хранилище — добавляем корни вручную
            SocketsHttpHandler handler = new SocketsHttpHandler()
            {
                PooledConnectionIdleTimeout = TimeSpan.FromMinutes(5),
                PooledConnectionLifetime = TimeSpan.FromHours(1),

                Proxy = _proxy,
                UseProxy = _proxy != null,

                SslOptions = new SslClientAuthenticationOptions
                {
                    EnabledSslProtocols = System.Security.Authentication.SslProtocols.Tls12
                                        | System.Security.Authentication.SslProtocols.Tls13,
                    CertificateChainPolicy = new X509ChainPolicy
                    {
                        RevocationMode = X509RevocationMode.NoCheck,
                        TrustMode = X509ChainTrustMode.CustomRootTrust,
                    }
                }
            };

            foreach (X509Certificate2 cert in _tInvestCertificates.Value)
            {
                handler.SslOptions.CertificateChainPolicy.CustomTrustStore.Add(cert);
            }

            if (_httpClient != null)
            {
                try
                {
                    _httpClient.Dispose();
                }
                catch
                {
                    // ignore
                }
            }

            _httpClient = new HttpClient(handler);
            _httpClient.BaseAddress = new Uri("https://invest-public-api.tbank.ru");
            _httpClient.DefaultRequestHeaders.Add("Authorization", "Bearer " + _token);
        }

        private void CreateStreamsConnection()
        {
            try
            {
                _gRpcMetadata = new Metadata();
                _gRpcMetadata.Add("Authorization", $"Bearer {_token}");
                _gRpcMetadata.Add("x-app-name", "OsEngine");

                _cancellationTokenSource = new CancellationTokenSource();

                X509Certificate2[] tInvestCertificates = _tInvestCertificates.Value;

                SocketsHttpHandler socketsHandler = new SocketsHttpHandler()
                {
                    // KeepAlive настройки
                    KeepAlivePingDelay = TimeSpan.FromSeconds(10),
                    KeepAlivePingTimeout = TimeSpan.FromSeconds(5),
                    KeepAlivePingPolicy = HttpKeepAlivePingPolicy.Always,

                    // Прокси настройки
                    Proxy = _proxy,
                    UseProxy = _proxy != null,

                    // Оптимизации
                    PooledConnectionIdleTimeout = TimeSpan.FromMinutes(5),
                    PooledConnectionLifetime = TimeSpan.FromHours(1),
                    EnableMultipleHttp2Connections = true,

                    // SSL настройки с доверенными корнями НУЦ Минцифры РФ
                    SslOptions = new SslClientAuthenticationOptions
                    {
                        EnabledSslProtocols = System.Security.Authentication.SslProtocols.Tls12
                                            | System.Security.Authentication.SslProtocols.Tls13,
                        CertificateChainPolicy = new X509ChainPolicy
                        {
                            RevocationMode = X509RevocationMode.NoCheck,
                            TrustMode = X509ChainTrustMode.CustomRootTrust,
                        }
                    }
                };

                foreach (X509Certificate2 cert in tInvestCertificates)
                {
                    socketsHandler.SslOptions.CertificateChainPolicy.CustomTrustStore.Add(cert);
                }

                _channel = GrpcChannel.ForAddress(_gRPCHost, new GrpcChannelOptions
                {
                    Credentials = ChannelCredentials.SecureSsl,
                    HttpHandler = socketsHandler
                });

                _marketDataServiceClient = new MarketDataService.MarketDataServiceClient(_channel);
                _marketDataStreamClient = new MarketDataStreamService.MarketDataStreamServiceClient(_channel);
                _instrumentsClient = new InstrumentsService.InstrumentsServiceClient(_channel);

                _marketDataStreams = new List<MarketDataStreamWrapper>();
                _securityStreamMap = new Dictionary<string, MarketDataStreamWrapper>();

                ServerStatus = ServerConnectStatus.Connect;
                ConnectEvent();
            }
            catch (Exception ex)
            {
                SendLogMessage(ex.ToString(), LogMessageType.System);
            }
        }

        #endregion

        #region 7 gRPC streams fast reconnect

        private bool TryReconnectDataStream(MarketDataStreamWrapper streamWrapper)
        {
            try
            {
                lock (_marketDataStreamLocker)
                {
                    if (streamWrapper.StreamClient != null)
                    {
                        try
                        {
                            Task completeTask = streamWrapper.StreamClient.RequestStream.CompleteAsync();
                            if (completeTask.Wait(_streamWaitTimeout) == false)
                            {
                                ObserveTaskFault(completeTask);
                            }
                            streamWrapper.StreamClient.Dispose();
                        }
                        catch
                        {
                            // ignore
                        }
                    }

                    // дожидаемся завершения старого читателя,
                    // чтобы он не успел изменить состояние нового стрима
                    Task oldReadingTask = streamWrapper.ReadingTask;
                    if (oldReadingTask != null)
                    {
                        try
                        {
                            if (oldReadingTask.Wait(_streamWaitTimeout) == false)
                            {
                                ObserveTaskFault(oldReadingTask);
                            }
                        }
                        catch
                        {
                            // исключение читателя уже обработано в нём самом
                        }
                    }

                    streamWrapper.StreamClient = _marketDataStreamClient.MarketDataStream(headers: _gRpcMetadata,
                        cancellationToken: _cancellationTokenSource.Token);

                    streamWrapper.ReadingTask = Task.Run(() => ReadStream(streamWrapper));

                    streamWrapper.IsConnected = true;
                    streamWrapper.LastMessageTime = DateTime.UtcNow;

                    if (streamWrapper.Subscriptions.Count > 0)
                    {
                        SubscribeTradesRequest tradesToResubscribe =
                            new SubscribeTradesRequest { SubscriptionAction = SubscriptionAction.Subscribe };
                        SubscribeOrderBookRequest orderBooksToResubscribe =
                            new SubscribeOrderBookRequest { SubscriptionAction = SubscriptionAction.Subscribe };

                        // все одиночные подписки собираем в пакетные запросы
                        for (int i = 0; i < streamWrapper.Subscriptions.Count; i++)
                        {
                            MarketDataRequest sub = streamWrapper.Subscriptions[i];

                            if (sub.SubscribeTradesRequest != null)
                            {
                                tradesToResubscribe.Instruments.AddRange(sub.SubscribeTradesRequest.Instruments);
                            }
                            else if (sub.SubscribeOrderBookRequest != null)
                            {
                                orderBooksToResubscribe.Instruments.AddRange(sub.SubscribeOrderBookRequest.Instruments);
                            }
                        }

                        _rateGateSubscribe.WaitToProceed();

                        if (tradesToResubscribe.Instruments.Count > 0)
                        {
                            MarketDataRequest batchTradeRequest =
                                new MarketDataRequest { SubscribeTradesRequest = tradesToResubscribe };
                            Task writeTradeTask = streamWrapper.StreamClient.RequestStream.WriteAsync(batchTradeRequest);
                            if (writeTradeTask.Wait(_streamWaitTimeout) == false)
                            {
                                ObserveTaskFault(writeTradeTask);
                                streamWrapper.IsConnected = false;
                                return false;
                            }
                            _rateGateSubscribe.WaitToProceed();
                        }

                        if (orderBooksToResubscribe.Instruments.Count > 0)
                        {
                            MarketDataRequest batchOrderBookRequest =
                                new MarketDataRequest { SubscribeOrderBookRequest = orderBooksToResubscribe };
                            Task writeOrderBookTask = streamWrapper.StreamClient.RequestStream.WriteAsync(batchOrderBookRequest);
                            if (writeOrderBookTask.Wait(_streamWaitTimeout) == false)
                            {
                                ObserveTaskFault(writeOrderBookTask);
                                streamWrapper.IsConnected = false;
                                return false;
                            }
                        }
                    }
                }
            }
            catch
            {
                return false;
            }

            return true;
        }

        private void ObserveTaskFault(Task task)
        {
            // наблюдаем возможный фолт брошенной задачи, чтобы она не стала UnobservedTaskException
            task.ContinueWith(t =>
            {
                try
                {
                    _ = t.Exception;
                }
                catch (Exception error)
                {
                    SendLogMessage(error.ToString(), LogMessageType.Error);
                }
            }, TaskContinuationOptions.OnlyOnFaulted);
        }

        #endregion

        #region 8 Security subscribe

        // лимиты Т-Инвест на подписки: до 100 топиков на стрим, до 100 запросов на подписку в минуту
        private RateGate _rateGateSubscribe = new RateGate(1, TimeSpan.FromMilliseconds(650));

        private List<MarketDataStreamWrapper> _marketDataStreams = new List<MarketDataStreamWrapper>();

        private Dictionary<string, MarketDataStreamWrapper> _securityStreamMap =
            new Dictionary<string, MarketDataStreamWrapper>();

        private string _marketDataStreamLocker = "_marketDataStreamLockerTInvestAutoFollow";

        private static readonly TimeSpan _streamWaitTimeout = TimeSpan.FromSeconds(5);

        public void Subscribe(Security security)
        {
            try
            {
                if (ServerStatus != ServerConnectStatus.Connect)
                {
                    return;
                }

                lock (_marketDataStreamLocker)
                {
                    if (_securityStreamMap.ContainsKey(security.NameId))
                    {
                        return;
                    }

                    // 1 берём общий стрим для тиков, либо создаём новый

                    MarketDataStreamWrapper streamWrapperCommon =
                        _marketDataStreams.FirstOrDefault(s => s.Subscriptions.Count < 99
                            && s.IsConnected == true
                            && s.Name.EndsWith("Common"));

                    if (streamWrapperCommon == null)
                    {
                        if (_marketDataStreams.Count < 16)
                        {
                            streamWrapperCommon = new MarketDataStreamWrapper()
                            {
                                Name = "Market data stream " + (_marketDataStreams.Count + 1) + " Common",
                                IsConnected = false,
                                LastMessageTime = DateTime.UtcNow,
                            };
                            _marketDataStreams.Add(streamWrapperCommon);
                            TryReconnectDataStream(streamWrapperCommon);
                            SendLogMessage("Autofollow. Created market data stream " + streamWrapperCommon.Name, LogMessageType.System);
                        }
                        else
                        {
                            SendLogMessage("Autofollow. Market data streams limit reached. Cant subscribe " + security.Name, LogMessageType.Error);
                            return;
                        }
                    }

                    // 2 берём стрим для стаканов, либо создаём новый

                    MarketDataStreamWrapper streamWrapperMarketDepth =
                        _marketDataStreams.FirstOrDefault(s => s.Subscriptions.Count < 99
                            && s.IsConnected == true
                            && s.Name.EndsWith("MarketDepth"));

                    if (streamWrapperMarketDepth == null)
                    {
                        if (_marketDataStreams.Count < 16)
                        {
                            streamWrapperMarketDepth = new MarketDataStreamWrapper()
                            {
                                Name = "Market data stream " + (_marketDataStreams.Count + 1) + " MarketDepth",
                                IsConnected = false,
                                LastMessageTime = DateTime.UtcNow,
                            };
                            _marketDataStreams.Add(streamWrapperMarketDepth);
                            TryReconnectDataStream(streamWrapperMarketDepth);
                            SendLogMessage("Autofollow. Created market data stream " + streamWrapperMarketDepth.Name, LogMessageType.System);
                        }
                        else
                        {
                            SendLogMessage("Autofollow. Market data streams limit reached. Cant subscribe " + security.Name, LogMessageType.Error);
                            return;
                        }
                    }

                    // 3 подписки. В список Subscriptions — только после успешной
                    // записи в стрим, иначе при таймауте реконнект воскресит
                    // подписку, которую движок не просил

                    TradeInstrument tradeInstrument = new TradeInstrument();
                    tradeInstrument.InstrumentId = security.NameId;

                    SubscribeTradesRequest subscribeTradesRequest = new SubscribeTradesRequest
                    {
                        SubscriptionAction = SubscriptionAction.Subscribe,
                        Instruments = { tradeInstrument }
                    };
                    MarketDataRequest tradeRequest = new MarketDataRequest();
                    tradeRequest.SubscribeTradesRequest = subscribeTradesRequest;

                    _rateGateSubscribe.WaitToProceed();
                    Task writeTradesTask = streamWrapperCommon.StreamClient.RequestStream.WriteAsync(tradeRequest);
                    if (writeTradesTask.Wait(_streamWaitTimeout) == false)
                    {
                        ObserveTaskFault(writeTradesTask);
                        streamWrapperCommon.IsConnected = false;
                        throw new TimeoutException("Market data stream write timeout");
                    }

                    streamWrapperCommon.Subscriptions.Add(tradeRequest);

                    // подписка на стакан

                    MarketDataRequest orderBookRequest = new MarketDataRequest();

                    OrderBookInstrument orderBookInstrument = new OrderBookInstrument();
                    orderBookInstrument.InstrumentId = security.NameId;
                    orderBookInstrument.Depth = GetOrderBookDepth();

                    SubscribeOrderBookRequest subscribeOrderBookRequest = new SubscribeOrderBookRequest
                    { SubscriptionAction = SubscriptionAction.Subscribe, Instruments = { orderBookInstrument } };
                    orderBookRequest.SubscribeOrderBookRequest = subscribeOrderBookRequest;

                    _rateGateSubscribe.WaitToProceed();
                    Task writeMdTask = streamWrapperMarketDepth.StreamClient.RequestStream.WriteAsync(orderBookRequest);
                    if (writeMdTask.Wait(_streamWaitTimeout) == false)
                    {
                        ObserveTaskFault(writeMdTask);
                        streamWrapperMarketDepth.IsConnected = false;

                        // откатываем тиковую подписку — иначе реконнект
                        // воскресит её сиротой (в map запись не попадёт)
                        streamWrapperCommon.Subscriptions.Remove(tradeRequest);

                        throw new TimeoutException("Market data stream write timeout");
                    }

                    streamWrapperMarketDepth.Subscriptions.Add(orderBookRequest);

                    _securityStreamMap.Add(security.NameId, streamWrapperMarketDepth);
                }
            }
            catch (Exception ex)
            {
                SendLogMessage("Autofollow. Failed to subscribe " + security.Name + " " + ex.ToString(), LogMessageType.Error);
            }
        }

        public void Unsubscribe(Security security)
        {
            try
            {
                lock (_marketDataStreamLocker)
                {
                    if (_securityStreamMap.ContainsKey(security.NameId) == false)
                    {
                        return;
                    }

                    // бумага может быть нужна самому коннектору: триггер локальных
                    // лимиток и определение типа стоп-сигнала смотрят на рынок
                    lock (_pendingLimitsLocker)
                    {
                        for (int i = 0; i < _pendingLimitOrders.Count; i++)
                        {
                            if (_pendingLimitOrders[i].SecurityNameCode == security.Name)
                            {
                                return;
                            }
                        }
                    }

                    _securityStreamMap.Remove(security.NameId);

                    for (int i = 0; i < _marketDataStreams.Count; i++)
                    {
                        MarketDataStreamWrapper stream = _marketDataStreams[i];

                        for (int j = stream.Subscriptions.Count - 1; j >= 0; j--)
                        {
                            MarketDataRequest sub = stream.Subscriptions[j];

                            if (sub.SubscribeTradesRequest != null)
                            {
                                if (sub.SubscribeTradesRequest.Instruments[0].InstrumentId != security.NameId)
                                {
                                    continue;
                                }

                                sub.SubscribeTradesRequest.SubscriptionAction = SubscriptionAction.Unsubscribe;
                            }
                            else if (sub.SubscribeOrderBookRequest != null)
                            {
                                if (sub.SubscribeOrderBookRequest.Instruments[0].InstrumentId != security.NameId)
                                {
                                    continue;
                                }

                                sub.SubscribeOrderBookRequest.SubscriptionAction = SubscriptionAction.Unsubscribe;
                            }
                            else
                            {
                                continue;
                            }

                            // сначала отписка на живом стриме, потом удаление из списка:
                            // список — источник правды для ресабскрайба при реконнекте
                            if (stream.IsConnected
                                && stream.StreamClient != null)
                            {
                                _rateGateSubscribe.WaitToProceed();

                                Task writeTask = stream.StreamClient.RequestStream.WriteAsync(sub);

                                if (writeTask.Wait(_streamWaitTimeout) == false)
                                {
                                    ObserveTaskFault(writeTask);
                                    stream.IsConnected = false;
                                }
                            }

                            stream.Subscriptions.RemoveAt(j);
                        }
                    }

                    SendLogMessage("Autofollow. Unsubscribed " + security.Name, LogMessageType.System);
                }
            }
            catch (Exception ex)
            {
                SendLogMessage("Autofollow. Failed to unsubscribe " + security.Name + " " + ex.ToString(), LogMessageType.Error);
            }
        }

        public bool SubscribeNews()
        {
            return false;
        }

        public event Action<News> NewsEvent { add { } remove { } }

        public event Action<MarketDepth> MarketDepthEvent;

        public event Action<Trade> NewTradesEvent;

        #endregion

        #region 9 Reading messages from data streams

        private async Task ReadStream(MarketDataStreamWrapper streamWrapper)
        {
            // запоминаем клиент, которого читает именно этот таск.
            // После реконнекта в обёртке уже другой клиент,
            // и умирающий читатель не должен трогать состояние нового стрима
            AsyncDuplexStreamingCall<MarketDataRequest, MarketDataResponse> myClient = streamWrapper.StreamClient;

            if (myClient == null)
            {
                return;
            }

            try
            {
                await foreach (MarketDataResponse marketData in myClient.ResponseStream.ReadAllAsync(
                                   cancellationToken: _cancellationTokenSource.Token))
                {
                    streamWrapper.LastMessageTime = DateTime.UtcNow;
                    ProcessMarketDataResponse(marketData);
                }

                // сервер штатно закрыл стрим без исключения
                if (ReferenceEquals(streamWrapper.StreamClient, myClient))
                {
                    streamWrapper.IsConnected = false;
                }
            }
            catch (Exception ex)
            {
                bool isCancelled =
                    ex is OperationCanceledException
                    || (ex is RpcException rpcEx && rpcEx.StatusCode == StatusCode.Cancelled);

                if (ReferenceEquals(streamWrapper.StreamClient, myClient) == false)
                {
                    // реконнект уже произошёл: исключение относится к старому клиенту
                    if (isCancelled == false)
                    {
                        SendLogMessage("TInvestAutoFollow stream " + streamWrapper.Name
                            + " exception: " + ex.Message, LogMessageType.System);
                    }
                    return;
                }

                if (isCancelled && _isDisposedNow)
                {
                    // штатная остановка коннектора
                    return;
                }

                SendLogMessage("TInvestAutoFollow stream " + streamWrapper.Name
                    + " exception: " + ex.Message, LogMessageType.System);
                streamWrapper.IsConnected = false;
            }
        }

        private void ProcessMarketDataResponse(MarketDataResponse marketData)
        {
            try
            {
                if (marketData.Trade != null)
                {
                    Tinkoff.InvestApi.V1.Trade trade = marketData.Trade;
                    Security security = GetSecurityByIdFast(trade.InstrumentUid);

                    if (security == null)
                    {
                        return;
                    }

                    Trade newTrade = new Trade();
                    newTrade.SecurityNameCode = security.Name;
                    newTrade.Price = GetValue(trade.Price);
                    newTrade.Volume = trade.Quantity;
                    newTrade.Time = TimeZoneInfo.ConvertTimeFromUtc(trade.Time.ToDateTime(), _mskTimeZone);

                    // время сервера знаем только по таймстемпам рыночных данных
                    ServerTime = newTrade.Time;
                    newTrade.Id = newTrade.Time.Ticks.ToString();
                    newTrade.Side = trade.Direction == TradeDirection.Buy ? Side.Buy : Side.Sell;

                    if (security.SecurityType == SecurityType.Bond
                        && security.NominalCurrent != 0)
                    {
                        newTrade.Price = newTrade.Price / 100 * security.NominalCurrent;
                    }

                    lock (_lastPricesLocker)
                    {
                        // цены нужны для определения типа отложенного сигнала (стоп-лосс / тейк-профит).
                        // Ключ — uid: тикеры могут совпадать у бумаг разных классов
                        _lastMarketPrices[security.NameId] = newTrade.Price;
                    }

                    NewTradesEvent?.Invoke(newTrade);
                }
                else if (marketData.Orderbook != null)
                {
                    OrderBook orderbook = marketData.Orderbook;
                    Security security = GetSecurityByIdFast(orderbook.InstrumentUid);

                    if (security == null)
                    {
                        return;
                    }

                    bool isBondNeedToNormalization = false;

                    if (security.SecurityType == SecurityType.Bond
                     && security.NominalCurrent != 0)
                    {
                        isBondNeedToNormalization = true;
                    }

                    MarketDepth depth = new MarketDepth();
                    depth.SecurityNameCode = security.Name;
                    depth.Time = GetMonotonicMarketDepthTime(security.NameId,
                        TimeZoneInfo.ConvertTimeFromUtc(orderbook.Time.ToDateTime(), _mskTimeZone));

                    // время сервера знаем только по таймстемпам рыночных данных
                    ServerTime = depth.Time;

                    depth.Bids = new List<MarketDepthLevel>(orderbook.Bids.Count);

                    foreach (Tinkoff.InvestApi.V1.Order bid in orderbook.Bids)
                    {
                        if (isBondNeedToNormalization)
                        {
                            depth.Bids.Add(new MarketDepthLevel
                            {
                                Price = (double)(GetValue(bid.Price) / 100 * security.NominalCurrent),
                                Bid = (double)bid.Quantity
                            });
                        }
                        else
                        {
                            depth.Bids.Add(new MarketDepthLevel { Price = (double)GetValue(bid.Price), Bid = (double)bid.Quantity });
                        }
                    }

                    depth.Asks = new List<MarketDepthLevel>(orderbook.Asks.Count);

                    foreach (Tinkoff.InvestApi.V1.Order ask in orderbook.Asks)
                    {
                        if (isBondNeedToNormalization)
                        {
                            depth.Asks.Add(new MarketDepthLevel
                            {
                                Price = (double)(GetValue(ask.Price) / 100 * security.NominalCurrent),
                                Ask = (double)ask.Quantity
                            });
                        }
                        else
                        {
                            depth.Asks.Add(new MarketDepthLevel { Price = (double)GetValue(ask.Price), Ask = (double)ask.Quantity });
                        }
                    }

                    // односторонний стакан (аукцион/халт) роботам не нужен
                    if (depth.Asks.Count > 0 && depth.Bids.Count > 0)
                    {
                        MarketDepthEvent?.Invoke(depth);
                    }

                    // проверяем локальные лимитки по лучшим ценам стакана
                    if (depth.Bids.Count > 0 && depth.Asks.Count > 0)
                    {
                        CheckPendingLimitOrders(security,
                            (decimal)depth.Bids[0].Price, (decimal)depth.Asks[0].Price);
                    }
                }
                else if (marketData.SubscribeTradesResponse != null)
                {
                    foreach (TradeSubscription sub in marketData.SubscribeTradesResponse.TradeSubscriptions)
                    {
                        if (sub.SubscriptionStatus != SubscriptionStatus.Success)
                        {
                            Security security = GetSecurityByIdFast(sub.InstrumentUid);
                            SendLogMessage("Autofollow. Failed to subscribe "
                                + (security != null ? security.Name : sub.InstrumentUid)
                                + " " + sub.SubscriptionStatus, LogMessageType.Error);
                        }
                    }
                }
                else if (marketData.SubscribeOrderBookResponse != null)
                {
                    foreach (OrderBookSubscription sub in marketData.SubscribeOrderBookResponse.OrderBookSubscriptions)
                    {
                        if (sub.SubscriptionStatus != SubscriptionStatus.Success)
                        {
                            Security security = GetSecurityByIdFast(sub.InstrumentUid);
                            SendLogMessage("Autofollow. Failed to subscribe "
                                + (security != null ? security.Name : sub.InstrumentUid)
                                + " " + sub.SubscriptionStatus, LogMessageType.Error);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                SendLogMessage("Error processing market data response: " + ex.ToString(), LogMessageType.Error);
            }
        }

        // Поток опроса портфелей стратегий. У API автоследования нет стримов, только опрос
        private void PortfolioMessageReader()
        {
            Thread.Sleep(1000);

            while (true)
            {
                try
                {
                    if (ServerStatus != ServerConnectStatus.Connect
                        || _httpClient == null)
                    {
                        Thread.Sleep(1000);
                        continue;
                    }

                    GetPortfolios();

                    Thread.Sleep(30000);
                }
                catch (Exception ex)
                {
                    SendLogMessage(ex.ToString(), LogMessageType.System);
                    Thread.Sleep(5000);
                }
            }
        }

        // Поток опроса сигналов стратегий
        private void SignalsMessageReader()
        {
            Thread.Sleep(1000);

            while (true)
            {
                try
                {
                    if (ServerStatus != ServerConnectStatus.Connect
                        || _httpClient == null)
                    {
                        Thread.Sleep(1000);
                        continue;
                    }

                    UpdateSignals();

                    Thread.Sleep(3000);
                }
                catch (Exception ex)
                {
                    SendLogMessage(ex.ToString(), LogMessageType.System);
                    Thread.Sleep(5000);
                }
            }
        }

        private void UpdateSignals()
        {
            List<Portfolio> portfolios;

            lock (_portfoliosLocker)
            {
                portfolios = new List<Portfolio>(_myPortfolios);
            }

            HashSet<string> seenThisRound = new HashSet<string>();
            HashSet<string> successfulStrategies = new HashSet<string>();

            // снимок ордеров, чьи POST-запросы в пути: их сигналы брокер уже
            // мог создать, но мы ещё не успели их зарегистрировать — по таким
            // сигналам нельзя создавать новый ордер, будет дубль
            List<Order> sendingSnapshot;

            lock (_sendingOrdersLocker)
            {
                sendingSnapshot = new List<Order>(_sendingOrders.Values);
            }

            for (int i = 0; i < portfolios.Count; i++)
            {
                string strategyId = portfolios[i].Number;
                bool success = true;

                List<AfSignal> signals = GetSignals(strategyId);

                if (signals == null)
                {
                    success = false;
                }
                else
                {
                    for (int j = 0; j < signals.Count; j++)
                    {
                        AfSignal signal = signals[j];

                        if (string.IsNullOrEmpty(signal.signalId))
                        {
                            continue;
                        }

                        seenThisRound.Add(signal.signalId);
                        ProcessSignal(signal, strategyId, sendingSnapshot);
                    }
                }

                List<AfStopSignal> stopSignals = GetStopSignals(strategyId);

                if (stopSignals == null)
                {
                    success = false;
                }
                else
                {
                    for (int j = 0; j < stopSignals.Count; j++)
                    {
                        AfStopSignal signal = stopSignals[j];

                        if (string.IsNullOrEmpty(signal.stopSignalId))
                        {
                            continue;
                        }

                        seenThisRound.Add(signal.stopSignalId);
                        ProcessStopSignal(signal, strategyId, sendingSnapshot);
                    }
                }

                if (success)
                {
                    successfulStrategies.Add(strategyId);
                }
            }

            // сигналы, исчезнувшие из списков активных, считаем исполненными.
            // Снимаем только по стратегиям, чей опрос прошёл успешно — 500 на одной
            // стратегии не должен массово закрыть её ордера
            RemoveDisappearedSignals(seenThisRound, successfulStrategies);
        }

        private void RememberCancelledSignal(string signalId)
        {
            // вызывается под _signalOrdersLocker
            _cancelledSignalTombstones[signalId] = DateTime.UtcNow;

            if (_cancelledSignalTombstones.Count > 50)
            {
                // чистим протухшие, словарь микроскопический
                DateTime limit = DateTime.UtcNow - _cancelTombstoneTtl;
                List<string> expired = new List<string>();

                foreach (KeyValuePair<string, DateTime> pair in _cancelledSignalTombstones)
                {
                    if (pair.Value < limit)
                    {
                        expired.Add(pair.Key);
                    }
                }

                for (int i = 0; i < expired.Count; i++)
                {
                    _cancelledSignalTombstones.Remove(expired[i]);
                }
            }
        }

        private bool IsCancelledSignalTombstoned(string signalId)
        {
            // вызывается под _signalOrdersLocker
            DateTime cancelledAtUtc;

            if (_cancelledSignalTombstones.TryGetValue(signalId, out cancelledAtUtc) == false)
            {
                return false;
            }

            if (cancelledAtUtc + _cancelTombstoneTtl < DateTime.UtcNow)
            {
                // TTL истёк: если брокер до сих пор отдаёт сигнал — отмена на
                // самом деле не прошла, принимаем его как новый
                _cancelledSignalTombstones.Remove(signalId);
                return false;
            }

            return true;
        }

        private bool HasSendingOrderFor(List<Order> sendingSnapshot, string strategyId,
            string ticker, Side side, decimal volume)
        {
            // свежий неизвестный сигнал может быть нашим собственным,
            // чей POST-ответ ещё не обработан. Совпадение — по стратегии,
            // бумаге, направлению и объёму одним кругом опроса
            for (int i = 0; i < sendingSnapshot.Count; i++)
            {
                Order sending = sendingSnapshot[i];

                if (sending.PortfolioNumber == strategyId
                    && sending.SecurityNameCode == ticker
                    && sending.Side == side
                    && sending.Volume == volume)
                {
                    return true;
                }
            }

            return false;
        }

        private void ProcessSignal(AfSignal signal, string strategyId, List<Order> sendingSnapshot)
        {
            decimal lotsRequested = signal.lotsRequested.ToDecimal();
            decimal lotsExecuted = signal.lotsExecuted.ToDecimal();
            decimal totalAmount = signal.totalAmount.ToDecimal();

            Order order = null;
            bool isNew = false;
            decimal prevTotalAmount = 0;

            lock (_signalOrdersLocker)
            {
                if (_activeSignalOrders.TryGetValue(signal.signalId, out order) == false)
                {
                    if (IsCancelledSignalTombstoned(signal.signalId))
                    {
                        // недавно отменённый нами сигнал из кэша брокера — не воскрешаем
                        if (_fullLog)
                        {
                            SendLogMessage("Signal " + signal.signalId + " was cancelled by us recently. Skipping "
                                + signal.ticker, LogMessageType.System);
                        }

                        return;
                    }

                    Side side = signal.direction == "buy" ? Side.Buy : Side.Sell;

                    if (HasSendingOrderFor(sendingSnapshot, strategyId, signal.ticker, side, lotsRequested))
                    {
                        // это наш собственный сигнал, чей POST-ответ ещё в пути.
                        // Ордер по нему зарегистрирует отправитель — пропускаем раунд
                        if (_fullLog)
                        {
                            SendLogMessage("Signal " + signal.signalId + " matches a pending POST. Skipping this round "
                                + signal.ticker, LogMessageType.System);
                        }

                        return;
                    }

                    order = new Order();
                    order.NumberUser = NumberGen.GetNumberOrder(StartProgram.IsOsTrader);
                    order.NumberMarket = signal.signalId;
                    order.SecurityNameCode = signal.ticker;
                    order.PortfolioNumber = strategyId;
                    order.Side = side;
                    order.TypeOrder = OrderPriceType.Market;
                    order.Volume = lotsRequested;
                    order.TimeCreate = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, _mskTimeZone);
                    order.TimeCallBack = order.TimeCreate;

                    Security security = GetSecurityByIdFast(signal.instrumentUid);

                    if (security == null)
                    {
                        security = _securities.Find(s => s.Name == signal.ticker);
                    }

                    if (security != null)
                    {
                        order.SecurityClassCode = security.NameClass;
                    }

                    // цены у сигнала автоследования нет
                    order.Price = 0;

                    _activeSignalOrders.Add(signal.signalId, order);
                    isNew = true;
                }
                else
                {
                    _signalTotalAmounts.TryGetValue(signal.signalId, out prevTotalAmount);
                }

                _signalTotalAmounts[signal.signalId] = totalAmount;
            }

            bool changed = false;
            decimal executedDelta = 0;

            if (isNew)
            {
                order.State = lotsExecuted > 0 ? OrderStateType.Partial : OrderStateType.Active;
                order.VolumeExecute = lotsExecuted;
                executedDelta = lotsExecuted;
                changed = true;
            }
            else if (order.VolumeExecute != lotsExecuted)
            {
                executedDelta = lotsExecuted - order.VolumeExecute;

                if (executedDelta < 0)
                {
                    // объём исполнения уменьшиться не может, но данные от брокера
                    // приоритетнее — выравниваем без эмиссии сделки
                    executedDelta = 0;
                }

                order.VolumeExecute = lotsExecuted;

                if (lotsExecuted > 0 && lotsExecuted < order.Volume)
                {
                    order.State = OrderStateType.Partial;
                }

                order.TimeCallBack = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, _mskTimeZone);
                changed = true;
            }

            if (changed)
            {
                if (_fullLog)
                {
                    SendLogMessage("Signal. Id: " + order.NumberMarket + ", " + order.SecurityNameCode
                        + " " + order.Side + ", lots " + order.VolumeExecute + "/" + order.Volume
                        + ", state: " + order.State, LogMessageType.System);
                }

                MyOrderEvent?.Invoke(order);

                SaveActiveSignals(strategyId);
            }

            if (executedDelta > 0)
            {
                Security security = GetSecurityByIdFast(signal.instrumentUid);

                if (security == null)
                {
                    security = _securities.Find(s => s.Name == signal.ticker);
                }

                // точный VWAP дельты исполнения из суммы сигнала
                decimal vwap = GetVwapFromAmountDelta(totalAmount - prevTotalAmount, executedDelta,
                    signal.currency, security);

                CreateMyTradeBySignal(order, executedDelta, vwap);
            }
        }

        private void ProcessStopSignal(AfStopSignal signal, string strategyId, List<Order> sendingSnapshot)
        {
            decimal lots = signal.lots.ToDecimal();
            decimal stopPrice = signal.stopPrice.ToDecimal();

            Order order = null;
            bool isNew = false;

            lock (_signalOrdersLocker)
            {
                if (_activeSignalOrders.TryGetValue(signal.stopSignalId, out order) == false)
                {
                    if (IsCancelledSignalTombstoned(signal.stopSignalId))
                    {
                        // недавно отменённый нами сигнал из кэша брокера — не воскрешаем
                        if (_fullLog)
                        {
                            SendLogMessage("Stop signal " + signal.stopSignalId + " was cancelled by us recently. Skipping "
                                + signal.ticker, LogMessageType.System);
                        }

                        return;
                    }

                    Side side = signal.direction == "buy" ? Side.Buy : Side.Sell;

                    if (HasSendingOrderFor(sendingSnapshot, strategyId, signal.ticker, side, lots))
                    {
                        // это наш собственный сигнал, чей POST-ответ ещё в пути.
                        // Ордер по нему зарегистрирует отправитель — пропускаем раунд
                        if (_fullLog)
                        {
                            SendLogMessage("Stop signal " + signal.stopSignalId + " matches a pending POST. Skipping this round "
                                + signal.ticker, LogMessageType.System);
                        }

                        return;
                    }

                    order = new Order();
                    order.NumberUser = NumberGen.GetNumberOrder(StartProgram.IsOsTrader);
                    order.NumberMarket = signal.stopSignalId;
                    order.SecurityNameCode = signal.ticker;
                    order.PortfolioNumber = strategyId;
                    order.Side = side;
                    order.TypeOrder = OrderPriceType.StopMarket;
                    order.Volume = lots;
                    order.StopPrice = stopPrice;
                    order.Price = stopPrice;
                    order.State = OrderStateType.Active;
                    order.TimeCreate = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, _mskTimeZone);
                    order.TimeCallBack = order.TimeCreate;

                    if (string.IsNullOrEmpty(signal.createDate) == false)
                    {
                        DateTimeOffset createTime;

                        if (DateTimeOffset.TryParse(signal.createDate, out createTime))
                        {
                            order.TimeCreate = TimeZoneInfo.ConvertTimeFromUtc(
                                createTime.UtcDateTime, _mskTimeZone);
                            order.TimeCallBack = order.TimeCreate;
                        }
                    }

                    if (string.IsNullOrEmpty(signal.expireDate) == false)
                    {
                        DateTimeOffset expireTime;

                        if (DateTimeOffset.TryParse(signal.expireDate, out expireTime))
                        {
                            _signalExpireDates[signal.stopSignalId] = expireTime.UtcDateTime;
                        }
                    }

                    Security security = GetSecurityByIdFast(signal.instrumentUid);

                    if (security == null)
                    {
                        security = _securities.Find(s => s.Name == signal.ticker);
                    }

                    if (security != null)
                    {
                        order.SecurityClassCode = security.NameClass;
                    }

                    _activeSignalOrders.Add(signal.stopSignalId, order);
                    isNew = true;
                }
            }

            if (isNew)
            {
                if (_fullLog)
                {
                    SendLogMessage("Stop signal. Id: " + order.NumberMarket + ", " + order.SecurityNameCode
                        + " " + order.Side + ", lots " + order.Volume + ", stop price " + order.StopPrice
                        + ", type: " + signal.stopOrderType, LogMessageType.System);
                }

                MyOrderEvent?.Invoke(order);

                SaveActiveSignals(strategyId);
            }
        }

        private decimal GetVwapFromAmountDelta(decimal amountDelta, decimal lotsDelta, string currency, Security security)
        {
            // VWAP дельты исполнения из суммы сигнала: цена = Δсумма / Δобъём в штуках.
            // Облигации исключаем (сумма может включать НКД), опционы исключаем
            // (стоимость шага не обогащена — коэффициент недостоверен).
            // У фьючерсов, торгуемых в пунктах, сумма приходит тоже в пунктах
            // (currency "pt.", подтверждено живыми ответами 06.10.2026) — конверсия
            // не нужна. У фьючерсов с денежной валютой сумма в валюте, а цена
            // в пунктах — перевод через стоимость шага
            if (amountDelta <= 0
                || lotsDelta <= 0
                || security == null
                || security.Lot <= 0)
            {
                return 0;
            }

            if (security.SecurityType == SecurityType.Bond
                || security.SecurityType == SecurityType.Option)
            {
                return 0;
            }

            decimal units = lotsDelta * security.Lot;
            decimal price = amountDelta / units;

            if (security.SecurityType == SecurityType.Futures
                && currency != "pt.")
            {
                if (security.PriceStepCost <= 0
                    || security.PriceStep <= 0)
                {
                    return 0;
                }

                price = price * security.PriceStep / security.PriceStepCost;
            }

            return price;
        }

        private void CreateMyTradeBySignal(Order order, decimal volume, decimal vwapPrice)
        {
            // исполнение сигнала приходит только как рост lotsExecuted в списке активных,
            // поэтому дельту отдаём движку как мою сделку — из них робот строит позицию.
            // Цены исполнения в API нет — иерархия: VWAP из суммы сигнала,
            // рыночная цена из потока, цена ордера

            Security security = GetSecurityByOrder(order);

            decimal streamPrice = security != null ? GetLastMarketPrice(security.NameId) : 0;

            decimal price = vwapPrice;

            if (price != 0
                && streamPrice != 0
                && Math.Abs(price - streamPrice) / streamPrice > 0.02m)
            {
                // семантика totalAmount документацией не подтверждена —
                // при сильном расхождении с рынком доверяем цене стрима
                SendLogMessage("Autofollow. VWAP from signal amount differs from market price. Order: "
                    + order.NumberMarket + " vwap " + price + " market " + streamPrice, LogMessageType.System);

                price = streamPrice;
            }

            if (price == 0)
            {
                price = streamPrice;
            }

            if (price == 0)
            {
                price = order.Price;
            }

            if (price == 0)
            {
                SendLogMessage("Autofollow. MyTrade with zero price. Order: " + order.NumberMarket + " "
                    + order.SecurityNameCode, LogMessageType.Error);
            }

            MyTrade trade = new MyTrade();
            trade.SecurityNameCode = order.SecurityNameCode;
            trade.NumberOrderParent = order.NumberMarket;
            trade.NumberTrade = order.NumberMarket + "_" + order.VolumeExecute
                .ToString(System.Globalization.CultureInfo.InvariantCulture);
            trade.Time = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, _mskTimeZone);
            trade.Price = price;
            trade.Volume = volume;
            trade.Side = order.Side;

            if (_fullLog)
            {
                SendLogMessage("MyTrade by signal. Order: " + order.NumberMarket + ", "
                    + trade.SecurityNameCode + " " + trade.Side + ", volume " + trade.Volume
                    + ", price " + trade.Price, LogMessageType.System);
            }

            RememberTrade(order, trade);

            MyTradeEvent?.Invoke(trade);
        }

        private void RemoveDisappearedSignals(HashSet<string> seenThisRound, HashSet<string> successfulStrategies)
        {
            List<Order> disappeared = new List<Order>();
            Dictionary<string, DateTime> disappearedExpireDates = new Dictionary<string, DateTime>();

            lock (_signalOrdersLocker)
            {
                List<string> keys = new List<string>(_activeSignalOrders.Keys);

                for (int i = 0; i < keys.Count; i++)
                {
                    string key = keys[i];
                    Order order = _activeSignalOrders[key];

                    if (successfulStrategies.Contains(order.PortfolioNumber) == false)
                    {
                        // по этой стратегии опрос не прошёл — не трогаем её ордера
                        continue;
                    }

                    if (seenThisRound.Contains(key) == false)
                    {
                        _activeSignalOrders.Remove(key);
                        _signalIdByNumberUser.Remove(order.NumberUser);
                        _signalTotalAmounts.Remove(key);

                        DateTime expireUtc;

                        if (_signalExpireDates.TryGetValue(key, out expireUtc))
                        {
                            _signalExpireDates.Remove(key);
                            disappearedExpireDates[key] = expireUtc;
                        }

                        disappeared.Add(order);
                    }
                }
            }

            HashSet<string> strategiesToSave = new HashSet<string>();

            for (int i = 0; i < disappeared.Count; i++)
            {
                Order order = disappeared[i];

                strategiesToSave.Add(order.PortfolioNumber);

                if (order.TypeOrder == OrderPriceType.StopMarket
                    || order.TypeOrder == OrderPriceType.StopLimit)
                {
                    DateTime expireUtc;

                    if (disappearedExpireDates.TryGetValue(order.NumberMarket, out expireUtc)
                        && DateTime.UtcNow > expireUtc)
                    {
                        // стоп-сигнал исчез после истечения expireDate — это отмена, а не исполнение
                        order.State = OrderStateType.Cancel;
                        order.TimeCallBack = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, _mskTimeZone);
                        order.TimeCancel = order.TimeCallBack;

                        SendLogMessage("Autofollow. Stop signal expired " + order.NumberMarket + " "
                            + order.SecurityNameCode, LogMessageType.System);

                        MyOrderEvent?.Invoke(order);
                        RememberCompletedOrder(order);
                        continue;
                    }
                }

                // API отдаёт только активные сигналы: исчезновение из списка,
                // которое не проходило через наш CancelOrder и не истекло по сроку,
                // считаем исполнением. Ручная отмена в терминале Т-Банка неотличима
                // от исполнения — ограничение API (истории сигналов нет)

                decimal executedDelta = order.Volume - order.VolumeExecute;

                order.State = OrderStateType.Done;
                order.VolumeExecute = order.Volume;
                order.TimeCallBack = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, _mskTimeZone);
                order.TimeDone = order.TimeCallBack;

                if (executedDelta > 0)
                {
                    // исполнение могло пройти между опросами — досчитываем его как сделку.
                    // Данных сигнала уже нет — VWAP недоступен, цена из потока
                    CreateMyTradeBySignal(order, executedDelta, 0);
                }

                SendLogMessage("Autofollow. Signal disappeared from active list. Executed or cancelled " + order.NumberMarket + " "
                    + order.SecurityNameCode + " " + order.VolumeExecute + "/" + order.Volume,
                    LogMessageType.System);

                MyOrderEvent?.Invoke(order);
                RememberCompletedOrder(order);
            }

            foreach (string strategyId in strategiesToSave)
            {
                SaveActiveSignals(strategyId);
            }
        }

        #endregion

        #region 10 Trade

        // лимит API автоследования: не чаще одного сигнала в 15 секунд.
        // Берём 18 с запасом: на ровных 15 брокер отвечал 429
        private RateGate _rateGateSendSignal = new RateGate(1, TimeSpan.FromSeconds(18));

        public void SendOrder(Order order)
        {
            try
            {
                if (ServerStatus != ServerConnectStatus.Connect
                    || _httpClient == null)
                {
                    CreateOrderFail(order, "Server is not connected.");
                    return;
                }

                Security security = GetSecurityByOrder(order);

                string validationError;

                if (ValidateSignalOrder(order, security, out validationError) == false)
                {
                    CreateOrderFail(order, validationError);
                    return;
                }

                if (order.NumberUser == 0)
                {
                    order.NumberUser = NumberGen.GetNumberOrder(StartProgram.IsOsTrader);
                }

                if (order.TypeOrder == OrderPriceType.Limit)
                {
                    // лимитных сигналов в API автоследования нет —
                    // держим заявку локально и шлём маркет-сигнал при достижении цены
                    RegisterPendingLimit(order, security);
                    return;
                }

                if (order.TypeOrder == OrderPriceType.StopLimit
                    || order.TypeOrder == OrderPriceType.StopMarket)
                {
                    // стопу нужны рыночные цены: тип сигнала (стоп-лосс/тейк-профит)
                    // выводится из положения стоп-цены относительно рынка
                    Subscribe(security);
                }

                // маркет- и стоп-сигналы уходят в очередь отправки (лимит 1 сигнал в 18 сек),
                // поток AServer при этом не блокируется. Номер даём временный, чтобы AServer
                // пропускал отмену до момента фактической отправки
                order.State = OrderStateType.Active;
                order.NumberMarket = "local_" + order.NumberUser;
                order.TimeCallBack = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, _mskTimeZone);

                MyOrderEvent?.Invoke(order);

                _signalsToSendQueue.Enqueue(order);
            }
            catch (Exception ex)
            {
                SendLogMessage("Autofollow. Error posting signal " + ex.ToString(), LogMessageType.Error);
                CreateOrderFail(order, ex.ToString());
            }
        }

        private bool ValidateSignalOrder(Order order, Security security, out string error)
        {
            // API автоследования не возвращает ошибок по кривой цене или объёму —
            // сигнал принимается молча, поэтому вся валидация локальная.
            // Жёсткие проверки — отказ (Fail), мягкие — warning в лог

            if (security == null)
            {
                error = "Autofollow. Security not found " + order.SecurityNameCode;
                return false;
            }

            if (order.Volume <= 0)
            {
                error = "Autofollow. Volume is zero " + order.SecurityNameCode;
                return false;
            }

            // объём — в лотах. Дробный лот брокер примет как fractional
            // и «исполнит» виртуально — без позиции на бирже
            if (order.Volume % 1 != 0)
            {
                error = "Autofollow. Volume is not a whole lot " + order.SecurityNameCode;
                return false;
            }

            if (string.IsNullOrEmpty(order.PortfolioNumber))
            {
                error = "Autofollow. Portfolio number. Strategy id is empty " + order.SecurityNameCode;
                return false;
            }

            if (order.TypeOrder == OrderPriceType.StopLimit
                || order.TypeOrder == OrderPriceType.StopMarket)
            {
                if (order.StopPrice <= 0)
                {
                    error = "Autofollow. Stop price is zero " + order.SecurityNameCode;
                    return false;
                }

                CheckStopPriceSideWarning(order, security);
            }
            else if (order.TypeOrder == OrderPriceType.Limit
                && order.Price <= 0)
            {
                error = "Autofollow. Limit price is zero " + order.SecurityNameCode;
                return false;
            }

            if (order.TypeOrder != OrderPriceType.Market)
            {
                CheckPriceStepWarning(order, security);
            }

            if (order.Side == Side.Sell)
            {
                CheckSellExceedsPositionWarning(order, security);
            }

            error = null;
            return true;
        }

        private void CheckPriceStepWarning(Order order, Security security)
        {
            decimal price = order.TypeOrder == OrderPriceType.Limit
                ? order.Price
                : order.StopPrice;

            if (security.PriceStep > 0
                && price % security.PriceStep != 0)
            {
                SendLogMessage("Autofollow. Price is not aligned to price step " + order.SecurityNameCode
                    + " price " + price + " step " + security.PriceStep, LogMessageType.System);
            }
        }

        private void CheckStopPriceSideWarning(Order order, Security security)
        {
            // стоп-цена на исполняемой стороне рынка — сигнал сработает мгновенно
            decimal lastPrice = GetLastMarketPrice(security.NameId);

            if (lastPrice == 0)
            {
                return;
            }

            bool executableNow =
                order.Side == Side.Sell && order.StopPrice >= lastPrice
                || order.Side == Side.Buy && order.StopPrice <= lastPrice;

            if (executableNow)
            {
                SendLogMessage("Autofollow. Stop price is on the executable side of the market. Signal may trigger immediately "
                    + order.SecurityNameCode + " stop " + order.StopPrice + " last " + lastPrice,
                    LogMessageType.System);
            }
        }

        private void CheckSellExceedsPositionWarning(Order order, Security security)
        {
            // шорт по срочному рынку разрешён всегда — там продажа сверх позиции норма
            if (security.SecurityType == SecurityType.Futures
                || security.SecurityType == SecurityType.Option)
            {
                return;
            }

            decimal position = 0;

            lock (_portfoliosLocker)
            {
                Portfolio portfolio = _myPortfolios.Find(p => p.Number == order.PortfolioNumber);

                if (portfolio != null
                    && portfolio.PositionOnBoard != null)
                {
                    PositionOnBoard pos = portfolio.PositionOnBoard.Find(p => p.SecurityNameCode == security.Name);

                    if (pos != null)
                    {
                        position = pos.ValueCurrent;
                    }
                }
            }

            if (order.Volume <= position)
            {
                return;
            }

            string shortInfo = "short flag unknown";

            lock (_shortFlagsLocker)
            {
                bool shortEnabled;

                if (_shortEnabledByNameId.TryGetValue(security.NameId, out shortEnabled))
                {
                    shortInfo = shortEnabled ? "short allowed" : "short NOT allowed";
                }
            }

            SendLogMessage("Autofollow. Sell volume exceeds known position " + order.SecurityNameCode
                + " volume " + order.Volume + " position " + position + ". " + shortInfo
                + ". Positions are polled with delay", LogMessageType.System);
        }

        private void SendMarketSignal(Order order, Security security)
        {
            try
            {
                AfPostSignalRequest request = new AfPostSignalRequest();
                request.instrumentId = security.NameId;
                request.direction = order.Side == Side.Buy ? "buy" : "sell";
                request.lots = order.Volume;

                string postError;
                AfPostSignalResponse response = PostSignal(order.PortfolioNumber, request, out postError);

                if (response == null
                    || string.IsNullOrEmpty(response.signalId))
                {
                    if (IsRetryablePostError(postError))
                    {
                        // транзитная ошибка брокера — кладём обратно в очередь,
                        // повтор через RateGate (подробность уже в логе из PostSignal)
                        SendLogMessage("Autofollow. Error posting signal. Retry later. "
                            + order.SecurityNameCode, LogMessageType.System);
                        _signalsToSendQueue.Enqueue(order);

                        return;
                    }

                    CreateOrderFail(order, "Autofollow. Error posting signal " + order.SecurityNameCode);
                    return;
                }

                order.State = response.requestAcceptanceType == "fractional"
                    ? OrderStateType.Partial
                    : OrderStateType.Active;
                order.TimeCallBack = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, _mskTimeZone);

                // NumberMarket остаётся local_ навсегда: журнал и тесты привязывают
                // сделки по первому номеру ордера. Связь с сигналом — в словаре,
                // опрос подхватит сигнал по signalId и не создаст дубль
                lock (_signalOrdersLocker)
                {
                    _signalIdByNumberUser[order.NumberUser] = response.signalId;

                    if (_activeSignalOrders.ContainsKey(response.signalId) == false)
                    {
                        _activeSignalOrders.Add(response.signalId, order);
                    }
                }

                SaveActiveSignals(order.PortfolioNumber);

                SendLogMessage("Autofollow. Signal posted " + response.signalId + " "
                    + order.SecurityNameCode + " " + order.Side + " " + order.Volume
                    + " " + response.requestAcceptanceType, LogMessageType.Trade);

                MyOrderEvent?.Invoke(order);
            }
            catch (Exception ex)
            {
                SendLogMessage("Autofollow. Error posting signal " + ex.ToString(), LogMessageType.Error);
                CreateOrderFail(order, ex.ToString());
            }
        }

        private void RegisterPendingLimit(Order order, Security security)
        {
            order.State = OrderStateType.Active;
            order.TimeCreate = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, _mskTimeZone);
            order.TimeCallBack = order.TimeCreate;

            // локальный номер нужен: AServer не отдаёт отмену/изменение цены без NumberMarket
            order.NumberMarket = "local_" + order.NumberUser;

            lock (_pendingLimitsLocker)
            {
                _pendingLimitOrders.Add(order);
            }

            // подписываемся на рыночные данные бумаги, чтобы увидеть триггер
            Subscribe(security);

            SendLogMessage("Autofollow. Limit order registered locally. Waiting for trigger price " + order.SecurityNameCode
                + " " + order.Side + " " + order.Volume + " " + order.Price, LogMessageType.Trade);

            MyOrderEvent?.Invoke(order);
        }

        private void CheckPendingLimitOrders(Security security, decimal bestBid, decimal bestAsk)
        {
            List<Order> triggered = null;

            lock (_pendingLimitsLocker)
            {
                for (int i = 0; i < _pendingLimitOrders.Count; i++)
                {
                    Order order = _pendingLimitOrders[i];

                    if (order.SecurityNameCode != security.Name)
                    {
                        continue;
                    }

                    if (string.IsNullOrEmpty(order.SecurityClassCode) == false
                        && order.SecurityClassCode != security.NameClass)
                    {
                        // одноимённая бумага из другого класса — не наша
                        continue;
                    }

                    bool isTriggered = false;

                    if (order.Side == Side.Buy
                        && bestAsk > 0
                        && bestAsk <= order.Price)
                    {
                        isTriggered = true; // покупка по лимиту исполнилась бы по лучшему аску
                    }
                    else if (order.Side == Side.Sell
                        && bestBid > 0
                        && bestBid >= order.Price)
                    {
                        isTriggered = true; // продажа по лимиту исполнилась бы по лучшему биду
                    }

                    if (isTriggered)
                    {
                        _pendingLimitOrders.RemoveAt(i);
                        i--;

                        if (triggered == null)
                        {
                            triggered = new List<Order>();
                        }

                        triggered.Add(order);
                    }
                }
            }

            if (triggered == null)
            {
                return;
            }

            // порядок исполнения — по логике движения цены, а не по порядку
            // выставления: покупки сверху вниз, продажи снизу вверх — как
            // уровни реально пробивались
            triggered.Sort((a, b) =>
            {
                if (a.Side != b.Side)
                {
                    return a.Side == Side.Buy ? -1 : 1;
                }

                return a.Side == Side.Buy
                    ? b.Price.CompareTo(a.Price)
                    : a.Price.CompareTo(b.Price);
            });

            for (int i = 0; i < triggered.Count; i++)
            {
                Order order = triggered[i];

                SendLogMessage("Autofollow. Limit order triggered. Sending market signal " + order.SecurityNameCode
                    + " " + order.Side + " " + order.Volume + " " + order.Price, LogMessageType.Trade);

                _signalsToSendQueue.Enqueue(order);
            }
        }

        private void PendingLimitSenderThread()
        {
            Thread.Sleep(1000);

            while (true)
            {
                try
                {
                    if (ServerStatus != ServerConnectStatus.Connect
                        || _httpClient == null)
                    {
                        Thread.Sleep(1000);
                        continue;
                    }

                    Order order = null;

                    if (_signalsToSendQueue.TryDequeue(out order) == false)
                    {
                        Thread.Sleep(100);
                        continue;
                    }

                    if (order.State == OrderStateType.Cancel)
                    {
                        // отменён, пока ждал в очереди — не отправляем
                        continue;
                    }

                    Security security = GetSecurityByOrder(order);

                    if (security == null)
                    {
                        CreateOrderFail(order, "Autofollow. Security not found " + order.SecurityNameCode);
                        continue;
                    }

                    // помечаем как отправляемый: локальная отмена в этом состоянии
                    // невозможна — сигнал уже на пути к брокеру
                    lock (_sendingOrdersLocker)
                    {
                        _sendingOrders[order.NumberUser] = order;
                    }

                    try
                    {
                        if (order.TypeOrder == OrderPriceType.StopLimit
                            || order.TypeOrder == OrderPriceType.StopMarket)
                        {
                            SendStopSignal(order, security);
                        }
                        else
                        {
                            SendMarketSignal(order, security);
                        }
                    }
                    finally
                    {
                        lock (_sendingOrdersLocker)
                        {
                            _sendingOrders.Remove(order.NumberUser);
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

        private void SaveActiveSignals(string strategyId)
        {
            try
            {
                if (string.IsNullOrEmpty(strategyId))
                {
                    return;
                }

                lock (_signalOrdersLocker)
                {
                    Directory.CreateDirectory(@"Engine\TInvestAutoFollow");

                    using (StreamWriter writer = new StreamWriter(
                        @"Engine\TInvestAutoFollow\ActiveSignals_" + strategyId + ".txt", false))
                    {
                        foreach (KeyValuePair<string, Order> pair in _activeSignalOrders)
                        {
                            Order order = pair.Value;

                            if (order.PortfolioNumber != strategyId)
                            {
                                continue;
                            }

                            long expireTicks = 0;

                            DateTime expireUtc;

                            if (_signalExpireDates.TryGetValue(pair.Key, out expireUtc))
                            {
                                expireTicks = expireUtc.Ticks;
                            }

                            writer.WriteLine(pair.Key + "%"
                                + order.SecurityNameCode + "%"
                                + order.SecurityClassCode + "%"
                                + order.Side + "%"
                                + order.Volume.ToString(System.Globalization.CultureInfo.InvariantCulture) + "%"
                                + order.VolumeExecute.ToString(System.Globalization.CultureInfo.InvariantCulture) + "%"
                                + order.TypeOrder + "%"
                                + order.StopPrice.ToString(System.Globalization.CultureInfo.InvariantCulture) + "%"
                                + order.NumberUser + "%"
                                + order.TimeCreate.Ticks + "%"
                                + expireTicks);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                SendLogMessage(ex.ToString(), LogMessageType.Error);
            }
        }

        private void RestoreActiveSignals(List<AfStrategy> strategies)
        {
            if (_activeSignalsRestored)
            {
                return;
            }

            try
            {
                for (int i = 0; i < strategies.Count; i++)
                {
                    string strategyId = strategies[i].strategyId;

                    if (string.IsNullOrEmpty(strategyId))
                    {
                        continue;
                    }

                    string path = @"Engine\TInvestAutoFollow\ActiveSignals_" + strategyId + ".txt";

                    if (File.Exists(path) == false)
                    {
                        continue;
                    }

                    string[] lines = File.ReadAllLines(path);

                    for (int j = 0; j < lines.Length; j++)
                    {
                        string[] parts = lines[j].Split('%');

                        if (parts.Length < 11)
                        {
                            continue;
                        }

                        string signalId = parts[0];

                        Order order = new Order();
                        order.NumberMarket = signalId;
                        order.SecurityNameCode = parts[1];
                        order.SecurityClassCode = parts[2];
                        order.Side = parts[3] == "Buy" ? Side.Buy : Side.Sell;
                        order.Volume = parts[4].ToDecimal();
                        order.TypeOrder = parts[6] == "StopMarket" ? OrderPriceType.StopMarket
                            : parts[6] == "StopLimit" ? OrderPriceType.StopLimit
                            : OrderPriceType.Market;
                        order.StopPrice = parts[7].ToDecimal();

                        if (order.TypeOrder != OrderPriceType.Market)
                        {
                            order.Price = order.StopPrice;
                        }

                        order.NumberUser = Convert.ToInt32(parts[8]);
                        order.TimeCreate = new DateTime(Convert.ToInt64(parts[9]));
                        order.TimeCallBack = order.TimeCreate;
                        order.PortfolioNumber = strategyId;

                        // восстанавливаем молча: хаб заберёт ордера через GetAllActivOrders,
                        // сделки за уже исполненный объём не эмитим —
                        // робот поднял их из своего журнала
                        decimal volumeExecute = parts[5].ToDecimal();

                        order.VolumeExecute = volumeExecute;
                        order.State = volumeExecute > 0 && volumeExecute < order.Volume
                            ? OrderStateType.Partial
                            : OrderStateType.Active;

                        long expireTicks = Convert.ToInt64(parts[10]);

                        lock (_signalOrdersLocker)
                        {
                            if (_activeSignalOrders.ContainsKey(signalId))
                            {
                                continue;
                            }

                            _activeSignalOrders.Add(signalId, order);

                            if (expireTicks > 0)
                            {
                                _signalExpireDates[signalId] = new DateTime(expireTicks, DateTimeKind.Utc);
                            }
                        }

                        SendLogMessage("Autofollow. Signal restored from file " + signalId + " "
                            + order.SecurityNameCode + " " + order.Side + " "
                            + order.VolumeExecute + "/" + order.Volume, LogMessageType.System);
                    }
                }

                // чистим файлы стратегий, которых у брокера больше нет —
                // иначе мусор копится бесконечно
                string directory = @"Engine\TInvestAutoFollow";

                if (Directory.Exists(directory))
                {
                    string[] files = Directory.GetFiles(directory, "ActiveSignals_*.txt");

                    for (int i = 0; i < files.Length; i++)
                    {
                        string fileStrategyId = Path.GetFileNameWithoutExtension(files[i])
                            .Substring("ActiveSignals_".Length);

                        bool alive = false;

                        for (int j = 0; j < strategies.Count; j++)
                        {
                            if (strategies[j].strategyId == fileStrategyId)
                            {
                                alive = true;
                                break;
                            }
                        }

                        if (alive == false)
                        {
                            File.Delete(files[i]);

                            SendLogMessage("Autofollow. Removed signals file of deleted strategy "
                                + fileStrategyId, LogMessageType.System);
                        }
                    }
                }

                // флаг — только после полного прохода: при исключении посередине
                // следующий Connect повторит восстановление (ContainsKey отсекает дубли)
                _activeSignalsRestored = true;
            }
            catch (Exception ex)
            {
                SendLogMessage(ex.ToString(), LogMessageType.Error);
            }
        }

        private void SendStopSignal(Order order, Security security)
        {
            // валидация уже пройдена в SendOrder (ValidateSignalOrder)
            AfPostStopSignalRequest request = new AfPostStopSignalRequest();
            request.instrumentId = security.NameId;
            request.direction = order.Side == Side.Buy ? "buy" : "sell";
            request.lots = order.Volume;
            request.stopPrice = order.StopPrice;
            request.stopOrderType = GetStopOrderType(order, security);

            // срок жизни стопа по соглашению платформы (как у основного TInvest):
            // Specified — сейчас + LifeTime, Day — до конца текущего дня (МСК).
            // GTC: далёкая дата отклоняется брокером (422 «Некорректная дата
            // окончания отложенного сигнала», проверено 06.10.2026) — поле
            // опускаем (сериализация с NullValueHandling.Ignore), действует
            // дефолт брокера
            if (order.OrderTypeTime == OrderTypeTime.Specified
                && order.LifeTime != TimeSpan.Zero)
            {
                DateTime expireUtc = DateTime.UtcNow + order.LifeTime;
                request.expireDate = expireUtc.ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'");
            }
            else if (order.OrderTypeTime == OrderTypeTime.Day)
            {
                DateTime mskNow = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, _mskTimeZone);
                DateTime endOfDayMsk = mskNow.Date.AddDays(1).AddSeconds(-1);
                DateTime expireUtc = TimeZoneInfo.ConvertTimeToUtc(
                    DateTime.SpecifyKind(endOfDayMsk, DateTimeKind.Unspecified), _mskTimeZone);
                request.expireDate = expireUtc.ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'");
            }

            string postError;
            AfPostStopSignalResponse response = PostStopSignal(order.PortfolioNumber, request, out postError);

            if (response == null
                || string.IsNullOrEmpty(response.stopSignalId))
            {
                if (IsRetryablePostError(postError))
                {
                    // транзитная ошибка брокера — кладём обратно в очередь,
                    // повтор через RateGate (подробность уже в логе из PostSignal)
                    SendLogMessage("Autofollow. Error posting signal. Retry later. "
                        + order.SecurityNameCode, LogMessageType.System);
                    _signalsToSendQueue.Enqueue(order);

                    return;
                }

                // expireDate в лог: брокер отклоняет сигнал по дате окончания
                // («Некорректная дата...»), без неё причину не различить
                CreateOrderFail(order, "Autofollow. Error posting signal " + order.SecurityNameCode
                    + ". Expire date was " + request.expireDate);
                return;
            }

            order.State = OrderStateType.Active;
            order.TimeCallBack = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, _mskTimeZone);

            lock (_signalOrdersLocker)
            {
                _signalIdByNumberUser[order.NumberUser] = response.stopSignalId;

                if (_activeSignalOrders.ContainsKey(response.stopSignalId) == false)
                {
                    _activeSignalOrders.Add(response.stopSignalId, order);
                }
            }

            SaveActiveSignals(order.PortfolioNumber);

            SendLogMessage("Autofollow. Signal posted " + response.stopSignalId + " "
                + order.SecurityNameCode + " " + order.Side + " " + order.Volume
                + " " + request.stopOrderType, LogMessageType.Trade);

            MyOrderEvent?.Invoke(order);
        }

        private void CreateOrderFail(Order order, string message)
        {
            SendLogMessage(message + " " + order.SecurityNameCode
                + " " + order.Side + " " + order.Volume, LogMessageType.Error);

            order.State = OrderStateType.Fail;

            MyOrderEvent?.Invoke(order);
            RememberCompletedOrder(order);
        }

        public void ChangeOrderPrice(Order order, decimal newPrice)
        {
            // API автоследования изменение цены сигнала не поддерживает
            // (permission IsCanChangeOrderPrice = false), движок сюда не зовёт
            SendLogMessage("Autofollow. Change order price is not supported " + order.SecurityNameCode,
                LogMessageType.System);
        }

        public bool CancelOrder(Order order)
        {
            try
            {
                if (ServerStatus != ServerConnectStatus.Connect
                    || _httpClient == null)
                {
                    return false;
                }

                // локальный ордер (несработавшая лимитка или ещё не отправленный маркет-сигнал) —
                // снимаем локально, без вызова API
                if (order.NumberMarket != null
                    && order.NumberMarket.StartsWith("local_"))
                {
                    bool removed = false;

                    lock (_pendingLimitsLocker)
                    {
                        for (int i = 0; i < _pendingLimitOrders.Count; i++)
                        {
                            if (_pendingLimitOrders[i].NumberUser == order.NumberUser)
                            {
                                _pendingLimitOrders.RemoveAt(i);
                                removed = true;
                                break;
                            }
                        }
                    }

                    if (removed == false)
                    {
                        bool isSending;

                        lock (_sendingOrdersLocker)
                        {
                            isSending = _sendingOrders.ContainsKey(order.NumberUser);
                        }

                        if (isSending)
                        {
                            // сигнал уже на пути к брокеру — локально не отменяем.
                            // Робот повторит отмену: либо сигнал вернётся в очередь (429/VWAP),
                            // либо запишется в словарь сигналов и отмена уйдёт через DELETE
                            SendLogMessage("Autofollow. Signal is being sent to broker. Try to cancel later " + order.SecurityNameCode, LogMessageType.System);
                            return false;
                        }

                        // сигнал уже у брокера — снимаем через DELETE по связке из словаря
                        string signalId = null;

                        lock (_signalOrdersLocker)
                        {
                            _signalIdByNumberUser.TryGetValue(order.NumberUser, out signalId);
                        }

                        if (signalId != null)
                        {
                            return CancelPostedSignal(order, signalId);
                        }
                    }

                    // ждущего в очереди отправщик пропустит как отменённого,
                    // потерянного подтверждаем — иначе робот будет крутить отмену бесконечно
                    order.State = OrderStateType.Cancel;
                    order.TimeCallBack = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, _mskTimeZone);
                    order.TimeCancel = order.TimeCallBack;

                    SendLogMessage("Autofollow. Signal cancelled " + order.SecurityNameCode, LogMessageType.Trade);

                    MyOrderEvent?.Invoke(order);
                    RememberCompletedOrder(order);

                    return true;
                }

                if (string.IsNullOrEmpty(order.NumberMarket)
                    || string.IsNullOrEmpty(order.PortfolioNumber))
                {
                    return false;
                }

                // ордер с биржевым номером (восстановленный или найденный опросом)
                return CancelPostedSignal(order, order.NumberMarket);
            }
            catch (Exception ex)
            {
                SendLogMessage("Autofollow. Error cancelling signal " + ex.ToString(), LogMessageType.Error);
                return false;
            }
        }

        private bool CancelPostedSignal(Order order, string signalId)
        {
            bool isStopSignal = order.TypeOrder == OrderPriceType.StopLimit
                || order.TypeOrder == OrderPriceType.StopMarket;

            string deleteError;
            bool result = DeleteSignal(order.PortfolioNumber, signalId, isStopSignal, out deleteError);

            if (result == false)
            {
                if (deleteError != null
                    && deleteError.Contains("CAN_NOT_PROCESS_THE_REQUEST"))
                {
                    // сигнал исполняется или уже исполнен — отмена невозможна, но это не ошибка.
                    // Брокер сам завершит сигнал, опрос подхватит Done
                    return false;
                }

                SendLogMessage("Autofollow. Error cancelling signal " + signalId, LogMessageType.Error);
                return false;
            }

            lock (_signalOrdersLocker)
            {
                _activeSignalOrders.Remove(signalId);
                _signalExpireDates.Remove(signalId);
                _signalIdByNumberUser.Remove(order.NumberUser);
                _signalTotalAmounts.Remove(signalId);
                RememberCancelledSignal(signalId);
            }

            SaveActiveSignals(order.PortfolioNumber);

            order.State = OrderStateType.Cancel;
            order.TimeCallBack = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, _mskTimeZone);
            order.TimeCancel = order.TimeCallBack;

            SendLogMessage("Autofollow. Signal cancelled " + signalId + " "
                + order.SecurityNameCode, LogMessageType.Trade);

            MyOrderEvent?.Invoke(order);
            RememberCompletedOrder(order);

            return true;
        }

        public void CancelAllOrders()
        {
            List<Order> orders = GetActiveOrdersSnapshot();

            for (int i = 0; i < orders.Count; i++)
            {
                CancelOrder(orders[i]);
            }
        }

        public void CancelAllOrdersToSecurity(Security security)
        {
            List<Order> orders = GetActiveOrdersSnapshot();

            for (int i = 0; i < orders.Count; i++)
            {
                if (orders[i].SecurityNameCode == security.Name)
                {
                    CancelOrder(orders[i]);
                }
            }
        }

        public void GetAllActivOrders()
        {
            // для восстановления после реконнекта отдаём движку известные активные сигналы
            List<Order> orders = GetActiveOrdersSnapshot();

            for (int i = 0; i < orders.Count; i++)
            {
                MyOrderEvent?.Invoke(orders[i]);
            }
        }

        public OrderStateType GetOrderStatus(Order order)
        {
            // хаб игнорирует возвращаемое значение и ждёт события (§8.5),
            // поэтому известным ордерам отдаём текущее состояние событием.
            // Это же механика восстановления «потерянного» Active

            lock (_pendingLimitsLocker)
            {
                for (int i = 0; i < _pendingLimitOrders.Count; i++)
                {
                    if (_pendingLimitOrders[i].NumberUser == order.NumberUser)
                    {
                        MyOrderEvent?.Invoke(_pendingLimitOrders[i]);
                        return OrderStateType.Active;
                    }
                }
            }

            lock (_sendingOrdersLocker)
            {
                Order sending;

                if (_sendingOrders.TryGetValue(order.NumberUser, out sending))
                {
                    MyOrderEvent?.Invoke(sending);
                    return OrderStateType.Active;
                }
            }

            // маркет-ордера и сработавшие лимитки, ждущие своей очереди на отправку
            foreach (Order queued in _signalsToSendQueue)
            {
                if (queued.NumberUser == order.NumberUser)
                {
                    MyOrderEvent?.Invoke(queued);
                    return queued.State;
                }
            }

            lock (_signalOrdersLocker)
            {
                string signalId;

                if (_signalIdByNumberUser.TryGetValue(order.NumberUser, out signalId))
                {
                    Order known;

                    if (_activeSignalOrders.TryGetValue(signalId, out known))
                    {
                        MyOrderEvent?.Invoke(known);
                        return known.State;
                    }
                }

                // восстановленные и найденные опросом сигналы живут с NumberMarket = signalId
                Order knownByMarket;

                if (_activeSignalOrders.TryGetValue(order.NumberMarket, out knownByMarket))
                {
                    MyOrderEvent?.Invoke(knownByMarket);
                    return knownByMarket.State;
                }
            }

            // недавно завершённые: истории у API нет, помним сами и отдаём
            // финальное состояние с его сделками (§8.5)
            lock (_completedOrdersLocker)
            {
                CompletedOrderInfo completed;

                if (_completedOrders.TryGetValue(order.NumberUser, out completed))
                {
                    MyOrderEvent?.Invoke(completed.Order);

                    if (completed.Trades != null)
                    {
                        for (int i = 0; i < completed.Trades.Count; i++)
                        {
                            MyTradeEvent?.Invoke(completed.Trades[i]);
                        }
                    }

                    return completed.Order.State;
                }
            }

            // local_ номер существует только внутри коннектора, на бирже такого
            // ордера не было и нет. Если мы о нём не знаем — он гарантированно мёртв.
            // Подтверждаем отмену событием: хаб снимет его с наблюдения, а робот
            // сможет финализировать позицию — иначе она висит в Closing вечно
            if (order.NumberMarket != null
                && order.NumberMarket.StartsWith("local_")
                && order.TimeCallBack != DateTime.MinValue
                && order.TimeCallBack.AddSeconds(60) < TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, _mskTimeZone))
            {
                order.State = OrderStateType.Cancel;
                order.TimeCallBack = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, _mskTimeZone);
                order.TimeCancel = order.TimeCallBack;

                MyOrderEvent?.Invoke(order);
            }

            return OrderStateType.None;
        }

        public List<Order> GetActiveOrders(int startIndex, int count)
        {
            List<Order> orders = GetActiveOrdersSnapshot();

            List<Order> result = new List<Order>();

            if (orders.Count != 0
                && startIndex < orders.Count)
            {
                if (startIndex + count < orders.Count)
                {
                    result = orders.GetRange(startIndex, count);
                }
                else
                {
                    result = orders.GetRange(startIndex, orders.Count - startIndex);
                }
            }

            return result;
        }

        public List<Order> GetHistoricalOrders(int startIndex, int count)
        {
            // API автоследования отдаёт только активные сигналы, истории нет
            return null;
        }

        private List<Order> GetActiveOrdersSnapshot()
        {
            List<Order> result = new List<Order>();

            lock (_pendingLimitsLocker)
            {
                result.AddRange(_pendingLimitOrders);
            }

            lock (_signalOrdersLocker)
            {
                result.AddRange(_activeSignalOrders.Values);
            }

            // маркет- и стоп-сигналы, ждущие своей очереди на отправку,
            // тоже активны — массовая отмена должна их видеть.
            // Дубли невозможны: при триггере лимитка вынимается из pending,
            // при отправке сигнал уходит из очереди в словарь
            result.AddRange(_signalsToSendQueue.ToArray());

            return result;
        }

        public event Action<Order> MyOrderEvent;

        public event Action<MyTrade> MyTradeEvent;

        #endregion

        #region 11 Helpers

        private bool IsShutdownRaceError(Exception ex)
        {
            // гонка при дисконекте: HttpClient закрыт в Dispose или запрос
            // отменён посередине. .Result заворачивает в AggregateException
            if (ex is ObjectDisposedException
                || ex is TaskCanceledException)
            {
                return true;
            }

            AggregateException aggregate = ex as AggregateException;

            if (aggregate != null
                && aggregate.InnerException != null)
            {
                return IsShutdownRaceError(aggregate.InnerException);
            }

            return false;
        }

        private void RememberTrade(Order order, MyTrade trade)
        {
            lock (_completedOrdersLocker)
            {
                List<MyTrade> trades;

                if (_recentTrades.TryGetValue(order.NumberUser, out trades) == false)
                {
                    trades = new List<MyTrade>();
                    _recentTrades[order.NumberUser] = trades;
                }

                trades.Add(trade);
            }
        }

        private void RememberCompletedOrder(Order order)
        {
            lock (_completedOrdersLocker)
            {
                CompletedOrderInfo info = new CompletedOrderInfo();
                info.Order = order;
                info.TimeCompletedUtc = DateTime.UtcNow;

                List<MyTrade> trades;

                if (_recentTrades.TryGetValue(order.NumberUser, out trades))
                {
                    info.Trades = trades;
                    _recentTrades.Remove(order.NumberUser);
                }

                _completedOrders[order.NumberUser] = info;

                SweepCompletedOrders();
            }
        }

        private void SweepCompletedOrders()
        {
            // держим не дольше 15 минут и не больше 300 записей
            DateTime limit = DateTime.UtcNow.AddMinutes(-15);

            List<int> expired = null;

            foreach (KeyValuePair<int, CompletedOrderInfo> pair in _completedOrders)
            {
                if (pair.Value.TimeCompletedUtc < limit)
                {
                    if (expired == null)
                    {
                        expired = new List<int>();
                    }

                    expired.Add(pair.Key);
                }
            }

            if (expired != null)
            {
                for (int i = 0; i < expired.Count; i++)
                {
                    _completedOrders.Remove(expired[i]);
                }
            }

            while (_completedOrders.Count > 300)
            {
                int oldestKey = 0;
                DateTime oldestTime = DateTime.MaxValue;

                foreach (KeyValuePair<int, CompletedOrderInfo> pair in _completedOrders)
                {
                    if (pair.Value.TimeCompletedUtc < oldestTime)
                    {
                        oldestTime = pair.Value.TimeCompletedUtc;
                        oldestKey = pair.Key;
                    }
                }

                _completedOrders.Remove(oldestKey);
            }
        }

        private bool IsRetryablePostError(string error)
        {
            if (string.IsNullOrEmpty(error))
            {
                return false;
            }

            // лимит создания сигналов (429) и транзитный сбой проверки
            // средневзвешенной цены у брокера (422 в быстром рынке) —
            // оба случая лечатся повтором через паузу
            return error.Contains("TOO_MANY_REQUESTS")
                || error.Contains("средневзвешенной");
        }

        private string GetStopOrderType(Order order, Security security)
        {
            // тип отложенного сигнала выводим по положению стоп-цены относительно рынка
            decimal lastPrice = GetLastMarketPrice(security.NameId);

            if (lastPrice == 0)
            {
                // бумага могла остаться без подписки на стрим — спрашиваем цену напрямую
                lastPrice = GetLastMarketPriceFromGrpc(security);
            }

            if (lastPrice == 0)
            {
                SendLogMessage("Autofollow. No market price to detect stop signal type. Default is stop loss " + security.Name, LogMessageType.System);
                return "stop_loss";
            }

            if (order.Side == Side.Sell)
            {
                return order.StopPrice < lastPrice ? "stop_loss" : "take_profit";
            }

            return order.StopPrice > lastPrice ? "stop_loss" : "take_profit";
        }

        private DateTime GetMonotonicMarketDepthTime(string securityNameId, DateTime time)
        {
            lock (_mdTimeLocker)
            {
                DateTime lastTime;

                if (_lastMdTimeBySecurity.TryGetValue(securityNameId, out lastTime)
                    && time <= lastTime)
                {
                    time = lastTime.AddTicks(1);
                }

                _lastMdTimeBySecurity[securityNameId] = time;

                return time;
            }
        }

        private int GetOrderBookDepth()
        {
            // глубина стакана по стандартному параметру «полный стакан».
            // Т-Инвест допускает только 1/10/20/50
            IServerParameter param = ServerParameters.Find(p => p.Name == OsLocalization.Market.ServerParam10);

            if (param != null
                && ((ServerParameterBool)param).Value == false)
            {
                return 10;
            }

            return 50;
        }

        private decimal GetLastMarketPrice(string securityNameId)
        {
            lock (_lastPricesLocker)
            {
                decimal price;

                if (_lastMarketPrices.TryGetValue(securityNameId, out price))
                {
                    return price;
                }
            }

            return 0;
        }

        private decimal GetLastMarketPriceFromGrpc(Security security)
        {
            // запасной источник цены для бумаг без подписки на стрим:
            // один унарный вызов, результат кэшируем
            try
            {
                if (_marketDataServiceClient == null)
                {
                    return 0;
                }

                _rateGateMarketData.WaitToProceed();

                GetLastPricesRequest request = new GetLastPricesRequest();
                request.InstrumentId.Add(security.NameId);

                GetLastPricesResponse response = _marketDataServiceClient.GetLastPrices(request, _gRpcMetadata);

                if (response == null
                    || response.LastPrices == null
                    || response.LastPrices.Count == 0)
                {
                    return 0;
                }

                decimal price = GetValue(response.LastPrices[0].Price);

                if (price == 0)
                {
                    return 0;
                }

                if (security.SecurityType == SecurityType.Bond
                    && security.NominalCurrent != 0)
                {
                    // из API цена облигации в процентах от номинала — к абсолюту, как в стриме
                    price = price / 100 * security.NominalCurrent;
                }

                lock (_lastPricesLocker)
                {
                    _lastMarketPrices[security.NameId] = price;
                }

                return price;
            }
            catch (Exception ex)
            {
                SendLogMessage("Autofollow. Failed to get last price from gRPC " + security.Name
                    + " " + ex.Message, LogMessageType.System);
                return 0;
            }
        }

        public void SetLeverage(Security security, decimal leverage) { }

        private HttpClient _httpClient;

        private List<AfStrategy> GetStrategies()
        {
            HttpClient client = _httpClient;

            if (client == null)
            {
                return null;
            }

            _rateGateRest.WaitToProceed();

            try
            {
                HttpResponseMessage response = client.GetAsync("/autofollow/strategy").Result;

                if (response.IsSuccessStatusCode)
                {
                    string responseBody = response.Content.ReadAsStringAsync().Result;
                    AfStrategiesResponse parsed = JsonConvert.DeserializeObject<AfStrategiesResponse>(responseBody);
                    return parsed != null && parsed.strategies != null ? parsed.strategies : new List<AfStrategy>();
                }

                SendLogMessage("Autofollow. Error getting strategies " + GetRestErrorMessage(response), LogMessageType.Error);
            }
            catch (Exception ex) when (IsShutdownRaceError(ex))
            {
                // штатная гонка при дисконекте: HttpClient уже закрыт в Dispose
                // или запрос отменён посередине
            }
            catch (Exception ex)
            {
                SendLogMessage("Autofollow. Error getting strategies " + ex.ToString(), LogMessageType.Error);
            }

            return null;
        }

        private List<AfInstrument> GetInstruments()
        {
            HttpClient client = _httpClient;

            if (client == null)
            {
                return null;
            }

            _rateGateRest.WaitToProceed();

            try
            {
                HttpResponseMessage response = client.GetAsync("/autofollow/instruments").Result;

                if (response.IsSuccessStatusCode)
                {
                    string responseBody = response.Content.ReadAsStringAsync().Result;
                    AfInstrumentsResponse parsed = JsonConvert.DeserializeObject<AfInstrumentsResponse>(responseBody);
                    return parsed != null && parsed.instruments != null ? parsed.instruments : new List<AfInstrument>();
                }

                SendLogMessage("Autofollow. Error loading instruments. Reconnect the connector " + GetRestErrorMessage(response), LogMessageType.Error);
            }
            catch (Exception ex)
            {
                SendLogMessage("Autofollow. Error loading instruments. Reconnect the connector " + ex.ToString(), LogMessageType.Error);
            }

            return null;
        }

        private AfPortfolioPositionResponse GetPortfolioPosition(string strategyId)
        {
            HttpClient client = _httpClient;

            if (client == null)
            {
                return null;
            }

            _rateGateRest.WaitToProceed();

            try
            {
                HttpResponseMessage response = client.GetAsync(
                    "/autofollow/strategy/" + strategyId + "/portfolio/position").Result;

                if (response.IsSuccessStatusCode)
                {
                    string responseBody = response.Content.ReadAsStringAsync().Result;
                    return JsonConvert.DeserializeObject<AfPortfolioPositionResponse>(responseBody);
                }

                SendLogMessage("Autofollow. Error getting portfolio position " + GetRestErrorMessage(response), LogMessageType.Error);
            }
            catch (Exception ex) when (IsShutdownRaceError(ex))
            {
                // штатная гонка при дисконекте: HttpClient уже закрыт в Dispose
                // или запрос отменён посередине
            }
            catch (Exception ex)
            {
                SendLogMessage("Autofollow. Error getting portfolio position " + ex.ToString(), LogMessageType.Error);
            }

            return null;
        }

        private List<AfSignal> GetSignals(string strategyId)
        {
            HttpClient client = _httpClient;

            if (client == null)
            {
                return null;
            }

            _rateGateRest.WaitToProceed();

            try
            {
                HttpResponseMessage response = client.GetAsync(
                    "/autofollow/strategy/" + strategyId + "/signals").Result;

                if (response.IsSuccessStatusCode)
                {
                    string responseBody = response.Content.ReadAsStringAsync().Result;
                    AfSignalsResponse parsed = JsonConvert.DeserializeObject<AfSignalsResponse>(responseBody);
                    return parsed != null && parsed.signals != null ? parsed.signals : new List<AfSignal>();
                }

                // 5xx у Т-Банка случаются в момент исполнения сигналов — не засоряем лог ошибками
                SendLogMessage("Autofollow. Error getting signals " + GetRestErrorMessage(response),
                    (int)response.StatusCode >= 500 ? LogMessageType.System : LogMessageType.Error);
            }
            catch (Exception ex) when (IsShutdownRaceError(ex))
            {
                // штатная гонка при дисконекте: HttpClient уже закрыт в Dispose
                // или запрос отменён посередине
            }
            catch (Exception ex)
            {
                SendLogMessage("Autofollow. Error getting signals " + ex.ToString(), LogMessageType.Error);
            }

            return null;
        }

        private List<AfStopSignal> GetStopSignals(string strategyId)
        {
            HttpClient client = _httpClient;

            if (client == null)
            {
                return null;
            }

            _rateGateRest.WaitToProceed();

            try
            {
                HttpResponseMessage response = client.GetAsync(
                    "/autofollow/strategy/" + strategyId + "/stop-signals").Result;

                if (response.IsSuccessStatusCode)
                {
                    string responseBody = response.Content.ReadAsStringAsync().Result;
                    AfStopSignalsResponse parsed = JsonConvert.DeserializeObject<AfStopSignalsResponse>(responseBody);
                    return parsed != null && parsed.stopSignals != null ? parsed.stopSignals : new List<AfStopSignal>();
                }

                // 5xx у Т-Банка случаются в момент исполнения сигналов — не засоряем лог ошибками
                SendLogMessage("Autofollow. Error getting stop signals " + GetRestErrorMessage(response),
                    (int)response.StatusCode >= 500 ? LogMessageType.System : LogMessageType.Error);
            }
            catch (Exception ex) when (IsShutdownRaceError(ex))
            {
                // штатная гонка при дисконекте: HttpClient уже закрыт в Dispose
                // или запрос отменён посередине
            }
            catch (Exception ex)
            {
                SendLogMessage("Autofollow. Error getting stop signals " + ex.ToString(), LogMessageType.Error);
            }

            return null;
        }

        private static readonly JsonSerializerSettings _postSettings = new JsonSerializerSettings
        {
            NullValueHandling = NullValueHandling.Ignore
        };

        private AfPostSignalResponse PostSignal(string strategyId, AfPostSignalRequest request, out string error)
        {
            // постит только поток-отправитель (PendingLimitSenderThread) — синхронизация не нужна
            HttpClient client = _httpClient;

            if (client == null)
            {
                error = null;
                return null;
            }

            _rateGateSendSignal.WaitToProceed();

            try
            {
                string json = JsonConvert.SerializeObject(request, _postSettings);

                HttpResponseMessage response = client.PostAsync(
                    "/autofollow/strategy/" + strategyId + "/signal",
                    new StringContent(json, Encoding.UTF8, "application/json")).Result;

                if (response.IsSuccessStatusCode)
                {
                    error = null;
                    string responseBody = response.Content.ReadAsStringAsync().Result;
                    return JsonConvert.DeserializeObject<AfPostSignalResponse>(responseBody);
                }

                error = GetRestErrorMessage(response);
                SendLogMessage("Autofollow. Error posting signal " + error, LogMessageType.Error);
            }
            catch (Exception ex) when (IsShutdownRaceError(ex))
            {
                // штатная гонка при дисконекте: HttpClient уже закрыт в Dispose
                // или запрос отменён посередине
                error = null;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                SendLogMessage("Autofollow. Error posting signal " + ex.ToString(), LogMessageType.Error);
            }

            return null;
        }

        private AfPostStopSignalResponse PostStopSignal(string strategyId, AfPostStopSignalRequest request, out string error)
        {
            HttpClient client = _httpClient;

            if (client == null)
            {
                error = null;
                return null;
            }

            _rateGateSendSignal.WaitToProceed();

            try
            {
                string json = JsonConvert.SerializeObject(request, _postSettings);

                HttpResponseMessage response = client.PostAsync(
                    "/autofollow/strategy/" + strategyId + "/stop-signal",
                    new StringContent(json, Encoding.UTF8, "application/json")).Result;

                if (response.IsSuccessStatusCode)
                {
                    error = null;
                    string responseBody = response.Content.ReadAsStringAsync().Result;
                    return JsonConvert.DeserializeObject<AfPostStopSignalResponse>(responseBody);
                }

                error = GetRestErrorMessage(response);
                SendLogMessage("Autofollow. Error posting signal " + error, LogMessageType.Error);
            }
            catch (Exception ex) when (IsShutdownRaceError(ex))
            {
                // штатная гонка при дисконекте: HttpClient уже закрыт в Dispose
                // или запрос отменён посередине
                error = null;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                SendLogMessage("Autofollow. Error posting signal " + ex.ToString(), LogMessageType.Error);
            }

            return null;
        }

        private bool DeleteSignal(string strategyId, string signalId, bool isStopSignal, out string error)
        {
            HttpClient client = _httpClient;

            if (client == null)
            {
                error = null;
                return false;
            }

            _rateGateRest.WaitToProceed();

            try
            {
                string path = "/autofollow/strategy/" + strategyId;

                if (isStopSignal)
                {
                    path += "/stop-signal/" + signalId;
                }
                else
                {
                    path += "/signal/" + signalId;
                }

                HttpResponseMessage response = client.DeleteAsync(path).Result;

                if (response.IsSuccessStatusCode)
                {
                    error = null;
                    return true;
                }

                error = GetRestErrorMessage(response);

                if (error.Contains("CAN_NOT_PROCESS_THE_REQUEST"))
                {
                    // брокер не даёт снять сигнал в момент исполнения — это штатная ситуация,
                    // сигнал завершится сам, повторная отмена не нужна
                    SendLogMessage("Autofollow. Signal is being executed for subscribers, cancellation is not possible now " + error, LogMessageType.System);
                }
                else
                {
                    SendLogMessage("Autofollow. Error cancelling signal " + error, LogMessageType.Error);
                }
            }
            catch (Exception ex) when (IsShutdownRaceError(ex))
            {
                // штатная гонка при дисконекте: HttpClient уже закрыт в Dispose
                // или запрос отменён посередине
                error = null;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                SendLogMessage("Autofollow. Error cancelling signal " + ex.ToString(), LogMessageType.Error);
            }

            return false;
        }

        private string GetRestErrorMessage(HttpResponseMessage response)
        {
            string result = "HTTP " + (int)response.StatusCode + ". ";

            try
            {
                string responseBody = response.Content.ReadAsStringAsync().Result;

                if (string.IsNullOrEmpty(responseBody))
                {
                    return result;
                }

                AfErrorResponse error = JsonConvert.DeserializeObject<AfErrorResponse>(responseBody);

                if (error != null
                    && string.IsNullOrEmpty(error.errorMessage) == false)
                {
                    // errorId нужен для общения с поддержкой Т-Банка
                    return result + "Code: " + error.errorCode
                        + ". Message: " + error.errorMessage + ". ErrorId: " + error.errorId;
                }

                return result + responseBody;
            }
            catch
            {
                return result;
            }
        }

        public decimal GetValue(Quotation quotation)
        {
            if (quotation == null)
                return 0.0m;

            if (quotation.Units == 0 && quotation.Nano == 0)
                return 0.0m;

            decimal bigDecimal = Convert.ToDecimal(quotation.Units);
            bigDecimal += Convert.ToDecimal(quotation.Nano) / 1000000000;

            return bigDecimal;
        }

        public decimal GetValue(MoneyValue moneyValue)
        {
            if (moneyValue == null)
                return 0.0m;

            if (moneyValue.Units == 0 && moneyValue.Nano == 0)
                return 0.0m;

            decimal bigDecimal = Convert.ToDecimal(moneyValue.Units);
            bigDecimal += Convert.ToDecimal(moneyValue.Nano) / 1000000000;

            return bigDecimal;
        }

        #endregion

        #region 12 Log

        private void SendLogMessage(string message, LogMessageType messageType)
        {
            LogMessageEvent?.Invoke(message, messageType);
        }

        public event Action<string, LogMessageType> LogMessageEvent;

        public event Action<Funding> FundingUpdateEvent { add { } remove { } }

        public event Action<SecurityVolumes> Volume24hUpdateEvent { add { } remove { } }

        public event Action<OptionMarketDataForConnector> AdditionalMarketDataEvent { add { } remove { } }

        #endregion
    }
}
