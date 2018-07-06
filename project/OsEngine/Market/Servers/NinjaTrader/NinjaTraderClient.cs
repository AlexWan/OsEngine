/*
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
using OsEngine.Logging;
using OsEngine.Market.Servers.Entity;

namespace OsEngine.Market.Servers.NinjaTrader
{
    public class NinjaTraderClient
    {
        /// <summary>
        /// класс реализующий подключение к нинзе
        /// </summary>
        public NinjaTraderClient(string serverAddres, string port)
        {
            Thread worker = new Thread(WorkerPlace);
            worker.IsBackground = true;
            worker.Start();
        }

        /// <summary>
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
        /// подключиться 
        /// </summary>
        public void Connect()
        {
            _messagesToSend.Enqueue("Connect");
        }

        public bool IsConnected;

// запросы к Нинзе

        /// <summary>
        /// запросить список бумаг
        /// </summary>
        public void GetSecurities()
        {
            _messagesToSend.Enqueue("GetSecurities");
        }

        /// <summary>
        /// запросить список портфелей
        /// </summary>
        public void GetPortfolios()
        {
            _messagesToSend.Enqueue("GetAccaunts");
        }

        /// <summary>
        /// подписаться на стаканы и трейды
        /// </summary>
        public void SubscribleTradesAndDepths(Security securityName)
        {
            //string message = "Subscrible@" + securityName;
            //_messagesToSend.Enqueue(message);
        }

        /// <summary>
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
        /// отозвать ордер
        /// </summary>
        public void CanselOrder(Order order)
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

// поток отсылающий сообщения с запросами

        /// <summary>
        /// нужно ли чтобы потоки были остановлены
        /// </summary>
        private bool _threadsNeadToStop;

        /// <summary>
        /// сообщения для отправки в Нинзю
        /// </summary>
        private ConcurrentQueue<string> _messagesToSend = new ConcurrentQueue<string>();

        /// <summary>
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
                    { // запрос каких-либо входящих данных для нас, которые копятся в сервере
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
        /// послать сообщение в нинзю
        /// </summary>
        private string SendMessage(string message)
        {
            // Соединяемся с удаленным устройством

            // Устанавливаем удаленную точку для сокета
            IPHostEntry ipHost = Dns.GetHostEntry("localhost");
            IPAddress ipAddr = ipHost.AddressList[1];
            IPEndPoint ipEndPoint = new IPEndPoint(ipAddr, 11000);

            Socket sender = new Socket(ipAddr.AddressFamily, SocketType.Stream, ProtocolType.Tcp);

            // Соединяем сокет с удаленной точкой
            try
            {
                sender.Connect(ipEndPoint);
            }
            catch (Exception)
            {
                SendLogMessage("Ninja не отвечает. Вероятно у Вас не активен скрипт OsEngineConnect в Ninja",
                    LogMessageType.Error);
                Thread.Sleep(10000);
            }


            byte[] msg = Encoding.UTF8.GetBytes(message);

            // Отправляем данные через сокет
            sender.Send(msg);


            // Буфер для входящих данных
            byte[] bytes = new byte[1024];

            if (message == "GetSecurities")
            {
                bytes = new byte[1048576];
            }

            // Получаем ответ от сервера
            int bytesRec = sender.Receive(bytes);

            string request = Encoding.UTF8.GetString(bytes, 0, bytesRec);

            // Освобождаем сокет
            sender.Shutdown(SocketShutdown.Both);
            sender.Close();

            return request;
        }

// обработка ответов после наших запросов     

        /// <summary>
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
                SendLogMessage("Ошибка на стороне нинзи " + message, LogMessageType.Error);
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
        /// список стаканов которые мы получаем из нинзи
        /// </summary>
        private List<MarketDepth> _marketDepths = new List<MarketDepth>();

        /// <summary>
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

                SendLogMessage("Ордер № " + errorOrder.NumberUser + " не выставился. Ошибка: " + message, LogMessageType.Error);
            }
            if (str[0] == "Accept")
            {
                Order order = new Order();
                order.NumberUser = Convert.ToInt32(str[1]);
                order.NumberMarket = str[3];
                order.State = OrderStateType.Activ;
                order.TimeCallBack = Convert.ToDateTime(str[4]);

                if (MyOrderEvent != null)
                {
                    MyOrderEvent(order);
                }

            }


        }

        /// <summary>
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
                    level.Ask = Convert.ToDecimal(str[4]);
                }
                else
                {
                    level.Bid = Convert.ToDecimal(str[4]);
                }
                level.Price = Convert.ToDecimal(str[3]);

                MarketDepth myDepth = _marketDepths.Find(m => m.SecurityNameCode == str[0]);

                if (myDepth == null)
                {
                    myDepth = new MarketDepth();
                    myDepth.SecurityNameCode = str[0];
                    _marketDepths.Add(myDepth);
                }

                myDepth.Time = Convert.ToDateTime(str[5]);

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

                if (myDepth.Bids != null &&
                    myDepth.Bids.Count > 0 &&
                    myDepth.Asks != null &&
                    myDepth.Asks.Count > 0)

                {
                    if (str[1] == "Bid" &&
                        myDepth.Bids[0].Price >= myDepth.Asks[0].Price)
                    {
                        myDepth.Asks.RemoveAt(0);
                    }
                    if (str[1] == "Ask" &&
                    myDepth.Bids[0].Price >= myDepth.Asks[0].Price)
                    {
                        myDepth.Bids.RemoveAt(0);
                    }

                }

            if (UpdateMarketDepth != null)
                {
                    UpdateMarketDepth(myDepth.GetCopy());
                }
            }
        }

        private void AddBid(List<MarketDepthLevel> levels, MarketDepthLevel newLevel)
        { // уровни покупок.  с индексом ноль бОльшее значение 
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
        {// уровни продаж.  с индексом ноль меньшее значение 
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
        /// разбор входящих сообщений о портфелях
        /// </summary>
        private void LoadPortfolios(string message)
        {
           /* string portfolioStr = account.Name + "#"; // портфель

            portfolioStr += value + "$"; // текущее кол-во денег на счёте
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
                newPos.ValueCurrent= Convert.ToDecimal(trdStr[1]);

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
        /// разбор входящих сообщений о позициях
        /// </summary>
        private void LoadPositions(string message)
        {
            /*string positionstr = position.Instrument.FullName + "#"; // бумага
            positionstr += position.Account.Name + "#"; // название портфеля
            positionstr += position.Quantity + "$"; // текущий объём на рынке
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
                newPos.ValueCurrent = Convert.ToDecimal(trdStr[2]);

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
        /// разбор входящих сообщений о трейдах
        /// </summary>
        private void LoadMyTrades(string message)
        {
           /* string tradeStr = execution.Instrument.FullName + "#"; // имя инструмента
            tradeStr += execution.ExecutionId + "#"; // номер на бирже
            tradeStr += execution.OrderId + "#"; // номер ордера по которому прошла сделка
            tradeStr += execution.Price + "#"; // цена сведения
            tradeStr += execution.Quantity + "#"; // объём

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
                newMyTrade.Price = Convert.ToDecimal(trdStr[3]);
                newMyTrade.Volume= Convert.ToDecimal(trdStr[4]);
                Enum.TryParse(trdStr[5], out newMyTrade.Side);
                newMyTrade.Time= Convert.ToDateTime(trdStr[6]);

                if (MyTradeEvent != null)
                {
                    MyTradeEvent(newMyTrade);
                }
            }
        }

        /// <summary>
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
                newOrder.Volume = Convert.ToDecimal(ordStr[3]);
                newOrder.VolumeExecute = Convert.ToDecimal(ordStr[4]);
                newOrder.Price = Convert.ToDecimal(ordStr[5]);
                newOrder.PortfolioNumber = ordStr[6];
                Enum.TryParse(ordStr[7], out newOrder.Side);

                OrderStateType state;
                Enum.TryParse(ordStr[8], out state);
                newOrder.State = state;
                newOrder.TimeCallBack = Convert.ToDateTime(ordStr[9]);

                if (MyOrderEvent != null)
                {
                    MyOrderEvent(newOrder);
                }
            }
        }

        /// <summary>
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
                    newSecurity.PriceStep = Convert.ToDecimal(sec[3].Replace(",", CultureInfo.InvariantCulture.NumberFormat.NumberDecimalSeparator), CultureInfo.InvariantCulture);
                    newSecurity.PriceStepCost = Convert.ToDecimal(sec[4].Replace(",", CultureInfo.InvariantCulture.NumberFormat.NumberDecimalSeparator), CultureInfo.InvariantCulture);
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

            List<Portfolio> portfolios = new List<Portfolio>();

            string[] securityArray = message.Split('&');

            for (int i = 0; i < securityArray.Length - 1; i++)
            {
                Portfolio newPortfolio = new Portfolio();
                newPortfolio.Number = securityArray[i];
                portfolios.Add(newPortfolio);
            }

            if (UpdatePortfolio != null)
            {
                UpdatePortfolio(portfolios);
            }
        }

        /// <summary>
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
                    Convert.ToDecimal(
                        tradeInArray[1].Replace(",", CultureInfo.InvariantCulture.NumberFormat.NumberDecimalSeparator),
                        CultureInfo.InvariantCulture);
                newTrade.Volume =
                    Convert.ToDecimal(
                        tradeInArray[2].Replace(",", CultureInfo.InvariantCulture.NumberFormat.NumberDecimalSeparator),
                        CultureInfo.InvariantCulture);
                newTrade.Time = Convert.ToDateTime(tradeInArray[3]);

                if (NewTradesEvent != null)
                {
                    NewTradesEvent(newTrade);
                }
            }
        }

        // исходящие события

        /// <summary>
        /// новые мои ордера
        /// </summary>
        public event Action<Order> MyOrderEvent;

        /// <summary>
        /// новые мои сделки
        /// </summary>
        public event Action<MyTrade> MyTradeEvent;

        /// <summary>
        /// событие обновления портфеля
        /// </summary>
        public event Action<List<Portfolio>> UpdatePortfolio;

        /// <summary>
        /// новые бумаги в системе
        /// </summary>
        public event Action<List<Security>> UpdateSecuritiesEvent;

        /// <summary>
        /// обновился стакан
        /// </summary>
        public event Action<MarketDepth> UpdateMarketDepth;

        /// <summary>
        /// обновились тики
        /// </summary>
        public event Action<Trade> NewTradesEvent;

        /// <summary>
        /// соединение с BitStamp API установлено
        /// </summary>
        public event Action Connected;

        /// <summary>
        /// соединение с BitStamp API разорвано
        /// </summary>
        public event Action Disconnected;

        // сообщения для лога

        /// <summary>
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
        /// отправляет исключения
        /// </summary>
        public event Action<string, LogMessageType> LogMessageEvent;
    }
}
