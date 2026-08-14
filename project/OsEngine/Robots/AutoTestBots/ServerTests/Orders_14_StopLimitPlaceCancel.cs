/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using OsEngine.Market;
using OsEngine.Market.Servers;
using System;
using System.Collections.Generic;
using System.Threading;
using OsEngine.Entity;

namespace OsEngine.Robots.AutoTestBots.ServerTests
{
    public class Orders_14_StopLimitPlaceCancel : AServerTester
    {
        public string SecurityNameToTrade = "ETHUSDT";

        public string SecurityClassToTrade = "Futures";

        public decimal VolumeToTrade;

        public string PortfolioName;

        public int CountOrders;

        public override void Process()
        {
            if (Server.ServerStatus != ServerConnectStatus.Connect)
            {
                this.SetNewError("Error 0. Server Status Disconnect");
                TestEnded();
                return;
            }

            if (CountOrders < 20)
            {
                this.SetNewError("Error 1. Iteration Count < 20.");
                TestEnded();
                return;
            }

            List<Security> securities = Server.Securities;

            if (securities == null ||
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

            IServerPermission permission = ServerMaster.GetServerPermission(Server.ServerType);

            if (permission == null)
            {
                SetNewError("Error 5. No server permission");
                TestEnded();
                return;
            }

            if (permission.StopOrdersIsSupport == false)
            {
                // коннектор не поддерживает серверные стопы. Тест пропускается
                SetNewServiceInfo("Server " + Server.ServerType
                    + " does not support server stop orders (StopOrdersIsSupport == false). Test SKIPPED");
                TestEnded();
                return;
            }

            // после подключения сервера нельзя сразу слать ордера:
            // AServer.ExecuteOrder отклонит их Fail'ом в первые WaitTimeSecondsAfterFirstStartToSendOrders секунд

            int waitAfterConnect = permission.WaitTimeSecondsAfterFirstStartToSendOrders;

            if (waitAfterConnect > 0)
            {
                SetNewServiceInfo("Waiting " + (waitAfterConnect + 5) + " sec after server connect before sending orders");
                Thread.Sleep((waitAfterConnect + 5) * 1000);
            }

            Server.NewMarketDepthEvent += Server_NewMarketDepthEvent;
            Server.NewOrderIncomeEvent += Server_NewOrderIncomeEvent;

            Server.ServerRealization.Subscribe(mySecurity);

            DateTime timeStartWait = DateTime.Now.AddMinutes(2);

            while (_md == null)
            {
                if (timeStartWait < DateTime.Now)
                {
                    SetNewError("Error 6. No market depth after 2 minutes");
                    TestEnded();
                    return;
                }

                Thread.Sleep(1000);
            }

            MarketDepth md = _md;

            if (md.Asks.Count == 0 ||
                md.Bids.Count == 0)
            {
                SetNewError("Error 7. No bid or ask in Market Depth");
                TestEnded();
                return;
            }

            if (md.Asks[0].Price == 0 ||
                md.Bids[0].Price == 0)
            {
                SetNewError("Error 8. Bid or Ask is zero price");
                TestEnded();
                return;
            }

            decimal midPrice = Math.Round((md.Asks[0].Price.ToDecimal() + md.Bids[0].Price.ToDecimal()) / 2, mySecurity.Decimals);

            // стоп-ордера на покупку. Активация выше рынка

            for (int i = 0; i < CountOrders; i++)
            {
                Order order = SendBuyStopOrder(mySecurity, midPrice);

                if (order != null)
                {
                    CancelOrder(order);
                }
                else
                {
                    TestEnded();
                    return;
                }
            }

            Thread.Sleep(10000);

            // стоп-ордера на продажу. Активация ниже рынка

            for (int i = 0; i < CountOrders; i++)
            {
                Order order = SendSellStopOrder(mySecurity, midPrice);

                if (order != null)
                {
                    CancelOrder(order);
                }
                else
                {
                    TestEnded();
                    return;
                }
            }

            Thread.Sleep(10000);

            // стоп-маркет на покупку. Выставление и отмена

            Order buyMarketStop = SendBuyStopMarketOrder(mySecurity, midPrice);

            if (buyMarketStop != null)
            {
                CancelOrder(buyMarketStop);
            }
            else
            {
                TestEnded();
                return;
            }

            Thread.Sleep(10000);

            // стоп-маркет на продажу. Выставление и отмена

            Order sellMarketStop = SendSellStopMarketOrder(mySecurity, midPrice);

            if (sellMarketStop != null)
            {
                CancelOrder(sellMarketStop);
            }
            else
            {
                TestEnded();
                return;
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

        Side _WaitSide;

        OrderPriceType _waitType = OrderPriceType.StopLimit;

        private Order SendBuyStopOrder(Security mySec, decimal midPrice)
        {
            decimal priceActivate = Math.Round(midPrice + midPrice * 0.02m, mySec.Decimals); // активация на 2% выше рынка
            decimal priceOrder = Math.Round(priceActivate + priceActivate * 0.005m, mySec.Decimals);

            Order newOrder = CreateStopOrder(mySec, priceOrder, priceActivate, VolumeToTrade, Side.Buy);
            _WaitSide = Side.Buy;
            _waitType = OrderPriceType.StopLimit;

            Server.ExecuteOrder(newOrder);

            DateTime timeEndWait = DateTime.Now.AddMinutes(2);

            Order order = null;

            // нужно дождаться когда будет Active order
            while (true)
            {
                if (timeEndWait < DateTime.Now)
                {
                    this.SetNewError("Error 9. No Active order from server BuyStopLimit");
                    return null;
                }

                if (_ordersActive.Count != 0)
                {
                    this.SetNewServiceInfo("BuyStopLimit Active order income Check!");
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

        private Order SendSellStopOrder(Security mySec, decimal midPrice)
        {
            decimal priceActivate = Math.Round(midPrice - midPrice * 0.02m, mySec.Decimals); // активация на 2% ниже рынка
            decimal priceOrder = Math.Round(priceActivate - priceActivate * 0.005m, mySec.Decimals);

            Order newOrder = CreateStopOrder(mySec, priceOrder, priceActivate, VolumeToTrade, Side.Sell);
            _WaitSide = Side.Sell;
            _waitType = OrderPriceType.StopLimit;

            Server.ExecuteOrder(newOrder);

            DateTime timeEndWait = DateTime.Now.AddMinutes(2);

            Order order = null;

            // нужно дождаться когда будет Active order
            while (true)
            {
                if (timeEndWait < DateTime.Now)
                {
                    this.SetNewError("Error 10. No Active order from server SellStopLimit");
                    return null;
                }

                if (_ordersActive.Count != 0)
                {
                    this.SetNewServiceInfo("SellStopLimit Active order income Check!");
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

        private Order SendBuyStopMarketOrder(Security mySec, decimal midPrice)
        {
            decimal priceActivate = Math.Round(midPrice + midPrice * 0.02m, mySec.Decimals); // активация на 2% выше рынка

            Order newOrder = CreateStopOrder(mySec, priceActivate, priceActivate, VolumeToTrade, Side.Buy);
            newOrder.TypeOrder = OrderPriceType.StopMarket;
            _WaitSide = Side.Buy;
            _waitType = OrderPriceType.StopMarket;

            Server.ExecuteOrder(newOrder);

            DateTime timeEndWait = DateTime.Now.AddMinutes(2);

            Order order = null;

            while (true)
            {
                if (timeEndWait < DateTime.Now)
                {
                    this.SetNewError("Error 26. No Active order from server BuyStopMarket");
                    return null;
                }

                if (_ordersActive.Count != 0)
                {
                    this.SetNewServiceInfo("BuyStopMarket Active order income Check!");
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

        private Order SendSellStopMarketOrder(Security mySec, decimal midPrice)
        {
            decimal priceActivate = Math.Round(midPrice - midPrice * 0.02m, mySec.Decimals); // активация на 2% ниже рынка

            Order newOrder = CreateStopOrder(mySec, priceActivate, priceActivate, VolumeToTrade, Side.Sell);
            newOrder.TypeOrder = OrderPriceType.StopMarket;
            _WaitSide = Side.Sell;
            _waitType = OrderPriceType.StopMarket;

            Server.ExecuteOrder(newOrder);

            DateTime timeEndWait = DateTime.Now.AddMinutes(2);

            Order order = null;

            while (true)
            {
                if (timeEndWait < DateTime.Now)
                {
                    this.SetNewError("Error 27. No Active order from server SellStopMarket");
                    return null;
                }

                if (_ordersActive.Count != 0)
                {
                    this.SetNewServiceInfo("SellStopMarket Active order income Check!");
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

            // нужно дождаться когда ордер будет отменён

            DateTime timeEndWait = DateTime.Now.AddMinutes(2);

            while (true)
            {
                if (timeEndWait < DateTime.Now)
                {
                    this.SetNewError("Error 11. No canceled order from server CancelOrder");
                    return;
                }

                if (_ordersCancel.Count != 0)
                {
                    this.SetNewServiceInfo("Canceled stop order incoming: Check!");
                    break;
                }
                else
                {
                    Thread.Sleep(1000);
                }
            }

            ClearOrders();
        }

        private Order CreateStopOrder(Security sec, decimal price, decimal priceCondition, decimal volume, Side side)
        {
            Order order = new Order();

            order.Price = price;
            order.StopPrice = priceCondition;
            order.Volume = volume;
            order.Side = side;
            order.NumberUser = NumberGen.GetNumberOrder(StartProgram.IsOsTrader);
            order.ServerType = Server.ServerType;
            order.TypeOrder = OrderPriceType.StopLimit;
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

        private void Server_NewOrderIncomeEvent(Order order)
        {
            if (order.State == OrderStateType.None)
            {
                this.SetNewError("Error 12. Order with state NONE");
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
            8.PriceCondition – цена активации стоп-ордера
            9.Volume – объём ордера
            10.State – статус ордера
            11.TimeCallBack, TimeCreate – забиваем при всех откликах от сервера по ордеру
            12.TimeDone – время сервера когда ордер получил статус Done
            13.TimeCancel – время сервера когда ордер получил статус Cancel
            */

            if (order.Side != _WaitSide)
            {
                this.SetNewError("Error 13. Wait side not equal. Wait: " + _WaitSide
                    + " Side in order: " + order.Side);
                return false;
            }

            if (order.TypeOrder != _waitType)
            {
                this.SetNewError("Error 14. Order Type is not expected. Wait: " + _waitType
                    + " Real type: " + order.TypeOrder);
                return false;
            }

            if (order.TimeCallBack == DateTime.MinValue)
            {
                this.SetNewError("Error 15. TimeCallBack is MinValue");
                return false;
            }

            if (order.TimeDone == DateTime.MinValue &&
                order.State == OrderStateType.Done)
            {
                this.SetNewError("Error 16. Order Done, buy TimeDone is MinValue");
                return false;
            }

            if (order.TimeCancel == DateTime.MinValue &&
                order.State == OrderStateType.Cancel)
            {
                this.SetNewError("Error 17. Order Cancel, buy TimeCancel is MinValue");
                return false;
            }

            if (order.NumberUser == 0)
            {
                this.SetNewError("Error 18. NumberUser is zero");
                return false;
            }

            if (order.State != OrderStateType.Fail
                && string.IsNullOrEmpty(order.NumberMarket))
            {
                this.SetNewError("Error 19. NumberMarket is null or empty");
                return false;
            }

            if (string.IsNullOrEmpty(order.SecurityNameCode))
            {
                this.SetNewError("Error 20. SecurityNameCode is null or empty");
                return false;
            }

            if (string.IsNullOrEmpty(order.PortfolioNumber))
            {
                this.SetNewError("Error 21. PortfolioNumber is null or empty");
                return false;
            }

            if (order.Side == Side.None)
            {
                this.SetNewError("Error 22. Side is None");
                return false;
            }

            if (order.State != OrderStateType.Fail
                && order.Price <= 0)
            {
                this.SetNewError("Error 23. Price is zero");
                return false;
            }

            if (order.State != OrderStateType.Fail
                && order.StopPrice <= 0)
            {
                this.SetNewError("Error 24. StopPrice is zero");
                return false;
            }

            if (order.State != OrderStateType.Fail &&
                order.State != OrderStateType.Cancel &&
                order.Volume <= 0)
            {
                this.SetNewError("Error 25. Volume is zero");
                return false;
            }

            return true;
        }
    }
}
