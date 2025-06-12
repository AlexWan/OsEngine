using Google.Protobuf.Collections;
using Google.Protobuf.WellKnownTypes;
//using Google.Type;
using Grpc.Core;
using Grpc.Net.Client;
using Grpc.Tradeapi.V1;
using Grpc.Tradeapi.V1.Accounts;
using Grpc.Tradeapi.V1.Assets;
using Grpc.Tradeapi.V1.Auth;
using Grpc.Tradeapi.V1.Marketdata;
using Grpc.Tradeapi.V1.Orders;
using OsEngine.Entity;
using OsEngine.Language;
using OsEngine.Logging;
using OsEngine.Market.Servers.Alor.Json;
using OsEngine.Market.Servers.Entity;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Candle = OsEngine.Entity.Candle;
using FPosition = Grpc.Tradeapi.V1.Accounts.Position;
using FSide = Grpc.Tradeapi.V1.Side;
using FTimeFrame = Grpc.Tradeapi.V1.Marketdata.TimeFrame;
using FTrade = Grpc.Tradeapi.V1.Marketdata.Trade;
using Order = OsEngine.Entity.Order;
using Portfolio = OsEngine.Entity.Portfolio;
using Security = OsEngine.Entity.Security;
using Side = OsEngine.Entity.Side;
using TimeFrame = OsEngine.Entity.TimeFrame;
using Trade = OsEngine.Entity.Trade;

namespace OsEngine.Market.Servers.FinamGrpc
{
    public class FinamGrpcServer : AServer
    {
        public FinamGrpcServer(int uniqueId)
        {
            ServerNum = uniqueId;

            FinamGrpcServerRealization realization = new FinamGrpcServerRealization();
            ServerRealization = realization;

            // Для работы с API необходим токен secret, сгенерированный на портале Finam ( https://tradeapi.finam.ru/docs/tokens )
            CreateParameterPassword(OsLocalization.Market.ServerParamToken, "");
            // Параметр account_id, это аккаунт в личном кабинете формата КлФ-account_id
            CreateParameterString("Account ID", "");
        }
    }

    public class FinamGrpcServerRealization : IServerRealization
    {

        #region 1 Constructor, Status, Connection
        public FinamGrpcServerRealization()
        {
            ServerTime = DateTime.UtcNow;
            Thread worker0 = new Thread(ConnectionCheckThread);
            worker0.Name = "CheckAliveFinamGrpc";
            worker0.Start();

            Thread worker1 = new Thread(TradesMessageReader);
            worker1.Name = "TradesMessageReaderFinamGrpc";
            worker1.Start();

            Thread worker2 = new Thread(MarketDepthMessageReader);
            worker2.Name = "MarketDepthMessageReaderFinamGrpc";
            worker2.Start();

            Thread worker3 = new Thread(MyOrderTradeMessageReader);
            worker2.Name = "MyOrderTradeMessageReaderFinamGrpc";
            worker2.Start();
        }

        public void Connect(WebProxy proxy)
        {
            try
            {
                SendLogMessage("Start Finam gRPC Connection", LogMessageType.System);

                _proxy = proxy;
                _accessToken = ((ServerParameterPassword)ServerParameters[0]).Value;
                _accountId = ((ServerParameterString)ServerParameters[1]).Value;

                if (string.IsNullOrEmpty(_accessToken))
                {
                    SendLogMessage("Finam gRPC connection terminated. You must specify the api token. You can get it on the Finam website [ https://tradeapi.finam.ru/docs/tokens ]",
                        LogMessageType.Error);
                    return;
                }

                CreateStreamsConnection();

                ServerStatus = ServerConnectStatus.Connect;
                ConnectEvent?.Invoke();
            }
            catch (Exception ex)
            {
                SendLogMessage($"Error connecting to server: {ex}", LogMessageType.Error);
                ServerStatus = ServerConnectStatus.Disconnect;
                DisconnectEvent?.Invoke();
            }
        }

        public void Dispose()
        {
            SendLogMessage("Connection to Finam gRPC closed. Data streams Closed Event", LogMessageType.System);

            if (ServerStatus != ServerConnectStatus.Disconnect)
            {
                ServerStatus = ServerConnectStatus.Disconnect;
                DisconnectEvent?.Invoke();
            }
        }

        public List<IServerParameter> ServerParameters { get; set; }
        public event Action ConnectEvent;
        public event Action DisconnectEvent;

        public ServerType ServerType => ServerType.FinamGrpc;
        public DateTime ServerTime { get; set; }
        public ServerConnectStatus ServerStatus { get; set; } = ServerConnectStatus.Disconnect;
        #endregion

        #region 2 Properties

        private string _accessToken;
        private string _accountId;

        private int _timezoneOffset = 3;

        //private Dictionary<string, int> _orderNumbers = new Dictionary<string, int>();
        #endregion

        #region 3 Securities
        public void GetSecurities()
        {
            _rateGateAssetsAsset.WaitToProceed();

            AssetsResponse assetsResponse = null;
            try
            {
                assetsResponse = _assetsClient.Assets(new AssetsRequest(), headers: _gRpcMetadata);
            }
            catch (RpcException ex)
            {
                string msg = GetGRPCErrorMessage(ex);
                SendLogMessage($"Error loading securities. Info: {msg}", LogMessageType.Error);
            }
            catch (Exception ex)
            {
                SendLogMessage($"Error loading securities: {ex}", LogMessageType.Error);
            }

            _updateSecuritiesFromServer(assetsResponse);

            if (_securities.Count > 0)
            {
                SendLogMessage("Securities loaded. Count: " + _securities.Count, LogMessageType.System);

                SecurityEvent?.Invoke(_securities);
            }
        }

        private void _updateSecuritiesFromServer(AssetsResponse assetsResponse)
        {
            if (assetsResponse == null ||
                assetsResponse.Assets.Count == 0)
            {
                return;
            }

            try
            {
                for (int i = 0; i < assetsResponse.Assets.Count; i++)
                {
                    Asset item = assetsResponse.Assets[i];

                    Security newSecurity = new Security();
                    newSecurity.Name = item.Symbol;
                    newSecurity.NameId = item.Symbol;
                    newSecurity.NameFull = string.IsNullOrEmpty(item.Name) ? item.Symbol : item.Name; // item.Ticker;
                    newSecurity.Exchange = item.Mic;
                    newSecurity.NameClass = item.Type;
                    newSecurity.SecurityType = item.Type.ToLower() switch
                    {
                        "funds" => SecurityType.Fund,
                        "equities" => SecurityType.Stock,
                        "futures" => SecurityType.Futures,
                        "bonds" => SecurityType.Bond,
                        "spreads" => SecurityType.Futures, // TODO may be other type
                        "currencies" => SecurityType.CurrencyPair,
                        "indices" => SecurityType.Index,
                        //"swaps" => SecurityType.None,
                        //"other" => SecurityType.None,
                        _ => SecurityType.None,
                    };
                    newSecurity.PriceStep = 1; // Нет данных
                    newSecurity.PriceStepCost = newSecurity.PriceStep;
                    newSecurity.VolumeStep = 1;
                    newSecurity.Lot = 1;
                    newSecurity.MinTradeAmount = 1;
                    newSecurity.MinTradeAmountType = MinTradeAmountType.Contract;

                    newSecurity.State = SecurityStateType.Activ;
                    _securities.Add(newSecurity);
                }

            }
            catch (Exception e)
            {
                SendLogMessage($"Error loading currency pairs: {e.Message}", LogMessageType.Error);
            }
        }

        private List<Security> _securities = new List<Security>();

        public event Action<List<Security>> SecurityEvent;

        private RateGate _rateGateAssetsAsset = new RateGate(60, TimeSpan.FromMinutes(1));
        #endregion

        #region 4 Portfolios
        public void GetPortfolios()
        {
            GetAccountResponse getAccountResponse = null;
            try
            {
                getAccountResponse = _accountsClient.GetAccount(new GetAccountRequest { AccountId = _accountId }, headers: _gRpcMetadata);
            }
            catch (RpcException ex)
            {
                string msg = GetGRPCErrorMessage(ex);
                SendLogMessage($"Error loading portfolios. Info: {msg}", LogMessageType.Error);
            }
            catch (Exception ex)
            {
                SendLogMessage($"Error loading portfolios: {ex}", LogMessageType.Error);
            }

            GetPortfolios(getAccountResponse);

            PortfolioEvent?.Invoke(_myPortfolios);
        }

        private void GetPortfolios(GetAccountResponse getAccountResponse)
        {
            Portfolio myPortfolio = _myPortfolios.Find(p => p.Number == getAccountResponse.AccountId);

            if (myPortfolio == null)
            {
                myPortfolio = new Portfolio();
                myPortfolio.Number = getAccountResponse.AccountId;
                myPortfolio.ValueCurrent = getAccountResponse.Equity.Value.ToDecimal();
                myPortfolio.ValueBegin = myPortfolio.ValueCurrent;
                myPortfolio.UnrealizedPnl = getAccountResponse.UnrealizedProfit.Value.ToDecimal();
                _myPortfolios.Add(myPortfolio);
            }

            //for (int i = 0; i < getAccountResponse.Cash.Count; i++)
            //{
            //    GoogleType.Money pos = getAccountResponse.Cash[i];
            //    PositionOnBoard newPos = new PositionOnBoard();

            //    newPos.PortfolioName = myPortfolio.Number;
            //    newPos.SecurityNameCode = pos.CurrencyCode;
            //    newPos.ValueCurrent = GetValue(pos);
            //    //newPos.ValueBlocked = pos.Blocked / instrument.Instrument.Lot;
            //    newPos.ValueBegin = newPos.ValueCurrent;

            //    myPortfolio.SetNewPosition(newPos);
            //}

            for (int i = 0; i < getAccountResponse.Positions.Count; i++)
            {
                FPosition pos = getAccountResponse.Positions[i];
                PositionOnBoard newPos = new PositionOnBoard();

                newPos.PortfolioName = myPortfolio.Number;
                newPos.SecurityNameCode = pos.Symbol; // TODO проверить
                newPos.ValueCurrent = pos.Quantity.Value.ToDecimal() * pos.CurrentPrice.Value.ToDecimal();
                //newPos.ValueBlocked = pos.Blocked / instrument.Instrument.Lot;
                newPos.ValueBegin = pos.Quantity.Value.ToDecimal() * pos.AveragePrice.Value.ToDecimal();

                myPortfolio.SetNewPosition(newPos);
            }

        }

        private RateGate _rateGateGetAccount = new RateGate(60, TimeSpan.FromMinutes(1));

        public event Action<List<Portfolio>> PortfolioEvent;

        private List<Portfolio> _myPortfolios = new List<Portfolio>();
        #endregion

        #region 5 Data
        public List<Candle> GetLastCandleHistory(Security security, TimeFrameBuilder timeFrameBuilder, int candleCount)
        {
            DateTime timeStart = DateTime.UtcNow.AddHours(_timezoneOffset) - TimeSpan.FromMinutes(timeFrameBuilder.TimeFrameTimeSpan.TotalMinutes * candleCount);
            DateTime timeEnd = DateTime.UtcNow.AddHours(_timezoneOffset); // to MSK

            List<Candle> candles = GetCandleDataToSecurity(security, timeFrameBuilder, timeStart, timeEnd, timeStart);

            return candles;
        }

        public List<Candle> GetCandleDataToSecurity(Security security, TimeFrameBuilder timeFrameBuilder, DateTime startTime, DateTime endTime,
            DateTime actualTime)
        {

            FTimeFrame ftf = CreateTimeFrameInterval(timeFrameBuilder.TimeFrame);

            List<Candle> candles = new List<Candle>();

            // Данные из будущего не заказываем
            //if (endTime > actualTime)
            //{
            //    endTime = actualTime;
            //}

            // ensure all times are UTC
            startTime = DateTime.SpecifyKind(startTime.AddHours(-_timezoneOffset), DateTimeKind.Utc); // MSK -> UTC
            endTime = DateTime.SpecifyKind(endTime.AddHours(-_timezoneOffset), DateTimeKind.Utc);

            TimeSpan tsHistoryDepth = getHistoryDepth(ftf);
            DateTime queryStartTime = startTime;

            while (queryStartTime < endTime)
            {
                DateTime queryEndTime = queryStartTime.Add(tsHistoryDepth);
                // Не заказываем лишних данных
                if (queryEndTime > endTime)
                    queryEndTime = endTime;


                List<Candle> range = _getCandleHistoryFromServer(queryStartTime, queryEndTime, security, ftf);

                // Если запрошен некорректный таймфрейм, то возвращает null
                if (range == null) return null;

                candles.AddRange(range);

                queryStartTime = queryEndTime;
            }

            return candles;
        }

        private RateGate _rateGateMarketDataBars = new RateGate(60, TimeSpan.FromMinutes(1));
        private List<Candle> _getCandleHistoryFromServer(DateTime fromDateTime, DateTime toDateTime, Security security, FTimeFrame ftf)
        {
            if (ftf == FTimeFrame.Unspecified) return null;

            BarsResponse resp = null;

            try
            {
                BarsRequest req = new BarsRequest
                {
                    Symbol = security.NameId,
                    Timeframe = ftf,
                    Interval = new Google.Type.Interval { StartTime = Timestamp.FromDateTime(fromDateTime), EndTime = Timestamp.FromDateTime(toDateTime) }
                };

                _rateGateMarketDataBars.WaitToProceed();
                resp = _marketDataClient.Bars(req, _gRpcMetadata);
            }
            catch (RpcException ex)
            {
                string message = GetGRPCErrorMessage(ex);
                SendLogMessage($"Error getting candles for {security.Name}. Info: {message}", LogMessageType.Error);
            }
            catch (Exception ex)
            {
                SendLogMessage($"Error getting candles for {security.Name}: " + ex.ToString(), LogMessageType.Error);
            }

            List<Candle> candles = _convertToOsEngineCandles(resp);

            return candles;
        }

        private List<Candle> _convertToOsEngineCandles(BarsResponse response)
        {
            List<Candle> candles = new List<Candle>();

            if (response == null)
                return candles;

            for (int i = 0; i < response.Bars.Count; i++)
            {
                Bar fCandle = response.Bars[i];
                Candle candle = new Candle
                {
                    High = fCandle.High.Value.ToString().ToDecimal(),
                    Low = fCandle.Low.Value.ToString().ToDecimal(),
                    Open = fCandle.Open.Value.ToString().ToDecimal(),
                    Close = fCandle.Close.Value.ToString().ToDecimal(),
                    Volume = fCandle.Volume.Value.ToString().ToDecimal(),
                    State = CandleState.Finished,
                    TimeStart = fCandle.Timestamp.ToDateTime().AddHours(_timezoneOffset) // convert to MSK
                };
                candles.Add(candle);
            }

            return candles;
        }

        private void _updateSecurityParams(Security security)
        {
            try
            {
                _rateGateGetAsset.WaitToProceed();
                GetAssetResponse getAssetResponse = _assetsClient.GetAsset(
                    new GetAssetRequest { AccountId = _accountId, Symbol = security.NameId },
                    headers: _gRpcMetadata);

                if (getAssetResponse == null) return;

                security.Lot = getAssetResponse.LotSize.Value.ToDecimal();
                security.Decimals = (int)getAssetResponse.Decimals;
                security.PriceStep = getAssetResponse.MinStep.ToString().ToDecimal();
                security.PriceStepCost = security.PriceStep;
                if (getAssetResponse.ExpirationDate != null)
                {
                    security.Expiration = getAssetResponse.ExpirationDate.ToDateTime().AddHours(_timezoneOffset); // convert to MSK
                }
            }
            catch (RpcException ex)
            {
                string message = GetGRPCErrorMessage(ex);
                SendLogMessage($"Error loading security params. Info: {message}", LogMessageType.Error);
            }
            catch (Exception ex)
            {
                SendLogMessage($"Error loading security params: {ex}", LogMessageType.Error);
            }

            // Получаем доп инфо по тикеру (лимит 60 запросов в минуту)
            //GetAssetParamsResponse assetParamsResponse = null;
            //try
            //{
            //    _rateGateGetAssetParams.WaitToProceed();
            //    assetParamsResponse = _assetsClient.GetAssetParams(
            //        new GetAssetParamsRequest { AccountId = _accountId, Symbol = item.Symbol },
            //        headers: _gRpcMetadata);
            //}
            //catch (RpcException ex)
            //{
            //    string message = GetGRPCErrorMessage(ex);
            //    SendLogMessage($"Error loading securities. Info: {message}", LogMessageType.Error);
            //}
            //catch (Exception ex)
            //{
            //    SendLogMessage($"Error loading securities: {ex}", LogMessageType.Error);
            //}
        }

        private MarketDepth _getMarketDepth(Security security)
        {
            MarketDepth depth = new MarketDepth();
            depth.SecurityNameCode = security.Name;

            try
            {
                _rateGateOrderBook.WaitToProceed();
                OrderBookResponse resp = _marketDataClient.OrderBook(new OrderBookRequest { Symbol = security.NameId }, _gRpcMetadata);

                OrderBook ob = resp.Orderbook;
                if (ob == null || ob.Rows.Count == 0)
                {
                    return depth;
                }

                depth.Time = ob.Rows[0].Timestamp.ToDateTime().AddHours(_timezoneOffset);// convert to MSK

                for (int i = 0; i < ob.Rows.Count; i++)
                {
                    OrderBook.Types.Row newLevel = ob.Rows[i];

                    if (newLevel.Action == OrderBook.Types.Row.Types.Action.Remove || newLevel.Action == OrderBook.Types.Row.Types.Action.Unspecified) continue;

                    MarketDepthLevel level = new MarketDepthLevel();
                    level.Price = newLevel.Price.Value.ToString().ToDecimal();

                    if (newLevel.SideCase == OrderBook.Types.Row.SideOneofCase.BuySize)
                    {
                        level.Bid = newLevel.BuySize.Value.ToString().ToDecimal();
                        depth.Bids.Add(level);
                    }

                    if (newLevel.SideCase == OrderBook.Types.Row.SideOneofCase.SellSize)
                    {
                        level.Ask = newLevel.SellSize.Value.ToString().ToDecimal();
                        depth.Asks.Add(level);
                    }
                }

                if (_lastMdTime != DateTime.MinValue &&
                    _lastMdTime >= depth.Time)
                {
                    depth.Time = _lastMdTime.AddMilliseconds(1);
                }

                depth.Asks.Sort((x, y) => x.Price.CompareTo(y.Price));
                depth.Bids.Sort((y, x) => x.Price.CompareTo(y.Price));

                _lastMdTime = depth.Time;
            }
            catch (RpcException ex)
            {
                string message = GetGRPCErrorMessage(ex);
                SendLogMessage($"Error getting Market Depth for {security.Name}. Info: {message}", LogMessageType.Error);
            }
            catch (Exception ex)
            {
                SendLogMessage($"Error getting Market Depth for {security.Name}: " + ex.ToString(), LogMessageType.Error);
            }

            return depth;
        }

        private RateGate _rateGateGetAsset = new RateGate(60, TimeSpan.FromMinutes(1));
        private RateGate _rateGateGetAssetParams = new RateGate(60, TimeSpan.FromMinutes(1));
        private RateGate _rateGateOrderBook = new RateGate(60, TimeSpan.FromMinutes(1));
        #endregion

        #region 6 gRPC streams creation
        private void CreateStreamsConnection()
        {
            _cancellationTokenSource = new CancellationTokenSource();

            var httpClient = new HttpClient(new HttpClientHandler
            {
                Proxy = _proxy,
                UseProxy = _proxy != null
            });
            httpClient.Timeout = Timeout.InfiniteTimeSpan;

            _channel = GrpcChannel.ForAddress(_gRPCHost, new GrpcChannelOptions
            {
                Credentials = ChannelCredentials.SecureSsl,
                HttpClient = httpClient,
                MaxRetryAttempts = null,

            });

            _authClient = new AuthService.AuthServiceClient(_channel);
            _assetsClient = new AssetsService.AssetsServiceClient(_channel);
            _accountsClient = new AccountsService.AccountsServiceClient(_channel);
            _myOrderTradeClient = new OrdersService.OrdersServiceClient(_channel);
            _marketDataClient = new MarketDataService.MarketDataServiceClient(_channel);

            try
            {
                // Получаем gwt токен
                AuthResponse auth = _authClient.Auth(new AuthRequest { Secret = _accessToken });
                if (auth?.Token == null)
                {
                    //string errorMessage = string.Join(", ", testResponse.Errors.Select(e => $"{e.Code}: {e.Message}"));
                    SendLogMessage($"Authentication failed. Wrong token?", LogMessageType.Error);
                    return;
                }

                _gRpcMetadata = new Metadata();
                _gRpcMetadata.Add("x-app-name", "OsEngine");
                _gRpcMetadata.Add("Authorization", auth.Token);
            }
            catch (RpcException ex)
            {
                string msg = GetGRPCErrorMessage(ex);
                SendLogMessage($"Error while auth. Info: {msg}", LogMessageType.Error);
                return;
            }
            catch (Exception ex)
            {
                SendLogMessage($"Error while auth. Info: {ex}", LogMessageType.Error);
            }

            SendLogMessage("All streams activated. Connect State", LogMessageType.System);
            ServerStatus = ServerConnectStatus.Connect;
            ConnectEvent?.Invoke();
        }

        private void ReconnectGRPCStreams()
        {
            SendLogMessage("Connecting GRPC streams", LogMessageType.Connect);



            _lastLatestTradesTime = DateTime.UtcNow;
        }


        //private readonly string _gRPCHost = "https://ftrr01.finam.ru:443";
        private readonly string _gRPCHost = "https://api.finam.ru:443"; // https://t.me/finam_trade_api/1/1751
        private Metadata _gRpcMetadata;
        private GrpcChannel _channel;
        private CancellationTokenSource _cancellationTokenSource;
        private WebProxy _proxy;

        //private SubscribeQuote<MarketDataRequest, MarketDataResponse> _marketDataStream;
        private DateTime _lastQuoteTime = DateTime.MinValue;
        private DateTime _lastLatestTradesTime = DateTime.MinValue;
        private DateTime _lastLatestOrdersTime = DateTime.MinValue;
        private DateTime _lastMdTime = DateTime.MinValue;

        private AuthService.AuthServiceClient _authClient;
        private AssetsService.AssetsServiceClient _assetsClient;
        private AccountsService.AccountsServiceClient _accountsClient;
        private OrdersService.OrdersServiceClient _myOrderTradeClient;
        private MarketDataService.MarketDataServiceClient _marketDataClient;
        #endregion

        #region 7 Security subscribe
        public void Subscrible(Security security)
        {
            if (security == null)
            {
                return;
            }

            try
            {
                for (int i = 0; i < _subscribedSecurities.Count; i++)
                {
                    if (_subscribedSecurities[i].Name == security.Name)
                    {
                        return;
                    }
                }

                _subscribedSecurities.Add(security);

                SubscribeQuoteRequest quoteRequest = new SubscribeQuoteRequest();
                for (int i = 0; i < _subscribedSecurities.Count; i++)
                {
                    quoteRequest.Symbols.Add(_subscribedSecurities[i].NameId);
                }
                _rateGateSubscribeSubscribeQuote.WaitToProceed();
                _quoteStream =
                    _marketDataClient.SubscribeQuote(quoteRequest, _gRpcMetadata, null, _cancellationTokenSource.Token);
                //quoteResponse.ResponseStream.ReadAllAsync(); //.ConfigureAwait(false);

                _rateGateSubscribeOrderBook.WaitToProceed();
                // TODO Проверить, что предыдущие подписки активны и данные по ним поступают
                _orderBookStream =
                    _marketDataClient.SubscribeOrderBook(new SubscribeOrderBookRequest { Symbol = security.NameId }, _gRpcMetadata, null, _cancellationTokenSource.Token);
                _latestTradesStream =
                    _marketDataClient.SubscribeLatestTrades(new SubscribeLatestTradesRequest { Symbol = security.NameId }, _gRpcMetadata, null, _cancellationTokenSource.Token);
                // Собственные заявки и сделки
                // Перенести, так как подписка нужна один раз
                _myOrderTradeStream =
                    //_myOrdersClient.SubscribeOrderTrade(new OrderTradeRequest { Action = OrderTradeRequest.Types.Action.Subscribe, AccountId = _accountId, DataType = OrderTradeRequest.Types.DataType.All }, _gRpcMetadata, null, _cancellationTokenSource.Token);
                    _myOrderTradeClient.SubscribeOrderTrade(_gRpcMetadata, null, _cancellationTokenSource.Token);

                // Получаем стакан
                MarketDepth depth = _getMarketDepth(security);
                MarketDepthEvent?.Invoke(depth);

                // Получаем недостающие данные по тикеру
                _updateSecurityParams(security);
            }
            catch (Exception ex)
            {
                SendLogMessage(ex.ToString(), LogMessageType.Error);
            }
        }
        public bool SubscribeNews()
        {
            return false;
        }

        public event Action<News> NewsEvent;

        //private AsyncDuplexStreamingCall<SubscribeQuoteRequest, SubscribeQuoteResponse> _marketDataStream;
        private AsyncServerStreamingCall<SubscribeQuoteResponse> _quoteStream;
        private AsyncServerStreamingCall<SubscribeOrderBookResponse> _orderBookStream;
        private AsyncServerStreamingCall<SubscribeLatestTradesResponse> _latestTradesStream;
        //private AsyncServerStreamingCall<SubscribeLatestTradesResponse> _myOrdersStream;
        private AsyncDuplexStreamingCall<OrderTradeRequest, OrderTradeResponse> _myOrderTradeStream;

        private RateGate _rateGateSubscribeOrderBook = new RateGate(60, TimeSpan.FromMinutes(1));
        private RateGate _rateGateSubscribeLatestTrades = new RateGate(60, TimeSpan.FromMinutes(1));
        private RateGate _rateGateSubscribeSubscribeQuote = new RateGate(60, TimeSpan.FromMinutes(1));
        List<Security> _subscribedSecurities = new List<Security>();
        #endregion

        #region 8 Reading messages from data streams

        private async void TradesMessageReader()
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

                    if (_latestTradesStream == null)
                    {
                        Thread.Sleep(1);
                        continue;
                    }

                    if (await _latestTradesStream.ResponseStream.MoveNext() == false)
                    {
                        Thread.Sleep(1);
                        continue;
                    }

                    SubscribeLatestTradesResponse latestTradesResponse = _latestTradesStream.ResponseStream.Current;

                    if (latestTradesResponse == null)
                    {
                        Thread.Sleep(1);
                        continue;
                    }

                    _lastLatestTradesTime = DateTime.UtcNow;

                    if (latestTradesResponse.Trades != null && latestTradesResponse.Trades.Count > 0)
                    {
                        Security security = GetSecurity(latestTradesResponse.Symbol);
                        if (security == null) { continue; }
                        for (int i = 0; i < latestTradesResponse.Trades.Count; i++)
                        {
                            FTrade newTrade = latestTradesResponse.Trades[i];
                            if (newTrade == null) continue;

                            Trade trade = new Trade();
                            trade.SecurityNameCode = security.Name;
                            //trade.Price = GetValue(newTrade.Price);
                            trade.Price = newTrade.Price.Value.ToString().ToDecimal();
                            trade.Time = newTrade.Timestamp.ToDateTime().AddHours(_timezoneOffset); // convert to MSK
                            trade.Id = newTrade.TradeId;
                            trade.Side = newTrade.Side switch
                            {
                                FSide.Buy => Side.Buy,
                                FSide.Sell => Side.Sell,
                                _ => Side.None
                            };
                            trade.Volume = newTrade.Size.Value.ToString().ToDecimal();

                            NewTradesEvent?.Invoke(trade);
                        }
                    }
                }
                catch (RpcException ex) when (ex.StatusCode == StatusCode.Cancelled)
                {
                    // Handle the cancellation gracefully
                    string message = GetGRPCErrorMessage(ex);
                    SendLogMessage($"Trades stream was cancelled: {message}", LogMessageType.System);
                    Thread.Sleep(5000);
                }
                catch (RpcException exception)
                {
                    SendLogMessage($"Trades stream was disconnected: {exception.Message}", LogMessageType.Error);

                    // need to reconnect everything
                    if (ServerStatus != ServerConnectStatus.Disconnect)
                    {
                        ServerStatus = ServerConnectStatus.Disconnect;
                        DisconnectEvent?.Invoke();
                    }
                    Thread.Sleep(5000);
                }
                catch (Exception exception)
                {
                    SendLogMessage(exception.ToString(), LogMessageType.Error);
                    Thread.Sleep(5000);
                }
            }
        }

        private async void MarketDepthMessageReader()
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

                    if (_orderBookStream == null)
                    {
                        Thread.Sleep(1);
                        continue;
                    }

                    if (await _orderBookStream.ResponseStream.MoveNext() == false)
                    {
                        Thread.Sleep(1);
                        continue;
                    }

                    SubscribeOrderBookResponse latestOrderBookResponse = _orderBookStream.ResponseStream.Current;

                    if (latestOrderBookResponse == null)
                    {
                        Thread.Sleep(1);
                        continue;
                    }

                    _lastLatestOrdersTime = DateTime.UtcNow;

                    if (latestOrderBookResponse.OrderBook != null && latestOrderBookResponse.OrderBook.Count > 0)
                    {
                        for (int j = 0; j < latestOrderBookResponse.OrderBook.Count; j++)
                        {
                            StreamOrderBook ob = latestOrderBookResponse.OrderBook[j];

                            Security security = GetSecurity(ob.Symbol);

                            if (security == null) { continue; }

                            MarketDepth depth = new MarketDepth();
                            depth.SecurityNameCode = security.Name; // TODO Проверить NameId
                            depth.Time = ob.Rows[0].Timestamp.ToDateTime().AddHours(_timezoneOffset);// convert to MSK
                            for (int i = 0; i < ob.Rows.Count; i++)
                            {
                                StreamOrderBook.Types.Row newLevel = ob.Rows[i];

                                if (newLevel.Action == StreamOrderBook.Types.Row.Types.Action.Remove || newLevel.Action == StreamOrderBook.Types.Row.Types.Action.Unspecified) continue;
                                MarketDepthLevel level = new MarketDepthLevel();
                                level.Price = newLevel.Price.Value.ToString().ToDecimal();

                                //if (!string.IsNullOrEmpty(newLevel.BuySize.Value))
                                //{
                                //    level.Bid = newLevel.BuySize.Value.ToString().ToDecimal();
                                //    depth.Bids.Add(level);
                                //}

                                //if (!string.IsNullOrEmpty(newLevel.SellSize.Value))
                                //{
                                //    level.Ask = newLevel.SellSize.Value.ToString().ToDecimal();
                                //    depth.Asks.Add(level);
                                //}

                                if (newLevel.SideCase == StreamOrderBook.Types.Row.SideOneofCase.BuySize)
                                {
                                    level.Bid = newLevel.BuySize.Value.ToString().ToDecimal();
                                    depth.Bids.Add(level);
                                }

                                if (newLevel.SideCase == StreamOrderBook.Types.Row.SideOneofCase.SellSize)
                                {
                                    level.Ask = newLevel.SellSize.Value.ToString().ToDecimal();
                                    depth.Asks.Add(level);
                                }
                            }
                            if (_lastMdTime != DateTime.MinValue &&
                                _lastMdTime >= depth.Time)
                            {
                                depth.Time = _lastMdTime.AddMilliseconds(1);
                            }

                            depth.Asks.Sort((x, y) => x.Price.CompareTo(y.Price));
                            depth.Bids.Sort((y, x) => x.Price.CompareTo(y.Price));

                            _lastMdTime = depth.Time;
                            MarketDepthEvent?.Invoke(depth);

                        }

                    }
                }
                catch (RpcException ex) when (ex.StatusCode == StatusCode.Cancelled)
                {
                    // Handle the cancellation gracefully
                    string message = GetGRPCErrorMessage(ex);
                    SendLogMessage($"Market depth stream was cancelled: {message}", LogMessageType.System);
                    Thread.Sleep(5000);
                }
                catch (RpcException exception)
                {
                    SendLogMessage($"Market depth stream was disconnected: {exception.Message}", LogMessageType.Error);

                    // need to reconnect everything
                    if (ServerStatus != ServerConnectStatus.Disconnect)
                    {
                        ServerStatus = ServerConnectStatus.Disconnect;
                        DisconnectEvent?.Invoke();
                    }
                    Thread.Sleep(5000);
                }
                catch (Exception exception)
                {
                    SendLogMessage(exception.ToString(), LogMessageType.Error);
                    Thread.Sleep(5000);
                }
            }
        }

        private async void MyOrderTradeMessageReader()
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

                    if (_myOrderTradeStream == null)
                    {
                        Thread.Sleep(1);
                        continue;
                    }

                    if (await _myOrderTradeStream.ResponseStream.MoveNext() == false)
                    {
                        Thread.Sleep(1);
                        continue;
                    }

                    OrderTradeResponse latestOrderTradeResponse = _myOrderTradeStream.ResponseStream.Current;

                    if (latestOrderTradeResponse == null)
                    {
                        Thread.Sleep(1);
                        continue;
                    }

                    _lastLatestOrdersTime = DateTime.UtcNow;

                    if (latestOrderTradeResponse.Orders != null && latestOrderTradeResponse.Orders.Count > 0)
                    {
                        for (int j = 0; j < latestOrderTradeResponse.OrderBook.Count; j++)
                        {
                            StreamOrderBook ob = latestOrderTradeResponse.OrderBook[j];

                            Security security = GetSecurity(ob.Symbol);

                            if (security == null) { continue; }

                            MarketDepth depth = new MarketDepth();
                            depth.SecurityNameCode = security.Name; // TODO Проверить NameId
                            depth.Time = ob.Rows[0].Timestamp.ToDateTime().AddHours(_timezoneOffset);// convert to MSK
                            for (int i = 0; i < ob.Rows.Count; i++)
                            {
                                StreamOrderBook.Types.Row newLevel = ob.Rows[i];

                                if (newLevel.Action == StreamOrderBook.Types.Row.Types.Action.Remove || newLevel.Action == StreamOrderBook.Types.Row.Types.Action.Unspecified) continue;
                                MarketDepthLevel level = new MarketDepthLevel();
                                level.Price = newLevel.Price.Value.ToString().ToDecimal();

                                //if (!string.IsNullOrEmpty(newLevel.BuySize.Value))
                                //{
                                //    level.Bid = newLevel.BuySize.Value.ToString().ToDecimal();
                                //    depth.Bids.Add(level);
                                //}

                                //if (!string.IsNullOrEmpty(newLevel.SellSize.Value))
                                //{
                                //    level.Ask = newLevel.SellSize.Value.ToString().ToDecimal();
                                //    depth.Asks.Add(level);
                                //}

                                if (newLevel.SideCase == StreamOrderBook.Types.Row.SideOneofCase.BuySize)
                                {
                                    level.Bid = newLevel.BuySize.Value.ToString().ToDecimal();
                                    depth.Bids.Add(level);
                                }

                                if (newLevel.SideCase == StreamOrderBook.Types.Row.SideOneofCase.SellSize)
                                {
                                    level.Ask = newLevel.SellSize.Value.ToString().ToDecimal();
                                    depth.Asks.Add(level);
                                }
                            }
                            if (_lastMdTime != DateTime.MinValue &&
                                _lastMdTime >= depth.Time)
                            {
                                depth.Time = _lastMdTime.AddMilliseconds(1);
                            }

                            depth.Asks.Sort((x, y) => x.Price.CompareTo(y.Price));
                            depth.Bids.Sort((y, x) => x.Price.CompareTo(y.Price));

                            _lastMdTime = depth.Time;
                            MarketDepthEvent?.Invoke(depth);

                        }

                    }

                    if (latestOrderTradeResponse.Trades != null && latestOrderTradeResponse.Trades.Count > 0)
                    {

                    }
                }
                catch (RpcException ex) when (ex.StatusCode == StatusCode.Cancelled)
                {
                    // Handle the cancellation gracefully
                    string message = GetGRPCErrorMessage(ex);
                    SendLogMessage($"OrderTrade stream was cancelled: {message}", LogMessageType.System);
                    Thread.Sleep(5000);
                }
                catch (RpcException exception)
                {
                    SendLogMessage($"OrderTrade stream was disconnected: {exception.Message}", LogMessageType.Error);

                    // need to reconnect everything
                    if (ServerStatus != ServerConnectStatus.Disconnect)
                    {
                        ServerStatus = ServerConnectStatus.Disconnect;
                        DisconnectEvent?.Invoke();
                    }
                    Thread.Sleep(5000);
                }
                catch (Exception exception)
                {
                    SendLogMessage(exception.ToString(), LogMessageType.Error);
                    Thread.Sleep(5000);
                }
            }
        }

        public event Action<MarketDepth> MarketDepthEvent;
        public event Action<Trade> NewTradesEvent;
        public event Action<MyTrade> MyTradeEvent;
        #endregion

        #region 9 Channel check alive
        private void ConnectionCheckThread()
        {
            while (true)
            {
                Thread.Sleep(50000); // Sleep1

                try
                {
                    if (ServerStatus != ServerConnectStatus.Connect)
                    {
                        continue;
                    }

                    ClockResponse resp = _assetsClient.Clock(new ClockRequest(), _gRpcMetadata);

                    if (resp == null)
                    {
                        Thread.Sleep(3000);
                        continue;
                    }
                    ServerTime = resp.Timestamp.ToDateTime();
                    Thread.Sleep(3000); // Sleep2

                    // Sleep1 + Sleep2 + some overhead
                    // Trigger when twice fail
                    if (_lastTimeCheckConnection.AddSeconds(5) < DateTime.Now && _lastTimeCheckConnection > DateTime.MinValue)
                    {
                        if (ServerStatus == ServerConnectStatus.Connect)
                        {
                            ServerStatus = ServerConnectStatus.Disconnect;
                            DisconnectEvent?.Invoke();
                        }
                    }
                }
                catch (RpcException ex) when (ex.StatusCode == StatusCode.Cancelled)
                {
                    // Handle the cancellation gracefully
                    string message = GetGRPCErrorMessage(ex);
                    SendLogMessage($"Keep Alive stream was cancelled: {message}", LogMessageType.System);
                    Thread.Sleep(5000);
                }
                catch (RpcException ex) when (ex.StatusCode == StatusCode.Internal)
                {
                    if (ex.Status.ToString().Contains("Token is expired"))
                    {
                        string message = GetGRPCErrorMessage(ex);
                        SendLogMessage($"Token is expired: {message}", LogMessageType.System);
                    }
                    if (ServerStatus == ServerConnectStatus.Connect)
                    {
                        ServerStatus = ServerConnectStatus.Disconnect;
                        DisconnectEvent?.Invoke();
                    }
                    Thread.Sleep(1000);
                }
                catch (RpcException ex)
                {
                    string msg = GetGRPCErrorMessage(ex);
                    SendLogMessage($"Error while get time from FinamGrpc. Info: {msg}", LogMessageType.Error);
                    if (ServerStatus == ServerConnectStatus.Connect)
                    {
                        ServerStatus = ServerConnectStatus.Disconnect;
                        DisconnectEvent?.Invoke();
                    }
                    Thread.Sleep(1000);
                }
                catch (Exception error)
                {
                    if (ServerStatus == ServerConnectStatus.Connect)
                    {
                        ServerStatus = ServerConnectStatus.Disconnect;
                        DisconnectEvent?.Invoke();
                    }
                    SendLogMessage(error.ToString(), LogMessageType.Error);
                    Thread.Sleep(1000);
                }
            }
        }

        private DateTime _lastTimeCheckConnection = DateTime.MinValue;
        #endregion

        #region 10 Trade

        public void GetAllActivOrders()
        {
            List<Order> orders = null;

            for (int i = 0; orders != null && i < orders.Count; i++)
            {
                MyOrderEvent?.Invoke(orders[i]);
            }
        }

        public event Action<Order> MyOrderEvent;
        private RateGate _rateGateOrders = new RateGate(100, TimeSpan.FromMinutes(1)); // https://russianinvestments.github.io/investAPI/limits/
        #endregion

        #region 11 Helpers

        private string GetGRPCErrorMessage(RpcException ex)
        {
            return string.Format("{0}: {1}", ex.Status.StatusCode, ex.Status.Detail);
        }

        private Security GetSecurity(string symbol)
        {
            for (int i = 0; i < _securities.Count; i++)
            {
                if (_securities[i].NameId == symbol)
                {
                    return _securities[i];
                }
            }

            return null;
        }

        //public decimal GetValue(GoogleType.Money moneyValue)
        //{
        //    if (moneyValue == null)
        //        return 0.0m;

        //    if (moneyValue.Units == 0 && moneyValue.Nanos == 0)
        //        return 0.0m;

        //    decimal bigDecimal = Convert.ToDecimal(moneyValue.Units);
        //    bigDecimal += Convert.ToDecimal(moneyValue.Nanos) / 10000000; // У Финама nano = 10-7

        //    return bigDecimal;
        //}

        protected TimeSpan getHistoryDepth(FTimeFrame tf)
        {
            return tf switch
            {
                FTimeFrame.M1 => TimeSpan.FromDays(7),
                FTimeFrame.D => TimeSpan.FromDays(365),
                //FTimeFrame.W => TimeSpan.FromDays(365),
                //FTimeFrame.MN => TimeSpan.FromDays(365),
                //FTimeFrame.QR => TimeSpan.FromDays(365),
                _ => TimeSpan.FromDays(30),
            };
        }

        private FTimeFrame CreateTimeFrameInterval(TimeFrame tf)
        {
            return tf switch
            {
                TimeFrame.Min1 => FTimeFrame.M1,
                TimeFrame.Min5 => FTimeFrame.M5,
                TimeFrame.Min15 => FTimeFrame.M15,
                TimeFrame.Min30 => FTimeFrame.M30,
                TimeFrame.Hour1 => FTimeFrame.H1,
                TimeFrame.Hour2 => FTimeFrame.H2,
                TimeFrame.Hour4 => FTimeFrame.H4,
                TimeFrame.Day => FTimeFrame.D,
                _ => FTimeFrame.Unspecified
            };
        }
        #endregion

        #region 12 Log

        private void SendLogMessage(string message, LogMessageType messageType)
        {
            LogMessageEvent?.Invoke(message, messageType);
        }

        public event Action<string, LogMessageType> LogMessageEvent;

        #endregion

        
        public event Action<OptionMarketDataForConnector> AdditionalMarketDataEvent;

        public void CancelAllOrders()
        {
            throw new NotImplementedException();
        }

        public void CancelAllOrdersToSecurity(Security security)
        {
            throw new NotImplementedException();
        }

        public void CancelOrder(Order order)
        {
            throw new NotImplementedException();
        }

        public void ChangeOrderPrice(Order order, decimal newPrice)
        {
            throw new NotImplementedException();
        }

        public void GetOrderStatus(Order order)
        {
            throw new NotImplementedException();
        }

        public List<Trade> GetTickDataToSecurity(Security security, DateTime startTime, DateTime endTime, DateTime actualTime)
        {
            return null; // Недоступно
            LatestTradesResponse resp = null;
            try
            {
                resp = _marketDataClient.LatestTrades(new LatestTradesRequest { Symbol = security.NameId }, _gRpcMetadata);
            }
            catch (RpcException exception)
            {
                SendLogMessage($"Error while getting latest trades: {exception.Message}", LogMessageType.Error);
            }
            catch (Exception exception)
            {
                SendLogMessage(exception.ToString(), LogMessageType.Error);
            }

            if (resp == null || resp.Trades.Count == 0)
            {
                return null;
            }

            List<Trade> trades = new List<Trade>();
            for (int i = 0; i < resp.Trades.Count; i++)
            {
                FTrade fTrade = resp.Trades[i];
                DateTime ts = fTrade.Timestamp.ToDateTime();
                if (ts > startTime && ts < endTime)
                {
                    Trade trade = new Trade();
                    trade.SecurityNameCode = resp.Symbol;
                    //trade.SecurityNameCode = security.NameId;
                    trade.Volume = fTrade.Size.Value.ToDecimal();
                    trade.Price = fTrade.Price.Value.ToDecimal();
                    trade.Time = fTrade.Timestamp.ToDateTime();
                    trade.Id = fTrade.TradeId;
                    trade.Side = fTrade.Side switch
                    {
                        FSide.Buy => Side.Buy,
                        FSide.Sell => Side.Sell,
                        _ => Side.None
                    };

                    trades.Add(trade);
                }
            }

            return trades.Count > 0 ? trades : null;
        }

        public void SendOrder(Order order)
        {
            throw new NotImplementedException();
        }
    }
}
