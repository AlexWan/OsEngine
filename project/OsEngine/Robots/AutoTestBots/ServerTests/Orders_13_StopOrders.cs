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
    public class Orders_13_StopOrders : AServerTester
    {
        public string SecurityNameToTrade = "ETHUSDT";

        public string SecurityClassToTrade = "Futures";

        public decimal VolumeToTrade;

        public string PortfolioName;

        public override void Process()
        {
            IServerPermission permission = ServerMaster.GetServerPermission(Server.ServerType);

            if (permission == null)
            {
                this.SetNewError("Error 0. No server permission");
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

            if (Server.ServerStatus != ServerConnectStatus.Connect)
            {
                this.SetNewError("Error 1. Server Status Disconnect");
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
                if (securities[i].Name == SecurityNameToTrade
                    && securities[i].NameClass == SecurityClassToTrade)
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

            int waitAfterConnect = permission.WaitTimeSecondsAfterFirstStartToSendOrders;

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

            // стоп-маркет на покупку. Активация выше лучшего аска - за противоположной стороной стакана

            SendBuyStopOrder(mySecurity, md.Asks[0].Price.ToDecimal());

            if (this._errors != null &&
                this._errors.Count > 0)
            {
                TestEnded();
                return;
            }

            Thread.Sleep(5000);

            md = _md;

            if (md == null ||
                md.Bids.Count == 0 ||
                md.Bids[0].Price == 0)
            {
                SetNewError("Error 8. No bid in Market Depth before sell stop");
                TestEnded();
                return;
            }

            // стоп-маркет на продажу. Активация ниже лучшего бида - за противоположной стороной стакана

            SendSellStopOrder(mySecurity, md.Bids[0].Price.ToDecimal());

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

        private void SendBuyStopOrder(Security mySec, decimal bestAsk)
        {
            // активация близко к цене. На живом рынке цена может уйти раньше срабатывания,
            // поэтому: не сработало за 75 секунд - снимаем и перевыставляем по свежему стакану

            for (int attempt = 0; attempt < 4; attempt++)
            {
                decimal ask = bestAsk;

                if (_md != null
                    && _md.Asks.Count > 0
                    && _md.Asks[0].Price != 0)
                {
                    ask = _md.Asks[0].Price.ToDecimal();
                }

                decimal priceActivate = Math.Round(ask + mySec.PriceStep * 2, mySec.Decimals); // активация на 2 пункта выше лучшего аска

                Order newOrder = CreateStopOrder(mySec, priceActivate, VolumeToTrade, Side.Buy);
                _waitSide = Side.Buy;

                Server.ExecuteOrder(newOrder);

                DateTime timeEndWait = DateTime.Now.AddSeconds(60);
                bool isAlive = false;

                // нужно дождаться когда будет Active order
                while (DateTime.Now < timeEndWait)
                {
                    if (_ordersFail.Count != 0)
                    {   // брокер отклонил (цена успела пересечь активацию) - новая попытка
                        break;
                    }

                    if (_ordersActive.Count != 0
                        || _ordersDone.Count != 0)
                    {
                        isAlive = true;
                        break;
                    }

                    Thread.Sleep(500);
                }

                if (isAlive == false)
                {
                    ClearOrders();
                    continue;
                }

                if (_ordersActive.Count != 0)
                {
                    _waitNumberMarket = _ordersActive[0].NumberMarket;
                }
                else
                {
                    _waitNumberMarket = _ordersDone[0].NumberMarket;
                }

                SetNewServiceInfo("BuyStopMarket Active order income Check! Attempt " + (attempt + 1));

                timeEndWait = DateTime.Now.AddSeconds(75);
                bool isTriggered = false;

                // ждём активацию стопа: стоп получает Cancel (с id дочернего ордера),
                // а порождённый им ордер исполняется по рынку и приходит со статусом Done
                while (DateTime.Now < timeEndWait)
                {
                    if (_ordersFail.Count != 0)
                    {
                        break;
                    }

                    if (_ordersCancel.Count != 0
                        && _childOrdersDone.Count != 0)
                    {
                        isTriggered = true;
                        break;
                    }

                    Thread.Sleep(500);
                }

                if (isTriggered == false)
                {
                    // не сработало - снимаем и перевыставляем по свежей цене

                    if (_ordersActive.Count != 0)
                    {
                        Order orderToCancel = _ordersActive[0];
                        Server.CancelOrder(orderToCancel);

                        DateTime cancelWait = DateTime.Now.AddSeconds(60);

                        while (DateTime.Now < cancelWait)
                        {
                            if (_ordersCancel.Count != 0)
                            {
                                break;
                            }

                            Thread.Sleep(500);
                        }
                    }

                    ClearOrders();
                    continue;
                }

                SetNewServiceInfo("BuyStopMarket triggered. Stop Cancel + child Done order income Check!");

                timeEndWait = DateTime.Now.AddMinutes(2);

                // нужно дождаться когда придут MyTrade на полный объём ордера
                while (true)
                {
                    if (timeEndWait < DateTime.Now)
                    {
                        this.SetNewError("Error 13. No MyTrade BuyStopMarket");
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
                    return;
                }

                SetNewServiceInfo("BuyStopMarket Done myTrade income Check!");

                ClearOrders();
                return;
            }

            this.SetNewError("Error 11. BuyStopMarket did not trigger after 4 attempts (no Cancel on stop + no Done on child order)");
        }

        private void SendSellStopOrder(Security mySec, decimal bestBid)
        {
            // активация близко к цене. На живом рынке цена может уйти раньше срабатывания,
            // поэтому: не сработало за 75 секунд - снимаем и перевыставляем по свежему стакану

            for (int attempt = 0; attempt < 4; attempt++)
            {
                decimal bid = bestBid;

                if (_md != null
                    && _md.Bids.Count > 0
                    && _md.Bids[0].Price != 0)
                {
                    bid = _md.Bids[0].Price.ToDecimal();
                }

                decimal priceActivate = Math.Round(bid - mySec.PriceStep * 2, mySec.Decimals); // активация на 2 пункта ниже лучшего бида

                Order newOrder = CreateStopOrder(mySec, priceActivate, VolumeToTrade, Side.Sell);
                newOrder.PositionConditionType = OrderPositionConditionType.Close;
                _waitSide = Side.Sell;

                Server.ExecuteOrder(newOrder);

                DateTime timeEndWait = DateTime.Now.AddSeconds(60);
                bool isAlive = false;

                // нужно дождаться когда будет Active order
                while (DateTime.Now < timeEndWait)
                {
                    if (_ordersFail.Count != 0)
                    {   // брокер отклонил (цена успела пересечь активацию) - новая попытка
                        break;
                    }

                    if (_ordersActive.Count != 0
                        || _ordersDone.Count != 0)
                    {
                        isAlive = true;
                        break;
                    }

                    Thread.Sleep(500);
                }

                if (isAlive == false)
                {
                    ClearOrders();
                    continue;
                }

                if (_ordersActive.Count != 0)
                {
                    _waitNumberMarket = _ordersActive[0].NumberMarket;
                }
                else
                {
                    _waitNumberMarket = _ordersDone[0].NumberMarket;
                }

                SetNewServiceInfo("SellStopMarket Active order income Check! Attempt " + (attempt + 1));

                timeEndWait = DateTime.Now.AddSeconds(75);
                bool isTriggered = false;

                // ждём активацию стопа: стоп получает Cancel (с id дочернего ордера),
                // а порождённый им ордер исполняется по рынку и приходит со статусом Done
                while (DateTime.Now < timeEndWait)
                {
                    if (_ordersFail.Count != 0)
                    {
                        break;
                    }

                    if (_ordersCancel.Count != 0
                        && _childOrdersDone.Count != 0)
                    {
                        isTriggered = true;
                        break;
                    }

                    Thread.Sleep(500);
                }

                if (isTriggered == false)
                {
                    // не сработало - снимаем и перевыставляем по свежей цене

                    if (_ordersActive.Count != 0)
                    {
                        Order orderToCancel = _ordersActive[0];
                        Server.CancelOrder(orderToCancel);

                        DateTime cancelWait = DateTime.Now.AddSeconds(60);

                        while (DateTime.Now < cancelWait)
                        {
                            if (_ordersCancel.Count != 0)
                            {
                                break;
                            }

                            Thread.Sleep(500);
                        }
                    }

                    ClearOrders();
                    continue;
                }

                SetNewServiceInfo("SellStopMarket triggered. Stop Cancel + child Done order income Check!");

                timeEndWait = DateTime.Now.AddMinutes(2);

                // нужно дождаться когда придут MyTrade на полный объём ордера
                while (true)
                {
                    if (timeEndWait < DateTime.Now)
                    {
                        this.SetNewError("Error 18. No MyTrade SellStopMarket");
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
                    return;
                }

                SetNewServiceInfo("SellStopMarket Done myTrade income Check!");

                ClearOrders();
                return;
            }

            this.SetNewError("Error 16. SellStopMarket did not trigger after 4 attempts (no Cancel on stop + no Done on child order)");
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
            _myTrades.Clear();
            _childNumberMarket = null;
        }

        private void Server_NewOrderIncomeEvent(Order order)
        {
            if (order.State == OrderStateType.None)
            {
                this.SetNewError("Error 19. Order with state NONE");
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

                if (string.IsNullOrEmpty(order.ChildOrderNumberMarket) == false)
                {   // активация стопа: запоминаем id порождённого им ордера
                    _childNumberMarket = order.ChildOrderNumberMarket;
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

        private void ChildOrderIncome(Order order)
        {
            if (order.ParentOrderNumberMarket != _waitNumberMarket)
            {   // дочерний ордер чужого стопа (например, сработал стоп от прошлого прогона) - не наш
                return;
            }

            if (order.TypeOrder != OrderPriceType.Market
                && order.TypeOrder != OrderPriceType.Limit)
            {
                this.SetNewError("Error 49. Child order Type is not Market or Limit. Real type: " + order.TypeOrder);
                return;
            }

            if (order.Side != _waitSide)
            {
                this.SetNewError("Error 50. Child order. Wait side not equal. Wait: " + _waitSide
                    + " Side in order: " + order.Side);
                return;
            }

            if (string.IsNullOrEmpty(order.NumberMarket))
            {
                this.SetNewError("Error 51. Child order. NumberMarket is null or empty");
                return;
            }

            if (order.NumberUser == 0)
            {
                this.SetNewError("Error 52. Child order. NumberUser is zero");
                return;
            }

            if (order.Volume <= 0)
            {
                this.SetNewError("Error 53. Child order. Volume is zero");
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
                this.SetNewError("Error 20. Wait side not equal. Wait: " + _waitSide
                    + " Side in order: " + order.Side);
                return false;
            }

            if (order.TypeOrder != OrderPriceType.StopMarket)
            {
                this.SetNewError("Error 21. Order Type is not StopMarket. Real type: " + order.TypeOrder);
                return false;
            }

            if (order.TimeCallBack == DateTime.MinValue)
            {
                this.SetNewError("Error 22. TimeCallBack is MinValue");
                return false;
            }

            DateTime now = DateTime.Now;

            if (order.TimeCallBack.AddDays(-1) > now)
            {
                this.SetNewError("Error 23. Order. TimeCallBack is to big. Time: " + order.TimeCallBack.ToString());
                return false;
            }

            if (order.TimeCallBack.AddDays(1) < now)
            {
                this.SetNewError("Error 24. Order. TimeCallBack is to small. Time: " + order.TimeCallBack.ToString());
                return false;
            }

            if (order.State == OrderStateType.Done)
            {
                if (order.TimeDone == DateTime.MinValue)
                {
                    this.SetNewError("Error 25. Order Done, TimeDone is MinValue");
                    return false;
                }
                if (order.TimeDone.AddDays(-1) > now)
                {
                    this.SetNewError("Error 26. Order. TimeDone is to big. Time: " + order.TimeDone.ToString());
                    return false;
                }

                if (order.TimeDone.AddDays(1) < now)
                {
                    this.SetNewError("Error 27. Order. TimeDone is to small. Time: " + order.TimeDone.ToString());
                    return false;
                }
            }

            if (order.State == OrderStateType.Cancel)
            {
                if (order.TimeCancel == DateTime.MinValue)
                {
                    this.SetNewError("Error 28. Order Cancel, buy TimeCancel is MinValue");
                    return false;
                }
                if (order.TimeCancel.AddDays(-1) > now)
                {
                    this.SetNewError("Error 29. Order. TimeCancel is to big. Time: " + order.TimeCancel.ToString());
                    return false;
                }

                if (order.TimeCancel.AddDays(1) < now)
                {
                    this.SetNewError("Error 30. Order. TimeCancel is to small. Time: " + order.TimeCancel.ToString());
                    return false;
                }
            }

            if (order.NumberUser == 0)
            {
                this.SetNewError("Error 31. NumberUser is zero");
                return false;
            }

            if (order.State != OrderStateType.Fail
                && string.IsNullOrEmpty(order.NumberMarket))
            {
                this.SetNewError("Error 32. NumberMarket is null or empty");
                return false;
            }

            if (string.IsNullOrEmpty(order.SecurityNameCode))
            {
                this.SetNewError("Error 33. SecurityNameCode is null or empty");
                return false;
            }

            if (string.IsNullOrEmpty(order.PortfolioNumber))
            {
                this.SetNewError("Error 34. PortfolioNumber is null or empty");
                return false;
            }

            if (order.Side == Side.None)
            {
                this.SetNewError("Error 35. Side is None");
                return false;
            }

            if (order.State != OrderStateType.Fail
                && order.StopPrice <= 0)
            {
                this.SetNewError("Error 36. PriceCondition is zero");
                return false;
            }

            if (order.State != OrderStateType.Fail &&
                order.State != OrderStateType.Cancel &&
                order.Volume <= 0)
            {
                this.SetNewError("Error 37. Volume is zero");
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
            for (int i = 0; i < _myTrades.Count; i++)
            {
                if (_myTrades[i].NumberOrderParent != _childNumberMarket)
                {   // трейды проходят по дочернему ордеру, порождённому стопом при активации
                    this.SetNewError("Error 47. MyTrade NumberOrderParent not equal child order NumberMarket. Wait: "
                        + _childNumberMarket + " Real: " + _myTrades[i].NumberOrderParent
                        + " Side: " + _myTrades[i].Side);
                    return false;
                }
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
                this.SetNewError("Error 38. MyTrade. Wait side not equal. Wait: " + _waitSide
                  + " Side in order: " + myTrade.Side);
                return false;
            }

            if (myTrade.Volume <= 0)
            {
                this.SetNewError("Error 39. MyTrade. Volume is zero");
                return false;
            }

            if (myTrade.Price <= 0)
            {
                this.SetNewError("Error 40. MyTrade. Price is zero");
                return false;
            }

            if (string.IsNullOrEmpty(myTrade.SecurityNameCode))
            {
                this.SetNewError("Error 41. MyTrade. SecurityNameCode is null or empty");
                return false;
            }

            if (string.IsNullOrEmpty(myTrade.NumberOrderParent))
            {
                this.SetNewError("Error 42. MyTrade. NumberOrderParent is null or empty");
                return false;
            }

            if (string.IsNullOrEmpty(myTrade.NumberTrade))
            {
                this.SetNewError("Error 43. MyTrade. NumberTrade is null or empty");
                return false;
            }

            if (myTrade.Time == DateTime.MinValue)
            {
                this.SetNewError("Error 44. MyTrade. Time is min value");
                return false;
            }

            DateTime now = DateTime.Now;

            if (myTrade.Time.AddDays(-1) > now)
            {
                this.SetNewError("Error 45. MyTrade. Time is to big. Time: " + myTrade.Time.ToString());
                return false;
            }

            if (myTrade.Time.AddDays(1) < now)
            {
                this.SetNewError("Error 46. MyTrade. Time is to small. Time: " + myTrade.Time.ToString());
                return false;
            }

            return true;
        }
    }
}
