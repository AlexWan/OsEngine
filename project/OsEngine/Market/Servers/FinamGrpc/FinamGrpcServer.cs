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
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Documents;
using System.Windows.Forms;
using Candle = OsEngine.Entity.Candle;
using FPosition = Grpc.Tradeapi.V1.Accounts.Position;
using FTimeFrame = Grpc.Tradeapi.V1.Marketdata.TimeFrame;
using FTrade = Grpc.Tradeapi.V1.Marketdata.Trade;
using Order = OsEngine.Entity.Order;
using Portfolio = OsEngine.Entity.Portfolio;
using Security = OsEngine.Entity.Security;
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

            Thread worker2 = new Thread(DataMessageReader);
            worker2.Name = "DataMessageReaderTInvest";
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
                    Asset item = assetsResponse.Assets[i];

                    Security newSecurity = new Security();
                    newSecurity.Name = string.IsNullOrEmpty(item.Name) ? item.Symbol : item.Name; // item.Ticker;
                    newSecurity.NameId = item.Symbol;
                    newSecurity.NameFull = item.Symbol;
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
                    newSecurity.MinTradeAmount = 1;
                    newSecurity.MinTradeAmountType = MinTradeAmountType.Contract;

                    // Не получаем доп инфо по тикеру (лимит 60 запросов в минуту)
                    //GetAssetParamsResponse assetParamsResponse = null;
                    //try
                    //{
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
                    //newSecurity.State = ...

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
                newPos.SecurityNameCode = pos.Symbol;
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
            DateTime timeStart = DateTime.UtcNow.AddHours(3) - TimeSpan.FromMinutes(timeFrameBuilder.TimeFrameTimeSpan.TotalMinutes * candleCount);
            DateTime timeEnd = DateTime.UtcNow.AddHours(3); // to MSK

            List<Candle> candles = GetCandleDataToSecurity(security, timeFrameBuilder, timeStart, timeEnd, timeStart);

            return candles;
        }

        public List<Candle> GetCandleDataToSecurity(Security security, TimeFrameBuilder timeFrameBuilder, DateTime startTime, DateTime endTime,
            DateTime actualTime)
        {

            FTimeFrame ftf = CreateTimeFrameInterval(timeFrameBuilder.TimeFrame);

            List<Candle> candles = new List<Candle>();

            // ensure all times are UTC
            startTime = DateTime.SpecifyKind(startTime.AddHours(-3), DateTimeKind.Utc); // MSK -> UTC
            endTime = DateTime.SpecifyKind(endTime.AddHours(-3), DateTimeKind.Utc);

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
                if (range == null) return null;

                candles.AddRange(range);

                queryStartTime = queryEndTime;
            }

            return candles;
        }

        private RateGate _rateGateMarketDataBars = new RateGate(60, TimeSpan.FromMinutes(1));
        private List<Candle> GetCandleHistoryFromServer(DateTime fromDateTime, DateTime toDateTime, Security security, FTimeFrame ftf)
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
                SendLogMessage($"Error getting candles for {security.Name}: " + ex.ToString(),
                    LogMessageType.Error);
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
                    TimeStart = fCandle.Timestamp.ToDateTime().AddHours(3)
                };
                candles.Add(candle);
            }

            return candles;
        }
        #endregion

        #region 6 gRPC streams creation
        private void CreateStreamsConnection()
        {
            _cancellationTokenSource = new CancellationTokenSource();

            _channel = GrpcChannel.ForAddress(_gRPCHost, new GrpcChannelOptions
            {
                Credentials = ChannelCredentials.SecureSsl,
                HttpClient = new HttpClient(new HttpClientHandler { Proxy = _proxy, UseProxy = _proxy != null })
            });

            _authClient = new AuthService.AuthServiceClient(_channel);
            _assetsClient = new AssetsService.AssetsServiceClient(_channel);
            _accountsClient = new AccountsService.AccountsServiceClient(_channel);
            _ordersClient = new OrdersService.OrdersServiceClient(_channel);
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

        private AuthService.AuthServiceClient _authClient;
        private AssetsService.AssetsServiceClient _assetsClient;
        private AccountsService.AccountsServiceClient _accountsClient;
        private OrdersService.OrdersServiceClient _ordersClient;
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

        private RateGate _rateGateSubscribeOrderBook = new RateGate(60, TimeSpan.FromMinutes(1));
        private RateGate _rateGateSubscribeLatestTrades = new RateGate(60, TimeSpan.FromMinutes(1));
        private RateGate _rateGateSubscribeSubscribeQuote = new RateGate(60, TimeSpan.FromMinutes(1));
        List<Security> _subscribedSecurities = new List<Security>();
        #endregion

        #region 8 Reading messages from data streams

        private async void DataMessageReader()
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

                    //if (quoteStream == null)
                    //{
                    //    Thread.Sleep(1);
                    //    continue;
                    //}

                    SubscribeLatestTradesResponse latestTradesResponse = _latestTradesStream.ResponseStream.Current;

                    if (latestTradesResponse == null)
                    {
                        Thread.Sleep(1);
                        continue;
                    }

                    _lastLatestTradesTime = DateTime.UtcNow;

                    //if (quoteResponse.Ping != null)
                    //{
                    //    Thread.Sleep(1);
                    //    continue;
                    //}

                    if (latestTradesResponse.Trades != null && latestTradesResponse.Trades.Count > 0)
                    {
                        Security security = GetSecurity(latestTradesResponse.Symbol);
                        if (security == null) { continue; }
                        for (int i = 0; i < latestTradesResponse.Trades.Count; i++)
                        {
                            FTrade newTrade = latestTradesResponse.Trades[i];

                            Trade trade = new Trade();
                            trade.SecurityNameCode = security.Name;
                            //trade.Price = GetValue(newTrade.Price);
                            trade.Price = newTrade.Price.Value.ToString().ToDecimal();
                            trade.Time = newTrade.Timestamp.ToDateTime().AddHours(3); // convert to MSK
                            trade.Id = newTrade.TradeId;
                            //trade.Side = quoteResponse.Trade.Direction == TradeDirection.Buy ? Side.Buy : Side.Sell;
                            // TODO В документации нет направления сделки
                            trade.Volume = newTrade.Size.Value.ToString().ToDecimal();
                        }

                    }

                    //    if (_filterOutNonMarketData)
                    //    {
                    //        if (isTodayATradingDayForSecurity(security) == false)
                    //            continue;
                    //    }


                    //    if (_ignoreMorningAuctionTrades && trade.Time.Hour < 9) // process only mornings
                    //    {
                    //        if (security.SecurityType == SecurityType.Futures)
                    //        {
                    //            if (trade.Time < trade.Time.Date.AddHours(9))
                    //            {
                    //                continue;
                    //            }
                    //        }
                    //        else
                    //        {
                    //            if (trade.Time < trade.Time.Date.AddHours(7))
                    //            {
                    //                continue;
                    //            }
                    //        }
                    //    }

                    //    if (NewTradesEvent != null)
                    //    {
                    //        NewTradesEvent(trade);
                    //    }
                    //}

                    //if (quoteResponse.LastPrice != null)
                    //{
                    //    ProcessLastPrice(quoteResponse.LastPrice);
                    //}

                    //if (quoteResponse.Orderbook != null)
                    //{
                    //    Security security = GetSecurity(quoteResponse.Orderbook.InstrumentUid);
                    //    if (security == null)
                    //        continue;

                    //    if (_filterOutNonMarketData)
                    //    {
                    //        if (isTodayATradingDayForSecurity(security) == false)
                    //            continue;
                    //    }

                    //    MarketDepth depth = new MarketDepth();
                    //    depth.SecurityNameCode = security.Name;
                    //    depth.Time = quoteResponse.Orderbook.Time.ToDateTime().AddHours(3);// convert to MSK


                    //    for (int i = 0; i < marketDataResponse.Orderbook.Bids.Count; i++)
                    //    {
                    //        MarketDepthLevel newBid = new MarketDepthLevel();
                    //        newBid.Price = GetValue(marketDataResponse.Orderbook.Bids[i].Price);
                    //        newBid.Bid = marketDataResponse.Orderbook.Bids[i].Quantity;
                    //        depth.Bids.Add(newBid);
                    //    }

                    //    for (int i = 0; i < marketDataResponse.Orderbook.Asks.Count; i++)
                    //    {
                    //        MarketDepthLevel newAsk = new MarketDepthLevel();
                    //        newAsk.Price = GetValue(marketDataResponse.Orderbook.Asks[i].Price);
                    //        newAsk.Ask = marketDataResponse.Orderbook.Asks[i].Quantity;
                    //        depth.Asks.Add(newAsk);
                    //    }

                    //    if (_lastMdTime != DateTime.MinValue &&
                    //        _lastMdTime >= depth.Time)
                    //    {
                    //        depth.Time = _lastMdTime.AddMilliseconds(1);
                    //    }

                    //    _lastMdTime = depth.Time;

                    //    if (MarketDepthEvent != null)
                    //    {
                    //        MarketDepthEvent(depth);
                    //    }
                    //}
                }
                catch (RpcException ex) when (ex.StatusCode == StatusCode.Cancelled)
                {
                    // Handle the cancellation gracefully
                    string message = GetGRPCErrorMessage(ex);
                    SendLogMessage($"Market data stream was cancelled: {message}", LogMessageType.System);
                    Thread.Sleep(5000);
                }
                catch (RpcException exception)
                {
                    SendLogMessage($"Market data stream was disconnected: {exception.Message}", LogMessageType.Error);

                    // need to reconnect everything
                    if (ServerStatus != ServerConnectStatus.Disconnect)
                    {
                        ServerStatus = ServerConnectStatus.Disconnect;
                        DisconnectEvent();
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
        #endregion

        #region 9 Trade

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

        #region 10 Helpers

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

            //DateTime historyStart = DateTime.Now - period;

            //return historyStart < dt ? dt : historyStart;
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

        #region 11 Log

        private void SendLogMessage(string message, LogMessageType messageType)
        {
            LogMessageEvent?.Invoke(message, messageType);
        }

        public event Action<string, LogMessageType> LogMessageEvent;

        #endregion


        public event Action<MarketDepth> MarketDepthEvent;
        public event Action<Trade> NewTradesEvent;

        public event Action<MyTrade> MyTradeEvent;
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
            throw new NotImplementedException();
        }

        public void SendOrder(Order order)
        {
            throw new NotImplementedException();
        }
    }
}
