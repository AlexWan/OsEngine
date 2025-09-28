using OsEngine.Market.Servers;
using System;
using System.Collections.Generic;
using System.Threading;
using OsEngine.Entity;
using OsEngine.Market;

namespace OsEngine.Robots.AutoTestBots.ServerTests
{
    // Тест на запросы данных по ордерам в массивах. Исторические / Актуальные

    // 1 выставляем N ордеров на покупку ниже рынка
    // 2 делаем запросы по активным ордерам:
    // 2.1) Запрос на возврат 100 активных ордеров. 2.2) Запрос на возврат по K штук. K запросов. Чтобы последний был пустым

    // 3 отзываем все ордера
    // 4 делам запросы по историческим ордерам
    // 4.1) Запрос на возврат 100 исторических ордеров. 4.2) Запрос на возврат по K штук. K запросов. Чтобы последний был пустым

    // 5 делаем тоже самое с ордерами на продажу

    // 6 выставляем 1 ордер на покупку, чтобы он исполнился
    // 7 вызываем запрос по историческим ордерам на 100 штук. И на одну штуку.
    // 7 Первым должен быть наш ордер со статусом Done и правильным указанием объёма исполненного

    // 8 выставляем 1 ордер на продажу, чтобы он исполнился
    // 9 вызываем запрос по историческим ордерам на 100 штук. И на одну штуку.
    // 9 Первым должен быть наш ордер со статусом Done и правильным указанием объёма исполненного

    public class Orders_12_RequestOrdersList : AServerTester
    {
        public string SecurityNameToTrade = "ETHUSDT";

        public string SecurityClassToTrade = "Futures";

        public decimal VolumeToTrade;

        public string PortfolioName;

        public int OrdersCount = 20;

        private MarketDepth _md;

        private void Server_NewMarketDepthEvent(MarketDepth md)
        {
            if (md.SecurityNameCode != SecurityNameToTrade)
            {
                return;
            }
            _md = md;
        }

        public override void Process()
        {
            if (Server.ServerStatus != ServerConnectStatus.Connect)
            {
                this.SetNewError("Error 0. Server Status Disconnect");
                TestEnded();
                return;
            }

            IServerPermission serverPermission = ServerMaster.GetServerPermission(_myServer.ServerType);

            if (serverPermission == null)
            {
                SetNewError("Error 1. No server permission.");
                TestEnded();
                return;
            }

            if (serverPermission.CanGetOrderLists == false)
            {
                SetNewServiceInfo("No permission. CanGetOrderLists == false. Test over");
                TestEnded();
                return;
            }

            List<Security> securities = Server.Securities;

            if (securities == null &&
                securities.Count == 0)
            {
                SetNewError("Error 2. No securities found");
                TestEnded();
                return;
            }

            Security mySecurity = null;

            for (int i = 0; i < securities.Count; i++)
            {
                if (securities[i].Name == SecurityNameToTrade &&
                    securities[i].NameClass == SecurityClassToTrade)
                {
                    mySecurity = securities[i];
                    break;
                }
            }

            if (mySecurity == null)
            {
                SetNewError("Error 3. No securities found");
                TestEnded();
                return;
            }

            if (VolumeToTrade <= 0)
            {
                SetNewError("Error 4. Volume is zero");
                TestEnded();
                return;
            }

            Server.NewMarketDepthEvent += Server_NewMarketDepthEvent;
            Server.NewOrderIncomeEvent += Server_NewOrderIncomeEvent;

            Server.ServerRealization.Subscribe(mySecurity);

            DateTime timeStartWait = DateTime.Now.AddMinutes(1);

            while (_md == null)
            {
                if (timeStartWait < DateTime.Now)
                {
                    SetNewError("Error 5. No market depth after 1 minute");
                    TestEnded();
                    return;
                }
            }

            MarketDepth md = _md;

            if (md.Asks.Count == 0 ||
                md.Bids.Count == 0)
            {
                SetNewError("Error 6. No bid or ask in Market Depth");
                TestEnded();
                return;
            }

            if (md.Asks[0].Price == 0 ||
                md.Bids[0].Price == 0)
            {
                SetNewError("Error 7. Bid or Ask is zero price");
                TestEnded();
                return;
            }

            // ПРОВЕРКА 1. Активные ордера

            decimal priceToBuyOrdersNoExecution = Math.Round(md.Bids[0].Price.ToDecimal() - md.Bids[0].Price.ToDecimal() * 0.01m, mySecurity.Decimals); 
            decimal priceToSellOrdersNoExecution = Math.Round(md.Asks[0].Price.ToDecimal() + md.Asks[0].Price.ToDecimal() * 0.01m, mySecurity.Decimals);

            if (CheckActiveOrders(Side.Buy, priceToBuyOrdersNoExecution, VolumeToTrade, mySecurity, _awaitOrderFirstStepBuy) == false)
            {
                TestEnded();
                return;
            }
            else
            {
                 this.SetNewServiceInfo("Active orders request test OK. Side: " + Side.Buy.ToString());
            }

            if (CheckActiveOrders(Side.Sell, priceToSellOrdersNoExecution, VolumeToTrade, mySecurity, _awaitOrderFirstStepSell) == false)
            {
                TestEnded();
                return;
            }
            else
            {
                this.SetNewServiceInfo("Active orders request test OK. Side: " + Side.Sell.ToString());
            }

            // ПРОВЕРКА 2. Cancel ордера. Проверка доступности в истории тех ордеров что мы отозвали в предыдущей части

            if (CheckHistoricalCancelOrders(_awaitOrderFirstStepBuy, _awaitOrderFirstStepSell, OrderStateType.Cancel) == false)
            {
                TestEnded();
                return;
            }

            // ПРОВЕРКА 3. Done ордера. Проверка доступности в истории исполненных ордеров. Чтобы по ним были объёмы

            if (CheckDoneOrders(md, mySecurity) == false)
            {
                TestEnded();
                return;
            }

            TestEnded();
        }

        private bool CheckActiveOrders(Side side, decimal price, decimal volume, Security mySecurity, List<Order> ordersArray)
        {
            // 1 выставляем N ордеров на покупку ниже рынка
            // 2 делаем запросы по активным ордерам:
            // 2.1) Запрос на возврат 100 активных ордеров. 2.2) Запрос на возврат по 2ве штуки. 5ть запросов. Чтобы последний был пустым

            // 3 отзываем все ордера
            // 4 делам запросы по историческим ордерам
            // 4.1) Запрос на возврат 100 исторических ордеров. 4.2) Запрос на возврат по 2ве штуки. 5ть запросов.

            // 1) отправляем ордера в рынок

            ordersArray.Clear();
            ClearOrders();

            for (int i = 0;i < OrdersCount;i++)
            {
                Order newOrder = CreateOrder(mySecurity, price, volume, side);

                ordersArray.Add(newOrder);
                Server.ExecuteOrder(newOrder);
                Thread.Sleep(500);
            }

            DateTime timeEndWait = DateTime.Now.AddMinutes(2);

            // 2) ждём когда все придут

            while (true)
            {
                if (timeEndWait < DateTime.Now)
                {
                    this.SetNewError("Error 8. No Active orders from server");
                    return false;
                }

                if (_ordersActive.Count == ordersArray.Count)
                {
                    this.SetNewServiceInfo("Active orders income Check! Side: " + side.ToString());
                    break;
                }
                else
                {
                    Thread.Sleep(1000);
                }
            }

            Thread.Sleep(1000);


            // 2.1) Запрос на возврат 100 активных ордеров.

            List<Order> ordersFromRequest = Server.GetActiveOrders();

            if(ordersFromRequest == null)
            {
                this.SetNewError("Error 9. ordersFromRequest == null");
                return false;
            }
            if (ordersFromRequest.Count == 0)
            {
                this.SetNewError("Error 10. ordersFromRequest.Count == 0");
                return false;
            }

            if (ordersFromRequest.Count != ordersArray.Count)
            {
                this.SetNewError("Error 11. ordersFromRequest.Count != _awaitOrderFirstStep.Count");
                return false;
            }

            for(int i = 0;i < ordersFromRequest.Count;i++)
            {
                if(OrderIsNormal(ordersFromRequest[i])== false)
                {
                    this.SetNewError("Error 12. OrderIsNormal(ordersFromRequest[i])== false");
                    return false;
                }
            }

            // 2.2) Запрос на возврат по 2 штуки

            List<Order> ordersFromPartRequests = new List<Order>();

            for(int i = 0; i < ordersArray.Count + 2; i += 2)
            {
                List<Order> currentOrders = Server.GetActiveOrders(i, 2);

                if(currentOrders == null ||
                    currentOrders.Count == 0)
                {
                    break;
                }

                for(int j = 0; j < currentOrders.Count; j++)
                {
                    if (ordersFromPartRequests.Find( order => order.NumberMarket == currentOrders[j].NumberMarket) != null)
                    {
                        this.SetNewError("Error 12/1. duplicate orders");
                        return false;
                    }
                }

                ordersFromPartRequests.AddRange(currentOrders);
            }

            if (ordersFromPartRequests.Count == 0)
            {
                this.SetNewError("Error 13. ordersFromPartRequests.Count == 0");
                return false;
            }

            if (ordersFromPartRequests.Count != ordersArray.Count)
            {
                this.SetNewError("Error 14. ordersFromPartRequests.Count != _awaitOrderFirstStep.Count");
                return false;
            }

            for (int i = 0; i < ordersFromPartRequests.Count; i++)
            {
                if (OrderIsNormal(ordersFromPartRequests[i]) == false)
                {
                    this.SetNewError("Error 15. OrderIsNormal(ordersFromPartRequests[i])== false");
                    return false;
                }
            }

            // 3 отзываем всё

            for(int i = 0;i < ordersFromPartRequests.Count;i++)
            {
                Server.CancelOrder(ordersFromPartRequests[i]);
                ordersArray[i].State = OrderStateType.Cancel;
                Thread.Sleep(500);
            }

            Thread.Sleep(5000);

            return true;
        }

        private bool CheckHistoricalCancelOrders(List<Order> ordersArrayBuy, List<Order> orderArraySell, OrderStateType stateToWatch)
        {
            int countOfAllOrdersInTest = ordersArrayBuy.Count + orderArraySell.Count;

            // 2.1) Запрос на возврат 100 активных ордеров.

            List<Order> ordersFromRequest = Server.GetHistoricalOrders();

            if (ordersFromRequest == null)
            {
                this.SetNewError("Error 16. ordersFromRequest == null");
                return false;
            }
            if (ordersFromRequest.Count == 0)
            {
                this.SetNewError("Error 17. ordersFromRequest.Count == 0");
                return false;
            }

            ordersFromRequest = ordersFromRequest.GetRange(0, countOfAllOrdersInTest);

            if (ordersFromRequest.Count != countOfAllOrdersInTest)
            {
                this.SetNewError("Error 18. ordersFromRequest.Count != countOfAllOrdersInTest");
                return false;
            }

            for (int i = 0; i < ordersFromRequest.Count; i++)
            {
                if (OrderIsNormal(ordersFromRequest[i]) == false)
                {
                    this.SetNewError("Error 19. OrderIsNormal(ordersFromRequest[i])== false");
                    return false;
                }

                if (ordersFromRequest[i].State != stateToWatch)
                {
                    this.SetNewError("Error 20. ordersFromRequest[i].State != stateToWatch");
                    return false;
                }

                Order orderRequest = ordersFromRequest[i];

                Order orderSocket = null;

                for(int j = 0; j < orderArraySell.Count; j++)
                {
                    if (orderArraySell[j].NumberUser == orderRequest.NumberUser)
                    {
                        orderSocket = orderArraySell[j];
                        break;
                    }
                }

                if(orderSocket == null)
                {
                    for (int j = 0; j < ordersArrayBuy.Count; j++)
                    {
                        if (ordersArrayBuy[j].NumberUser == orderRequest.NumberUser)
                        {
                            orderSocket = ordersArrayBuy[j];
                            break;
                        }
                    }
                }

                if (orderSocket == null)
                {
                    this.SetNewError("Error 21. orderSocket == null. Cant find order in orders array from socket");
                    return false;
                }

                if(OrdersIsCompare(orderSocket,orderRequest) == false)
                {
                    this.SetNewError("Error 22. OrdersIsCompare(orderSocket,orderRequest) == false");
                    return false;
                }
            }

            // 2.2) Запрос на возврат по 1 штуке

            List<Order> ordersFromPartRequests = new List<Order>();

            for (int i = 0; i < countOfAllOrdersInTest; i += 1)
            {
                List<Order> currentOrders = Server.GetHistoricalOrders(i, 1);

                if (currentOrders == null ||
                    currentOrders.Count == 0)
                {
                    break;
                }

                ordersFromPartRequests.AddRange(currentOrders);
            }

            if (ordersFromPartRequests.Count == 0)
            {
                this.SetNewError("Error 23. ordersFromPartRequests.Count == 0");
                return false;
            }

            if (ordersFromPartRequests.Count != countOfAllOrdersInTest)
            {
                this.SetNewError("Error 24. ordersFromPartRequests.Count != _awaitOrderFirstStep.Count");
                return false;
            }

            for (int i = 0; i < ordersFromPartRequests.Count; i++)
            {
                if (OrderIsNormal(ordersFromPartRequests[i]) == false)
                {
                    this.SetNewError("Error 25. OrderIsNormal(ordersFromPartRequests[i])== false");
                    return false;
                }
            }

            for (int i = 0; i < ordersFromPartRequests.Count; i++)
            {
                if (OrderIsNormal(ordersFromPartRequests[i]) == false)
                {
                    this.SetNewError("Error 26. OrderIsNormal(ordersFromPartRequests[i])== false");
                    return false;
                }

                if (ordersFromPartRequests[i].State != stateToWatch)
                {
                    this.SetNewError("Error 27. ordersFromPartRequests[i].State != stateToWatch");
                    return false;
                }

                Order orderRequest = ordersFromPartRequests[i];

                Order orderSocket = null;

                for (int j = 0; j < orderArraySell.Count; j++)
                {
                    if (orderArraySell[j].NumberUser == orderRequest.NumberUser)
                    {
                        orderSocket = orderArraySell[j];
                        break;
                    }
                }

                if (orderSocket == null)
                {
                    for (int j = 0; j < ordersArrayBuy.Count; j++)
                    {
                        if (ordersArrayBuy[j].NumberUser == orderRequest.NumberUser)
                        {
                            orderSocket = ordersArrayBuy[j];
                            break;
                        }
                    }
                }

                if (orderSocket == null)
                {
                    this.SetNewError("Error 28. orderSocket == null. Cant find order in orders array from socket");
                    return false;
                }

                if (OrdersIsCompare(orderSocket, orderRequest) == false)
                {
                    this.SetNewError("Error 29. OrdersIsCompare(orderSocket,orderRequest) == false");
                    return false;
                }
            }

            return true;
        }

        private bool CheckDoneOrders(MarketDepth md, Security mySecurity)
        {
           
            ClearOrders();

            // 1 высылаем ордера на покупку

            List<Order> ordersArrayBuy = new List<Order>();
            decimal priceToBuyOrders = Math.Round(md.Asks[0].Price.ToDecimal() + md.Asks[0].Price.ToDecimal() * 0.01m, mySecurity.Decimals);

            for (int i = 0; i < OrdersCount; i++)
            {
                Order newOrder = CreateOrder(mySecurity, priceToBuyOrders, VolumeToTrade, Side.Buy);
                newOrder.State = OrderStateType.Done;
                ordersArrayBuy.Add(newOrder);
                Server.ExecuteOrder(newOrder);
                Thread.Sleep(500);
            }

            DateTime timeEndWait = DateTime.Now.AddMinutes(2);

            while (true)
            {
                if (timeEndWait < DateTime.Now)
                {
                    this.SetNewError("Error 30. No Done order from server");
                    return false;
                }

                if (_ordersDone.Count == ordersArrayBuy.Count)
                {
                    break;
                }
                else
                {
                    Thread.Sleep(1000);
                }
            }

            Thread.Sleep(1000);

            // 2 высылаем ордера на продажу

            List<Order> orderArraySell = new List<Order>();
            decimal priceToSellOrders = Math.Round(md.Bids[0].Price.ToDecimal() - md.Bids[0].Price.ToDecimal() * 0.01m, mySecurity.Decimals);

            for (int i = 0; i < OrdersCount; i++)
            {
                Order newOrder = CreateOrder(mySecurity, priceToSellOrders, VolumeToTrade, Side.Sell);
                newOrder.State = OrderStateType.Done;
                
                orderArraySell.Add(newOrder);
                Server.ExecuteOrder(newOrder);
                Thread.Sleep(500);
            }

            timeEndWait = DateTime.Now.AddMinutes(2);

            while (true)
            {
                if (timeEndWait < DateTime.Now)
                {
                    this.SetNewError("Error 31. No Done order from server");
                    return false;
                }

                if (_ordersDone.Count - ordersArrayBuy.Count == orderArraySell.Count)
                {
                    break;
                }
                else
                {
                    Thread.Sleep(1000);
                }
            }

            Thread.Sleep(1000);

            // 3 проверяем done ордера через запросы к истории

            if (CheckHistoricalCancelOrders(ordersArrayBuy, orderArraySell, OrderStateType.Done) == false)
            {
                return false;
            }

            return true;
        }

        private Order CreateOrder(Security sec, decimal price, decimal volume, Side side)
        {
            Order order = new Order();

            order.Price = price;
            order.Volume = volume;
            order.Side = side;
            order.NumberUser = NumberGen.GetNumberOrder(StartProgram.IsOsTrader);
            order.ServerType = Server.ServerType;
            order.TypeOrder = OrderPriceType.Limit;
            order.SecurityNameCode = sec.Name;
            order.SecurityClassCode = sec.NameClass;
            order.PortfolioNumber = PortfolioName;

            return order;
        }

        List<Order> _ordersActive = new List<Order>();
        List<Order> _ordersCancel = new List<Order>();
        List<Order> _ordersDone = new List<Order>();
        List<Order> _ordersFail = new List<Order>();
        List<Order> _ordersPartial = new List<Order>();
        List<Order> _ordersPending = new List<Order>();

        private void ClearOrders()
        {
            _ordersActive.Clear();
            _ordersCancel.Clear();
            _ordersDone.Clear();
            _ordersFail.Clear();
            _ordersPartial.Clear();
            _ordersPending.Clear();
        }

        private List<Order> _awaitOrderFirstStepBuy = new List<Order>();

        private List<Order> _awaitOrderFirstStepSell = new List<Order>();

        private List<Order> _awaitOrderSecondStep = new List<Order>(); 

        private void Server_NewOrderIncomeEvent(Order order)
        {
            if (order.State == OrderStateType.None)
            {
                this.SetNewError("Error 10. Order with state NONE");
                return;
            }

            if (OrderIsNormal(order) == false)
            {
                return;
            }

            if (order.State == OrderStateType.Active)
            {
                bool isInArray = false;

                for(int i = 0;i < _ordersActive.Count;i++)
                {
                    if (_ordersActive[i].NumberUser == order.NumberUser)
                    {
                        isInArray = true;
                        _ordersActive[i] = order;
                        break;
                    }
                }

                if (isInArray == false)
                {
                    _ordersActive.Add(order);
                }
            }
            else if (order.State == OrderStateType.Cancel)
            {
                bool isInArray = false;

                for (int i = 0; i < _ordersCancel.Count; i++)
                {
                    if (_ordersCancel[i].NumberUser == order.NumberUser)
                    {
                        isInArray = true;
                        _ordersCancel[i] = order;
                        break;
                    }
                }

                if (isInArray == false)
                {
                    _ordersCancel.Add(order);
                }
            }
            else if (order.State == OrderStateType.Done)
            {
                bool isInArray = false;

                for (int i = 0; i < _ordersDone.Count; i++)
                {
                    if (_ordersDone[i].NumberUser == order.NumberUser)
                    {
                        isInArray = true;
                        _ordersDone[i] = order;
                        break;
                    }
                }

                if (isInArray == false)
                {
                    _ordersDone.Add(order);
                }
            }
            else if (order.State == OrderStateType.Fail)
            {
                bool isInArray = false;

                for (int i = 0; i < _ordersFail.Count; i++)
                {
                    if (_ordersFail[i].NumberUser == order.NumberUser)
                    {
                        isInArray = true;
                        _ordersFail[i] = order;
                        break;
                    }
                }

                if (isInArray == false)
                {
                    _ordersFail.Add(order);
                }
            }
            else if (order.State == OrderStateType.Partial)
            {
                bool isInArray = false;

                for (int i = 0; i < _ordersPartial.Count; i++)
                {
                    if (_ordersPartial[i].NumberUser == order.NumberUser)
                    {
                        isInArray = true;
                        _ordersPartial[i] = order;
                        break;
                    }
                }

                if (isInArray == false)
                {
                    _ordersPartial.Add(order);
                }
            }
            else if (order.State == OrderStateType.Pending)
            {
                bool isInArray = false;

                for (int i = 0; i < _ordersPending.Count; i++)
                {
                    if (_ordersPending[i].NumberUser == order.NumberUser)
                    {
                        isInArray = true;
                        _ordersPending[i] = order;
                        break;
                    }
                }

                if (isInArray == false)
                {
                    _ordersPending.Add(order);
                }
            }
        }

        private bool OrdersIsCompare(Order orderFromSocket, Order orderFromRequest)
        {
            if(orderFromSocket.State != orderFromRequest.State)
            {
                this.SetNewError("Error 32. orderFromSocket.State != orderFromRequest.State");
                return false;
            }

            if (orderFromSocket.Side != orderFromRequest.Side)
            {
                this.SetNewError("Error 33. orderFromSocket.Side != orderFromRequest.Side");
                return false;
            }

            if (orderFromSocket.Price != orderFromRequest.Price)
            {
                this.SetNewError("Error 34. orderFromSocket.Price != orderFromRequest.Price");
                return false;
            }

            if (orderFromSocket.Volume != orderFromRequest.Volume)
            {
                this.SetNewError("Error 35. orderFromSocket.Volume != orderFromRequest.Volume");
                return false;
            }

            if (orderFromSocket.TypeOrder != orderFromRequest.TypeOrder)
            {
                this.SetNewError("Error 36. orderFromSocket.TypeOrder != orderFromRequest.TypeOrder");
                return false;
            }

            if (orderFromSocket.ServerType != orderFromRequest.ServerType)
            {
                this.SetNewError("Error 37. orderFromSocket.ServerType != orderFromRequest.ServerType");
                return false;
            }

            if (orderFromSocket.SecurityNameCode != orderFromRequest.SecurityNameCode)
            {
                this.SetNewError("Error 38. orderFromSocket.ServerType != orderFromRequest.ServerType");
                return false;
            }

            return true;
        }

        private bool OrderIsNormal(Order order)
        {
            /*
            1.NumberUser – нужно указывать чтобы OsEngine распознал данный ордер как свой.
            2.NumberMarket – номер ордера на бирже
            3.SecurityNameCode – название бумаги
            4.SecurityClassCode – название класса бумаги
            5.PortfolioNumber – название портфеля
            6.Side – сторона ордера
            7.Price – цена ордера
            8.Volume – объём ордера
            9.State – статус ордера
            10.TimeCallBack, TimeCreate – забиваем при всех откликах от сервера по ордеру
            11.TimeDone – время сервера когда ордер получил статус Done
            12.TimeCancel – время сервера когда ордер получил статус Cancel
            */

            if (order.TypeOrder != OrderPriceType.Limit)
            {
                this.SetNewError("Error 39. Order Type is note Limit. Real type: " + order.TypeOrder);
                return false;
            }


            if (order.TimeCallBack == DateTime.MinValue)
            {
                this.SetNewError("Error 40. TimeCallBack is MinValue");
                return false;
            }

            if (order.TimeDone == DateTime.MinValue &&
                order.State == OrderStateType.Done)
            {
                this.SetNewError("Error 41. Order Done, buy TimeDone is MinValue");
                return false;
            }

            if (order.TimeCancel == DateTime.MinValue &&
                order.State == OrderStateType.Cancel)
            {
                this.SetNewError("Error 42. Order Cancel, buy TimeCancel is MinValue");
                return false;
            }

            if (order.State != OrderStateType.Fail
                && string.IsNullOrEmpty(order.NumberMarket))
            {
                this.SetNewError("Error 43. NumberMarket is null or empty");
                return false;
            }

            if (string.IsNullOrEmpty(order.SecurityNameCode))
            {
                this.SetNewError("Error 44. SecurityNameCode is null or empty");
                return false;
            }

            if (string.IsNullOrEmpty(order.PortfolioNumber))
            {
                this.SetNewError("Error 45. PortfolioNumber is null or empty");
                return false;
            }

            if (order.Side == Side.None)
            {
                this.SetNewError("Error 46. Side is None");
                return false;
            }

            if (order.State != OrderStateType.Fail
                && order.TypeOrder != OrderPriceType.Market
                && order.Price <= 0)
            {
                this.SetNewError("Error 47. Price is zero");
                return false;
            }

            if (order.State != OrderStateType.Fail &&
                order.State != OrderStateType.Cancel &&
                order.Volume <= 0)
            {
                this.SetNewError("Error 48. Volume is zero");
                return false;
            }

            return true;
        }
    }
}