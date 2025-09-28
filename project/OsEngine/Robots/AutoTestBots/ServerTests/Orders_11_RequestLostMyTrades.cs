using OsEngine.Market.Servers;
using System;
using System.Collections.Generic;
using System.Threading;
using OsEngine.Entity;
using OsEngine.Market;

namespace OsEngine.Robots.AutoTestBots.ServerTests
{
    public class Orders_11_RequestLostMyTrades : AServerTester
    {
        public string SecurityNameToTrade = "ETHUSDT";

        public string SecurityClassToTrade = "Futures";

        public decimal VolumeToTrade;

        public string PortfolioName;

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

            if (serverPermission.CanQueryOrderStatus == false)
            {
                SetNewServiceInfo("No permission. CanQueryOrderStatus == false. Test over");
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
            Server.NewMyTradeEvent += Server_NewMyTradeEvent;

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

            // 1 отключаем у коннектора приём ордеров на время
            // в таком случае Хаб Ордеров не увидит входящее событие о новом Активном ордере и о его исполнении

            Server.TestValue_CanSendMyTradesUp = false;

            // 2 выставляем ордер на покупку. Чтобы исполнился

            decimal price = Math.Round(md.Asks[0].Price.ToDecimal() + md.Asks[0].Price.ToDecimal() * 0.01m, mySecurity.Decimals);
            decimal volume = VolumeToTrade;
            Order newOrder = CreateOrder(mySecurity, price, volume, Side.Buy);
            _awaitOrderFirstStep = newOrder;
            Server.ExecuteOrder(newOrder);

            Thread.Sleep(3000);

            // 3 включаем приём ордеров у коннектора

            Server.TestValue_CanSendMyTradesUp = true;

            DateTime timeEndWait = DateTime.Now.AddMinutes(2);

            Order order = null;

            // 4 нужно дождаться когда будет Done order

            while (true)
            {
                if (timeEndWait < DateTime.Now)
                {
                    this.SetNewError("Error 8. No Done order from server BuyLimit");
                    TestEnded();
                    return;
                }

                if (_ordersDone.Count != 0)
                {
                    this.SetNewServiceInfo("BuyLimit Done order income Check!");
                    order = _ordersDone[0];
                    break;
                }
                else
                {
                    Thread.Sleep(1000);
                }
            }

            Thread.Sleep(1000);

            if (order == null)
            {
                TestEnded();
                return;
            }

            // 5 записываем активные ордера какие пришли после реконнекта

            for (int i = 0; i < _ordersActive.Count; i++)
            {
                SetNewServiceInfo("API send Done order. NumUser: " + _ordersActive[i].NumberUser +
                     " NumMarket: " + _ordersActive[i].NumberMarket +
                     " Security: " + _ordersActive[i].SecurityNameCode);
            }

            ClearOrders();

            Thread.Sleep(10000);

            // 6 проверяем пришёл ли MyTrade

            if (_myTradesToOrder.Count == 0)
            {
                this.SetNewError("Error 9. No MyTrade for Done order");
            }

            for (int i = 0; i < _myTradesToOrder.Count; i++)
            {
                SetNewServiceInfo("API send MyTrade. NumOrderParent: " + _myTradesToOrder[i].NumberOrderParent +
                      " Num trade: " + _myTradesToOrder[i].NumberTrade +
                      " Volume: " + _myTradesToOrder[i].Volume +
                      " Security: " + _myTradesToOrder[i].SecurityNameCode);
            }

            TestEnded();
        }

        MarketDepth _md;

        private void Server_NewMarketDepthEvent(MarketDepth md)
        {
            if (md.SecurityNameCode != SecurityNameToTrade)
            {
                return;
            }
            _md = md;
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

        private Order _awaitOrderFirstStep = null;

        private Order _awaitOrderSecondStep = null;

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
                _ordersActive.Add(order);
            }
            else if (order.State == OrderStateType.Cancel)
            {
                _ordersCancel.Add(order);
            }
            else if (order.State == OrderStateType.Done)
            {
                _ordersDone.Add(order);
            }
            else if (order.State == OrderStateType.Fail)
            {
                _ordersFail.Add(order);
            }
            else if (order.State == OrderStateType.Partial)
            {
                _ordersPartial.Add(order);
            }
            else if (order.State == OrderStateType.Pending)
            {
                _ordersPending.Add(order);
            }

            if (order.NumberUser != 0 &&
                _awaitOrderFirstStep.NumberUser == order.NumberUser)
            {
                _awaitOrderSecondStep = order;
            }
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
                this.SetNewError("Error 11. Order Type is note Limit. Real type: " + order.TypeOrder);
                return false;
            }


            if (order.TimeCallBack == DateTime.MinValue)
            {
                this.SetNewError("Error 12. TimeCallBack is MinValue");
                return false;
            }

            if (order.TimeDone == DateTime.MinValue &&
                order.State == OrderStateType.Done)
            {
                this.SetNewError("Error 13. Order Done, buy TimeDone is MinValue");
                return false;
            }

            if (order.TimeCancel == DateTime.MinValue &&
                order.State == OrderStateType.Cancel)
            {
                this.SetNewError("Error 14. Order Cancel, buy TimeCancel is MinValue");
                return false;
            }

            if (order.State != OrderStateType.Fail
                && string.IsNullOrEmpty(order.NumberMarket))
            {
                this.SetNewError("Error 15. NumberMarket is null or empty");
                return false;
            }

            if (string.IsNullOrEmpty(order.SecurityNameCode))
            {
                this.SetNewError("Error 16. SecurityNameCode is null or empty");
                return false;
            }

            if (string.IsNullOrEmpty(order.PortfolioNumber))
            {
                this.SetNewError("Error 17. PortfolioNumber is null or empty");
                return false;
            }

            if (order.Side == Side.None)
            {
                this.SetNewError("Error 18. Side is None");
                return false;
            }

            if (order.State != OrderStateType.Fail
                && order.TypeOrder != OrderPriceType.Market
                && order.Price <= 0)
            {
                this.SetNewError("Error 19. Price is zero");
                return false;
            }

            if (order.State != OrderStateType.Fail &&
                order.State != OrderStateType.Cancel &&
                order.Volume <= 0)
            {
                this.SetNewError("Error 20. Volume is zero");
                return false;
            }

            return true;
        }

        List<MyTrade> _myTradesToOrder = new List<MyTrade>();

        private void Server_NewMyTradeEvent(MyTrade myTrade)
        {
            if (_awaitOrderSecondStep == null)
            {
                return;
            }

            if (string.IsNullOrEmpty(_awaitOrderSecondStep.NumberMarket) == true)
            {
                return;
            }
            if (_awaitOrderSecondStep.NumberMarket == myTrade.NumberOrderParent)
            {
                _myTradesToOrder.Add(myTrade);
                MyTradeIsNormal(myTrade);
            }
        }

        private void MyTradeIsNormal(MyTrade myTrade)
        {

            /*
            12.2.1.Volume – объём исполненный по данному трейду
            12.2.2.Price – цена исполнения объёма
            12.2.3.NumberTrade – номер трейда. Обязательное поле
            12.2.4.NumberOrderParent – номер ордера по которому этот трейд прошёл
            12.2.5.NumberPosition – НЕ НУЖНО устанавливать.Это внутреннее поле для OsEngine
            12.2.6.SecurityNameCode – имя бумаги
            12.2.7.Time – время исполнения трейда
            12.2.8.MicroSeconds – НЕ ОБЯЗАТЕЛЬНОЕ поле.Используется только в HFT подключениях к MOEX
            12.2.9.Side – сторона ордера
            */

            if (myTrade.Volume <= 0)
            {
                this.SetNewError("Error 21. MyTrade. Volume is zero");
            }

            if (myTrade.Price <= 0)
            {
                this.SetNewError("Error 22. MyTrade. Price is zero");
            }

            if (string.IsNullOrEmpty(myTrade.SecurityNameCode))
            {
                this.SetNewError("Error 23. MyTrade. SecurityNameCode is null or empty");
            }

            if (string.IsNullOrEmpty(myTrade.NumberOrderParent))
            {
                this.SetNewError("Error 24. MyTrade. NumberOrderParent is null or empty");
            }

            if (string.IsNullOrEmpty(myTrade.NumberTrade))
            {
                this.SetNewError("Error 25. MyTrade. NumberTrade is null or empty");
            }

            if (myTrade.Time == DateTime.MinValue)
            {
                this.SetNewError("Error 26. MyTrade. Time is min value");
            }

            DateTime now = DateTime.Now;

            if (myTrade.Time.AddDays(-1) > now)
            {
                this.SetNewError("Error 27. MyTrade. Time is to big. Time: " + myTrade.Time.ToString());
            }

            if (myTrade.Time.AddDays(1) < now)
            {
                this.SetNewError("Error 28. MyTrade. Time is to small. Time: " + myTrade.Time.ToString());
            }
        }
    }
}