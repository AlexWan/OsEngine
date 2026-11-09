/*
 *Your rights to use the code are governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 *Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using OsEngine.Entity;
using OsEngine.Language;
using OsEngine.Logging;
using OsEngine.Market.Servers.Entity;
using OsEngine.Market.Servers.TInvest.Entity;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Collections.Concurrent;
using System.Threading;
using Google.Protobuf.Collections;
using Google.Protobuf.WellKnownTypes;
using Tinkoff.InvestApi.V1;
using Option = Tinkoff.InvestApi.V1.Option;
using Candle = OsEngine.Entity.Candle;
using Order = OsEngine.Entity.Order;
using Trade = OsEngine.Entity.Trade;
using Security = OsEngine.Entity.Security;
using Portfolio = OsEngine.Entity.Portfolio;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using System.Text.RegularExpressions;
using Grpc.Net.Client;
using Grpc.Core;
using System.Threading.Tasks;

namespace OsEngine.Market.Servers.TInvest
{
    public class TInvestServer : AServer
    {
        public TInvestServer(int uniqueId)
        {
            ServerNum = uniqueId;

            TInvestServerRealization realization = new TInvestServerRealization();
            ServerRealization = realization;

            ServerParameterPassword token = CreateParameterPassword(OsLocalization.Market.ServerParamToken, "");
            token.Comment = OsLocalization.Market.ServerParamTokenDescription;

            ServerParameterBool useStock = CreateParameterBoolean(OsLocalization.Market.UseStock, true);
            ServerParameterBool useFutures = CreateParameterBoolean(OsLocalization.Market.UseFutures, true);
            ServerParameterBool useOptions = CreateParameterBoolean(OsLocalization.Market.UseOptions, false); // с некоторого времени торговля опционами не доступна по API Т-Инвестиций
            ServerParameterBool useOther = CreateParameterBoolean(OsLocalization.Market.UseOther, true);
            useStock.Comment = OsLocalization.Market.UseStockDescription;
            useFutures.Comment = OsLocalization.Market.UseFuturesDescription;
            useOptions.Comment = OsLocalization.Market.UseOptionsDescription;
            useOther.Comment = OsLocalization.Market.UseOtherDescription;
            useStock.ValueChange += UseSector_ValueChange;
            useFutures.ValueChange += UseSector_ValueChange;
            useOptions.ValueChange += UseSector_ValueChange;
            useOther.ValueChange += UseSector_ValueChange;

            ServerParameterBool filterOutDealerData = CreateParameterBoolean(OsLocalization.Market.FilterOutDealerData, true);
            filterOutDealerData.Comment = OsLocalization.Market.FilterOutDealerDataDescription;

            ServerParameterBool ignoreMorningAuction = CreateParameterBoolean(OsLocalization.Market.IgnoreMorningAuctionTrades, true);
            ignoreMorningAuction.Comment = OsLocalization.Market.IgnoreMorningAuctionTradesDescription;

            ServerParameterBool fullLog = CreateParameterBoolean(OsLocalization.Market.FullLogConnector, false);
            fullLog.Comment = OsLocalization.Market.FullLogConnectorDescription;

            ServerParameterEnum depthParam = CreateParameterEnum(OsLocalization.Market.ServerParam13, "10", new List<string> { "10", "20", "50" });
            depthParam.Comment = OsLocalization.Market.SetDepthLevelsDescription;
        }

        private void UseSector_ValueChange()
        {
            Task.Run(ServerRealization.GetSecurities);
        }
    }

    public class TInvestServerRealization : IServerRealization
    {
        private readonly TimeZoneInfo _mskTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Russian Standard Time");

        #region 1 Constructor, Status, Connection

        public TInvestServerRealization()
        {
            Thread worker = new Thread(ConnectionCheckThread);
            worker.Name = "CheckAliveTInvest";
            worker.IsBackground = true;
            worker.Start();

            Thread worker3 = new Thread(PortfolioMessageReader);
            worker3.Name = "PortfolioMessageReaderTInvest";
            worker3.IsBackground = true;
            worker3.Start();

            Thread worker4 = new Thread(PositionsMessageReader);
            worker4.Name = "PositionsMessageReaderTInvest";
            worker4.IsBackground = true;
            worker4.Start();

            Thread worker6 = new Thread(LastPricesPoller);
            worker6.Name = "LastPricesPollerTInvest";
            worker6.IsBackground = true;
            worker6.Start();

            Thread worker7 = new Thread(OrderStateMessageReader);
            worker7.Name = "OrderStateMessageReaderTInvest";
            worker7.IsBackground = true;
            worker7.Start();

            Thread worker8 = new Thread(NewsPoller);
            worker8.Name = "NewsPollerTInvest";
            worker8.IsBackground = true;
            worker8.Start();

            Thread worker9 = new Thread(StopOrderExecutionsReader);
            worker9.Name = "StopOrderExecutionsReaderTInvest";
            worker9.IsBackground = true;
            worker9.Start();
        }

        public void Connect(WebProxy proxy)
        {
            _proxy = proxy;

            // флаг «идёт Dispose» снимаем здесь, а не в конце Dispose:
            // между Dispose и Connect ридеры не должны реагировать
            // на исключения от уже убитых стримов
            _isDisposedNow = false;

            try
            {
                try
                {
                    string osNameAndVersion = System.Runtime.InteropServices.RuntimeInformation.OSDescription;

                    if (osNameAndVersion.StartsWith("Microsoft Windows 7"))
                    {
                        SendLogMessage(OsLocalization.Market.Label299, LogMessageType.System);
                        return;
                    }

                }
                catch
                {
                    // ignore
                }

                _pollSubscribedSecurities.Clear();
                _subscribeRetryCounts.Clear();

                // сброс троттлинга портфелей: первая загрузка после Connect
                // обязана пройти, иначе при быстром реконнекте портфели останутся пустыми
                _lastTimeGetPortfolio = DateTime.MinValue;

                _marketDataStreams = new List<MarketDataStreamWrapper>();
                _securityStreamMap = new Dictionary<string, MarketDataStreamWrapper>();

                lock (_stopOrdersLocker)
                {
                    _activeStopOrders.Clear();
                }

                _newsIsSubscribed = false;
                _lastNewsId = 0;

                SendLogMessage(OsLocalization.Market.Label284, LogMessageType.System);

                _accessToken = ((ServerParameterPassword)ServerParameters[0]).Value;
                _filterOutDealerData = ((ServerParameterBool)ServerParameters[5]).Value;
                _ignoreMorningAuctionTrades = ((ServerParameterBool)ServerParameters[6]).Value;
                _fullLog = ((ServerParameterBool)ServerParameters[7]).Value;

                string depthStr = ((ServerParameterEnum)ServerParameters[8]).Value;

                if (int.TryParse(depthStr, out int depthValue))
                {
                    _orderBookDepth = depthValue;
                }

                if (string.IsNullOrEmpty(_accessToken))
                {
                    SendLogMessage(OsLocalization.Market.Label283,
                        LogMessageType.Error);
                    return;
                }

                CreateStreamsConnection();
            }
            catch (Exception ex)
            {
                SendLogMessage(OsLocalization.Market.Label289 + ex.Message.ToString(), LogMessageType.Error);
            }
        }

        private DateTime _lastTimeEntryLogicConnectionCheckThread;

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

                    if (_securitiesDictionary.Count == 0)
                    {
                        Thread.Sleep(1000);
                        continue;
                    }

                    DateTime utcTime = DateTime.UtcNow;

                    if (_lastTimeEntryLogicConnectionCheckThread != DateTime.MinValue
                        && _lastTimeEntryLogicConnectionCheckThread.Hour == 2
                        && utcTime.Hour == 3)
                    {
                        _lastTimeEntryLogicConnectionCheckThread = utcTime;

                        SendLogMessage(OsLocalization.Market.Label321, LogMessageType.System);
                        ServerStatus = ServerConnectStatus.Disconnect;
                        DisconnectEvent();
                        Thread.Sleep(2000);
                        continue;
                    }

                    _lastTimeEntryLogicConnectionCheckThread = utcTime;

                    bool streamsIsLost = false;
                    string lostStreamName = null;

                    if (_marketDataStreams != null)
                    {
                        foreach (MarketDataStreamWrapper stream in _marketDataStreams)
                        {
                            if (stream.LastMessageTime.AddSeconds(30) < DateTime.UtcNow
                                || stream.IsConnected == false)
                            {
                                lostStreamName = stream.Name;
                                streamsIsLost = true;
                                break;
                            }
                        }
                    }

                    if (_portfolioDataStream != null && _lastPortfolioDataTime.AddSeconds(30) < DateTime.UtcNow)
                    {
                        lostStreamName = "Portfolio data stream";
                        streamsIsLost = true;
                    }

                    if (_positionsDataStream != null
                        && _lastPositionsDataTime.AddSeconds(30) < DateTime.UtcNow)
                    {
                        lostStreamName = "Positions data stream";
                        streamsIsLost = true;
                    }

                    if (_myOrderStateDataStream != null && _lastMyOrderStateDataTime.AddSeconds(30) < DateTime.UtcNow)
                    {
                        lostStreamName = "Order state data stream";

                        streamsIsLost = true;
                    }

                    if (streamsIsLost)
                    {
                        SendLogMessage(
                            "Stream is lost. ConnectionCheckThread(). stream = "
                            + lostStreamName, LogMessageType.System);

                        if (_isDisposedNow == true)
                        {
                            continue;
                        }

                        if (lostStreamName == "Order state data stream")
                        {
                            _isReconnectByOrdersData = true;

                            if (TryReconnectOrdersStream() == true)
                            {
                                _lastMyOrderStateDataTime = DateTime.UtcNow;
                                SendLogMessage(OsLocalization.Market.Label295 + "\nOrders data. ConnectionCheckThread()", LogMessageType.System);

                                if (ForceCheckOrdersAfterReconnectEvent != null)
                                {
                                    ForceCheckOrdersAfterReconnectEvent();
                                }
                                Thread.Sleep(2000);
                                _isReconnectByOrdersData = false;
                                continue;

                            }
                        }
                        else if (lostStreamName == "Portfolio data stream")
                        {
                            _isReconnectByPingPortfoliosData = true;

                            if (TryReconnectPortfolioStream() == true)
                            {
                                _lastPortfolioDataTime = DateTime.UtcNow;
                                SendLogMessage(OsLocalization.Market.Label295 + "\nPortfolio and Positions data. ConnectionCheckThread()", LogMessageType.System);
                                Thread.Sleep(2000);
                                _isReconnectByPingPortfoliosData = false;
                                continue;
                            }
                        }
                        else if (lostStreamName == "Positions data stream")
                        {
                            _isReconnectByPingPortfoliosData = true;

                            if (TryReconnectPositionsStream() == true)
                            {
                                _lastPositionsDataTime = DateTime.UtcNow;
                                SendLogMessage(OsLocalization.Market.Label295 + "\nPositions data stream. ConnectionCheckThread()", LogMessageType.System);
                                Thread.Sleep(2000);
                                _isReconnectByPingPortfoliosData = false;
                                continue;
                            }
                        }
                        else if (lostStreamName != null
                            && lostStreamName.StartsWith("Market data stream"))
                        {
                            MarketDataStreamWrapper streamToReconnect = _marketDataStreams.FirstOrDefault(s => s.Name == lostStreamName);
                            if (streamToReconnect != null)
                            {
                                if (TryReconnectDataStream(streamToReconnect) == true)
                                {
                                    streamToReconnect.LastMessageTime = DateTime.UtcNow;
                                    SendLogMessage(OsLocalization.Market.Label295 + $"\n{streamToReconnect.Name}. ConnectionCheckThread()", LogMessageType.System);
                                    Thread.Sleep(2000);
                                    continue;
                                }
                            }
                        }

                        _isReconnectByOrdersData = false;
                        _isReconnectByPingPortfoliosData = false;

                        if (ServerStatus == ServerConnectStatus.Connect)
                        {
                            SendLogMessage(OsLocalization.Market.Label286 + lostStreamName, LogMessageType.System);
                            SendMessageOnReconnectInErrorLog();
                            ServerStatus = ServerConnectStatus.Disconnect;
                            DisconnectEvent();
                            Thread.Sleep(2000);
                        }
                    }
                    else
                    {
                        if (_lastTimeGetPortfolio.AddSeconds(10) < DateTime.Now)
                        {
                            GetPortfolios();
                        }

                        if (_lastSubscriptionsReconcile.AddMinutes(2) < DateTime.Now)
                        {
                            _lastSubscriptionsReconcile = DateTime.Now;
                            RequestMySubscriptions();
                        }

                        Thread.Sleep(5000);
                    }
                }
                catch (Exception ex)
                {
                    _isReconnectByOrdersData = false;
                    _isReconnectByPingPortfoliosData = false;

                    SendLogMessage(ex.ToString(), LogMessageType.System);
                    Thread.Sleep(5000);
                }
            }
        }

        private bool _isDisposedNow = false;

        private bool _isReconnectByPingPortfoliosData = false;

        private bool _isReconnectByOrdersData = false;

        public void Dispose()
        {
            _isDisposedNow = true;

            try
            {

                // сначала аварийная отмена токена - читатели стримов умирают мгновенно,
                // дальнейшее закрытие пойдёт быстро даже при мёртвом соединении
                if (_cancellationTokenSource != null)
                {
                    try
                    {
                        _cancellationTokenSource.Cancel();
                    }
                    catch
                    {
                        // ignore
                    }
                }

                // останавливаем чтение всех потоков маркет-данных
                // (таймаут короткий: остановка коннектора не должна ждать больной сервер)
                if (_marketDataStreams != null)
                {
                    lock (_marketDataStreamLocker)
                    {
                        for (int i = 0; i < _marketDataStreams.Count; i++)
                        {
                            CloseDataStreamClient(_marketDataStreams[i], TimeSpan.FromSeconds(1));
                        }
                    }
                }

                if (_portfolioDataStream != null)
                {
                    try
                    {
                        _portfolioDataStream.Dispose();
                    }
                    catch
                    {
                        // ignore
                    }
                }

                if (_positionsDataStream != null)
                {
                    try
                    {
                        _positionsDataStream.Dispose();
                    }
                    catch
                    {
                        // ignore
                    }
                }

                if (_myOrderStateDataStream != null)
                {
                    try
                    {
                        _myOrderStateDataStream.Dispose();
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

                _portfolioDataStream = null;
                _positionsDataStream = null;
                _myOrderStateDataStream = null;
                _marketDataStreams?.Clear();
                _securityStreamMap?.Clear();
                _pollSubscribedSecurities.Clear();
                _stopOrderExecutions = new ConcurrentQueue<StopOrderExecution>();
                _myPortfolios.Clear();
                _newsIsSubscribed = false;
                _lastMarketDataTime = DateTime.UtcNow;
                _lastMdTime = DateTime.UtcNow;
                _lastPortfolioDataTime = DateTime.UtcNow;
                _lastPositionsDataTime = DateTime.UtcNow;

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
        }

        public DateTime ServerTime { get; set; }

        public ServerType ServerType => ServerType.TInvest;

        public ServerConnectStatus ServerStatus { get; set; } = ServerConnectStatus.Disconnect;

        public List<IServerParameter> ServerParameters { get; set; }

        public event Action ConnectEvent;

        public event Action DisconnectEvent;

        public bool IsCompletelyDeleted { get; set; }

        #endregion

        #region 2 Properties

        private bool _useStock = false;
        private bool _useFutures = false;
        private bool _useOptions = false;
        private bool _useOther = false;

        private bool _filterOutDealerData; // отфильтровать данные дилера (внутренняя ликвидность Т-Инвест, торги выходного дня)
        private bool _ignoreMorningAuctionTrades; // ignore trades before 7:00 MSK for stocks and before 9:00 for futures
        private bool _fullLog; // полное логирование ордеров и трейдов
        private string _accessToken;

        private Dictionary<string, int> _orderNumbers = new Dictionary<string, int>();

        private string _orderNumbersLocker = "_orderNumbersLocker";

        private ConcurrentDictionary<string, decimal> _orderPrices = new ConcurrentDictionary<string, decimal>();

        private List<Order> _activeStopOrders = new List<Order>();

        private string _stopOrdersLocker = "_stopOrdersLocker";

        private Dictionary<string, int> _stopOrderNumbers = new Dictionary<string, int>();

        private bool _newsIsSubscribed = false;

        private long _lastNewsId = 0; // id последней обработанной новости. Новости приходят от новых к старым

        private RateGate _rateGateNews = new RateGate(30, TimeSpan.FromMinutes(1));

        private int _orderBookDepth = 10; // глубина стакана, параметр коннектора

        // лимит стримов маркет-даты: обновляется из тарифа пользователя (GetUserTariff)
        private int _maxMarketDataStreams = 16;

        #endregion

        #region 3 Securities

        private RateGate _rateGateInstruments = new RateGate(200, TimeSpan.FromMinutes(1));

        private string _getSecuritiesLocker = "_getSecuritiesLocker";

        public void GetSecurities()
        {
            try
            {
                lock (_getSecuritiesLocker)
                {
                    if (ServerStatus != ServerConnectStatus.Connect)
                    {
                        return;
                    }

                    _useStock = ((ServerParameterBool)ServerParameters[1]).Value;
                    _useFutures = ((ServerParameterBool)ServerParameters[2]).Value;
                    _useOptions = ((ServerParameterBool)ServerParameters[3]).Value;
                    _useOther = ((ServerParameterBool)ServerParameters[4]).Value;

                    // Справочник отдаём движку частями - сразу после каждого загруженного типа.
                    // Вкладки начинают находить свои бумаги в первые секунды,
                    // не дожидаясь загрузки всего списка целиком.
                    // Ошибка загрузки одного типа не должна обрывать весь справочник:
                    // логируем и продолжаем со следующим типом

                    try
                    {
                        _rateGateInstruments.WaitToProceed();
                        CurrenciesResponse currenciesResponse = _instrumentsClient.Currencies(new InstrumentsRequest(), headers: _gRpcMetadata);

                        if (UpdateCurrenciesFromServer(currenciesResponse))
                        {
                            SendCurrentSecurities();
                        }
                    }
                    catch (Exception ex)
                    {
                        SendLogMessage(OsLocalization.Market.Label323 + " currencies. " + ex.ToString(), LogMessageType.Error);
                    }

                    if (_useStock || _useOther)
                    {
                        try
                        {
                            _rateGateInstruments.WaitToProceed();

                            SharesResponse result = _instrumentsClient.Shares(new InstrumentsRequest(), headers: _gRpcMetadata);

                            if (UpdateSharesFromServer(result))
                            {
                                SendCurrentSecurities();
                            }
                        }
                        catch (Exception ex)
                        {
                            SendLogMessage(OsLocalization.Market.Label323 + " shares. " + ex.ToString(), LogMessageType.Error);
                        }
                    }

                    if (_useFutures)
                    {
                        try
                        {
                            _rateGateInstruments.WaitToProceed();

                            FuturesResponse result = _instrumentsClient.Futures(new InstrumentsRequest(), headers: _gRpcMetadata);

                            if (UpdateFuturesFromServer(result))
                            {
                                SendCurrentSecurities();
                            }
                        }
                        catch (Exception ex)
                        {
                            SendLogMessage(OsLocalization.Market.Label323 + " futures. " + ex.ToString(), LogMessageType.Error);
                        }
                    }

                    if (_useOptions)
                    {
                        // https://russianinvestments.github.io/investAPI/faq_instruments/ v1.23
                        // No options still for T-Invest
                        //SendLogMessage("Options trading not supported by T-Invest API", LogMessageType.System);

                        //_rateGateInstruments.WaitToProceed();

                        //OptionsResponse result = null;
                        //try
                        //{
                        //    result = _instrumentsClient.Options(new InstrumentsRequest(), headers: _gRpcMetadata);
                        //}
                        //catch (RpcException ex)
                        //{
                        //    string message = GetGRPCErrorMessage(ex);
                        //    SendLogMessage($"Error getting options data. Info: {message}", LogMessageType.System);
                        //}
                        //catch (Exception ex)
                        //{
                        //    SendLogMessage("Error loading securities", LogMessageType.System);
                        //}

                        //UpdateOptionsFromServer(result);
                    }

                    if (_useOther)
                    {
                        try
                        {
                            _rateGateInstruments.WaitToProceed();

                            BondsResponse result = _instrumentsClient.Bonds(new InstrumentsRequest(), headers: _gRpcMetadata);

                            if (UpdateBondsFromServer(result))
                            {
                                SendCurrentSecurities();
                            }
                        }
                        catch (Exception ex)
                        {
                            SendLogMessage(OsLocalization.Market.Label323 + " bonds. " + ex.ToString(), LogMessageType.Error);
                        }

                        try
                        {
                            _rateGateInstruments.WaitToProceed();

                            EtfsResponse etfs = _instrumentsClient.Etfs(new InstrumentsRequest(), headers: _gRpcMetadata);

                            if (UpdateEtfsFromServer(etfs))
                            {
                                SendCurrentSecurities();
                            }
                        }
                        catch (Exception ex)
                        {
                            SendLogMessage(OsLocalization.Market.Label323 + " etfs. " + ex.ToString(), LogMessageType.Error);
                        }

                        try
                        {
                            _rateGateInstruments.WaitToProceed();

                            IndicativesResponse indicatives = _instrumentsClient.Indicatives(new IndicativesRequest(), headers: _gRpcMetadata);

                            if (UpdateIndicativesFromServer(indicatives))
                            {
                                SendCurrentSecurities();
                            }
                        }
                        catch (Exception ex)
                        {
                            SendLogMessage(OsLocalization.Market.Label323 + " indicatives. " + ex.ToString(), LogMessageType.Error);
                        }
                    }

                    if (_securities.Count > 0)
                    {
                        SendLogMessage(OsLocalization.Market.Label287 + " " + _securities.Count, LogMessageType.System);

                        SendCurrentSecurities();

                        GetPortfolios();
                    }
                    else
                    {
                        if (ServerStatus != ServerConnectStatus.Disconnect)
                        {
                            SendLogMessage(OsLocalization.Market.Label305, LogMessageType.Error);

                            ServerStatus = ServerConnectStatus.Disconnect;
                            DisconnectEvent();
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                if (ServerStatus != ServerConnectStatus.Disconnect)
                {
                    SendLogMessage(OsLocalization.Market.Label323 + ex.ToString(), LogMessageType.Error);

                    ServerStatus = ServerConnectStatus.Disconnect;
                    DisconnectEvent();
                }
            }
        }

        private void SendCurrentSecurities()
        {
            // снимок списка: дальше по коду в _securities продолжают добавляться бумаги
            List<Security> snapshot = new List<Security>(_securities);

            if (snapshot.Count > 0
                && SecurityEvent != null)
            {
                SecurityEvent.Invoke(snapshot);
            }
        }

        private bool UpdateSharesFromServer(SharesResponse sharesResponse)
        {
            try
            {
                if (sharesResponse == null ||
                    sharesResponse.Instruments.Count == 0)
                {
                    return false;
                }

                for (int i = 0; i < sharesResponse.Instruments.Count; i++)
                {
                    Share item = sharesResponse.Instruments[i];

                    Security newSecurity = new Security();
                    newSecurity.Name = item.Ticker;
                    newSecurity.NameId = item.Uid;
                    newSecurity.NameFull = item.Name;
                    newSecurity.Exchange = item.Exchange;

                    if (item.MinPriceIncrement != null)
                    {
                        newSecurity.PriceStep = GetValue(item.MinPriceIncrement);
                    }
                    else
                    {
                        newSecurity.PriceStep = 1;
                    }

                    if (newSecurity.PriceStep == 0)
                    {
                        newSecurity.PriceStep = 1;
                    }

                    newSecurity.PriceStepCost = newSecurity.PriceStep;

                    newSecurity.NameClass = SecurityType.Stock.ToString() + " " + item.Currency;


                    newSecurity.SecurityType = SecurityType.Stock;
                    newSecurity.Lot = item.Lot;
                    newSecurity.VolumeStep = 1;

                    newSecurity.State = SecurityStateType.Activ;

                    if (_securities.Find(s => s.NameId == newSecurity.NameId) == null)
                    {
                        _securities.Add(newSecurity);
                    }

                    Security outSec = null;
                    if (_securitiesDictionary.TryGetValue(newSecurity.NameId, out outSec) == false)
                    {
                        _securitiesDictionary.Add(newSecurity.NameId, newSecurity);
                    }
                }

            }
            catch (Exception e)
            {
                SendLogMessage($"Error loading stocks: {e.Message}", LogMessageType.System);
                return false;
            }
            return true;
        }

        private bool UpdateBondsFromServer(BondsResponse bondsResponse)
        {
            try
            {
                if (bondsResponse == null ||
                    bondsResponse.Instruments.Count == 0)
                {
                    return false;
                }

                for (int i = 0; i < bondsResponse.Instruments.Count; i++)
                {
                    Bond item = bondsResponse.Instruments[i];

                    Security newSecurity = new Security();
                    newSecurity.Name = item.Ticker;
                    newSecurity.NameId = item.Uid;
                    newSecurity.NameFull = item.Name;
                    newSecurity.Exchange = item.Exchange;

                    if (item.MinPriceIncrement != null)
                    {
                        newSecurity.PriceStep = GetValue(item.MinPriceIncrement);
                    }
                    else
                    {
                        newSecurity.PriceStep = 1;
                    }

                    if (newSecurity.PriceStep == 0)
                    {
                        newSecurity.PriceStep = 1;
                    }

                    newSecurity.PriceStepCost = newSecurity.PriceStep;


                    newSecurity.NameClass = SecurityType.Bond.ToString() + " " + item.Currency;

                    newSecurity.SecurityType = SecurityType.Bond;
                    newSecurity.Lot = item.Lot;
                    newSecurity.VolumeStep = 1;

                    newSecurity.State = SecurityStateType.Activ;

                    newSecurity.NominalCurrent = GetValue(item.Nominal);
                    newSecurity.NominalInitial = GetValue(item.InitialNominal);

                    if (item.MaturityDate != null)
                    {
                        newSecurity.MaturityDate = TimeZoneInfo.ConvertTimeFromUtc(item.MaturityDate.ToDateTime(), _mskTimeZone); // convert to MSK;
                    }

                    if (item.PlacementDate != null)
                    {
                        newSecurity.PlacementDate = TimeZoneInfo.ConvertTimeFromUtc(item.PlacementDate.ToDateTime(), _mskTimeZone); // convert to MSK;
                    }

                    newSecurity.PlacementPrice = GetValue(item.PlacementPrice);
                    newSecurity.AciValue = GetValue(item.AciValue);

                    if (_securities.Find(s => s.NameId == newSecurity.NameId) == null)
                    {
                        _securities.Add(newSecurity);
                    }

                    Security outSec = null;
                    if (_securitiesDictionary.TryGetValue(newSecurity.NameId, out outSec) == false)
                    {
                        _securitiesDictionary.Add(newSecurity.NameId, newSecurity);
                    }
                }

            }
            catch (Exception e)
            {
                SendLogMessage($"Error loading bonds: {e.Message}", LogMessageType.System);
                return false;
            }

            return true;
        }

        private bool UpdateEtfsFromServer(EtfsResponse etfs)
        {
            try
            {
                if (etfs == null ||
                    etfs.Instruments.Count == 0)
                {
                    return false;
                }

                for (int i = 0; i < etfs.Instruments.Count; i++)
                {
                    Etf item = etfs.Instruments[i];

                    Security newSecurity = new Security();
                    newSecurity.Name = item.Ticker;
                    newSecurity.NameId = item.Uid;
                    newSecurity.NameFull = item.Name;
                    newSecurity.Exchange = item.Exchange;

                    if (item.MinPriceIncrement != null)
                    {
                        newSecurity.PriceStep = GetValue(item.MinPriceIncrement);
                    }
                    else
                    {
                        newSecurity.PriceStep = 1;
                    }

                    if (newSecurity.PriceStep == 0)
                    {
                        newSecurity.PriceStep = 1;
                    }

                    newSecurity.PriceStepCost = newSecurity.PriceStep;

                    newSecurity.NameClass = SecurityType.Fund.ToString() + " " + item.Currency;

                    newSecurity.SecurityType = SecurityType.Fund;
                    newSecurity.Lot = item.Lot;
                    newSecurity.VolumeStep = 1;

                    newSecurity.State = SecurityStateType.Activ;

                    if (_securities.Find(s => s.NameId == newSecurity.NameId) == null)
                    {
                        _securities.Add(newSecurity);
                    }

                    Security outSec = null;
                    if (_securitiesDictionary.TryGetValue(newSecurity.NameId, out outSec) == false)
                    {
                        _securitiesDictionary.Add(newSecurity.NameId, newSecurity);
                    }
                }
            }
            catch (Exception e)
            {
                SendLogMessage($"Error loading ETFs: {e.Message}", LogMessageType.System);
                return false;
            }
            return true;
        }

        private bool UpdateIndicativesFromServer(IndicativesResponse indicatives)
        {
            try
            {
                if (indicatives == null ||
                    indicatives.Instruments.Count == 0)
                {
                    return false;
                }

                for (int i = 0; i < indicatives.Instruments.Count; i++)
                {
                    IndicativeResponse item = indicatives.Instruments[i];

                    Security newSecurity = new Security();
                    newSecurity.Name = item.Ticker;
                    newSecurity.NameId = item.Uid;
                    newSecurity.NameFull = item.Name;
                    newSecurity.Exchange = item.Exchange;

                    if (string.IsNullOrEmpty(newSecurity.Exchange))
                    {
                        // у ставок ЦБ (KEYRATE, RUONIA и т.п.) биржи в API нет
                        newSecurity.Exchange = "NONE";
                    }

                    newSecurity.PriceStep = 1;
                    newSecurity.PriceStepCost = newSecurity.PriceStep;


                    newSecurity.NameClass = SecurityType.Index.ToString() + " " + item.Currency;

                    newSecurity.SecurityType = SecurityType.Index;
                    newSecurity.Lot = 1;
                    newSecurity.VolumeStep = 1;

                    newSecurity.State = SecurityStateType.Activ;

                    if (_securities.Find(s => s.NameId == newSecurity.NameId) == null)
                    {
                        _securities.Add(newSecurity);
                    }

                    Security outSec = null;
                    if (_securitiesDictionary.TryGetValue(newSecurity.NameId, out outSec) == false)
                    {
                        _securitiesDictionary.Add(newSecurity.NameId, newSecurity);
                    }
                }

            }
            catch (Exception e)
            {
                SendLogMessage($"Error loading indicatives: {e.Message}", LogMessageType.System);
                return false;
            }
            return true;
        }

        private bool UpdateCurrenciesFromServer(CurrenciesResponse currenciesResponse)
        {
            try
            {
                if (currenciesResponse == null ||
                    currenciesResponse.Instruments.Count == 0)
                {
                    return false;
                }

                for (int i = 0; i < currenciesResponse.Instruments.Count; i++)
                {
                    Currency item = currenciesResponse.Instruments[i];

                    Security newSecurity = new Security();
                    newSecurity.Name = item.Ticker;
                    newSecurity.NameId = item.Uid;
                    newSecurity.NameFull = item.Name;
                    newSecurity.Exchange = item.Exchange;

                    if (item.MinPriceIncrement != null)
                    {
                        newSecurity.PriceStep = GetValue(item.MinPriceIncrement);
                    }
                    else
                    {
                        newSecurity.PriceStep = 1;
                    }

                    if (newSecurity.PriceStep == 0)
                    {
                        newSecurity.PriceStep = 1;
                    }

                    newSecurity.PriceStepCost = newSecurity.PriceStep;


                    newSecurity.NameClass = "Currency pair";

                    newSecurity.SecurityType = SecurityType.CurrencyPair;
                    newSecurity.Lot = item.Lot;
                    newSecurity.VolumeStep = 1;

                    newSecurity.State = SecurityStateType.Activ;

                    if (_securities.Find(s => s.NameId == newSecurity.NameId) == null)
                    {
                        _securities.Add(newSecurity);
                    }

                    Security outSec = null;
                    if (_securitiesDictionary.TryGetValue(newSecurity.NameId, out outSec) == false)
                    {
                        _securitiesDictionary.Add(newSecurity.NameId, newSecurity);
                    }
                }

            }
            catch (Exception e)
            {
                SendLogMessage($"Error loading currency pairs: {e.Message}", LogMessageType.System);
                return false;
            }
            return true;
        }

        private bool UpdateFuturesFromServer(FuturesResponse futures)
        {
            try
            {
                if (futures == null ||
                    futures.Instruments.Count == 0)
                {
                    return false;
                }

                for (int i = 0; i < futures.Instruments.Count; i++)
                {
                    Future item = futures.Instruments[i];

                    Security newSecurity = new Security();
                    newSecurity.Name = item.Ticker;
                    newSecurity.NameId = item.Uid;
                    newSecurity.NameFull = item.Name;
                    newSecurity.Exchange = item.Exchange;
                    newSecurity.UsePriceStepCostToCalculateVolume = true;

                    if (item.MinPriceIncrement != null)
                    {
                        newSecurity.PriceStep = GetValue(item.MinPriceIncrement);
                    }
                    else
                    {
                        newSecurity.PriceStep = 1;
                    }

                    newSecurity.Expiration = TimeZoneInfo.ConvertTimeFromUtc(item.ExpirationDate.ToDateTime(), _mskTimeZone);// convert to MSK;

                    if (newSecurity.PriceStep == 0)
                    {
                        newSecurity.PriceStep = 1;
                    }

                    newSecurity.PriceStepCost = newSecurity.PriceStep;


                    // neo-assets (perpetual futures of SPB Exchange) are marked as a separate class
                    if (item.Exchange != null
                        && item.Exchange.StartsWith("spb_future", StringComparison.OrdinalIgnoreCase))
                    {
                        newSecurity.NameClass = "FuturesNeoSpb";
                    }
                    else
                    {
                        newSecurity.NameClass = SecurityType.Futures.ToString();
                    }

                    newSecurity.Lot = item.Lot;

                    newSecurity.SecurityType = SecurityType.Futures;
                    newSecurity.VolumeStep = 1;

                    decimal magrinBuyRiskCoeffClient = GetValue(item.DlongClient);
                    decimal magrinSellRiskCoeffMoex = GetValue(item.DshortClient);

                    newSecurity.MarginBuy = GetValue(item.InitialMarginOnBuy);
                    newSecurity.MarginSell = GetValue(item.InitialMarginOnSell);

                    if (item.MinPriceIncrementAmount != null)
                    {
                        newSecurity.PriceStepCost = GetValue(item.MinPriceIncrementAmount);
                    }

                    newSecurity.State = SecurityStateType.Activ;

                    if (_securities.Find(s => s.NameId == newSecurity.NameId) == null)
                    {
                        _securities.Add(newSecurity);
                    }

                    Security outSec = null;
                    if (_securitiesDictionary.TryGetValue(newSecurity.NameId, out outSec) == false)
                    {
                        _securitiesDictionary.Add(newSecurity.NameId, newSecurity);
                    }

                    TinSecuritiesRisksFutures riskFutures = null;

                    if (_tSecuritiesRiskFutures.TryGetValue(newSecurity.NameId, out riskFutures) == false)
                    {
                        riskFutures = new TinSecuritiesRisksFutures();
                        riskFutures.MarginBuyCoeffClient = magrinBuyRiskCoeffClient;
                        riskFutures.MarginSellCoeffClient = magrinSellRiskCoeffMoex;

                        _tSecuritiesRiskFutures.Add(newSecurity.NameId, riskFutures);
                    }
                }
            }
            catch (Exception e)
            {
                SendLogMessage($"Error loading futures: {e.Message}", LogMessageType.System);
                return false;
            }
            return true;
        }

        private void UpdateOptionsFromServer(OptionsResponse options)
        {
            try
            {
                if (options == null ||
                    options.Instruments.Count == 0)
                {
                    return;
                }

                for (int i = 0; i < options.Instruments.Count; i++)
                {
                    Option item = options.Instruments[i];

                    Security newSecurity = new Security();
                    newSecurity.Name = item.Ticker;
                    newSecurity.NameId = item.Uid;
                    newSecurity.NameFull = item.Name;
                    newSecurity.Exchange = item.Exchange;
                    newSecurity.UsePriceStepCostToCalculateVolume = true;

                    if (item.MinPriceIncrement != null)
                    {
                        newSecurity.PriceStep = GetValue(item.MinPriceIncrement);
                    }
                    else
                    {
                        newSecurity.PriceStep = 1;
                    }

                    if (newSecurity.PriceStep == 0)
                    {
                        newSecurity.PriceStep = 1;
                    }

                    newSecurity.PriceStepCost = newSecurity.PriceStep;


                    newSecurity.NameClass = SecurityType.Option.ToString();

                    newSecurity.SecurityType = SecurityType.Option;
                    newSecurity.Lot = item.Lot;
                    newSecurity.VolumeStep = 1;

                    newSecurity.State = SecurityStateType.Activ;
                    _securities.Add(newSecurity);
                    _securitiesDictionary.Add(newSecurity.NameId, newSecurity);
                }
            }
            catch (Exception e)
            {
                SendLogMessage($"Error loading options: {e.Message}", LogMessageType.System);
            }
        }

        private List<Security> _securities = new List<Security>();

        private Dictionary<string, Security> _securitiesDictionary = new Dictionary<string, Security>();

        private Dictionary<string, TinSecuritiesRisksFutures> _tSecuritiesRiskFutures = new Dictionary<string, TinSecuritiesRisksFutures>();

        private Security GetSecurityByIdFast(string instrumentId)
        {
            Security mySecurity = null;

            if (_securitiesDictionary.TryGetValue(instrumentId, out mySecurity))
            {
                return mySecurity;
            }

            return null;
        }

        private string GetClassName(Tinkoff.InvestApi.V1.Instrument instrument)
        {
            // shares newSecurity.NameClass = SecurityType.Stock.ToString() + " " + item.Currency;
            // bonds  newSecurity.NameClass = SecurityType.Bond.ToString() + " " + item.Currency;
            // etfs   newSecurity.NameClass = SecurityType.Fund.ToString() + " " + item.Currency;
            // indexes  newSecurity.NameClass = SecurityType.Index.ToString() + " " + item.Currency;
            // currency newSecurity.NameClass = "Currency pair";
            // futures newSecurity.NameClass = SecurityType.Futures.ToString();

            string uid = instrument.Uid;

            Security mySecurity = GetSecurityByIdFast(uid);

            if (mySecurity == null)
            {
                return null;
            }

            return mySecurity.NameClass;
        }

        public event Action<List<Security>> SecurityEvent;

        // данные инструмента по uid: сначала локальный справочник (без сети),
        // при промахе - запрос в API (редкий случай: бумага выключенного в настройках класса)
        private Security GetSecurityByUidOrApi(string instrumentUid)
        {
            Security security = GetSecurityByIdFast(instrumentUid);

            if (security != null)
            {
                return security;
            }

            try
            {
                _rateGateInstruments.WaitToProceed();

                InstrumentRequest instrumentRequest = new InstrumentRequest();
                instrumentRequest.Id = instrumentUid;
                instrumentRequest.IdType = InstrumentIdType.Uid;

                InstrumentResponse instrument = _instrumentsClient.GetInstrumentBy(instrumentRequest, _gRpcMetadata);

                if (instrument != null
                    && instrument.Instrument != null)
                {
                    security = new Security();
                    security.Name = instrument.Instrument.Ticker;
                    security.NameId = instrument.Instrument.Uid;
                    security.Lot = instrument.Instrument.Lot > 0 ? instrument.Instrument.Lot : 1;
                    security.NameClass = GetClassName(instrument.Instrument);
                }
            }
            catch (RpcException ex)
            {
                string message = GetGRPCErrorMessage(ex);
                SendLogMessage($"Error getting instrument data. Info: {message}", LogMessageType.System);
            }
            catch (Exception ex)
            {
                SendLogMessage("Error getting instrument data for " + instrumentUid + " " + ex.ToString(), LogMessageType.System);
            }

            return security;
        }

        #endregion

        #region 4 Portfolios

        private List<Portfolio> _myPortfolios = new List<Portfolio>();

        private readonly object _portfolioDataLocker = new object();

        private List<Portfolio> CreatePortfoliosSnapshot()
        {
            lock (_portfolioDataLocker)
            {
                List<Portfolio> snapshot = new List<Portfolio>(_myPortfolios.Count);

                for (int i = 0; i < _myPortfolios.Count; i++)
                {
                    Portfolio source = _myPortfolios[i];
                    Portfolio clone = new Portfolio();
                    clone.Number = source.Number;
                    clone.ValueBegin = source.ValueBegin;
                    clone.ValueCurrent = source.ValueCurrent;
                    clone.ValueBlocked = source.ValueBlocked;
                    clone.UnrealizedPnl = source.UnrealizedPnl;
                    clone.ValueLiquidCash = source.ValueLiquidCash;
                    clone.ServerType = source.ServerType;
                    clone.ServerUniqueName = source.ServerUniqueName;

                    if (source.PositionOnBoard != null)
                    {
                        clone.PositionOnBoard = new List<PositionOnBoard>(source.PositionOnBoard);
                    }

                    snapshot.Add(clone);
                }

                return snapshot;
            }
        }

        private DateTime _lastTimeGetPortfolio;

        public void GetPortfolios()
        {
            if (_securitiesDictionary.Count == 0)
            {
                return;
            }

            GetPortfolioRecursion(0);
        }

        private void GetPortfolioRecursion(int tryCount)
        {
            try
            {
                tryCount++;

                if (tryCount == 1
                    && _lastTimeGetPortfolio.AddSeconds(5) > DateTime.Now)
                {
                    return;
                }

                _lastTimeGetPortfolio = DateTime.Now;

                GetAccountsResponse accountsResponse = _usersClient.GetAccounts(new GetAccountsRequest(), _gRpcMetadata);

                if (accountsResponse.Accounts.Count == 0)
                {
                    throw new Exception(OsLocalization.Market.Label318);
                }

                for (int i = 0; i < accountsResponse.Accounts.Count; i++)
                {
                    try
                    {
                        Account account = accountsResponse.Accounts[i];

                        if (string.IsNullOrEmpty(account.Id))
                        {
                            continue;
                        }

                        if (account.AccessLevel != AccessLevel.AccountAccessLevelFullAccess) // этот игнорируем, так как ключ API не дает доступа    
                        {
                            continue;
                        }

                        if (account.Type == AccountType.InvestBox) // инвест-копилка - это какая-то неторговая приблуда
                        {
                            continue;
                        }

                        if (account.Type != AccountType.Tinkoff
                            && account.Type != AccountType.TinkoffIis)
                        {
                            continue;
                        }

                        PortfolioRequest portfolioRequest = new PortfolioRequest();
                        portfolioRequest.AccountId = account.Id;

                        PortfolioResponse portfolioResponse = null;

                        try
                        {
                            portfolioResponse = _operationsClient.GetPortfolio(portfolioRequest, _gRpcMetadata);
                        }
                        catch (Exception ex)
                        {
                            // счёт без портфеля не должен теряться молча
                            SendLogMessage("Error getting portfolio. Account: " + account.Id + ". " + ex.ToString(), LogMessageType.System);
                        }

                        if (portfolioResponse != null)
                        {
                            GetMarginAttributesResponse marginAttributes = null;

                            try
                            {
                                marginAttributes = _usersClient.GetMarginAttributes(new GetMarginAttributesRequest() { AccountId = account.Id }, _gRpcMetadata);
                            }
                            catch (Exception ex)
                            {
                                SendLogMessage("Error getting margin attributes. Account: " + account.Id + ". " + ex.ToString(), LogMessageType.System);
                            }

                            GetPortfolios(portfolioResponse, marginAttributes);
                            UpdatePositionsInPortfolio(portfolioResponse, 0);
                        }
                    }
                    catch (Exception ex)
                    {
                        SendLogMessage("Error loading portfolio. Account: " + accountsResponse.Accounts[i].Id + ". " + ex.ToString(), LogMessageType.System);
                    }
                }

                if (_myPortfolios.Count != 0)
                {
                    if (PortfolioEvent != null)
                    {
                        PortfolioEvent(CreatePortfoliosSnapshot());
                    }
                }
                else
                {
                    // нет портфелей. Токен просмотровый
                    if (ServerStatus != ServerConnectStatus.Disconnect)
                    {
                        SendLogMessage(OsLocalization.Market.Label300, LogMessageType.Error);
                        ServerStatus = ServerConnectStatus.Disconnect;
                        DisconnectEvent();
                    }
                }
            }
            catch (Exception ex)
            {
                if (tryCount == 1)
                {// отправляем ещё на один круг. Возможно был кратковременный сбой
                    GetPortfolioRecursion(tryCount);
                }
                else
                {
                    if (ServerStatus != ServerConnectStatus.Disconnect)
                    {
                        SendLogMessage(OsLocalization.Market.Label290 + " \n" + ex.ToString(), LogMessageType.Error);

                        ServerStatus = ServerConnectStatus.Disconnect;
                        DisconnectEvent();
                    }
                }
            }
        }

        private void GetPortfolios(PortfolioResponse portfolioResponse, GetMarginAttributesResponse marginAttributes)
        {
            if (portfolioResponse == null)
            {
                return;
            }

            Portfolio myPortfolio = _myPortfolios.Find(p => p.Number == portfolioResponse.AccountId);

            if (myPortfolio == null)
            {
                myPortfolio = new Portfolio();
                myPortfolio.Number = portfolioResponse.AccountId;
                myPortfolio.ValueCurrent = portfolioResponse.TotalAmountPortfolio != null ? GetValue(portfolioResponse.TotalAmountPortfolio) : 1;
                myPortfolio.ValueBegin = myPortfolio.ValueCurrent;
                _myPortfolios.Add(myPortfolio);
            }
            else
            {
                if (portfolioResponse.TotalAmountPortfolio != null)
                {
                    myPortfolio.ValueCurrent = GetValue(portfolioResponse.TotalAmountPortfolio);
                }
            }

            // вариационная маржа портфеля: общая маржа, начисляемая/списываемая в клиринг (релиз 1.51)
            myPortfolio.UnrealizedPnl = GetTotalVariationMargin(portfolioResponse);

            // ликвидная стоимость портфеля по ставкам риска биржи (UsersService.GetMarginAttributes)
            if (marginAttributes != null
                && marginAttributes.LiquidPortfolio != null)
            {
                myPortfolio.ValueLiquidCash = GetValue(marginAttributes.LiquidPortfolio);
            }
        }

        private decimal GetVariationMargin(Dictionary<string, PortfolioPosition> portfolioPositionsByUid, string instrumentUid)
        {
            PortfolioPosition pos = null;

            if (portfolioPositionsByUid.TryGetValue(instrumentUid, out pos) == false)
            {
                return 0;
            }

            if (pos.VarMarginSettled != null)
            {   // расчётная вар. маржа, которая будет начислена/списана в клиринг
                return GetValue(pos.VarMarginSettled);
            }

            if (pos.VarMargin != null)
            {   // текущая вар. маржа
                return GetValue(pos.VarMargin);
            }

            return 0;
        }

        private decimal GetTotalVariationMargin(PortfolioResponse portfolio)
        {
            if (portfolio.TotalVarMarginSettled != null)
            {   // общая расчётная вар. маржа, которая будет начислена/списана в клиринг
                return GetValue(portfolio.TotalVarMarginSettled);
            }

            if (portfolio.TotalVarMargin != null)
            {   // общая текущая вар. маржа
                return GetValue(portfolio.TotalVarMargin);
            }

            return 0;
        }

        private void UpdatePositionsInPortfolio(PortfolioResponse portfolio, int tryCount)
        {
            lock (_portfolioDataLocker)
            {
                if (portfolio == null)
                {
                    return;
                }

                tryCount++;

                Portfolio portf = _myPortfolios.Find(p => p.Number == portfolio.AccountId);

                if (portf == null)
                {
                    return;
                }

                List<PositionOnBoard> sectionPoses = new List<PositionOnBoard>();

                PositionsRequest positionsRequest = new PositionsRequest();
                positionsRequest.AccountId = portf.Number;

                PositionsResponse posData = null;

                try
                {
                    posData = _operationsClient.GetPositions(positionsRequest, _gRpcMetadata);
                }
                catch (RpcException ex)
                {
                    if (tryCount < 3)
                    {// дополнительно две попытки запросить данные. На случай сбоев связи
                        UpdatePositionsInPortfolio(portfolio, tryCount);
                        return;
                    }
                    else
                    {
                        string message = GetGRPCErrorMessage(ex);
                        SendLogMessage($"Error getting positions in portfolio. Portfolio id: " + portfolio.AccountId + " Info: " + message, LogMessageType.System);
                        return;
                    }
                }
                catch
                {
                    if (tryCount < 3)
                    {// дополнительно две попытки запросить данные. На случай сбоев связи
                        UpdatePositionsInPortfolio(portfolio, tryCount);
                        return;
                    }
                    else
                    {
                        SendLogMessage("Error getting positions in portfolio. Portfolio id: " + portfolio.AccountId, LogMessageType.System);
                        return;
                    }
                }

                // переменные для учёта позиций
                decimal futuresAndOptionsGO = 0;
                decimal spotShortValue = 0;

                // вариационная маржа приходит только в GetPortfolio, GetPositions её не отдаёт
                Dictionary<string, PortfolioPosition> portfolioPositionsByUid = new Dictionary<string, PortfolioPosition>();

                for (int i = 0; i < portfolio.Positions.Count; i++)
                {
                    PortfolioPosition portfolioPos = portfolio.Positions[i];

                    if (string.IsNullOrEmpty(portfolioPos.InstrumentUid) == false
                        && portfolioPositionsByUid.ContainsKey(portfolioPos.InstrumentUid) == false)
                    {
                        portfolioPositionsByUid.Add(portfolioPos.InstrumentUid, portfolioPos);
                    }
                }

                for (int i = 0; i < posData.Securities.Count; i++)
                {
                    PositionsSecurities pos = posData.Securities[i];

                    Security posSecurity = GetSecurityByUidOrApi(pos.InstrumentUid);

                    if (posSecurity == null)
                    {
                        continue;
                    }

                    PositionOnBoard newPos = new PositionOnBoard();

                    newPos.PortfolioName = portf.Number;
                    newPos.ValueCurrent = pos.Balance / posSecurity.Lot;
                    newPos.ValueBlocked = pos.Blocked / posSecurity.Lot;

                    if (newPos.ValueBlocked != 0)
                    {
                        newPos.ValueCurrent += newPos.ValueBlocked;
                    }

                    newPos.ValueBegin = newPos.ValueCurrent;
                    newPos.SecurityNameCode = posSecurity.Name;
                    newPos.SecurityNameClass = posSecurity.NameClass;

                    sectionPoses.Add(newPos);

                    if (pos.Balance < 0
                        && posSecurity.NameClass != null
                        && posSecurity.NameClass.EndsWith(" rub"))
                    {
                        spotShortValue += GetGoByShortSpotOperations(pos.InstrumentUid, newPos.ValueCurrent);
                    }
                }

                for (int i = 0; i < posData.Futures.Count; i++)
                {
                    PositionsFutures pos = posData.Futures[i];

                    Security posSecurity = GetSecurityByUidOrApi(pos.InstrumentUid);

                    if (posSecurity == null)
                    {
                        continue;
                    }

                    PositionOnBoard newPos = new PositionOnBoard();

                    newPos.PortfolioName = portf.Number;
                    newPos.ValueCurrent = pos.Balance / posSecurity.Lot;
                    newPos.ValueBlocked = pos.Blocked / posSecurity.Lot;

                    if (newPos.ValueBlocked != 0)
                    {
                        newPos.ValueCurrent += newPos.ValueBlocked;
                    }

                    newPos.ValueBegin = newPos.ValueCurrent;
                    newPos.SecurityNameCode = posSecurity.Name;
                    newPos.SecurityNameClass = posSecurity.NameClass;

                    // вариационная маржа по фьючерсу
                    newPos.UnrealizedPnl = GetVariationMargin(portfolioPositionsByUid, pos.InstrumentUid);

                    sectionPoses.Add(newPos);

                    if (posSecurity.NameClass != null
                        && posSecurity.NameClass.EndsWith(" rub"))
                    {
                        futuresAndOptionsGO += GetGoByFuturesOrOptions(pos.InstrumentUid, newPos.ValueCurrent);
                    }
                }

                for (int i = 0; i < posData.Options.Count; i++)
                {
                    PositionsOptions pos = posData.Options[i];

                    Security posSecurity = GetSecurityByUidOrApi(pos.InstrumentUid);

                    if (posSecurity == null)
                    {
                        continue;
                    }

                    PositionOnBoard newPos = new PositionOnBoard();

                    newPos.PortfolioName = portf.Number;
                    newPos.ValueCurrent = pos.Balance / posSecurity.Lot;
                    newPos.ValueBlocked = pos.Blocked / posSecurity.Lot;

                    if (newPos.ValueBlocked != 0)
                    {
                        newPos.ValueCurrent += newPos.ValueBlocked;
                    }

                    newPos.ValueBegin = newPos.ValueCurrent;
                    newPos.SecurityNameCode = posSecurity.Name;
                    newPos.SecurityNameClass = posSecurity.NameClass;

                    // вариационная маржа по опциону
                    newPos.UnrealizedPnl = GetVariationMargin(portfolioPositionsByUid, pos.InstrumentUid);

                    sectionPoses.Add(newPos);

                    if (posSecurity.NameClass != null
                        && posSecurity.NameClass.EndsWith(" rub"))
                    {
                        futuresAndOptionsGO += GetGoByFuturesOrOptions(pos.InstrumentUid, newPos.ValueCurrent);
                    }
                }

                PortfolioPosition rubPosition = null;
                for (int i = 0; i < portfolio.Positions.Count; i++)
                {
                    if (portfolio.Positions[i].Figi == "RUB000UTSTOM")
                    {
                        rubPosition = portfolio.Positions[i];
                        break;
                    }
                }

                // Блокированные средства по портфелю целиком

                decimal valueBlock = 0;

                for (int i = 0; i < portfolio.Positions.Count; i++)
                {
                    if (portfolio.Positions[i].InstrumentType == "currency")
                    {
                        valueBlock += GetValue(portfolio.Positions[i].BlockedLots) * GetValue(portfolio.Positions[i].AveragePositionPrice);
                    }
                }

                portf.ValueBlocked = valueBlock;

                // Денежная позиция в портфеле

                for (int i = 0; i < posData.Money.Count; i++)
                {
                    MoneyValue posMoney = posData.Money[i];

                    PositionOnBoard newPos = new PositionOnBoard();
                    newPos.SecurityNameCode = posMoney.Currency;
                    newPos.PortfolioName = portf.Number;

                    if (newPos.SecurityNameCode == "rub")
                    {
                        decimal valuePortfolio = GetValue(posMoney);

                        decimal blockRub = portf.ValueBlocked;

                        newPos.ValueCurrent = valuePortfolio - blockRub + GetTotalVariationMargin(portfolio); // - futuresAndOptionsGO; // -spotShortValue;

                        /*if(portf.ValueBlocked != 0)
                        {
                            newPos.ValueCurrent -= portf.ValueBlocked;
                        }*/
                    }
                    else
                    {
                        newPos.ValueCurrent = GetValue(posMoney);
                    }

                    newPos.ValueBegin = newPos.ValueCurrent;

                    sectionPoses.Add(newPos);
                }

                // удаляем не существующие на текущий момент позиции из портфеля

                for (int i = 0; portf.PositionOnBoard != null && i < portf.PositionOnBoard.Count; i++)
                {
                    PositionOnBoard pos = portf.PositionOnBoard[i];

                    if (pos.ValueCurrent == 0)
                    {
                        continue;
                    }

                    if (sectionPoses.Count == 0
                        || sectionPoses.Find(p => p.SecurityNameCode == pos.SecurityNameCode) == null)
                    {
                        portf.PositionOnBoard.RemoveAt(i);
                        i--;
                    }
                }

                // обновляем в портфеле существующие позиции

                for (int i = 0; i < sectionPoses.Count; i++)
                {
                    portf.SetNewPosition(sectionPoses[i]);
                }
            }
        }

        private decimal GetGoByShortSpotOperations(string tickerId, decimal volume)
        {
            if (volume >= 0)
            {
                return 0;
            }

            Security mySecurity = _securities.Find(s => s.NameId == tickerId);

            if (mySecurity == null)
            {
                return 0;
            }

            GetLastPricesRequest request = new GetLastPricesRequest();
            request.InstrumentId.Add(tickerId);
            request.LastPriceType = _filterOutDealerData ? LastPriceType.LastPriceExchange : LastPriceType.LastPriceUnspecified;

            GetLastPricesResponse response = _marketDataServiceClient.GetLastPrices(request, _gRpcMetadata);

            if (response == null
                || response.LastPrices == null
                || response.LastPrices.Count == 0)
            {
                return 0;
            }

            decimal lastPrice = GetValue(response.LastPrices[0].Price);

            if (lastPrice == 0)
            {
                return 0;
            }


            decimal result = -(volume * lastPrice * mySecurity.Lot) * 2;

            return result;

        }

        private decimal GetGoByFuturesOrOptions(string tickerId, decimal volume)
        {
            if (volume == 0)
            {
                return 0;
            }

            Security mySecurity = _securities.Find(s => s.NameId == tickerId);

            if (mySecurity == null)
            {
                return 0;
            }

            if (volume > 0)
            {
                decimal result = volume * mySecurity.MarginBuy;
                return result;
            }
            else
            {
                decimal result = -volume * mySecurity.MarginSell;
                return result;
            }
        }

        public event Action<List<Portfolio>> PortfolioEvent;

        #endregion

        #region 5 Data

        // https://russianinvestments.github.io/investAPI/limits/
        // Сервис котировок: лимит 600 запр/мин суммарно по всем методам и счетам. Держим 570 с запасом
        private RateGate _rateGateMarketData = new RateGate(570, TimeSpan.FromMinutes(1));

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

        public List<Candle> GetCandleDataToSecurity(Security security, TimeFrameBuilder timeFrameBuilder, DateTime startTime, DateTime endTime,
            DateTime actualTime)
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

                if (range == null) // Если запрошен некорректный таймфрейм, то возвращает null
                    return null;

                candles.AddRange(range);

                startTime = endDateTime;
            }

            // под конец фильтруем одинаковые от брокера
            return filterCorrectCandles(candles);
        }

        private List<Candle> filterCorrectCandles(List<Candle> candles)
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

        private List<Candle> GetCandleHistoryFromDays(DateTime fromDateTime, DateTime toDateTime, Security security, TimeFrame tf, int tryCount)
        {
            CandleInterval requestedCandleInterval = CreateTimeFrameInterval(tf);

            if (requestedCandleInterval == CandleInterval.Unspecified)
                return null;

            Timestamp from = Timestamp.FromDateTime(fromDateTime);
            Timestamp to = Timestamp.FromDateTime(toDateTime);

            GetCandlesResponse candlesResp = null;
            int retries = 3; // try to get 'em this many times

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
                    // всегда запрашиваем все свечи: дилерские свечи выходного дня
                    // отбрасываем на клиенте по их тегу источника (см. ConvertToOsEngineCandles)
                    getCandlesRequest.CandleSourceType = GetCandlesRequest.Types.CandleSource.IncludeWeekend;

                    candlesResp = _marketDataServiceClient.GetCandles(getCandlesRequest, _gRpcMetadata);
                }
                catch (RpcException ex)
                {
                    string message = GetGRPCErrorMessage(ex);

                    if (message == "no server message")
                    {
                        SendLogMessage($"Couldn't get candles for {security.Name}. Info: probably invalid time interval {fromDateTime}UTC - {toDateTime}UTC", LogMessageType.System);
                        _getCandlesErrorsCount++;
                        Thread.Sleep(300);
                    }
                    else
                    {
                        SendLogMessage($"Error getting candles for {security.Name}. Info: {message}", LogMessageType.System);
                        _getCandlesErrorsCount++;
                        Thread.Sleep(300);
                    }
                }
                catch (Exception ex)
                {
                    if (ServerStatus == ServerConnectStatus.Disconnect)
                    {
                        _getCandlesErrorsCount = 0;
                        break; // connection broke before we could get candles
                    }

                    _getCandlesErrorsCount++;
                    Thread.Sleep(300);

                    SendLogMessage($"Error getting candles for {security.Name}: " + ex.ToString(),
                        LogMessageType.System);
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

            if (candles == null
                || candles.Count == 0)
            {
                if (_getCandlesErrorsCount >= 8
                     && ServerStatus != ServerConnectStatus.Disconnect)
                {
                    SendLogMessage(OsLocalization.Market.Label322 + "\n Security: " + security.Name, LogMessageType.Error);
                    ServerStatus = ServerConnectStatus.Disconnect;
                    DisconnectEvent();
                }
            }

            if (tf == TimeFrame.Day
                && candles != null)
            {
                for (int i = 0; i < candles.Count; i++)
                {
                    candles[i].TimeStart = candles[i].TimeStart.Date;
                }
            }

            _getCandlesErrorsCount = 0;
            return candles;
        }

        private int _getCandlesErrorsCount;

        // расписания торгов разных бирж по дням
        private Dictionary<DateTime, TradingSchedulesResponse> _tradingSchedules = new Dictionary<DateTime, TradingSchedulesResponse>();

        bool isTodayATradingDayForSecurity(Security security)
        {
            if (security == null)
                return true;

            string exchangeToAskSchedule = security.Exchange.Split('_')[0];

            TradingSchedulesResponse thisDaySchedules = null;

            DateTime mskNow = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, _mskTimeZone);
            DateTime mskDate = mskNow.Date;

            if (_tradingSchedules.ContainsKey(mskDate))
            {
                thisDaySchedules = _tradingSchedules[mskDate];
            }
            else
            {
                Timestamp from = Timestamp.FromDateTime(DateTime.UtcNow.Date);
                Timestamp to = Timestamp.FromDateTime(TimeZoneInfo.ConvertTimeToUtc(mskDate.AddDays(1).AddTicks(-1), _mskTimeZone));

                TradingSchedulesRequest tradingSchedulesRequest = new TradingSchedulesRequest();
                tradingSchedulesRequest.From = from;
                tradingSchedulesRequest.To = to;

                try
                {
                    _rateGateInstruments.WaitToProceed();
                    thisDaySchedules = _instrumentsClient.TradingSchedules(tradingSchedulesRequest, _gRpcMetadata);
                }
                catch (RpcException ex)
                {
                    string message = GetGRPCErrorMessage(ex);
                    SendLogMessage($"Error getting trading schedules. Info: {message}", LogMessageType.System);
                }
                catch (Exception ex)
                {
                    SendLogMessage($"Error fetching trading schedules: {ex.ToString()}", LogMessageType.System);
                }

                _tradingSchedules[mskDate] = thisDaySchedules;
            }

            if (thisDaySchedules == null)
                return true;

            TradingDay day = null;
            for (int i = 0; i < thisDaySchedules.Exchanges.Count; i++)
            {
                if (thisDaySchedules.Exchanges[i].Exchange == exchangeToAskSchedule)
                {
                    day = thisDaySchedules.Exchanges[i].Days[0];
                    break;
                }
            }

            if (day != null)
                return day.IsTradingDay;

            return true;
        }

        private List<Candle> ConvertToOsEngineCandles(GetCandlesResponse response, Security security)
        {
            List<Candle> candles = new List<Candle>();

            if (response == null)
                return candles;

            for (int i = 0; i < response.Candles.Count; i++)
            {
                HistoricCandle histCandle = response.Candles[i];

                // дилерские свечи торгов выходного дня отбрасываем при включённом фильтре;
                // биржевые свечи выходных сессий MOEX (тег Exchange) остаются
                if (_filterOutDealerData
                    && histCandle.CandleSource == CandleSource.DealerWeekend)
                {
                    continue;
                }

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
        private static readonly Lazy<X509Certificate2[]> _tInvestCertificates = new Lazy<X509Certificate2[]>(LoadTInvestCertificates);
        private Metadata _gRpcMetadata;

        private GrpcChannel _channel;
        private CancellationTokenSource _cancellationTokenSource;
        private WebProxy _proxy;

        private UsersService.UsersServiceClient _usersClient;
        private OperationsService.OperationsServiceClient _operationsClient;
        private OperationsStreamService.OperationsStreamServiceClient _operationsStreamClient;
        private InstrumentsService.InstrumentsServiceClient _instrumentsClient;
        private MarketDataService.MarketDataServiceClient _marketDataServiceClient;
        private MarketDataStreamService.MarketDataStreamServiceClient _marketDataStreamClient;
        private OrdersService.OrdersServiceClient _ordersClient;
        private OrdersStreamService.OrdersStreamServiceClient _ordersStreamClient;
        private StopOrdersService.StopOrdersServiceClient _stopOrdersClient;

        private void GetUserLimits()
        {
            GetUserTariffRequest request = new GetUserTariffRequest();
            GetUserTariffResponse response = null;
            try
            {
                response = _usersClient.GetUserTariff(request, _gRpcMetadata);
            }
            catch (RpcException ex)
            {
                string message = GetGRPCErrorMessage(ex);
                SendLogMessage($"Error getting user limits. Info: {message}", LogMessageType.System);
            }
            catch (Exception ex)
            {
                SendLogMessage(ex.ToString(), LogMessageType.System);
            }

            if (response == null)
                return;

            string limits = "";
            for (int i = 0; i < response.StreamLimits.Count; i++)
            {
                StreamLimit sl = response.StreamLimits[i];
                limits += $"\n {sl.Open}/{sl.Limit}: {sl.Streams}";

                for (int j = 0; j < sl.Streams.Count; j++)
                {
                    if (sl.Streams[j].Contains("MarketDataStreamService"))
                    {
                        // запас в один стрим - на переподключение
                        _maxMarketDataStreams = Math.Max(2, sl.Limit - 1);
                    }
                }
            }

            SendLogMessage($"User stream limits: {limits}", LogMessageType.User);
        }

        private static X509Certificate2[] LoadTInvestCertificates()
        {
            System.Reflection.Assembly assembly = typeof(TInvestServer).Assembly;

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

        private void CreateStreamsConnection()
        {
            try
            {
                _gRpcMetadata = new Metadata();

                _gRpcMetadata.Add("Authorization", $"Bearer {_accessToken}");
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

                // gRPC через прокси с авторизацией: при 407 .NET открывает повторное соединение,
                // которое клиентский балансировщик не может привязать к subchannel.
                // Отключаем балансировку (внутренний ключ Grpc.Net.Client 2.71.0) — для одного фиксированного хоста она не нужна
                if (_proxy != null)
                {
                    socketsHandler.Properties["__GrpcLoadBalancingDisabled"] = true;
                    SendLogMessage("T-Invest: client-side gRPC load balancing disabled (proxy connection)", LogMessageType.System);
                }

                _channel = GrpcChannel.ForAddress(_gRPCHost, new GrpcChannelOptions
                {
                    Credentials = ChannelCredentials.SecureSsl,
                    HttpHandler = socketsHandler
                });

                _usersClient = new UsersService.UsersServiceClient(_channel);
                _operationsClient = new OperationsService.OperationsServiceClient(_channel);
                _operationsStreamClient = new OperationsStreamService.OperationsStreamServiceClient(_channel);
                _instrumentsClient = new InstrumentsService.InstrumentsServiceClient(_channel);
                _ordersClient = new OrdersService.OrdersServiceClient(_channel);
                _ordersStreamClient = new OrdersStreamService.OrdersStreamServiceClient(_channel);
                _stopOrdersClient = new StopOrdersService.StopOrdersServiceClient(_channel);
                _marketDataServiceClient = new MarketDataService.MarketDataServiceClient(_channel);
                _marketDataStreamClient = new MarketDataStreamService.MarketDataStreamServiceClient(_channel);

                try
                {
                    ServerStatus = ServerConnectStatus.Connect;

                    // сначала справочник бумаг и портфели:
                    // стримы должны стартовать с реальными номерами счетов
                    GetSecurities();

                    if (ServerStatus != ServerConnectStatus.Connect
                        || _myPortfolios.Count == 0)
                    {
                        // загрузка не удалась - ConnectEvent не вызываем,
                        // движок штатно переподключится
                        return;
                    }

                    GetUserLimits();
                    ConnectGRPCStreams();

                    // ConnectEvent - только когда всё реально живо
                    ConnectEvent();
                }
                catch (Exception ex)
                {
                    SendLogMessage(ex.ToString(), LogMessageType.System);
                }
            }
            catch (Exception exception)
            {
                SendLogMessage(exception.ToString(), LogMessageType.System);
            }
        }

        private void ConnectGRPCStreams()
        {
            RepeatedField<string> accountsList = new RepeatedField<string>();
            for (int i = 0; i < _myPortfolios.Count; i++)
            {
                accountsList.Add(_myPortfolios[i].Number);
            }

            //_myTradesDataStream = _ordersStreamClient.TradesStream(new TradesStreamRequest
            //{
            //    Accounts = { accountsList }
            //}, headers: _gRpcMetadata, cancellationToken: _cancellationTokenSource.Token);

            // пинги каждые 5 секунд - быстрый детект мёртвого стрима watchdog'ом
            _myOrderStateDataStream = _ordersStreamClient.OrderStateStream(new OrderStateStreamRequest
            {
                Accounts = { accountsList },
                PingDelayMillis = 5000
            }, headers: _gRpcMetadata, cancellationToken: _cancellationTokenSource.Token);

            _portfolioDataStream =
                _operationsStreamClient.PortfolioStream(new PortfolioStreamRequest { Accounts = { accountsList }, PingSettings = new PingDelaySettings { PingDelayMs = 5000 } },
                    headers: _gRpcMetadata, cancellationToken: _cancellationTokenSource.Token);

            _positionsDataStream =
                _operationsStreamClient.PositionsStream(new PositionsStreamRequest { Accounts = { accountsList }, WithInitialPositions = true, PingSettings = new PingDelaySettings { PingDelayMs = 5000 } },
                    headers: _gRpcMetadata, cancellationToken: _cancellationTokenSource.Token);

            _lastPortfolioDataTime = DateTime.UtcNow;
            _lastPositionsDataTime = DateTime.UtcNow;
            _lastMarketDataTime = DateTime.UtcNow;
            _lastMyOrderStateDataTime = DateTime.UtcNow;
        }

        #endregion

        #region 6 gRPC streams fast reconnect

        private DateTime _lastTryReconnectPortfolioStream;

        private object _portfolioStreamReconnectLocker = new object();

        private bool TryReconnectPortfolioStream()
        {
            try
            {
                // лок на весь цикл реконнекта: watchdog и ридер не должны
                // пересоздать стрим одновременно (иначе стрим-сирота на сервере)
                lock (_portfolioStreamReconnectLocker)
                {
                    if (_lastTryReconnectPortfolioStream != DateTime.MinValue
                     && _lastTryReconnectPortfolioStream.AddSeconds(30) > DateTime.Now)
                    {
                        return false;
                    }

                    _lastTryReconnectPortfolioStream = DateTime.Now;

                    if (_portfolioDataStream != null)
                    {
                        try
                        {
                            _portfolioDataStream.Dispose();
                        }
                        catch
                        {
                            // ignore
                        }
                    }

                    RepeatedField<string> accountsList = new RepeatedField<string>();
                    for (int i = 0; i < _myPortfolios.Count; i++)
                    {
                        accountsList.Add(_myPortfolios[i].Number);
                    }

                    _portfolioDataStream =
                        _operationsStreamClient.PortfolioStream(new PortfolioStreamRequest { Accounts = { accountsList }, PingSettings = new PingDelaySettings { PingDelayMs = 5000 } },
                            headers: _gRpcMetadata, cancellationToken: _cancellationTokenSource.Token);
                    _lastPortfolioDataTime = DateTime.UtcNow;
                }
            }
            catch
            {
                return false;
            }

            try
            {
                GetPortfolios();
            }
            catch
            {
                // ignore
            }

            return true;
        }

        private DateTime _lastTryReconnectPositionsStream;

        private object _positionsStreamReconnectLocker = new object();

        private bool TryReconnectPositionsStream()
        {
            try
            {
                lock (_positionsStreamReconnectLocker)
                {
                    if (_lastTryReconnectPositionsStream != DateTime.MinValue
                        && _lastTryReconnectPositionsStream.AddSeconds(30) > DateTime.Now)
                    {
                        return false;
                    }

                    _lastTryReconnectPositionsStream = DateTime.Now;

                    if (_positionsDataStream != null)
                    {
                        try
                        {
                            _positionsDataStream.Dispose();
                        }
                        catch
                        {
                            // ignore
                        }
                    }

                    if (_myPortfolios.Count == 0)
                    {
                        return false;
                    }

                    RepeatedField<string> accountsList = new RepeatedField<string>();
                    for (int i = 0; i < _myPortfolios.Count; i++)
                    {
                        accountsList.Add(_myPortfolios[i].Number);
                    }

                    _positionsDataStream =
                        _operationsStreamClient.PositionsStream(new PositionsStreamRequest { Accounts = { accountsList }, WithInitialPositions = true, PingSettings = new PingDelaySettings { PingDelayMs = 5000 } },
                            headers: _gRpcMetadata, cancellationToken: _cancellationTokenSource.Token);

                    _lastPositionsDataTime = DateTime.UtcNow;
                }
            }
            catch
            {
                return false;
            }

            try
            {
                GetPortfolios();
            }
            catch
            {
                // ignore
            }

            return true;
        }

        private DateTime _lastTryReconnectOrdersStream;

        private object _ordersStreamReconnectLocker = new object();

        private bool TryReconnectOrdersStream()
        {
            try
            {
                lock (_ordersStreamReconnectLocker)
                {
                    if (_lastTryReconnectOrdersStream != DateTime.MinValue
                        && _lastTryReconnectOrdersStream.AddSeconds(30) > DateTime.Now)
                    {
                        return false;
                    }

                    _lastTryReconnectOrdersStream = DateTime.Now;

                    if (_myOrderStateDataStream != null)
                    {
                        try
                        {
                            _myOrderStateDataStream.Dispose();
                        }
                        catch
                        {
                            // ignore
                        }
                    }

                    RepeatedField<string> accountsList = new RepeatedField<string>();
                    for (int i = 0; i < _myPortfolios.Count; i++)
                    {
                        accountsList.Add(_myPortfolios[i].Number);
                    }

                    _myOrderStateDataStream = _ordersStreamClient.OrderStateStream(new OrderStateStreamRequest
                    {
                        Accounts = { accountsList },
                        PingDelayMillis = 5000
                    }, headers: _gRpcMetadata, cancellationToken: _cancellationTokenSource.Token);
                }
            }
            catch
            {
                return false;
            }

            return true;
        }

        private void CloseDataStreamClient(MarketDataStreamWrapper streamWrapper, TimeSpan waitTimeout)
        {
            if (streamWrapper == null)
            {
                return;
            }

            if (streamWrapper.StreamClient != null)
            {
                try
                {
                    // вежливое закрытие: сообщаем серверу, что запросов больше не будет
                    Task completeTask = streamWrapper.StreamClient.RequestStream.CompleteAsync();
                    if (completeTask.Wait(waitTimeout) == false)
                    {
                        ObserveTaskFault(completeTask);
                    }
                }
                catch (Exception ex)
                {
                    SendLogMessage("Error completing market data stream. " + ex.ToString(), LogMessageType.System);
                }

                try
                {
                    streamWrapper.StreamClient.Dispose();
                }
                catch (Exception ex)
                {
                    SendLogMessage("Error disposing market data stream. " + ex.ToString(), LogMessageType.System);
                }

                streamWrapper.StreamClient = null;
            }

            // дожидаемся завершения читателя, чтобы он не трогал состояние после закрытия
            Task readingTask = streamWrapper.ReadingTask;
            if (readingTask != null)
            {
                try
                {
                    if (readingTask.Wait(waitTimeout) == false)
                    {
                        ObserveTaskFault(readingTask);
                    }
                }
                catch
                {
                    // ignore: исключение читателя уже обработано в нём самом
                }

                streamWrapper.ReadingTask = null;
            }

            streamWrapper.IsConnected = false;
        }

        private bool TryReconnectDataStream(MarketDataStreamWrapper streamWrapper)
        {
            try
            {
                lock (_marketDataStreamLocker)
                {
                    CloseDataStreamClient(streamWrapper, _streamWaitTimeout);

                    streamWrapper.StreamClient = _marketDataStreamClient.MarketDataStream(headers: _gRpcMetadata,
                        cancellationToken: _cancellationTokenSource.Token);

                    streamWrapper.ReadingTask = Task.Run(() => ReadStream(streamWrapper));

                    streamWrapper.IsConnected = true;
                    streamWrapper.LastMessageTime = DateTime.UtcNow;

                    // просим сервер пинговать каждые 5 секунд - быстрый детект мёртвого стрима.
                    // Нефатально: если запись не прошла - стрим живёт без ускоренных пингов
                    try
                    {
                        MarketDataRequest pingSettingsRequest = new MarketDataRequest();
                        pingSettingsRequest.PingSettings = new PingDelaySettings { PingDelayMs = 5000 };

                        Task pingWriteTask = streamWrapper.StreamClient.RequestStream.WriteAsync(pingSettingsRequest);
                        if (pingWriteTask.Wait(_streamWaitTimeout) == false)
                        {
                            ObserveTaskFault(pingWriteTask);
                            SendLogMessage("Ping settings write timeout on " + streamWrapper.Name, LogMessageType.System);
                        }
                    }
                    catch (Exception ex)
                    {
                        SendLogMessage("Ping settings write failed on " + streamWrapper.Name + ": " + ex.Message, LogMessageType.System);
                    }

                    if (streamWrapper.Subscriptions.Count > 0)
                    {
                        SubscribeTradesRequest tradesToResubscribe = new SubscribeTradesRequest { SubscriptionAction = SubscriptionAction.Subscribe };
                        SubscribeOrderBookRequest orderBooksToResubscribe = new SubscribeOrderBookRequest { SubscriptionAction = SubscriptionAction.Subscribe };
                        SubscribeLastPriceRequest lastPricesToResubscribe = new SubscribeLastPriceRequest { SubscriptionAction = SubscriptionAction.Subscribe };
                        SubscribeCandlesRequest candlesToResubscribe = new SubscribeCandlesRequest { SubscriptionAction = SubscriptionAction.Subscribe };

                        // Consolidate all individual subscriptions into batch requests
                        foreach (MarketDataRequest sub in streamWrapper.Subscriptions)
                        {
                            if (sub.SubscribeTradesRequest != null)
                            {
                                tradesToResubscribe.Instruments.AddRange(sub.SubscribeTradesRequest.Instruments);
                                tradesToResubscribe.TradeSource = sub.SubscribeTradesRequest.TradeSource;
                                tradesToResubscribe.WithOpenInterest = sub.SubscribeTradesRequest.WithOpenInterest;
                            }
                            else if (sub.SubscribeOrderBookRequest != null)
                            {
                                orderBooksToResubscribe.Instruments.AddRange(sub.SubscribeOrderBookRequest.Instruments);
                            }
                            else if (sub.SubscribeLastPriceRequest != null)
                            {
                                lastPricesToResubscribe.Instruments.AddRange(sub.SubscribeLastPriceRequest.Instruments);
                            }
                            else if (sub.SubscribeCandlesRequest != null)
                            {
                                candlesToResubscribe.Instruments.AddRange(sub.SubscribeCandlesRequest.Instruments);
                            }
                        }

                        _rateGateSubscribeCommon.WaitToProceed();

                        if (tradesToResubscribe.Instruments.Any())
                        {
                            MarketDataRequest batchTradeRequest = new MarketDataRequest { SubscribeTradesRequest = tradesToResubscribe };
                            Task writeTradeTask = streamWrapper.StreamClient.RequestStream.WriteAsync(batchTradeRequest);
                            if (writeTradeTask.Wait(_streamWaitTimeout) == false)
                            {
                                ObserveTaskFault(writeTradeTask);
                                streamWrapper.IsConnected = false;
                                return false;
                            }
                            _rateGateSubscribeCommon.WaitToProceed();
                        }
                        if (orderBooksToResubscribe.Instruments.Any())
                        {
                            MarketDataRequest batchOrderBookRequest = new MarketDataRequest { SubscribeOrderBookRequest = orderBooksToResubscribe };
                            Task writeOrderBookTask = streamWrapper.StreamClient.RequestStream.WriteAsync(batchOrderBookRequest);
                            if (writeOrderBookTask.Wait(_streamWaitTimeout) == false)
                            {
                                ObserveTaskFault(writeOrderBookTask);
                                streamWrapper.IsConnected = false;
                                return false;
                            }
                            _rateGateSubscribeCommon.WaitToProceed();
                        }
                        if (lastPricesToResubscribe.Instruments.Any())
                        {
                            MarketDataRequest batchLastPriceRequest = new MarketDataRequest { SubscribeLastPriceRequest = lastPricesToResubscribe };
                            Task writeLastPriceTask = streamWrapper.StreamClient.RequestStream.WriteAsync(batchLastPriceRequest);
                            if (writeLastPriceTask.Wait(_streamWaitTimeout) == false)
                            {
                                ObserveTaskFault(writeLastPriceTask);
                                streamWrapper.IsConnected = false;
                                return false;
                            }
                            _rateGateSubscribeCommon.WaitToProceed();
                        }
                        if (candlesToResubscribe.Instruments.Any())
                        {
                            MarketDataRequest batchCandlesRequest = new MarketDataRequest { SubscribeCandlesRequest = candlesToResubscribe };
                            Task writeCandlesTask = streamWrapper.StreamClient.RequestStream.WriteAsync(batchCandlesRequest);
                            if (writeCandlesTask.Wait(_streamWaitTimeout) == false)
                            {
                                ObserveTaskFault(writeCandlesTask);
                                streamWrapper.IsConnected = false;
                                return false;
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                SendLogMessage("Reconnect data stream failed: " + streamWrapper.Name + ". " + ex.ToString(), LogMessageType.System);
                return false;
            }

            return true;
        }

        public event Action ForceCheckOrdersAfterReconnectEvent;

        #endregion

        #region 7 Security subscribe

        // Для всех типов подписок в методе установлены ограничения максимального количества запросов на подписку.
        // Если количество запросов за минуту превысит 100, то для всех элементов будет установлен статус SUBSCRIPTION_STATUS_TOO_MANY_REQUESTS.
        private RateGate _rateGateSubscribeMd = new RateGate(1, TimeSpan.FromMilliseconds(650));
        private RateGate _rateGateSubscribeCommon = new RateGate(1, TimeSpan.FromMilliseconds(650));

        private AsyncServerStreamingCall<OrderStateStreamResponse> _myOrderStateDataStream;
        private AsyncServerStreamingCall<PortfolioStreamResponse> _portfolioDataStream;
        private AsyncServerStreamingCall<PositionsStreamResponse> _positionsDataStream;

        // Для всех типов подписок в методе установлены ограничения максимального количества запросов на подписку. Если количество запросов за минуту превысит 100, то для всех элементов будет установлен статус SUBSCRIPTION_STATUS_TOO_MANY_REQUESTS.
        // мы подписываемся на стаканы+сделки, поэтому лимит пополам
        private List<MarketDataStreamWrapper> _marketDataStreams;
        private Dictionary<string, MarketDataStreamWrapper> _securityStreamMap;
        List<Security> _pollSubscribedSecurities = new List<Security>();
        private bool _useStreamForMarketData = true;

        private DateTime _lastMarketDataTime = DateTime.MinValue;
        private DateTime _lastPortfolioDataTime = DateTime.MinValue;
        private DateTime _lastPositionsDataTime = DateTime.MinValue;
        private DateTime _lastMyOrderStateDataTime = DateTime.MinValue;

        private string _marketDataStreamLocker = "_marketDataStreamLocker";

        private static readonly TimeSpan _streamWaitTimeout = TimeSpan.FromSeconds(5);

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

        public void Subscribe(Security security)
        {
            SubscribeLoop(0, security);
        }

        private void SubscribeLoop(int tryCount, Security security)
        {
            // запросы, добавленные на этом вызове - для полного отката при ошибке
            MarketDataRequest tradesRequest = null;
            MarketDataRequest orderBookRequest = null;
            MarketDataRequest lastPriceRequest = null;

            MarketDataStreamWrapper tradesStream = null;
            MarketDataStreamWrapper depthStream = null;

            try
            {
                tryCount++;

                if (_securityStreamMap.ContainsKey(security.NameId) ||
                    _pollSubscribedSecurities.Any(s => s.Name == security.Name))
                {
                    return;
                }

                // 1 берём общий сокет, либо создаём новый. У него в конце имени запись "Common"

                MarketDataStreamWrapper streamWrapperCommon =
                             _marketDataStreams.FirstOrDefault(s => s.Subscriptions.Count < 99 // 99 topics per stream. 
                             && s.IsConnected == true
                             && s.Name.EndsWith("Common"));

                if (streamWrapperCommon == null)
                {
                    if (_marketDataStreams.Count < _maxMarketDataStreams)
                    {
                        streamWrapperCommon = new MarketDataStreamWrapper()
                        {
                            Name = "Market data stream " + (_marketDataStreams.Count + 1) + " Common",
                            IsConnected = false,
                            LastMessageTime = DateTime.UtcNow,
                        };
                        _marketDataStreams.Add(streamWrapperCommon);

                        if (TryReconnectDataStream(streamWrapperCommon) == false)
                        {
                            SendLogMessage("Failed to create market data stream for " + security.Name, LogMessageType.System);
                            return;
                        }

                        SendLogMessage("Created market data stream: " + streamWrapperCommon.Name, LogMessageType.System);
                    }
                    else
                    {
                        _useStreamForMarketData = false;
                        SendLogMessage("Switching to polling mode for new market data subscriptions.", LogMessageType.System);
                        _pollSubscribedSecurities.Add(security);
                        return;
                    }
                }

                // 2 берём сокет для стаканов, либо создаём новый. У него в конце имени запись "MarketDepth"

                MarketDataStreamWrapper streamWrapperMarketDepth =
                  _marketDataStreams.FirstOrDefault(s => s.Subscriptions.Count < 99 // 99 topics per stream. 
                  && s.IsConnected == true
                  && s.Name.EndsWith("MarketDepth"));

                if (streamWrapperMarketDepth == null)
                {
                    if (_marketDataStreams.Count < _maxMarketDataStreams)
                    {
                        streamWrapperMarketDepth = new MarketDataStreamWrapper()
                        {
                            Name = "Market data stream " + (_marketDataStreams.Count + 1) + " MarketDepth",
                            IsConnected = false,
                            LastMessageTime = DateTime.UtcNow,
                        };
                        _marketDataStreams.Add(streamWrapperMarketDepth);

                        if (TryReconnectDataStream(streamWrapperMarketDepth) == false)
                        {
                            SendLogMessage("Failed to create market data stream for " + security.Name, LogMessageType.System);
                            return;
                        }

                        SendLogMessage("Created market data stream: " + streamWrapperMarketDepth.Name, LogMessageType.System);
                    }
                    else
                    {
                        _useStreamForMarketData = false;
                        SendLogMessage("Switching to polling mode for new market data subscriptions.", LogMessageType.System);
                        _pollSubscribedSecurities.Add(security);
                        return;
                    }
                }

                if (_useStreamForMarketData)
                {
                    lock (_marketDataStreamLocker)
                    {
                        tradesStream = streamWrapperCommon;
                        depthStream = streamWrapperMarketDepth;

                        // стрим мог быть закрыт реконнектом/Dispose между выбором обёртки и входом в лок
                        if (streamWrapperCommon.StreamClient == null
                            || streamWrapperCommon.IsConnected == false
                            || (security.SecurityType != SecurityType.Index
                                && (streamWrapperMarketDepth.StreamClient == null
                                    || streamWrapperMarketDepth.IsConnected == false)))
                        {
                            throw new Exception("Market data stream is not connected");
                        }

                        if (security.SecurityType == SecurityType.Index)
                        {// Подписка индекса. Один поток
                            LastPriceInstrument instrument = new LastPriceInstrument
                            {
                                InstrumentId = security.NameId
                            };

                            SubscribeLastPriceRequest lpRequest = new SubscribeLastPriceRequest
                            {
                                SubscriptionAction = SubscriptionAction.Subscribe,
                                Instruments = { instrument },
                            };
                            MarketDataRequest marketDataRequest = new MarketDataRequest();
                            marketDataRequest.SubscribeLastPriceRequest = lpRequest;

                            lastPriceRequest = marketDataRequest;
                            streamWrapperCommon.Subscriptions.Add(marketDataRequest);

                            _rateGateSubscribeCommon.WaitToProceed();
                            Task writeIndexTask = streamWrapperCommon.StreamClient.RequestStream.WriteAsync(marketDataRequest);
                            if (writeIndexTask.Wait(_streamWaitTimeout) == false)
                            {
                                ObserveTaskFault(writeIndexTask);
                                streamWrapperCommon.IsConnected = false;
                                throw new TimeoutException("Market data stream write timeout");
                            }
                        }
                        else
                        { // Обычный инструмент

                            // 1 Подписка на ленту сделок

                            TradeInstrument tradeInstrument = new TradeInstrument();
                            tradeInstrument.InstrumentId = security.NameId;

                            SubscribeTradesRequest subscribeTradesRequest = new SubscribeTradesRequest
                            {
                                SubscriptionAction = SubscriptionAction.Subscribe,
                                Instruments = { tradeInstrument },
                                TradeSource = _filterOutDealerData
                                    ? TradeSourceType.TradeSourceExchange
                                    : TradeSourceType.TradeSourceAll,
                                WithOpenInterest = true
                            };
                            MarketDataRequest marketDataRequest = new MarketDataRequest();
                            marketDataRequest.SubscribeTradesRequest = subscribeTradesRequest;

                            tradesRequest = marketDataRequest;
                            streamWrapperCommon.Subscriptions.Add(marketDataRequest);
                            _rateGateSubscribeCommon.WaitToProceed();
                            Task writeTradesTask = streamWrapperCommon.StreamClient.RequestStream.WriteAsync(marketDataRequest);
                            if (writeTradesTask.Wait(_streamWaitTimeout) == false)
                            {
                                ObserveTaskFault(writeTradesTask);
                                streamWrapperCommon.IsConnected = false;
                                throw new TimeoutException("Market data stream write timeout");
                            }

                            // 2 Подписка на стакан

                            marketDataRequest = new MarketDataRequest();

                            OrderBookInstrument orderBookInstrument = new OrderBookInstrument();
                            orderBookInstrument.InstrumentId = security.NameId;
                            orderBookInstrument.Depth = _orderBookDepth;
                            orderBookInstrument.OrderBookType =
                                _filterOutDealerData ? OrderBookType.Exchange : OrderBookType.All;

                            SubscribeOrderBookRequest subscribeOrderBookRequest = new SubscribeOrderBookRequest
                            { SubscriptionAction = SubscriptionAction.Subscribe, Instruments = { orderBookInstrument } };
                            marketDataRequest.SubscribeOrderBookRequest = subscribeOrderBookRequest;

                            orderBookRequest = marketDataRequest;
                            streamWrapperMarketDepth.Subscriptions.Add(marketDataRequest);
                            _rateGateSubscribeMd.WaitToProceed();
                            Task writeMdTask = streamWrapperMarketDepth.StreamClient.RequestStream.WriteAsync(marketDataRequest);
                            if (writeMdTask.Wait(_streamWaitTimeout) == false)
                            {
                                ObserveTaskFault(writeMdTask);
                                streamWrapperMarketDepth.IsConnected = false;
                                throw new TimeoutException("Market data stream write timeout");
                            }
                        }
                        _securityStreamMap.Add(security.NameId, streamWrapperMarketDepth);
                    }
                }
                else
                {
                    _pollSubscribedSecurities.Add(security);
                    return;
                }
            }
            catch (Exception ex)
            {
                // откатываем всё, что добавили в обёртки на этом вызове,
                // иначе повторная подписка задвоит ленту/стакан
                lock (_marketDataStreamLocker)
                {
                    if (lastPriceRequest != null
                        && tradesStream != null)
                    {
                        tradesStream.Subscriptions.Remove(lastPriceRequest);
                    }

                    if (tradesRequest != null
                        && tradesStream != null)
                    {
                        tradesStream.Subscriptions.Remove(tradesRequest);
                    }

                    if (orderBookRequest != null
                        && depthStream != null)
                    {
                        depthStream.Subscriptions.Remove(orderBookRequest);
                    }

                    if (_securityStreamMap.ContainsKey(security.NameId))
                    {
                        _securityStreamMap.Remove(security.NameId);
                    }
                }

                if (ServerStatus != ServerConnectStatus.Connect)
                {
                    // при дисконекте ретраить бессмысленно - движок переиздаст подписки после реконнекта
                    SendLogMessage("Subscribe aborted (server disconnected). Security: " + security.Name, LogMessageType.System);
                    return;
                }

                if (tryCount <= 3)
                {
                    // ретрай с экспоненциальной задержкой, как рекомендуют доки API: 1/10/60 сек
                    int delaySeconds = tryCount == 1 ? 1 : tryCount == 2 ? 10 : 60;

                    SendLogMessage("Subscribe error. Retry " + tryCount + " in " + delaySeconds + "s. Security: " + security.Name
                        + ". " + ex.ToString(), LogMessageType.System);
                    Thread.Sleep(delaySeconds * 1000);
                    SubscribeLoop(tryCount, security);
                    return;
                }

                SendLogMessage("Subscribe failed after retries. Security: " + security.Name
                    + ". " + ex.ToString(), LogMessageType.Error);
            }

        }

        public bool SubscribeNews()
        {
            if (ServerStatus == ServerConnectStatus.Disconnect)
            {
                return false;
            }

            _newsIsSubscribed = true;

            return true;
        }

        public event Action<News> NewsEvent;

        // Поток опроса новостей. Метод News unary, стрима у Т-Инвестиций для новостей нет.
        // Глубина истории метода ограничена 24 часами
        // https://developer.tbank.ru/invest/services/instruments/methods#news

        private void NewsPoller()
        {
            Thread.Sleep(10000);

            while (true)
            {
                try
                {
                    if (ServerStatus != ServerConnectStatus.Connect
                        || _newsIsSubscribed == false)
                    {
                        Thread.Sleep(1000);
                        continue;
                    }

                    GetNews();

                    Thread.Sleep(10000);
                }
                catch (Exception e)
                {
                    SendLogMessage(e.ToString(), LogMessageType.System);
                    Thread.Sleep(5000);
                }
            }
        }

        private void GetNews()
        {
            _rateGateNews.WaitToProceed();

            NewsResponse response = null;

            try
            {
                NewsRequest request = new NewsRequest();
                request.Limit = 100;

                response = _instrumentsClient.News(request, _gRpcMetadata);
            }
            catch (RpcException ex)
            {
                string message = GetGRPCErrorMessage(ex);
                SendLogMessage($"Error getting news. Info: {message}", LogMessageType.System);
                return;
            }
            catch (Exception ex)
            {
                SendLogMessage("Error getting news. " + ex.ToString(), LogMessageType.System);
                return;
            }

            if (response == null
                || response.Items == null
                || response.Items.Count == 0)
            {
                return;
            }

            long maxId = _lastNewsId;

            for (int i = 0; i < response.Items.Count; i++)
            {
                if (response.Items[i].Id > maxId)
                {
                    maxId = response.Items[i].Id;
                }
            }

            if (_lastNewsId == 0)
            {
                _lastNewsId = maxId;
                return;
            }

            for (int i = response.Items.Count - 1; i >= 0; i--)
            {
                NewsItem item = response.Items[i];

                if (item.Id <= _lastNewsId)
                {
                    continue;
                }

                News news = new News();

                news.TimeMessage = item.Ts != null
                    ? TimeZoneInfo.ConvertTimeFromUtc(item.Ts.ToDateTime(), _mskTimeZone) // convert to MSK
                    : TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, _mskTimeZone);

                news.Source = "TInvest " + item.Source;

                news.Value = BuildNewsText(item);

                NewsEvent?.Invoke(news);
            }

            _lastNewsId = maxId;
        }

        private string BuildNewsText(NewsItem item)
        {
            string text = item.Title ?? "";

            if (string.IsNullOrEmpty(item.Content) == false)
            {
                text += "\n" + item.Content;
            }
            else if (string.IsNullOrEmpty(item.Summary) == false)
            {
                text += "\n" + item.Summary;
            }

            string tickers = "";

            for (int i = 0; i < item.InstrumentId.Count; i++)
            {
                NewsInstrumentInfo info = item.InstrumentId[i].Instrument;

                if (info == null
                    || string.IsNullOrEmpty(info.Ticker))
                {
                    continue;
                }

                if (tickers.Contains(info.Ticker))
                {
                    continue;
                }

                if (tickers.Length > 0)
                {
                    tickers += ", ";
                }

                tickers += info.Ticker;
            }

            if (tickers.Length > 0)
            {
                text += "\nТикеры: " + tickers;
            }

            return CleanNewsText(text);
        }

        private string CleanNewsText(string input)
        {
            if (string.IsNullOrEmpty(input))
            {
                return input;
            }

            string result = Regex.Replace(input, "<.*?>", string.Empty);

            return WebUtility.HtmlDecode(result);
        }

        #endregion

        #region 8 Reading messages from data streams

        private Dictionary<string, OpenInterest> _openInterestData = new Dictionary<string, OpenInterest>(); // save open interest data to use later in trade updates

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
                    _lastMarketDataTime = DateTime.UtcNow;
                    streamWrapper.LastMessageTime = _lastMarketDataTime;
                    ProcessMarketDataResponse(marketData, streamWrapper);
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
                    // реконнект уже произошёл: исключение относится к старому клиенту.
                    // Состояние нового стрима не трогаем. Cancelled/disposed -
                    // штатное следствие нашего dispose при реконнекте, не логируем
                    if (isCancelled == false)
                    {
                        SendLogMessage($"TInvest stream {streamWrapper.Name} exception: " + ex.Message, LogMessageType.System);
                    }
                    return;
                }

                if (isCancelled && _isDisposedNow)
                {
                    // штатная остановка коннектора
                    return;
                }

                SendLogMessage($"TInvest stream {streamWrapper.Name} exception: " + ex.Message, LogMessageType.System);
                streamWrapper.IsConnected = false;
            }
        }

        private void ProcessMarketDataResponse(MarketDataResponse marketData, MarketDataStreamWrapper streamWrapper)
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

                    if (_ignoreMorningAuctionTrades)
                    {
                        DateTime tradeTimeMsk = TimeZoneInfo.ConvertTimeFromUtc(trade.Time.ToDateTime(), _mskTimeZone);
                        if (tradeTimeMsk.Hour < 7)
                        {
                            return;
                        }
                    }

                    Trade newTrade = new Trade();
                    newTrade.SecurityNameCode = security.Name;
                    newTrade.Price = GetValue(trade.Price);
                    newTrade.Volume = trade.Quantity;
                    newTrade.Time = TimeZoneInfo.ConvertTimeFromUtc(trade.Time.ToDateTime(), _mskTimeZone);
                    newTrade.Id = newTrade.Time.Ticks.ToString();
                    newTrade.Side = trade.Direction == TradeDirection.Buy ? Side.Buy : Side.Sell;

                    if (_openInterestData.TryGetValue(security.Name, out OpenInterest oi))
                    {
                        newTrade.OpenInterest = oi.OpenInterest_;
                    }

                    if (security.SecurityType == SecurityType.Bond
                        && security.NominalCurrent != 0)
                    {
                        newTrade.Price = newTrade.Price / 100 * security.NominalCurrent;
                    }

                    NewTradesEvent?.Invoke(newTrade);

                    if (security.SecurityType == SecurityType.Futures
                        && newTrade.Price != 0)
                    {
                        TinSecuritiesRisksFutures riskFutures = null;

                        if (_tSecuritiesRiskFutures.TryGetValue(security.NameId, out riskFutures) == true)
                        {
                            decimal price = newTrade.Price / security.PriceStep * security.PriceStepCost;

                            if (riskFutures.MarginBuyCoeffClient != 0)
                            {
                                security.MarginBuy = price * riskFutures.MarginBuyCoeffClient;
                            }
                            if (riskFutures.MarginSellCoeffClient != 0)
                            {
                                security.MarginSell = price * riskFutures.MarginSellCoeffClient;
                            }
                        }
                    }
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
                    depth.Time = TimeZoneInfo.ConvertTimeFromUtc(orderbook.Time.ToDateTime(), _mskTimeZone);

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

                    if (_openInterestData.TryGetValue(security.Name, out OpenInterest oi))
                    {
                        depth.OpenInterest = oi.OpenInterest_;
                    }

                    if (depth.Asks.Count > 0 || depth.Bids.Count > 0)
                    {
                        MarketDepthEvent?.Invoke(depth);
                    }

                    if (isBondNeedToNormalization)
                    {
                        security.PriceLimitHigh = GetValue(marketData.Orderbook.LimitUp) / 100 * security.NominalCurrent;
                        security.PriceLimitLow = GetValue(marketData.Orderbook.LimitDown) / 100 * security.NominalCurrent;
                    }
                    else
                    {
                        security.PriceLimitHigh = GetValue(marketData.Orderbook.LimitUp);
                        security.PriceLimitLow = GetValue(marketData.Orderbook.LimitDown);
                    }
                }
                else if (marketData.Candle != null)
                {
                    Tinkoff.InvestApi.V1.Candle tinvestCandle = marketData.Candle;
                    Security security = GetSecurityByIdFast(tinvestCandle.InstrumentUid);
                    if (security == null)
                    {
                        return;
                    }

                    Candle osCandle = new Candle();

                    if (security.SecurityType == SecurityType.Bond
                         && security.NominalCurrent != 0)
                    {
                        osCandle.Open = GetValue(tinvestCandle.Open) / 100 * security.NominalCurrent;
                        osCandle.High = GetValue(tinvestCandle.High) / 100 * security.NominalCurrent;
                        osCandle.Low = GetValue(tinvestCandle.Low) / 100 * security.NominalCurrent;
                        osCandle.Close = GetValue(tinvestCandle.Close) / 100 * security.NominalCurrent;
                    }
                    else
                    {
                        osCandle.Open = GetValue(tinvestCandle.Open);
                        osCandle.High = GetValue(tinvestCandle.High);
                        osCandle.Low = GetValue(tinvestCandle.Low);
                        osCandle.Close = GetValue(tinvestCandle.Close);
                    }

                    osCandle.Volume = tinvestCandle.Volume;
                    osCandle.TimeStart = TimeZoneInfo.ConvertTimeFromUtc(tinvestCandle.Time.ToDateTime(), _mskTimeZone);
                    osCandle.State = CandleState.Finished;


                    NewCandleEvent?.Invoke(osCandle);
                }
                else if (marketData.LastPrice != null)
                {
                    ProcessLastPrice(marketData.LastPrice);
                }
                else if (marketData.OpenInterest != null)
                {
                    Tinkoff.InvestApi.V1.OpenInterest oi = marketData.OpenInterest;
                    Security security = GetSecurityByIdFast(oi.InstrumentUid);
                    if (security != null)
                    {
                        _openInterestData[security.Name] = oi;
                    }
                }
                else if (marketData.Ping != null)
                {
                    // Already handled in ReadStream by updating LastMessageTime
                }
                else if (marketData.SubscribeTradesResponse != null ||
                         marketData.SubscribeOrderBookResponse != null ||
                         marketData.SubscribeInfoResponse != null ||
                         marketData.SubscribeLastPriceResponse != null ||
                         marketData.SubscribeCandlesResponse != null)
                {
                    if (marketData.SubscribeTradesResponse != null)
                    {
                        foreach (TradeSubscription sub in marketData.SubscribeTradesResponse.TradeSubscriptions)
                        {
                            if (sub.SubscriptionStatus != SubscriptionStatus.Success)
                            {
                                Security security = GetSecurityByIdFast(sub.InstrumentUid);
                                SendLogMessage($"Failed to subscribe to trades for {security?.Name}. Status: {sub.SubscriptionStatus}", LogMessageType.Error);

                                string uid = sub.InstrumentUid;
                                Task.Run(() => RetryFailedSubscription(uid, "trades"));
                            }
                        }
                    }
                    if (marketData.SubscribeOrderBookResponse != null)
                    {
                        foreach (OrderBookSubscription sub in marketData.SubscribeOrderBookResponse.OrderBookSubscriptions)
                        {
                            if (sub.SubscriptionStatus != SubscriptionStatus.Success)
                            {
                                Security security = GetSecurityByIdFast(sub.InstrumentUid);
                                SendLogMessage($"Failed to subscribe to order book for {security?.Name}. Status: {sub.SubscriptionStatus}", LogMessageType.Error);

                                string uid = sub.InstrumentUid;
                                Task.Run(() => RetryFailedSubscription(uid, "order book"));
                            }
                        }
                    }
                    if (marketData.SubscribeLastPriceResponse != null)
                    {
                        foreach (LastPriceSubscription sub in marketData.SubscribeLastPriceResponse.LastPriceSubscriptions)
                        {
                            if (sub.SubscriptionStatus != SubscriptionStatus.Success)
                            {
                                Security security = GetSecurityByIdFast(sub.InstrumentUid);
                                SendLogMessage($"Failed to subscribe to last price for {security?.Name}. Status: {sub.SubscriptionStatus}", LogMessageType.Error);

                                string uid = sub.InstrumentUid;
                                Task.Run(() => RetryFailedSubscription(uid, "last price"));
                            }
                        }
                    }
                    if (marketData.SubscribeCandlesResponse != null)
                    {
                        foreach (CandleSubscription sub in marketData.SubscribeCandlesResponse.CandlesSubscriptions)
                        {
                            if (sub.SubscriptionStatus != SubscriptionStatus.Success)
                            {
                                Security security = GetSecurityByIdFast(sub.InstrumentUid);
                                SendLogMessage($"Failed to subscribe to candles for {security?.Name}. Status: {sub.SubscriptionStatus}", LogMessageType.Error);

                                string uid = sub.InstrumentUid;
                                Task.Run(() => RetryFailedSubscription(uid, "candles"));
                            }
                        }
                    }

                    // реконсиляция: ответ на наш GetMySubscriptions - сверяем списки
                    if (streamWrapper != null
                        && streamWrapper.ReconcileRequestTime != DateTime.MinValue)
                    {
                        streamWrapper.ReconcileRequestTime = DateTime.MinValue;
                        ReconcileSubscriptions(streamWrapper, marketData);
                    }
                }
            }
            catch (Exception ex)
            {
                SendLogMessage($"Error processing market data response: {ex}", LogMessageType.Error);
            }
        }

        // Чудо-поток для опроса последних цен инструментов и эмуляции стакана L1.
        // Работает только если количество подписок превышает лимит gRPC-потока

        private ConcurrentDictionary<string, int> _subscribeRetryCounts = new ConcurrentDictionary<string, int>();

        // повторная подписка при отказе или потере подписки (максимум 3 попытки на бумагу)
        private void RetryFailedSubscription(string instrumentUid, string what)
        {
            try
            {
                Security security = GetSecurityByIdFast(instrumentUid);

                if (security == null)
                {
                    return;
                }

                int count = _subscribeRetryCounts.AddOrUpdate(instrumentUid, 1, (key, value) => value + 1);

                if (count > 3)
                {
                    SendLogMessage("Subscribe retry limit reached for " + security.Name + " " + what, LogMessageType.Error);
                    return;
                }

                SendLogMessage("Retrying failed subscription: " + security.Name + " " + what + " (attempt " + count + ")", LogMessageType.System);

                lock (_marketDataStreamLocker)
                {
                    if (_securityStreamMap.ContainsKey(security.NameId))
                    {
                        _securityStreamMap.Remove(security.NameId);
                    }
                }

                SubscribeLoop(0, security);
            }
            catch (Exception ex)
            {
                SendLogMessage("Error retrying subscription: " + ex.ToString(), LogMessageType.Error);
            }
        }

        // сверка наших подписок стрима с ответом на GetMySubscriptions:
        // чего сервер не подтвердил - переподписываем
        private void ReconcileSubscriptions(MarketDataStreamWrapper streamWrapper, MarketDataResponse response)
        {
            try
            {
                HashSet<string> tradesOk = new HashSet<string>();
                HashSet<string> orderBookOk = new HashSet<string>();
                HashSet<string> lastPriceOk = new HashSet<string>();

                if (response.SubscribeTradesResponse != null)
                {
                    foreach (TradeSubscription sub in response.SubscribeTradesResponse.TradeSubscriptions)
                    {
                        if (sub.SubscriptionStatus == SubscriptionStatus.Success)
                        {
                            tradesOk.Add(sub.InstrumentUid);
                        }
                    }
                }

                if (response.SubscribeOrderBookResponse != null)
                {
                    foreach (OrderBookSubscription sub in response.SubscribeOrderBookResponse.OrderBookSubscriptions)
                    {
                        if (sub.SubscriptionStatus == SubscriptionStatus.Success)
                        {
                            orderBookOk.Add(sub.InstrumentUid);
                        }
                    }
                }

                if (response.SubscribeLastPriceResponse != null)
                {
                    foreach (LastPriceSubscription sub in response.SubscribeLastPriceResponse.LastPriceSubscriptions)
                    {
                        if (sub.SubscriptionStatus == SubscriptionStatus.Success)
                        {
                            lastPriceOk.Add(sub.InstrumentUid);
                        }
                    }
                }

                List<MarketDataRequest> subs;

                lock (_marketDataStreamLocker)
                {
                    subs = new List<MarketDataRequest>(streamWrapper.Subscriptions);
                }

                for (int i = 0; i < subs.Count; i++)
                {
                    MarketDataRequest request = subs[i];

                    if (request.SubscribeTradesRequest != null
                        && response.SubscribeTradesResponse != null)
                    {
                        foreach (TradeInstrument instrument in request.SubscribeTradesRequest.Instruments)
                        {
                            if (tradesOk.Contains(instrument.InstrumentId) == false)
                            {
                                string uid = instrument.InstrumentId;
                                Task.Run(() => RetryFailedSubscription(uid, "trades (reconcile)"));
                            }
                        }
                    }
                    else if (request.SubscribeOrderBookRequest != null
                        && response.SubscribeOrderBookResponse != null)
                    {
                        foreach (OrderBookInstrument instrument in request.SubscribeOrderBookRequest.Instruments)
                        {
                            if (orderBookOk.Contains(instrument.InstrumentId) == false)
                            {
                                string uid = instrument.InstrumentId;
                                Task.Run(() => RetryFailedSubscription(uid, "order book (reconcile)"));
                            }
                        }
                    }
                    else if (request.SubscribeLastPriceRequest != null
                        && response.SubscribeLastPriceResponse != null)
                    {
                        foreach (LastPriceInstrument instrument in request.SubscribeLastPriceRequest.Instruments)
                        {
                            if (lastPriceOk.Contains(instrument.InstrumentId) == false)
                            {
                                string uid = instrument.InstrumentId;
                                Task.Run(() => RetryFailedSubscription(uid, "last price (reconcile)"));
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                SendLogMessage("Error reconciling subscriptions: " + ex.ToString(), LogMessageType.Error);
            }
        }

        private DateTime _lastSubscriptionsReconcile = DateTime.MinValue;

        // штатная реконсиляция подписок по докам: спрашиваем у каждого стрима его список,
        // ответы приходят в стрим как Subscribe*Response и сверяются с нашим списком
        private void RequestMySubscriptions()
        {
            try
            {
                if (_marketDataStreams == null)
                {
                    return;
                }

                for (int i = 0; i < _marketDataStreams.Count; i++)
                {
                    MarketDataStreamWrapper stream = _marketDataStreams[i];

                    if (stream.IsConnected == false
                        || stream.Subscriptions.Count == 0
                        || stream.StreamClient == null)
                    {
                        continue;
                    }

                    MarketDataRequest request = new MarketDataRequest();
                    request.GetMySubscriptions = new GetMySubscriptions();

                    lock (_marketDataStreamLocker)
                    {
                        stream.ReconcileRequestTime = DateTime.UtcNow;

                        Task writeTask = stream.StreamClient.RequestStream.WriteAsync(request);
                        if (writeTask.Wait(_streamWaitTimeout) == false)
                        {
                            ObserveTaskFault(writeTask);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                SendLogMessage("Error requesting subscriptions: " + ex.ToString(), LogMessageType.Error);
            }
        }

        private void LastPricesPoller()
        {
            Thread.Sleep(10000);

            while (true)
            {
                try
                {
                    if (ServerStatus == ServerConnectStatus.Disconnect ||
                        _pollSubscribedSecurities.Count == 0)
                    {
                        Thread.Sleep(1000);
                        continue;
                    }

                    Thread.Sleep(500);

                    if (_filterOutDealerData)
                    {
                        if (_pollSubscribedSecurities.Count == 0)
                        {
                            continue;
                        }
                        if (isTodayATradingDayForSecurity(_pollSubscribedSecurities[0]) == false)
                            continue;
                    }
                    //var watch = System.Diagnostics.Stopwatch.StartNew();
                    //SendLogMessage($"Polling for {_pollSubscribedSecurities.Count} securities.", LogMessageType.System);

                    UpdateLastPrices(_pollSubscribedSecurities);

                    //watch.Stop();
                    //SendLogMessage($"Polling for {_pollSubscribedSecurities.Count} securities completed in {watch.ElapsedMilliseconds} ms.", LogMessageType.System);
                }
                catch (Exception e)
                {
                    SendLogMessage(e.ToString(), LogMessageType.System);
                    Thread.Sleep(5000);
                }
            }
        }

        public void UpdateLastPrices(List<Security> securitiesToPoll)
        {
            if (securitiesToPoll.Count == 0)
            {
                return;
            }

            List<string> instrumentIds = new List<string>();

            // Количество инструментов в списке не может быть больше 3000.
            // https://russianinvestments.github.io/investAPI/errors/
            // Поэтому разбиваем обновления на дозы по 3000 штуки
            for (int i = 0; i < securitiesToPoll.Count; i++)
            {
                instrumentIds.Add(securitiesToPoll[i].NameId);

                if (instrumentIds.Count == 3000)
                {
                    GetLastPrices(instrumentIds);

                    instrumentIds.Clear();
                }
            }

            // пустой список запрашивать нельзя: API вернёт цены по ВСЕМ инструментам
            if (instrumentIds.Count > 0)
            {
                GetLastPrices(instrumentIds);
            }
        }

        private void GetLastPrices(List<string> instrumentIds)
        {
            _rateGateMarketData.WaitToProceed();
            GetLastPricesResponse priceResp = null;
            try
            {
                priceResp = _marketDataServiceClient.GetLastPrices(new GetLastPricesRequest
                {
                    InstrumentId = { instrumentIds },
                    LastPriceType = _filterOutDealerData ? LastPriceType.LastPriceExchange : LastPriceType.LastPriceUnspecified
                }, _gRpcMetadata);
            }
            catch (RpcException ex)
            {
                string message = GetGRPCErrorMessage(ex);
                SendLogMessage($"Error getting last prices. Status: {ex.StatusCode}, Message: {message}, Details: {ex.ToString()}", LogMessageType.System);
            }
            catch (Exception ex)
            {
                SendLogMessage(ex.ToString(), LogMessageType.System);
            }

            if (priceResp == null)
                return;

            for (int i = 0; i < priceResp.LastPrices.Count; i++)
            {
                ProcessLastPrice(priceResp.LastPrices[i]);
            }
        }

        private void ProcessLastPrice(LastPrice price)
        {
            Security mySec = GetSecurityByIdFast(price.InstrumentUid);

            if (price.Price == null)
                return;

            if (mySec == null)
            {
                return;
            }

            Trade newTrade = new Trade();

            newTrade.SecurityNameCode = mySec.Name;
            newTrade.Time = TimeZoneInfo.ConvertTimeFromUtc(price.Time.ToDateTime(), _mskTimeZone);// convert to MSK
            newTrade.Price = GetValue(price.Price);
            newTrade.Side = Side.Buy;
            newTrade.Volume = 1;
            newTrade.Id = newTrade.Time.Ticks.ToString();

            if (_ignoreMorningAuctionTrades && newTrade.Time.Hour < 7)
            {
                return;
            }

            if (_openInterestData.ContainsKey(mySec.Name))
            {
                newTrade.OpenInterest = _openInterestData[mySec.Name].OpenInterest_;
            }

            if (mySec.SecurityType == SecurityType.Bond
               && mySec.NominalCurrent != 0)
            {
                newTrade.Price = newTrade.Price / 100 * mySec.NominalCurrent;
            }

            NewTradesEvent?.Invoke(newTrade);

            CreateFakeMdByTrade(newTrade);
        }

        private void CreateFakeMdByTrade(Trade trade)
        {
            List<MarketDepthLevel> bids = new List<MarketDepthLevel>();

            MarketDepthLevel newBid = new MarketDepthLevel();
            newBid.Bid = Convert.ToDouble(trade.Volume);
            newBid.Price = Convert.ToDouble(trade.Price);
            bids.Add(newBid);

            MarketDepth depth = new MarketDepth();

            depth.SecurityNameCode = trade.SecurityNameCode;
            depth.Time = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, _mskTimeZone);// convert to MSK
            depth.Bids = bids;

            List<MarketDepthLevel> asks = new List<MarketDepthLevel>();

            MarketDepthLevel newAsk = new MarketDepthLevel();
            newAsk.Ask = Convert.ToDouble(trade.Volume);
            newAsk.Price = Convert.ToDouble(trade.Price);
            asks.Add(newAsk);

            depth.Asks = asks;

            if (depth.Asks == null ||
                depth.Asks.Count == 0 ||
                depth.Bids == null ||
                depth.Bids.Count == 0)
            {
                return;
            }

            if (MarketDepthEvent != null)
            {
                MarketDepthEvent(depth);
            }
        }

        private DateTime _lastMdTime = DateTime.MinValue;

        public event Action<Trade> NewTradesEvent;

        public event Action<MarketDepth> MarketDepthEvent;

        public event Action<Candle> NewCandleEvent;

        private async void PortfolioMessageReader()
        {
            Thread.Sleep(1000);

            while (true)
            {
                try
                {
                    if (ServerStatus == ServerConnectStatus.Disconnect)
                    {
                        Thread.Sleep(1);
                        continue;
                    }

                    if (_portfolioDataStream == null)
                    {
                        Thread.Sleep(1);
                        continue;
                    }

                    if (await _portfolioDataStream.ResponseStream.MoveNext() == false)
                    {
                        Thread.Sleep(1);
                        continue;
                    }

                    if (_portfolioDataStream == null)
                    {
                        Thread.Sleep(1);
                        continue;
                    }

                    PortfolioStreamResponse portfolioResponse = _portfolioDataStream.ResponseStream.Current;
                    if (portfolioResponse == null)
                    {
                        Thread.Sleep(1);
                        continue;
                    }

                    _lastPortfolioDataTime = DateTime.UtcNow;

                    if (portfolioResponse.Ping != null)
                    {
                        Thread.Sleep(1);
                        continue;
                    }

                    if (portfolioResponse.Portfolio != null)
                    {
                        GetPortfolios();
                    }
                }
                catch (Exception exception)
                {
                    if (_isDisposedNow == true)
                    {
                        continue;
                    }

                    if (_isReconnectByPingPortfoliosData == true)
                    {
                        continue;
                    }

                    string message = exception.ToString();

                    if (message.Contains("limit") == false)
                    {
                        // пробуем восстановить поток без перезапуска коннектора

                        if (TryReconnectPortfolioStream() == true)
                        {
                            _portfolioReconnectFails = 0;
                            SendLogMessage(OsLocalization.Market.Label295 + "\nPortfolio", LogMessageType.System);
                            Thread.Sleep(1000);
                            continue;
                        }

                        _portfolioReconnectFails++;

                        if (_portfolioReconnectFails >= 3)
                        {
                            // мягкий реконнект не выходит - проверяем, жив ли сам канал
                            _portfolioReconnectFails = 0;

                            if (IsChannelAlive() == true)
                            {
                                // канал жив, мёртв только стрим - ждём и пробуем снова
                                SendLogMessage("Stream reconnect deferred. Portfolio stream", LogMessageType.System);
                                Thread.Sleep(10000);
                                continue;
                            }
                        }
                        else
                        {
                            Thread.Sleep(10000);
                            continue;
                        }
                    }

                    // need to reconnect everything
                    if (ServerStatus != ServerConnectStatus.Disconnect)
                    {
                        SendLogMessage(OsLocalization.Market.Label294 + "\nPortfolio\n" + message, LogMessageType.System);
                        SendMessageOnReconnectInErrorLog();
                        ServerStatus = ServerConnectStatus.Disconnect;
                        DisconnectEvent();
                    }
                    Thread.Sleep(5000);
                }
            }
        }

        private int _portfolioReconnectFails = 0;

        private int _positionsReconnectFails = 0;

        private int _ordersReconnectFails = 0;

        // проверка живости канала дешёвым unary-вызовом:
        // стрим мёртв != канал мёртв
        private bool IsChannelAlive()
        {
            try
            {
                _usersClient.GetAccounts(new GetAccountsRequest(), _gRpcMetadata);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private async void PositionsMessageReader()
        {
            Thread.Sleep(1000);

            while (true)
            {
                try
                {
                    if (ServerStatus == ServerConnectStatus.Disconnect)
                    {
                        Thread.Sleep(1);
                        continue;
                    }

                    if (_positionsDataStream == null)
                    {
                        Thread.Sleep(1);
                        continue;
                    }

                    if (await _positionsDataStream.ResponseStream.MoveNext() == false)
                    {
                        Thread.Sleep(1);
                        continue;
                    }

                    if (_positionsDataStream == null)
                    {
                        Thread.Sleep(1);
                        continue;
                    }

                    PositionsStreamResponse positionsResponse = _positionsDataStream.ResponseStream.Current;
                    if (positionsResponse == null)
                    {
                        Thread.Sleep(1);
                        continue;
                    }

                    _lastPositionsDataTime = DateTime.UtcNow;

                    if (positionsResponse.Ping != null)
                    {
                        Thread.Sleep(1);
                        continue;
                    }

                    if (positionsResponse.Position != null)
                    {
                        lock (_portfolioDataLocker)
                        {

                            PositionData posData = positionsResponse.Position;
                            Portfolio portf = _myPortfolios.Find((p) => p.Number == posData.AccountId);

                            if (portf == null)
                            {
                                continue;
                            }

                            for (int i = 0; i < posData.Securities.Count; i++)
                            {
                                PositionsSecurities pos = posData.Securities[i];

                                Security posSecurity = GetSecurityByUidOrApi(pos.InstrumentUid);

                                if (posSecurity == null)
                                {
                                    continue;
                                }

                                PositionOnBoard newPos = new PositionOnBoard();

                                newPos.PortfolioName = portf.Number;
                                newPos.ValueCurrent = pos.Balance / posSecurity.Lot;
                                newPos.ValueBlocked = pos.Blocked / posSecurity.Lot;

                                if (newPos.ValueBlocked != 0)
                                {
                                    newPos.ValueCurrent += newPos.ValueBlocked;
                                }

                                newPos.SecurityNameCode = posSecurity.Name;
                                newPos.SecurityNameClass = posSecurity.NameClass;

                                portf.SetNewPosition(newPos);
                            }

                            for (int i = 0; i < posData.Futures.Count; i++)
                            {
                                PositionsFutures pos = posData.Futures[i];

                                Security posSecurity = GetSecurityByUidOrApi(pos.InstrumentUid);

                                if (posSecurity == null)
                                {
                                    continue;
                                }

                                PositionOnBoard newPos = new PositionOnBoard();

                                newPos.PortfolioName = portf.Number;
                                newPos.ValueCurrent = pos.Balance / posSecurity.Lot;
                                newPos.ValueBlocked = pos.Blocked / posSecurity.Lot;
                                newPos.SecurityNameCode = posSecurity.Name;
                                newPos.SecurityNameClass = posSecurity.NameClass;

                                portf.SetNewPosition(newPos);

                            }

                            for (int i = 0; i < posData.Options.Count; i++)
                            {
                                PositionsOptions pos = posData.Options[i];

                                Security posSecurity = GetSecurityByUidOrApi(pos.InstrumentUid);

                                if (posSecurity == null)
                                {
                                    continue;
                                }

                                PositionOnBoard newPos = new PositionOnBoard();

                                newPos.PortfolioName = portf.Number;
                                newPos.ValueCurrent = pos.Balance / posSecurity.Lot;
                                newPos.ValueBlocked = pos.Blocked / posSecurity.Lot;
                                newPos.SecurityNameCode = posSecurity.Name;
                                newPos.SecurityNameClass = posSecurity.NameClass;

                                portf.SetNewPosition(newPos);
                            }

                            if (PortfolioEvent != null)
                            {
                                PortfolioEvent(CreatePortfoliosSnapshot());
                            }
                        }
                    }
                }
                catch (Exception exception)
                {
                    if (_isDisposedNow == true)
                    {
                        continue;
                    }

                    if (_isReconnectByPingPortfoliosData == true)
                    {
                        continue;
                    }

                    string message = exception.ToString();

                    if (message.Contains("limit") == false)
                    {
                        // пробуем восстановить поток без перезапуска коннектора

                        if (TryReconnectPositionsStream() == true)
                        {
                            _positionsReconnectFails = 0;
                            SendLogMessage(OsLocalization.Market.Label295 + "\nPositions", LogMessageType.System);
                            Thread.Sleep(1000);
                            continue;
                        }

                        _positionsReconnectFails++;

                        if (_positionsReconnectFails >= 3)
                        {
                            // мягкий реконнект не выходит - проверяем, жив ли сам канал
                            _positionsReconnectFails = 0;

                            if (IsChannelAlive() == true)
                            {
                                // канал жив, мёртв только стрим - ждём и пробуем снова
                                SendLogMessage("Stream reconnect deferred. Positions stream", LogMessageType.System);
                                Thread.Sleep(10000);
                                continue;
                            }
                        }
                        else
                        {
                            Thread.Sleep(10000);
                            continue;
                        }
                    }

                    // need to reconnect everything
                    if (ServerStatus != ServerConnectStatus.Disconnect)
                    {
                        SendLogMessage(OsLocalization.Market.Label294 + "\nPositions\n" + message, LogMessageType.System);
                        SendMessageOnReconnectInErrorLog();
                        ServerStatus = ServerConnectStatus.Disconnect;
                        DisconnectEvent();
                    }
                    Thread.Sleep(5000);
                }
            }
        }

        private async void OrderStateMessageReader()
        {
            Thread.Sleep(1000);

            while (true)
            {
                try
                {
                    if (ServerStatus == ServerConnectStatus.Disconnect)
                    {
                        Thread.Sleep(1);
                        continue;
                    }

                    if (_myOrderStateDataStream == null)
                    {
                        Thread.Sleep(1);
                        continue;
                    }

                    if (await _myOrderStateDataStream.ResponseStream.MoveNext() == false)
                    {
                        Thread.Sleep(1);
                        continue;
                    }

                    if (_myOrderStateDataStream == null)
                    {
                        Thread.Sleep(1);
                        continue;
                    }

                    OrderStateStreamResponse orderStateResponse = _myOrderStateDataStream.ResponseStream.Current;
                    if (orderStateResponse == null)
                    {
                        Thread.Sleep(1);
                        continue;
                    }

                    _lastMyOrderStateDataTime = DateTime.UtcNow;

                    if (orderStateResponse.Ping != null)
                    {
                        Thread.Sleep(1);
                        continue;
                    }

                    if (orderStateResponse.OrderState != null)
                    {
                        Security security = GetSecurityByIdFast(orderStateResponse.OrderState.InstrumentUid);
                        OrderStateStreamResponse.Types.OrderState state = orderStateResponse.OrderState;

                        if (security == null)
                        {
                            Thread.Sleep(1);
                            continue;
                        }

                        if (string.IsNullOrEmpty(state.TradeOrderId) == false
                            && IsOurStopOrder(state.TradeOrderId))
                        {   // дочерняя биржевая заявка нашего стоп-ордера
                            ProcessStopOrderChildOrder(state, security);
                            continue;
                        }

                        Order order = new Order();

                        lock (_orderNumbersLocker)
                        {
                            if (!_orderNumbers.ContainsKey(state.OrderRequestId)) // значит сделка была вручную и это не наш ордер
                            {
                                continue;
                            }

                            order.NumberUser = _orderNumbers[state.OrderRequestId];
                        }

                        if (state.ExecutionReportStatus == OrderExecutionReportStatus.ExecutionReportStatusNew)
                        {
                            if (state.OrderId != null
                                && state.OrderId.Split('-').Length > 3)
                            { // отсекаем внутренний статус о том что ордер дошёл до торговой системы Т.
                              // С не настоящим id
                                continue;
                            }
                        }

                        order.NumberMarket = state.OrderId;
                        order.SecurityNameCode = security.Name;
                        order.PortfolioNumber = state.AccountId;
                        order.Side = state.Direction == OrderDirection.Buy ? Side.Buy : Side.Sell;
                        order.TypeOrder = state.OrderType == OrderType.Limit || state.OrderType == OrderType.Unspecified
                            ? OrderPriceType.Limit
                            : OrderPriceType.Market;

                        order.Volume = state.LotsRequested;
                        order.VolumeExecute = state.LotsExecuted;

                        if (order.TypeOrder == OrderPriceType.Limit)
                        {
                            if (_orderPrices.TryGetValue(state.OrderRequestId, out decimal originalPrice))
                            {
                                order.Price = originalPrice;
                            }
                            else
                            {
                                // цена в стриме приходит в пунктах - берём как есть
                                order.Price = GetValue(state.OrderPrice);
                                SendLogMessage($"Could not find original price for order request ID {state.OrderRequestId}. Using price from broker.", LogMessageType.System);
                            }
                        }
                        else
                        {
                            order.Price = 0;
                        }
                        order.TimeCallBack = state.CreatedAt?.ToDateTime() != null ? TimeZoneInfo.ConvertTimeFromUtc(state.CreatedAt.ToDateTime(), _mskTimeZone) : TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, _mskTimeZone);// convert to MSK
                        order.SecurityClassCode = security.NameClass;

                        if (state.ExecutionReportStatus == OrderExecutionReportStatus.ExecutionReportStatusUnspecified)
                        {
                            order.State = OrderStateType.None;
                        }
                        else if (state.ExecutionReportStatus == OrderExecutionReportStatus.ExecutionReportStatusFill)
                        {
                            order.State = OrderStateType.Done;
                        }
                        else if (state.ExecutionReportStatus ==
                                 OrderExecutionReportStatus.ExecutionReportStatusRejected)
                        {
                            order.State = OrderStateType.Fail;
                        }
                        else if (state.ExecutionReportStatus ==
                                 OrderExecutionReportStatus.ExecutionReportStatusCancelled)
                        {
                            order.State = OrderStateType.Cancel;
                        }
                        else if (state.ExecutionReportStatus == OrderExecutionReportStatus.ExecutionReportStatusNew)
                        {
                            order.State = OrderStateType.Active;

                            if (order.TypeOrder == OrderPriceType.Limit && order.Price == 0)
                                continue; // ignore such status
                        }
                        else if (state.ExecutionReportStatus ==
                                 OrderExecutionReportStatus.ExecutionReportStatusPartiallyfill)
                        {
                            order.State = OrderStateType.Partial;
                            if (state.CompletionTime != null)
                            {
                                order.State = OrderStateType.Cancel; // partially filled orders never go to cancelled state 
                            }
                        }

                        /* SendLogMessage("New order state. Security: " + order.SecurityNameCode
                             + "\n NumberUser: " + order.NumberUser
                             + "\n State: " + order.State
                             + "\n NumberMarket: " + order.NumberMarket, LogMessageType.System);*/

                        if (IsCancelOrderInClearing(order, state))
                        {   // это у нас отзыв ордера в клиринг вечерний. Фьючерсная площадка
                            // после этого ордера должны будут восстановиться
                            /* SendLogMessage("Ордер пропущен в клиринг. Security: " + order.SecurityNameCode
                               + "\n NumberUser: " + order.NumberUser
                               + "\n NumberMarket: " + order.NumberMarket, LogMessageType.System);*/
                            continue;
                        }

                        if (order.State == OrderStateType.Done ||
                            order.State == OrderStateType.Fail ||
                            order.State == OrderStateType.Cancel)
                        {
                            _orderPrices.TryRemove(state.OrderRequestId, out _);
                        }

                        if (orderStateResponse.OrderState.Trades != null)
                        {
                            for (int i = 0; i < orderStateResponse.OrderState.Trades.Count; i++)
                            {
                                OrderTrade orderTrade = orderStateResponse.OrderState.Trades[i];

                                MyTrade trade = new MyTrade();
                                trade.SecurityNameCode = security.Name;

                                trade.Price = GetValue(orderTrade.Price);

                                if (security.SecurityType == SecurityType.Bond
                                 && security.NominalCurrent != 0)
                                {
                                    trade.Price = trade.Price * (security.NominalCurrent / 100);
                                }

                                trade.Volume = orderTrade.Quantity / GetLotSizeForTrade(state, security);
                                trade.NumberOrderParent = order.NumberMarket;
                                trade.NumberTrade = orderTrade.TradeId;
                                trade.Time = TimeZoneInfo.ConvertTimeFromUtc(orderTrade.DateTime.ToDateTime(), _mskTimeZone); // convert to MSK

                                if (trade.Time == new DateTime(1970, 1, 1, 3, 0, 0))
                                {
                                    DateTime tTime = orderTrade.DateTime.ToDateTime();
                                    SendLogMessage($"TInvest sent trade with time == {tTime} for trade Id {orderTrade.TradeId}", LogMessageType.System);

                                    trade.Time = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, _mskTimeZone); // fix trade time
                                }

                                trade.Side = order.Side;

                                LogTradeInFullLog(trade);

                                MyTradeEvent?.Invoke(trade);
                            }
                        }

                        LogOrderInFullLog(order);

                        MyOrderEvent?.Invoke(order);
                    }

                    if (orderStateResponse.StopOrderState != null)
                    {
                        ProcessStopOrderStateFromStream(orderStateResponse.StopOrderState);
                    }
                }
                catch (Exception exception)
                {
                    if (_isDisposedNow == true)
                    {
                        continue;
                    }

                    if (_isReconnectByOrdersData == true)
                    {
                        continue;
                    }

                    string message = exception.ToString();

                    if (message.Contains("limit") == false)
                    {
                        // пробуем восстановить поток без перезапуска коннектора

                        if (TryReconnectOrdersStream() == true)
                        {
                            _ordersReconnectFails = 0;
                            SendLogMessage(OsLocalization.Market.Label295 + "\nOrders", LogMessageType.System);

                            if (ForceCheckOrdersAfterReconnectEvent != null)
                            {
                                ForceCheckOrdersAfterReconnectEvent();
                            }

                            Thread.Sleep(1000);
                            continue;
                        }

                        _ordersReconnectFails++;

                        if (_ordersReconnectFails >= 3)
                        {
                            // мягкий реконнект не выходит - проверяем, жив ли сам канал
                            _ordersReconnectFails = 0;

                            if (IsChannelAlive() == true)
                            {
                                // канал жив, мёртв только стрим - ждём и пробуем снова
                                SendLogMessage("Stream reconnect deferred. Orders stream", LogMessageType.System);
                                Thread.Sleep(10000);
                                continue;
                            }
                        }
                        else
                        {
                            Thread.Sleep(10000);
                            continue;
                        }
                    }

                    // need to reconnect everything
                    if (ServerStatus != ServerConnectStatus.Disconnect)
                    {
                        SendLogMessage(OsLocalization.Market.Label294 + "\nOrders\n" + message, LogMessageType.System);
                        SendMessageOnReconnectInErrorLog();
                        ServerStatus = ServerConnectStatus.Disconnect;
                        DisconnectEvent();
                    }
                    Thread.Sleep(5000);
                }
            }
        }

        private void LogOrderInFullLog(Order order, string source = "")
        {
            if (_fullLog)
            {
                SendLogMessage($"Пришел ордер: Source {source}, Security {order.SecurityNameCode}, NumberMarket {order.NumberMarket}, NumberUser {order.NumberUser}, Side {order.Side}, Price {order.Price} " +
                    $"Volume {order.Volume}, VolumeExecute {order.VolumeExecute}, Time {order.TimeCallBack}, Status {order.State}", LogMessageType.System);
            }
        }

        private void LogTradeInFullLog(MyTrade trade, string source = "")
        {
            if (_fullLog)
            {
                SendLogMessage($"Пришел трейд: Source {source}, Security {trade.SecurityNameCode}, NumberOrder {trade.NumberOrderParent}, Side {trade.Side}, Price {trade.Price} " +
                    $"Volume {trade.Volume}, Time {trade.Time}", LogMessageType.System);
            }
        }

        private DateTime _lastClearingCancelLog;

        // скрываем от движка только отмену биржей в окне вечернего клиринга (фьючерсы MOEX).
        // Клиентские отмены (ByClient) и любые отмены вне окна проходят всегда
        private bool IsCancelOrderInClearing(Order order, OrderStateStreamResponse.Types.OrderState state)
        {
            if (order.State != OrderStateType.Cancel)
            {
                return false;
            }

            // причина известна и это не биржа - обычная отмена
            if (state.StatusInfo != OrderStateStreamResponse.Types.StatusCauseInfo.CauseCancelledByExchange)
            {
                return false;
            }

            DateTime time = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, _mskTimeZone);

            if (time.DayOfWeek == DayOfWeek.Sunday
                || time.DayOfWeek == DayOfWeek.Saturday)
            {
                return false;
            }

            bool inClearingWindow =
                (time.Hour == 18 && time.Minute >= 50)
                || (time.Hour == 19 && time.Minute < 4);

            if (inClearingWindow == false)
            {
                return false;
            }

            if (_lastClearingCancelLog.AddMinutes(1) < DateTime.Now)
            {
                _lastClearingCancelLog = DateTime.Now;
                SendLogMessage("Order cancelled by exchange in evening clearing window, hidden from engine: "
                    + order.SecurityNameCode + " " + order.NumberMarket, LogMessageType.System);
            }

            return true;
        }

        private DateTime _lastErrorMessageOnReconnectTime;

        private void SendMessageOnReconnectInErrorLog()
        {
            if (_lastErrorMessageOnReconnectTime.AddSeconds(5) > DateTime.Now)
            {
                return;
            }

            _lastErrorMessageOnReconnectTime = DateTime.Now;

            SendLogMessage(OsLocalization.Market.Label296, LogMessageType.Error);
        }

        private void ProcessStopOrderStateFromStream(OrderStateStreamResponse.Types.StopOrderState state)
        {
            try
            {
                Security security = GetSecurityByIdFast(state.InstrumentUid);

                if (security == null)
                {
                    return;
                }

                Order order = new Order();

                order.NumberMarket = state.StopOrderId;
                order.SecurityNameCode = security.Name;
                order.SecurityClassCode = security.NameClass;
                order.PortfolioNumber = state.AccountId;
                order.Side = state.Direction == OrderDirection.Buy ? Side.Buy : Side.Sell;
                order.TypeOrder = state.OrderType == OrderType.Limit
                    ? OrderPriceType.StopLimit
                    : OrderPriceType.StopMarket;

                order.TimeCallBack = state.CreatedAt != null
                    ? TimeZoneInfo.ConvertTimeFromUtc(state.CreatedAt.ToDateTime(), _mskTimeZone)
                    : TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, _mskTimeZone);

                order.State = GetStateFromStopOrderStatus(state.Status);

                decimal price = GetValue(state.Price);
                decimal stopPrice = GetValue(state.StopPrice);

                if (security.SecurityType == SecurityType.Bond
                    && security.NominalCurrent != 0)
                {
                    price = price * (security.NominalCurrent / 100);
                    stopPrice = stopPrice * (security.NominalCurrent / 100);
                }

                order.Price = price;
                order.StopPrice = stopPrice;

                bool isOurOrder = false;

                lock (_stopOrdersLocker)
                {
                    Order activeOrder = null;

                    for (int i = 0; i < _activeStopOrders.Count; i++)
                    {
                        if (_activeStopOrders[i].NumberMarket == state.StopOrderId)
                        {
                            activeOrder = _activeStopOrders[i];
                            break;
                        }
                    }

                    if (activeOrder != null)
                    {
                        isOurOrder = true;
                        order.NumberUser = activeOrder.NumberUser;
                        order.Volume = activeOrder.Volume;

                        if (order.Price == 0)
                        {   // у стоп-маркет заявок цена в стриме пустая. Берём цену пользователя
                            order.Price = activeOrder.Price;
                        }

                        if (order.StopPrice == 0)
                        {
                            order.StopPrice = activeOrder.StopPrice;
                        }

                        if (order.State != OrderStateType.Active)
                        {
                            _activeStopOrders.Remove(activeOrder);
                        }
                    }
                }

                if (isOurOrder == false)
                {
                    lock (_orderNumbersLocker)
                    {
                        if (_stopOrderNumbers.ContainsKey(state.StopOrderId))
                        {
                            isOurOrder = true;
                            order.NumberUser = _stopOrderNumbers[state.StopOrderId];
                        }
                    }
                }

                if (isOurOrder == false)
                {
                    // стоп-ордер не наш, игнорируем
                    return;
                }

                if (state.Status == StopOrderStatusOption.StopOrderStatusExecuted)
                {
                    // исполнение стопа требует REST-дозапросов (ExchangeOrderId, ActivationDateTime, сделки) -
                    // уводим в отдельный поток, чтобы не блокировать чтение стрима ордеров
                    _stopOrderExecutions.Enqueue(new StopOrderExecution(order, state.AccountId, state.StopOrderId));
                    return;
                }

                LogOrderInFullLog(order, "OrderStateMessageReader");

                MyOrderEvent?.Invoke(order);
            }
            catch (Exception ex)
            {
                SendLogMessage("Error processing stop order state from stream. " + ex.ToString(), LogMessageType.Error);
            }
        }

        private ConcurrentQueue<StopOrderExecution> _stopOrderExecutions = new ConcurrentQueue<StopOrderExecution>();

        private void StopOrderExecutionsReader()
        {
            Thread.Sleep(1000);

            while (true)
            {
                try
                {
                    if (_stopOrderExecutions.IsEmpty)
                    {
                        Thread.Sleep(10);
                        continue;
                    }

                    StopOrderExecution execution = null;
                    _stopOrderExecutions.TryDequeue(out execution);

                    if (execution == null)
                    {
                        continue;
                    }

                    Order order = execution.Order;

                    StopOrder stopFromServer = GetStopOrderFromServer(execution.AccountId, execution.StopOrderId);

                    if (stopFromServer != null)
                    {
                        if (stopFromServer.HasExchangeOrderId)
                        {
                            order.ChildOrderNumberMarket = stopFromServer.ExchangeOrderId;
                        }

                        if (stopFromServer.ActivationDateTime != null)
                        {
                            order.TimeCancel = TimeZoneInfo.ConvertTimeFromUtc(stopFromServer.ActivationDateTime.ToDateTime(), _mskTimeZone);
                        }

                        ProcessStopOrderTrades(order, stopFromServer, "OrderStateMessageReader");
                    }

                    LogOrderInFullLog(order, "OrderStateMessageReader");

                    MyOrderEvent?.Invoke(order);
                }
                catch (Exception ex)
                {
                    SendLogMessage("Error processing executed stop order. " + ex.ToString(), LogMessageType.Error);
                    Thread.Sleep(5000);
                }
            }
        }

        private StopOrder GetStopOrderFromServer(string accountId, string stopOrderId)
        {   // стоп-ордер с сервера по его id (нужен ExchangeOrderId и ActivationDateTime после активации)
            try
            {
                List<StopOrder> stopsFromServer = GetStopOrdersFromServer(accountId, StopOrderStatusOption.StopOrderStatusAll);

                if (stopsFromServer == null)
                {
                    return null;
                }

                for (int i = 0; i < stopsFromServer.Count; i++)
                {
                    if (stopsFromServer[i].StopOrderId == stopOrderId)
                    {
                        return stopsFromServer[i];
                    }
                }
            }
            catch (Exception ex)
            {
                SendLogMessage("Error getting stop order from server. " + ex.ToString(), LogMessageType.System);
            }

            return null;
        }

        private bool IsOurStopOrder(string stopOrderId)
        {
            lock (_stopOrdersLocker)
            {
                for (int i = 0; i < _activeStopOrders.Count; i++)
                {
                    if (_activeStopOrders[i].NumberMarket == stopOrderId)
                    {
                        return true;
                    }
                }
            }

            lock (_orderNumbersLocker)
            {
                return _stopOrderNumbers.ContainsKey(stopOrderId);
            }
        }

        private OrderStateType GetStateFromExecutionReportStatus(OrderExecutionReportStatus status, bool hasCompletionTime)
        {
            if (status == OrderExecutionReportStatus.ExecutionReportStatusUnspecified)
            {
                return OrderStateType.None;
            }

            if (status == OrderExecutionReportStatus.ExecutionReportStatusFill)
            {
                return OrderStateType.Done;
            }

            if (status == OrderExecutionReportStatus.ExecutionReportStatusRejected)
            {
                return OrderStateType.Fail;
            }

            if (status == OrderExecutionReportStatus.ExecutionReportStatusCancelled)
            {
                return OrderStateType.Cancel;
            }

            if (status == OrderExecutionReportStatus.ExecutionReportStatusNew)
            {
                return OrderStateType.Active;
            }

            if (status == OrderExecutionReportStatus.ExecutionReportStatusPartiallyfill)
            {   // partially filled orders never go to cancelled state
                return hasCompletionTime ? OrderStateType.Cancel : OrderStateType.Partial;
            }

            return OrderStateType.None;
        }

        private void ProcessStopOrderChildOrder(OrderStateStreamResponse.Types.OrderState state, Security security)
        {   // дочерняя биржевая заявка активированного стоп-ордера
            try
            {
                if (state.ExecutionReportStatus == OrderExecutionReportStatus.ExecutionReportStatusNew)
                {
                    if (state.OrderId != null
                        && state.OrderId.Split('-').Length > 3)
                    { // отсекаем внутренний статус о том что ордер дошёл до торговой системы Т.
                      // С не настоящим id
                        return;
                    }
                }

                Order order = new Order();

                order.NumberUser = NumberGen.GetNumberOrder(StartProgram.IsOsTrader);
                order.NumberMarket = state.OrderId;
                order.ParentOrderNumberMarket = state.TradeOrderId; // id материнского стоп-ордера
                order.SecurityNameCode = security.Name;
                order.SecurityClassCode = security.NameClass;
                order.PortfolioNumber = state.AccountId;
                order.Side = state.Direction == OrderDirection.Buy ? Side.Buy : Side.Sell;
                order.TypeOrder = state.OrderType == OrderType.Limit || state.OrderType == OrderType.Unspecified
                    ? OrderPriceType.Limit
                    : OrderPriceType.Market;

                order.Volume = state.LotsRequested;
                order.VolumeExecute = state.LotsExecuted;

                if (order.TypeOrder == OrderPriceType.Limit)
                {
                    // цена в стриме приходит в пунктах - берём как есть
                    order.Price = GetValue(state.OrderPrice);
                }
                else
                {
                    order.Price = 0;
                }

                order.TimeCallBack = state.CreatedAt?.ToDateTime() != null
                    ? TimeZoneInfo.ConvertTimeFromUtc(state.CreatedAt.ToDateTime(), _mskTimeZone)
                    : TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, _mskTimeZone);// convert to MSK

                order.TimeCreate = order.TimeCallBack;
                order.OrderTypeTime = OrderTypeTime.GTC;

                order.State = GetStateFromExecutionReportStatus(state.ExecutionReportStatus, state.CompletionTime != null);

                if (order.State == OrderStateType.Active
                    && order.TypeOrder == OrderPriceType.Limit
                    && order.Price == 0)
                {
                    return; // ignore such status
                }

                if (IsCancelOrderInClearing(order, state))
                {   // это у нас отзыв ордера в клиринг вечерний. Фьючерсная площадка
                    return;
                }

                LogOrderInFullLog(order, "StopChildOrder");

                MyOrderEvent?.Invoke(order);

                // трейды дочерней заявки
                ProcessStopOrderChildOrderTrades(state, security);
            }
            catch (Exception ex)
            {
                SendLogMessage("Error processing stop order child order. " + ex.ToString(), LogMessageType.Error);
            }
        }

        private void ProcessStopOrderChildOrderTrades(OrderStateStreamResponse.Types.OrderState state, Security security)
        {
            try
            {
                if (state.Trades == null)
                {
                    return;
                }

                Side side = state.Direction == OrderDirection.Buy ? Side.Buy : Side.Sell;

                for (int i = 0; i < state.Trades.Count; i++)
                {
                    OrderTrade orderTrade = state.Trades[i];

                    MyTrade trade = new MyTrade();
                    trade.SecurityNameCode = security.Name;

                    trade.Price = GetValue(orderTrade.Price);

                    if (security.SecurityType == SecurityType.Bond
                     && security.NominalCurrent != 0)
                    {
                        trade.Price = trade.Price * (security.NominalCurrent / 100);
                    }

                    trade.Volume = orderTrade.Quantity / GetLotSizeForTrade(state, security);
                    trade.NumberOrderParent = state.OrderId; // привязываем к дочерней биржевой заявке, а не к стоп-ордеру
                    trade.NumberTrade = orderTrade.TradeId;
                    trade.Time = TimeZoneInfo.ConvertTimeFromUtc(orderTrade.DateTime.ToDateTime(), _mskTimeZone); // convert to MSK

                    if (trade.Time == new DateTime(1970, 1, 1, 3, 0, 0))
                    {
                        trade.Time = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, _mskTimeZone); // fix trade time
                    }

                    trade.Side = side;

                    LogTradeInFullLog(trade, "OrderStateMessageReader");

                    MyTradeEvent?.Invoke(trade);
                }
            }
            catch (Exception ex)
            {
                SendLogMessage("Error processing stop order child trades. " + ex.ToString(), LogMessageType.Error);
            }
        }

        private void RemoveActiveStopOrderUnsafe(string numberMarket)
        {
            for (int i = 0; i < _activeStopOrders.Count; i++)
            {
                if (_activeStopOrders[i].NumberMarket == numberMarket)
                {
                    _activeStopOrders.RemoveAt(i);
                    return;
                }
            }
        }

        private void ProcessStopOrderTrades(Order order, StopOrder stopFromServer, string source)
        {
            try
            {
                Security security = _securities.Find((sec) => sec.Name == order.SecurityNameCode);

                if (stopFromServer.HasExchangeOrderId == false
                    || security == null)
                {
                    return;
                }

                lock (_rageGateOrdersLocker)
                {
                    _rateGateOrders.WaitToProceed();
                }

                GetOrderStateRequest stateRequest = new GetOrderStateRequest();
                stateRequest.OrderId = stopFromServer.ExchangeOrderId;
                stateRequest.AccountId = order.PortfolioNumber;

                OrderState state = _ordersClient.GetOrderState(stateRequest, _gRpcMetadata);

                if (state == null)
                {
                    return;
                }

                // сначала эмитим дочерний ордер, чтобы ядро привязало его к позиции материнского стоп-ордера

                Order childOrder = new Order();

                childOrder.NumberUser = NumberGen.GetNumberOrder(StartProgram.IsOsTrader);
                childOrder.NumberMarket = stopFromServer.ExchangeOrderId;
                childOrder.ParentOrderNumberMarket = order.NumberMarket; // id материнского стоп-ордера
                childOrder.SecurityNameCode = order.SecurityNameCode;
                childOrder.SecurityClassCode = order.SecurityClassCode;
                childOrder.PortfolioNumber = order.PortfolioNumber;
                childOrder.Side = order.Side;
                childOrder.TypeOrder = state.OrderType == OrderType.Limit
                    ? OrderPriceType.Limit
                    : OrderPriceType.Market;
                childOrder.Volume = state.LotsRequested;
                childOrder.VolumeExecute = state.LotsExecuted;
                childOrder.Price = childOrder.TypeOrder == OrderPriceType.Limit
                    ? GetValue(state.InitialSecurityPrice) / security.PriceStepCost * security.PriceStep
                    : 0;
                childOrder.TimeCallBack = state.OrderDate != null
                    ? TimeZoneInfo.ConvertTimeFromUtc(state.OrderDate.ToDateTime(), _mskTimeZone)
                    : TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, _mskTimeZone);// convert to MSK

                childOrder.TimeCreate = childOrder.TimeCallBack;
                childOrder.OrderTypeTime = OrderTypeTime.GTC;

                childOrder.State = GetStateFromExecutionReportStatus(state.ExecutionReportStatus, false);

                LogOrderInFullLog(childOrder, source);

                MyOrderEvent?.Invoke(childOrder);

                if (state.Stages == null
                    || state.Stages.Count == 0)
                {
                    return;
                }

                for (int i = 0; i < state.Stages.Count; i++)
                {
                    OrderStage stage = state.Stages[i];

                    MyTrade trade = new MyTrade();

                    trade.SecurityNameCode = order.SecurityNameCode;
                    trade.Price = GetValue(stage.Price) / security.PriceStepCost * security.PriceStep;

                    if (security.SecurityType == SecurityType.Bond
                       && security.NominalCurrent != 0)
                    {
                        trade.Price = trade.Price * (security.NominalCurrent / 100);
                    }

                    // OrderStage.quantity по контракту API уже в лотах - не делим
                    trade.Volume = stage.Quantity;
                    trade.NumberOrderParent = stopFromServer.ExchangeOrderId; // привязываем к дочерней биржевой заявке
                    trade.NumberTrade = stage.TradeId;
                    trade.Time = TimeZoneInfo.ConvertTimeFromUtc(stage.ExecutionTime.ToDateTime(), _mskTimeZone);// convert to MSK
                    trade.Side = order.Side;

                    LogTradeInFullLog(trade, source);

                    MyTradeEvent?.Invoke(trade);
                }
            }
            catch (Exception ex)
            {
                SendLogMessage("Error getting stop order trades. " + ex.ToString(), LogMessageType.Error);
            }
        }

        public event Action<Order> MyOrderEvent;

        public event Action<MyTrade> MyTradeEvent;

        public event Action<OptionMarketDataForConnector> AdditionalMarketDataEvent { add { } remove { } }

        #endregion

        #region 9 Trade

        private RateGate _rateGateOrders = new RateGate(98, TimeSpan.FromMinutes(1)); // https://russianinvestments.github.io/investAPI/limits/
        private string _rageGateOrdersLocker = "_rageGateOrdersLocker";

        // Метод postOrder: лимит 300 запр/мин. Держим 290 с запасом
        private RateGate _rateGatePostOrders = new RateGate(290, TimeSpan.FromMinutes(1));
        private string _rageGatePostOrdersLocker = "_rageGatePostOrdersLocker";

        // Сервис стоп-ордеров: лимит 50 запр/мин суммарно по всем методам и счетам. Держим 45 с запасом
        private RateGate _rateGateStopOrders = new RateGate(45, TimeSpan.FromMinutes(1));

        // единая точка отказа по заявке: статус Fail + событие, чтобы робот не ждал её вечно.
        // Текст ошибки прикладываем к самой заявке (Comment) - роботам не нужно
        // искать причину в общем логе коннектора
        private void CreateOrderFail(Order order, string brokerMessage = null)
        {
            if (string.IsNullOrEmpty(brokerMessage) == false)
            {
                order.Comment = brokerMessage;
            }

            order.State = OrderStateType.Fail;

            LogOrderInFullLog(order);

            if (MyOrderEvent != null)
            {
                MyOrderEvent(order);
            }
        }

        public void SendOrder(Order order)
        {
            lock (_rageGatePostOrdersLocker)
            {
                _rateGatePostOrders.WaitToProceed();
            }

            if (order.TypeOrder == OrderPriceType.StopLimit
                || order.TypeOrder == OrderPriceType.StopMarket)
            {
                SendStopOrder(order);
                return;
            }

            try
            {
                Security security = _securities.Where(s => _securityStreamMap.ContainsKey(s.NameId)).FirstOrDefault((sec) =>
                    sec.Name == order.SecurityNameCode);

                if (security == null)
                {
                    security = _pollSubscribedSecurities.Find((sec) => sec.Name == order.SecurityNameCode);
                }

                if (security == null)
                {
                    security = _securities.Find((sec) =>
                    sec.Name == order.SecurityNameCode);
                }

                if (security == null)
                {
                    SendLogMessage(OsLocalization.Market.Label291 + "\nSecurity not found: " + order.SecurityNameCode, LogMessageType.Error);
                    CreateOrderFail(order, "Security not found: " + order.SecurityNameCode);
                    return;
                }

                if (order.Volume <= 0)
                {
                    SendLogMessage(OsLocalization.Market.Label291 + "\nVolume is zero: " + order.SecurityNameCode, LogMessageType.Error);
                    CreateOrderFail(order, "Volume is zero: " + order.SecurityNameCode);
                    return;
                }

                if (order.TypeOrder == OrderPriceType.Limit
                    && order.Price == 0)
                {
                    SendLogMessage(OsLocalization.Market.Label291 + "\nPrice is zero: " + order.SecurityNameCode, LogMessageType.Error);
                    CreateOrderFail(order, "Price is zero: " + order.SecurityNameCode);
                    return;
                }

                decimal orderPrice = order.Price;

                if (security.SecurityType == SecurityType.Bond
                    && security.NominalCurrent != 0)
                {
                    orderPrice = order.Price / (security.NominalCurrent / 100);
                }

                PostOrderRequest request = new PostOrderRequest();
                request.Direction = order.Side == Side.Buy ? OrderDirection.Buy : OrderDirection.Sell;
                request.OrderType = order.TypeOrder == OrderPriceType.Limit ? OrderType.Limit : OrderType.Market; // еще есть BestPrice
                request.Quantity = Convert.ToInt64(order.Volume);
                request.Price = ConvertToQuotation(orderPrice);
                request.ConfirmMarginTrade = true;

                // цены облигаций (в % от номинала) и фьючерсов (в пунктах) шлём как пункты
                if (security.SecurityType == SecurityType.Bond
                    || security.SecurityType == SecurityType.Futures)
                {
                    request.PriceType = PriceType.Point;
                }

                request.InstrumentId = security.NameId;
                request.AccountId = order.PortfolioNumber;
                request.TimeInForce = TimeInForceType.TimeInForceDay; // по-умолчанию сегодняшний день

                if (order.TypeOrder == OrderPriceType.Limit)
                {
                    if (order.OrderTypeTime == OrderTypeTime.Day)
                    {
                        request.TimeInForce = TimeInForceType.TimeInForceDay;
                    }
                    else if (order.OrderTypeTime == OrderTypeTime.Specified)
                    {
                        request.TimeInForce = TimeInForceType.TimeInForceUnspecified;
                    }
                }

                // генерируем новый номер ордера и добавляем его в словарь
                Guid newUid = Guid.NewGuid();
                string orderId = newUid.ToString();

                lock (_orderNumbersLocker)
                {
                    _orderNumbers.Add(orderId, order.NumberUser);
                }

                _orderPrices[orderId] = order.Price;

                request.OrderId = orderId;

                PostOrderResponse response = null;

                try
                {
                    response = PostOrderPrivateLoop(request, 0, order);
                }
                catch (RpcException ex)
                {
                    string message = GetGRPCErrorMessage(ex);

                    if (message.Contains("Not enough assets"))
                    {
                        CheckCrazyNotEnoughAssetsOrderSpam();
                        message = OsLocalization.Market.Label301;
                    }
                    else if (message.Contains("The price is too high"))
                    {
                        message = OsLocalization.Market.Label302;
                    }
                    else if (message.Contains("The price is outside the limits for"))
                    {
                        message = OsLocalization.Market.Label304;
                    }
                    else if (message.Contains("Pol`zovatel` ne najden"))
                    {
                        message = OsLocalization.Market.Label319;
                    }

                    SendLogMessage(OsLocalization.Market.Label291 +
                            "\n" + message +
                            "\n" + order.SecurityNameCode
                            + ", " + OsLocalization.Market.Message21 + order.Volume
                            + ", " + OsLocalization.Market.Label303 + " " + order.Price + " " + order.Side
                            , LogMessageType.Error);

                    CreateOrderFail(order, message);

                    return;
                }
                catch (Exception exception)
                {
                    SendLogMessage(OsLocalization.Market.Label291 + "\n" + exception.Message, LogMessageType.Error);

                    CreateOrderFail(order, exception.Message);

                    return;
                }

                if (response.ExecutionReportStatus == OrderExecutionReportStatus.ExecutionReportStatusRejected)
                {
                    CreateOrderFail(order, response.Message);
                }
                else
                {
                    order.State = OrderStateType.Active;
                    order.NumberMarket = response.OrderId;

                    if (_lastMyOrderStateDataTime.AddSeconds(5) < DateTime.UtcNow)
                    {   // Сбрасываем счётчики жизни потока принимающего статусы ордеров
                        // если он отсох, надо чтобы через 3 секунды уже переподключался.
                        _lastMyOrderStateDataTime = DateTime.UtcNow.AddSeconds(-177);
                        _lastTryReconnectOrdersStream = DateTime.Now.AddMinutes(-1);
                    }

                    LogOrderInFullLog(order);

                    if (MyOrderEvent != null)
                    {
                        MyOrderEvent(order);
                    }
                }
            }
            catch (Exception exception)
            {
                SendLogMessage(OsLocalization.Market.Label291 + "\n" + exception, LogMessageType.Error);

                CreateOrderFail(order, exception.Message);
            }
        }

        private void CheckCrazyNotEnoughAssetsOrderSpam()
        {
            // некоторые пользователи выставляют внутри дня тысячи заявок без обеспечения
            // отключая при этом все реакции в роботах, нагружая сервера Т-Банк
            // решение: вырубаем у них коннектор, когда за час больше 100 ошибок "Not enough assets"

            if (_hourNotEnoughAssetsOrders != DateTime.Now.Hour)
            {
                _hourNotEnoughAssetsOrders = DateTime.Now.Hour;
                _badOrdersCount = 0;
            }

            _badOrdersCount++;

            if (_badOrdersCount > 100)
            {
                if (ServerStatus == ServerConnectStatus.Connect)
                {
                    SendLogMessage(
                        " Сервер был отключен. Т.к. кол-во необеспеченных ордеров внутри часа больше 100\n "
                        + "Прекратите спамить биржу, это мешает людям торговать\n "
                        + "Пожалуйста посчитайте обеспечение и баланс. И в соответствии с этим настройте роботов. ", LogMessageType.Error);

                    ServerStatus = ServerConnectStatus.Disconnect;
                    DisconnectEvent();
                }
            }
        }

        private int _hourNotEnoughAssetsOrders;
        private int _badOrdersCount;

        private PostOrderResponse PostOrderPrivateLoop(PostOrderRequest request, int attemptNumber, Order order)
        {
            // Метод для обработки ошибок в ядре брокера, не позволяющих принять заявку с первого раза
            // В таком случае приходит ошибка: "Internal network error"
            // Рекомендация поддержки: Выслать тут же ещё раз, с тем же номером ордера. Сделали

            attemptNumber++;

            if (attemptNumber > 2)
            {
                throw new Exception("Internal network error. Ошибки на стороне Т-Апи. Две попытки выставить ордер не привели к успеху.");
            }

            PostOrderResponse response = null;

            Metadata metaData = _gRpcMetadata;

            try
            {
                response = _ordersClient.PostOrder(request, metaData);
            }
            catch (RpcException ex)
            {
                string message = GetGRPCErrorMessage(ex);

                if (message.Contains("Internal network error"))
                {
                    OrderStateType orderStateType = GetOrderStatus(order);

                    if (orderStateType == OrderStateType.None)
                    {
                        lock (_rageGatePostOrdersLocker)
                        {
                            _rateGatePostOrders.WaitToProceed();
                        }

                        return PostOrderPrivateLoop(request, attemptNumber, order);
                    }
                    else
                    { // ордер всё таки выставлен, но отчёт о нём не пришёл!
                        throw new Exception("Internal network error. Ошибки на стороне Т-Апи. Ордер выставлен, но его номер в торговом ядре не известен. Нужно синхронизировать позиции");
                    }
                }

                throw;
            }

            return response;
        }

        // возврат маппинга на место после неудачной перестановки заявки
        private void RestoreOrderMapping(string oldRequestId, int numberUser)
        {
            if (oldRequestId == null)
            {
                return;
            }

            lock (_orderNumbersLocker)
            {
                if (_orderNumbers.ContainsKey(oldRequestId) == false)
                {
                    _orderNumbers.Add(oldRequestId, numberUser);
                }
            }
        }

        // при быстрой серии перестановок брокер может не отменить предыдущую заявку:
        // внутренний cancel бьёт по ещё не зарегистрированной на бирже заявке и теряется.
        // Проверяем старый номер и добиваем его вручную. Событие Cancel по нему
        // придёт со старым requestId, которого уже нет в мапе - стрим его проглотит
        private void VerifyReplacedOrderIsClosed(string accountId, string oldNumberMarket)
        {
            try
            {
                if (string.IsNullOrEmpty(oldNumberMarket))
                {
                    return;
                }

                lock (_rageGateOrdersLocker)
                {
                    _rateGateOrders.WaitToProceed();
                }

                GetOrderStateRequest stateRequest = new GetOrderStateRequest();
                stateRequest.OrderId = oldNumberMarket;
                stateRequest.AccountId = accountId;

                OrderState oldState = _ordersClient.GetOrderState(stateRequest, _gRpcMetadata);

                if (oldState == null)
                {
                    return;
                }

                if (oldState.ExecutionReportStatus == OrderExecutionReportStatus.ExecutionReportStatusNew
                    || oldState.ExecutionReportStatus == OrderExecutionReportStatus.ExecutionReportStatusPartiallyfill)
                {
                    SendLogMessage("Replaced order is still active on broker, cancelling: " + oldNumberMarket, LogMessageType.System);

                    lock (_rageGateOrdersLocker)
                    {
                        _rateGateOrders.WaitToProceed();
                    }

                    CancelOrderRequest cancelRequest = new CancelOrderRequest();
                    cancelRequest.OrderId = oldNumberMarket;
                    cancelRequest.AccountId = accountId;

                    _ordersClient.CancelOrder(cancelRequest, _gRpcMetadata);
                }
            }
            catch (Exception ex)
            {
                SendLogMessage("Error verifying replaced order. " + ex.ToString(), LogMessageType.System);
            }
        }

        // сериализация перестановок: два быстрых вызова по одной заявке
        // не должны читать один и тот же старый биржевой номер,
        // иначе один ReplaceOrder падает, а лишнее звено цепочки остаётся живым у брокера
        private object _changeOrderPriceLocker = new object();

        public void ChangeOrderPrice(Order order, decimal newPrice)
        {
            lock (_changeOrderPriceLocker)
            {
                ChangeOrderPriceInternal(order, newPrice);
            }
        }

        private void ChangeOrderPriceInternal(Order order, decimal newPrice)
        {
            try
            {
                lock (_rageGateOrdersLocker)
                {
                    _rateGateOrders.WaitToProceed();
                }

                if (order.TypeOrder == OrderPriceType.Market)
                {
                    SendLogMessage("Can`t change price to market order", LogMessageType.System);
                    return;
                }

                if (order.State != OrderStateType.Active
                    && order.State != OrderStateType.Partial)
                {
                    SendLogMessage("Can`t change order price because it`s not in Active state", LogMessageType.System);
                    return;
                }

                long quantityLeft = Convert.ToInt64(order.Volume - order.VolumeExecute);

                if (quantityLeft <= 0)
                {
                    SendLogMessage("Can`t change order price because nothing left to execute", LogMessageType.System);
                    return;
                }

                Security security = _securities.Where(s => _securityStreamMap.ContainsKey(s.NameId)).FirstOrDefault((sec) =>
                 sec.Name == order.SecurityNameCode);

                if (security == null)
                {
                    security = _pollSubscribedSecurities.Find((sec) => sec.Name == order.SecurityNameCode);
                }

                if (security == null)
                {
                    security = _securities.Find((sec) =>
                    sec.Name == order.SecurityNameCode);
                }

                if (security == null)
                {
                    SendLogMessage("ChangeOrderPrice. Security not found: " + order.SecurityNameCode, LogMessageType.Error);
                    return;
                }

                // ReplaceOrder у брокера неатомарен: сначала отменяет старую заявку,
                // потом выставляет новую. Цену за пределами лимитов отсекаем локально,
                // не трогая заявку на бирже
                if (security.PriceLimitHigh > 0
                    && newPrice > security.PriceLimitHigh)
                {
                    SendLogMessage("ChangeOrderPrice rejected locally. Price above limit: " + newPrice + " > " + security.PriceLimitHigh, LogMessageType.System);
                    return;
                }

                if (security.PriceLimitLow > 0
                    && newPrice < security.PriceLimitLow)
                {
                    SendLogMessage("ChangeOrderPrice rejected locally. Price below limit: " + newPrice + " < " + security.PriceLimitLow, LogMessageType.System);
                    return;
                }

                decimal priceToSend = newPrice;

                if (security.SecurityType == SecurityType.Bond
                    && security.NominalCurrent != 0)
                {
                    priceToSend = priceToSend / (security.NominalCurrent / 100);
                }

                ReplaceOrderRequest request = new ReplaceOrderRequest();
                request.AccountId = order.PortfolioNumber;
                request.OrderId = order.NumberMarket;
                request.ConfirmMarginTrade = true;
                request.Quantity = quantityLeft;
                request.Price = ConvertToQuotation(priceToSend);

                // цены облигаций (в % от номинала) и фьючерсов (в пунктах) шлём как пункты
                if (security.SecurityType == SecurityType.Bond
                    || security.SecurityType == SecurityType.Futures)
                {
                    request.PriceType = PriceType.Point;
                }

                string newRequestId = Guid.NewGuid().ToString();
                request.IdempotencyKey = newRequestId;

                // старый маппинг снимаем ДО вызова: при перестановке брокер отменит старую заявку,
                // и её Cancel из стрима должен быть отброшен как «не наш», иначе движок
                // пометит ордер отменённым посреди перестановки. При неудаче - вернём обратно
                string oldRequestId = null;

                lock (_orderNumbersLocker)
                {
                    foreach (KeyValuePair<string, int> kvp in _orderNumbers)
                    {
                        if (kvp.Value == order.NumberUser)
                        {
                            oldRequestId = kvp.Key;
                            break;
                        }
                    }

                    if (oldRequestId != null)
                    {
                        _orderNumbers.Remove(oldRequestId);
                    }
                }

                PostOrderResponse response = null;

                try
                {
                    response = _ordersClient.ReplaceOrder(request, _gRpcMetadata);
                }
                catch (RpcException ex)
                {
                    string message = GetGRPCErrorMessage(ex);
                    SendLogMessage($"Error replacing order. Info: {message}", LogMessageType.System);

                    RestoreOrderMapping(oldRequestId, order.NumberUser);

                    // брокер мог отменить старую заявку и не выставить новую - сверяем факт
                    GetOrderStatusWithTrades(order, false, "ChangeOrderPrice");

                    return;
                }
                catch (Exception exception)
                {
                    SendLogMessage("Error on order Execution \n" + exception.Message, LogMessageType.System);

                    RestoreOrderMapping(oldRequestId, order.NumberUser);

                    GetOrderStatusWithTrades(order, false, "ChangeOrderPrice");

                    return;
                }

                if (response.ExecutionReportStatus == OrderExecutionReportStatus.ExecutionReportStatusRejected)
                {
                    SendLogMessage("Replace order rejected by broker. Order: " + order.NumberUser, LogMessageType.System);

                    RestoreOrderMapping(oldRequestId, order.NumberUser);

                    GetOrderStatusWithTrades(order, false, "ChangeOrderPrice");

                    return;
                }

                lock (_orderNumbersLocker)
                {
                    _orderNumbers[newRequestId] = order.NumberUser;
                    _orderPrices[newRequestId] = newPrice; // цена робота: пункты для фьючерсов, абсолют для облигаций
                }

                order.State = OrderStateType.Active;

                string oldNumberMarket = order.NumberMarket;

                order.NumberMarket = response.OrderId;
                order.Price = newPrice;
                order.Volume = quantityLeft;
                order.VolumeExecute = 0;
                order.TimeCallBack = TimeZoneInfo.ConvertTimeFromUtc(response.ResponseMetadata.ServerTime.ToDateTime(), _mskTimeZone);// convert to MSK

                LogOrderInFullLog(order);

                if (MyOrderEvent != null)
                {
                    MyOrderEvent(order);
                }

                // при быстрой серии перестановок брокер может не отменить предыдущую заявку -
                // проверяем старый номер и добиваем его вручную
                VerifyReplacedOrderIsClosed(order.PortfolioNumber, oldNumberMarket);
            }
            catch (Exception error)
            {
                SendLogMessage(error.ToString(), LogMessageType.System);
            }
        }

        List<string> _cancelOrderNums = new List<string>();

        private string _cancelOrdersLocker = "_cancelOrdersLocker";

        public bool CancelOrder(Order order)
        {
            try
            {
                if (string.IsNullOrEmpty(order.NumberMarket))
                {
                    // без биржевого номера отзывать нечего - брокеру не зовём
                    SendLogMessage("Can't cancel order without market number. " + order.SecurityNameCode
                        + " " + order.NumberUser, LogMessageType.System);
                    return false;
                }

                lock (_cancelOrdersLocker)
                {
                    int countTryRevokeOrder = 0;

                    for (int i = 0; i < _cancelOrderNums.Count; i++)
                    {
                        if (_cancelOrderNums[i].Equals(order.NumberMarket))
                        {
                            countTryRevokeOrder++;
                        }
                    }

                    if (countTryRevokeOrder >= 2)
                    {
                        SendLogMessage(OsLocalization.Market.Label292 + " " + order.SecurityNameCode,
                            LogMessageType.Error);
                        return false;
                    }

                    _cancelOrderNums.Add(order.NumberMarket);

                    while (_cancelOrderNums.Count > 100)
                    {
                        _cancelOrderNums.RemoveAt(0);
                    }
                }

                lock (_rageGateOrdersLocker)
                {
                    _rateGateOrders.WaitToProceed();
                }

                if (order.TypeOrder == OrderPriceType.StopLimit
                    || order.TypeOrder == OrderPriceType.StopMarket)
                {
                    return CancelStopOrder(order);
                }

                CancelOrderRequest request = new CancelOrderRequest();
                request.AccountId = order.PortfolioNumber;
                request.OrderId = order.NumberMarket;

                CancelOrderResponse response = null;

                Metadata metaData = _gRpcMetadata;

                string cancelErrorMessage = null;

                try
                {
                    response = _ordersClient.CancelOrder(request, metaData);
                }
                catch (RpcException ex)
                {
                    // не логируем сразу: сначала уточним фактический статус заявки,
                    // чтобы отличить штатную гонку отмены с исполнением от реального отказа
                    cancelErrorMessage = GetGRPCErrorMessage(ex);
                }
                catch (Exception exception)
                {
                    cancelErrorMessage = exception.Message + "  " + order.SecurityClassCode;
                }

                if (response != null)
                {
                    if (_lastMyOrderStateDataTime.AddSeconds(5) < DateTime.UtcNow)
                    {   // Сбрасываем счётчики жизни потока принимающего статусы ордеров
                        // если он отсох, надо чтобы через 3 секунды уже переподключался.
                        _lastMyOrderStateDataTime = DateTime.UtcNow.AddSeconds(-177);
                        _lastTryReconnectOrdersStream = DateTime.Now.AddMinutes(-1);
                    }

                    return true;
                }

                OrderStateType state = GetOrderStatus(order);

                if (cancelErrorMessage != null)
                {
                    if (state == OrderStateType.Done
                        || state == OrderStateType.Cancel
                        || state == OrderStateType.Partial)
                    {
                        // заявка уже исполнена или отменена до нашего запроса - это не авария
                        SendLogMessage("Cancel was not needed, order already " + state + ": "
                            + order.SecurityNameCode + " " + order.NumberMarket
                            + ". Broker message: " + cancelErrorMessage, LogMessageType.System);

                        return true;
                    }

                    // заявка жива или статус не установлен - реальный отказ отмены
                    SendLogMessage(OsLocalization.Market.Label293 + "\n" + cancelErrorMessage, LogMessageType.Error);

                    return false;
                }

                if (state == OrderStateType.None)
                {
                    return false;
                }

                return true;
            }
            catch (Exception exception)
            {
                SendLogMessage(OsLocalization.Market.Label293 + "\n" + exception.ToString(), LogMessageType.System);
            }
            return false;
        }

        public void CancelAllOrders()
        {
            List<Order> orders = GetAllOrdersFromExchange(true);

            for (int i = 0; i < orders.Count; i++)
            {
                Order order = orders[i];

                if (order.State == OrderStateType.Active)
                {
                    CancelOrder(order);
                }
            }

            List<Order> stopOrders = GetAllActiveStopOrders();

            for (int i = 0; stopOrders != null && i < stopOrders.Count; i++)
            {
                if (stopOrders[i].State == OrderStateType.Active)
                {
                    CancelOrder(stopOrders[i]);
                }
            }
        }

        public void CancelAllOrdersToSecurity(Security security)
        {
            List<Order> orders = GetAllOrdersFromExchange(true);

            for (int i = 0; i < orders.Count; i++)
            {
                Order order = orders[i];

                if (order.State == OrderStateType.Active
                    && order.SecurityNameCode == security.Name)
                {
                    CancelOrder(order);
                }
            }

            List<Order> stopOrders = GetAllActiveStopOrders();

            for (int i = 0; stopOrders != null && i < stopOrders.Count; i++)
            {
                Order order = stopOrders[i];

                if (order.State == OrderStateType.Active
                    && order.SecurityNameCode == security.Name)
                {
                    CancelOrder(order);
                }
            }
        }

        public void GetAllActivOrders()
        {
            List<Order> orders = GetAllOrdersFromExchange(true);

            for (int i = 0; orders != null && i < orders.Count; i++)
            {
                if (orders[i] == null)
                {
                    continue;
                }

                if (orders[i].State != OrderStateType.Active
                    && orders[i].State != OrderStateType.Partial
                    && orders[i].State != OrderStateType.Pending)
                {
                    continue;
                }

                orders[i].TimeCreate = orders[i].TimeCallBack;

                LogOrderInFullLog(orders[i]);

                if (MyOrderEvent != null)
                {
                    MyOrderEvent(orders[i]);
                }
            }

            List<Order> stopOrders = GetAllActiveStopOrders();

            for (int i = 0; stopOrders != null && i < stopOrders.Count; i++)
            {
                stopOrders[i].TimeCreate = stopOrders[i].TimeCallBack;

                LogOrderInFullLog(stopOrders[i]);

                if (MyOrderEvent != null)
                {
                    MyOrderEvent(stopOrders[i]);
                }
            }
        }

        public OrderStateType GetOrderStatusWithTrades(Order order, bool processTrades, string source = "")
        {
            lock (_rageGateOrdersLocker)
            {
                _rateGateOrders.WaitToProceed();
            }

            try
            {
                // запрашиваем состояние ордера
                GetOrderStateRequest getOrderStateRequest = new GetOrderStateRequest();
                getOrderStateRequest.OrderId = order.NumberMarket;
                getOrderStateRequest.AccountId = order.PortfolioNumber;

                if (order.TimeCreate != DateTime.MinValue)
                {
                    getOrderStateRequest.OrderDate = TimeZoneInfo.ConvertTimeToUtc(order.TimeCreate, _mskTimeZone).ToTimestamp();
                }

                OrderState state = null;
                try
                {
                    state = _ordersClient.GetOrderState(getOrderStateRequest, _gRpcMetadata);
                }
                catch (RpcException ex)
                {
                    string message = GetGRPCErrorMessage(ex);

                    if (message.Contains("Order not found"))
                    {
                        // брокер однозначно: такой заявки нет - финализируем её в движке
                        // как отменённую, иначе хаб будет опрашивать её вечно
                        SendLogMessage("Order not found on broker, marking as cancelled: " + order.SecurityNameCode
                            + " " + order.NumberMarket, LogMessageType.System);

                        order.State = OrderStateType.Cancel;
                        order.TimeCancel = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, _mskTimeZone);

                        if (MyOrderEvent != null)
                        {
                            MyOrderEvent(order);
                        }

                        return OrderStateType.Cancel;
                    }

                    SendLogMessage($"Error getting order state. Info: {message}", LogMessageType.System);

                    Thread.Sleep(1);
                    return OrderStateType.None;
                }
                catch (Exception ex)
                {
                    SendLogMessage("Error getting order state " + order.SecurityNameCode + " exception: " + ex.ToString(), LogMessageType.System);

                    Thread.Sleep(1);
                    return OrderStateType.None;
                }
                Order newOrder = new Order();

                Security security = _securities.FirstOrDefault(s => s.Name == order.SecurityNameCode);
                if (security == null)
                {
                    SendLogMessage($"Error getting security for {order.SecurityNameCode} in GetOrderStatusWithTrades", LogMessageType.System);
                    return OrderStateType.None;
                }

                lock (_orderNumbersLocker)
                {
                    if (!_orderNumbers.ContainsKey(state.OrderRequestId))
                    {
                        order.NumberUser = order.NumberUser != 0 ? order.NumberUser : NumberGen.GetNumberOrder(StartProgram.IsOsTrader);
                        _orderNumbers.Add(state.OrderRequestId, order.NumberUser);
                    }
                    newOrder.NumberUser = _orderNumbers[state.OrderRequestId];
                }

                newOrder.NumberMarket = state.OrderId;
                newOrder.SecurityNameCode = order.SecurityNameCode;
                newOrder.PortfolioNumber = order.PortfolioNumber;
                newOrder.Side = state.Direction == OrderDirection.Buy ? Side.Buy : Side.Sell;
                newOrder.TypeOrder = state.OrderType == OrderType.Limit
                    ? OrderPriceType.Limit
                    : OrderPriceType.Market;

                newOrder.Volume = state.LotsRequested;
                newOrder.VolumeExecute = state.LotsExecuted;
                newOrder.Price = order.TypeOrder == OrderPriceType.Limit ? GetValue(state.InitialSecurityPrice) / security.PriceStepCost * security.PriceStep : 0;
                newOrder.TimeCallBack = TimeZoneInfo.ConvertTimeFromUtc(state.OrderDate.ToDateTime(), _mskTimeZone);// convert to MSK
                newOrder.SecurityClassCode = order.SecurityClassCode;

                if (state.ExecutionReportStatus == OrderExecutionReportStatus.ExecutionReportStatusUnspecified)
                {
                    newOrder.State = OrderStateType.None;
                }
                else if (state.ExecutionReportStatus == OrderExecutionReportStatus.ExecutionReportStatusFill)
                {
                    newOrder.State = OrderStateType.Done;
                }
                else if (state.ExecutionReportStatus ==
                         OrderExecutionReportStatus.ExecutionReportStatusRejected)
                {
                    newOrder.State = OrderStateType.Fail;
                }
                else if (state.ExecutionReportStatus ==
                         OrderExecutionReportStatus.ExecutionReportStatusCancelled)
                {
                    newOrder.State = OrderStateType.Cancel;
                }
                else if (state.ExecutionReportStatus == OrderExecutionReportStatus.ExecutionReportStatusNew)
                {
                    newOrder.State = OrderStateType.Active;
                }
                else if (state.ExecutionReportStatus ==
                         OrderExecutionReportStatus.ExecutionReportStatusPartiallyfill)
                {
                    newOrder.State = OrderStateType.Partial;
                }

                if (MyOrderEvent != null)
                {
                    LogOrderInFullLog(newOrder, source);

                    MyOrderEvent(newOrder);
                }

                if (processTrades && (newOrder.State == OrderStateType.Done || newOrder.State == OrderStateType.Partial))
                {
                    // add all trades for this order
                    for (int i = 0; i < state.Stages.Count; i++)
                    {
                        OrderStage stage = state.Stages[i];

                        MyTrade trade = new MyTrade();

                        trade.SecurityNameCode = order.SecurityNameCode;
                        trade.Price = GetValue(stage.Price) / security.PriceStepCost * security.PriceStep;

                        if (security.SecurityType == SecurityType.Bond
                           && security.NominalCurrent != 0)
                        {
                            trade.Price = trade.Price * (security.NominalCurrent / 100);
                        }

                        trade.Volume = stage.Quantity;
                        trade.NumberOrderParent = state.OrderId;
                        trade.NumberTrade = stage.TradeId;
                        trade.Time = TimeZoneInfo.ConvertTimeFromUtc(stage.ExecutionTime.ToDateTime(), _mskTimeZone);// convert to MSK
                        trade.Side = state.Direction == OrderDirection.Buy
                            ? Side.Buy
                            : Side.Sell;

                        LogTradeInFullLog(trade, source);

                        MyTradeEvent?.Invoke(trade);
                    }
                }

                return newOrder.State;
            }
            catch (RpcException ex)
            {
                string message = GetGRPCErrorMessage(ex);
                SendLogMessage($"Error getting order state. Info: {message}", LogMessageType.System);
            }
            catch (Exception exception)
            {
                SendLogMessage("Get order state request error. " + exception.ToString(), LogMessageType.System);
            }

            return OrderStateType.None;
        }

        public OrderStateType GetOrderStatus(Order order)
        {
            if (order.TypeOrder == OrderPriceType.StopLimit
                || order.TypeOrder == OrderPriceType.StopMarket)
            {
                return GetStopOrderStatus(order);
            }

            return GetOrderStatusWithTrades(order, true);
        }

        private List<Order> GetAllOrdersFromExchange(bool onlyActive)
        {
            List<Order> orders = new List<Order>();

            for (int i = 0; i < _myPortfolios.Count; i++)
            {
                List<Order> newOrders = GetAllOrdersFromExchangeByPortfolio(_myPortfolios[i].Number, onlyActive, out _);
                if (newOrders != null && newOrders.Count > 0)
                {
                    orders.AddRange(newOrders);
                }
            }

            return orders;
        }

        // История заявок — скользящее окно в часах: период «от полуночи» растёт в течение дня и биржа его отбивает.
        // История заявок: две фазы - свежие 30 минут (всегда полные), потом час перед ними.
        // Лимиты брокера (фактура live): span ~1ч проходит, шире - «maximum request period exceeded»,
        // ответ молча усекается до 100 заявок; без from биржа отвечает «Missing parameter: 'from'».
        // запрос заявок за окно [fromUtc, toUtc]. При усечении/отказе сужаем окно от хвоста:
        // свежая часть важнее
        private GetOrdersResponse RequestOrdersWindow(string accountId, bool historyFilters, DateTime fromUtc, DateTime toUtc, out bool wasCapped)
        {
            wasCapped = false;

            DateTime from = fromUtc;
            DateTime to = toUtc;

            for (int attempt = 0; attempt < 6; attempt++)
            {
                lock (_rageGateOrdersLocker)
                {
                    _rateGateOrders.WaitToProceed();
                }

                GetOrdersRequest getOrdersRequest = new GetOrdersRequest();
                getOrdersRequest.AccountId = accountId;

                if (historyFilters)
                {
                    getOrdersRequest.AdvancedFilters = new GetOrdersRequest.Types.GetOrdersRequestFilters();
                    getOrdersRequest.AdvancedFilters.ExecutionStatus.Add(OrderExecutionReportStatus.ExecutionReportStatusCancelled);
                    getOrdersRequest.AdvancedFilters.ExecutionStatus.Add(OrderExecutionReportStatus.ExecutionReportStatusRejected);
                    getOrdersRequest.AdvancedFilters.ExecutionStatus.Add(OrderExecutionReportStatus.ExecutionReportStatusFill);
                    getOrdersRequest.AdvancedFilters.From = from.ToTimestamp();
                    getOrdersRequest.AdvancedFilters.To = to.ToTimestamp();
                }

                try
                {
                    GetOrdersResponse response = _ordersClient.GetOrders(getOrdersRequest, _gRpcMetadata);

                    if (historyFilters
                        && response != null
                        && response.Orders.Count >= 100)
                    {
                        TimeSpan span = to - from;

                        if (span.TotalMinutes > 5)
                        {
                            wasCapped = true;
                            from = to - TimeSpan.FromTicks(span.Ticks / 2);
                            continue;
                        }
                    }

                    return response;
                }
                catch (RpcException ex)
                {
                    string message = GetGRPCErrorMessage(ex);

                    if (message.IndexOf("maximum request period exceeded", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        TimeSpan span = to - from;

                        if (span.TotalMinutes > 5)
                        {
                            from = to - TimeSpan.FromTicks(span.Ticks / 2);
                            continue;
                        }
                    }

                    if (message.Contains("no server message") == false)
                    {
                        SendLogMessage($"Error getting all orders. Info: {message}", LogMessageType.System);
                    }

                    return null;
                }
                catch (Exception exception)
                {
                    SendLogMessage("Get all orders request error. " + exception.ToString(), LogMessageType.System);
                    return null;
                }
            }

            return null;
        }

        private List<Order> GetAllOrdersFromExchangeByPortfolio(string accountId, bool onlyActive, out bool failed)
        {
            failed = true;
            failed = true;

            if (_securities == null
                || _securities.Count == 0)
            {
                return null;
            }

            GetOrdersResponse response = null;

            if (onlyActive)
            {
                bool cappedActive;
                response = RequestOrdersWindow(accountId, false, DateTime.MinValue, DateTime.MinValue, out cappedActive);
            }
            else
            {
                // история двумя фазами: сначала узкое окно свежих (гарантированно полное),
                // потом час перед ними
                DateTime utcNow = DateTime.UtcNow;
                DateTime dayStart = utcNow.Date;

                DateTime recentFrom = utcNow.AddMinutes(-5);
                if (recentFrom < dayStart)
                {
                    recentFrom = dayStart;
                }

                bool cappedRecent;
                GetOrdersResponse recentResponse = RequestOrdersWindow(accountId, true, recentFrom, utcNow, out cappedRecent);

                // потом час перед ними (брокер отбивает окна шире ~1-2 часов)
                DateTime restFrom = recentFrom.AddHours(-1);
                if (restFrom < dayStart)
                {
                    restFrom = dayStart;
                }

                GetOrdersResponse olderResponse = null;

                if (restFrom < recentFrom)
                {
                    bool cappedOlder;
                    olderResponse = RequestOrdersWindow(accountId, true, restFrom, recentFrom, out cappedOlder);
                }

                if (recentResponse == null
                    && olderResponse == null)
                {
                    return null;
                }

                // слияние двух фаз с дедупликацией по биржевому номеру
                response = new GetOrdersResponse();

                if (recentResponse != null)
                {
                    response.Orders.AddRange(recentResponse.Orders);
                }

                if (olderResponse != null)
                {
                    for (int i = 0; i < olderResponse.Orders.Count; i++)
                    {
                        OrderState older = olderResponse.Orders[i];

                        bool alreadyHave = false;

                        for (int j = 0; j < response.Orders.Count; j++)
                        {
                            if (response.Orders[j].OrderId == older.OrderId)
                            {
                                alreadyHave = true;
                                break;
                            }
                        }

                        if (alreadyHave == false)
                        {
                            response.Orders.Add(older);
                        }
                    }
                }
            }

            try
            {
                if (response != null)
                {
                    List<Order> osEngineOrders = new List<Order>();

                    for (int i = 0; i < response.Orders.Count; i++)
                    {
                        OrderState state = response.Orders[i];
                        Security security = GetSecurityByIdFast(state.InstrumentUid);

                        if (security == null)
                        {
                            continue;
                        }

                        Order newOrder = new Order();

                        newOrder.ServerType = ServerType.TInvest;
                        newOrder.SecurityNameCode = security.Name;
                        newOrder.Volume = state.LotsRequested;
                        newOrder.VolumeExecute = state.LotsExecuted;
                        newOrder.PortfolioNumber = accountId;
                        newOrder.TypeOrder = state.OrderType == OrderType.Limit
                            ? OrderPriceType.Limit
                            : OrderPriceType.Market;

                        if (state.OrderType == OrderType.Limit)
                        {
                            newOrder.Price = GetValue(state.InitialSecurityPrice) / security.PriceStepCost * security.PriceStep;
                        }

                        string orderId = state.OrderRequestId;

                        lock (_orderNumbersLocker)
                        {
                            if (_orderNumbers.ContainsKey(orderId))
                            {
                                newOrder.NumberUser = _orderNumbers[orderId];
                            }
                            else
                            {   // не наша заявка (ручная или порождённая стоп-ордером). Пропускаем только её,
                                // а не весь список заявок по счёту
                                continue;
                            }

                        }

                        newOrder.NumberMarket = state.OrderId;
                        newOrder.TimeCallBack = TimeZoneInfo.ConvertTimeFromUtc(state.OrderDate.ToDateTime(), _mskTimeZone);// convert to MSK
                        newOrder.Side = state.Direction == OrderDirection.Buy ? Side.Buy : Side.Sell;

                        if (state.ExecutionReportStatus == OrderExecutionReportStatus.ExecutionReportStatusUnspecified)
                        {
                            newOrder.State = OrderStateType.None;
                        }
                        else if (state.ExecutionReportStatus == OrderExecutionReportStatus.ExecutionReportStatusFill)
                        {
                            newOrder.State = OrderStateType.Done;
                            newOrder.TimeDone = TimeZoneInfo.ConvertTimeFromUtc(state.OrderDate.ToDateTime(), _mskTimeZone);// convert to MSK
                        }
                        else if (state.ExecutionReportStatus == OrderExecutionReportStatus.ExecutionReportStatusRejected)
                        {
                            newOrder.State = OrderStateType.Fail;
                        }
                        else if (state.ExecutionReportStatus == OrderExecutionReportStatus.ExecutionReportStatusCancelled)
                        {
                            newOrder.State = OrderStateType.Cancel;
                            newOrder.TimeCancel = TimeZoneInfo.ConvertTimeFromUtc(state.OrderDate.ToDateTime(), _mskTimeZone);// convert to MSK
                        }
                        else if (state.ExecutionReportStatus == OrderExecutionReportStatus.ExecutionReportStatusNew)
                        {
                            newOrder.State = OrderStateType.Active;
                        }
                        else if (state.ExecutionReportStatus == OrderExecutionReportStatus.ExecutionReportStatusPartiallyfill)
                        {
                            newOrder.State = OrderStateType.Partial;
                        }

                        osEngineOrders.Add(newOrder);
                    }

                    failed = false;   // только на полностью размеченном ответе: бросок в маппинге должен остаться failed=true
                    return osEngineOrders;
                }
                else
                {
                    SendLogMessage("Get all orders request error. ", LogMessageType.System);
                }
            }
            catch (Exception exception)
            {
                SendLogMessage("Get all orders request error. " + exception.ToString(), LogMessageType.System);
            }

            return null;
        }

        public List<Order> GetActiveOrders(int startIndex, int count)
        {
            // 1 берём все ордера

            List<Order> orders = new List<Order>();

            for (int i = 0; i < _myPortfolios.Count; i++)
            {
                List<Order> newOrders = GetAllOrdersFromExchangeByPortfolio(_myPortfolios[i].Number, true, out _);
                if (newOrders != null && newOrders.Count > 0)
                {
                    orders.AddRange(newOrders);
                }
            }

            List<Order> activeStopOrders = GetAllActiveStopOrders();

            if (activeStopOrders != null && activeStopOrders.Count > 0)
            {
                orders.AddRange(activeStopOrders);
            }

            // 2 оставляем только активные

            List<Order> ordersActive = new List<Order>();

            for (int i = 0; i < orders.Count; i++)
            {
                Order order = orders[i];

                if (order.State != OrderStateType.Active
                    && order.State != OrderStateType.Pending
                    && order.State != OrderStateType.Partial)
                {
                    continue;
                }

                ordersActive.Add(order);
            }

            if (ordersActive.Count > 1)
            {
                ordersActive = ordersActive.OrderByDescending(x => x.TimeCallBack).ToList();
            }

            // 3 берём из массива по индексам

            List<Order> resultExit = new List<Order>();

            if (ordersActive.Count != 0
                && startIndex < ordersActive.Count)
            {
                if (startIndex + count < ordersActive.Count)
                {
                    resultExit = ordersActive.GetRange(startIndex, count);
                }
                else
                {
                    resultExit = ordersActive.GetRange(startIndex, ordersActive.Count - startIndex);
                }
            }

            for (int i = 0; i < resultExit.Count; i++)
            {
                LogOrderInFullLog(resultExit[i]);
            }

            return resultExit;
        }

        public List<Order> GetHistoricalOrders(int startIndex, int count)
        {
            // 1 берём все ордера

            List<Order> orders = new List<Order>();
            bool historyFailed = false;

            for (int i = 0; i < _myPortfolios.Count; i++)
            {
                bool failed;
                List<Order> newOrders = GetAllOrdersFromExchangeByPortfolio(_myPortfolios[i].Number, false, out failed);
                if (failed)
                {
                    historyFailed = true;
                    continue;
                }

                if (newOrders != null && newOrders.Count > 0)
                {
                    orders.AddRange(newOrders);
                }
            }

            if (historyFailed)
            {   // заявки не получены — доклеивать стопы и выдавать это как историю нельзя.
                // System, не Error: Error на поллинге журнала = ErrorLogMessageBox каждые 5 секунд
                SendLogMessage("GetHistoricalOrders: orders request failed", LogMessageType.System);
                return null;
            }

            List<Order> historicalStopOrders = GetHistoricalStopOrders();

            if (historicalStopOrders != null && historicalStopOrders.Count > 0)
            {
                orders.AddRange(historicalStopOrders);
            }

            // 2 оставляем только исторические, не активные ордера

            List<Order> ordersDontActive = new List<Order>();

            for (int i = 0; i < orders.Count; i++)
            {
                Order order = orders[i];

                if (order.State == OrderStateType.Active
                    || order.State == OrderStateType.Pending
                    || order.State == OrderStateType.Partial)
                {
                    continue;
                }
                ordersDontActive.Add(order);
            }

            if (ordersDontActive.Count > 1)
            {
                ordersDontActive = ordersDontActive.OrderByDescending(x => x.TimeCallBack).ToList();
            }

            // 3 берём из массива по индексам

            List<Order> resultExit = new List<Order>();

            if (ordersDontActive.Count != 0
                && startIndex < ordersDontActive.Count)
            {
                if (startIndex + count < ordersDontActive.Count)
                {
                    resultExit = ordersDontActive.GetRange(startIndex, count);
                }
                else
                {
                    resultExit = ordersDontActive.GetRange(startIndex, ordersDontActive.Count - startIndex);
                }
            }

            for (int i = 0; i < resultExit.Count; i++)
            {
                LogOrderInFullLog(resultExit[i]);
            }

            return resultExit;
        }

        public void SendStopOrder(Order order)
        {
            try
            {
                _rateGateStopOrders.WaitToProceed();

                Security security = _securities.Where(s => _securityStreamMap.ContainsKey(s.NameId)).FirstOrDefault((sec) =>
                    sec.Name == order.SecurityNameCode);

                if (security == null)
                {
                    security = _pollSubscribedSecurities.Find((sec) => sec.Name == order.SecurityNameCode);
                }

                if (security == null)
                {
                    security = _securities.Find((sec) =>
                    sec.Name == order.SecurityNameCode);
                }

                if (security == null)
                {
                    SendLogMessage(OsLocalization.Market.Label291 + "\nSecurity not found: " + order.SecurityNameCode, LogMessageType.Error);

                    CreateOrderFail(order, "Security not found: " + order.SecurityNameCode);

                    return;
                }

                if (order.Volume <= 0)
                {
                    SendLogMessage(OsLocalization.Market.Label291 + "\nVolume is zero: " + order.SecurityNameCode, LogMessageType.Error);

                    CreateOrderFail(order, "Volume is zero: " + order.SecurityNameCode);

                    return;
                }

                decimal orderPrice = order.Price;
                decimal priceCondition = order.StopPrice;

                if (security.SecurityType == SecurityType.Bond
                    && security.NominalCurrent != 0)
                {
                    orderPrice = orderPrice / (security.NominalCurrent / 100);
                    priceCondition = priceCondition / (security.NominalCurrent / 100);
                }

                PostStopOrderRequest request = new PostStopOrderRequest();
                request.Direction = order.Side == Side.Buy ? StopOrderDirection.Buy : StopOrderDirection.Sell;
                request.AccountId = order.PortfolioNumber;
                request.InstrumentId = security.NameId;
                request.Quantity = Convert.ToInt64(order.Volume);
                request.StopPrice = ConvertToQuotation(priceCondition);
                request.ExchangeOrderType = order.TypeOrder == OrderPriceType.StopLimit
                    ? ExchangeOrderType.Limit
                    : ExchangeOrderType.Market;
                request.StopOrderType = order.TypeOrder == OrderPriceType.StopLimit
                    ? StopOrderType.StopLimit
                    : StopOrderType.StopLoss;
                request.ExpirationType = StopOrderExpirationType.GoodTillCancel;
                request.ConfirmMarginTrade = true;

                if (order.TypeOrder == OrderPriceType.StopLimit)
                {
                    request.Price = ConvertToQuotation(orderPrice);
                }

                // цены облигаций (в % от номинала) и фьючерсов (в пунктах) шлём как пункты
                if (security.SecurityType == SecurityType.Bond
                    || security.SecurityType == SecurityType.Futures)
                {
                    request.PriceType = PriceType.Point;
                }

                // генерируем новый номер ордера и добавляем его в словарь
                Guid newUid = Guid.NewGuid();
                string orderId = newUid.ToString();

                lock (_orderNumbersLocker)
                {
                    _orderNumbers.Add(orderId, order.NumberUser);
                }

                _orderPrices[orderId] = order.Price;

                request.OrderId = orderId;

                PostStopOrderResponse response = null;

                try
                {
                    response = _stopOrdersClient.PostStopOrder(request, _gRpcMetadata);
                }
                catch (RpcException ex)
                {
                    string message = GetGRPCErrorMessage(ex);

                    if (message.Contains("Not enough assets"))
                    {
                        CheckCrazyNotEnoughAssetsOrderSpam();
                        message = OsLocalization.Market.Label301;
                    }
                    else if (message.Contains("The price is too high"))
                    {
                        message = OsLocalization.Market.Label302;
                    }
                    else if (message.Contains("The price is outside the limits for"))
                    {
                        message = OsLocalization.Market.Label304;
                    }
                    else if (message.Contains("Pol`zovatel` ne najden"))
                    {
                        message = OsLocalization.Market.Label319;
                    }

                    SendLogMessage(OsLocalization.Market.Label291 +
                            "\n" + message +
                            "\n" + order.SecurityNameCode
                            + ", " + OsLocalization.Market.Message21 + order.Volume
                            + ", " + OsLocalization.Market.Label303 + " " + order.Price + " " + order.Side
                            , LogMessageType.Error);

                    CreateOrderFail(order, message);

                    return;
                }
                catch (Exception exception)
                {
                    SendLogMessage(OsLocalization.Market.Label291 + "\n" + exception.Message, LogMessageType.Error);

                    CreateOrderFail(order, exception.Message);

                    return;
                }

                order.State = OrderStateType.Active;
                order.NumberMarket = response.StopOrderId;

                lock (_stopOrdersLocker)
                {   // сначала в _activeStopOrders: там полные данные ордера для событий из стрима
                    _activeStopOrders.Add(order);
                }

                lock (_orderNumbersLocker)
                {
                    if (_stopOrderNumbers.ContainsKey(response.StopOrderId) == false)
                    {
                        _stopOrderNumbers.Add(response.StopOrderId, order.NumberUser);
                    }
                }

                if (MyOrderEvent != null)
                {
                    MyOrderEvent(order);
                }
            }
            catch (Exception exception)
            {
                SendLogMessage(OsLocalization.Market.Label291 + "\n" + exception, LogMessageType.Error);

                CreateOrderFail(order, exception.Message);
            }
        }

        private bool CancelStopOrder(Order order, string source = "")
        {
            _rateGateStopOrders.WaitToProceed();

            CancelStopOrderRequest request = new CancelStopOrderRequest();
            request.AccountId = order.PortfolioNumber;
            request.StopOrderId = order.NumberMarket;

            CancelStopOrderResponse response = null;

            try
            {
                response = _stopOrdersClient.CancelStopOrder(request, _gRpcMetadata);
            }
            catch (RpcException ex)
            {
                string message = GetGRPCErrorMessage(ex);
                SendLogMessage(OsLocalization.Market.Label293 + "\n" + message, LogMessageType.Error);
            }
            catch (Exception exception)
            {
                SendLogMessage(OsLocalization.Market.Label293 + "\n" +
                    exception.Message + "  " + order.SecurityClassCode, LogMessageType.Error);
            }

            if (response != null)
            {
                // статус Cancel выставит стрим заявок, когда биржа подтвердит отзыв
                return true;
            }

            OrderStateType state = GetStopOrderStatus(order, source);

            if (state == OrderStateType.None)
            {
                return false;
            }

            return true;
        }

        private OrderStateType GetStopOrderStatus(Order order, string source = "")
        {
            try
            {
                List<StopOrder> stopsFromServer = GetStopOrdersFromServer(order.PortfolioNumber, StopOrderStatusOption.StopOrderStatusAll);

                if (stopsFromServer == null)
                {
                    return OrderStateType.None;
                }

                for (int i = 0; i < stopsFromServer.Count; i++)
                {
                    if (stopsFromServer[i].StopOrderId == order.NumberMarket)
                    {
                        StopOrderStatusOption serverStatus = stopsFromServer[i].Status;
                        OrderStateType state = GetStateFromStopOrderStatus(serverStatus);

                        if (state == OrderStateType.Cancel)
                        {
                            order.State = state;
                            order.TimeCallBack = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, _mskTimeZone);// convert to MSK

                            if (serverStatus == StopOrderStatusOption.StopOrderStatusExecuted)
                            {   // активация: стоп завершил жизнь, породив биржевую заявку
                                order.TimeCancel = stopsFromServer[i].ActivationDateTime != null
                                    ? TimeZoneInfo.ConvertTimeFromUtc(stopsFromServer[i].ActivationDateTime.ToDateTime(), _mskTimeZone)
                                    : order.TimeCallBack;

                                if (stopsFromServer[i].HasExchangeOrderId)
                                {   // биржевой id порождённой заявки
                                    order.ChildOrderNumberMarket = stopsFromServer[i].ExchangeOrderId;
                                }
                            }
                            else
                            {
                                order.TimeCancel = order.TimeCallBack;
                            }

                            lock (_stopOrdersLocker)
                            {
                                RemoveActiveStopOrderUnsafe(order.NumberMarket);
                            }

                            if (serverStatus == StopOrderStatusOption.StopOrderStatusExecuted)
                            {
                                ProcessStopOrderTrades(order, stopsFromServer[i], source);
                            }

                            LogOrderInFullLog(order, source);

                            MyOrderEvent?.Invoke(order);
                        }

                        return state;
                    }
                }
            }
            catch (RpcException ex)
            {
                string message = GetGRPCErrorMessage(ex);
                SendLogMessage($"Error getting stop order state. Info: {message}", LogMessageType.System);
            }
            catch (Exception ex)
            {
                SendLogMessage("Error getting stop order state " + order.SecurityNameCode + " exception: " + ex.ToString(), LogMessageType.System);
            }

            return OrderStateType.None;
        }

        private List<Order> GetAllActiveStopOrders()
        {
            List<Order> result = new List<Order>();

            for (int i = 0; i < _myPortfolios.Count; i++)
            {
                try
                {
                    List<StopOrder> stopsFromServer = GetStopOrdersFromServer(_myPortfolios[i].Number, StopOrderStatusOption.StopOrderStatusActive);

                    if (stopsFromServer == null)
                    {
                        continue;
                    }

                    for (int j = 0; j < stopsFromServer.Count; j++)
                    {
                        StopOrder stop = stopsFromServer[j];

                        Security security = GetSecurityByIdFast(stop.InstrumentUid);

                        if (security == null)
                        {
                            continue;
                        }

                        Order newOrder = new Order();

                        newOrder.ServerType = ServerType.TInvest;
                        newOrder.SecurityNameCode = security.Name;
                        newOrder.SecurityClassCode = security.NameClass;
                        newOrder.PortfolioNumber = _myPortfolios[i].Number;
                        newOrder.NumberMarket = stop.StopOrderId;
                        newOrder.Side = stop.Direction == StopOrderDirection.Buy ? Side.Buy : Side.Sell;
                        newOrder.TypeOrder = stop.ExchangeOrderType == ExchangeOrderType.Market
                            ? OrderPriceType.StopMarket
                            : OrderPriceType.StopLimit;
                        newOrder.Volume = stop.LotsRequested;
                        newOrder.State = OrderStateType.Active;
                        newOrder.TimeCallBack = stop.CreateDate != null
                            ? TimeZoneInfo.ConvertTimeFromUtc(stop.CreateDate.ToDateTime(), _mskTimeZone)// convert to MSK
                            : TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, _mskTimeZone);

                        decimal price = GetValue(stop.Price);
                        decimal priceCondition = GetValue(stop.StopPrice);

                        if (security.SecurityType == SecurityType.Bond
                            && security.NominalCurrent != 0)
                        {
                            price = price * (security.NominalCurrent / 100);
                            priceCondition = priceCondition * (security.NominalCurrent / 100);
                        }

                        newOrder.Price = price;
                        newOrder.StopPrice = priceCondition;

                        lock (_orderNumbersLocker)
                        {
                            if (_stopOrderNumbers.ContainsKey(stop.StopOrderId))
                            {
                                newOrder.NumberUser = _stopOrderNumbers[stop.StopOrderId];
                            }
                            else
                            {
                                newOrder.NumberUser = NumberGen.GetNumberOrder(StartProgram.IsOsTrader);
                                _stopOrderNumbers.Add(stop.StopOrderId, newOrder.NumberUser);
                            }
                        }

                        lock (_stopOrdersLocker)
                        {
                            bool alreadyInList = false;

                            for (int k = 0; k < _activeStopOrders.Count; k++)
                            {
                                if (_activeStopOrders[k].NumberMarket == newOrder.NumberMarket)
                                {
                                    alreadyInList = true;
                                    break;
                                }
                            }

                            if (alreadyInList == false)
                            {
                                _activeStopOrders.Add(newOrder);
                            }
                        }

                        result.Add(newOrder);
                    }
                }
                catch (RpcException ex)
                {
                    string message = GetGRPCErrorMessage(ex);
                    SendLogMessage($"Error getting active stop orders. Info: {message}", LogMessageType.System);
                }
                catch (Exception ex)
                {
                    SendLogMessage("Error getting active stop orders. " + ex.ToString(), LogMessageType.System);
                }
            }

            return result;
        }

        private List<Order> GetHistoricalStopOrders()
        {
            List<Order> result = new List<Order>();

            for (int i = 0; i < _myPortfolios.Count; i++)
            {
                try
                {
                    List<StopOrder> stopsFromServer = GetStopOrdersFromServer(_myPortfolios[i].Number, StopOrderStatusOption.StopOrderStatusAll);

                    if (stopsFromServer == null)
                    {
                        continue;
                    }

                    for (int j = 0; j < stopsFromServer.Count; j++)
                    {
                        StopOrder stop = stopsFromServer[j];

                        if (stop.Status == StopOrderStatusOption.StopOrderStatusActive)
                        {
                            continue;
                        }

                        Security security = GetSecurityByIdFast(stop.InstrumentUid);

                        if (security == null)
                        {
                            continue;
                        }

                        Order newOrder = new Order();

                        newOrder.ServerType = ServerType.TInvest;
                        newOrder.SecurityNameCode = security.Name;
                        newOrder.SecurityClassCode = security.NameClass;
                        newOrder.PortfolioNumber = _myPortfolios[i].Number;
                        newOrder.NumberMarket = stop.StopOrderId;
                        newOrder.Side = stop.Direction == StopOrderDirection.Buy ? Side.Buy : Side.Sell;
                        newOrder.TypeOrder = stop.ExchangeOrderType == ExchangeOrderType.Market
                            ? OrderPriceType.StopMarket
                            : OrderPriceType.StopLimit;
                        newOrder.Volume = stop.LotsRequested;
                        newOrder.State = GetStateFromStopOrderStatus(stop.Status);

                        DateTime createTime = stop.CreateDate != null
                            ? TimeZoneInfo.ConvertTimeFromUtc(stop.CreateDate.ToDateTime(), _mskTimeZone)
                            : TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, _mskTimeZone);

                        newOrder.TimeCallBack = createTime;

                        if (stop.Status == StopOrderStatusOption.StopOrderStatusExecuted)
                        {   // активация: стоп завершил жизнь, породив биржевую заявку (State == Cancel)
                            newOrder.TimeCancel = stop.ActivationDateTime != null
                                ? TimeZoneInfo.ConvertTimeFromUtc(stop.ActivationDateTime.ToDateTime(), _mskTimeZone)
                                : createTime;

                            if (stop.HasExchangeOrderId)
                            {   // биржевой id порождённой заявки
                                newOrder.ChildOrderNumberMarket = stop.ExchangeOrderId;
                            }
                        }
                        else if (newOrder.State == OrderStateType.Cancel)
                        {
                            newOrder.TimeCancel = stop.ExpirationTime != null
                                ? TimeZoneInfo.ConvertTimeFromUtc(stop.ExpirationTime.ToDateTime(), _mskTimeZone)
                                : createTime;
                        }

                        decimal price = GetValue(stop.Price);
                        decimal priceCondition = GetValue(stop.StopPrice);

                        if (security.SecurityType == SecurityType.Bond
                            && security.NominalCurrent != 0)
                        {
                            price = price * (security.NominalCurrent / 100);
                            priceCondition = priceCondition * (security.NominalCurrent / 100);
                        }

                        newOrder.Price = price;
                        newOrder.StopPrice = priceCondition;

                        lock (_orderNumbersLocker)
                        {
                            if (_stopOrderNumbers.ContainsKey(stop.StopOrderId))
                            {
                                newOrder.NumberUser = _stopOrderNumbers[stop.StopOrderId];
                            }
                            else
                            {
                                newOrder.NumberUser = NumberGen.GetNumberOrder(StartProgram.IsOsTrader);
                                _stopOrderNumbers.Add(stop.StopOrderId, newOrder.NumberUser);
                            }
                        }

                        result.Add(newOrder);
                    }
                }
                catch (RpcException ex)
                {
                    string message = GetGRPCErrorMessage(ex);
                    SendLogMessage($"Error getting historical stop orders. Info: {message}", LogMessageType.System);
                }
                catch (Exception ex)
                {
                    SendLogMessage("Error getting historical stop orders. " + ex.ToString(), LogMessageType.System);
                }
            }

            return result;
        }

        #endregion

        #region 10 Helpers

        // лотность для пересчёта объёма сделки из штук в лоты:
        // сначала из самого события стрима, потом из справочника бумаг
        private decimal GetLotSizeForTrade(OrderStateStreamResponse.Types.OrderState state, Security security)
        {
            if (state != null
                && state.LotSize > 0)
            {
                return state.LotSize;
            }

            if (security != null
                && security.Lot > 0)
            {
                return security.Lot;
            }

            return 1;
        }

        private OrderStateType GetStateFromStopOrderStatus(StopOrderStatusOption status)
        {
            if (status == StopOrderStatusOption.StopOrderStatusActive)
            {
                return OrderStateType.Active;
            }

            if (status == StopOrderStatusOption.StopOrderStatusExecuted)
            {
                return OrderStateType.Cancel;
            }

            if (status == StopOrderStatusOption.StopOrderStatusCanceled
                || status == StopOrderStatusOption.StopOrderStatusExpired)
            {
                return OrderStateType.Cancel;
            }

            return OrderStateType.None;
        }

        private List<StopOrder> GetStopOrdersFromServer(string accountId, StopOrderStatusOption status)
        {
            _rateGateStopOrders.WaitToProceed();

            GetStopOrdersRequest request = new GetStopOrdersRequest();
            request.AccountId = accountId;
            request.Status = status;

            GetStopOrdersResponse response = _stopOrdersClient.GetStopOrders(request, _gRpcMetadata);

            if (response == null)
            {
                return null;
            }

            List<StopOrder> result = new List<StopOrder>();

            for (int i = 0; i < response.StopOrders.Count; i++)
            {
                result.Add(response.StopOrders[i]);
            }

            return result;
        }

        private string GetGRPCErrorMessage(RpcException exception)
        {
            string message = "no server message";
            string trackingId = "";

            if (exception.Trailers == null)
                return message;

            for (int i = 0; i < exception.Trailers.Count; i++)
            {
                if (exception.Trailers[i].Key == "x-tracking-id")
                    trackingId = exception.Trailers[i].Value;

                if (exception.Trailers[i].Key == "message")
                    message = exception.Trailers[i].Value;
            }

            if (trackingId.Length > 0)
            {
                message = "Tracking id: " + trackingId + "; Message: " + message;
            }

            return message;
        }

        private Quotation ConvertToQuotation(decimal value)
        {
            const decimal nanoFactor = 1_000_000_000;
            long wholePart = (long)value;

            Quotation quotation = new Quotation();

            quotation.Units = wholePart;
            quotation.Nano = (int)((value - wholePart) * nanoFactor);

            return quotation;
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

        public void SetLeverage(Security security, decimal leverage) { }

        #endregion

        #region 11 Log

        private void SendLogMessage(string message, LogMessageType messageType)
        {
            LogMessageEvent?.Invoke(message, messageType);
        }

        public event Action<string, LogMessageType> LogMessageEvent;

        public event Action<Funding> FundingUpdateEvent { add { } remove { } }

        public event Action<SecurityVolumes> Volume24hUpdateEvent { add { } remove { } }

        #endregion
    }

    public class TinSecuritiesRisksFutures
    {
        public decimal MarginBuyCoeffClient;

        public decimal MarginSellCoeffClient;
    }

    // исполненная стоп-заявка, ожидающая дозапроса данных с сервера
    public class StopOrderExecution
    {
        public StopOrderExecution(Order order, string accountId, string stopOrderId)
        {
            Order = order;
            AccountId = accountId;
            StopOrderId = stopOrderId;
        }

        public Order Order;
        public string AccountId;
        public string StopOrderId;
    }
}