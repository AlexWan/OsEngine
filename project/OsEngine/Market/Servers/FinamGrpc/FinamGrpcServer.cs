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
using OsEngine.Market.Servers.Entity;
using OsEngine.Market.Servers.Transaq.TransaqEntity;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Candle = OsEngine.Entity.Candle;
using FAsset = Grpc.Tradeapi.V1.Assets.Asset;
using FOrder = Grpc.Tradeapi.V1.Orders.Order;
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
            Thread worker1 = new Thread(ConnectionCheckThread);
            worker1.Name = "CheckAliveFinamGrpc";
            worker1.Start();

            Thread worker2 = new Thread(ReSubscribleThread);
            worker2.Name = "ReIssueTokenThreadFinamGrpc";
            worker2.Start();

            //Thread worker2 = new Thread(MarketDepthMessageReader);
            //worker2.Name = "MarketDepthMessageReaderFinamGrpc";
            //worker2.Start();

            Thread worker3 = new Thread(MyOrderTradeMessageReader);
            worker3.Name = "MyOrderTradeMessageReaderFinamGrpc";
            worker3.Start();

            Thread worker3s = new Thread(MyOrderTradeKeepAlive);
            worker3s.Name = "MyOrderTradeSubscriberFinamGrpc";
            worker3s.Start();
        }

        public void Connect(WebProxy proxy)
        {
            try
            {
                _myPortfolios.Clear();
                _subscribedSecurities.Clear();
                _processedOrders.Clear();
                _processedMyTrades.Clear();

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
                if (ServerStatus != ServerConnectStatus.Connect)
                {
                    ServerStatus = ServerConnectStatus.Connect;
                    ConnectEvent?.Invoke();
                }
            }
            catch (Exception ex)
            {
                SendLogMessage($"Error connecting to server: {ex}", LogMessageType.Error);
                //SetDisconnected();
            }
        }

        public void Dispose()
        {
            try
            {
                DisconnectAllDataStreams();
            }
            catch (Exception ex)
            {
                SendLogMessage($"Error cancelling stream: {ex}", LogMessageType.Error);
            }

            if (_myOrderTradeStream?.RequestStream != null)
            {
                try
                {
                    _myOrderTradeStream.RequestStream.CompleteAsync().Wait();
                }
                catch (ObjectDisposedException)
                {
                    // Already disposed, ignore
                }
                catch (Exception ex)
                {
                    SendLogMessage($"Error cancelling stream: {ex}", LogMessageType.Error);
                }

                SendLogMessage("Completed exchange with my orders and trades stream", LogMessageType.System);
                _myOrderTradeStream = null;
            }

            SendLogMessage("Completed exchange with data streams (orderbook and trades)", LogMessageType.System);

            _channel?.Dispose();
            _channel = null;

            _subscribedSecurities.Clear();
            _myPortfolios.Clear();
            _dicOrderBookStreams.Clear();
            _dicLatestTradesStreams.Clear();
            _dicLastMdTime.Clear();
            _processedOrders.Clear();
            _processedMyTrades.Clear();

            SendLogMessage("Connection to Finam gRPC closed. Data streams Closed Event", LogMessageType.System);

            SetDisconnected();
        }

        public List<IServerParameter> ServerParameters { get; set; }
        public event Action ConnectEvent;
        public event Action DisconnectEvent;

        public DateTime ServerTime { get; set; }
        public ServerConnectStatus ServerStatus { get; set; } = ServerConnectStatus.Disconnect;
        #endregion

        #region 2 Properties
        public ServerType ServerType => ServerType.FinamGrpc;

        private string _accessToken;
        private string _accountId;

        private int _timezoneOffset = 3;

        // Срок жизни JWT токена 15 минту
        private TimeSpan _jwtTokenLifetime = new TimeSpan(0, 0, 15, 0);

        //private readonly string _gRPCHost = "https://ftrr01.finam.ru:443";
        private readonly string _gRPCHost = "https://api.finam.ru:443"; // https://t.me/finam_trade_api/1/1751
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

            UpdateSecuritiesFromServer(assetsResponse);

            if (_securities.Count > 0)
            {
                SendLogMessage("Securities loaded. Count: " + _securities.Count, LogMessageType.System);

                SecurityEvent?.Invoke(_securities);
            }
        }

        private void UpdateSecuritiesFromServer(AssetsResponse assetsResponse)
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
                    FAsset item = assetsResponse.Assets[i];

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

                    // Если нет соответствия типу OSEngine - пропускаем тикер
                    if (newSecurity.SecurityType == SecurityType.None) continue;

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

        private RateGate _rateGateAssetsAsset = new RateGate(200, TimeSpan.FromMinutes(1));
        #endregion

        #region 4 Portfolios
        public void GetPortfolios()
        {
            GetAccountResponse getAccountResponse = null;
            _rateGateAccountsGetAccount.WaitToProceed();
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
                myPortfolio.ValueCurrent = Math.Truncate(getAccountResponse.Equity.Value.ToDecimal() * 100m) / 100m;
                myPortfolio.ValueBegin = myPortfolio.ValueCurrent;
                myPortfolio.UnrealizedPnl = getAccountResponse.UnrealizedProfit.Value.ToDecimal();
                _myPortfolios.Add(myPortfolio);
            }
            else
            {
                myPortfolio.ValueCurrent = Math.Truncate(getAccountResponse.Equity.Value.ToDecimal() * 100m) / 100m;
                myPortfolio.UnrealizedPnl = getAccountResponse.UnrealizedProfit.Value.ToDecimal();
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
                    newPos.SecurityNameCode = pos.Symbol;
                    newPos.ValueCurrent = pos.Quantity.Value.ToDecimal();
                    //newPos.ValueCurrent = pos.Quantity.Value.ToDecimal() * pos.CurrentPrice.Value.ToDecimal();
                    //newPos.ValueBlocked = pos.Blocked / instrument.Instrument.Lot;
                    newPos.ValueBegin = pos.Quantity.Value.ToDecimal();
                    //newPos.ValueBegin = pos.Quantity.Value.ToDecimal() * pos.AveragePrice.Value.ToDecimal();

                    myPortfolio.SetNewPosition(newPos);
                }

        }

        private RateGate _rateGateAccountsGetAccount = new RateGate(200, TimeSpan.FromMinutes(1));

        public event Action<List<Portfolio>> PortfolioEvent;

        private List<Portfolio> _myPortfolios = new List<Portfolio>();
        #endregion

        #region 5 Data
        public List<Candle> GetLastCandleHistory(Security security, TimeFrameBuilder timeFrameBuilder, int candleCount)
        {
            DateTime timeStart = DateTime.UtcNow - TimeSpan.FromMinutes(timeFrameBuilder.TimeFrameTimeSpan.TotalMinutes * candleCount);
            DateTime timeEnd = DateTime.UtcNow;

            List<Candle> candles = GetCandleDataToSecurity(security, timeFrameBuilder, timeStart, timeEnd, timeStart);

            return candles;
        }

        public List<Candle> GetCandleDataToSecurity(Security security, TimeFrameBuilder timeFrameBuilder, DateTime startTime, DateTime endTime,
            DateTime actualTime)
        {
            if (startTime != actualTime)
            {
                startTime = actualTime;
            }

            FTimeFrame ftf = CreateTimeFrameInterval(timeFrameBuilder.TimeFrame);
            if (ftf == FTimeFrame.Unspecified) return null;

            List<Candle> candles = new List<Candle>();

            TimeSpan tsHistoryDepth = getHistoryDepth(ftf);
            DateTime queryStartTime = startTime;

            while (queryStartTime < endTime)
            {
                DateTime queryEndTime = queryStartTime.Add(tsHistoryDepth);
                // Не заказываем лишних данных
                if (queryEndTime > endTime)
                    queryEndTime = endTime;


                List<Candle> range = GetCandleHistoryFromServer(queryStartTime, queryEndTime, security, ftf);

                // Если запрошен некорректный таймфрейм, то возвращает null
                //if (range == null) return null;
                if (range != null && range.Count > 0)
                {
                    candles.AddRange(range);
                }

                queryStartTime = queryEndTime;
            }

            while (candles != null &&
                candles.Count != 0 &&
                candles[candles.Count - 1].TimeStart > endTime)
            {
                candles.RemoveAt(candles.Count - 1);
            }

            while (candles != null &&
                candles.Count != 0 &&
                candles[0].TimeStart < startTime)
            {
                candles.RemoveAt(0);
            }

            return candles.Count == 0 ? null : candles;
        }

        private List<Candle> GetCandleHistoryFromServer(DateTime fromDateTime, DateTime toDateTime, Security security, FTimeFrame ftf)
        {
            if (ftf == FTimeFrame.Unspecified) return null;

            if (toDateTime < fromDateTime) return null;

            BarsResponse resp = null;

            try
            {
                // convert all times to UTC
                DateTime fromDateTimeUtc = DateTime.SpecifyKind(fromDateTime.AddHours(-_timezoneOffset).Date, DateTimeKind.Utc); // MSK -> UTC
                DateTime toDateTimeUtc = DateTime.SpecifyKind(toDateTime.AddHours(-_timezoneOffset).AddDays(1).Date, DateTimeKind.Utc);

                BarsRequest req = new BarsRequest
                {
                    Symbol = security.NameId,
                    Timeframe = ftf,
                    Interval = new Google.Type.Interval { StartTime = Timestamp.FromDateTime(fromDateTimeUtc), EndTime = Timestamp.FromDateTime(toDateTimeUtc) }
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

            List<Candle> candles = ConvertToOsEngineCandles(resp);

            while (candles != null &&
                candles.Count != 0 &&
                candles[candles.Count - 1].TimeStart > toDateTime)
            {
                candles.RemoveAt(candles.Count - 1);
            }

            while (candles != null &&
                candles.Count != 0 &&
                candles[0].TimeStart < fromDateTime)
            {
                candles.RemoveAt(0);
            }

            return candles;
        }

        private List<Candle> ConvertToOsEngineCandles(BarsResponse response)
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

        private void UpdateSecurityParams(Security security)
        {
            try
            {
                _rateGateAssetsGetAsset.WaitToProceed();
                GetAssetResponse getAssetResponse = _assetsClient.GetAsset(
                    new GetAssetRequest { AccountId = _accountId, Symbol = security.NameId },
                    headers: _gRpcMetadata
                    );

                if (getAssetResponse != null)
                {
                    security.Lot = getAssetResponse.LotSize.Value.ToDecimal();
                    security.Decimals = (int)getAssetResponse.Decimals;
                    security.PriceStep = security.Decimals.GetValueByDecimals();
                    security.PriceStepCost = security.PriceStep;
                    if (getAssetResponse.ExpirationDate != null)
                    {
                        security.Expiration = getAssetResponse.ExpirationDate.ToDateTime().AddHours(_timezoneOffset); // convert to MSK
                    }
                }

                GetAssetParamsResponse getAssetParamsResponse = _assetsClient.GetAssetParams(
                    new GetAssetParamsRequest { AccountId = _accountId, Symbol = security.NameId },
                    headers: _gRpcMetadata
                    );

                if (getAssetParamsResponse != null)
                {
                    security.State = getAssetParamsResponse.Tradeable ? SecurityStateType.Activ : SecurityStateType.Close;
                }
            }
            catch (RpcException ex)
            {
                string message = GetGRPCErrorMessage(ex);
                SendLogMessage($"Error loading security [{security.NameId}] params. Info: {message}", LogMessageType.Error);
            }
            catch (Exception ex)
            {
                SendLogMessage($"Error loading security [{security.NameId}] params: {ex}", LogMessageType.Error);
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

        private MarketDepth GetMarketDepth(Security security)
        {
            MarketDepth depth = new MarketDepth();
            depth.SecurityNameCode = security.Name;

            try
            {
                _rateGateMarketDataOrderBook.WaitToProceed();
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

                depth.Asks.Sort((x, y) => x.Price.CompareTo(y.Price));
                depth.Bids.Sort((y, x) => x.Price.CompareTo(y.Price));

                if (_dicLastMdTime[security.NameId] >= depth.Time)
                {
                    depth.Time = _dicLastMdTime[security.NameId].AddMilliseconds(1);
                }
                _dicLastMdTime[security.NameId] = depth.Time;
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
                    trade.Side = GetSide(fTrade.Side);

                    trades.Add(trade);
                }
            }

            return trades.Count > 0 ? trades : null;
        }

        private RateGate _rateGateAssetsGetAsset = new RateGate(200, TimeSpan.FromMinutes(1));
        private RateGate _rateGateMarketDataOrderBook = new RateGate(200, TimeSpan.FromMinutes(1));
        private RateGate _rateGateMarketDataBars = new RateGate(200, TimeSpan.FromMinutes(1));
        #endregion

        #region 6 gRPC streams creation
        private void CreateStreamsConnection()
        {
            _cancellationTokenSource = new CancellationTokenSource();

            HttpClient httpClient = new HttpClient(new HttpClientHandler
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
                updateAuth(_authClient);

                // Подписка один раз
                _rateGateMyOrderTradeSubscribeOrderTrade.WaitToProceed();
                _myOrderTradeStream = _myOrderTradeClient.SubscribeOrderTrade(_gRpcMetadata, null, _cancellationTokenSource.Token);
            }
            catch (RpcException ex)
            {
                string msg = GetGRPCErrorMessage(ex);
                SendLogMessage($"gRPC Error while auth. Info: {msg}", LogMessageType.Error);
                return;
            }
            catch (Exception ex)
            {
                SendLogMessage($"Error while auth. Info: {ex}", LogMessageType.Error);
            }

            SendLogMessage("All streams activated. Connect State", LogMessageType.System);
        }

        private void updateAuth(AuthService.AuthServiceClient client)
        {
            _rateGateAuth.WaitToProceed();
            // Получаем gwt токен
            AuthResponse auth = client.Auth(new AuthRequest { Secret = _accessToken });
            if (auth?.Token == null)
            {
                SendLogMessage("Authentication Error. Probably an invalid token is specified. You can see it on the Finam website.",
                    LogMessageType.Error);
                return;
            }

            _gRpcMetadata = new Metadata();
            _gRpcMetadata.Add("x-app-name", "OsEngine");
            _gRpcMetadata.Add("Authorization", auth.Token);
        }


        private Metadata _gRpcMetadata;
        private GrpcChannel _channel;
        private CancellationTokenSource _cancellationTokenSource;
        private WebProxy _proxy;

        private Dictionary<string, OrderBookStreamReaderInfo> _dicOrderBookStreams = new Dictionary<string, OrderBookStreamReaderInfo>();
        private Dictionary<string, TradesStreamReaderInfo> _dicLatestTradesStreams = new Dictionary<string, TradesStreamReaderInfo>();
        private AsyncDuplexStreamingCall<OrderTradeRequest, OrderTradeResponse> _myOrderTradeStream;

        private Dictionary<string, DateTime> _dicLastMdTime = new Dictionary<string, DateTime>();

        private AuthService.AuthServiceClient _authClient;
        private AssetsService.AssetsServiceClient _assetsClient;
        private AccountsService.AccountsServiceClient _accountsClient;
        private OrdersService.OrdersServiceClient _myOrderTradeClient;
        private MarketDataService.MarketDataServiceClient _marketDataClient;

        private RateGate _rateGateAuth = new RateGate(200, TimeSpan.FromMinutes(1));
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

                // Получаем недостающие данные по тикеру
                UpdateSecurityParams(security);

                // Отменяем подписку, если инструмент не торгуется
                if (security.State != SecurityStateType.Activ)
                {
                    SendLogMessage($"Error subscribe security {security.Name}: IS NOT TRADEABLE. Subscription skipped.", LogMessageType.Error);
                    return;
                }

                _subscribedSecurities.Add(security);
                StartLatestTradesStream(security);
                StartOrderBookStream(security);

                // Получаем начальный стакан
                _dicLastMdTime.TryAdd(security.NameId, DateTime.UtcNow.AddHours(_timezoneOffset));
                MarketDepth depth = GetMarketDepth(security);
                if ((depth.Bids != null && depth.Bids.Count > 0) || (depth.Asks != null && depth.Asks.Count > 0))
                {
                    MarketDepthEvent?.Invoke(depth);
                }

            }
            catch (RpcException ex)
            {
                string message = GetGRPCErrorMessage(ex);
                SendLogMessage($"Error subscribe security {security.Name}. Info: {message}", LogMessageType.Error);
            }
            catch (Exception ex)
            {
                SendLogMessage(ex.ToString(), LogMessageType.Error);
            }
        }

        public void ReSubscrible(Security security)
        {
            if (security == null)
            {
                return;
            }

            try
            {
                ReconnectLatestTradesStream(security);
                ReconnectOrderBookStream(security);
            }
            catch (RpcException ex)
            {
                string message = GetGRPCErrorMessage(ex);
                SendLogMessage($"Error subscribe security {security.Name}. Info: {message}", LogMessageType.Error);
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

        List<Security> _subscribedSecurities = new List<Security>();

        //private AsyncServerStreamingCall<SubscribeOrderBookResponse> _orderBookStream;
        private RateGate _rateGateMarketDataSubscribeOrderBook = new RateGate(200, TimeSpan.FromMinutes(1));
        private RateGate _rateGateMarketDataSubscribeLatestTrades = new RateGate(200, TimeSpan.FromMinutes(1));
        private RateGate _rateGateMyOrderTradeSubscribeOrderTrade = new RateGate(200, TimeSpan.FromMinutes(1));
        #endregion

        #region 8 Reading messages from data streams

        // Запуск reader для конкретного инструмента
        private void StartLatestTradesStream(Security security)
        {
            if (_dicLatestTradesStreams.ContainsKey(security.NameId))
            {
                // Уже есть reader, не запускаем второй
                return;
            }

            //SendLogMessage($"[DEBUG] StartLatestTradesStream called for {security.NameId}", LogMessageType.Error);

            TradesStreamReaderInfo streamReaderInfo = new TradesStreamReaderInfo();
            // Свой CTS токен для каждого потока
            CancellationTokenSource cts = CancellationTokenSource.CreateLinkedTokenSource(_cancellationTokenSource.Token);
            streamReaderInfo.CancellationTokenSource = cts;

            _rateGateMarketDataSubscribeLatestTrades.WaitToProceed();
            streamReaderInfo.Stream = _marketDataClient.SubscribeLatestTrades(new SubscribeLatestTradesRequest { Symbol = security.NameId }, _gRpcMetadata, null, cts.Token);
            streamReaderInfo.ReaderTask = Task.Run(() => SingleTradesMessageReader(streamReaderInfo.Stream, cts.Token, security), cts.Token);

            _dicLatestTradesStreams[security.NameId] = streamReaderInfo;
        }

        private void StartOrderBookStream(Security security)
        {
            if (_dicOrderBookStreams.ContainsKey(security.NameId))
            {
                // Уже есть reader, не запускаем второй
                return;
            }

            //SendLogMessage($"[DEBUG] StartOrderBookStream called for {security.NameId}", LogMessageType.Error);

            OrderBookStreamReaderInfo streamReaderInfo = new OrderBookStreamReaderInfo();
            // Свой CTS токен для каждого потока
            CancellationTokenSource cts = CancellationTokenSource.CreateLinkedTokenSource(_cancellationTokenSource.Token);
            streamReaderInfo.CancellationTokenSource = cts;

            _rateGateMarketDataSubscribeOrderBook.WaitToProceed();
            streamReaderInfo.Stream = _marketDataClient.SubscribeOrderBook(new SubscribeOrderBookRequest { Symbol = security.NameId }, _gRpcMetadata, null, cts.Token);
            streamReaderInfo.ReaderTask = Task.Run(() => SingleMarketDepthReader(streamReaderInfo.Stream, cts.Token, security), cts.Token);

            _dicOrderBookStreams[security.NameId] = streamReaderInfo;
        }

        // Переподключение reader для конкретного инструмента
        private void ReconnectLatestTradesStream(Security security)
        {
            DisconnectLatestTradesStream(security);
            // Запускаем новый ридер со своим новым токеном
            StartLatestTradesStream(security);
        }

        private void DisconnectLatestTradesStream(Security security)
        {
            if (_dicLatestTradesStreams.TryGetValue(security.NameId, out TradesStreamReaderInfo info))
            {
                // Отменяем токен только для этого ридера
                info.CancellationTokenSource.Cancel();
                try { info.ReaderTask.Wait(1000); } catch { }
                if (info.Stream != null) info.Stream.Dispose();
                _dicLatestTradesStreams.Remove(security.NameId);
            }
        }

        private void ReconnectOrderBookStream(Security security)
        {
            DisconnectOrderBookStream(security);
            // Запускаем новый ридер со своим новым токеном
            StartOrderBookStream(security);
        }

        private void DisconnectOrderBookStream(Security security)
        {
            if (_dicOrderBookStreams.TryGetValue(security.NameId, out OrderBookStreamReaderInfo info))
            {
                // Отменяем токен только для этого ридера
                info.CancellationTokenSource.Cancel();
                try { info.ReaderTask.Wait(1000); } catch { }
                if (info.Stream != null) info.Stream.Dispose();
                _dicLatestTradesStreams.Remove(security.NameId);
            }
        }

        // Переподключение всех reader-ов
        private void ReconnectAllDataStreams()
        {
            //var securities = _subscribedSecurities.ToArray();
            foreach (Security sec in _subscribedSecurities)
            {
                ReconnectLatestTradesStream(sec);
                ReconnectOrderBookStream(sec);
            }
        }

        private void DisconnectAllDataStreams()
        {
            foreach (Security sec in _subscribedSecurities)
            {
                DisconnectLatestTradesStream(sec);
                DisconnectOrderBookStream(sec);
            }
        }

        // Reader для стрима
        private async Task SingleTradesMessageReader(AsyncServerStreamingCall<SubscribeLatestTradesResponse> stream, CancellationToken token, Security security)
        {
            //SendLogMessage($"[DEBUG] Start trades reader for {security.NameId}", LogMessageType.System);
            await Task.Delay(1000, token);

            try
            {
                while (!token.IsCancellationRequested)
                {
                    if (ServerStatus == ServerConnectStatus.Disconnect)
                    {
                        await Task.Delay(5, token);
                        continue;
                    }

                    bool hasData = false;
                    try
                    {
                        //SendLogMessage($"[DEBUG] MoveNext called for {security.NameId}", LogMessageType.Error);
                        hasData = await stream.ResponseStream.MoveNext();
                    }
                    catch (Exception ex)
                    {
                        //SendLogMessage($"[DEBUG] Exception in trades stream for {security.NameId}: {ex}", LogMessageType.Error);
                        await Task.Delay(5, token);
                    }

                    if (!hasData)
                    {
                        //SendLogMessage($"[DEBUG] Trades stream closed by server for {security.NameId}. Reconnect stream.", LogMessageType.Error);
                        ReconnectLatestTradesStream(security);
                        await Task.Delay(5, token);
                        continue;
                        //break;
                    }

                    SubscribeLatestTradesResponse latestTradesResponse = stream.ResponseStream.Current;
                    if (latestTradesResponse == null)
                    {
                        await Task.Delay(1, token);
                        continue;
                    }

                    if (security.NameId != latestTradesResponse.Symbol)
                    {
                        SendLogMessage($"Trades stream. Expected trade for {security.NameId}, got for {latestTradesResponse.Symbol}.", LogMessageType.System);
                        continue;
                    }

                    if (latestTradesResponse.Trades != null && latestTradesResponse.Trades.Count > 0)
                    {
                        for (int i = 0; i < latestTradesResponse.Trades.Count; i++)
                        {
                            FTrade newTrade = latestTradesResponse.Trades[i];
                            if (newTrade == null) continue;
                            Trade trade = new Trade();
                            trade.SecurityNameCode = security.Name;
                            trade.Price = newTrade.Price.Value.ToString().ToDecimal();
                            trade.Time = newTrade.Timestamp.ToDateTime().AddHours(_timezoneOffset);
                            trade.Id = newTrade.TradeId;
                            trade.Side = GetSide(newTrade.Side);
                            trade.Volume = newTrade.Size.Value.ToString().ToDecimal();
                            NewTradesEvent?.Invoke(trade);
                        }
                        //SendLogMessage($"[DEBUG] Received trades for {security.NameId}: {latestTradesResponse.Trades?.Count ?? 0}", LogMessageType.Error);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                //SendLogMessage($"[DEBUG] Reader for {security.NameId} cancelled", LogMessageType.Error); 
            }
            catch (Exception ex)
            {
                //SendLogMessage($"[DEBUG] Reader for {security.NameId} exception: {ex}", LogMessageType.Error); 
            }
            finally
            {
                //SendLogMessage($"[DEBUG] Reader for {security.NameId} finished", LogMessageType.Error);
            }
        }

        private async Task SingleMarketDepthReader(AsyncServerStreamingCall<SubscribeOrderBookResponse> stream, CancellationToken token, Security security)
        {
            //SendLogMessage($"[DEBUG] Start MarketDepth reader for {security.NameId}", LogMessageType.Error);
            await Task.Delay(1000, token);

            try
            {
                while (!token.IsCancellationRequested)
                {
                    if (ServerStatus == ServerConnectStatus.Disconnect)
                    {
                        await Task.Delay(5, token);
                        continue;
                    }

                    bool hasData = false;
                    try
                    {
                        //SendLogMessage($"[DEBUG] MarketDepth MoveNext called for {security.NameId}", LogMessageType.Error);
                        hasData = await stream.ResponseStream.MoveNext(token);
                    }
                    catch (Exception ex)
                    {
                        //SendLogMessage($"[DEBUG] Exception in MarketDepth reader for {security.NameId}: {ex}", LogMessageType.Error);
                        await Task.Delay(5, token);
                        continue;
                        //break;
                    }

                    if (!hasData)
                    {
                        //SendLogMessage($"[DEBUG] MarketDepth stream closed by server for {security.NameId}. Reconnect stream.", LogMessageType.Error);
                        ReconnectOrderBookStream(security);
                        await Task.Delay(5, token);
                        continue;
                        //break;
                    }

                    SubscribeOrderBookResponse latestOrderBookResponse = stream.ResponseStream.Current;
                    if (latestOrderBookResponse == null)
                    {
                        await Task.Delay(1, token);
                        continue;
                    }

                    _dicLastMdTime[security.NameId] = DateTime.UtcNow.AddHours(_timezoneOffset);

                    if (latestOrderBookResponse.OrderBook != null && latestOrderBookResponse.OrderBook.Count > 0)
                    {
                        foreach (StreamOrderBook ob in latestOrderBookResponse.OrderBook)
                        {
                            if (security.NameId != ob.Symbol)
                            {
                                SendLogMessage($"MarketDepth stream. Expected Market Depth Level for {security.NameId}, got for {ob.Symbol}.", LogMessageType.Error);
                                continue;
                            }

                            MarketDepth depth = new MarketDepth
                            {
                                SecurityNameCode = security.Name,
                                Time = ob.Rows[0].Timestamp.ToDateTime().AddHours(_timezoneOffset)
                            };

                            foreach (StreamOrderBook.Types.Row newLevel in ob.Rows)
                            {
                                if (newLevel.Action == StreamOrderBook.Types.Row.Types.Action.Remove || newLevel.Action == StreamOrderBook.Types.Row.Types.Action.Unspecified) continue;
                                MarketDepthLevel level = new MarketDepthLevel
                                {
                                    Price = newLevel.Price.Value.ToString().ToDecimal()
                                };

                                if (newLevel.SideCase == StreamOrderBook.Types.Row.SideOneofCase.BuySize)
                                {
                                    level.Bid = newLevel.BuySize.Value.ToString().ToDecimal();
                                    depth.Bids.Add(level);
                                }
                                else if (newLevel.SideCase == StreamOrderBook.Types.Row.SideOneofCase.SellSize)
                                {
                                    level.Ask = newLevel.SellSize.Value.ToString().ToDecimal();
                                    depth.Asks.Add(level);
                                }
                            }


                            depth.Asks.Sort((x, y) => x.Price.CompareTo(y.Price));
                            depth.Bids.Sort((y, x) => x.Price.CompareTo(y.Price));

                            if (_dicLastMdTime[security.NameId] >= depth.Time)
                            {
                                depth.Time = _dicLastMdTime[security.NameId].AddMilliseconds(1);
                            }
                            _dicLastMdTime[security.NameId] = depth.Time;

                            if ((depth.Bids != null && depth.Bids.Count > 0) || (depth.Asks != null && depth.Asks.Count > 0))
                            {
                                MarketDepthEvent?.Invoke(depth);
                            }
                        }
                    }
                }
            }
            catch (OperationCanceledException)
            {
                //SendLogMessage($"[DEBUG] MarketDepth reader for {security.NameId} cancelled", LogMessageType.Error); 
            }
            catch (Exception ex)
            {
                //SendLogMessage($"[DEBUG] MarketDepth reader for {security.NameId} exception: {ex}", LogMessageType.Error); 
            }
            finally
            {
                //SendLogMessage($"[DEBUG] MarketDepth reader for {security.NameId} finished", LogMessageType.Error);
            }
        }

        private async void MyOrderTradeKeepAlive()
        {
            // Собственные заявки и сделки
            // Повторящаяся подписка, так как нет пинга и поток отваливается без реанимации
            // Пример для duplex stream
            while (_cancellationTokenSource == null)
            {
                await Task.Delay(TimeSpan.FromSeconds(5));
            }

            while (!_cancellationTokenSource.IsCancellationRequested)
            {
                await Task.Delay(TimeSpan.FromSeconds(30), _cancellationTokenSource.Token);
                try
                {
                    if (_myOrderTradeStream != null)
                    {
                        await _myOrderTradeStream.RequestStream.WriteAsync(new OrderTradeRequest { AccountId = _accountId, Action = OrderTradeRequest.Types.Action.Subscribe, DataType = OrderTradeRequest.Types.DataType.All });
                    }
                }
                catch (Exception ex)
                {
                    SendLogMessage($"MyOrderTrade keepalive failed: {ex}", LogMessageType.Error);
                    break;
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

                    OrderTradeResponse myOrderTradeResponse = _myOrderTradeStream.ResponseStream.Current;

                    if (myOrderTradeResponse == null)
                    {
                        Thread.Sleep(1);
                        continue;
                    }

                    if (myOrderTradeResponse.Orders != null && myOrderTradeResponse.Orders.Count > 0)
                    {
                        for (int j = 0; j < myOrderTradeResponse.Orders.Count; j++)
                        {
                            OrderState myOrder = myOrderTradeResponse.Orders[j];

                            Order order = ConvertToOSEngineOrder(myOrder);

                            //if (order == null) continue;

                            InvokeMyOrderEvent(order);
                            //MyOrderEvent?.Invoke(order);
                        }
                    }

                    if (myOrderTradeResponse.Trades != null && myOrderTradeResponse.Trades.Count > 0)
                    {
                        for (int j = 0; j < myOrderTradeResponse.Trades.Count; j++)
                        {
                            AccountTrade myTrade = myOrderTradeResponse.Trades[j];

                            MyTrade trade = ConvertToOSEngineTrade(myTrade);

                            //if (trade == null) continue;

                            //MyTradeEvent?.Invoke(trade);
                            InvokeMyTradeEvent(trade);
                        }
                    }
                }
                catch (RpcException ex) when (ex.StatusCode == StatusCode.Cancelled)
                {
                    // Handle the cancellation gracefully
                    //string message = GetGRPCErrorMessage(ex);
                    //SendLogMessage($"OrderTrade stream was cancelled: {message}", LogMessageType.System);
                    Thread.Sleep(5000);
                }
                catch (RpcException exception)
                {
                    SendLogMessage($"OrderTrade stream was disconnected: {exception.Message}", LogMessageType.Error);

                    // need to reconnect everything
                    //SetDisconnected();
                    Thread.Sleep(5000);
                }
                catch (Exception exception)
                {
                    SendLogMessage(exception.ToString(), LogMessageType.Error);
                    Thread.Sleep(5000);
                }
            }
        }

        private MyTrade ConvertToOSEngineTrade(AccountTrade myTrade)
        {
            MyTrade trade = new MyTrade();

            trade.Volume = myTrade.Size.Value.ToDecimal();
            trade.Price = myTrade.Price.Value.ToDecimal();
            trade.Side = GetSide(myTrade.Side);
            trade.NumberTrade = myTrade.TradeId;
            trade.NumberOrderParent = myTrade.OrderId;
            trade.SecurityNameCode = myTrade.Symbol; // TODO Баг АПИ (Не содержит названия биржи) "Symbol": "VTBR@MISX" - должно быть, есть "Symbol": "VTBR"
            trade.Time = myTrade.Timestamp.ToDateTime();
            return trade;
        }

        private Order ConvertToOSEngineOrder(OrderState orderState)
        {
            if (orderState == null || orderState.Order == null) return null;

            Order myOrder = new Order();
            FOrder fOrder = orderState.Order;
            if (int.TryParse(fOrder.ClientOrderId, out int numUser))
            {
                myOrder.NumberUser = numUser;
                //return null;
            }
            // Not OS Engine order
            if (myOrder.NumberUser == 0) return null;

            myOrder.PortfolioNumber = fOrder.AccountId;
            myOrder.NumberMarket = orderState.OrderId;
            myOrder.TimeCallBack = orderState.TransactAt.ToDateTime(); // TODO Проверить
            myOrder.TimeCreate = orderState.TransactAt.ToDateTime();  // TODO Проверить
            if (fOrder.LimitPrice != null)
            {
                myOrder.Price = fOrder.LimitPrice.Value.ToDecimal();
            }
            myOrder.Volume = fOrder.Quantity.Value.ToDecimal();
            //myOrder.ServerType = ServerType;
            myOrder.TypeOrder = fOrder.Type switch
            {
                OrderType.Market => OrderPriceType.Market,
                OrderType.Limit => OrderPriceType.Limit,
                _ => throw new Exception("Order type is not supported")
            };

            Security security = GetSecurity(fOrder.Symbol);
            myOrder.SecurityNameCode = security?.Name ?? fOrder.Symbol;
            myOrder.SecurityClassCode = security.NameClass;
            myOrder.Side = GetSide(fOrder.Side);
            myOrder.State = GetOrderStateType(orderState.Status);

            if (myOrder.State == OrderStateType.Cancel)
            {
                myOrder.TimeCancel = orderState.TransactAt.ToDateTime(); // TODO Описание в документации не соответствует названию параметра. Уточнить.
            }

            if (myOrder.State == OrderStateType.Done)
            {
                myOrder.TimeDone = orderState.TransactAt.ToDateTime();
            }

            //if (order.TimeInForce == TimeInForce.Day)
            //{
            //    myOrder.OrderTypeTime = OrderTypeTime.Day;
            //}

            return myOrder;
        }

        public event Action<OptionMarketDataForConnector> AdditionalMarketDataEvent;

        public event Action<MarketDepth> MarketDepthEvent;
        public event Action<Trade> NewTradesEvent;
        public event Action<MyTrade> MyTradeEvent;
        #endregion

        #region 9 Channel check alive
        private void ConnectionCheckThread()
        {
            while (true)
            {
                Thread.Sleep(50000);

                try
                {
                    if (ServerStatus != ServerConnectStatus.Connect)
                    {
                        continue;
                    }

                    ClockResponse resp = _assetsClient.Clock(new ClockRequest(), _gRpcMetadata);

                    if (resp == null)
                    {
                        SetDisconnected();
                        Thread.Sleep(3000);
                        continue;
                    }
                    ServerTime = resp.Timestamp.ToDateTime();
                    Thread.Sleep(3000); // Sleep2
                }
                catch (RpcException ex) when (ex.StatusCode == StatusCode.Cancelled)
                {
                    // Handle the cancellation gracefully
                    //string message = GetGRPCErrorMessage(ex);
                    //SendLogMessage($"Keep Alive stream was cancelled: {message}", LogMessageType.System);
                    Thread.Sleep(5000);
                }
                catch (RpcException ex) when (ex.StatusCode == StatusCode.Internal)
                {
                    string message = GetGRPCErrorMessage(ex);
                    if (ex.Status.ToString().Contains("Token is expired"))
                    {
                        //string message = GetGRPCErrorMessage(ex);
                        SendLogMessage($"Token is expired: {message}", LogMessageType.Error);
                        // TODO RECONNECT!!!
                    }
                    else
                    {
                        SendLogMessage($"Keep Alive stream error: {message}", LogMessageType.Error);
                    }
                    //SetDisconnected();
                    Thread.Sleep(1000);
                }
                catch (RpcException ex)
                {
                    string msg = GetGRPCErrorMessage(ex);
                    SendLogMessage($"Error while get time from FinamGrpc. Info: {msg}", LogMessageType.Error);
                    //SetDisconnected();
                    Thread.Sleep(1000);
                }
                catch (Exception error)
                {
                    //SetDisconnected();
                    SendLogMessage(error.ToString(), LogMessageType.Error);
                    Thread.Sleep(1000);
                }
            }
        }

        /// <summary>
        /// Срок жизни JWT-токена 15 минут
        /// </summary>
        private void ReSubscribleThread()
        {
            int ms = Convert.ToInt32(_jwtTokenLifetime.TotalMilliseconds) - 60000;
            while (true)
            {
                Thread.Sleep(ms);
                updateAuth(_authClient);

                _rateGateMyOrderTradeSubscribeOrderTrade.WaitToProceed();
                _myOrderTradeStream = _myOrderTradeClient.SubscribeOrderTrade(_gRpcMetadata, null, _cancellationTokenSource.Token);

                for (int i = 0; i < _subscribedSecurities.Count - 1; i++)
                {
                    ReSubscrible(_subscribedSecurities[i]);
                }
            }
        }
        #endregion

        #region 10 Trade
        public void SendOrder(Order order)
        {
            //Security security = GetSecurity(order.SecurityNameCode);
            FOrder fOrder = new FOrder();
            fOrder.AccountId = _accountId;
            fOrder.Symbol = order.SecurityNameCode;
            fOrder.Quantity = new Google.Type.Decimal { Value = order.Volume.ToString().Replace(",", ".") };
            fOrder.Side = GetFSide(order.Side);
            fOrder.ClientOrderId = order.NumberUser.ToString();
            if (order.TypeOrder == OrderPriceType.Limit)
            {
                fOrder.Type = OrderType.Limit;
                fOrder.LimitPrice = new Google.Type.Decimal { Value = order.Price.ToString().Replace(",", ".") };
            }
            else if (order.TypeOrder == OrderPriceType.Market)
            {
                fOrder.Type = OrderType.Market;
            }

            OrderState orderState;
            _rateGateMyOrderTradePlaceOrder.WaitToProceed();
            try
            {
                orderState = _myOrderTradeClient.PlaceOrder(fOrder, _gRpcMetadata);
            }
            catch (RpcException ex)
            {
                string message = GetGRPCErrorMessage(ex);
                SendLogMessage($"Error place order. Info: {message}", LogMessageType.Error);
                //order.Comment = message;
                InvokeOrderFail(order);
                return;
            }
            catch (Exception exception)
            {
                SendLogMessage($"Error on order execution: {exception.Message}", LogMessageType.Error);
                InvokeOrderFail(order);
                return;
            }

            if (orderState == null)
            {
                InvokeOrderFail(order);
                return;
            }

            FOrder newOrder = orderState.Order;
            order.State = GetOrderStateType(orderState.Status);
            order.NumberMarket = orderState.OrderId;
            order.TimeCallBack = orderState.TransactAt.ToDateTime();
            if (newOrder.LimitPrice != null)
            {
                order.Price = newOrder.LimitPrice.Value.ToDecimal();
            }
            order.Volume = newOrder.Quantity.Value.ToDecimal();

            if (order.State == OrderStateType.Cancel)
            {
                order.TimeCancel = orderState.WithdrawAt.ToDateTime(); // TODO Описание в документации не соответствует названию параметра. Уточнить.
            }

            if (order.State == OrderStateType.Done)
            {
                order.TimeDone = orderState.AcceptAt.ToDateTime();
            }
            //MyOrderEvent?.Invoke(order);
            InvokeMyOrderEvent(order);
        }

        public void GetAllActivOrders()
        {
            List<Order> orders = GetAllActiveOrdersFromExchange();

            if (orders == null || orders.Count == 0) return;

            for (int i = 0; orders != null && i < orders.Count; i++)
            {
                if (orders[i] == null) continue;
                if (orders[i].State == OrderStateType.Fail
                    || orders[i].State == OrderStateType.Done
                    || orders[i].State == OrderStateType.Cancel
                    || orders[i].State == OrderStateType.None
                    ) continue;
                InvokeMyOrderEvent(orders[i]);
                //MyOrderEvent?.Invoke(orders[i]);
            }
        }

        private List<Order> GetAllActiveOrdersFromExchange()
        {
            while (_securities == null || _securities.Count == 0)
            {
                Task.Delay(50);
            }

            OrdersResponse ordersResponse = null;
            _rateGateMyOrderTradeGetOrders.WaitToProceed();
            try
            {
                ordersResponse = _myOrderTradeClient.GetOrders(new OrdersRequest { AccountId = _accountId }, _gRpcMetadata);
            }
            catch (RpcException ex)
            {
                string message = GetGRPCErrorMessage(ex);
                SendLogMessage($"Get all orders request error. Info: {message}", LogMessageType.Error);
                return null;
            }
            catch (Exception exception)
            {
                SendLogMessage($"Get get all orders request error: {exception.Message}", LogMessageType.Error);
                return null;
            }

            if (ordersResponse == null || ordersResponse.Orders.Count == 0) return null;

            List<Order> orders = new List<Order>();
            for (int i = 0; i < ordersResponse.Orders.Count; i++)
            {
                OrderState orderState = ordersResponse.Orders[i];
                Order order = ConvertToOSEngineOrder(orderState); //TODO проверить логику
                orders.Add(order);
            }

            return orders;
        }

        public bool CancelOrder(Order order)
        {
            OrderState orderCancelResponse = null;
            _rateGateMyOrderTradeCancelOrder.WaitToProceed();
            try
            {
                orderCancelResponse = _myOrderTradeClient.CancelOrder(new CancelOrderRequest { AccountId = _accountId, OrderId = order.NumberMarket }, _gRpcMetadata);
            }
            catch (RpcException ex)
            {
                string message = GetGRPCErrorMessage(ex);
                SendLogMessage($"Cancel order request error. Info: {message}", LogMessageType.Error);
                return false;
            }
            catch (Exception exception)
            {
                SendLogMessage($"Cancel order request error: {exception.Message}", LogMessageType.Error);
                return false;
            }

            if (orderCancelResponse != null)
            {
                order.State = GetOrderStateType(orderCancelResponse.Status);

                //MyOrderEvent?.Invoke(order);
                InvokeMyOrderEvent(order);
                return true;
            }
            return false;
        }

        public OrderStateType GetOrderStatus(Order order)
        {
            OrderState orderResponse = null;
            _rateGateMyOrderTradeGetOrder.WaitToProceed();
            try
            {
                orderResponse = _myOrderTradeClient.GetOrder(new GetOrderRequest { AccountId = _accountId, OrderId = order.NumberMarket }, _gRpcMetadata);
            }
            catch (RpcException ex)
            {
                string message = GetGRPCErrorMessage(ex);
                SendLogMessage($"Get all orders request error. Info: {message}", LogMessageType.Error);
                return OrderStateType.None;
            }
            catch (Exception exception)
            {
                SendLogMessage($"Get get all orders request error: {exception.Message}", LogMessageType.Error);
                return OrderStateType.None;
            }

            if (orderResponse == null || string.IsNullOrEmpty(orderResponse.Order.ClientOrderId)) return OrderStateType.None;

            Order orderUpdated = ConvertToOSEngineOrder(orderResponse);
            if (orderUpdated == null) return OrderStateType.None;

            MyOrderEvent?.Invoke(orderUpdated);
            // Событие, не через обработчик
            //InvokeMyOrderEvent(orderUpdated);

            return orderUpdated.State;
        }

        public void CancelAllOrders()
        {
            List<Order> orders = GetAllActiveOrdersFromExchange();

            for (int i = 0; i < orders.Count; i++)
            {
                Order order = orders[i];

                if (order.State == OrderStateType.Active)
                {
                    CancelOrder(order);
                }
            }
        }

        public void CancelAllOrdersToSecurity(Security security)
        {
            List<Order> orders = GetAllActiveOrdersFromExchange();

            for (int i = 0; i < orders.Count; i++)
            {
                Order order = orders[i];

                if (order == null) continue;

                if (order.State == OrderStateType.Active
                    && order.SecurityNameCode == security.Name)
                {
                    CancelOrder(order);
                }
            }
        }

        public void ChangeOrderPrice(Order order, decimal newPrice) { }

        public event Action<Order> MyOrderEvent;
        private RateGate _rateGateMyOrderTradePlaceOrder = new RateGate(200, TimeSpan.FromMinutes(1));
        private RateGate _rateGateMyOrderTradeCancelOrder = new RateGate(200, TimeSpan.FromMinutes(1));
        private RateGate _rateGateMyOrderTradeGetOrders = new RateGate(200, TimeSpan.FromMinutes(1));
        private RateGate _rateGateMyOrderTradeGetOrder = new RateGate(200, TimeSpan.FromMinutes(1));
        #endregion

        #region 11 Helpers
        public void SetDisconnected()
        {
            if (ServerStatus != ServerConnectStatus.Disconnect)
            {
                ServerStatus = ServerConnectStatus.Disconnect;
                DisconnectEvent?.Invoke();
            }
        }

        private string GetGRPCErrorMessage(RpcException ex)
        {
            return string.Format("{0}: {1}", ex.Status.StatusCode, ex.Status.Detail);
        }

        private Side GetSide(FSide side)
        {
            return side switch
            {
                FSide.Buy => Side.Buy,
                FSide.Sell => Side.Sell,
                _ => throw new Exception("Order side is not defined!")
            };
        }

        private FSide GetFSide(Side side)
        {
            return side switch
            {
                Side.Buy => FSide.Buy,
                Side.Sell => FSide.Sell,
                _ => throw new Exception("Order side is not defined!")
            };
        }

        private OrderStateType GetOrderStateType(OrderStatus status)
        {
            return status switch
            {
                OrderStatus.Canceled => OrderStateType.Cancel,
                OrderStatus.Expired => OrderStateType.Cancel, // По смыслу подходит? или fail
                OrderStatus.Executed => OrderStateType.Done,
                OrderStatus.New => OrderStateType.Active,
                OrderStatus.PendingNew => OrderStateType.Pending,
                OrderStatus.Filled => OrderStateType.Done,
                OrderStatus.PartiallyFilled => OrderStateType.Partial,
                OrderStatus.Rejected => OrderStateType.Fail,
                OrderStatus.RejectedByExchange => OrderStateType.Fail,
                OrderStatus.DeniedByBroker => OrderStateType.Fail,
                OrderStatus.Failed => OrderStateType.Fail,
                _ => OrderStateType.None
            };
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


        private void InvokeMyTradeEvent(MyTrade trade)
        {
            if (trade == null) return;
            if (string.IsNullOrEmpty(trade.NumberOrderParent)) return;
            // Fix Finam возвращает список всех трейдов за день
            // Выбираем только ещё необработанные
            MyTrade processedMyTrade;
            if (_processedMyTrades.Contains(trade.NumberTrade))
            {
                return;
            }

            _processedMyTrades.Add(trade.NumberTrade);

            MyTradeEvent?.Invoke(trade);
        }
        private HashSet<string> _processedMyTrades = new HashSet<string>();

        private void InvokeMyOrderEvent(Order order)
        {
            if (order == null) return;
            if (order.NumberUser == 0) return;
            // Fix Finam возвращает список всех заявок за день
            // Выбираем только ещё необработанные
            OrderStateType processedOrderState;

            if (_processedOrders.TryGetValue(order.NumberUser, out processedOrderState))
            {
                if (processedOrderState == order.State
                    // Final states
                    || processedOrderState == OrderStateType.Done
                    || processedOrderState == OrderStateType.Cancel
                    || processedOrderState == OrderStateType.Fail

                    )
                {
                    return;
                }
            }

            if (_processedOrders.ContainsKey(order.NumberUser))
            {
                _processedOrders[order.NumberUser] = order.State;
            }
            else
            {
                _processedOrders.Add(order.NumberUser, order.State);
            }

            MyOrderEvent?.Invoke(order);
            Task.Run(GetPortfolios); // Обновялем портфель, в апи нет потока с обновлениями портфеля
        }
        private Dictionary<int, OrderStateType> _processedOrders = new Dictionary<int, OrderStateType>();

        private void InvokeOrderFail(Order order)
        {
            order.State = OrderStateType.Fail;
            InvokeMyOrderEvent(order);
            //MyOrderEvent?.Invoke(order);
        }

        protected TimeSpan getHistoryDepth(FTimeFrame tf)
        {
            // Substract 2 Days for Finam quirks
            return tf switch
            {
                FTimeFrame.M1 => TimeSpan.FromDays(5),
                FTimeFrame.D => TimeSpan.FromDays(363),
                //FTimeFrame.W => TimeSpan.FromDays(365),
                //FTimeFrame.MN => TimeSpan.FromDays(365),
                //FTimeFrame.QR => TimeSpan.FromDays(365),
                _ => TimeSpan.FromDays(28),
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
        public event Action<Funding> FundingUpdateEvent;
        public event Action<SecurityVolumes> Volume24hUpdateEvent;
        #endregion

        #region 13 Структуры
        // Для управления потоками чтения стримов
        private class TradesStreamReaderInfo
        {
            //public MarketDataService.MarketDataServiceClient MarketDataClient;
            public AsyncServerStreamingCall<SubscribeLatestTradesResponse> Stream { get; set; }
            public CancellationTokenSource CancellationTokenSource { get; set; }
            public Task ReaderTask { get; set; }
        }

        private class OrderBookStreamReaderInfo
        {
            public AsyncServerStreamingCall<SubscribeOrderBookResponse> Stream { get; set; }
            public CancellationTokenSource CancellationTokenSource { get; set; }
            public Task ReaderTask { get; set; }
        }

       
        #endregion
    }
}