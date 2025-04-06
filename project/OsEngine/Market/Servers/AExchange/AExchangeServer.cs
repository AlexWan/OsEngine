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

        private void UpdateMyTrade(string message)
        {
            WebSocketTradeMessage trade = JsonConvert.DeserializeObject<WebSocketTradeMessage>(message, _jsonSettings);
            MyTrade newTrade = new MyTrade();
            newTrade.SecurityNameCode = trade.Ticker;
            newTrade.NumberTrade = trade.TradeId.ToString();
            newTrade.NumberOrderParent = trade.OrderId.ToString();
            newTrade.Volume = Math.Abs(trade.Shares);
            newTrade.Price = trade.Price;
            newTrade.Time = trade.Moment;
            newTrade.Side = trade.Shares > 0 ? Side.Buy : Side.Sell;

            MyTradeEvent!(newTrade);
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
        }

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
            command.Id = Interlocked.Increment(ref _messageId);
            command.Timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

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
                    }
                }
            }
            catch
            {

            }
            finally
            {
                _ws = null;
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

                    if (_ws.Ping() == false)
                    {
                        SendLogMessage("AE connector. WARNING. Sockets Ping Pong does not work. No internet or the server AE is not available", LogMessageType.Error);
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

        public event Action<Order> MyOrderEvent;

        public event Action<MyTrade> MyTradeEvent;

        #endregion

        #region 11 Trade

        private RateGate _rateGateSendOrder = new RateGate(1, TimeSpan.FromMilliseconds(350));
        private RateGate _rateGateCancelOrder = new RateGate(1, TimeSpan.FromMilliseconds(350));

        public void SendOrder(Order order)
        {
            _rateGateSendOrder.WaitToProceed();

            try
            {
                // генерируем новый номер ордера и добавляем его в словарь
                Guid newUid = Guid.NewGuid();
                string orderId = newUid.ToString();

                _orderNumbers.Add(orderId, order.NumberUser);

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
                SendLogMessage("Order send error " + exception.ToString(), LogMessageType.Error);
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
                    OrderId = int.Parse(order.NumberMarket),
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

        private List<Order> GetAllOrdersFromExchange()
        {
            List<Order> orders = new List<Order>();

            return orders;
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