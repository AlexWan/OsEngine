using System;
using System.Collections.Generic;
using System.Threading;
using OsEngine.Entity;
using OsEngine.Language;
using OsEngine.Logging;
using OsEngine.Market.Servers.Entity;
using System.IO;


namespace OsEngine.Market.Servers.InteractivBrokers
{
    public class InteractiveBrokersServer : AServer
    {
        public InteractiveBrokersServer()
        {
            InteractiveBrokersServerRealization realization = new InteractiveBrokersServerRealization();
            ServerRealization = realization;

            CreateParameterString("Host", "127.0.0.1");
            CreateParameterInt("Port", 7497);
            CreateParameterButton("Show securities");
            ((ServerParameterButton)ServerParameters[2]).UserClickButton
                += () =>
                {
                    realization.ShowSecuritySubscribleUi();
                };

        }

        /// <summary>
        /// instrument history query
        /// запрос истории по инструменту
        /// </summary>
        public List<Candle> GetCandleHistory(string nameSec, TimeFrame tf)
        {
            return ((InteractiveBrokersServerRealization)ServerRealization).GetCandleHistory(nameSec, tf);
        }
    }

    public class InteractiveBrokersServerRealization : IServerRealization
    {
        public InteractiveBrokersServerRealization()
        {
            ServerStatus = ServerConnectStatus.Disconnect;
            LoadIbSecurities();
        }

        /// <summary>
        /// server type
        /// тип сервера
        /// </summary>
        public ServerType ServerType
        {
            get { return ServerType.InteractivBrokers; }
        }

        /// <summary>
        /// server parameters
        /// параметры сервера
        /// </summary>
        public List<IServerParameter> ServerParameters { get; set; }

        /// <summary>
        /// server time
        /// время сервера
        /// </summary>
        public DateTime ServerTime { get; set; }

        // requests запросы

        /// <summary>
        /// client
        /// </summary>
        private IbClient _client;

        #region Connect / Disconnect

        public ServerConnectStatus ServerStatus { get; set; }

        /// <summary>
        /// release API
        /// освободить апи
        /// </summary>
        public void Dispose()
        {
            if (_client != null)
            {
                _client.ConnectionFail -= _ibClient_ConnectionFail;
                _client.ConnectionSucsess -= _ibClient_ConnectionSucsess;
                _client.LogMessageEvent -= SendLogMessage;
                _client.NewAccauntValue -= _ibClient_NewAccauntValue;
                _client.NewPortfolioPosition -= _ibClient_NewPortfolioPosition;
                _client.NewContractEvent -= _ibClient_NewContractEvent;
                _client.NewMarketDepth -= _ibClient_NewMarketDepth;
                _client.NewMyTradeEvent -= _ibClient_NewMyTradeEvent;
                _client.NewOrderEvent -= _ibClient_NewOrderEvent;
                _client.NewTradeEvent -= AddTick;
                _client.CandlesUpdateEvent -= _client_CandlesUpdateEvent;
                _client.Disconnect();
            }

            _client = null;
            _connectedContracts = new List<string>();

            ServerStatus = ServerConnectStatus.Disconnect;

            if (DisconnectEvent != null)
            {
                DisconnectEvent();
            }
        }

        /// <summary>
        /// connect to API
        /// подсоединиться к апи
        /// </summary>
        public void Connect()
        {
            if (_client == null)
            {
                _client = new IbClient();
                _client.ConnectionFail += _ibClient_ConnectionFail;
                _client.ConnectionSucsess += _ibClient_ConnectionSucsess;
                _client.LogMessageEvent += SendLogMessage;
                _client.NewAccauntValue += _ibClient_NewAccauntValue;
                _client.NewPortfolioPosition += _ibClient_NewPortfolioPosition;
                _client.NewContractEvent += _ibClient_NewContractEvent;
                _client.NewMarketDepth += _ibClient_NewMarketDepth;
                _client.NewMyTradeEvent += _ibClient_NewMyTradeEvent;
                _client.NewOrderEvent += _ibClient_NewOrderEvent;
                _client.NewTradeEvent += AddTick;
                _client.CandlesUpdateEvent += _client_CandlesUpdateEvent;
            }

            _client.Connect(
                ((ServerParameterString)ServerParameters[0]).Value,
                ((ServerParameterInt)ServerParameters[1]).Value);
        }

        /// <summary>
        /// connection is established
        /// соединение установлено
        /// </summary>
        void _ibClient_ConnectionSucsess()
        {
            ServerStatus = ServerConnectStatus.Connect;

            if (ConnectEvent != null)
            {
                ConnectEvent();
            }
        }

        /// <summary>
        /// connection is lost
        /// соединение разорвано
        /// </summary>
        void _ibClient_ConnectionFail()
        {
            ServerStatus = ServerConnectStatus.Disconnect;
            if (DisconnectEvent != null)
            {
                DisconnectEvent();
            }
        }

        #endregion

        #region Securities

        /// <summary>
        /// request securities
        /// запросить бумаги
        /// </summary>
        public void GetSecurities()
        {
            if (_client == null)
            {
                return;
            }

            if (_secIB == null ||
                _secIB.Count == 0)
            {
                SendLogMessage(OsLocalization.Market.Label52, LogMessageType.System);
                Thread.Sleep(15000);
                return;
            }

            if (_namesSubscribleSecurities == null)
            {
                _namesSubscribleSecurities = new List<string>();
            }
            for (int i = 0; i < _secIB.Count; i++)
            {
                string name = _secIB[i].Symbol + "_" + _secIB[i].SecType + "_" + _secIB[i].Exchange + "_" + _secIB[i].Currency + "_" + _secIB[i].LocalSymbol;
                if (_namesSubscribleSecurities.Find(s => s == name) != null)
                {
                    // if we have already subscribed to this instrument / если мы уже подписывались на данные этого инструмента
                    continue;
                }
                _namesSubscribleSecurities.Add(name);

                _client.GetSecurityDetail(_secIB[i]);
            }
        }

        // security. What the user enters for the subscription. Storage and management
        // бумаги. То что пользователь вводит для подписки. Хранение и менеджмент

        /// <summary>
        /// show security settings window
        /// показать окно настроек бумаг
        /// </summary>
        public void ShowSecuritySubscribleUi()
        {
            IbContractStorageUi ui = new IbContractStorageUi(_secIB, this);
            ui.ShowDialog();
            _secIB = ui.SecToSubscrible;
            GetSecurities();
            SaveIbSecurities();
        }

        /// <summary>
        /// security for subscription to server in the IB format
        /// бумаги для подписи у сервера в формате IB
        /// </summary>
        private List<SecurityIb> _secIB = new List<SecurityIb>();

        /// <summary>
        /// names of the instruments on which we have already subscribed
        /// названия инструментов на которые мы уже подписались
        /// </summary>
        private List<string> _namesSubscribleSecurities;

        private List<Security> _securities;

        /// <summary>
        /// save security for connection
        /// сохранить бумаги для подключения
        /// </summary>
        public void SaveIbSecurities()
        {
            try
            {
                using (StreamWriter writer = new StreamWriter(@"Engine\" + @"IbSecuritiesToWatch.txt", false))
                {
                    for (int i = 0; _secIB != null && i < _secIB.Count; i++)
                    {
                        if (_secIB[i] == null)
                        {
                            continue;
                        }
                        string saveStr = "";
                        //saveStr +=  _secToSubscrible[i].ComboLegs + "@";
                        saveStr += _secIB[i].ComboLegsDescription + "@";
                        saveStr += /*_secIB[i].ConId+ */ "@";
                        saveStr += _secIB[i].Currency + "@";
                        saveStr += _secIB[i].Exchange + "@";
                        saveStr += _secIB[i].Expiry + "@";
                        saveStr += _secIB[i].IncludeExpired + "@";
                        saveStr += _secIB[i].LocalSymbol + "@";
                        saveStr += _secIB[i].Multiplier + "@";
                        saveStr += _secIB[i].PrimaryExch + "@";
                        saveStr += _secIB[i].Right + "@";
                        saveStr += /*_secIB[i].SecId + */ "@";
                        saveStr += _secIB[i].SecIdType + "@";
                        saveStr += _secIB[i].SecType + "@";
                        saveStr += _secIB[i].Strike + "@";
                        saveStr += _secIB[i].Symbol + "@";
                        saveStr += _secIB[i].TradingClass + "@";
                        saveStr += _secIB[i].CreateMarketDepthFromTrades + "@";
                        //saveStr += _secToSubscrible[i].UnderComp + "@";

                        writer.WriteLine(saveStr);
                    }
                    writer.Close();
                }
            }
            catch (Exception)
            {
                // ignored
            }
        }

        /// <summary>
        /// upload security for connection
        /// загрузить бумаги для подключения
        /// </summary>
        private void LoadIbSecurities()
        {
            if (!File.Exists(@"Engine\" + @"IbServer.txt"))
            {
                return;
            }

            try
            {
                using (StreamReader reader = new StreamReader(@"Engine\" + @"IbSecuritiesToWatch.txt"))
                {
                    _secIB = new List<SecurityIb>();
                    while (!reader.EndOfStream)
                    {
                        try
                        {
                            SecurityIb security = new SecurityIb();

                            string[] contrStrings = reader.ReadLine().Split('@');

                            security.ComboLegsDescription = contrStrings[0];
                            Int32.TryParse(contrStrings[1],out security.ConId);
                            security.Currency = contrStrings[2];
                            security.Exchange = contrStrings[3];
                            security.Expiry = contrStrings[4];
                            Boolean.TryParse(contrStrings[5],out security.IncludeExpired);
                            security.LocalSymbol = contrStrings[6];
                            security.Multiplier = contrStrings[7];
                            security.PrimaryExch = contrStrings[8];
                            security.Right = contrStrings[9];
                            security.SecId = contrStrings[10];
                            security.SecIdType = contrStrings[11];
                            security.SecType = contrStrings[12];
                            Double.TryParse(contrStrings[13], out security.Strike);
                            security.Symbol = contrStrings[14];
                            security.TradingClass = contrStrings[15];

                            if (contrStrings.Length > 15 &&
                                string.IsNullOrEmpty(contrStrings[16]) == false)
                            {
                                security.CreateMarketDepthFromTrades = Convert.ToBoolean(contrStrings[16]);
                            }

                            _secIB.Add(security);
                        }
                        catch
                        {
                            // ignore
                        }
                    }

                    if (_secIB.Count == 0)
                    {
                        SecurityIb sec1 = new SecurityIb();
                        sec1.Symbol = "AAPL";
                        sec1.Exchange = "SMART";
                        sec1.SecType = "STK";
                        _secIB.Add(sec1);

                        SecurityIb sec2 = new SecurityIb();
                        sec2.Symbol = "FB";
                        sec2.Exchange = "SMART";
                        sec2.SecType = "STK";
                        _secIB.Add(sec2);

                        SecurityIb sec3 = new SecurityIb();
                        sec3.Symbol = "EUR";
                        sec3.Exchange = "IDEALPRO";
                        sec3.SecType = "CASH";
                        _secIB.Add(sec3);

                        SecurityIb sec4 = new SecurityIb();
                        sec4.Symbol = "GBP";
                        sec4.Exchange = "IDEALPRO";
                        sec4.SecType = "CASH";
                        _secIB.Add(sec4);
                    }
                }
            }
            catch (Exception)
            {
                // ignored
            }
        }

        void _ibClient_NewContractEvent(SecurityIb contract)
        {
            try
            {
                if (_securities == null)
                {
                    _securities = new List<Security>();
                }

                SecurityIb securityIb = _secIB.Find(security => security.Symbol == contract.Symbol
                                                                        && security.Exchange == contract.Exchange);

                if (securityIb == null)
                {
                    securityIb = _secIB.Find(security => security.LocalSymbol == contract.LocalSymbol
                                                                        && security.Exchange == contract.Exchange);
                }

                if (securityIb == null)
                {
                    return;
                }

                securityIb.Exchange = contract.Exchange;
                securityIb.Expiry = contract.Expiry;
                securityIb.LocalSymbol = contract.LocalSymbol;
                securityIb.Multiplier = contract.Multiplier;
                securityIb.Right = contract.Right;
                securityIb.ConId = contract.ConId;
                securityIb.Currency = contract.Currency;
                securityIb.Strike = contract.Strike;
                securityIb.MinTick = contract.MinTick;
                securityIb.Symbol = contract.Symbol;
                securityIb.TradingClass = contract.TradingClass;

                //_twsServer.reqMktData(securityIb.ConId, securityIb.Symbol, securityIb.SecType, securityIb.Expiry, securityIb.Strike,
                //    securityIb.Right, securityIb.Multiplier, securityIb.Exchange, securityIb.PrimaryExch, securityIb.Currency,"",true, new TagValueList());
                //_twsServer.reqMktData2(securityIb.ConId, securityIb.LocalSymbol, securityIb.SecType, securityIb.Exchange, securityIb.PrimaryExch, securityIb.Currency, "", false, new TagValueList());
                _client.GetMarketDataToSecurity(securityIb);


                string name = securityIb.Symbol + "_" + securityIb.SecType + "_" + securityIb.Exchange;

                if (_securities.Find(securiti => securiti.Name == name) == null)
                {
                    Security security = new Security();
                    security.Name = name;
                    security.NameFull = name;
                    security.NameClass = securityIb.SecType;

                    if (string.IsNullOrWhiteSpace(security.NameClass))
                    {
                        security.NameClass = "Unknown";
                    }

                    security.PriceStep = Convert.ToDecimal(securityIb.MinTick);
                    security.PriceStepCost = Convert.ToDecimal(securityIb.MinTick);
                    security.Lot = 1;
                    security.PriceLimitLow = 0;
                    security.PriceLimitHigh = 0;
                    security.NameId = name;
                    security.SecurityType = SecurityType.Stock;

                    _securities.Add(security);

                    if (SecurityEvent != null)
                    {
                        SecurityEvent(_securities);
                    }
                }
            }
            catch (Exception error)
            {
                SendLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        #endregion

        #region portfolios

        /// <summary>
        /// request portfolios
        /// запросить портфели
        /// </summary>
        public void GetPortfolios()
        {
            _client.GetPortfolios();
        }

        List<Portfolio> _portfolios;

        void _ibClient_NewAccauntValue(string account, decimal value)
        {
            try
            {
                if (_portfolios == null)
                {
                    _portfolios = new List<Portfolio>();
                }

                Portfolio myPortfolio = _portfolios.Find(portfolio => portfolio.Number == account);

                if (myPortfolio == null)
                {
                    Portfolio newpPortfolio = new Portfolio();
                    newpPortfolio.Number = account;
                    _portfolios.Add(newpPortfolio);
                    myPortfolio = newpPortfolio;
                    myPortfolio.ValueBlocked = 0;
                    SendLogMessage(OsLocalization.Market.Label53 + account, LogMessageType.System);
                }

                myPortfolio.ValueCurrent = value;

                if (PortfolioEvent != null)
                {
                    PortfolioEvent(_portfolios);
                }

                StartListeningPortfolios();
            }
            catch (Exception error)
            {
                SendLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        void _ibClient_NewPortfolioPosition(SecurityIb contract, string accountName, int value)
        {
            try
            {
                if (_portfolios == null ||
               _portfolios.Count == 0)
                {
                    return;
                }
                // see if you already have the right portfolio / смотрим, есть ли уже нужный портфель
                Portfolio portfolio = _portfolios.Find(portfolio1 => portfolio1.Number == accountName);

                if (portfolio == null)
                {
                    //SendLogMessage("обновляли позицию. Не можем найти портфель");
                    return;
                }

                // see if you already have the right Os.Engine security / смотрим, есть ли нужная бумага в формате Os.Engine
                string name = contract.Symbol + "_" + contract.SecType + "_" + contract.Exchange;

                if (_securities == null ||
                    _securities.Find(security => security.Name == name) == null)
                {
                    //SendLogMessage("обновляли позицию. Не можем найти бумагу. " + contract.Symbol);
                    return;
                }

                // update the contract position / обновляем позицию по контракту

                PositionOnBoard positionOnBoard = new PositionOnBoard();

                positionOnBoard.SecurityNameCode = name;
                positionOnBoard.PortfolioName = accountName;
                positionOnBoard.ValueCurrent = value;

                portfolio.SetNewPosition(positionOnBoard);

                if (PortfolioEvent != null)
                {
                    PortfolioEvent(_portfolios);
                }
            }
            catch (Exception error)
            {
                SendLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        private void StartListeningPortfolios()
        {
            Thread.Sleep(3000);

            for (int i = 0; i < _portfolios.Count; i++)
            {
                _client.ListenPortfolio(_portfolios[i].Number);
            }

            Thread.Sleep(5000);
        }


        #endregion

        #region Trades

        private void SendMdFromTrade(Trade trade)
        {
            MarketDepth myDepth = _depths.Find(depth => depth.SecurityNameCode == trade.SecurityNameCode);

            if (myDepth == null)
            {
                myDepth = new MarketDepth();
                myDepth.SecurityNameCode = trade.SecurityNameCode;
                _depths.Add(myDepth);
            }

            myDepth.Time = DateTime.Now;

            Security mySecurity = _securities.Find(security => security.Name == myDepth.SecurityNameCode);

            if (mySecurity == null)
            {
                return;
            }

            List<MarketDepthLevel> bids = myDepth.Bids;
            List<MarketDepthLevel> asks = myDepth.Asks;

            if (asks == null || asks.Count == 0)
            {
                asks = new List<MarketDepthLevel>();
                bids = new List<MarketDepthLevel>();

                asks.Add(new MarketDepthLevel());
                bids.Add(new MarketDepthLevel());

                myDepth.Bids = bids;
                myDepth.Asks = asks;
            }

            if (myDepth.Bids.Count > 1 &&
                myDepth.Asks.Count > 1)
            {
                return;
            }

            myDepth.Asks[0].Price = trade.Price + mySecurity.PriceStep;
            myDepth.Bids[0].Price = trade.Price - mySecurity.PriceStep;

            myDepth.Asks[0].Ask = 1;
            myDepth.Bids[0].Bid = 1;

            if (MarketDepthEvent != null)
            {
                MarketDepthEvent(myDepth.GetCopy());
            }
        }

        private void AddTick(Trade trade, SecurityIb sec)
        {
            try
            {
                if (trade.Price <= 0)
                {
                    return;
                }

                ServerTime = trade.Time;

                SecurityIb contractIb =
                    _secIB.Find(
                        contract =>
                            contract.ConId == sec.ConId);

                if (contractIb != null && contractIb.CreateMarketDepthFromTrades)
                {
                    SendMdFromTrade(trade);
                }

                if (NewTradesEvent != null)
                {
                    NewTradesEvent(trade);
                }
            }
            catch (Exception error)
            {
                SendLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        #endregion

        #region MarketDepth

        /// <summary>
        /// all depths
        /// все стаканы
        /// </summary>
        private List<MarketDepth> _depths = new List<MarketDepth>();

        void _ibClient_NewMarketDepth(int id, int position, int operation, int side, decimal price, int size)
        {

            try
            {
                // take all the necessary data / берём все нужные данные
                SecurityIb myContract = _secIB.Find(contract => contract.ConId == id);

                if (myContract == null)
                {
                    return;
                }

                if (position > 10)
                {
                    return;
                }

                string name = myContract.Symbol + "_" + myContract.SecType + "_" + myContract.Exchange;

                Security mySecurity = _securities.Find(security => security.Name == name);

                if (mySecurity == null)
                {
                    return;
                }

                if (_depths == null)
                {
                    _depths = new List<MarketDepth>();
                }

                MarketDepth myDepth = _depths.Find(depth => depth.SecurityNameCode == name);
                if (myDepth == null)
                {
                    myDepth = new MarketDepth();
                    myDepth.SecurityNameCode = name;
                    _depths.Add(myDepth);
                }

                myDepth.Time = DateTime.Now;

                Side sideLine;
                if (side == 1)
                { // ask/аск
                    sideLine = Side.Buy;
                }
                else
                { // bid/бид
                    sideLine = Side.Sell;
                }

                List<MarketDepthLevel> bids = myDepth.Bids;
                List<MarketDepthLevel> asks = myDepth.Asks;

                if (asks == null || asks.Count < 10)
                {
                    asks = new List<MarketDepthLevel>();
                    bids = new List<MarketDepthLevel>();

                    for (int i = 0; i < 10; i++)
                    {
                        asks.Add(new MarketDepthLevel());
                        bids.Add(new MarketDepthLevel());
                    }
                    myDepth.Bids = bids;
                    myDepth.Asks = asks;
                }

                if (operation == 2)
                {// if need to remove / если нужно удалить

                    if (sideLine == Side.Buy)
                    {
                        // asks.RemoveAt(position);
                        MarketDepthLevel level = bids[position];
                        level.Ask = 0;
                        level.Bid = 0;
                        level.Price = 0;
                    }
                    else if (sideLine == Side.Sell)
                    {
                        //bids.RemoveAt(position);
                        MarketDepthLevel level = asks[position];
                        level.Ask = 0;
                        level.Bid = 0;
                        level.Price = 0;
                    }
                }
                else if (operation == 0 || operation == 1)
                { // need to update / нужно обновить
                    if (sideLine == Side.Buy)
                    {
                        MarketDepthLevel level = bids[position];
                        level.Bid = Convert.ToDecimal(size);
                        level.Ask = 0;
                        level.Price = price;
                    }
                    else if (sideLine == Side.Sell)
                    {
                        MarketDepthLevel level = asks[position];
                        level.Bid = 0;
                        level.Ask = Convert.ToDecimal(size);
                        level.Price = price;
                    }
                }

                if (myDepth.Bids[0].Price != 0 &&
                    myDepth.Asks[0].Price != 0)
                {
                    MarketDepth copy = myDepth.GetCopy();

                    if (MarketDepthEvent != null)
                    {
                        MarketDepthEvent(copy);
                    }
                }
            }
            catch (Exception error)
            {
                SendLogMessage(error.ToString(), LogMessageType.Error);
            }
        }


        #endregion

        #region MyTrades

        void _ibClient_NewMyTradeEvent(MyTrade trade)
        {
            if (trade.Price <= 0)
            {
                return;
            }

            if (MyTradeEvent != null)
            {
                MyTradeEvent(trade);
            }
        }

        #endregion

        #region Orders execution and events

        public void SendOrder(Order order)
        {
            SecurityIb contractIb =
    _secIB.Find(
        contract =>
            contract.Symbol + "_" + contract.SecType + "_" + contract.Exchange ==
            order.SecurityNameCode);

            if (contractIb == null)
            {
                return;
            }

            if (contractIb.MinTick < 1)
            {
                int decimals = 0;
                decimal minTick = Convert.ToDecimal(contractIb.MinTick);

                while (true)
                {
                    minTick = minTick * 10;

                    decimals++;

                    if (minTick > 1)
                    {
                        break;
                    }
                }

                while (true)
                {
                    if (order.Price % Convert.ToDecimal(contractIb.MinTick) != 0)
                    {
                        string minusVal = "0.";

                        for (int i = 0; i < decimals - 1; i++)
                        {
                            minusVal += "0";
                        }
                        minusVal += "1";
                        order.Price -= minusVal.ToDecimal();
                    }
                    else
                    {
                        break;
                    }
                }
            }


            _client.ExecuteOrder(order, contractIb);
        }

        public void CancelOrder(Order order)
        {
            _client.CancelOrder(order);
        }

        void _ibClient_NewOrderEvent(Order order)
        {
            if (MyOrderEvent != null)
            {
                MyOrderEvent(order);
            }
        }

        public void GetOrdersState(List<Order> orders)
        {

        }

        #endregion

        private List<string> _connectedContracts = new List<string>();

        public void Subscrible(Security security)
        {

            SecurityIb contractIb =
           _secIB.Find(
         contract =>
             contract.Symbol + "_" + contract.SecType + "_" + contract.Exchange == security.Name);

            if (contractIb == null)
            {
                return;
            }


            if (_connectedContracts.Find(s => s == security.Name) == null)
            {
                _connectedContracts.Add(security.Name);

                _client.GetMarketDepthToSecurity(contractIb);

            }
        }

        /// <summary>
        /// take candle history for period
        /// взять историю свечек за период
        /// </summary>
        public List<Candle> GetCandleDataToSecurity(Security security, TimeFrameBuilder timeFrameBuilder,
            DateTime startTime, DateTime endTime, DateTime actualTime)
        {
            return null;
            /*  List<Candle> candles = new List<Candle>();

              actualTime = startTime;

              while (actualTime < endTime)
              {
                  List<Candle> newCandles = _client.GetCandlesForTimes(security.Name,
                      timeFrameBuilder.TimeFrameTimeSpan,
                      actualTime, endTime);

                  if (candles.Count != 0 && newCandles.Count != 0)
                  {
                      for (int i = 0; i < newCandles.Count; i++)
                      {
                          if (candles[candles.Count - 1].TimeStart >= newCandles[i].TimeStart)
                          {
                              newCandles.RemoveAt(i);
                              i--;
                          }

                      }
                  }

                  if (newCandles == null)
                  {
                      continue;
                  }

                  if (newCandles.Count == 0)
                  {
                      return candles;
                  }

                  candles.AddRange(newCandles);

                  actualTime = candles[candles.Count - 1].TimeStart;
              }

              if (candles.Count == 0)
              {
                  return null;
              }

              return candles;*/
        }

        /// <summary>
        /// take ticks data on instrument for period
        /// взять тиковые данные по инструменту за период
        /// </summary>
        public List<Trade> GetTickDataToSecurity(Security security, DateTime startTime, DateTime endTime, DateTime lastDate)
        {
            return null;
            /*  string markerDateTime = "";

              List<Trade> trades = new List<Trade>();

              DateTime startOver = startTime;
              long lastId = 0;

              while (true)
              {
                  if (startOver >= endTime)
                  {
                      break;
                  }

                  List<Trade> newTrades = new List<Trade>();

                  if (lastId == 0)
                  {
                      List<Trade> firstTrades = new List<Trade>();

                      do
                      {
                          firstTrades = _client.GetTickHistoryToSecurity(security.Name, startOver, startOver.AddSeconds(60), 0);
                          startOver.AddSeconds(60);
                          Thread.Sleep(10);
                      }
                      while (firstTrades == null || firstTrades.Count == 0);


                      Trade firstTrade = firstTrades.First();

                      lastId = Convert.ToInt64(firstTrade.Id);

                      newTrades.Add(firstTrade);
                  }
                  else
                  {
                      newTrades = _client.GetTickHistoryToSecurity(security.Name, new DateTime(), new DateTime(), lastId + 1);

                      lastId = Convert.ToInt64(newTrades[newTrades.Count - 1].Id);

                  }

                  if (newTrades != null && newTrades.Count != 0)
                      trades.AddRange(newTrades);
                  else
                      break;

                  startOver = trades[trades.Count - 1].Time.AddMilliseconds(1);


                  if (markerDateTime != startOver.ToShortDateString())
                  {
                      markerDateTime = startOver.ToShortDateString();
                      SendLogMessage(security.Name + " Binance Spot start loading: " + markerDateTime, LogMessageType.System);
                  }

                  Thread.Sleep(10);
              }

              if (trades.Count == 0)
              {
                  return null;
              }

              while (trades.Last().Time >= endTime)
                  trades.Remove(trades.Last());


              return trades;*/
        }

        #region Candles request

        /// <summary>
        /// request instrument history
        /// запрос истории по инструменту
        /// </summary>
        public List<Candle> GetCandleHistory(string nameSec, TimeFrame tf)
        {
            SecurityIb contractIb =
_secIB.Find(
contract =>
 contract.Symbol + "_" + contract.SecType + "_" + contract.Exchange == nameSec);

            if (contractIb == null)
            {
                return null; ;
            }

            DateTime timeEnd = DateTime.Now.ToUniversalTime();
            DateTime timeStart = timeEnd.AddMinutes(60);

            string barSize = "1 min";

            int mergeCount = 0;


            if (tf == TimeFrame.Sec1)
            {
                barSize = "1 sec";
                timeStart = timeEnd.AddMinutes(10);
            }
            else if (tf == TimeFrame.Sec5)
            {
                barSize = "5 secs";
            }
            else if (tf == TimeFrame.Sec15)
            {
                barSize = "15 secs";
            }
            else if (tf == TimeFrame.Sec30)
            {
                barSize = "30 secs";
            }
            else if (tf == TimeFrame.Min1)
            {
                timeStart = timeEnd.AddHours(15);
                barSize = "1 min";
            }
            else if (tf == TimeFrame.Min5)
            {
                timeStart = timeEnd.AddHours(50);
                barSize = "5 mins";
            }
            else if (tf == TimeFrame.Min15)
            {
                timeStart = timeEnd.AddHours(150);
                barSize = "15 mins";
            }
            else if (tf == TimeFrame.Min30)
            {
                timeStart = timeEnd.AddHours(250);
                barSize = "30 mins";
            }
            else if (tf == TimeFrame.Hour1)
            {
                timeStart = timeEnd.AddHours(1300);
                barSize = "1 hour";
            }
            else if (tf == TimeFrame.Hour2)
            {
                timeStart = timeEnd.AddHours(2100);
                barSize = "1 hour";
                mergeCount = 2;
            }
            else if (tf == TimeFrame.Hour4)
            {
                timeStart = timeEnd.AddHours(4200);
                barSize = "1 hour";
                mergeCount = 4;
            }
            else if (tf == TimeFrame.Day)
            {
                barSize = "1 day";
                timeStart = timeEnd.AddDays(701);
            }
            else
            {
                return null;
            }

            CandlesRequestResult = null;

            _client.GetCandles(contractIb, timeEnd, timeStart, barSize, "TRADES");

            DateTime startSleep = DateTime.Now;

            while (true)
            {
                Thread.Sleep(1000);

                if (startSleep.AddSeconds(15) < DateTime.Now)
                {
                    break;
                }

                if (CandlesRequestResult != null)
                {
                    break;
                }
            }

            if (CandlesRequestResult != null &&
                CandlesRequestResult.CandlesArray.Count != 0)
            {
                if (mergeCount != 0)
                {
                    return Merge(CandlesRequestResult.CandlesArray, mergeCount);
                }

                return CandlesRequestResult.CandlesArray;
            }


            _client.GetCandles(contractIb, timeEnd, timeStart, barSize, "MIDPOINT");

            startSleep = DateTime.Now;

            while (true)
            {
                Thread.Sleep(1000);

                if (startSleep.AddSeconds(15) < DateTime.Now)
                {
                    break;
                }

                if (CandlesRequestResult != null)
                {
                    break;
                }
            }

            if (CandlesRequestResult != null &&
                CandlesRequestResult.CandlesArray.Count != 0)
            {
                if (mergeCount != 0)
                {
                    return Merge(CandlesRequestResult.CandlesArray, mergeCount);
                }

                return CandlesRequestResult.CandlesArray;
            }

            return null;
        }

        private void _client_CandlesUpdateEvent(Candles series)
        {
            CandlesRequestResult = series;
        }

        private Candles CandlesRequestResult = null;

        public List<Candle> Merge(List<Candle> candles, int countMerge)
        {
            if (countMerge <= 1)
            {
                return candles;
            }

            if (candles == null ||
                candles.Count == 0 ||
                candles.Count < countMerge)
            {
                return candles;
            }


            List<Candle> mergeCandles = new List<Candle>();

            // we know the initial index.        
            // узнаём начальный индекс

            int firstIndex = 0;

            // " Gathering
            // собираем

            for (int i = firstIndex; i < candles.Count;)
            {
                int countReal = countMerge;

                if (countReal + i > candles.Count)
                {
                    countReal = candles.Count - i;
                }
                else if (i + countMerge < candles.Count &&
                    candles[i].TimeStart.Day != candles[i + countMerge].TimeStart.Day)
                {
                    countReal = 0;

                    for (int i2 = i; i2 < candles.Count; i2++)
                    {
                        if (candles[i].TimeStart.Day == candles[i2].TimeStart.Day)
                        {
                            countReal += 1;
                        }
                        else
                        {
                            break;
                        }
                    }
                }
                if (countReal == 0)
                {
                    break;
                }

                if (candles[i].TimeStart.Hour == 10 && candles[i].TimeStart.Minute == 1 &&
                    countReal == countMerge)
                {
                    countReal -= 1;
                }

                mergeCandles.Add(Concate(candles, i, countReal));
                i += countReal;

            }

            return mergeCandles;
        }

        private Candle Concate(List<Candle> candles, int index, int count)
        {
            Candle candle = new Candle();

            candle.Open = candles[index].Open;
            candle.High = Decimal.MinValue;
            candle.Low = Decimal.MaxValue;
            candle.TimeStart = candles[index].TimeStart;

            for (int i = index; i < candles.Count && i < index + count; i++)
            {
                if (candles[i].Trades != null)
                {
                    candle.Trades.AddRange(candles[i].Trades);
                }

                candle.Volume += candles[i].Volume;

                if (candles[i].High > candle.High)
                {
                    candle.High = candles[i].High;
                }

                if (candles[i].Low < candle.Low)
                {
                    candle.Low = candles[i].Low;
                }

                candle.Close = candles[i].Close;
            }

            return candle;
        }

        #endregion



        /// <summary>
        /// called when order changed
        /// вызывается когда изменился ордер
        /// </summary>
        public event Action<Order> MyOrderEvent;

        /// <summary>
        /// called when my trade changed
        /// вызывается когда изменился мой трейд
        /// </summary>
        public event Action<MyTrade> MyTradeEvent;

        /// <summary>
        /// new portfolios appeared
        /// появились новые портфели
        /// </summary>
        public event Action<List<Portfolio>> PortfolioEvent;

        /// <summary>
        /// new securities
        /// новые бумаги
        /// </summary>
        public event Action<List<Security>> SecurityEvent;

        /// <summary>
        /// new depth
        /// новый стакан
        /// </summary>
        public event Action<MarketDepth> MarketDepthEvent;

        /// <summary>
        /// new trade
        /// новый трейд
        /// </summary>
        public event Action<Trade> NewTradesEvent;

        /// <summary>
        /// API connection established
        /// соединение с API установлено
        /// </summary>
        public event Action ConnectEvent;

        /// <summary>
        /// API connection lost
        /// соединение с API разорвано
        /// </summary>
        public event Action DisconnectEvent;

        // log messages сообщения для лога

        /// <summary>
        /// add a new log message
        /// добавить в лог новое сообщение
        /// </summary>
        private void SendLogMessage(string message, LogMessageType type)
        {
            if (LogMessageEvent != null)
            {
                LogMessageEvent(message, type);
            }
        }

        /// <summary>
        /// outgoing log message
        /// исходящее сообщение для лога
        /// </summary>
        public event Action<string, LogMessageType> LogMessageEvent;
    }
}
