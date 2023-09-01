using OsEngine.Market.Servers;
using System;
using System.Collections.Generic;
using System.Threading;
using OsEngine.Entity;

namespace OsEngine.Robots.AutoTestBots.ServerTests
{
    public class Orders_3_MarketOrders : AServerTester
    {
        public string SecurityToTrade = "ETHUSDT";

        public decimal VolumeToTrade;

        public string PortfolioName;

        public override void Process()
        {
            if (Server.ServerStatus != ServerConnectStatus.Connect)
            {
                this.SetNewError("Error 1. Server Status Disconnect");
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
                if (securities[i].Name == SecurityToTrade)
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

            Server.ServerRealization.Subscrible(mySecurity);

            DateTime timeStartWait = DateTime.Now.AddMinutes(2);

            while (_md == null)
            {
                if (timeStartWait < DateTime.Now)
                {
                    SetNewError("Error 5. No market depth after 2 minutes");
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

            // ордер на покупку

            decimal price = Math.Round((md.Asks[0].Price + md.Bids[0].Price) / 2, mySecurity.Decimals);

            SendBuyOrder(mySecurity, price);

            if (this._errors != null &&
                this._errors.Count > 0)
            {
                TestEnded();
                return;
            }

            // ордер на продажу

            price = Math.Round((md.Asks[0].Price + md.Bids[0].Price) / 2, mySecurity.Decimals);

            SendSellOrder(mySecurity, price);

            TestEnded();
        }

        MarketDepth _md;

        private void Server_NewMarketDepthEvent(MarketDepth md)
        {
            _md = md;
        }

        Side _whaitSide;

        private void SendBuyOrder(Security mySec, decimal price)
        {
            decimal volume = VolumeToTrade;

            price = Math.Round(price + price * 0.05m, mySec.Decimals);

            Order newOrder = CreateOrder(mySec, price, volume, Side.Buy);
            _whaitSide = Side.Buy;

            Server.ExecuteOrder(newOrder);

            DateTime timeEndWhait = DateTime.Now.AddMinutes(2);

            // нужно дождаться когда будет Active order
            while (true)
            {
                if (timeEndWhait < DateTime.Now)
                {
                    this.SetNewError("Error 8. No Active order from server BuyMarket");
                    return;
                }

                if (_ordersFail.Count != 0)
                {
                    this.SetNewError("Error 9. Order FAIL found from server BuyMarket");
                    return;
                }

                if (_ordersActive.Count != 0)
                {
                    this.SetNewServiceInfo("BuyMarket Active order income Check!");
                    break;
                }
                else
                {
                    Thread.Sleep(1000);
                }
            }

            timeEndWhait = DateTime.Now.AddMinutes(2);

            // нужно дождаться когда будет Done order
            while (true)
            {
                if (timeEndWhait < DateTime.Now)
                {
                    this.SetNewError("Error 10. No Done order from server BuyMarket");
                    return;
                }

                if (_ordersFail.Count != 0)
                {
                    this.SetNewError("Error 11. Order FAIL found from server BuyMarket");
                    return;
                }

                if (_ordersDone.Count != 0)
                {
                    this.SetNewServiceInfo("BuyMarket Done order income Check!");
                    break;
                }
                else
                {
                    Thread.Sleep(1000);
                }
            }

            timeEndWhait = DateTime.Now.AddMinutes(2);

            // нужно дождаться когда будет придёт MyTrade
            while (true)
            {
                if (timeEndWhait < DateTime.Now)
                {
                    this.SetNewError("Error 12. No MyTrade BuyMarket");
                    return;
                }

                if (_myTrades.Count != 0)
                {
                    this.SetNewServiceInfo("BuyMarket Done myTrade income Check!");
                    break;
                }
                else
                {
                    Thread.Sleep(1000);
                }
            }

            ClearOrders();
        }

        private void SendSellOrder(Security mySec, decimal price)
        {
            decimal volume = VolumeToTrade;

            price = Math.Round(price - price * 0.05m, mySec.Decimals);

            Order newOrder = CreateOrder(mySec, price, volume, Side.Sell);
            _whaitSide = Side.Sell;

            Server.ExecuteOrder(newOrder);

            DateTime timeEndWhait = DateTime.Now.AddMinutes(2);

            // нужно дождаться когда будет Active order
            while (true)
            {
                if (timeEndWhait < DateTime.Now)
                {
                    this.SetNewError("Error 13. No reject order from server SellMarket");
                    return;
                }

                if (_ordersFail.Count != 0)
                {
                    this.SetNewError("Error 14. Order FAIL found from server SellMarket");
                    return;
                }

                if (_ordersActive.Count != 0)
                {
                    this.SetNewServiceInfo("SellMarket Active order income Check!");
                    break;
                }
                else
                {
                    Thread.Sleep(1000);
                }
            }

            timeEndWhait = DateTime.Now.AddMinutes(2);

            // нужно дождаться когда будет Done order
            while (true)
            {
                if (timeEndWhait < DateTime.Now)
                {
                    this.SetNewError("Error 15. No reject order from server SellMarket");
                    return;
                }

                if (_ordersFail.Count != 0)
                {
                    this.SetNewError("Error 16. Order FAIL found from server SellMarket");
                    return;
                }

                if (_ordersActive.Count != 0)
                {
                    this.SetNewServiceInfo("SellMarket Done order income Check!");
                    break;
                }
                else
                {
                    Thread.Sleep(1000);
                }
            }

            // нужно дождаться когда будет придёт MyTrade

            timeEndWhait = DateTime.Now.AddMinutes(2);

            while (true)
            {
                if (timeEndWhait < DateTime.Now)
                {
                    this.SetNewError("Error 17. No MyTrade SellMarket");
                    return;
                }

                if (_myTrades.Count != 0)
                {
                    this.SetNewServiceInfo("SellMarket Done myTrade income Check!");
                    break;
                }
                else
                {
                    Thread.Sleep(1000);
                }
            }

            ClearOrders();
        }

        private Order CreateOrder(Security sec, decimal price, decimal volume, Side side)
        {
            Order order = new Order();

            order.Price = price;
            order.Volume = volume;
            order.Side = side;
            order.NumberUser = NumberGen.GetNumberOrder(StartProgram.IsOsTrader);
            order.ServerType = Server.ServerType;
            order.TypeOrder = OrderPriceType.Market;
            order.SecurityNameCode = sec.Name;
            order.SecurityClassCode = sec.NameClass;
            order.PortfolioNumber = PortfolioName;

            return order;
        }

        List<Order> _ordersActive = new List<Order>();
        List<Order> _ordersCancel = new List<Order>();
        List<Order> _ordersDone = new List<Order>();
        List<Order> _ordersFail = new List<Order>();
        List<Order> _ordersPatrial = new List<Order>();
        List<Order> _ordersPending = new List<Order>();

        private void ClearOrders()
        {
            _ordersActive.Clear();
            _ordersCancel.Clear();
            _ordersDone.Clear();
            _ordersFail.Clear();
            _ordersPatrial.Clear();
            _ordersPending.Clear();
            _myTrades.Clear();
        }

        private void Server_NewOrderIncomeEvent(Order order)
        {
            if (order.State == OrderStateType.None)
            {
                this.SetNewError("Error 18. Order whith state NONE");
                return;
            }

            if (OrderIsNormal(order) == false)
            {
                return;
            }

            if (order.State == OrderStateType.Activ)
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
            else if (order.State == OrderStateType.Patrial)
            {
                _ordersPatrial.Add(order);
            }
            else if (order.State == OrderStateType.Pending)
            {
                _ordersPending.Add(order);
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

            if (order.Side != _whaitSide)
            {
                this.SetNewError("Error 19. Whait side note equal. Whait: " + _whaitSide
                    + " Side in order: " + order.Side);
                return false;
            }

            if (order.TypeOrder != OrderPriceType.Market)
            {
                this.SetNewError("Error 20. Order Type is note Market. Real type: " + order.TypeOrder);
                return false;
            }


            if (order.TimeCallBack == DateTime.MinValue)
            {
                this.SetNewError("Error 21. TimeCallBack is MinValue");
                return false;
            }

            if (order.TimeDone == DateTime.MinValue &&
                order.State == OrderStateType.Done)
            {
                this.SetNewError("Error 22. Order Done, buy TimeDone is MinValue");
                return false;
            }

            if (order.TimeCancel == DateTime.MinValue &&
                order.State == OrderStateType.Cancel)
            {
                this.SetNewError("Error 23. Order Cancel, buy TimeCancel is MinValue");
                return false;
            }

            if (order.NumberUser == 0)
            {
                this.SetNewError("Error 24. NumberUser is zero");
                return false;
            }

            if (order.State != OrderStateType.Fail
                && string.IsNullOrEmpty(order.NumberMarket))
            {
                this.SetNewError("Error 25. NumberMarket is null or empty");
                return false;
            }

            if (string.IsNullOrEmpty(order.SecurityNameCode))
            {
                this.SetNewError("Error 26. SecurityNameCode is null or empty");
                return false;
            }

            if (string.IsNullOrEmpty(order.PortfolioNumber))
            {
                this.SetNewError("Error 27. PortfolioNumber is null or empty");
                return false;
            }

            if (order.Side == Side.None)
            {
                this.SetNewError("Error 28. Side is None");
                return false;
            }

            if (order.State != OrderStateType.Fail
                && order.TypeOrder != OrderPriceType.Market
                && order.Price <= 0)
            {
                this.SetNewError("Error 29. Price is zero");
                return false;
            }

            if (order.State != OrderStateType.Fail &&
                order.State != OrderStateType.Cancel &&
                order.Volume <= 0)
            {
                this.SetNewError("Error 30. Volume is zero");
                return false;
            }

            return true;
        }

        List<MyTrade> _myTrades = new List<MyTrade>();

        private void Server_NewMyTradeEvent(MyTrade myTrade)
        {
            if (MyTradeIsNormal(myTrade))
            {
                _myTrades.Add(myTrade);
            }
        }

        private bool MyTradeIsNormal(MyTrade myTrade)
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

            if (myTrade.Side != _whaitSide)
            {
                this.SetNewError("Error 31. MyTrade. Whait side note equal. Whait: " + _whaitSide
                  + " Side in order: " + myTrade.Side);
                return false;
            }

            if (myTrade.Volume <= 0)
            {
                this.SetNewError("Error 32. MyTrade. Volume is zero");
                return false;
            }

            if (myTrade.Price <= 0)
            {
                this.SetNewError("Error 33. MyTrade. Price is zero");
                return false;
            }

            if (string.IsNullOrEmpty(myTrade.SecurityNameCode))
            {
                this.SetNewError("Error 34. MyTrade. SecurityNameCode is null or empty");
                return false;
            }

            if (string.IsNullOrEmpty(myTrade.NumberOrderParent))
            {
                this.SetNewError("Error 35. MyTrade. NumberOrderParent is null or empty");
                return false;
            }

            if (string.IsNullOrEmpty(myTrade.NumberTrade))
            {
                this.SetNewError("Error 36. MyTrade. NumberTrade is null or empty");
                return false;
            }

            if (myTrade.Time == DateTime.MinValue)
            {
                this.SetNewError("Error 37. MyTrade. Time is min value");
                return false;
            }

            return true;
        }
    }
}