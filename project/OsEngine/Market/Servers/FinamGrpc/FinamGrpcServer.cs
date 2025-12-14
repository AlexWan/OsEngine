using Google.Protobuf.WellKnownTypes;
using Google.Type;
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
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading;
using Candle = OsEngine.Entity.Candle;
using DateTime = System.DateTime;
using FAsset = Grpc.Tradeapi.V1.Assets.Asset;
using FOrder = Grpc.Tradeapi.V1.Orders.Order;
using FPosition = Grpc.Tradeapi.V1.Accounts.Position;
using FSide = Grpc.Tradeapi.V1.Side;
using FTimeFrame = Grpc.Tradeapi.V1.Marketdata.TimeFrame;
using FTrade = Grpc.Tradeapi.V1.Marketdata.Trade;
//using GTime = Google.Type.DateTime;
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

            Thread worker2 = new Thread(ReSubscribeThread);
            worker2.Name = "ReIssueTokenThreadFinamGrpc";
            worker2.Start();


            Thread worker3 = new Thread(MyOrderTradeMessageReader);
            worker3.Name = "MyOrderTradeMessageReaderFinamGrpc";
            worker3.Start();

            Thread worker3s = new Thread(MyOrderTradeKeepAlive);
            worker3s.Name = "MyOrderTradeKeepAliveFinamGrpc";
            worker3s.IsBackground = true;
            worker3s.Start();

            Thread worker4 = new Thread(PortfolioUpdater);
            worker4.Name = "PortfolioUpdaterFinamGrpc";
            worker4.Start();

        }

        public void Connect(WebProxy proxy)
        {
            try
            {
                _myPortfolios.Clear();
                _subscribedSecurities.Clear();
                _processedOrders.Clear();

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

                SetСonnected();
            }
            catch (Exception ex)
            {
                SendLogMessage($"Error connecting to server: {ex.Message}", LogMessageType.Error);
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
                SendLogMessage($"Error cancelling stream: {ex.Message}", LogMessageType.Error);
            }

            disconnectMyOrderTradeStream();

            if (_cancellationTokenSource != null)
            {
                try
                {
                    _cancellationTokenSource.Cancel();
                }
                catch (Exception ex)
                {
                    SendLogMessage($"Error disposing stream: {ex}", LogMessageType.Error);
                }
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

            SendLogMessage("Connection to Finam gRPC closed. Data streams Closed Event", LogMessageType.System);

            SetDisconnected();
        }

        public event Action ConnectEvent;

        public event Action DisconnectEvent;

        public event Action ForceCheckOrdersAfterReconnectEvent { add { } remove { } }

        public DateTime ServerTime { get; set; }

        public ServerConnectStatus ServerStatus { get; set; } = ServerConnectStatus.Disconnect;

        public List<IServerParameter> ServerParameters { get; set; }

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
            catch (RpcException rpcEx)
            {
                string msg = GetGRPCErrorMessage(rpcEx);
                SendLogMessage($"Error loading securities. Info: {msg}", LogMessageType.Error);
            }
            catch (Exception ex)
            {
                SendLogMessage($"Error loading securities: {ex.Message}", LogMessageType.Error);
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
            catch (Exception ex)
            {
                SendLogMessage($"Error loading currency pairs: {ex.Message}", LogMessageType.Error);
            }
        }

        public event Action<List<Security>> SecurityEvent;

        private RateGate _rateGateAssetsAsset = new RateGate(200, TimeSpan.FromMinutes(1));

        private List<Security> _securities = new List<Security>();

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
            //catch (RpcException rpcEx) when (rpcEx.StatusCode == StatusCode.Unavailable)
            //{
            //    // Do nothing
            //}
            catch (RpcException rpcEx)
            {
                SetDisconnected();
                string msg = GetGRPCErrorMessage(rpcEx);
                SendLogMessage($"Error getting portfolios. Info: {msg}", LogMessageType.Error);
            }
            catch (Exception ex)
            {
                SetDisconnected();
                SendLogMessage($"Error getting portfolios: {ex.Message}", LogMessageType.Error);
            }

            UpdatePortfolios(getAccountResponse);

            PortfolioEvent?.Invoke(_myPortfolios);
        }

        private void UpdatePortfolios(GetAccountResponse getAccountResponse)
        {
            if (getAccountResponse == null) return;
            lock (_portfolioLocker)
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
        }

        private RateGate _rateGateAccountsGetAccount = new RateGate(200, TimeSpan.FromMinutes(1));

        public event Action<List<Portfolio>> PortfolioEvent;

        private List<Portfolio> _myPortfolios = new List<Portfolio>();

        private string _portfolioLocker = "portfolioLockerFinamGrpc";
        #endregion

        #region 5 Data
        public List<Candle> GetLastCandleHistory(Security security, TimeFrameBuilder timeFrameBuilder, int candleCount)
        {
            DateTime timeStart = DateTime.UtcNow.AddHours(_timezoneOffset) - TimeSpan.FromMinutes(timeFrameBuilder.TimeFrameTimeSpan.TotalMinutes * candleCount);
            DateTime timeEnd = DateTime.UtcNow.AddHours(_timezoneOffset);

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
            catch (RpcException rpcEx)
            {
                string msg = GetGRPCErrorMessage(rpcEx);
                SendLogMessage($"Error getting candles for {security.Name}. Info: {msg}", LogMessageType.Error);
            }
            catch (Exception ex)
            {
                SendLogMessage($"Error getting candles for {security.Name}: {ex.Message}", LogMessageType.Error);
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
            catch (RpcException rpcEx)
            {
                string msg = GetGRPCErrorMessage(rpcEx);
                SendLogMessage($"Error loading security [{security.NameId}] params. Info: {msg}", LogMessageType.Error);
            }
            catch (Exception ex)
            {
                SendLogMessage($"Error loading security [{security.NameId}] params: {ex.Message}", LogMessageType.Error);
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
            //catch (RpcException rpcEx)
            //{
            //    string msg = GetGRPCErrorMessage(rpcEx);
            //    SendLogMessage($"Error loading securities. Info: {msg}", LogMessageType.Error);
            //}
            //catch (Exception ex)
            //{
            //    SendLogMessage($"Error loading securities: {ex.Message}", LogMessageType.Error);
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
                    level.Price = newLevel.Price.Value.ToString().ToDouble();

                    if (newLevel.SideCase == OrderBook.Types.Row.SideOneofCase.BuySize)
                    {
                        level.Bid = newLevel.BuySize.Value.ToString().ToDouble();
                        depth.Bids.Add(level);
                    }

                    if (newLevel.SideCase == OrderBook.Types.Row.SideOneofCase.SellSize)
                    {
                        level.Ask = newLevel.SellSize.Value.ToString().ToDouble();
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
            catch (RpcException rpcEx)
            {
                string msg = GetGRPCErrorMessage(rpcEx);
                SendLogMessage($"Error getting Market Depth for {security.Name}. Info: {msg}", LogMessageType.Error);
            }
            catch (Exception ex)
            {
                SendLogMessage($"Error getting Market Depth for {security.Name}: {ex.Message}", LogMessageType.Error);
            }

            return depth;
        }

        public List<Trade> GetTickDataToSecurity(Security security, DateTime startTime, DateTime endTime, DateTime actualTime)
        {
            return null; // Недоступно
            /*
            LatestTradesResponse resp = null;
            try
            {
                resp = _marketDataClient.LatestTrades(new LatestTradesRequest { Symbol = security.NameId }, _gRpcMetadata);
            }
            catch (RpcException rpcEx)
            {
                string msg = GetGRPCErrorMessage(rpcEx);
                SendLogMessage($"Error while getting latest trades data. Info: {msg}", LogMessageType.Error);
            }
            catch (Exception ex)
            {
                SendLogMessage($"Error while getting latest trades data: {ex.Message}", LogMessageType.Error);
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

            return trades.Count > 0 ? trades : null;*/
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
                MaxRetryAttempts = 5,
            });

            _authClient = new AuthService.AuthServiceClient(_channel);
            _assetsClient = new AssetsService.AssetsServiceClient(_channel);
            _accountsClient = new AccountsService.AccountsServiceClient(_channel);
            _myOrderTradeClient = new OrdersService.OrdersServiceClient(_channel);
            _marketDataClient = new MarketDataService.MarketDataServiceClient(_channel);

            // Получаем gwt токен
            updateAuth(_authClient);

            // Подписываемся на свои события
            connectMyOrderTradeStream();

            SendLogMessage("All streams activated. State: connected.", LogMessageType.System);
        }

        private void disconnectMyOrderTradeStream()
        {
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
                    SendLogMessage($"Error cancelling stream: {ex.Message}", LogMessageType.Error);
                }

                SendLogMessage("Disconnected exchange with my orders and trades stream.", LogMessageType.System);
                _myOrderTradeStream = null;
            }
        }

        private void connectMyOrderTradeStream()
        {
            try
            {
                // Подписка один раз
                _rateGateMyOrderTradeSubscribeOrderTrade.WaitToProceed();
                _myOrderTradeStream = _myOrderTradeClient.SubscribeOrderTrade(_gRpcMetadata, null, _cancellationTokenSource.Token);
            }
            catch (RpcException rpcEx)
            {
                string msg = GetGRPCErrorMessage(rpcEx);
                SendLogMessage($"gRPC Error while auth. Info: {msg}", LogMessageType.Error);
                return;
            }
            catch (Exception ex)
            {
                SendLogMessage($"Error while auth. Info: {ex.Message}", LogMessageType.Error);
            }
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

        private ConcurrentDictionary<string, OrderBookStreamReaderInfo> _dicOrderBookStreams = new ConcurrentDictionary<string, OrderBookStreamReaderInfo>();

        private ConcurrentDictionary<string, TradesStreamReaderInfo> _dicLatestTradesStreams = new ConcurrentDictionary<string, TradesStreamReaderInfo>();

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

        public void Subscribe(Security security)
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
            catch (RpcException rpcEx)
            {
                string msg = GetGRPCErrorMessage(rpcEx);
                SendLogMessage($"Error subscribe security {security.Name}. Info: {msg}", LogMessageType.Error);
            }
            catch (Exception ex)
            {
                SendLogMessage($"Error subscribe security {security.Name}. {ex.Message}", LogMessageType.Error);
            }
        }

        public void ReSubscribe(Security security)
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
            catch (RpcException rpcEx)
            {
                string msg = GetGRPCErrorMessage(rpcEx);
                SendLogMessage($"Error subscribe security {security.Name}. Info: {msg}", LogMessageType.Error);
            }
            catch (Exception ex)
            {
                SendLogMessage($"Error subscribe security {security.Name}. {ex.Message}", LogMessageType.Error);
            }
        }

        public bool SubscribeNews()
        {
            return false;
        }

        public event Action<News> NewsEvent { add { } remove { } }

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
            if (!_dicLatestTradesStreams.TryAdd(security.NameId, null))
            {
                // Уже есть reader, не запускаем второй
                return;
            }

            TradesStreamReaderInfo streamReaderInfo = new TradesStreamReaderInfo();
            // Свой CTS токен для каждого потока
            CancellationTokenSource cts = CancellationTokenSource.CreateLinkedTokenSource(_cancellationTokenSource.Token);
            streamReaderInfo.CancellationTokenSource = cts;

            _rateGateMarketDataSubscribeLatestTrades.WaitToProceed();
            streamReaderInfo.Stream = _marketDataClient.SubscribeLatestTrades(new SubscribeLatestTradesRequest { Symbol = security.NameId }, _gRpcMetadata, null, cts.Token);

            Thread ReaderThread = new Thread(() => SingleTradesMessageReader(streamReaderInfo.Stream, cts.Token, security));
            ReaderThread.IsBackground = true;
            ReaderThread.Start();


            _dicLatestTradesStreams[security.NameId] = streamReaderInfo;
        }

        private void StartOrderBookStream(Security security)
        {
            if (!_dicOrderBookStreams.TryAdd(security.NameId, null))
            {
                // Уже есть reader, не запускаем второй
                return;
            }

            OrderBookStreamReaderInfo streamReaderInfo = new OrderBookStreamReaderInfo();
            CancellationTokenSource cts = CancellationTokenSource.CreateLinkedTokenSource(_cancellationTokenSource.Token);
            streamReaderInfo.CancellationTokenSource = cts;

            _rateGateMarketDataSubscribeOrderBook.WaitToProceed();
            streamReaderInfo.Stream = _marketDataClient.SubscribeOrderBook(new SubscribeOrderBookRequest { Symbol = security.NameId }, _gRpcMetadata, null, cts.Token);

            Thread ReaderThread = new Thread(() => SingleMarketDepthReader(streamReaderInfo.Stream, cts.Token, security));
            ReaderThread.IsBackground = true;
            ReaderThread.Start();


            _dicOrderBookStreams[security.NameId] = streamReaderInfo;
        }

        // Переподключение reader для конкретного инструмента
        private void ReconnectLatestTradesStream(Security security)
        {
            DisconnectLatestTradesStream(security);
            StartLatestTradesStream(security);
        }

        private void DisconnectLatestTradesStream(Security security)
        {
            if (_dicLatestTradesStreams.TryRemove(security.NameId, out TradesStreamReaderInfo info))
            {
                // Отменяем токен только для этого ридера
                info.CancellationTokenSource.Cancel();
                //if (info.ReaderThread != null) info.ReaderThread.Abort();
                if (info.Stream != null) info.Stream.Dispose();
            }
        }

        private void ReconnectOrderBookStream(Security security)
        {
            DisconnectOrderBookStream(security);
            StartOrderBookStream(security);
        }

        private void DisconnectOrderBookStream(Security security)
        {
            if (_dicOrderBookStreams.TryRemove(security.NameId, out OrderBookStreamReaderInfo info))
            {
                // Отменяем токен только для этого ридера
                info.CancellationTokenSource.Cancel();
                //if (info.ReaderThread != null) info.ReaderThread.Abort();
                if (info.Stream != null) info.Stream.Dispose();
            }
        }

        // Переподключение всех reader-ов
        private void ReconnectAllDataStreams()
        {
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
        private void SingleTradesMessageReader(AsyncServerStreamingCall<SubscribeLatestTradesResponse> stream, CancellationToken token, Security security)
        {
            Thread.Sleep(1000);

            try
            {
                while (!token.IsCancellationRequested)
                {
                    if (ServerStatus == ServerConnectStatus.Disconnect)
                    {
                        Thread.Sleep(5);
                        continue;
                    }

                    bool hasData = false;
                    try
                    {
                        hasData = stream.ResponseStream.MoveNext().Result;
                    }
                    catch
                    {
                        Thread.Sleep(5);
                    }

                    if (!hasData)
                    {
                        ReconnectLatestTradesStream(security);
                        Thread.Sleep(5);
                        continue;
                    }

                    SubscribeLatestTradesResponse latestTradesResponse = stream.ResponseStream.Current;
                    if (latestTradesResponse == null)
                    {
                        Thread.Sleep(1);
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
                            if (newTrade == null || newTrade.Price == null) continue;
                            Trade trade = new Trade();
                            trade.SecurityNameCode = security.Name;
                            trade.Price = newTrade.Price.Value.ToString().ToDecimal();
                            trade.Time = newTrade.Timestamp.ToDateTime().AddHours(_timezoneOffset);
                            trade.Id = newTrade.TradeId;
                            trade.Side = GetSide(newTrade.Side);
                            trade.Volume = newTrade.Size.Value.ToString().ToDecimal();
                            NewTradesEvent?.Invoke(trade);
                        }
                    }
                }
            }
            catch (OperationCanceledException)
            {
            }
            catch
            {
            }
        }

        private void SingleMarketDepthReader(AsyncServerStreamingCall<SubscribeOrderBookResponse> stream, CancellationToken token, Security security)
        {
            Thread.Sleep(1000);

            try
            {
                while (!token.IsCancellationRequested)
                {
                    if (ServerStatus == ServerConnectStatus.Disconnect)
                    {
                        Thread.Sleep(5);
                        continue;
                    }

                    bool hasData = false;
                    try
                    {
                        hasData = stream.ResponseStream.MoveNext(token).Result;
                    }
                    catch
                    {
                        Thread.Sleep(5);
                        continue;
                    }

                    if (!hasData)
                    {
                        ReconnectOrderBookStream(security);
                        Thread.Sleep(5);
                        continue;
                    }

                    SubscribeOrderBookResponse latestOrderBookResponse = stream.ResponseStream.Current;
                    if (latestOrderBookResponse == null)
                    {
                        Thread.Sleep(1);
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
                                    Price = newLevel.Price.Value.ToString().ToDouble()
                                };

                                if (newLevel.SideCase == StreamOrderBook.Types.Row.SideOneofCase.BuySize)
                                {
                                    level.Bid = newLevel.BuySize.Value.ToString().ToDouble();
                                    depth.Bids.Add(level);
                                }
                                else if (newLevel.SideCase == StreamOrderBook.Types.Row.SideOneofCase.SellSize)
                                {
                                    level.Ask = newLevel.SellSize.Value.ToString().ToDouble();
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
            }
            catch
            {
            }
        }

        private void MyOrderTradeKeepAlive()
        {
            // Собственные заявки и сделки
            // Повторящаяся подписка, так как нет пинга и поток отваливается без реанимации
            while (_cancellationTokenSource == null)
            {
                Thread.Sleep(5000);
            }

            while (!_cancellationTokenSource.IsCancellationRequested)
            {
                Thread.Sleep(30000);
                try
                {
                    if (_myOrderTradeStream != null)
                    {
                        _myOrderTradeStream.RequestStream.WriteAsync(new OrderTradeRequest { AccountId = _accountId, Action = OrderTradeRequest.Types.Action.Subscribe, DataType = OrderTradeRequest.Types.DataType.All });
                    }
                }
                catch (RpcException rpcEx)
                {
                    string msg = GetGRPCErrorMessage(rpcEx);
                    SendLogMessage($"RPC. MyOrderTrade keepalive failed. {msg}", LogMessageType.Error);

                    Thread.Sleep(5000);
                }
                catch (Exception ex)
                {
                    SendLogMessage($"MyOrderTrade keepalive failed: {ex.Message}. Try to reconnect.", LogMessageType.Error);
                    string msg = ex.ToString();
                    if (msg.Contains("stream timeout"))
                    {
                        Thread.Sleep(5000);
                        disconnectMyOrderTradeStream();
                        connectMyOrderTradeStream();
                    }
                }
            }
        }

        private void PortfolioUpdater()
        {
            // Собственные заявки и сделки
            // Повторящаяся подписка, так как нет пинга и поток отваливается без реанимации
            while (_cancellationTokenSource == null)
            {
                Thread.Sleep(5000);
            }

            while (!_cancellationTokenSource.IsCancellationRequested)
            {
                Thread.Sleep(10000); // 10 c - достаточно для прохождения тестов
                if (ServerStatus == ServerConnectStatus.Disconnect) continue;
                GetPortfolios();
            }
        }

        private void MyOrderTradeMessageReader()
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

                    bool hasNext = _myOrderTradeStream.ResponseStream.MoveNext().ConfigureAwait(false).GetAwaiter().GetResult();
                    if (!hasNext)
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
                        List<Order> orders = new List<Order>();
                        for (int j = myOrderTradeResponse.Orders.Count - 1; j >= 0; j--)
                        {
                            OrderState myOrder = myOrderTradeResponse.Orders[j];

                            Order order = ConvertToOSEngineOrder(myOrder);

                            if (order != null && order.NumberUser > 0)
                            {
                                // MayBe OSEngine order
                                orders.Add(order);
                            }
                        }

                        //orders.Sort((x, y) => y.NumberUser.CompareTo(x.NumberUser));
                        orders.Sort((x, y) => y.TimeCallBack.CompareTo(x.TimeCallBack));

                        for (int j = 0; j < orders.Count; j++)
                        {
                            InvokeMyOrderEvent(orders[j]);
                        }
                    }

                    if (myOrderTradeResponse.Trades != null && myOrderTradeResponse.Trades.Count > 0)
                    {
                        List<MyTrade> trades = new List<MyTrade>();
                        for (int j = myOrderTradeResponse.Trades.Count - 1; j >= 0; j--)
                        {
                            AccountTrade myTrade = myOrderTradeResponse.Trades[j];

                            MyTrade trade = ConvertToOSEngineTrade(myTrade);

                            trades.Add(trade);
                        }

                        trades.Sort((x, y) => y.NumberTrade.CompareTo(x.NumberTrade));

                        for (int j = 0; j < trades.Count; j++)
                        {
                            InvokeMyTradeEvent(trades[j]);
                        }
                    }
                }
                catch (RpcException rpcEx) when (rpcEx.StatusCode == StatusCode.Cancelled)
                {
                    Thread.Sleep(5000);
                }
                catch (RpcException rpcEx) when (rpcEx.StatusCode == StatusCode.Internal)
                {
                    // Connection reset - attempt reconnect
                    SendLogMessage("OrderTrade stream connection reset.", LogMessageType.System);
                    SetDisconnected();
                    //SendLogMessage("Try to reconnect.", LogMessageType.System);
                    //disconnectMyOrderTradeStream();
                    //connectMyOrderTradeStream();
                }
                catch (RpcException rpcEx)
                {
                    string msg = GetGRPCErrorMessage(rpcEx);
                    SendLogMessage($"OrderTrade stream error. {msg}", LogMessageType.Error);

                    if (msg.Contains("stream timeout"))
                    {
                        SetDisconnected();
                        //SendLogMessage("Try to reconnect.", LogMessageType.Error);
                        //disconnectMyOrderTradeStream();
                        //connectMyOrderTradeStream();
                    }
                    Thread.Sleep(5000);
                }
                catch (Exception ex)
                {
                    SendLogMessage($"OrderTrade stream error. Reason: {ex.Message}", LogMessageType.Error);
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
            trade.Time = myTrade.Timestamp.ToDateTime().AddHours(_timezoneOffset);
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
            }
            // Non OS Engine order
            if (myOrder.NumberUser == 0) return null;

            myOrder.PortfolioNumber = fOrder.AccountId;
            myOrder.NumberMarket = orderState.OrderId;
            myOrder.TimeCallBack = orderState.TransactAt.ToDateTime().AddHours(_timezoneOffset);
            myOrder.TimeCreate = orderState.TransactAt.ToDateTime().AddHours(_timezoneOffset);
            if (fOrder.LimitPrice != null)
            {
                myOrder.Price = fOrder.LimitPrice.Value.ToDecimal();
            }
            myOrder.Volume = fOrder.Quantity.Value.ToDecimal();
            myOrder.TypeOrder = fOrder.Type switch
            {
                OrderType.Market => OrderPriceType.Market,
                OrderType.Limit => OrderPriceType.Limit,
                _ => throw new Exception("Order type is not supported")
            };

            Security security = GetSecurity(fOrder.Symbol);
            if (security == null)
            {
                SendLogMessage($"Can't find security for : {fOrder.Symbol}", LogMessageType.Error);
                return null;
            }
            myOrder.SecurityNameCode = security?.Name ?? fOrder.Symbol;
            myOrder.SecurityClassCode = security.NameClass;
            myOrder.Side = GetSide(fOrder.Side);
            myOrder.State = GetOrderStateType(orderState.Status);

            if (myOrder.State == OrderStateType.Cancel)
            {
                myOrder.TimeCancel = orderState.TransactAt.ToDateTime().AddHours(_timezoneOffset); // TODO Описание в документации не соответствует названию параметра. Уточнить.
            }

            if (myOrder.State == OrderStateType.Done)
            {
                myOrder.TimeDone = orderState.TransactAt.ToDateTime().AddHours(_timezoneOffset);
            }

            return myOrder;
        }

        public event Action<OptionMarketDataForConnector> AdditionalMarketDataEvent { add { } remove { } }

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
                    ServerTime = resp.Timestamp.ToDateTime().AddHours(_timezoneOffset);
                    Thread.Sleep(3000);
                }
                catch (RpcException rpcEx) when (rpcEx.StatusCode == StatusCode.Cancelled)
                {
                    Thread.Sleep(5000);
                }
                catch (RpcException rpcEx) when (rpcEx.StatusCode == StatusCode.Internal)
                {
                    string msg = GetGRPCErrorMessage(rpcEx);
                    if (rpcEx.Status.ToString().Contains("Token is expired"))
                    {
                        SendLogMessage($"Token is expired. {msg}", LogMessageType.Error);
                        // TODO RECONNECT!!!
                    }
                    else
                    {
                        SendLogMessage($"Keep Alive stream error. {msg}", LogMessageType.Error);
                    }
                    //SetDisconnected();
                    Thread.Sleep(1000);
                }
                catch (RpcException rpcEx)
                {
                    SetDisconnected();
                    string msg = GetGRPCErrorMessage(rpcEx);
                    SendLogMessage($"Error while get time from FinamGrpc. Info: {msg}", LogMessageType.Error);
                    Thread.Sleep(1000);
                }
                catch (Exception ex)
                {
                    SetDisconnected();
                    SendLogMessage($"Error while get time from FinamGrpc. {ex.Message}", LogMessageType.Error);
                    Thread.Sleep(1000);
                }
            }
        }

        /// <summary>
        /// Срок жизни JWT-токена 15 минут
        /// </summary>
        private void ReSubscribeThread()
        {
            int ms = Convert.ToInt32(_jwtTokenLifetime.TotalMilliseconds) - 60000;
            while (true)
            {
                Thread.Sleep(ms);

                if (_authClient == null || _myOrderTradeClient == null) continue;

                updateAuth(_authClient);

                _rateGateMyOrderTradeSubscribeOrderTrade.WaitToProceed();
                _myOrderTradeStream = _myOrderTradeClient.SubscribeOrderTrade(_gRpcMetadata, null, _cancellationTokenSource.Token);

                for (int i = 0; i < _subscribedSecurities.Count - 1; i++)
                {
                    ReSubscribe(_subscribedSecurities[i]);
                }
            }
        }

        #endregion

        #region 10 Trade

        public void SendOrder(Order order)
        {
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
            catch (RpcException rpcEx)
            {
                string msg = GetGRPCErrorMessage(rpcEx);
                SendLogMessage($"Error on order execution. Info: {msg}", LogMessageType.Error);
                InvokeOrderFail(order);
                return;
            }
            catch (Exception ex)
            {
                SendLogMessage($"Error on order execution: {ex.Message}", LogMessageType.Error);
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
            order.TimeCallBack = orderState.TransactAt.ToDateTime().AddHours(_timezoneOffset);
            if (newOrder.LimitPrice != null)
            {
                order.Price = newOrder.LimitPrice.Value.ToDecimal();
            }
            order.Volume = newOrder.Quantity.Value.ToDecimal();

            if (order.State == OrderStateType.Cancel)
            {
                order.TimeCancel = orderState.WithdrawAt.ToDateTime().AddHours(_timezoneOffset);
            }

            if (order.State == OrderStateType.Done)
            {
                order.TimeDone = orderState.AcceptAt.ToDateTime().AddHours(_timezoneOffset);
            }
            InvokeMyOrderEvent(order);
        }

        public void GetAllActivOrders()
        {
            List<Order> orders = GetAllActiveOrdersFromExchange();

            if (orders == null || orders.Count == 0) return;

            for (int i = 0; orders != null && i < orders.Count; i++)
            {
                if (orders[i] == null) continue;
                if (orders[i].State != OrderStateType.Active
                    && orders[i].State != OrderStateType.Partial
                    && orders[i].State != OrderStateType.Pending)
                {
                    continue;
                }

                InvokeMyOrderEvent(orders[i]);
            }
        }

        private List<Order> GetAllActiveOrdersFromExchange()
        {
            while (_securities == null || _securities.Count == 0)
            {
                Thread.Sleep(50);
            }

            OrdersResponse ordersResponse = null;
            _rateGateMyOrderTradeGetOrders.WaitToProceed();
            try
            {
                ordersResponse = _myOrderTradeClient.GetOrders(new OrdersRequest { AccountId = _accountId }, _gRpcMetadata);
            }
            catch (RpcException rpcEx)
            {
                string msg = GetGRPCErrorMessage(rpcEx);
                SendLogMessage($"Get all orders request error. Info: {msg}", LogMessageType.Error);
                return null;
            }
            catch (Exception ex)
            {
                SendLogMessage($"Get get all orders request error: {ex.Message}", LogMessageType.Error);
                return null;
            }

            if (ordersResponse == null || ordersResponse.Orders.Count == 0) return null;

            List<Order> orders = new List<Order>();
            for (int i = 0; i < ordersResponse.Orders.Count; i++)
            {
                OrderState orderState = ordersResponse.Orders[i];
                Order order = ConvertToOSEngineOrder(orderState);
                if (order == null) continue;
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
            catch (RpcException rpcEx)
            {
                string msg = GetGRPCErrorMessage(rpcEx);
                SendLogMessage($"Cancel order request error. Info: {msg}", LogMessageType.Error);
                return false;
            }
            catch (Exception ex)
            {
                SendLogMessage($"Cancel order request error: {ex.Message}", LogMessageType.Error);
                return false;
            }

            if (orderCancelResponse != null)
            {
                order.State = GetOrderStateType(orderCancelResponse.Status);

                InvokeMyOrderEvent(order);
                return true;
            }
            return false;
        }

        public OrderStateType GetOrderStatus(Order order)
        {
            if (order == null || string.IsNullOrEmpty(order.NumberMarket)) return OrderStateType.None;

            OrderState orderResponse = null;
            _rateGateMyOrderTradeGetOrder.WaitToProceed();
            try
            {
                orderResponse = _myOrderTradeClient.GetOrder(new GetOrderRequest { AccountId = _accountId, OrderId = order.NumberMarket }, _gRpcMetadata);
            }
            catch (RpcException rpcEx)
            {
                string msg = GetGRPCErrorMessage(rpcEx);
                SendLogMessage($"Get single order request error. Info: {msg}", LogMessageType.Error);
                return OrderStateType.None;
            }
            catch (Exception ex)
            {
                SendLogMessage($"Get single order request error: {ex.Message}", LogMessageType.Error);
                return OrderStateType.None;
            }

            if (orderResponse == null || string.IsNullOrEmpty(orderResponse.Order.ClientOrderId)) return OrderStateType.None;

            Order orderUpdated = ConvertToOSEngineOrder(orderResponse);
            if (orderUpdated == null) return OrderStateType.None;

            if (orderUpdated.State == OrderStateType.Done
                || orderUpdated.State == OrderStateType.Partial)
            {
                List<MyTrade> tradesForMyOrder
                    = GetMyTradesForMyOrder(order);

                if (tradesForMyOrder != null && tradesForMyOrder.Count > 0)
                {
                    for (int i = tradesForMyOrder.Count - 1; i >= 0; i--)
                    {
                        InvokeMyTradeEvent(tradesForMyOrder[i]);
                    }
                }
            }

            if (!InvokeMyOrderEvent(orderUpdated))
            {
                MyOrderEvent?.Invoke(orderUpdated);
            }

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

        private List<MyTrade> GetMyTradesForMyOrder(Order order)
        {
            TradesResponse tradesResponse = null;
            _rateGateAccountTrades.WaitToProceed();
            try
            {
                DateTime utcNow = DateTime.UtcNow;
                DateTime start, end;
                if (order.TimeCreate == DateTime.MinValue)
                {
                    // Start of today UTC
                    start = new DateTime(utcNow.Year, utcNow.Month, utcNow.Day, 0, 0, 0, DateTimeKind.Utc);
                }
                else
                {
                    start = order.TimeCreate.AddMinutes(-1).AddHours(-_timezoneOffset);
                }
                if (order.TimeDone == DateTime.MinValue)
                {
                    // End of today UTC
                    end = new DateTime(utcNow.Year, utcNow.Month, utcNow.Day, 23, 59, 59, 999, DateTimeKind.Utc);
                }
                else
                {
                    end = order.TimeDone.AddMinutes(1).AddHours(-_timezoneOffset);
                }
                var interval = new Interval
                {
                    StartTime = Timestamp.FromDateTime(DateTime.SpecifyKind(start, DateTimeKind.Utc)),
                    EndTime = Timestamp.FromDateTime(DateTime.SpecifyKind(end, DateTimeKind.Utc))
                };
                tradesResponse = _accountsClient.Trades(new TradesRequest { AccountId = _accountId, Interval = interval, Limit = 100 }, _gRpcMetadata);

                if (tradesResponse == null || tradesResponse.Trades.Count == 0) return null;

                List<MyTrade> trades = new List<MyTrade>();

                for (int i = 0; i < tradesResponse.Trades.Count; i++)
                {
                    MyTrade newTrade = ConvertToOSEngineTrade(tradesResponse.Trades[i]);

                    // Add only related trades
                    if (newTrade.NumberOrderParent == order.NumberMarket)
                    {
                        trades.Add(newTrade);
                    }
                }

                return trades.Count > 0 ? trades : null;
            }
            catch (RpcException rpcEx)
            {
                string msg = GetGRPCErrorMessage(rpcEx);
                SendLogMessage($"Get trades for order request error. Info: {msg}", LogMessageType.Error);
            }
            catch (Exception ex)
            {
                SendLogMessage($"Get get all orders request error: {ex.Message}", LogMessageType.Error);
            }

            return null;
        }

        public void ChangeOrderPrice(Order order, decimal newPrice) { }

        public event Action<Order> MyOrderEvent;

        private RateGate _rateGateMyOrderTradePlaceOrder = new RateGate(200, TimeSpan.FromMinutes(1));

        private RateGate _rateGateMyOrderTradeCancelOrder = new RateGate(200, TimeSpan.FromMinutes(1));

        private RateGate _rateGateMyOrderTradeGetOrders = new RateGate(200, TimeSpan.FromMinutes(1));

        private RateGate _rateGateMyOrderTradeGetOrder = new RateGate(200, TimeSpan.FromMinutes(1));

        private RateGate _rateGateAccountTrades = new RateGate(200, TimeSpan.FromMinutes(1));

        public List<Order> GetActiveOrders(int startIndex, int count)
        {
            return null;
        }

        public List<Order> GetHistoricalOrders(int startIndex, int count)
        {
            return null;
        }

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

        public void SetСonnected()
        {
            if (ServerStatus != ServerConnectStatus.Connect)
            {
                ServerStatus = ServerConnectStatus.Connect;
                ConnectEvent?.Invoke();
            }
        }

        private string GetGRPCErrorMessage(RpcException rpcEx)
        {
            return string.Format("{0}: {1}", rpcEx.Status.StatusCode, rpcEx.Status.Detail);
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
            if (trade == null || string.IsNullOrEmpty(trade.NumberOrderParent))
            {
                return;
            }

            MyTradeEvent?.Invoke(trade);
            return;
        }

        private bool InvokeMyOrderEvent(Order order)
        {
            if (order == null) return false;

            //MyOrderEvent?.Invoke(order);
            //return true;

            // Fix Finam возвращает список всех заявок за день
            // Выбираем только не обработанные

            if (_processedOrders.TryGetValue(order.NumberUser, out OrderStateType processedOrderState))
            {
                if (
                    (processedOrderState == order.State
                        && (processedOrderState != OrderStateType.Partial && processedOrderState != OrderStateType.Active && processedOrderState != OrderStateType.Done))
                    // Final states
                    //|| processedOrderState == OrderStateType.Done
                    || processedOrderState == OrderStateType.Cancel
                    || processedOrderState == OrderStateType.Fail
                    )
                {
                    // Skip order processing
                    return false;
                }
            }

            if (
                (order.State == OrderStateType.Done
                || order.State == OrderStateType.Partial)
                //&& processedOrderState.TradesIsComing == false
                )
            {
                //if (!order.TradesIsComing) { 
                List<MyTrade> tradesForMyOrder
                    = GetMyTradesForMyOrder(order);

                if (tradesForMyOrder != null && tradesForMyOrder.Count > 0)
                {
                    for (int i = tradesForMyOrder.Count - 1; i >= 0; i--)
                    {
                        order.SetTrade(tradesForMyOrder[i]);
                        InvokeMyTradeEvent(tradesForMyOrder[i]);
                    }
                    Thread.Sleep(5);
                }

                /*if (!order.TradesIsComing)
                {
                    // Содаем фейковый трейд
                    // Особенность АПИ (трейды могу запаздывать (не приходить?) относительно заявок)
                    SendLogMessage($"Create fake trade for order: {order.NumberUser}, state: {order.State}, trades count: {tradesForMyOrder.Count}.", LogMessageType.Error);
                    MyTrade fakeTrade = new MyTrade();
                    fakeTrade.Volume = order.VolumeExecute > 0 ? order.VolumeExecute : order.Volume;
                    fakeTrade.Price = order.Price;
                    fakeTrade.Side = order.Side;
                    fakeTrade.NumberTrade = (new Random()).Next(1, 1 ^ 10).ToString();
                    fakeTrade.NumberOrderParent = order.NumberMarket;
                    fakeTrade.SecurityNameCode = order.SecurityNameCode;
                    fakeTrade.Time = order.TimeCallBack;
                    InvokeMyTradeEvent(fakeTrade);
                    Thread.Sleep(50);
                    order.SetTrade(fakeTrade);
                }*/
            }

            //processedOrderState.TradesIsComing = order.TradesIsComing;
            _processedOrders.AddOrUpdate(order.NumberUser, processedOrderState, (key, oldValue) => processedOrderState);

            MyOrderEvent?.Invoke(order);
            //GetPortfolios();  // Обновляем портфель, в апи нет потока с обновлениями портфеля. Медленно
            return true;
        }

        private readonly ConcurrentDictionary<int, OrderStateType> _processedOrders = new ConcurrentDictionary<int, OrderStateType>();

        private void InvokeOrderFail(Order order)
        {
            order.State = OrderStateType.Fail;
            InvokeMyOrderEvent(order);
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

        public void SetLeverage(Security security, decimal leverage) { }

        #endregion

        #region 12 Log

        private void SendLogMessage(string msg, LogMessageType msgType)
        {
            LogMessageEvent?.Invoke(msg, msgType);
        }

        public event Action<string, LogMessageType> LogMessageEvent;

        public event Action<Funding> FundingUpdateEvent { add { } remove { } }

        public event Action<SecurityVolumes> Volume24hUpdateEvent { add { } remove { } }

        #endregion

        #region 13 Structures

        // Управление потоками чтения стримов
        private class TradesStreamReaderInfo
        {
            public AsyncServerStreamingCall<SubscribeLatestTradesResponse> Stream { get; set; }
            public CancellationTokenSource CancellationTokenSource { get; set; }
            //public Thread ReaderThread { get; set; }
        }

        private class OrderBookStreamReaderInfo
        {
            public AsyncServerStreamingCall<SubscribeOrderBookResponse> Stream { get; set; }
            public CancellationTokenSource CancellationTokenSource { get; set; }
            public Thread ReaderThread { get; set; }
        }

        #endregion
    }
}
