/*
 *Your rights to use the code are governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 *Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using OsEngine.Entity;
using OsEngine.Logging;
using OsEngine.Market.Servers.AE.Json;
using OsEngine.Market.Servers.Entity;
using RestSharp;
using System;
using System.IO;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using WebSocketSharp;
using System.Text;
using System.Threading.Tasks;
using OptionType = OsEngine.Entity.OptionType;
using Order = OsEngine.Entity.Order;
using System.Data;
using Position = OsEngine.Entity.Position;

namespace OsEngine.Market.Servers.AE
{
    public class AExchangeServer : AServer
    {
        public AExchangeServer()
        {
            AExchangeServerRealization realization = new AExchangeServerRealization();
            ServerRealization = realization;

            CreateParameterPath("Path to pem key file"); //
            CreateParameterPassword("Key file passphrase", ""); //
            CreateParameterString("User name", ""); //
        }
    }

    public class AExchangeServerRealization : IServerRealization
    {
        #region 1 Constructor, Status, Connection

        public AExchangeServerRealization()
        {
            //Thread worker = new Thread(ConnectionCheckThread);
            //worker.Name = "AECheckAlive";
            //worker.Start();

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
                _lastGetLiveTimeTokenTime = DateTime.MinValue;

                SendLogMessage("Start AE Connection", LogMessageType.System);

                _pathToKeyFile = ((ServerParameterPath)ServerParameters[0]).Value + "/trade.pfx";
              
                if (string.IsNullOrEmpty(_pathToKeyFile))
                {
                    SendLogMessage("Connection terminated. You must specify path to pem file containing certificate. You can get it on the AE website",
                        LogMessageType.Error);
                    return;
                }

                _keyFilePassphrase = ((ServerParameterPassword)ServerParameters[1]).Value;

                if (string.IsNullOrEmpty(_keyFilePassphrase))
                {
                    SendLogMessage("Connection terminated. You must specify passphrase to pem file containing certificate. You can get it on the AE website",
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

                SendCommand(new WebSocketLoginMessage
                {
                    Id = Interlocked.Increment(ref _messageId),
                    Timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                    Login = _username
                });
            }
            catch (Exception ex)
            {
                SendLogMessage(ex.Message.ToString(), LogMessageType.Error);
            }
        }

        private void ConnectionCheckThread()
        {
            while (true)
            {
                Thread.Sleep(10000);

                if (ServerStatus != ServerConnectStatus.Connect)
                {
                    continue;
                }

                //if (_lastGetLiveTimeTokenTime.AddMinutes(20) < DateTime.Now)
                //{
                //    if (GetCurSessionToken() == false)
                //    {
                //        if (ServerStatus == ServerConnectStatus.Connect)
                //        {
                //            ServerStatus = ServerConnectStatus.Disconnect;
                //            DisconnectEvent();
                //        }
                //    }
                //}
            }
        }

        DateTime _lastGetLiveTimeTokenTime = DateTime.MinValue;

        public void Dispose()
        {
            if (_ws != null)
            {
                SendCommand(new WebSocketMessageBase
                {
                    Id = Interlocked.Increment(ref _messageId),
                    Timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                    Type = "Logout",
                });
            }

            _securities.Clear();
            _myPortfolios.Clear();
            _lastGetLiveTimeTokenTime = DateTime.MinValue;

            DeleteWebSocketConnection();

            SendLogMessage("Connection Closed by AE. WebSocket Data Closed Event", LogMessageType.System);

            if (ServerStatus != ServerConnectStatus.Disconnect)
            {
                ServerStatus = ServerConnectStatus.Disconnect;
                DisconnectEvent();
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

        private readonly string _apiHost = "213.219.228.50";
        private readonly int _apiPort = 21300;
        private string _pathToKeyFile;
        private string _keyFilePassphrase;
        private string _username;
        private Dictionary<string, int> _orderNumbers = new Dictionary<string, int>();

        #endregion

        #region 3 Securities

        public void GetSecurities()
        {
            SendCommand(new WebSocketMessageBase
            {
                Id = Interlocked.Increment(ref _messageId),
                Timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                Type = "GetInstruments",
            });

            //string apiEndpoint;

            //if (_useStock || _useOther)
            //{
            //    apiEndpoint = $"/md/v2/Securities/MOEX?format=Simple&market=FOND&includeOld=false";
            //    UpdateSec(apiEndpoint);
            //}

            //if (_useCurrency)
            //{
            //    apiEndpoint = $"/md/v2/Securities/MOEX?format=Simple&market=CURR&includeOld=false";
            //    UpdateSec(apiEndpoint);
            //}

            //if (_useFutures)
            //{
            //    apiEndpoint = $"/md/v2/Securities/MOEX?format=Simple&market=FORTS&includeOld=false";
            //    UpdateSec(apiEndpoint);
            //}

            //if (_useOptions)
            //{
            //    apiEndpoint = $"/md/v2/Securities/MOEX?format=Simple&market=SPBX&includeOld=false";
            //    UpdateSec(apiEndpoint);
            //}

            if (_securities.Count > 0)
            {
                SendLogMessage("Securities loaded. Count: " + _securities.Count, LogMessageType.System);

                if (SecurityEvent != null)
                {
                    SecurityEvent.Invoke(_securities);
                }
            }

        }

        private List<Security> _securities = new List<Security>();

        private void UpdateSec(string endPoint)
        {
            //curl - X GET "https://api.AE.ru/md/v2/Securities/MOEX?format=Simple&market=FOND&includeOld=false" - H "accept: application/json"

            try
            {
                //RestRequest requestRest = new RestRequest(endPoint, Method.GET);
                //requestRest.AddHeader("Authorization", "Bearer " + _apiTokenReal);
                //RestClient client = new RestClient(_restApiHost);
                //IRestResponse response = client.Execute(requestRest);

                //if (response.StatusCode == HttpStatusCode.OK)
                //{
                //    string content = response.Content;
                //    List<AESecurity> securities = JsonConvert.DeserializeAnonymousType(content, new List<AESecurity>());
                //    UpdateSecuritiesFromServer(securities);
                //}
                //else
                //{
                //    SendLogMessage("Securities request error. Status: " + response.StatusCode, LogMessageType.Error);
                //}
            }
            catch (Exception exception)
            {
                SendLogMessage("Securities request error" + exception.ToString(), LogMessageType.Error);
            }
        }

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
                newSecurity.NameFull = instrument.FullName;
                newSecurity.Exchange = "AE";
                newSecurity.PriceStep = instrument.PriceStep ?? 1;

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
            
            if (type == "OrderPending")
            {
                order.State = OrderStateType.Pending;

                OrderPendingMessage orderData = JsonConvert.DeserializeObject<OrderPendingMessage>(message, _jsonSettings);
                externalId = orderData.ExternalId;
                order.TimeCallBack = orderData.Moment;
                order.NumberMarket = orderData.OrderId.ToString();
            }
            else if (type == "OrderRejected")
            {
                order.State = OrderStateType.Fail;

                OrderRejectedMessage orderData = JsonConvert.DeserializeObject<OrderRejectedMessage>(message, _jsonSettings);
                order.TimeCallBack = orderData.Moment;
                order.NumberMarket = orderData.OrderId.ToString();

                SendLogMessage($"Order rejected. #{orderData.OrderId}. Message: {orderData.Message}", LogMessageType.Error);
            }
            else if (type == "OrderCanceled")
            {
                order.State = OrderStateType.Cancel;

                OrderCanceledMessage orderData = JsonConvert.DeserializeObject<OrderCanceledMessage>(message, _jsonSettings);
                order.TimeCallBack = orderData.Moment;
                order.NumberMarket = orderData.OrderId.ToString();
            }
            else if (type == "OrderFilled")
            {
                order.State = OrderStateType.Partial;

                OrderFilledMessage orderData = JsonConvert.DeserializeObject<OrderFilledMessage>(message, _jsonSettings);
                order.TimeCallBack = orderData.Moment;
                order.NumberMarket = orderData.OrderId.ToString();

                if (orderData.SharesRemaining == 0.0m)
                {
                    order.State = OrderStateType.Done;
                }
            }

            if (!_orderNumbers.ContainsKey(externalId)) // this order was sent not via our terminal
            {
                return;
            }

            order.NumberUser = _orderNumbers[externalId];

            MyOrderEvent!(order);
        }

        private void UpdateQuote(string message)
        {
            WebSocketQuoteMessage q = JsonConvert.DeserializeObject<WebSocketQuoteMessage>(message, _jsonSettings);

            if (q.LastPrice != null) // quote is trade
            {
                Trade newTrade = new Trade();
                newTrade.Volume = q.LastVolume ?? 0;
                newTrade.Time = q.LastTradeTime ?? DateTime.UtcNow;
                newTrade.Price = q.LastPrice ?? 0;
                newTrade.Id = q.Id.ToString();
                newTrade.SecurityNameCode = q.Ticker;

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
                    newMarketDepth.Time = newTrade.Time;

                    MarketDepthEvent!(newMarketDepth);
                }

                //q.Volatility ???

                NewTradesEvent!(newTrade);
            }

        }

        private void UpdateAccountState(string message)
        {
            WebSocketAccountStateMessage account=
                JsonConvert.DeserializeObject<WebSocketAccountStateMessage>(message, _jsonSettings);

            Portfolio newPortfolio = new Portfolio();
            newPortfolio.Number = account.AccountNumber;
            newPortfolio.ValueBlocked = account.GuaranteeMargin ?? 0; // это неправильно, так как не учитывает цену всех позиций?

            PortfolioEvent!(new List<Portfolio>{newPortfolio});
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
                newPortfolio.ValueBlocked = account.GuaranteeMargin;// это неправильно, так как не учитывает цену всех позиций?

                foreach (var position in account.Positions)
                {
                    PositionOnBoard newPosition = new PositionOnBoard();
                    newPosition.SecurityNameCode = position.Ticker;
                    newPosition.ValueCurrent = position.Shares;

                    newPortfolio.SetNewPosition(newPosition);
                }

                foreach (var order in account.Orders)
                {
                    Order newOrder = new Order();

                    newOrder.PortfolioNumber = newPortfolio.Number;
                    newOrder.NumberMarket = order.OrderId.ToString();
                    newOrder.SecurityNameCode = order.Ticker;
                    newOrder.TimeCallBack = order.Placed;
                    newOrder.Price = order.Price;
                    newOrder.Volume = order.Shares;
                    newOrder.VolumeExecute = order.Shares - order.SharesRemaining;
                    if (order.Comment != null)
                    {
                        newOrder.Comment = order.Comment;
                    }

                    if (order.ExternalId != null)
                    {
                        newOrder.NumberUser = int.Parse(order.ExternalId);
                    }

                    orders.Add(newOrder);
                }

                _myPortfolios.Add(newPortfolio);
            }

            PortfolioEvent!(_myPortfolios);

            // send all orders
            foreach (var order in orders)
            {
                MyOrderEvent!(order);
            }
        }


        //private void UpdateSecuritiesFromServer(List<AESecurity> stocks)
        //{
        //    try
        //    {
        //        if (stocks == null ||
        //            stocks.Count == 0)
        //        {
        //            return;
        //        }

        //        for(int i = 0;i < stocks.Count;i++)
        //        {
        //            AESecurity item = stocks[i];

        //            if(item.symbol == "IMOEX2")
        //            {

        //            }

        //            SecurityType instrumentType = GetSecurityType(item);

        //            if (!CheckNeedSecurity(instrumentType))
        //            {
        //                continue;
        //            }

        //            if(instrumentType == SecurityType.None)
        //            {
        //                continue;
        //            }
                   
        //            Security newSecurity = new Security();
        //            newSecurity.SecurityType = instrumentType;
        //            newSecurity.Exchange = item.exchange;
        //            newSecurity.DecimalsVolume = 0;
        //            newSecurity.Lot = item.lotsize.ToDecimal();
        //            newSecurity.Name = item.symbol;
        //            newSecurity.NameFull = item.symbol + "_" + item.board;

        //            if (newSecurity.SecurityType == SecurityType.Option)
        //            {
                        
        //                newSecurity.Go = item.marginbuy.ToDecimal();

        //                if(item.type != null &&
        //                    item.type.Contains("Прем. европ. Call "))
        //                {
        //                    newSecurity.NameClass = "Option_Eur";
        //                    newSecurity.OptionType = OptionType.Call;
        //                    string strike = item.type.Replace("Прем. европ. Call ", "");
        //                    strike = strike.Split(' ')[0];
        //                    newSecurity.Strike = strike.ToDecimal();
        //                }
        //                else if (item.type != null &&
        //                    item.type.Contains("Нед. прем. европ. Call "))
        //                {
        //                    newSecurity.NameClass = "Option_Eur";
        //                    newSecurity.OptionType = OptionType.Call;
        //                    string strike = item.type.Replace("Нед. прем. европ. Call ", "");
        //                    strike = strike.Split(' ')[0];
        //                    newSecurity.Strike = strike.ToDecimal();
        //                }
        //                else if (item.type != null &&
        //                    item.type.Contains("Прем. европ. Put "))
        //                {
        //                    newSecurity.NameClass = "Option_Eur";
        //                    newSecurity.OptionType = OptionType.Put;
        //                    string strike = item.type.Replace("Прем. европ. Put ", "");
        //                    strike = strike.Split(' ')[0];
        //                    newSecurity.Strike = strike.ToDecimal();
        //                }
        //                else if (item.type != null &&
        //                    item.type.Contains("Нед. прем. европ. Put "))
        //                {
        //                    newSecurity.NameClass = "Option_Eur";
        //                    newSecurity.OptionType = OptionType.Put;
        //                    string strike = item.type.Replace("Нед. прем. европ. Put ", "");
        //                    strike = strike.Split(' ')[0];
        //                    newSecurity.Strike = strike.ToDecimal();
        //                }
        //                else if (item.type != null &&
        //                    item.type.Contains("Нед. марж. амер. Call "))
        //                {
        //                    newSecurity.NameClass = "Option_Us";
        //                    newSecurity.OptionType = OptionType.Call;
        //                    string strike = item.type.Replace("Нед. марж. амер. Call ", "");
        //                    strike = strike.Split(' ')[0];
        //                    newSecurity.Strike = strike.ToDecimal();
        //                }
        //                else if (item.type != null &&
        //                    item.type.Contains("Марж. амер. Call "))
        //                {
        //                    newSecurity.NameClass = "Option_Us";
        //                    newSecurity.OptionType = OptionType.Call;
        //                    string strike = item.type.Replace("Марж. амер. Call ", "");
        //                    strike = strike.Split(' ')[0];
        //                    newSecurity.Strike = strike.ToDecimal();
        //                }
        //                else if (item.type != null &&
        //                    item.type.Contains("Марж. амер. Put "))
        //                {
        //                    newSecurity.NameClass = "Option_Us";
        //                    newSecurity.OptionType = OptionType.Put;
        //                    string strike = item.type.Replace("Марж. амер. Put ", "");
        //                    strike = strike.Split(' ')[0];
        //                    newSecurity.Strike = strike.ToDecimal();
        //                }
        //                else if (item.type != null &&
        //                    item.type.Contains("Нед. марж. амер. Put "))
        //                {
        //                    newSecurity.NameClass = "Option_Us";
        //                    newSecurity.OptionType = OptionType.Put;
        //                    string strike = item.type.Replace("Нед. марж. амер. Put ", "");
        //                    strike = strike.Split(' ')[0];
        //                    newSecurity.Strike = strike.ToDecimal();
        //                }
        //                else
        //                {

        //                }

        //            }
        //            else if (item.type == null)
        //            {
        //                if(item.description.StartsWith("Индекс"))
        //                {
        //                    newSecurity.NameClass = "Index";
        //                    newSecurity.SecurityType = SecurityType.Index;
        //                }
        //                else
        //                {
        //                    newSecurity.NameClass = "Unknown";
        //                    newSecurity.SecurityType = SecurityType.None;
        //                }
        //            }
        //            else if (item.type.StartsWith("Календарный спред"))
        //            {
        //                newSecurity.NameClass = "Futures spread";
        //            }
        //            else if (newSecurity.SecurityType == SecurityType.Futures)
        //            {
        //                newSecurity.NameClass = "Futures";
        //                newSecurity.Go = item.marginbuy.ToDecimal();
        //            }
        //            else if (newSecurity.SecurityType == SecurityType.CurrencyPair)
        //            {
        //                newSecurity.NameClass = "Currency";
        //            }
        //            else if (item.type == "CS")
        //            {
        //                if (item.board == "TQBR")
        //                {
        //                    newSecurity.NameClass = "Stock";
        //                }
        //                else if (item.board == "FQBR")
        //                {
        //                    newSecurity.NameClass = "Stock World";
        //                }
        //                else 
        //                {
        //                    newSecurity.NameClass = "Stock";
        //                }
        //            }
		      //      else if (item.type == "CORP")
        //            {
        //                newSecurity.NameClass = "Bond";
        //            }
        //            else if (item.type == "PS")
        //            {
        //                newSecurity.NameClass = "Stock";
        //            }
        //            else if (newSecurity.SecurityType == SecurityType.Fund)
        //            {
        //                newSecurity.NameClass = "Fund";
        //            }
        //            else
        //            {
        //                newSecurity.NameClass = item.type;
        //            }

        //            if (string.IsNullOrEmpty(item.cancellation) == false 
        //                && (newSecurity.SecurityType == SecurityType.Futures ||
        //                newSecurity.SecurityType == SecurityType.Option ||
        //                newSecurity.NameClass == "Futures spread"))
        //            {
        //                int year = Convert.ToInt32(item.cancellation.Substring(0, 4));
        //                int month = Convert.ToInt32(item.cancellation.Substring(5, 2));
        //                int day = Convert.ToInt32(item.cancellation.Substring(8, 2));

        //                newSecurity.Expiration = new DateTime(year, month, day);
        //            }

        //            newSecurity.NameId = item.shortname;
                   
        //            newSecurity.Decimals = GetDecimals(item.minstep.ToDecimal());
        //            newSecurity.PriceStep = item.minstep.ToDecimal();
        //            newSecurity.PriceStepCost = newSecurity.PriceStep;
        //            newSecurity.State = SecurityStateType.Activ;

        //            if (newSecurity.SecurityType == SecurityType.Futures 
        //                || newSecurity.SecurityType == SecurityType.Option)
        //            {
        //                newSecurity.PriceStepCost = item.pricestep.ToDecimal();

        //                if(newSecurity.PriceStepCost <= 0)
        //                {
        //                    newSecurity.PriceStepCost = newSecurity.PriceStep;
        //                }
        //            }

        //            if(string.IsNullOrEmpty(item.priceMax) == false)
        //            {
        //                newSecurity.PriceLimitHigh = item.priceMax.ToDecimal();
        //            }
        //            if (string.IsNullOrEmpty(item.priceMin) == false)
        //            {
        //                newSecurity.PriceLimitLow = item.priceMin.ToDecimal();
        //            }

        //             _securities.Add(newSecurity);
        //        }  
        //    }
        //    catch (Exception e)
        //    {
        //        SendLogMessage($"Error loading stocks: {e.Message}", LogMessageType.Error);
        //    }
        //}

        //private SecurityType GetSecurityType(AESecurity security)
        //{
        //    var cfiCode = security.cfiCode;

        //    if (cfiCode.StartsWith("F"))
        //    {
        //        return SecurityType.Futures;
        //    }
        //    else if (cfiCode.StartsWith("O"))
        //    {
        //        return SecurityType.Option;
        //    }
        //    else if (cfiCode.StartsWith("ES") || cfiCode.StartsWith("EP"))
        //    {
        //        return SecurityType.Stock;
        //    }
        //    else if (cfiCode.StartsWith("DB"))
        //    { 
        //        return SecurityType.Bond; 
        //    }
        //    else if(cfiCode.StartsWith("EUX"))
        //    {
        //        return SecurityType.Fund;
        //    }
        //    else if(security.description.Contains("Индекс"))
        //    {
        //        return SecurityType.Index;
        //    }

        //    var board = security.board;
        //    if (board == "CETS") return SecurityType.CurrencyPair;

        //    return SecurityType.None;
        //}

        //private bool CheckNeedSecurity(SecurityType instrumentType)
        //{
        //    switch (instrumentType)
        //    {
        //        case SecurityType.Stock when _useStock:
        //        case SecurityType.Futures when _useFutures:
        //        case SecurityType.Option when _useOptions:
        //        case SecurityType.CurrencyPair when _useCurrency:
        //        case SecurityType.None when _useOther:
        //        case SecurityType.Bond when _useOther:
        //        case SecurityType.Index when _useOther:
        //        case SecurityType.Fund when _useOther:
        //            return true;
        //        default:
        //            return false;
        //    }
        //}

        private int GetDecimals(decimal x)
        {
            var precision = 0;
            while (x * (decimal)Math.Pow(10, precision) != Math.Round(x * (decimal)Math.Pow(10, precision)))
                precision++;
            return precision;
        }

        public event Action<List<Security>> SecurityEvent;

        #endregion

        #region 4 Portfolios

        private List<Portfolio> _myPortfolios = new List<Portfolio>();

        public void GetPortfolios()
        {
            //if(string.IsNullOrEmpty(_portfolioSpotId) == false)
            //{
            //    GetCurrentPortfolio(_portfolioSpotId, "SPOT");
            //}

            //if (string.IsNullOrEmpty(_portfolioFutId) == false)
            //{
            //    GetCurrentPortfolio(_portfolioFutId, "FORTS");
            //}

            //if (string.IsNullOrEmpty(_portfolioCurrencyId) == false)
            //{
            //    GetCurrentPortfolio(_portfolioCurrencyId, "CURR");
            //}

            //if (string.IsNullOrEmpty(_portfolioSpareId) == false)
            //{
            //    GetCurrentPortfolio(_portfolioSpareId, "SPARE");
            //}

            if(_myPortfolios.Count != 0)
            {
                if(PortfolioEvent != null)
                {
                    PortfolioEvent(_myPortfolios);
                }
            }

            ActivatePortfolioSocket();
        }

        private void GetCurrentPortfolio(string portfolioId, string namePrefix)
        {
            try
            {
                //string exchange = "MOEX";
                //if (portfolioId.StartsWith("E"))
                //{
                //    exchange = "UNITED";
                //}

                //string endPoint = $"/md/v2/clients/{exchange}/{portfolioId}/summary?format=Simple";
                //RestRequest requestRest = new RestRequest(endPoint, Method.GET);
                //requestRest.AddHeader("Authorization", "Bearer " + _apiTokenReal);
                //requestRest.AddHeader("accept", "application/json");

                //RestClient client = new RestClient(_restApiHost);

                //IRestResponse response = client.Execute(requestRest);

                //if (response.StatusCode == HttpStatusCode.OK)
                //{
                //    string content = response.Content;
                //    AEPortfolioRest portfolio = JsonConvert.DeserializeAnonymousType(content, new AEPortfolioRest());

                //    ConvertToPortfolio(portfolio, portfolioId, namePrefix);
                //}
                //else
                //{
                //    SendLogMessage("Portfolio request error. Status: " 
                //        + response.StatusCode + "  " + namePrefix, LogMessageType.Error);
                //}
            }
            catch (Exception exception)
            {
                SendLogMessage("Portfolio request error " + exception.ToString(), LogMessageType.Error);
            }
        }

        //private void ConvertToPortfolio(AEPortfolioRest portfolio, string name, string prefix)
        //{
        //    Portfolio newPortfolio = new Portfolio();
        //    newPortfolio.Number = name + "_" + prefix;
        //    newPortfolio.ValueCurrent = portfolio.buyingPower.ToDecimal();
        //    newPortfolio.UnrealizedPnl = portfolio.profit.ToDecimal();
        //    _myPortfolios.Add(newPortfolio);
        //}

        public event Action<List<Portfolio>> PortfolioEvent;

        #endregion

        #region 5 Data

        public List<Candle> GetLastCandleHistory(Security security, TimeFrameBuilder timeFrameBuilder, int candleCount)
        {
            DateTime endTime = DateTime.Now.ToUniversalTime();

            while(endTime.Hour != 23)
            {
                endTime = endTime.AddHours(1);
            }

            int candlesInDay = 0;

            if(timeFrameBuilder.TimeFrameTimeSpan.TotalMinutes >= 1)
            {
                candlesInDay = 900 / Convert.ToInt32(timeFrameBuilder.TimeFrameTimeSpan.TotalMinutes);
            }
            else
            {
                candlesInDay = 54000/ Convert.ToInt32(timeFrameBuilder.TimeFrameTimeSpan.TotalSeconds);
            }

            if(candlesInDay == 0)
            {
                candlesInDay = 1;
            }

            int daysCount = candleCount / candlesInDay;

            if(daysCount == 0)
            {
                daysCount = 1;
            }

            daysCount++;

            if(daysCount > 5)
            { // добавляем выходные
                daysCount = daysCount + (daysCount / 5) * 2;
            }

            DateTime startTime = endTime.AddDays(-daysCount);

            if (endTime.DayOfWeek == DayOfWeek.Monday)
            {
                startTime = startTime.AddDays(-2);
            }
            if (endTime.DayOfWeek == DayOfWeek.Tuesday)
            {
                startTime = startTime.AddDays(-1);
            }

            List<Candle> candles = GetCandleDataToSecurity(security, timeFrameBuilder, startTime, endTime, startTime);
        
            while(candles.Count > candleCount)
            {
                candles.RemoveAt(0);
            }

            return candles;
        }

        public List<Candle> GetCandleDataToSecurity(Security security, TimeFrameBuilder timeFrameBuilder, DateTime startTime, DateTime endTime,
            DateTime actualTime)
        { 
            if(startTime != actualTime)
            {
                startTime = actualTime;
            }

            DateTime requestedStartTime = startTime;

            List<Candle> candles = new List<Candle>();

            TimeSpan additionTime = TimeSpan.FromMinutes(timeFrameBuilder.TimeFrameTimeSpan.TotalMinutes * 2500);

            DateTime endTimeReal = startTime.Add(additionTime);

            if (endTimeReal > endTime) 
                endTimeReal = endTime;

            //while (startTime < endTime)
            //{
            //    CandlesHistoryAE history = GetHistoryCandle(security, timeFrameBuilder, startTime, endTimeReal);

            //    List<Candle> newCandles = ConvertToOsEngineCandles(history);

            //    if(newCandles != null &&
            //        newCandles.Count > 0)
            //    {
            //        candles.AddRange(newCandles);
            //    }

            //    if(string.IsNullOrEmpty(history.prev) 
            //        && string.IsNullOrEmpty(history.next))
            //    {// на случай если указаны очень старые данные, и их там нет
            //        startTime = startTime.Add(additionTime);
            //        endTimeReal = startTime.Add(additionTime);
            //        continue;
            //    }

            //    if (string.IsNullOrEmpty(history.next))
            //    {
            //        break;
            //    }

            //    DateTime realStart = ConvertToDateTimeFromUnixFromSeconds(history.next);

            //    startTime = realStart;
            //    endTimeReal = realStart.Add(additionTime);

            //    if (endTimeReal > endTime)
            //        endTimeReal = endTime;
            //}

            //while (candles != null &&
            //    candles.Count != 0 && 
            //    candles[candles.Count - 1].TimeStart > endTime)
            //{
            //    candles.RemoveAt(candles.Count - 1);
            //}

            //while (candles != null &&
            //    candles.Count != 0 && 
            //    candles[0].TimeStart < requestedStartTime)
            //{
            //    candles.RemoveAt(0);
            //}

            return candles;
        }

        //private CandlesHistoryAE GetHistoryCandle(Security security, TimeFrameBuilder timeFrameBuilder, DateTime startTime, DateTime endTime)
        //{
        //    // curl -X GET "https://api.AE.ru/md/v2/history?symbol=SBER&exchange=MOEX&tf=60&from=1549000661&to=1550060661&format=Simple" -H "accept: application/json"

        //    string endPoint = "md/v2/history?symbol=" + security.Name;
        //    endPoint += "&exchange=MOEX";

        //    //Начало отрезка времени (UTC) в формате Unix Time Seconds

        //    endPoint += "&tf=" + GetAETf(timeFrameBuilder);
        //    endPoint += "&from=" + ConvertToUnixTimestamp(startTime);
        //    endPoint += "&to=" + ConvertToUnixTimestamp(endTime);
        //    endPoint += "&format=Simple";

        //    try
        //    {
        //        RestRequest requestRest = new RestRequest(endPoint, Method.GET);
        //        requestRest.AddHeader("accept", "application/json");
        //        requestRest.AddHeader("Authorization", "Bearer " + _apiTokenReal);
        //        RestClient client = new RestClient(_restApiHost);
        //        IRestResponse response = client.Execute(requestRest);

        //        if (response.StatusCode == HttpStatusCode.OK)
        //        {
        //            string content = response.Content;
        //            CandlesHistoryAE candles = JsonConvert.DeserializeAnonymousType(content, new CandlesHistoryAE());
        //            return candles;
        //        }
        //        else
        //        {
        //            SendLogMessage("Candles request error. Status: " + response.StatusCode, LogMessageType.Error);
        //        }
        //    }
        //    catch (Exception exception)
        //    {
        //        SendLogMessage("Candles request error" + exception.ToString(), LogMessageType.Error);
        //    }
        //    return null;
        //}

        //private List<Candle> ConvertToOsEngineCandles(CandlesHistoryAE candles)
        //{
        //    List<Candle> result = new List<Candle>();

        //    if(candles == null 
        //        || candles.history == null 
        //        || candles.history.Count == 0)
        //    {
        //        return result;
        //    }

        //    for(int i = 0;i < candles.history.Count;i++)
        //    {
        //        if(candles.history[i] == null)
        //        {
        //            continue;
        //        }

        //        AECandle curCandle = candles.history[i];

        //        Candle newCandle = new Candle();
        //        newCandle.Open = curCandle.open.ToDecimal();
        //        newCandle.High = curCandle.high.ToDecimal();
        //        newCandle.Low = curCandle.low.ToDecimal();
        //        newCandle.Close = curCandle.close.ToDecimal();
        //        newCandle.Volume = curCandle.volume.ToDecimal();
        //        newCandle.TimeStart = ConvertToDateTimeFromUnixFromSeconds(curCandle.time);

        //        result.Add(newCandle);
        //    }

        //    return result;
        //}

        private string GetAETf(TimeFrameBuilder timeFrameBuilder)
        {
            //Длительность таймфрейма в секундах или код ("D" - дни, "W" - недели, "M" - месяцы, "Y" - годы)
            // 15
            // 60
            // 300
            // 3600
            // D - Day
            // W - Week
            // M - Month
            // Y - Year

            string result = "";

            if(timeFrameBuilder.TimeFrame == TimeFrame.Day)
            {
                result = "D";
            }
            else
            {
                result = timeFrameBuilder.TimeFrameTimeSpan.TotalSeconds.ToString();
            }

            return result;
        }

        public List<Trade> GetTickDataToSecurity(Security security, DateTime startTime, DateTime endTime, DateTime actualTime)
        {
            return null; // так как указано, что данные не поддерживаются

        }

        //private TradesHistoryAE GetHistoryTrades(Security security, DateTime startTime, DateTime endTime)
        //{
        //    // /md/v2/Securities/MOEX/SBER/alltrades/history?instrumentGroup=TQBR&from=1593430060&to=1593430560&limit=100&offset=10&format=Simple

        //    string endPoint = "/md/v2/Securities/MOEX/" + security.Name;
        //    endPoint += "/alltrades/history?";

        //    endPoint += "instrumentGroup=" + security.NameFull.Split('_')[security.NameFull.Split('_').Length - 1];

        //    endPoint += "&from=" + ConvertToUnixTimestamp(startTime);
        //    endPoint += "&to=" + ConvertToUnixTimestamp(endTime);
        //    endPoint += "&limit=50000";
        //    endPoint += "&format=Simple";

        //    try
        //    {
        //        RestRequest requestRest = new RestRequest(endPoint, Method.GET);
        //        requestRest.AddHeader("accept", "application/json");
        //        requestRest.AddHeader("Authorization", "Bearer " + _apiTokenReal);
        //        RestClient client = new RestClient(_restApiHost);
        //        IRestResponse response = client.Execute(requestRest);

        //        if (response.StatusCode == HttpStatusCode.OK)
        //        {
        //            string content = response.Content;
        //            TradesHistoryAE trades = JsonConvert.DeserializeAnonymousType(content, new TradesHistoryAE());
        //            return trades;
        //        }
        //        else
        //        {
        //            SendLogMessage("Trades request error. Status: " + response.StatusCode, LogMessageType.Error);
        //        }
        //    }
        //    catch (Exception exception)
        //    {
        //        SendLogMessage("Trades request error" + exception.ToString(), LogMessageType.Error);
        //    }
        //    return null;
        //}

        //private List<Trade> ConvertToOsEngineTrades(TradesHistoryAE trades)
        //{
        //    List<Trade> result = new List<Trade>();

        //    if(trades.list == null)
        //    {
        //        return result;
        //    }

        //    for (int i = 0; i < trades.list.Count; i++)
        //    {
        //        AETrade curTrade = trades.list[i];

        //        Trade newTrade = new Trade();
        //        newTrade.Volume = curTrade.qty.ToDecimal();
        //        newTrade.Time = ConvertToDateTimeFromTimeAEData(curTrade.time);
        //        newTrade.Price = curTrade.price.ToDecimal();
        //        newTrade.Id = curTrade.id;
        //        newTrade.SecurityNameCode = curTrade.symbol;

        //        if(curTrade.side == "buy")
        //        {
        //            newTrade.Side = Side.Buy;
        //        }
        //        else
        //        {
        //            newTrade.Side = Side.Sell;
        //        }

        //        result.Add(newTrade);
        //    }

        //    return result;
        //}

        #endregion

        #region 6 WebSocket creation

        private string _socketLocker = "webSocketLockerAE";

        private string GetGuid()
        {
            Guid newUid = Guid.NewGuid();
            return newUid.ToString();
        }

        private static long _messageId = 0;
        private static readonly JsonSerializerSettings _jsonSettings = new JsonSerializerSettings
        {
            ContractResolver = new CamelCasePropertyNamesContractResolver(),
            Formatting = Formatting.None,
            NullValueHandling = NullValueHandling.Ignore
        };

        private void SendCommand(WebSocketMessageBase command)
        {
            string json = JsonConvert.SerializeObject(command, _jsonSettings);
            _ws.Send(json);
        }

        private void CreateWebSocketConnection()
        {
            try
            {
                _subscriptionsData.Clear();
                _subscriptionsPortfolio.Clear();

                if (_ws != null)
                {
                    return;
                }

                _socketDataIsActive = false;

                lock (_socketLocker)
                {
                    WebSocketDataMessage = new ConcurrentQueue<string>();
                    WebSocketPortfolioMessage = new ConcurrentQueue<string>();

                    var certificate = new X509Certificate2(_pathToKeyFile, _keyFilePassphrase, X509KeyStorageFlags.MachineKeySet);

                    _ws = new WebSocket($"wss://{_apiHost}:{_apiPort}/clientapi/v1");
                    _ws.SslConfiguration.ClientCertificateSelectionCallback =
                        (sender, targethost, localCertificates, remoteCertificate, acceptableIssuers) =>
                        {
                            return certificate;
                        };

                    _ws.SslConfiguration.ClientCertificates = new X509CertificateCollection();
                    // Add client certificate
                    _ws.SslConfiguration.ClientCertificates.Add(certificate);

                    // Set SSL/TLS protocol (adjust as needed)
                    _ws.SslConfiguration.EnabledSslProtocols = System.Security.Authentication.SslProtocols.Tls12;

                    // Optional: Bypass server certificate validation (for testing only)
                    _ws.SslConfiguration.ServerCertificateValidationCallback = (sender, cert, chain, errors) => true;

                    _ws.EmitOnPing = true;
                    _ws.OnOpen += WebSocketData_Opened;
                    _ws.OnClose += WebSocketData_Closed;
                    _ws.OnMessage += WebSocketData_MessageReceived;
                    _ws.OnError += WebSocketData_Error;
                    _ws.Connect();
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
                        try
                        {
                            _ws.OnOpen -= WebSocketData_Opened;
                            _ws.OnClose -= WebSocketData_Closed;
                            _ws.OnMessage -= WebSocketData_MessageReceived;
                            _ws.OnError -= WebSocketData_Error;
                            _ws.CloseAsync();
                        }
                        catch
                        {
                            // ignore
                        }

                        //try
                        //{
                        //    _webSocketPortfolio.OnOpen -= _webSocketPortfolio_Opened;
                        //    _webSocketPortfolio.OnClose -= _webSocketPortfolio_Closed;
                        //    _webSocketPortfolio.OnMessage -= _webSocketPortfolio_MessageReceived;
                        //    _webSocketPortfolio.OnError -= _webSocketPortfolio_Error;
                        //    _webSocketPortfolio.CloseAsync();
                        //}
                        //catch
                        //{
                        //    // ignore
                        //}
                    }
                }
            }
            catch
            {

            }
            finally
            {
                _ws = null;
                _webSocketPortfolio = null;
            }
        }

        private bool _socketDataIsActive;

        private bool _socketPortfolioIsActive;

        private string _activationLocker = "activationLocker";

        private void CheckActivationSockets()
        {
            if (_socketDataIsActive == false)
            {
                return;
            }

            //if (_socketPortfolioIsActive == false)
            //{
            //    return;
            //}

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

        private WebSocket _webSocketPortfolio;

        private void ActivatePortfolioSocket()
        {
            //if (string.IsNullOrEmpty(_portfolioSpotId) == false)
            //{
            //    ActivateCurrentPortfolioListening(_portfolioSpotId);
            //}
            //if (string.IsNullOrEmpty(_portfolioFutId) == false)
            //{
            //    ActivateCurrentPortfolioListening(_portfolioFutId);
            //}
            //if (string.IsNullOrEmpty(_portfolioCurrencyId) == false)
            //{
            //    ActivateCurrentPortfolioListening(_portfolioCurrencyId);
            //}
            //if (string.IsNullOrEmpty(_portfolioSpareId) == false)
            //{
            //    ActivateCurrentPortfolioListening(_portfolioSpareId);
            //}
        }

        private void ActivateCurrentPortfolioListening(string portfolioName)
        {
            //// myTrades subscription

            //RequestSocketSubscribeMyTrades subObjTrades = new RequestSocketSubscribeMyTrades();
            //subObjTrades.guid = GetGuid();
            //subObjTrades.token = _apiTokenReal;
            //subObjTrades.portfolio = portfolioName;

            //if (portfolioName.StartsWith("E"))
            //{
            //    subObjTrades.exchange = "UNITED";
            //}

            //string messageTradeSub = JsonConvert.SerializeObject(subObjTrades);

            //AESocketSubscription myTradesSub = new AESocketSubscription();
            //myTradesSub.SubType = AESubType.MyTrades;
            //myTradesSub.Guid = subObjTrades.guid;

            //_subscriptionsPortfolio.Add(myTradesSub);
            //_webSocketPortfolio.Send(messageTradeSub);

            //Thread.Sleep(1000);

            //// orders subscription

            //RequestSocketSubscribeOrders subObjOrders = new RequestSocketSubscribeOrders();
            //subObjOrders.guid = GetGuid();
            //subObjOrders.token = _apiTokenReal;
            //subObjOrders.portfolio = portfolioName;

            //if (portfolioName.StartsWith("E"))
            //{
            //    subObjOrders.exchange = "UNITED";
            //}

            //string messageOrderSub = JsonConvert.SerializeObject(subObjOrders);

            //AESocketSubscription ordersSub = new AESocketSubscription();
            //ordersSub.SubType = AESubType.Orders;
            //ordersSub.Guid = subObjOrders.guid;
            //ordersSub.ServiceInfo = portfolioName;

            //_subscriptionsPortfolio.Add(ordersSub);
            //_webSocketPortfolio.Send(messageOrderSub);

            //Thread.Sleep(1000);

            //// portfolio subscription

            //RequestSocketSubscribePortfolio subObjPortf = new RequestSocketSubscribePortfolio();
            //subObjPortf.guid = GetGuid();
            //subObjPortf.token = _apiTokenReal;
            //subObjPortf.portfolio = portfolioName;

            //if (portfolioName.StartsWith("E"))
            //{
            //    subObjPortf.exchange = "UNITED";
            //}

            //string messagePortfolioSub = JsonConvert.SerializeObject(subObjPortf);

            //AESocketSubscription portfSub = new AESocketSubscription();
            //portfSub.SubType = AESubType.Portfolio;
            //portfSub.ServiceInfo = portfolioName;
            //portfSub.Guid = subObjPortf.guid;

            //_subscriptionsPortfolio.Add(portfSub);
            //_webSocketPortfolio.Send(messagePortfolioSub);

            //Thread.Sleep(1000);

            //// positions subscription

            //RequestSocketSubscribePositions subObjPositions = new RequestSocketSubscribePositions();
            //subObjPositions.guid = GetGuid();
            //subObjPositions.token = _apiTokenReal;
            //subObjPositions.portfolio = portfolioName;

            //if (portfolioName.StartsWith("E"))
            //{
            //    subObjPositions.exchange = "UNITED";
            //}

            //string messagePositionsSub = JsonConvert.SerializeObject(subObjPositions);

            //AESocketSubscription positionsSub = new AESocketSubscription();
            //positionsSub.SubType = AESubType.Positions;
            //positionsSub.ServiceInfo = portfolioName;
            //positionsSub.Guid = subObjPositions.guid;

            //_subscriptionsPortfolio.Add(positionsSub);
            //_webSocketPortfolio.Send(messagePositionsSub);
        }

        #endregion

        #region 7 WebSocket events

        private void WebSocketData_Opened(object sender, EventArgs e)
        {
            SendLogMessage("Socket Data activated", LogMessageType.System);
            _socketDataIsActive = true;
            CheckActivationSockets();
        }

        private void WebSocketData_Closed(object sender, EventArgs e)
        {
            try
            {
                SendLogMessage("Connection Closed by AE. WebSocket Data Closed Event", LogMessageType.Error);

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

        private void WebSocketData_Error(object sender, WebSocketSharp.ErrorEventArgs e)
        {
            try
            {
                var error = e;

                if (error.Exception != null)
                {
                    SendLogMessage(error.Exception.ToString(), LogMessageType.Error);
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
                if (e.Data.Length == 4)
                { // pong message
                    return;
                }

                if (e.Data.StartsWith("{\"requestGuid"))
                {
                    return;
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
                SendLogMessage("Trade socket error. " + error.ToString(), LogMessageType.Error);
            }
        }

        private void _webSocketPortfolio_Opened(object sender, EventArgs e)
        {
            SendLogMessage("Socket Portfolio activated", LogMessageType.System);
            _socketPortfolioIsActive = true;
            CheckActivationSockets();
        }

        private void _webSocketPortfolio_Closed(object sender, EventArgs e)
        {
            try
            {
                SendLogMessage("Connection Closed by AE. WebSocket Portfolio Closed Event", LogMessageType.Error);

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

        private void _webSocketPortfolio_Error(object sender, WebSocketSharp.ErrorEventArgs e)
        {
            try
            {
                var error = e;

                if (error.Exception != null)
                {
                    SendLogMessage(error.Exception.ToString(), LogMessageType.Error);
                }
            }
            catch (Exception ex)
            {
                SendLogMessage("Portfolio socket error" + ex.ToString(), LogMessageType.Error);
            }
        }

        private void _webSocketPortfolio_MessageReceived(object sender, MessageEventArgs e)
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

                if (e.Data.StartsWith("{\"requestGuid"))
                {
                    return;
                }

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

        #endregion

        #region 8 WebSocket check alive

        private void KeepaliveUserDataStream()
        {
            while (true)
            {
                try
                {
                    Thread.Sleep(30000);

                    if (ServerStatus == ServerConnectStatus.Disconnect)
                    {
                        continue;
                    }

                    if (_ws.Ping() == false &&
                        _webSocketPortfolio.Ping() == false)
                    {
                        SendLogMessage("AE connector. WARNING. Sockets Ping Pong not work. No internet or the server AE is not available", LogMessageType.Error);
                    }
                }
                catch (Exception ex)
                {
                    SendLogMessage(ex.ToString(), LogMessageType.Error);
                    Thread.Sleep(5000);
                }
            }
        }

        #endregion

        #region 9 WebSocket Security subscrible

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
                    Id = Interlocked.Increment(ref _messageId),
                    Timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
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

        #region 10 WebSocket parsing the messages

        private List<AESocketSubscription> _subscriptionsData = new List<AESocketSubscription>();

        private List<AESocketSubscription> _subscriptionsPortfolio = new List<AESocketSubscription>();

        private ConcurrentQueue<string> WebSocketDataMessage = new ConcurrentQueue<string>();

        private ConcurrentQueue<string> WebSocketPortfolioMessage = new ConcurrentQueue<string>();

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
                    } else if (baseMessage.Type.StartsWith("AccountState"))
                    {
                        UpdateAccountState(message);
                    } else if (baseMessage.Type == "Error")
                    {
                        WebSocketErrorMessage errorMessage = JsonConvert.DeserializeObject<WebSocketErrorMessage>(message, _jsonSettings);

                        SendLogMessage($"Msg: {errorMessage.Message}, code: {errorMessage.Code}", LogMessageType.Error);
                    }
                    else
                    {
                        SendLogMessage(message, LogMessageType.System);
                    }

                    //for(int i = 0;i < _subscriptionsData.Count;i++)
                    //{
                    //    if (_subscriptionsData[i].Guid != baseMessage.guid)
                    //    {
                    //        continue;
                    //    }

                    //    if (_subscriptionsData[i].SubType == AESubType.Trades)
                    //    {
                    //        UpDateTrade(baseMessage.data.ToString(), _subscriptionsData[i].ServiceInfo);
                    //        break;
                    //    }
                    //    else if (_subscriptionsData[i].SubType == AESubType.MarketDepth)
                    //    {
                    //        UpDateMarketDepth(message, _subscriptionsData[i].ServiceInfo);
                    //        break;
                    //    }
                    //}
                }
                catch (Exception exception)
                {
                    SendLogMessage(exception.ToString(), LogMessageType.Error);
                    Thread.Sleep(5000);
                }
            }
        }

        private void UpDateTrade(string data, string secName)
        {
            //QuotesAE baseMessage =
            //JsonConvert.DeserializeAnonymousType(data, new QuotesAE());

            //if(string.IsNullOrEmpty(baseMessage.timestamp))
            //{
            //    return;
            //}

            //Trade trade = new Trade();
            //trade.SecurityNameCode = baseMessage.symbol;
            //trade.Price = baseMessage.price.ToDecimal();
            //trade.Time = ConvertToDateTimeFromUnixFromMilliseconds(baseMessage.timestamp);
            //trade.Id = baseMessage.id;

            //if(string.IsNullOrEmpty(baseMessage.oi) == false)
            //{
            //    trade.OpenInterest = baseMessage.oi.ToDecimal();
            //}

            //if (baseMessage.side == "sell")
            //{
            //    trade.Side = Side.Sell;
            //}
            //else
            //{
            //    trade.Side = Side.Buy;
            //}
            
            //trade.Volume = baseMessage.qty.ToDecimal();

            //if(trade.Price < 0)
            //{

            //}

            //if (NewTradesEvent != null)
            //{
            //    NewTradesEvent(trade);
            //}
        }

        private void UpDateMarketDepth(string data, string secName)
        {
            //MarketDepthFullMessage baseMessage =
            //JsonConvert.DeserializeAnonymousType(data, new MarketDepthFullMessage());

            //if (baseMessage.data.bids == null ||
            //    baseMessage.data.asks == null)
            //{
            //    return;
            //}

            //if (baseMessage.data.bids.Count == 0 ||
            //    baseMessage.data.asks.Count == 0)
            //{
            //    return;
            //}

            //MarketDepth depth = new MarketDepth();
            //depth.SecurityNameCode = secName;
            //depth.Time = ConvertToDateTimeFromUnixFromMilliseconds(baseMessage.data.ms_timestamp);

            //for (int i = 0; i < baseMessage.data.bids.Count; i++)
            //{
            //    MarketDepthLevel newBid = new MarketDepthLevel();
            //    newBid.Price = baseMessage.data.bids[i].price.ToDecimal();
            //    newBid.Bid = baseMessage.data.bids[i].volume.ToDecimal();
            //    depth.Bids.Add(newBid);
            //}

            //for (int i = 0; i < baseMessage.data.asks.Count; i++)
            //{
            //    MarketDepthLevel newAsk = new MarketDepthLevel();
            //    newAsk.Price = baseMessage.data.asks[i].price.ToDecimal();
            //    newAsk.Ask = baseMessage.data.asks[i].volume.ToDecimal();
            //    depth.Asks.Add(newAsk);
            //}

            //if(_lastMdTime != DateTime.MinValue &&
            //    _lastMdTime >= depth.Time)
            //{
            //    depth.Time = _lastMdTime.AddTicks(1);
            //}

            //_lastMdTime = depth.Time;

            //if (MarketDepthEvent != null)
            //{
            //    MarketDepthEvent(depth);
            //}
        }

        private DateTime _lastMdTime = DateTime.MinValue;

        public event Action<Trade> NewTradesEvent;

        public event Action<MarketDepth> MarketDepthEvent;

        private void UpDatePositionOnBoard(string data, string portfolioName)
        {
            //PositionOnBoardAE baseMessage =
            //           JsonConvert.DeserializeAnonymousType(data, new PositionOnBoardAE());

            //Portfolio portf = null;

            //for (int i = 0; i < _myPortfolios.Count; i++)
            //{
            //    string realPortfName = _myPortfolios[i].Number.Split('_')[0];
            //    if (realPortfName == portfolioName)
            //    {
            //        portf = _myPortfolios[i];
            //        break;
            //    }
            //}

            //if (portf == null)
            //{
            //    return;
            //}

            //PositionOnBoard newPos = new PositionOnBoard();
            //newPos.PortfolioName = portf.Number;
            //newPos.ValueCurrent = baseMessage.qty.ToDecimal();
            //newPos.SecurityNameCode = baseMessage.symbol;
            //newPos.UnrealizedPnl = baseMessage.dailyUnrealisedPl.ToDecimal();

            //portf.SetNewPosition(newPos);

            //if (PortfolioEvent != null)
            //{
            //    PortfolioEvent(_myPortfolios);
            //}
        }

        private void UpDateMyOrder(string data, string portfolioName)
        {
            //OrderAE baseMessage =
            //JsonConvert.DeserializeAnonymousType(data, new OrderAE());

            //Order order = ConvertToOsEngineOrder(baseMessage, portfolioName);

            //if(order == null)
            //{
            //    return;
            //}

            //lock (_sendOrdersArrayLocker)
            //{
            //    for (int i = 0; i < _sendOrders.Count; i++)
            //    {
            //        if (_sendOrders[i] == null)
            //        {
            //            continue;
            //        }

            //        if (_sendOrders[i].NumberUser == order.NumberUser)
            //        {
            //            order.TypeOrder = _sendOrders[i].TypeOrder;
            //            break;
            //        }
            //    }
            //}

            //if (MyOrderEvent != null)
            //{
            //    MyOrderEvent(order);
            //}

            //if(order.State == OrderStateType.Done)
            //{
            //    // Проверяем, является ли бумага спредом

            //    for (int i = 0; i < _spreadOrders.Count; i++)
            //    {
            //        if (_spreadOrders[i].NumberUser == order.NumberUser 
            //            && _spreadOrders[i].NumberMarket == "")
            //        {
            //            _spreadOrders[i].NumberMarket = order.NumberMarket;
            //        }
            //    }

            //    for (int i = 0; i < _spreadOrders.Count; i++)
            //    {
            //        if (_spreadOrders[i].SecurityNameCode == order.SecurityNameCode)
            //        {
            //            if(TryGenerateFakeMyTradeToOrderBySpread(order))
            //            {
            //                _spreadOrders.RemoveAt(i);
            //                break;
            //            }
            //        }
            //    }
            //}
            //else if(order.State == OrderStateType.Cancel
            //        || order.State == OrderStateType.Fail)
            //{
            //    for (int i = 0; i < _spreadOrders.Count; i++)
            //    {
            //        if (_spreadOrders[i].NumberUser == order.NumberUser)
            //        {
            //            _spreadOrders.RemoveAt(i);
            //            break;
            //        }
            //    }
            //}
        }

        private bool TryGenerateFakeMyTradeToOrderBySpread(Order order)
        {
            MyTrade tradeFirst = null;

            MyTrade tradeSecond = null;

            for(int i = 0;i < _spreadMyTrades.Count;i++)
            {
                if (_spreadMyTrades[i].NumberOrderParent == order.NumberMarket)
                {
                    if(tradeFirst == null)
                    {
                        tradeFirst = _spreadMyTrades[i];
                    }
                    else if(tradeSecond == null)
                    {
                        tradeSecond = _spreadMyTrades[i];
                        break;
                    }
                }
            }

            if(tradeFirst != null && 
                tradeSecond != null)
            {
                if(order.SecurityNameCode.StartsWith(tradeFirst.SecurityNameCode) == false)
                {
                    MyTrade third = tradeFirst;
                    tradeFirst = tradeSecond;
                    tradeSecond = third;
                }

                MyTrade trade = new MyTrade();
                trade.SecurityNameCode = order.SecurityNameCode;
                trade.Price = tradeSecond.Price - tradeFirst.Price;
                trade.Volume = order.Volume;
                trade.NumberOrderParent = order.NumberMarket;
                trade.NumberTrade = order.NumberMarket + "fakeSpreadTrade";
                trade.Time = order.TimeCallBack;
                trade.Side = order.Side;

                if (MyTradeEvent != null)
                {
                    MyTradeEvent(trade);
                }

                for (int i = 0; i < _spreadMyTrades.Count; i++)
                {
                    if (_spreadMyTrades[i].NumberOrderParent == order.NumberMarket)
                    {
                        _spreadMyTrades.RemoveAt(i);
                        i--;
                    }
                }

                return true;
            }

            return false;
        }

        //private Order ConvertToOsEngineOrder(OrderAE baseMessage, string portfolioName)
        //{
        //    Order order = new Order();

        //    order.SecurityNameCode = baseMessage.symbol;

        //    if(string.IsNullOrEmpty(baseMessage.filled) == false 
        //        && baseMessage.filled != "0")
        //    {
        //        order.Volume = baseMessage.filled.ToDecimal();
        //    }
        //    else
        //    {
        //        order.Volume = baseMessage.qty.ToDecimal();
        //    }

        //    order.PortfolioNumber = portfolioName;
            
        //    if (baseMessage.type == "limit")
        //    {
        //        order.Price = baseMessage.price.ToDecimal();
        //        order.TypeOrder = OrderPriceType.Limit;
        //    }
        //    else if (baseMessage.type == "market")
        //    {
        //        order.TypeOrder = OrderPriceType.Market;
        //    }

        //    try
        //    {
        //        order.NumberUser = Convert.ToInt32(baseMessage.comment);
        //    }
        //    catch
        //    {
        //        // ignore
        //    }

        //    order.NumberMarket = baseMessage.id;

        //    order.TimeCallBack = ConvertToDateTimeFromTimeAEData(baseMessage.transTime);

        //    if (baseMessage.side == "buy")
        //    {
        //        order.Side = Side.Buy;
        //    }
        //    else
        //    {
        //        order.Side = Side.Sell;
        //    }

        //    //working - На исполнении
        //    //filled - Исполнена
        //    //canceled - Отменена
        //    //rejected - Отклонена

        //    if (baseMessage.status == "working")
        //    {
        //        order.State = OrderStateType.Active;
        //    }
        //    else if (baseMessage.status == "filled")
        //    {
        //        order.State = OrderStateType.Done;
        //    }
        //    else if (baseMessage.status == "canceled")
        //    {
        //        lock (_changePriceOrdersArrayLocker)
        //        {
        //            DateTime now = DateTime.Now;
        //            for (int i = 0; i < _changePriceOrders.Count; i++)
        //            {
        //                if (_changePriceOrders[i].TimeChangePriceOrder.AddSeconds(2) < now)
        //                {
        //                    _changePriceOrders.RemoveAt(i);
        //                    i--;
        //                    continue;
        //                }

        //                if (_changePriceOrders[i].MarketId == order.NumberMarket)
        //                {
        //                    return null;
        //                }
        //            }
        //        }

        //        if (string.IsNullOrEmpty(baseMessage.filledQtyUnits))
        //        {
        //            order.State = OrderStateType.Cancel;
        //        }
        //        else if (baseMessage.filledQtyUnits == "0")
        //        {
        //            order.State = OrderStateType.Cancel;
        //        }
        //        else
        //        {
        //            try
        //            {
        //                decimal volFilled = baseMessage.filledQtyUnits.ToDecimal();

        //                if (volFilled > 0)
        //                {
        //                    order.State = OrderStateType.Done;
        //                }
        //                else
        //                {
        //                    order.State = OrderStateType.Cancel;
        //                }
        //            }
        //            catch
        //            {
        //                order.State = OrderStateType.Cancel;
        //            }
        //        }
        //    }
        //    else if (baseMessage.status == "rejected")
        //    {
        //        order.State = OrderStateType.Fail;
        //    }

        //    return order;
        //}

        private void UpDateMyPortfolio(string data, string portfolioName)
        {
            //AEPortfolioSocket baseMessage =
            //JsonConvert.DeserializeAnonymousType(data, new AEPortfolioSocket());

            //Portfolio portf = null;

            //for(int i = 0;i < _myPortfolios.Count;i++)
            //{
            //    string realPortfName = _myPortfolios[i].Number.Split('_')[0];
            //    if (realPortfName == portfolioName)
            //    {
            //        portf = _myPortfolios[i];
            //        break;
            //    }
            //}

            //if(portf == null)
            //{
            //    return;
            //}

            //if(portf.ValueBegin == 0)
            //{
            //    portf.ValueBegin = baseMessage.portfolioLiquidationValue.ToDecimal();
            //}

            //portf.ValueCurrent = baseMessage.portfolioLiquidationValue.ToDecimal();
            
            //portf.ValueBlocked = baseMessage.portfolioLiquidationValue.ToDecimal() - baseMessage.buyingPower.ToDecimal();
           
            //portf.UnrealizedPnl = baseMessage.profit.ToDecimal();

            //if (PortfolioEvent != null)
            //{
            //    PortfolioEvent(_myPortfolios);
            //}
        }

        public event Action<Order> MyOrderEvent;

        public event Action<MyTrade> MyTradeEvent;

        #endregion

        #region 11 Trade

        private RateGate _rateGateSendOrder = new RateGate(1, TimeSpan.FromMilliseconds(350));

        private RateGate _rateGateCancelOrder = new RateGate(1, TimeSpan.FromMilliseconds(350));

        private RateGate _rateGateChangePriceOrder = new RateGate(1, TimeSpan.FromMilliseconds(350));

        private List<AESecuritiesAndPortfolios> _securitiesAndPortfolios = new List<AESecuritiesAndPortfolios>();

        private List<Order> _sendOrders = new List<Order>();

        private string _sendOrdersArrayLocker = "AESendOrdersArrayLocker";

        private List<Order> _spreadOrders = new List<Order>();

        public void SendOrder(Order order)
        {
            _rateGateSendOrder.WaitToProceed();

            try
            {
                //if (order.SecurityClassCode == "Futures spread")
                //{ // календарный спред
                //  // сохраняем бумагу для дальнейшего использования
                //    _spreadOrders.Add(order);
                //}

                //if (order.TypeOrder == OrderPriceType.Market)
                //{
                //    lock (_sendOrdersArrayLocker)
                //    {
                //        _sendOrders.Add(order);

                //        while (_sendOrders.Count > 100)
                //        {
                //            _sendOrders.RemoveAt(0);
                //        }
                //    }
                //}

                //string endPoint = "";

                //if(order.TypeOrder == OrderPriceType.Limit)
                //{
                //    endPoint = "/commandapi/warptrans/TRADE/v2/client/orders/actions/limit";
                //}
                //else if (order.TypeOrder == OrderPriceType.Market)
                //{
                //    endPoint = "/commandapi/warptrans/TRADE/v2/client/orders/actions/market";
                //}

                //RestRequest requestRest = new RestRequest(endPoint, Method.POST);
                //requestRest.AddHeader("Authorization", "Bearer " + _apiTokenReal);
                //requestRest.AddHeader("X-REQID", order.NumberUser.ToString() + "|" + GetGuid());
                //requestRest.AddHeader("accept", "application/json");

                //if(order.TypeOrder == OrderPriceType.Market)
                //{
                //    MarketOrderAERequest body = GetMarketRequestObj(order);
                //    requestRest.AddJsonBody(body);
                //}
                //else if(order.TypeOrder == OrderPriceType.Limit)
                //{
                //    LimitOrderAERequest body = GetLimitRequestObj(order);
                //    requestRest.AddJsonBody(body);
                //}

                //RestClient client = new RestClient(_restApiHost);

                //IRestResponse response = client.Execute(requestRest);

                //if (response.StatusCode == HttpStatusCode.OK)
                //{
                //    bool isInArray = false;
                //    for(int i = 0;i < _securitiesAndPortfolios.Count;i++)
                //    {
                //        if (_securitiesAndPortfolios[i].Security == order.SecurityNameCode)
                //        {
                //            isInArray = true;
                //            break;
                //        }
                //    }
                //    if(isInArray == false)
                //    {
                //        AESecuritiesAndPortfolios newValue = new AESecuritiesAndPortfolios();
                //        newValue.Security = order.SecurityNameCode;
                //        newValue.Portfolio = order.PortfolioNumber;
                //        _securitiesAndPortfolios.Add(newValue);
                //    }

                //    return;
                //}
                //else
                //{
                //    SendLogMessage("Order Fail. Status: "
                //        + response.StatusCode + "  " + order.SecurityNameCode , LogMessageType.Error);

                //    if(response.Content != null)
                //    {
                //        SendLogMessage("Fail reasons: "
                //      + response.Content, LogMessageType.Error);
                //    }

                //    order.State = OrderStateType.Fail;

                //    if(MyOrderEvent != null)
                //    {
                //        MyOrderEvent(order);
                //    }
                //}
            }
            catch (Exception exception)
            {
                SendLogMessage("Order send error " + exception.ToString(), LogMessageType.Error);
            }
        }

        //private LimitOrderAERequest GetLimitRequestObj(Order order)
        //{
        //    LimitOrderAERequest requestObj = new LimitOrderAERequest();

        //    if(order.Side == Side.Buy)
        //    {
        //        requestObj.side = "buy";
        //    }
        //    else
        //    {
        //        requestObj.side = "sell";

        //    }
        //    requestObj.type = "limit";
        //    requestObj.quantity = Convert.ToInt32(order.Volume);
        //    requestObj.price = order.Price;
        //    requestObj.comment = order.NumberUser.ToString();
        //    requestObj.instrument = new instrumentAE();
        //    requestObj.instrument.symbol = order.SecurityNameCode;
        //    requestObj.user = new User();
        //    requestObj.user.portfolio = order.PortfolioNumber.Split('_')[0];

        //    return requestObj;
        //}

        //private MarketOrderAERequest GetMarketRequestObj(Order order)
        //{
        //    MarketOrderAERequest requestObj = new MarketOrderAERequest();

        //    if (order.Side == Side.Buy)
        //    {
        //        requestObj.side = "buy";
        //    }
        //    else
        //    {
        //        requestObj.side = "sell";
        //    }
        //    requestObj.type = "market";
        //    requestObj.quantity = Convert.ToInt32(order.Volume);
        //    requestObj.comment = order.NumberUser.ToString();
        //    requestObj.instrument = new instrumentAE();
        //    requestObj.instrument.symbol = order.SecurityNameCode;
        //    requestObj.user = new User();
        //    requestObj.user.portfolio = order.PortfolioNumber.Split('_')[0];

        //    return requestObj;
        //}

        List<AEChangePriceOrder> _changePriceOrders = new List<AEChangePriceOrder>();

        private string _changePriceOrdersArrayLocker = "cangePriceArrayLocker";

        /// <summary>
        /// Order price change
        /// </summary>
        /// <param name="order">An order that will have a new price</param>
        /// <param name="newPrice">New price</param>
        public void ChangeOrderPrice(Order order, decimal newPrice)
        {
            //try
            //{
            //    _rateGateChangePriceOrder.WaitToProceed();

            //    if (order.TypeOrder == OrderPriceType.Market)
            //    {
            //        SendLogMessage("Can`t change price to market order", LogMessageType.Error);
            //        return;
            //    }
                
            //    string endPoint = "/commandapi/warptrans/TRADE/v2/client/orders/actions/limit/";

            //    endPoint += order.NumberMarket;

            //    RestRequest requestRest = new RestRequest(endPoint, Method.PUT);
            //    requestRest.AddHeader("Authorization", "Bearer " + _apiTokenReal);
            //    requestRest.AddHeader("X-REQID", order.NumberUser.ToString() + "|" + GetGuid()); ;
            //    requestRest.AddHeader("accept", "application/json");

            //    LimitOrderAERequest body = GetLimitRequestObj(order);
            //    body.price = newPrice;

            //    int qty = Convert.ToInt32(order.Volume - order.VolumeExecute);

            //    if(qty <= 0 ||
            //        order.State != OrderStateType.Active)
            //    {
            //        SendLogMessage("Can`t change price to order. It's not in Activ state", LogMessageType.Error);
            //        return;
            //    }

            //    requestRest.AddJsonBody(body);
                
            //    RestClient client = new RestClient(_restApiHost);

            //    AEChangePriceOrder AEChangePriceOrder = new AEChangePriceOrder();
            //    AEChangePriceOrder.MarketId = order.NumberMarket;
            //    AEChangePriceOrder.TimeChangePriceOrder = DateTime.Now;

            //    lock(_changePriceOrdersArrayLocker)
            //    {
            //        _changePriceOrders.Add(AEChangePriceOrder);
            //    }

            //    IRestResponse response = client.Execute(requestRest);

            //    if (response.StatusCode == HttpStatusCode.OK)
            //    {
            //        SendLogMessage("Order change price. New price: " + newPrice
            //            + "  " + order.SecurityNameCode, LogMessageType.System);

            //        order.Price = newPrice;
            //        if (MyOrderEvent != null)
            //        {
            //            MyOrderEvent(order);
            //        }

            //        //return;
            //    }
            //    else
            //    {
            //        SendLogMessage("Change price order Fail. Status: "
            //            + response.StatusCode + "  " + order.SecurityNameCode, LogMessageType.Error);

            //        if (response.Content != null)
            //        {
            //            SendLogMessage("Fail reasons: "
            //          + response.Content, LogMessageType.Error);
            //        }
            //    }

            //}
            //catch (Exception error)
            //{
            //    SendLogMessage(error.ToString(), LogMessageType.Error);
            //}
        }

        List<string> _cancelOrderNums = new List<string>();

        public void CancelOrder(Order order)
        {
            _rateGateCancelOrder.WaitToProceed();

            //curl -X DELETE "/commandapi/warptrans/TRADE/v2/client/orders/93713183?portfolio=D39004&exchange=MOEX&stop=false&format=Simple" -H "accept: application/json"

            try
            {
                //int countTryRevokeOrder = 0;

                //for(int i = 0; i< _cancelOrderNums.Count;i++)
                //{
                //    if (_cancelOrderNums[i].Equals(order.NumberMarket))
                //    {
                //        countTryRevokeOrder++;
                //    }
                //}

                //if(countTryRevokeOrder >= 2)
                //{
                //    SendLogMessage("Order cancel request error. The order has already been revoked " + order.SecurityClassCode, LogMessageType.Error);
                //    return;
                //}

                //_cancelOrderNums.Add(order.NumberMarket);

                //while(_cancelOrderNums.Count > 100)
                //{
                //    _cancelOrderNums.RemoveAt(0);
                //}

                //string portfolio = order.PortfolioNumber.Split('_')[0];

                //string exchange = "MOEX";
                //string endPoint 
                //    = $"/commandapi/warptrans/TRADE/v2/client/orders/{order.NumberMarket}?portfolio={portfolio}&exchange={exchange}&stop=false&jsonResponse=true&format=Simple";

                //RestRequest requestRest = new RestRequest(endPoint, Method.DELETE);
                //requestRest.AddHeader("Authorization", "Bearer " + _apiTokenReal);
                //requestRest.AddHeader("accept", "application/json");

                //RestClient client = new RestClient(_restApiHost);

                //IRestResponse response = client.Execute(requestRest);

                //if (response.StatusCode == HttpStatusCode.OK)
                //{
                //    return;
                //}
                //else
                //{
                //    SendLogMessage("Order cancel request error. Status: "
                //        + response.StatusCode + "  " + order.SecurityClassCode, LogMessageType.Error);

                //    if (response.Content != null)
                //    {
                //        SendLogMessage("Fail reasons: "
                //      + response.Content, LogMessageType.Error);
                //    }
                //}
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
            List<Order> orders = GetAllOrdersFromExchange();

            for (int i = 0; i < orders.Count;i++)
            {
                Order order = orders[i];

                if(order.State == OrderStateType.Active)
                {
                    CancelOrder(order);
                }
            }
        }

        public void CancelAllOrdersToSecurity(Security security)
        {
            List<Order> orders = GetAllOrdersFromExchange();

            for (int i = 0; i < orders.Count; i++)
            {
                Order order = orders[i];

                if (order.State == OrderStateType.Active
                    && order.SecurityNameCode == security.Name)
                {
                    CancelOrder(order);
                }
            }
        }

        public void GetAllActivOrders()
        {
            List<Order> orders = GetAllOrdersFromExchange();

            for(int i = 0; orders != null && i < orders.Count; i++)
            {
                if(orders[i] == null)
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

                if (MyOrderEvent != null)
                {
                    MyOrderEvent(orders[i]);
                }
            }
        }

        public void GetOrderStatus(Order order)
        {
            List<Order> orders = GetAllOrdersFromExchange();

            if(orders == null ||
                orders.Count == 0)
            {
                return;
            }

            Order orderOnMarket = null;

            for(int i = 0;i < orders.Count;i++)
            {
                Order curOder = orders[i];

                if (order.NumberUser != 0
                    && curOder.NumberUser != 0
                    && curOder.NumberUser == order.NumberUser)
                {
                    orderOnMarket = curOder;
                    break;
                }

                if(string.IsNullOrEmpty(order.NumberMarket) == false 
                    && order.NumberMarket == curOder.NumberMarket)
                {
                    orderOnMarket = curOder;
                    break;
                }
            }

            if(orderOnMarket == null)
            {
                return;
            }

            if (orderOnMarket != null && 
                MyOrderEvent != null)
            {
                MyOrderEvent(orderOnMarket);
            }

            if(orderOnMarket.State == OrderStateType.Done 
                || orderOnMarket.State == OrderStateType.Partial)
            {
                List<MyTrade> tradesBySecurity 
                    = GetMyTradesBySecurity(order.SecurityNameCode, order.PortfolioNumber.Split('_')[0]);

                if(tradesBySecurity == null)
                {
                    return;
                }

                List<MyTrade> tradesByMyOrder = new List<MyTrade>();

                for(int i = 0;i < tradesBySecurity.Count;i++)
                {
                    if (tradesBySecurity[i].NumberOrderParent == orderOnMarket.NumberMarket)
                    {
                        tradesByMyOrder.Add(tradesBySecurity[i]);
                    }
                }

                for(int i = 0;i < tradesByMyOrder.Count;i++)
                {
                    if(MyTradeEvent != null)
                    {
                        MyTradeEvent(tradesByMyOrder[i]);
                    }
                }
            }
        }

        private List<Order> GetAllOrdersFromExchange()
        {
            List<Order> orders = new List<Order>();

            //if (string.IsNullOrEmpty(_portfolioSpotId) == false)
            //{
            //    List<Order> newOrders = GetAllOrdersFromExchangeByPortfolio(_portfolioSpotId);

            //    if (newOrders != null &&
            //        newOrders.Count > 0)
            //    {
            //        orders.AddRange(newOrders);
            //    }
            //}

            //if (string.IsNullOrEmpty(_portfolioFutId) == false)
            //{
            //    List<Order> newOrders = GetAllOrdersFromExchangeByPortfolio(_portfolioFutId);

            //    if (newOrders != null &&
            //        newOrders.Count > 0)
            //    {
            //        orders.AddRange(newOrders);
            //    }
            //}

            //if (string.IsNullOrEmpty(_portfolioCurrencyId) == false)
            //{
            //    List<Order> newOrders = GetAllOrdersFromExchangeByPortfolio(_portfolioCurrencyId);

            //    if (newOrders != null &&
            //        newOrders.Count > 0)
            //    {
            //        orders.AddRange(newOrders);
            //    }
            //}

            //if (string.IsNullOrEmpty(_portfolioSpareId) == false)
            //{
            //    List<Order> newOrders = GetAllOrdersFromExchangeByPortfolio(_portfolioSpareId);

            //    if (newOrders != null &&
            //        newOrders.Count > 0)
            //    {
            //        orders.AddRange(newOrders);
            //    }
            //}

            return orders;
        }

        private List<Order> GetAllOrdersFromExchangeByPortfolio(string portfolio)
        {
            _rateGateSendOrder.WaitToProceed();

            try
            {
                //string exchange = "MOEX";
                //if (portfolio.StartsWith("E"))
                //{
                //    exchange = "UNITED";
                //}

                //string endPoint = $"/md/v2/clients/{exchange}/" + portfolio + "/orders?format=Simple";

                //RestRequest requestRest = new RestRequest(endPoint, Method.GET);
                //requestRest.AddHeader("Authorization", "Bearer " + _apiTokenReal);
                //requestRest.AddHeader("accept", "application/json");

                //RestClient client = new RestClient(_restApiHost);

                //IRestResponse response = client.Execute(requestRest);


                //if (response.StatusCode == HttpStatusCode.OK)
                //{
                //    string respString = response.Content;

                //    if(respString == "[]")
                //    {
                //        return null;
                //    }
                //    else
                //    {

                //        List<OrderAE> orders = JsonConvert.DeserializeAnonymousType(respString, new List<OrderAE>());

                //        List<Order> osEngineOrders = new List<Order>();

                //        for(int i = 0;i < orders.Count;i++)
                //        {
                //            Order newOrd = ConvertToOsEngineOrder(orders[i], portfolio);

                //            if(newOrd == null)
                //            {
                //                continue;
                //            }

                //            osEngineOrders.Add(newOrd);
                //        }

                //        return osEngineOrders;
                        
                //    }
                //}
                //else if(response.StatusCode == HttpStatusCode.NotFound)
                //{
                //    return null;
                //}
                //else
                //{
                //    SendLogMessage("Get all orders request error. ", LogMessageType.Error);

                //    if (response.Content != null)
                //    {
                //        SendLogMessage("Fail reasons: "
                //      + response.Content, LogMessageType.Error);
                //    }
                //}
            }
            catch (Exception exception)
            {
                SendLogMessage("Get all orders request error." + exception.ToString(), LogMessageType.Error);
            }

            return null;
        }

        private List<MyTrade> GetMyTradesBySecurity(string security, string portfolio)
        {
            try
            {
                // /md/v2/Clients/MOEX/D39004/LKOH/trades?format=Simple

                //string endPoint = "/md/v2/clients/MOEX/" + portfolio + "/" + security + "/trades?format=Simple";

                //RestRequest requestRest = new RestRequest(endPoint, Method.GET);
                //requestRest.AddHeader("Authorization", "Bearer " + _apiTokenReal);
                //requestRest.AddHeader("accept", "application/json");

                //RestClient client = new RestClient(_restApiHost);

                //IRestResponse response = client.Execute(requestRest);


                //if (response.StatusCode == HttpStatusCode.OK)
                //{
                //    string respString = response.Content;

                //    if (respString == "[]")
                //    {
                //        return null;
                //    }
                //    else
                //    {
                //        List<MyTradeAERest> allTradesJson 
                //            = JsonConvert.DeserializeAnonymousType(respString, new List<MyTradeAERest>());

                //        List<MyTrade> osEngineOrders = new List<MyTrade>();

                //        for (int i = 0; i < allTradesJson.Count; i++)
                //        {
                //            MyTradeAERest tradeRest = allTradesJson[i];

                //            MyTrade newTrade = new MyTrade();
                //            newTrade.SecurityNameCode = security;
                //            newTrade.NumberTrade = tradeRest.id;
                //            newTrade.NumberOrderParent = tradeRest.orderno;
                //            newTrade.Volume = tradeRest.qty.ToDecimal();
                //            newTrade.Price = tradeRest.price.ToDecimal();
                //            newTrade.Time =  ConvertToDateTimeFromTimeAEData(tradeRest.date);

                //            if (tradeRest.side == "buy")
                //            {
                //                newTrade.Side = Side.Buy;
                //            }
                //            else
                //            {
                //                newTrade.Side = Side.Sell;
                //            }

                //            osEngineOrders.Add(newTrade);
                //        }

                //        return osEngineOrders;

                //    }
                //}
                //else if (response.StatusCode == HttpStatusCode.NotFound)
                //{
                //    return null;
                //}
                //else
                //{
                //    SendLogMessage("Get all orders request error. ", LogMessageType.Error);

                //    if (response.Content != null)
                //    {
                //        SendLogMessage("Fail reasons: "
                //      + response.Content, LogMessageType.Error);
                //    }
                //}
            }
            catch (Exception exception)
            {
                SendLogMessage("Get all orders request error." + exception.ToString(), LogMessageType.Error);
            }

            return null;
        }

        #endregion

        #region 12 Helpers

        public long ConvertToUnixTimestamp(DateTime date)
        {
            DateTime origin = new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc);
            TimeSpan diff = date.ToUniversalTime() - origin;
            return Convert.ToInt64(diff.TotalSeconds);
        }

        private DateTime ConvertToDateTimeFromUnixFromSeconds(string seconds)
        {
            DateTime origin = new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc);
            DateTime result = origin.AddSeconds(seconds.ToDouble()).ToLocalTime();

            return result;
        }

        private DateTime ConvertToDateTimeFromUnixFromMilliseconds(string seconds)
        {
            DateTime origin = new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc);
            DateTime result = origin.AddMilliseconds(seconds.ToDouble());

            return result.ToLocalTime();
        }

        private DateTime ConvertToDateTimeFromTimeAEData(string AETime)
        {
            //"time": "2018-08-07T08:40:03.445Z",

            string date = AETime.Split('T')[0];

            int year = Convert.ToInt32(date.Substring(0,4));
            int month = Convert.ToInt32(date.Substring(5, 2));
            int day = Convert.ToInt32(date.Substring(8, 2));

            string time = AETime.Split('T')[1];

            int hour = Convert.ToInt32(time.Substring(0, 2));

            if (AETime.EndsWith("+00:00"))
            {
                hour += 3;
            }

            if (AETime.EndsWith("+01:00"))
            {
                hour += 2;
            }

            if (AETime.EndsWith("+02:00"))
            {
                hour += 1;
            }
            int minute = Convert.ToInt32(time.Substring(3, 2));
            int second = Convert.ToInt32(time.Substring(6, 2));
            int ms = Convert.ToInt32(time.Substring(10, 3));

            DateTime dateTime = new DateTime(year, month, day, hour, minute, second, ms);

            return dateTime;
        }

        #endregion

        #region 13 Log

        private void SendLogMessage(string message, LogMessageType messageType)
        {
            LogMessageEvent?.Invoke(message, messageType);
        }

        public event Action<string, LogMessageType> LogMessageEvent;

        #endregion
    }

    public enum AEAvailableExchanges
    {
        MOEX,
        SPBX,
        UNITED
    }

    public class AESocketSubscription
    {
        public string Guid;

        public AESubType SubType;

        public string ServiceInfo;
    }

    public class AEChangePriceOrder
    {
        public string MarketId;

        public DateTime TimeChangePriceOrder;
    }

    public class AESecuritiesAndPortfolios
    {
       public string Security;

       public string Portfolio;
    }

    public enum AESubType
    {
        Trades,
        MarketDepth,
        Portfolio,
        Positions,
        Orders,
        MyTrades
    }
}