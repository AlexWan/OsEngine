﻿using OsEngine.Market.Servers;
using System;
using System.Collections.Generic;
using System.Threading;
using OsEngine.Entity;
using OsEngine.Market;

namespace OsEngine.Robots.AutoTestBots.ServerTests
{
    public class Orders_8_RequestOnReconnect : AServerTester
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

            if (serverPermission.CanQueryOrdersAfterReconnect == false)
            {
                SetNewServiceInfo("No permission. CanQueryOrdersAfterReconnect == false. Test over");
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

            // 1 выставляем ордер на покупку. Далеко от края стакана

            decimal price = Math.Round((md.Asks[0].Price + md.Bids[0].Price) / 2, mySecurity.Decimals);

            Order order = SendBuyOrder(mySecurity, price);

            if(order == null)
            {
                TestEnded();
                return;
            }

            // 2 делаем реконнект

            Server.StopServer();
            Thread.Sleep(10000);

            ClearOrders(); // очищаем все массивы с входящими статусами ордеров. Пусто

            Server.StartServer();

            DateTime startAwait = DateTime.Now;

            while(Server.ServerStatus != ServerConnectStatus.Connect)
            {
                Thread.Sleep(1000);

                if(startAwait.AddMinutes(5) < DateTime.Now)
                {
                    SetNewError("Error 8. Server status is not change in 5 minutes");
                    TestEnded();
                    return;
                }
            }

            // 3 дожидаемся прихода активного ордера от сервера
            // ждём 20 секунд. Больше смысла нет

            Thread.Sleep(20000);

            if(_ordersActive.Count == 0)
            {
                SetNewError("Error 9. No active order after 20 seconds");
                TestEnded();
                return;
            }

            // 4 записываем активные ордера какие пришли после реконнекта

            for(int i = 0;i < _ordersActive.Count;i++)
            {
                SetNewServiceInfo("API send Activ order. NumUser: " + _ordersActive[i].NumberUser + 
                     "NumMarket: "  + _ordersActive[i].NumberMarket + 
                     "Security: " + _ordersActive[i].SecurityNameCode);
            }
           
            // 5 отзываем ордер

            CancelOrder(order);

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

        Side _whaitSide;

        private Order SendBuyOrder(Security mySec, decimal price)
        {
            decimal volume = VolumeToTrade;

            price = Math.Round(price - price * 0.01m, mySec.Decimals); // проскальзывание 1%

            Order newOrder = CreateOrder(mySec, price, volume, Side.Buy);
            _whaitSide = Side.Buy;

            Server.ExecuteOrder(newOrder);

            DateTime timeEndWhait = DateTime.Now.AddMinutes(2);

            Order order = null;

            // нужно дождаться когда будет Active order
            while (true)
            {
                if (timeEndWhait < DateTime.Now)
                {
                    this.SetNewError("Error 10. No Active order from server BuyLimit");
                    return null;
                }

                if (_ordersActive.Count != 0)
                {
                    this.SetNewServiceInfo("BuyLimit Active order income Check!");
                    order = _ordersActive[0];
                    break;
                }
                else
                {
                    Thread.Sleep(1000);
                }
            }

            ClearOrders();

            return order;
        }

        private void CancelOrder(Order order)
        {
            Server.CancelOrder(order);

            // нужно дождаться когда будет Active order

            DateTime timeEndWhait = DateTime.Now.AddMinutes(2);

            while (true)
            {
                if (timeEndWhait < DateTime.Now)
                {
                    this.SetNewError("Error 11. No reject order from server CancelOrder");
                    return;
                }

                if (_ordersCancel.Count != 0)
                {
                    this.SetNewServiceInfo("Cancel order income Check!");
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
        }

        private void Server_NewOrderIncomeEvent(Order order)
        {
            if (order.State == OrderStateType.None)
            {
                this.SetNewError("Error 12. Order whith state NONE");
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

            if (order.TypeOrder != OrderPriceType.Limit)
            {
                this.SetNewError("Error 13. Order Type is note Liimt. Real type: " + order.TypeOrder);
                return false;
            }


            if (order.TimeCallBack == DateTime.MinValue)
            {
                this.SetNewError("Error 14. TimeCallBack is MinValue");
                return false;
            }

            if (order.TimeDone == DateTime.MinValue &&
                order.State == OrderStateType.Done)
            {
                this.SetNewError("Error 15. Order Done, buy TimeDone is MinValue");
                return false;
            }

            if (order.TimeCancel == DateTime.MinValue &&
                order.State == OrderStateType.Cancel)
            {
                this.SetNewError("Error 16. Order Cancel, buy TimeCancel is MinValue");
                return false;
            }

            if (order.State != OrderStateType.Fail
                && string.IsNullOrEmpty(order.NumberMarket))
            {
                this.SetNewError("Error 17. NumberMarket is null or empty");
                return false;
            }

            if (string.IsNullOrEmpty(order.SecurityNameCode))
            {
                this.SetNewError("Error 18. SecurityNameCode is null or empty");
                return false;
            }

            if (string.IsNullOrEmpty(order.PortfolioNumber))
            {
                this.SetNewError("Error 19. PortfolioNumber is null or empty");
                return false;
            }

            if (order.Side == Side.None)
            {
                this.SetNewError("Error 20. Side is None");
                return false;
            }

            if (order.State != OrderStateType.Fail
                && order.TypeOrder != OrderPriceType.Market
                && order.Price <= 0)
            {
                this.SetNewError("Error 21. Price is zero");
                return false;
            }

            if (order.State != OrderStateType.Fail &&
                order.State != OrderStateType.Cancel &&
                order.Volume <= 0)
            {
                this.SetNewError("Error 22. Volume is zero");
                return false;
            }

            return true;
        }
    }
}