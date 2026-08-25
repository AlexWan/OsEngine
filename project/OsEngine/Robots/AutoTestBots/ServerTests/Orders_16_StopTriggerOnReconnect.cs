/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using OsEngine.Market.Servers;
using System;
using System.Collections.Generic;
using System.Threading;
using OsEngine.Entity;
using OsEngine.Market;

namespace OsEngine.Robots.AutoTestBots.ServerTests
{
    public class Orders_16_StopTriggerOnReconnect : AServerTester
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

            if (serverPermission.StopOrdersIsSupport == false)
            {
                SetNewServiceInfo("No permission. StopOrdersIsSupport == false. Test over");
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

            // после подключения сервера нельзя сразу слать ордера:
            // AServer.ExecuteOrder отклонит их Fail'ом в первые WaitTimeSecondsAfterFirstStartToSendOrders секунд

            int waitAfterConnect = serverPermission.WaitTimeSecondsAfterFirstStartToSendOrders;

            if (waitAfterConnect > 0)
            {
                SetNewServiceInfo("Waiting " + (waitAfterConnect + 5) + " sec after server connect before sending orders");
                Thread.Sleep((waitAfterConnect + 5) * 1000);
            }

            Server.NewMarketDepthEvent += Server_NewMarketDepthEvent;
            Server.NewOrderIncomeEvent += Server_NewOrderIncomeEvent;
            Server.NewMyTradeEvent += Server_NewMyTradeEvent;

            Server.ServerRealization.Subscribe(mySecurity);

            DateTime timeStartWait = DateTime.Now.AddMinutes(2);

            while (_md == null)
            {
                if (timeStartWait < DateTime.Now)
                {
                    SetNewError("Error 5. No market depth after 2 minutes");
                    TestEnded();
                    return;
                }

                Thread.Sleep(1000);
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

            // 1 выставляем стоп-маркет на покупку близко к рынку.
            // Он должен остаться активным до отключения сервера

            Order order = SendBuyStopMarketOrder(mySecurity);

            if (order == null)
            {
                TestEnded();
                return;
            }

            int orderNumberUser = order.NumberUser;
            _waitNumberMarket = order.NumberMarket;

            SetNewServiceInfo("Stop order placed. NumUser: " + orderNumberUser
                + " NumMarket: " + _waitNumberMarket
                + ". Disconnecting server for 5 minutes");

            // 2 отключаем сервер на 5 минут. За это время стоп должен сработать

            Server.StopServer();

            Thread.Sleep(TimeSpan.FromMinutes(5));

            ClearOrders(); // очищаем все массивы с входящими статусами ордеров. Пусто

            Server.StartServer();

            DateTime startAwait = DateTime.Now;

            while (Server.ServerStatus != ServerConnectStatus.Connect)
            {
                Thread.Sleep(1000);

                if (startAwait.AddMinutes(5) < DateTime.Now)
                {
                    SetNewError("Error 8. Server status did not change in 5 minutes");
                    TestEnded();
                    return;
                }
            }

            SetNewServiceInfo("Server reconnected. Waiting stop order Cancel (activation) and child Done order");

            // 3 дожидаемся после реконнекта активации стопа:
            // стоп получает Cancel (с id дочернего ордера),
            // а порождённый им ордер исполняется и приходит со статусом Done

            DateTime timeEndWait = DateTime.Now.AddMinutes(4);
            bool isTriggered = false;

            while (DateTime.Now < timeEndWait)
            {
                if (_stopActivated
                    && _childOrdersDone.Count != 0)
                {
                    isTriggered = true;
                    break;
                }

                Thread.Sleep(1000);
            }

            if (isTriggered == false)
            {
                // стоп не сработал за время отключения. Отзываем его и фиксируем ошибку

                for (int i = 0; i < _ordersActive.Count; i++)
                {
                    if (_ordersActive[i].NumberUser == orderNumberUser)
                    {
                        CancelOrder(_ordersActive[i]);
                        break;
                    }
                }

                SetNewError("Error 9. Stop order did not trigger during 5 minutes disconnect");
                TestEnded();
                return;
            }

            SetNewServiceInfo("API sent activated stop (Cancel) and child Done order after reconnect. Stop NumMarket: " +
                 _waitNumberMarket + " Child NumMarket: " + _childNumberMarket);

            // 4 дожидаемся MyTrade на полный объём ордера

            timeEndWait = DateTime.Now.AddMinutes(2);

            while (true)
            {
                if (timeEndWait < DateTime.Now)
                {
                    this.SetNewError("Error 10. No MyTrade after reconnect");
                    TestEnded();
                    return;
                }

                if (MyTradesVolume() >= VolumeToTrade)
                {
                    break;
                }

                Thread.Sleep(1000);
            }

            // ждём возможные дубли трейдов, затем проверяем привязку и суммарный объём

            Thread.Sleep(10000);

            if (CheckMyTrades() == false)
            {
                TestEnded();
                return;
            }

            SetNewServiceInfo("MyTrade after reconnect income Check!");

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

        Side _waitSide;

        private Order SendBuyStopMarketOrder(Security mySec)
        {
            // активация близко к цене. Если стоп сработал до отключения сервера
            // или брокер отклонил - перевыставляем по свежему стакану

            for (int attempt = 0; attempt < 4; attempt++)
            {
                decimal ask = 0;

                if (_md != null
                    && _md.Asks.Count > 0
                    && _md.Asks[0].Price != 0)
                {
                    ask = _md.Asks[0].Price.ToDecimal();
                }

                if (ask == 0)
                {
                    Thread.Sleep(1000);
                    continue;
                }

                decimal priceActivate = Math.Round(ask + mySec.PriceStep * 2, mySec.Decimals); // активация на 2 пункта выше лучшего аска

                Order newOrder = CreateStopOrder(mySec, priceActivate, VolumeToTrade, Side.Buy);
                _waitSide = Side.Buy;

                Server.ExecuteOrder(newOrder);

                DateTime timeEndWait = DateTime.Now.AddSeconds(60);

                // нужно дождаться когда будет Active order
                while (DateTime.Now < timeEndWait)
                {
                    if (_ordersFail.Count != 0)
                    {   // брокер отклонил (цена успела пересечь активацию) - новая попытка
                        break;
                    }

                    if (_ordersActive.Count != 0)
                    {
                        Order order = _ordersActive[0];

                        SetNewServiceInfo("BuyStopMarket Active order income Check! Attempt " + (attempt + 1));

                        ClearOrders();

                        return order;
                    }

                    if (_ordersDone.Count != 0)
                    {   // стоп успел сработать до отключения - новая попытка
                        break;
                    }

                    Thread.Sleep(500);
                }

                ClearOrders();
            }

            this.SetNewError("Error 11. No Active order from server BuyStopMarket after 4 attempts");
            return null;
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
                    this.SetNewError("Error 12. No canceled stop order from server CancelOrder");
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

        private Order CreateStopOrder(Security sec, decimal priceActivate, decimal volume, Side side)
        {
            Order order = new Order();

            order.Price = priceActivate;
            order.StopPrice = priceActivate;
            order.Volume = volume;
            order.Side = side;
            order.NumberUser = NumberGen.GetNumberOrder(StartProgram.IsOsTrader);
            order.ServerType = Server.ServerType;
            order.TypeOrder = OrderPriceType.StopMarket;
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
            _childOrdersDone.Clear();
            _childNumberMarket = null;
            _stopActivated = false;
        }

        private void Server_NewOrderIncomeEvent(Order order)
        {
            if (order.State == OrderStateType.None)
            {
                this.SetNewError("Error 13. Order with state NONE");
                return;
            }

            if (string.IsNullOrEmpty(order.ParentOrderNumberMarket) == false)
            {   // дочерний ордер активированного стопа. Валидируется отдельно
                ChildOrderIncome(order);
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

                if (order.NumberMarket == _waitNumberMarket)
                {   // активация нашего стопа (Cancel - штатный статус активированного стопа)
                    _stopActivated = true;

                    if (string.IsNullOrEmpty(order.ChildOrderNumberMarket) == false)
                    {
                        _childNumberMarket = order.ChildOrderNumberMarket;
                    }
                }
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

        // дочерние ордера активированного стопа: TypeOrder Limit/Market, без StopPrice.
        // TimeDone у дочернего Done не требуем - коннектор его пока не заполняет

        List<Order> _childOrdersDone = new List<Order>();

        string _childNumberMarket;

        bool _stopActivated = false;

        private void ChildOrderIncome(Order order)
        {
            if (order.ParentOrderNumberMarket != _waitNumberMarket)
            {   // дочерний ордер чужого стопа (например, сработал стоп от прошлого прогона) - не наш
                return;
            }

            if (order.TypeOrder != OrderPriceType.Market
                && order.TypeOrder != OrderPriceType.Limit)
            {
                this.SetNewError("Error 41. Child order Type is not Market or Limit. Real type: " + order.TypeOrder);
                return;
            }

            if (order.Side != _waitSide)
            {
                this.SetNewError("Error 42. Child order. Wait side not equal. Wait: " + _waitSide
                    + " Side in order: " + order.Side);
                return;
            }

            if (string.IsNullOrEmpty(order.NumberMarket))
            {
                this.SetNewError("Error 43. Child order. NumberMarket is null or empty");
                return;
            }

            if (order.NumberUser == 0)
            {
                this.SetNewError("Error 44. Child order. NumberUser is zero");
                return;
            }

            if (order.Volume <= 0)
            {
                this.SetNewError("Error 45. Child order. Volume is zero");
                return;
            }

            _childNumberMarket = order.NumberMarket;

            if (order.State == OrderStateType.Done)
            {   // дедупликация: коннектор может эмитить дочерний ордер повторно
                bool alreadyExists = false;

                for (int i = 0; i < _childOrdersDone.Count; i++)
                {
                    if (_childOrdersDone[i].NumberMarket == order.NumberMarket)
                    {
                        alreadyExists = true;
                        break;
                    }
                }

                if (alreadyExists == false)
                {
                    _childOrdersDone.Add(order);
                }
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
            7.PriceCondition – цена активации стоп-ордера
            8.Volume – объём ордера
            9.State – статус ордера
            10.TimeCallBack, TimeCreate – забиваем при всех откликах от сервера по ордеру
            11.TimeDone – время сервера когда ордер получил статус Done
            12.TimeCancel – время сервера когда ордер получил статус Cancel
            */

            if (order.Side != _waitSide)
            {
                this.SetNewError("Error 14. Wait side not equal. Wait: " + _waitSide
                    + " Side in order: " + order.Side);
                return false;
            }

            if (order.TypeOrder != OrderPriceType.StopMarket)
            {
                this.SetNewError("Error 15. Order Type is not StopMarket. Real type: " + order.TypeOrder);
                return false;
            }

            if (order.TimeCallBack == DateTime.MinValue)
            {
                this.SetNewError("Error 16. TimeCallBack is MinValue");
                return false;
            }

            if (order.TimeDone == DateTime.MinValue &&
                order.State == OrderStateType.Done)
            {
                this.SetNewError("Error 17. Order Done, buy TimeDone is MinValue");
                return false;
            }

            if (order.TimeCancel == DateTime.MinValue &&
                order.State == OrderStateType.Cancel)
            {
                this.SetNewError("Error 18. Order Cancel, buy TimeCancel is MinValue");
                return false;
            }

            if (order.NumberUser == 0)
            {
                this.SetNewError("Error 19. NumberUser is zero");
                return false;
            }

            if (order.State != OrderStateType.Fail
                && string.IsNullOrEmpty(order.NumberMarket))
            {
                this.SetNewError("Error 20. NumberMarket is null or empty");
                return false;
            }

            if (string.IsNullOrEmpty(order.SecurityNameCode))
            {
                this.SetNewError("Error 21. SecurityNameCode is null or empty");
                return false;
            }

            if (string.IsNullOrEmpty(order.PortfolioNumber))
            {
                this.SetNewError("Error 22. PortfolioNumber is null or empty");
                return false;
            }

            if (order.Side == Side.None)
            {
                this.SetNewError("Error 23. Side is None");
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

        List<MyTrade> _myTrades = new List<MyTrade>();

        string _waitNumberMarket;

        private decimal MyTradesVolume()
        {
            decimal volume = 0;

            for (int i = 0; i < _myTrades.Count; i++)
            {
                volume += _myTrades[i].Volume;
            }

            return volume;
        }

        private bool CheckMyTrades()
        {
            decimal volume = 0;

            for (int i = 0; i < _myTrades.Count; i++)
            {
                if (_myTrades[i].NumberOrderParent != _childNumberMarket)
                {   // трейды проходят по дочернему ордеру, порождённому стопом при активации
                    this.SetNewError("Error 26. MyTrade NumberOrderParent not equal child order NumberMarket. Wait: "
                        + _childNumberMarket + " Real: " + _myTrades[i].NumberOrderParent);
                    return false;
                }

                volume += _myTrades[i].Volume;
            }

            if (volume != VolumeToTrade)
            {
                this.SetNewError("Error 27. MyTrades total volume not equal order volume. Wait: "
                    + VolumeToTrade + " Real: " + volume);
                return false;
            }

            return true;
        }

        private void Server_NewMyTradeEvent(MyTrade myTrade)
        {
            if (_childNumberMarket != null
                && myTrade.NumberOrderParent != _childNumberMarket)
            {   // трейд по другому ордеру (например, по стопу от прошлого прогона) - не наш
                return;
            }

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

            if (myTrade.Side != _waitSide)
            {
                this.SetNewError("Error 28. MyTrade. Wait side not equal. Wait: " + _waitSide
                  + " Side in order: " + myTrade.Side);
                return false;
            }

            if (myTrade.Volume <= 0)
            {
                this.SetNewError("Error 29. MyTrade. Volume is zero");
                return false;
            }

            if (myTrade.Price <= 0)
            {
                this.SetNewError("Error 30. MyTrade. Price is zero");
                return false;
            }

            if (string.IsNullOrEmpty(myTrade.SecurityNameCode))
            {
                this.SetNewError("Error 31. MyTrade. SecurityNameCode is null or empty");
                return false;
            }

            if (string.IsNullOrEmpty(myTrade.NumberOrderParent))
            {
                this.SetNewError("Error 32. MyTrade. NumberOrderParent is null or empty");
                return false;
            }

            if (string.IsNullOrEmpty(myTrade.NumberTrade))
            {
                this.SetNewError("Error 33. MyTrade. NumberTrade is null or empty");
                return false;
            }

            if (myTrade.Time == DateTime.MinValue)
            {
                this.SetNewError("Error 34. MyTrade. Time is min value");
                return false;
            }

            DateTime now = DateTime.Now;

            if (myTrade.Time.AddDays(-1) > now)
            {
                this.SetNewError("Error 35. MyTrade. Time is to big. Time: " + myTrade.Time.ToString());
                return false;
            }

            if (myTrade.Time.AddDays(1) < now)
            {
                this.SetNewError("Error 36. MyTrade. Time is to small. Time: " + myTrade.Time.ToString());
                return false;
            }

            return true;
        }
    }
}
