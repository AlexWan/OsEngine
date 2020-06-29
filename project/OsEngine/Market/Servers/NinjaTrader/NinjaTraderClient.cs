/*
 *Your rights to use the code are governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 *Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using OsEngine.Entity;
using OsEngine.Language;
using OsEngine.Logging;

namespace OsEngine.Market.Servers.NinjaTrader
{
    public class NinjaTraderClient
    {
        /// <summary>
		/// class realizing the connection to Ninja
        /// класс реализующий подключение к нинзе
        /// </summary>
        public NinjaTraderClient(string serverAddres, string port)
        {
            Thread worker = new Thread(WorkerPlace);
            worker.IsBackground = true;
            worker.Start();
        }

        /// <summary>
		/// dispose the object
        /// освободить объект
        /// </summary>
        public void Dispose()
        {
            _messagesToSend.Enqueue("Disconnect");

            Thread.Sleep(1000);

            _threadsNeadToStop = true;

            _marketDepths = new List<MarketDepth>();
        }

        /// <summary>
		/// connect
        /// подключиться 
        /// </summary>
        public void Connect()
        {
            _messagesToSend.Enqueue("Connect");
        }

        public bool IsConnected;

// request to Ninja
// запросы к Нинзе

        /// <summary>
		/// request list of securities
        /// запросить список бумаг
        /// </summary>
        public void GetSecurities()
        {
            _messagesToSend.Enqueue("GetSecurities");
        }

        /// <summary>
		/// request list of portfolios
        /// запросить список портфелей
        /// </summary>
        public void GetPortfolios()
        {
            _messagesToSend.Enqueue("GetAccaunts");
        }

        /// <summary>
		/// subscribe to depths and trades
        /// подписаться на стаканы и трейды
        /// </summary>
        public void SubscribleTradesAndDepths(Security securityName)
        {
            //string message = "Subscrible@" + securityName;
            //_messagesToSend.Enqueue(message);
        }

        /// <summary>
		/// execute order
        /// исполнить ордер
        /// </summary>
        public void ExecuteOrder(Order order)
        {
            string orderMes = "OrderExecute" + "@";
            orderMes += order.SecurityNameCode +"#";
            orderMes += order.PortfolioNumber + "#";
            orderMes += order.NumberUser + "#";
            orderMes += order.Side + "#";
            orderMes += order.Price + "#";
            orderMes += order.Volume + "#";

            _messagesToSend.Enqueue(orderMes);
        }

        /// <summary>
		/// cancel order
        /// отозвать ордер
        /// </summary>
        public void CancelOrder(Order order)
        {
            string orderMes = "OrderCancel" + "@";
            orderMes += order.SecurityNameCode + "#";
            orderMes += order.PortfolioNumber + "#";
            orderMes += order.NumberMarket + "#";
            orderMes += order.NumberUser + "#";
            orderMes += order.Price + "#";
            orderMes += order.Volume + "#";

            _messagesToSend.Enqueue(orderMes);
        }

// thread sending request messages
// поток отсылающий сообщения с запросами

        /// <summary>
		/// whether need threads to be stopped
        /// нужно ли чтобы потоки были остановлены
        /// </summary>
        private bool _threadsNeadToStop;

        /// <summary>
		/// messages for sending to Ninja
        /// сообщения для отправки в Нинзю
        /// </summary>
        private ConcurrentQueue<string> _messagesToSend = new ConcurrentQueue<string>();

        /// <summary>
		/// work place of thread communicating with Ninja
        /// место работы потока поддерживающего связь с нинзей
        /// </summary>
        private void WorkerPlace()
        {
            while (true)
            {
                Thread.Sleep(100);
                try
                {
                    if (_threadsNeadToStop == true)
                    {
                        return;
                    }
                    if (_messagesToSend.IsEmpty)
                    { // request any incoming data for us that are saving in server / запрос каких-либо входящих данных для нас, которые копятся в сервере
                        IncomeMessageFromSender(SendMessage("Process"));
                        continue;
                    }

                    string message = null;
                    _messagesToSend.TryDequeue(out message);

                    if (message == null)
                    {
                        continue;
                    }

                    IncomeMessageFromSender(SendMessage(message));

                }
                catch (Exception error)
                {
                    SendLogMessage(error.ToString(), LogMessageType.Error);
                }
            }
        }

        /// <summary>
		/// send message to Ninja
        /// послать сообщение в нинзю
        /// </summary>
        private string SendMessage(string message)
        {
            // Connect to remote device / Соединяемся с удаленным устройством

            // Set a remote point for socket / Устанавливаем удаленную точку для сокета
            IPHostEntry ipHost = Dns.GetHostEntry("localhost");
            IPAddress ipAddr = ipHost.AddressList[1];
            IPEndPoint ipEndPoint = new IPEndPoint(ipAddr, 11000);

            Socket sender = new Socket(ipAddr.AddressFamily, SocketType.Stream, ProtocolType.Tcp);

            // Connecting socket to remote point / Соединяем сокет с удаленной точкой
            try
            {
                sender.Connect(ipEndPoint);
            }
            catch (Exception)
            {
                SendLogMessage(OsLocalization.Market.Message76,
                    LogMessageType.Error);
                Thread.Sleep(10000);
            }


            byte[] msg = Encoding.UTF8.GetBytes(message);

            // send data through socket / Отправляем данные через сокет
            sender.Send(msg);


            // Input buffer / Буфер для входящих данных
            byte[] bytes = new byte[1024];

            if (message == "GetSecurities")
            {
                bytes = new byte[1048576];
            }

            // get response from the server / Получаем ответ от сервера
            int bytesRec = sender.Receive(bytes);

            string request = Encoding.UTF8.GetString(bytes, 0, bytesRec);

            // clear socket / Освобождаем сокет
            sender.Shutdown(SocketShutdown.Both);
            sender.Close();

            return request;
        }

// processing responses after our requests
// обработка ответов после наших запросов     

        /// <summary>
		/// place to sort incoming data from Ninja
        /// место сортировки входящих от нинзи данных
        /// </summary>
        /// <param name="message"></param>
        private void IncomeMessageFromSender(string message)
        {
            if (string.IsNullOrEmpty(message))
            {
                return;
            }

            string tag = message.Split('@')[0];

            if (tag == "Connect")
            {
                IsConnected = true;
                if (Connected != null)
                {
                    Connected();
                }
            }
            else if (tag == "Disconnect")
            {
                IsConnected = false;

                if (Disconnected != null)
                {
                    Disconnected();
                }
            }
            else if (tag == "securities")
            {
                LoadSecurities(message.Split('@')[1]);
            }
            else if (tag == "portfoliosOnStart")
            {
                LoadPortfoliosOnStart(message.Split('@')[1]);
            }
            else if (tag == "trades")
            {
                LoadTrades(message.Split('@')[1]);
            }
            else if (tag == "error")
            {
                SendLogMessage("Ninja script error " + message, LogMessageType.Error);
            }
            else if (tag == "orders")
            {
                LoadOrders(message.Split('@')[1]);
            }
            else if (tag == "myTrades")
            {
                LoadMyTrades(message.Split('@')[1]);
            }
            else if (tag == "positions")
            {
                LoadPositions(message.Split('@')[1]);
            }
            else if (tag == "portfolios")
            {
                LoadPortfolios(message.Split('@')[1]);
            }
            else if (tag == "marketDepth")
            {
                LoadMarketDepths(message.Split('@')[1]);
            }
            else if (tag == "orderExecuteReport")
            {
                OrderExecuteReport(message.Split('@')[1]);
            }
            else if (tag == "orderCanselReport")
            {
                OrderCanselReport(message.Split('@')[1]);
            }
        }

        /// <summary>
		/// list of depths that we get from Ninja
        /// список стаканов которые мы получаем из нинзи
        /// </summary>
        private List<MarketDepth> _marketDepths = new List<MarketDepth>();

        /// <summary>
		/// order withdrawal notification has been received
        /// пришло оповещение об отзыве ордера
        /// </summary>
        private void OrderCanselReport(string message)
        {
            string[] array = message.Split('#');

            if (array[0] == "Cancel")
            {
                Order errorOrder = new Order();
                errorOrder.NumberMarket = array[1];
                errorOrder.NumberUser = Convert.ToInt32(array[2]);

                errorOrder.State = OrderStateType.Fail;

                if (MyOrderEvent != null)
                {
                    MyOrderEvent(errorOrder);
                }
            }
            else
            {
                SendLogMessage(message.Split('@')[1], LogMessageType.System);
            }
        }

        /// <summary>
		/// parsing incoming messages about order execution
        /// разбор входящих сообощений об исполнении ордера
        /// </summary>
        private void OrderExecuteReport(string message)
        {
            string[] str = message.Split('#');

            if (str[0] == "Error")
            {
                Order errorOrder = new Order();
                errorOrder.NumberUser = Convert.ToInt32(str[2]);

                errorOrder.State = OrderStateType.Fail;

                if (MyOrderEvent != null)
                {
                    MyOrderEvent(errorOrder);
                }

                SendLogMessage("Order # " + errorOrder.NumberUser + " dont execute. Error: " + message, LogMessageType.Error);
            }
            if (str[0] == "Accept")
            {
                Order order = new Order();
                order.NumberUser = Convert.ToInt32(str[1]);
                order.NumberMarket = str[3];
                order.State = OrderStateType.Activ;
                order.TimeCallBack = Convert.ToDateTime(str[4], CultureInfo.InvariantCulture);

                if (MyOrderEvent != null)
                {
                    MyOrderEvent(order);
                }

            }


        }

        /// <summary>
        /// parsing incoming messages about depth
        /// разбор входящих сообощений о стакане
        /// </summary>
        private void LoadMarketDepths(string message)
        {
            /* string marketDepthStr = marketDepthUpdate.Instrument.FullName + "#"; // бумага

             if (marketDepthUpdate.MarketDataType == MarketDataType.Ask)
             {
                 marketDepthStr += "Ask" + "#";
             }
             else
             {
                 marketDepthStr += "Bid" + "#";
             }

             //Operation.Add //Operation.Remove //Operation.Update
             
             marketDepthStr += marketDepthUpdate.Operation + "#"; // операция

             marketDepthStr += marketDepthUpdate.Price + "#"; // цена
             marketDepthStr += marketDepthUpdate.Volume + "#"; // объём на уровне

             marketDepthStr += marketDepthUpdate.Time + "$"; // время*/


            string[] messageInArray = message.Split('$');

            for (int i = 0; i < messageInArray.Length - 1; i++)
            {
                string[] str = messageInArray[i].Split('#');

                MarketDepthLevel level = new MarketDepthLevel();

                if (str[1] == "Ask")
                {
                    level.Ask = str[4].ToDecimal();
                }
                else
                {
                    level.Bid = str[4].ToDecimal();
                }

                level.Price = str[3].ToDecimal();

                MarketDepth myDepth = _marketDepths.Find(m => m.SecurityNameCode == str[0]);

                if (myDepth == null)
                {
                    myDepth = new MarketDepth();
                    myDepth.SecurityNameCode = str[0];
                    _marketDepths.Add(myDepth);
                }

                myDepth.Time = Convert.ToDateTime(str[5], CultureInfo.InvariantCulture);

                if (myDepth.Bids == null)
                {
                    myDepth.Bids = new List<MarketDepthLevel>();
                }

                if (myDepth.Asks == null)
                {
                    myDepth.Asks = new List<MarketDepthLevel>();
                }

                //Operation.Add //Operation.Remove //Operation.Update
                if (str[1] == "Ask" && (str[2] == "Add" || str[2] == "Update"))
                {
                    AddAsk(myDepth.Asks, level);
                }

                if (str[1] == "Ask" && str[2] == "Remove")
                {
                    Remove(myDepth.Asks, level);
                }

                if (str[1] == "Bid" && (str[2] == "Add" || str[2] == "Update"))
                {
                    AddBid(myDepth.Bids, level);
                }

                if (str[1] == "Bid" && str[2] == "Remove")
                {
                    Remove(myDepth.Bids, level);
                }

                if (myDepth.Bids != null &&
                    myDepth.Bids.Count > 0 &&
                    myDepth.Asks != null &&
                    myDepth.Asks.Count > 0)

                {
                    while(str[1] == "Bid" && (str[2] == "Add" || str[2] == "Update") &&
                          myDepth.Asks.Count > 0 &&
                        myDepth.Bids[0].Price >= myDepth.Asks[0].Price)
                    {
                        myDepth.Asks.RemoveAt(0);
                    }

                    while (str[1] == "Ask" && (str[2] == "Add" || str[2] == "Update") &&
                        myDepth.Bids.Count > 0 &&
                        myDepth.Bids[0].Price >= myDepth.Asks[0].Price)
                    {
                        myDepth.Bids.RemoveAt(0);
                    }

                }

                while (myDepth.Asks != null &&
                       myDepth.Asks.Count > 10)
                {
                    myDepth.Asks.RemoveAt(myDepth.Asks.Count - 1);
                }

                while (myDepth.Bids != null &&
                       myDepth.Bids.Count > 10)
                {
                    myDepth.Bids.RemoveAt(myDepth.Bids.Count - 1);
                }

                if (myDepth.Time == DateTime.MinValue)
                {
                    return;
                }

                if (myDepth.Asks == null ||
                    myDepth.Bids == null ||
                    myDepth.Asks.Count == 0||
                    myDepth.Bids.Count == 0)
                {
                    return;
                }

                if (UpdateMarketDepth != null)
                {
                    UpdateMarketDepth(myDepth.GetCopy());
                }
            }
        }

        private void AddBid(List<MarketDepthLevel> levels, MarketDepthLevel newLevel)
        { // buy levels. with index zero greater value / уровни покупок.  с индексом ноль бОльшее значение 
            for (int i = 0; i < levels.Count; i++)
            {
                if (levels[i].Price == newLevel.Price)
                {
                    levels[i] = newLevel;
                    return;
                }

                if (levels[i].Price < newLevel.Price)
                {
                    levels.Insert(i,newLevel);
                    return;
                }
            }

            levels.Add(newLevel);
        }

        private void AddAsk(List<MarketDepthLevel> levels, MarketDepthLevel newLevel)
        {// sale levels. with index zero lower value / уровни продаж.  с индексом ноль меньшее значение 
            for (int i = 0; i < levels.Count; i++)
            {
                if (levels[i].Price == newLevel.Price)
                {
                    levels[i] = newLevel;
                    return;
                }

                if (levels[i].Price > newLevel.Price)
                {
                    levels.Insert(i, newLevel);
                    return;
                }
            }

            levels.Add(newLevel);
        }

        private void Remove(List<MarketDepthLevel> levels, MarketDepthLevel newLevel)
        {
            for (int i = 0; i < levels.Count; i++)
            {
                if (levels[i].Price == newLevel.Price)
                {
                    levels.RemoveAt(i);
                    return;
                }
            }
        }

        /// <summary>
		/// parsing incoming messages about portfolios
        /// разбор входящих сообщений о портфелях
        /// </summary>
        private void LoadPortfolios(string message)
        {
           /* string portfolioStr = account.Name + "#"; // portfolio / портфель

            portfolioStr += value + "$"; // current amount of money in the account / текущее кол-во денег на счёте
            */
            if (_portfolios == null)
            {
                _portfolios = new List<Portfolio>();
            }

            string[] messageInArray = message.Split('$');

            for (int i = 0; i < messageInArray.Length - 1; i++)
            {
                string[] trdStr = messageInArray[i].Split('#');

                Portfolio newPos = new Portfolio();
                newPos.Number = trdStr[0];
                newPos.ValueCurrent= trdStr[1].ToDecimal();

                Portfolio myPortfolio = _portfolios.Find(p => p.Number == newPos.Number);

                if (myPortfolio == null)
                {
                   _portfolios.Add(newPos);
                }
                else
                {
                    myPortfolio.ValueCurrent = newPos.ValueCurrent;
                }

                if (UpdatePortfolio != null)
                {
                    UpdatePortfolio(_portfolios);
                }
            }
        }
        
        private List<Portfolio> _portfolios;

        /// <summary>
		/// parsing incoming messages about positions
        /// разбор входящих сообщений о позициях
        /// </summary>
        private void LoadPositions(string message)
        {
            /*string positionstr = position.Instrument.FullName + "#"; // security / бумага
            positionstr += position.Account.Name + "#"; // portfolio name / название портфеля
            positionstr += position.Quantity + "$"; // current volume in the market / текущий объём на рынке
            */

            if (_portfolios == null || _portfolios.Count == 0)
            {
                return;
            }

            string[] messageInArray = message.Split('$');

            for (int i = 0; i < messageInArray.Length - 1; i++)
            {
                string[] trdStr = messageInArray[i].Split('#');

                PositionOnBoard newPos = new PositionOnBoard();
                newPos.SecurityNameCode = trdStr[0];
                newPos.PortfolioName= trdStr[1];
                newPos.ValueCurrent = trdStr[2].ToDecimal();

                Portfolio myPortfolio = _portfolios.Find(p => p.Number == newPos.PortfolioName);

                if (myPortfolio == null)
                {
                    continue;
                }

                myPortfolio.SetNewPosition(newPos);

                if (UpdatePortfolio != null)
                {
                    UpdatePortfolio(_portfolios);
                }
            }
        }
        
        /// <summary>
		/// parsing incoming messages about trades
        /// разбор входящих сообщений о трейдах
        /// </summary>
        private void LoadMyTrades(string message)
        {
           /* string tradeStr = execution.Instrument.FullName + "#"; // security name / имя инструмента
            tradeStr += execution.ExecutionId + "#"; // number in the exchange / номер на бирже
            tradeStr += execution.OrderId + "#"; // order number / номер ордера по которому прошла сделка
            tradeStr += execution.Price + "#"; // matching price / цена сведения
            tradeStr += execution.Quantity + "#"; // volume / объём

            if (execution.MarketPosition == MarketPosition.Long)
            {
                tradeStr += "Buy" + "#";
            }
            else
            {
                tradeStr += "Sell" + "#";
            }

            tradeStr += execution.Time + "$";*/


            string[] messageInArray = message.Split('$');

            for (int i = 0; i < messageInArray.Length - 1; i++)
            {
                string[] trdStr = messageInArray[i].Split('#');

                MyTrade newMyTrade = new MyTrade();
                newMyTrade.SecurityNameCode = trdStr[0];
                newMyTrade.NumberTrade= trdStr[1];
                newMyTrade.NumberOrderParent = trdStr[2];
                newMyTrade.Price = trdStr[3].ToDecimal();
                newMyTrade.Volume= trdStr[4].ToDecimal();
                Enum.TryParse(trdStr[5], out newMyTrade.Side);
                newMyTrade.Time= Convert.ToDateTime(trdStr[6], CultureInfo.InvariantCulture);

                if (MyTradeEvent != null)
                {
                    MyTradeEvent(newMyTrade);
                }
            }
        }

        /// <summary>
		/// parsing incoming messages about orders
        /// разбор входящих сообщений о ордерах
        /// </summary>
        private void LoadOrders(string message)
        {
          /*  
           * string orderstr = order.ClientId + "#"; // номер который ему дал клиент
            orderstr += order.Id + "#"; // номер на бирже
            orderstr += order.Instrument.FullName + "#"; // имя инструмента
            orderstr += order.Quantity + "#"; // весь объём ордера
            orderstr += order.Filled + "#"; // исполнено
            orderstr += order.LimitPrice + "#"; // цена ордера
            orderstr += order.Account.Name + "#"; // портфель

            if (order.OrderAction == OrderAction.Buy ||
                order.OrderAction == OrderAction.BuyToCover)
            {
                orderstr += "Buy" + "#";
            }
            else
            {
                orderstr += "Sell" + "#";
            }

            if
                (order.OrderState == OrderState.Initialized ||
                order.OrderState == OrderState.Accepted ||
                order.OrderState == OrderState.Submitted ||
                order.OrderState == OrderState.Working)
            {
                orderstr += "Activ" + "#";
            }
            else if
                (order.OrderState == OrderState.Cancelled ||
                order.OrderState == OrderState.Rejected ||
                order.OrderState == OrderState.CancelPending ||
                order.OrderState == OrderState.CancelSubmitted)
            {
                orderstr += "Cansel" + "#";
            }
            else if
                (order.OrderState == OrderState.PartFilled)
            {
                orderstr += "Partial" + "#";
            }
            else if
                (order.OrderState == OrderState.Filled)
            {
                orderstr += "Done" + "#";
            }
            else
            {
                return;
            }


            orderstr += order.Time + "$";*/

            string[] messageInArray = message.Split('$');

            for (int i = 0; i < messageInArray.Length - 1; i++)
            {
                string[] ordStr = messageInArray[i].Split('#');

                Order newOrder = new Order();
                newOrder.NumberUser = Convert.ToInt32(ordStr[0]);
                newOrder.NumberMarket= ordStr[1];
                newOrder.SecurityNameCode = ordStr[2];
                newOrder.Volume = ordStr[3].ToDecimal();
                newOrder.VolumeExecute = ordStr[4].ToDecimal();
                newOrder.Price = ordStr[5].ToDecimal();
                newOrder.PortfolioNumber = ordStr[6];
                Enum.TryParse(ordStr[7], out newOrder.Side);

                OrderStateType state;
                Enum.TryParse(ordStr[8], out state);
                newOrder.State = state;
                newOrder.TimeCallBack = Convert.ToDateTime(ordStr[9], CultureInfo.InvariantCulture);

                if (MyOrderEvent != null)
                {
                    MyOrderEvent(newOrder);
                }
            }
        }

        /// <summary>
        /// parsing incoming messages about list of securities
        /// разбор входящих сообщений о списке бумаг
        /// </summary>
        private void LoadSecurities(string message)
        {
            // name,NameFull, NameId, nameClass, PriceStep, PriceStepCost

            /* string newSecurity = "";

             newSecurity += securities[i].FullName + "#";
             newSecurity += securities[i].Id + "#";
             newSecurity += securities[i].MasterInstrument.InstrumentType + "#";
             newSecurity += securities[i].MasterInstrument.TickSize + "#";
             newSecurity += securities[i].MasterInstrument.PointValue + "#";
             result.Append(newSecurity + "&");*/

            List<Security> securities = new List<Security>();

            string[] securityArray = message.Split('&');

            for (int i = 0; i < securityArray.Length - 1; i++)
            {
                Security newSecurity = new Security();

                string[] sec = securityArray[i].Split('%');

                if (sec[0] == "6B 09-18")
                {

                }

                try
                {
                    newSecurity.Name = sec[0];
                    newSecurity.NameFull = sec[0];
                    newSecurity.NameId = sec[1];
                    newSecurity.NameClass = sec[2];
                    newSecurity.PriceStep =
                            sec[3].ToDecimal();
                    newSecurity.PriceStepCost =
                            sec[4].ToDecimal();
                    newSecurity.Lot = 1;

                    if (newSecurity.NameClass == "Stock")
                    {
                        newSecurity.SecurityType = SecurityType.Stock;
                    }
                    else if (newSecurity.NameClass == "Future")
                    {
                        newSecurity.SecurityType = SecurityType.Futures;
                    }
                    else if (newSecurity.NameClass == "Index")
                    {
                        newSecurity.SecurityType = SecurityType.Index;
                    }
                    else if (newSecurity.NameClass == "Forex")
                    {
                        newSecurity.SecurityType = SecurityType.CurrencyPair;
                    }
                }
                catch (Exception)
                {

                }

                securities.Add(newSecurity);
            }

            if (UpdateSecuritiesEvent != null)
            {
                UpdateSecuritiesEvent(securities);
            }
        }

        /// <summary>
		/// parsing incoming messages about portfolios
        /// разбор входящих сообощений о портфелях
        /// </summary>
        private void LoadPortfoliosOnStart(string message)
        {
            /*StringBuilder result = new StringBuilder();

            for (int i = 0; i < accounts.Count; i++)
            {
                // number             , valueBegin, valueCerrent, valueBlocked

                string newAccount = "";

                newAccount += accounts[i].Name + "#";
                //newAccount += accounts[i].BPAccountStatistics. + "#";

                result.Append(newAccount + "&");
            }*/

            if(_portfolios == null)
            { _portfolios = new List<Portfolio>();}

            string[] securityArray = message.Split('&');

            for (int i = 0; i < securityArray.Length - 1; i++)
            {
                Portfolio newPortfolio = new Portfolio();
                newPortfolio.Number = securityArray[i];

                if (_portfolios.Find(port => port.Number == newPortfolio.Number) == null)
                {
                    _portfolios.Add(newPortfolio);
                }
            }

            if (UpdatePortfolio != null)
            {
                UpdatePortfolio(_portfolios);
            }
        }

        /// <summary>
		/// parsing incoming messages about trades
        /// разбор входящих сообощений о трейдах
        /// </summary>
        private void LoadTrades(string str)
        {
            /*try
            {
				string trade = marketDataUpdate.Instrument.FullName + "#";
				trade += marketDataUpdate.Price + "#";
				trade += marketDataUpdate.Volume + "#";
				trade += marketDataUpdate.Time + "$";	
			    _tradesToSend.Enqueue(trade);
            }
            catch (Exception error)
            {
                MessageBox.Show(error.ToString());
            }*/

            string[] trades = str.Split('$');

            for (int i = 0; i < trades.Length - 1; i++)
            {
                string[] tradeInArray = trades[i].Split('#');

                Trade newTrade = new Trade();

                newTrade.SecurityNameCode = tradeInArray[0];
                newTrade.Side = Side.Buy;
                newTrade.Price =
                        tradeInArray[1].ToDecimal();
                newTrade.Volume =
                        tradeInArray[2].ToDecimal();
                newTrade.Time = Convert.ToDateTime(tradeInArray[3], CultureInfo.InvariantCulture);

                if (NewTradesEvent != null)
                {
                    NewTradesEvent(newTrade);
                }
            }
        }

		// outgoing events
        // исходящие события
        
        /// <summary>
		/// my new orders
        /// новые мои ордера
        /// </summary>
        public event Action<Order> MyOrderEvent;

        /// <summary>
		/// my new trades
        /// новые мои сделки
        /// </summary>
        public event Action<MyTrade> MyTradeEvent;

        /// <summary>
		/// update portfolio event
        /// событие обновления портфеля
        /// </summary>
        public event Action<List<Portfolio>> UpdatePortfolio;

        /// <summary>
		/// new securities in the system 
        /// новые бумаги в системе
        /// </summary>
        public event Action<List<Security>> UpdateSecuritiesEvent;

        /// <summary>
		/// updated depth
        /// обновился стакан
        /// </summary>
        public event Action<MarketDepth> UpdateMarketDepth;

        /// <summary>
		/// updated ticks
        /// обновились тики
        /// </summary>
        public event Action<Trade> NewTradesEvent;

        /// <summary>
		/// connection to API established
        /// соединение с BitStamp API установлено
        /// </summary>
        public event Action Connected;

        /// <summary>
		/// connection to API lost
        /// соединение с BitStamp API разорвано
        /// </summary>
        public event Action Disconnected;

		// log messages
        // сообщения для лога

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
		/// send exeptions
        /// отправляет исключения
        /// </summary>
        public event Action<string, LogMessageType> LogMessageEvent;
    }
}
