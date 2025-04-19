﻿/*
 *Your rights to use the code are governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 *Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using OsEngine.Entity;
using OsEngine.Logging;
using OsEngine.Market.Servers.AE.Json;
using OsEngine.Market.Servers.Entity;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using OsEngine.OsTrader;
using OsEngine.OsTrader.Panels;
using WebSocketSharp;
using OptionType = OsEngine.Entity.OptionType;
using Order = OsEngine.Entity.Order;
using Position = OsEngine.Entity.Position;
using System.Net;

namespace OsEngine.Market.Servers.AE
{
    public class AExchangeServer : AServer
    {
        public AExchangeServer()
        {
            AExchangeServerRealization realization = new AExchangeServerRealization();
            ServerRealization = realization;

            CreateParameterPath("Path to pfx key file"); //
            CreateParameterPassword("Key file passphrase", ""); //
            CreateParameterString("User name", ""); //
        }
    }

    public class AExchangeServerRealization : IServerRealization
    {
        #region 1 Constructor, Status, Connection

        public AExchangeServerRealization()
        {
            Thread messageReader = new Thread(DataMessageReader);
            messageReader.Name = "AEDataMessageReader";
            messageReader.Start();
        }

        public void Connect()
        {
            try
            {
                _securities.Clear();
                _myPortfolios.Clear();
                _subscribedSecurities.Clear();

                SendLogMessage("Start AE Connection", LogMessageType.System);

                _pathToKeyFile = ((ServerParameterPath)ServerParameters[0]).Value + "/trade.pfx";
              
                if (string.IsNullOrEmpty(_pathToKeyFile))
                {
                    SendLogMessage("Connection terminated. You must specify path to pfx file containing certificate. You can get pem certificate on the AE website and convert it to pfx format",
                        LogMessageType.Error);
                    return;
                }

                _keyFilePassphrase = ((ServerParameterPassword)ServerParameters[1]).Value;

                if (string.IsNullOrEmpty(_keyFilePassphrase))
                {
                    SendLogMessage("Connection terminated. You must specify passphrase to pfx file containing certificate. You can get it on the AE website",
                        LogMessageType.Error);
                    return;
                }

                _username = ((ServerParameterString)ServerParameters[2]).Value;

                if (string.IsNullOrEmpty(_username))
                {
                    SendLogMessage("Connection terminated. You must specify your username. You can get it on the AE website",
                        LogMessageType.Error);
                    return;
                }

                CreateWebSocketConnection();
            }
            catch (Exception ex)
            {
                SendLogMessage(ex.Message.ToString(), LogMessageType.Error);
            }
        }

        public void Dispose()
        {
            if (_ws != null)
            {
                SendCommand(new WebSocketMessageBase
                {
                    Type = "Logout",
                });

                SendLogMessage("Logout sent to AE", LogMessageType.System);

                _securities.Clear();
                _myPortfolios.Clear();

                DeleteWebSocketConnection();
            }
        }

        public DateTime ServerTime { get; set; }

        public ServerType ServerType => ServerType.AExchange;

        public ServerConnectStatus ServerStatus { get; set; } = ServerConnectStatus.Disconnect;

        public List<IServerParameter> ServerParameters { get; set; }

        public event Action ConnectEvent;

        public event Action DisconnectEvent;

        #endregion

        #region 2 Properties

        private readonly string _apiHost = "213.219.228.50"; // prod
        //private readonly int _apiPort = 21300; // prod
         private readonly int _apiPort = 21513; // game  

        private string _pathToKeyFile;
        private string _keyFilePassphrase;
        private string _username;
        private Dictionary<string, int> _orderNumbers = new Dictionary<string, int>();
        private Dictionary<string, Order> _sentOrders = new Dictionary<string, Order>();

        #endregion

        #region 3 Securities

        public void GetSecurities()
        {
            SendCommand(new WebSocketMessageBase
            {
                Type = "GetInstruments",
            });
        }

        private List<Security> _securities = new List<Security>();

        private void UpdateInstruments(string message)
        {
            // Cast to the derived class to access Instruments
            WebSocketInstrumentsMessage instrumentsMessage = JsonConvert.DeserializeObject<WebSocketInstrumentsMessage>(
                message, new JsonSerializerSettings
                {
                    ContractResolver = new CamelCasePropertyNamesContractResolver(),
                    NullValueHandling = NullValueHandling.Ignore,
                    Formatting = Formatting.Indented
                });
            List<InstrumentDefinition> instruments = instrumentsMessage.Instruments;

            foreach (InstrumentDefinition instrument in instruments)
            {
                Security newSecurity = new Security();

                if (instrument.Type == InstrumentType.Equity)
                {
                    newSecurity.SecurityType = SecurityType.Stock;
                }

                if (instrument.Type == InstrumentType.Futures)
                {
                    newSecurity.SecurityType = SecurityType.Futures;
                    newSecurity.UnderlyingAsset = instrument.Parent;
                }

                if (instrument.Type == InstrumentType.Option)
                {
                    newSecurity.SecurityType = SecurityType.Option;
                    newSecurity.UnderlyingAsset = instrument.Parent;
                    newSecurity.OptionType = instrument.OType == Json.OptionType.Call
                        ? OptionType.Call
                        : OptionType.Put;
                }

                if (instrument.Type == InstrumentType.Index)
                {
                    newSecurity.SecurityType = SecurityType.Index;
                }

                newSecurity.NameId = instrument.Ticker;
                newSecurity.Name = instrument.Ticker;
                newSecurity.NameClass = instrument.Type.ToString();
                newSecurity.NameFull = instrument.FullName.IsNullOrEmpty() ? instrument.Ticker : instrument.FullName;
                newSecurity.Exchange = "AE";
                newSecurity.PriceStep = instrument.PriceStep ?? 1;
                newSecurity.State = SecurityStateType.Activ;

                if (instrument.ExpDate != null)
                {
                    newSecurity.Expiration = instrument.ExpDate ?? DateTime.MaxValue;
                }

                newSecurity.Strike = instrument.Strike ?? 0;
                newSecurity.PriceStepCost = instrument.PSPrice ?? 1;
                newSecurity.Lot = instrument.LotVol ?? 1;

                newSecurity.DecimalsVolume = 0;

                _securities.Add(newSecurity);
            }

            SecurityEvent!(_securities);

            SendLogMessage($"Total instruments: {_securities.Count}", LogMessageType.System);
        }

        private void UpdateOrder(string type, string message)
        {
            Order order = new Order();

            string externalId = "";
            decimal sharesRemaining = 0.0m;
            
            if (type == "OrderPending")
            {
                order.State = OrderStateType.Active;

                OrderPendingMessage orderData = JsonConvert.DeserializeObject<OrderPendingMessage>(message, _jsonSettings);
                externalId = orderData.ExternalId;
                order.TimeCallBack = orderData.Moment;
                order.NumberMarket = orderData.OrderId;
            }
            else if (type == "OrderRejected")
            {
                order.State = OrderStateType.Fail;

                OrderRejectedMessage orderData = JsonConvert.DeserializeObject<OrderRejectedMessage>(message, _jsonSettings);
                externalId = orderData.ExternalId;
                order.NumberMarket = orderData.OrderId ?? "";

                SendLogMessage($"Order rejected. #{orderData.OrderId}. Message: {orderData.Message}", LogMessageType.Error);
            }
            else if (type == "OrderCanceled")
            {
                order.State = OrderStateType.Cancel;

                OrderCanceledMessage orderData = JsonConvert.DeserializeObject<OrderCanceledMessage>(message, _jsonSettings);
                externalId = orderData.ExternalId;
                order.TimeCallBack = orderData.Moment;
                order.NumberMarket = orderData.OrderId;
            }
            else if (type == "OrderFilled")
            {
                order.State = OrderStateType.Partial;

                OrderFilledMessage orderData = JsonConvert.DeserializeObject<OrderFilledMessage>(message, _jsonSettings);
                externalId = orderData.ExternalId;
                order.TimeCallBack = orderData.Moment;
                order.NumberMarket = orderData.OrderId;
                sharesRemaining = orderData.SharesRemaining;

                if (orderData.SharesRemaining == 0.0m)
                {
                    order.State = OrderStateType.Done;
                }
            }

            if (!_orderNumbers.ContainsKey(externalId)) // this order was sent not via our terminal
            {
                return;
            }

            Order origOrder = _sentOrders[externalId];
            order.NumberUser = origOrder.NumberUser;
            order.Side = origOrder.Side;
            order.PortfolioNumber = origOrder.PortfolioNumber;
            order.SecurityNameCode = origOrder.SecurityNameCode;
            order.SecurityClassCode = origOrder.SecurityClassCode;
            order.TypeOrder = origOrder.TypeOrder;
            order.Price = origOrder.Price;
            order.Volume = origOrder.Volume;

            if (order.State == OrderStateType.Partial)
            {
                order.VolumeExecute = order.Volume - sharesRemaining;
            }

            MyOrderEvent!(order);
        }

        DateTime _lastMDTime = DateTime.MinValue;

        private void UpdateQuote(string message)
        {
            WebSocketQuoteMessage q = JsonConvert.DeserializeObject<WebSocketQuoteMessage>(message, _jsonSettings);

            Security sec = _securities.Find((s) => s.NameId == q.Ticker);

            if (q.LastPrice != null) // quote is trade
            {
                Trade newTrade = new Trade();
                newTrade.Volume = Math.Abs(q.LastVolume ?? 0);
                newTrade.Time = q.LastTradeTime ?? DateTime.UtcNow;
                newTrade.Price = q.LastPrice ?? 0;
                newTrade.Id = q.Id.ToString();
                newTrade.SecurityNameCode = q.Ticker;
                newTrade.Side = q.LastVolume > 0 ? Side.Buy : Side.Sell;

                if (q.Ask != null)
                {
                    newTrade.Ask = q.Ask ?? 0;
                    newTrade.AsksVolume = q.AskVolume ?? 0;
                    newTrade.Bid = q.Bid ?? 0;
                    newTrade.BidsVolume = q.BidVolume ?? 0;

                    MarketDepth newMarketDepth = new MarketDepth();
                    MarketDepthLevel askLevel = new MarketDepthLevel();
                    askLevel.Ask = newTrade.AsksVolume;
                    askLevel.Price = newTrade.Ask;

                    MarketDepthLevel bidLevel = new MarketDepthLevel();
                    bidLevel.Bid = newTrade.BidsVolume;
                    bidLevel.Price = newTrade.Bid;

                    newMarketDepth.Asks.Add(askLevel);
                    newMarketDepth.Bids.Add(bidLevel);

                    newMarketDepth.SecurityNameCode = q.Ticker;
                    newMarketDepth.Time = DateTime.UtcNow;

                    if (newMarketDepth.Time == _lastMDTime)
                    {
                        newMarketDepth.Time = newMarketDepth.Time.AddTicks(1);
                    }

                    _lastMDTime = newMarketDepth.Time;

                    MarketDepthEvent!(newMarketDepth);
                }

                if (q.Volatility != null)
                {
                    OptionMarketDataForConnector data = new OptionMarketDataForConnector();

                    data.MarkIV = q.Volatility.ToString();
                    data.SecurityName = q.Ticker;
                    data.TimeCreate = q.Timestamp.ToString();
                    data.UnderlyingAsset = sec?.UnderlyingAsset ?? "";

                    AdditionalMarketDataEvent(data);
                }

                NewTradesEvent!(newTrade);
            }
        }

        private void UpdateAccountState(string message)
        {
            WebSocketAccountStateMessage account=
                JsonConvert.DeserializeObject<WebSocketAccountStateMessage>(message, _jsonSettings);

            Portfolio portf = null;

            for (int i = 0; i < _myPortfolios.Count; i++)
            {
                if (_myPortfolios[i].Number == account.AccountNumber)
                {
                    portf = _myPortfolios[i];
                    break;
                }
            }

            if (portf == null)
            {
                portf = new Portfolio();
                _myPortfolios.Add(portf);
            }

            portf.Number = account.AccountNumber;
            portf.ValueCurrent = account.Money ?? 0;
            portf.ValueBlocked = account.GuaranteeMargin ?? 0; 

            PortfolioEvent!(_myPortfolios);
        }

        private void UpdateMyTrade(string message)
        {
            WebSocketTradeMessage trade = JsonConvert.DeserializeObject<WebSocketTradeMessage>(message, _jsonSettings);
            MyTrade newTrade = new MyTrade();
            newTrade.SecurityNameCode = trade.Ticker;
            newTrade.NumberTrade = trade.TradeId;
            newTrade.NumberOrderParent = trade.OrderId;
            newTrade.Volume = Math.Abs(trade.Shares);
            newTrade.Price = trade.Price;
            newTrade.Time = trade.Moment;
            newTrade.Side = trade.Shares > 0 ? Side.Buy : Side.Sell;

            if (trade.TradeType != TradeType.Regular)
                return;

            MyTradeEvent!(newTrade);
        }


        private List<Position> GetPositionsInPortfolioByRobots(string portfolioName)
        {
            List<BotPanel> bots = OsTraderMaster.Master.PanelsArray;

            List<Position> openPositions = new List<Position>();

            if(bots == null)
            {
                return openPositions;
            }

            for (int i = 0; i < bots.Count; i++)
            {
                if (bots[i] == null)
                {
                    continue;
                }

                List<Position> curPositions = bots[i].OpenPositions;

                if (curPositions == null)
                {
                    continue;
                }

                for (int j = 0; j < curPositions.Count; j++)
                {
                    if (curPositions[j] == null)
                    {
                        continue;
                    }

                    string pName = curPositions[j].PortfolioName;

                    if (pName != null
                        && pName == portfolioName)
                    {
                        openPositions.Add(curPositions[j]);
                    }
                }
            }

            return openPositions;
        }

        private decimal calculateUnrealizedPnL(Portfolio portf)
        {
            decimal pnl = 0;

            if (portf == null)
                return 0;

            List<Position> positions = GetPositionsInPortfolioByRobots(portf.Number);

            for (int i = 0; i < positions.Count; i++)
            {
                Security sec = _securities.Find((s) => s.NameId == positions[i].SecurityName);

                if (sec  == null)
                    continue;

                pnl += positions[i].ProfitOperationAbs * sec.PriceStepCost;
            }

            return pnl;
        }

        private void UpdatePosition(string message)
        {
            WebSocketPositionUpdateMessage posMsg =
                JsonConvert.DeserializeObject<WebSocketPositionUpdateMessage>(message, _jsonSettings);

            Portfolio portf = null;

            for (int i = 0; i < _myPortfolios.Count; i++)
            {
                if (_myPortfolios[i].Number == posMsg.AccountNumber)
                {
                    portf = _myPortfolios[i];
                    break;
                }
            }

            PositionOnBoard newPosition = new PositionOnBoard();
            newPosition.SecurityNameCode = posMsg.Ticker;
            newPosition.ValueCurrent = posMsg.Shares;

            portf!.SetNewPosition(newPosition);
            portf.UnrealizedPnl = calculateUnrealizedPnL(portf);

            PortfolioEvent!(_myPortfolios);
        }

        private void UpdateAccounts(string message)
        {
            // Cast to the derived class to access Instruments
            WebSocketAccountsMessage accountsMessage = JsonConvert.DeserializeObject<WebSocketAccountsMessage>(message, _jsonSettings);
            List<Account> accounts = accountsMessage.Accounts;

            List<Order> orders = new List<Order>();

            foreach (var account in accounts)
            {
                Portfolio newPortfolio = new Portfolio();

                newPortfolio.Number = account.AccountNumber;
                newPortfolio.ValueBegin = 1;
                newPortfolio.ValueCurrent = account.Money;
                newPortfolio.ValueBlocked = account.GuaranteeMargin;
                newPortfolio.UnrealizedPnl = calculateUnrealizedPnL(newPortfolio);

                foreach (var position in account.Positions)
                {
                    PositionOnBoard newPosition = new PositionOnBoard();
                    newPosition.SecurityNameCode = position.Ticker;
                    newPosition.ValueCurrent = position.Shares;

                    newPortfolio.SetNewPosition(newPosition);
                }

                //foreach (var order in account.Orders)
                //{
                //    Order newOrder = new Order();

                //    newOrder.PortfolioNumber = newPortfolio.Number;
                //    newOrder.NumberMarket = order.OrderId.ToString();
                //    newOrder.SecurityNameCode = order.Ticker;
                //    newOrder.TimeCallBack = order.Placed;
                //    newOrder.Price = order.Price;
                //    newOrder.Volume = order.Shares;
                //    newOrder.VolumeExecute = order.Shares - order.SharesRemaining;
                //    if (order.Comment != null)
                //    {
                //        newOrder.Comment = order.Comment;
                //    }

                //    if (order.ExternalId != null)
                //    {
                //        newOrder.NumberUser = int.Parse(order.ExternalId);
                //    }

                //    orders.Add(newOrder);
                //}

                _myPortfolios.Add(newPortfolio);
            }

            PortfolioEvent!(_myPortfolios);

            // send all orders
            //foreach (var order in orders)
            //{
            //    MyOrderEvent!(order);
            //}
        }

        public event Action<List<Security>> SecurityEvent;

        #endregion

        #region 4 Portfolios

        private List<Portfolio> _myPortfolios = new List<Portfolio>();

        public void GetPortfolios()
        {
        }

        public event Action<List<Portfolio>> PortfolioEvent;

        #endregion

        #region 5 Data

        public List<Candle> GetLastCandleHistory(Security security, TimeFrameBuilder timeFrameBuilder, int candleCount)
        {
            return null; // not supported
        }

        public List<Candle> GetCandleDataToSecurity(Security security, TimeFrameBuilder timeFrameBuilder, DateTime startTime, DateTime endTime,
            DateTime actualTime)
        { 
            return null; // not supported
        }

        public List<Trade> GetTickDataToSecurity(Security security, DateTime startTime, DateTime endTime, DateTime actualTime)
        {
            return null; // not supported

        }

        #endregion

        #region 6 WebSocket creation

        private string _socketLocker = "webSocketLockerAE";

        private static long _messageId = 0;
        private static readonly JsonSerializerSettings _jsonSettings = new JsonSerializerSettings
        {
            ContractResolver = new CamelCasePropertyNamesContractResolver(),
            Formatting = Formatting.None,
            NullValueHandling = NullValueHandling.Ignore
        };

        private void SendCommand(WebSocketMessageBase command)
        {
            command.Id = Interlocked.Increment(ref _messageId);
            command.Timestamp = DateTime.UtcNow;

            string json = JsonConvert.SerializeObject(command, _jsonSettings);

            lock (_socketLocker)
            {
                _ws.Send(json);
            }
        }

        private enum SslProtocolsHack
        {
            Tls = 192,
            Tls11 = 768,
            Tls12 = 3072
        }

        private void CreateWebSocketConnection()
        {
            try
            {
                if (_ws != null)
                {
                    return;
                }

                _socketDataIsActive = false;
                _messageId = 0;

                lock (_socketLocker)
                {
                    _dataMessageQueue = new ConcurrentQueue<string>();

                    if (_certificate == null)
                    {
                        _certificate = new X509Certificate2(_pathToKeyFile, _keyFilePassphrase,
                            X509KeyStorageFlags.MachineKeySet | X509KeyStorageFlags.PersistKeySet);
                    }

                    _ws = new WebSocket($"wss://{_apiHost}:{_apiPort}/clientapi/v1");
                    _ws.SslConfiguration.ClientCertificateSelectionCallback =
                        (sender, targethost, localCertificates, remoteCertificate, acceptableIssuers) =>
                        {
                            return _certificate;
                        };

                    _ws.SslConfiguration.ClientCertificates = new X509CertificateCollection{_certificate};
                    // Add client certificate
                    //_ws.SslConfiguration.ClientCertificates.Add(_certificate);

                    // Set SSL/TLS protocol (adjust as needed)
                    _ws.SslConfiguration.EnabledSslProtocols = System.Security.Authentication.SslProtocols.Tls12;
                    _ws.SslConfiguration.CheckCertificateRevocation = false;

                    _ws.EmitOnPing = true;
                    _ws.OnOpen += WebSocketData_Opened;
                    _ws.OnClose += WebSocketData_Closed;
                    _ws.OnMessage += WebSocketData_MessageReceived;
                    _ws.OnError += WebSocketData_Error;

                    //_ws.OnClose += (sender, e) =>
                    //{
                    //    var sslProtocolHack = (System.Security.Authentication.SslProtocols)(SslProtocolsHack.Tls12 | SslProtocolsHack.Tls11 | SslProtocolsHack.Tls);
                    //    //TlsHandshakeFailure
                    //    if (e.Code == 1015 && _ws.SslConfiguration.EnabledSslProtocols != sslProtocolHack)
                    //    {
                    //        ws.SslConfiguration.EnabledSslProtocols = sslProtocolHack;
                    //        ws.Connect();
                    //    }
                    //};


                    try
                    {
                        _ws.Connect();
                    }
                    catch (WebSocketException ex)
                    {
                        SendLogMessage(ex.ToString(), LogMessageType.Error);
                    }
                }
            }
            catch (Exception exception)
            {
                SendLogMessage(exception.ToString(), LogMessageType.Error);
            }
        }

        private void DeleteWebSocketConnection()
        {
            try
            {
                lock (_socketLocker)
                {
                    if (_ws != null)
                    {
                        _ws.Close();
                    }
                }
            }
            catch
            {
            }
            finally
            {
            }
        }

        private bool _socketDataIsActive;

        private string _activationLocker = "activationLocker";

        private void CheckActivationSockets()
        {
            if (_socketDataIsActive == false)
            {
                return;
            }

            try
            {
                lock(_activationLocker)
                {
                    if (ServerStatus != ServerConnectStatus.Connect)
                    {
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

        private WebSocket _ws;
        private X509Certificate2 _certificate; 

        #endregion

        #region 7 WebSocket events

        private void WebSocketData_Opened(object sender, EventArgs e)
        {
            SendLogMessage("Socket activated", LogMessageType.System);
            _socketDataIsActive = true;

            if (_securities.Count == 0)
                GetSecurities();
            
            SendCommand(new WebSocketLoginMessage
            {
                Login = _username
            });

            SendLogMessage("Login sent to AE", LogMessageType.System);

            CheckActivationSockets();
        }

        private void WebSocketData_Closed(object sender, CloseEventArgs e)
        {
            var sslProtocolHack = (System.Security.Authentication.SslProtocols)(SslProtocolsHack.Tls12 | SslProtocolsHack.Tls11 | SslProtocolsHack.Tls);
            //TlsHandshakeFailure
            if (e.Code == 1015) // && _ws.SslConfiguration.EnabledSslProtocols != sslProtocolHack)
            {
                SendLogMessage($"Connection to AE closed unexpectedly Close code = {e.Code} with reason = {e.Reason}. Attempting reconnect.", LogMessageType.System);
                _ws.SslConfiguration.EnabledSslProtocols = sslProtocolHack;
                _ws.Connect();
                return;
            }

            try
            {
                SendLogMessage($"Connection to AE closed. Close code = {e.Code} with reason = {e.Reason}", LogMessageType.System);

                lock (_socketLocker)
                {
                    _ws.OnOpen -= WebSocketData_Opened;
                    _ws.OnClose -= WebSocketData_Closed;
                    _ws.OnMessage -= WebSocketData_MessageReceived;
                    _ws.OnError -= WebSocketData_Error;
                    _ws.SslConfiguration.ClientCertificates = null;
                    _ws.SslConfiguration.ClientCertificateSelectionCallback = null;

                    _ws = null;
                }

                if (ServerStatus != ServerConnectStatus.Disconnect)
                {
                    ServerStatus = ServerConnectStatus.Disconnect;
                    DisconnectEvent();
                }
            }
            catch (Exception ex)
            {
                SendLogMessage(ex.ToString(), LogMessageType.Error);
            }
        }

        private void WebSocketData_Error(object sender, WebSocketSharp.ErrorEventArgs error)
        {
            try
            {
                if (error.Exception != null)
                {
                    SendLogMessage(error.Exception.ToString(), LogMessageType.Error);
                }
            }
            catch (Exception ex)
            {
                SendLogMessage("Data socket error: " + ex.ToString(), LogMessageType.Error);
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

                if (e.Data.Length == 4)
                { // pong message
                    return;
                }

                if (_dataMessageQueue == null)
                {
                    return;
                }

                if (ServerStatus == ServerConnectStatus.Disconnect)
                {
                    return;
                }

                _dataMessageQueue.Enqueue(e.Data);
            }
            catch (Exception error)
            {
                SendLogMessage("AE websocket error. " + error.ToString(), LogMessageType.Error);
            }
        }

        #endregion

        #region 8 WebSocket Security subscription

        private RateGate _rateGateSubscribe = new RateGate(1, TimeSpan.FromMilliseconds(50));

        List<Security> _subscribedSecurities = new List<Security>();

        public void Subscrible(Security security)
        {
            try
            {
                for (int i = 0; i < _subscribedSecurities.Count; i++)
                {
                    if (_subscribedSecurities[i].Name == security.Name)
                    {
                        return;
                    }
                }

                _rateGateSubscribe.WaitToProceed();

                _subscribedSecurities.Add(security);

                SendCommand(new WebSocketSubscribeOnQuoteMessage
                {
                    Tickers = new List<string>{security.NameId}
                });
            }
            catch (Exception exception)
            {
                SendLogMessage(exception.ToString(),LogMessageType.Error);
            }
        }

        public bool SubscribeNews()
        {
            return false;
        }

        public event Action<News> NewsEvent;

        public event Action<OptionMarketDataForConnector> AdditionalMarketDataEvent;

        #endregion

        #region 9 WebSocket parsing the messages

        private ConcurrentQueue<string> _dataMessageQueue = new ConcurrentQueue<string>();

        private void DataMessageReader()
        {
            Thread.Sleep(1000);

            while (true)
            {
                try
                {
                    if (_dataMessageQueue.IsEmpty)
                    {
                        Thread.Sleep(1);
                        continue;
                    }

                    string message;

                    _dataMessageQueue.TryDequeue(out message);
                    
                    if (message == null)
                    {
                        continue;
                    }

                    if (message.Equals("pong"))
                    {
                        continue;
                    }

                    WebSocketMessageBase baseMessage = 
                        JsonConvert.DeserializeObject<WebSocketMessageBase>(message, _jsonSettings);

                    if (baseMessage == null)
                    {
                        continue;
                    }

                    if (baseMessage.Type == "Instruments")
                    {
                        UpdateInstruments(message);
                    } else if (baseMessage.Type == "Accounts")
                    {
                        UpdateAccounts(message);
                    } else if (baseMessage.Type == "Q")
                    {
                        UpdateQuote(message);
                    } else if (baseMessage.Type.StartsWith("Order"))
                    {
                        UpdateOrder(baseMessage.Type, message);
                    } else if (baseMessage.Type == "AccountState")
                    {
                        UpdateAccountState(message);
                    } else if (baseMessage.Type == "PositionUpdate")
                    {
                        UpdatePosition(message);
                    } else if (baseMessage.Type == "Trade")
                    {
                        UpdateMyTrade(message);
                    } else if (baseMessage.Type == "Error")
                    {
                        WebSocketErrorMessage errorMessage = JsonConvert.DeserializeObject<WebSocketErrorMessage>(message, _jsonSettings);

                        SendLogMessage($"Msg: {errorMessage.Message}, code: {errorMessage.Code}", LogMessageType.Error);
                    }
                    else
                    {
                        SendLogMessage(message, LogMessageType.System);
                    }
                }
                catch (Exception exception)
                {
                    SendLogMessage(exception.ToString(), LogMessageType.Error);
                    Thread.Sleep(5000);
                }
            }
        }

        public event Action<Trade> NewTradesEvent;

        public event Action<MarketDepth> MarketDepthEvent;

        public event Action<Order> MyOrderEvent;

        public event Action<MyTrade> MyTradeEvent;

        #endregion

        #region 11 Trade

        private RateGate _rateGateSendOrder = new RateGate(1, TimeSpan.FromMilliseconds(50));
        private RateGate _rateGateCancelOrder = new RateGate(1, TimeSpan.FromMilliseconds(50));

        public void SendOrder(Order order)
        {
            _rateGateSendOrder.WaitToProceed();

            try
            {
                // генерируем новый номер ордера и добавляем его в словарь
                Guid newUid = Guid.NewGuid();
                string orderId = newUid.ToString();

                _orderNumbers.Add(orderId, order.NumberUser);
                _sentOrders.Add(orderId, order);

                if (order.Volume <= 0)
                {
                    order.State = OrderStateType.Fail; // wtf?
                    
                    MyOrderEvent!(order);
                    SendLogMessage($"Order sending error: volume must be positive number. Volume: {order.Volume}", LogMessageType.Error);
                    return;
                }

                SendCommand(new WebSocketPlaceOrderMessage
                {
                    Account = order.PortfolioNumber,
                    ExternalId = orderId,
                    Ticker = order.SecurityNameCode,
                    Price = order.Price,
                    Shares = order.Side == Side.Buy ? order.Volume : -order.Volume,
                    Comment = order.Comment
                });
            }
            catch (Exception exception)
            {
                SendLogMessage("Order sending error " + exception.ToString(), LogMessageType.Error);
            }
        }

        /// <summary>
        /// Order price change
        /// </summary>
        /// <param name="order">An order that will have a new price</param>
        /// <param name="newPrice">New price</param>
        public void ChangeOrderPrice(Order order, decimal newPrice)
        {
        }

        public void CancelOrder(Order order)
        {
            _rateGateCancelOrder.WaitToProceed();

            try
            {
                SendCommand(new WebSocketCancelOrderMessage
                {
                    Account = order.PortfolioNumber,
                    OrderId = long.Parse(order.NumberMarket),
                    Ticker = order.SecurityNameCode
                });
            }
            catch (Exception exception)
            {
                SendLogMessage("Order cancel request error " + exception.ToString(), LogMessageType.Error);
            }
        }

        public void GetOrdersState(List<Order> orders)
        {

        }

        public void ResearchTradesToOrders(List<Order> orders)
        {

        }

        public void CancelAllOrders()
        {
            _rateGateCancelOrder.WaitToProceed();

            try
            {
                for (int i = 0; i < _myPortfolios.Count; i++)
                {
                    SendCommand(new WebSocketCancelOrderMessage
                    {
                        Account = _myPortfolios[i].Number
                    });
                }
            }
            catch (Exception exception)
            {
                SendLogMessage("Order cancel request error " + exception.ToString(), LogMessageType.Error);
            }
        }

        public void CancelAllOrdersToSecurity(Security security)
        {
            _rateGateCancelOrder.WaitToProceed();

            try
            {
                for (int i = 0; i < _myPortfolios.Count; i++)
                {
                    SendCommand(new WebSocketCancelOrderMessage
                    {
                        Account = _myPortfolios[i].Number,
                        Ticker = security.NameId
                    });
                }
            }
            catch (Exception exception)
            {
                SendLogMessage("Order cancel request error " + exception.ToString(), LogMessageType.Error);
            }
        }

        public void GetAllActivOrders()
        {
            SendCommand(new WebSocketMessageBase
            {
                Type = "GetAccounts"
            });
        }

        public void GetOrderStatus(Order order)
        {
        }

        #endregion

        #region 12 Helpers

        #endregion

        #region 13 Log

        private void SendLogMessage(string message, LogMessageType messageType)
        {
            LogMessageEvent?.Invoke(message, messageType);
        }

        public event Action<string, LogMessageType> LogMessageEvent;

        #endregion
    }
}