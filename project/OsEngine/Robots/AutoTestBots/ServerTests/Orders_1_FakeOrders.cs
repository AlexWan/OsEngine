using OsEngine.Market.Servers;
using System;
using System.Collections.Generic;
using System.Threading;
using OsEngine.Entity;

namespace OsEngine.Robots.AutoTestBots.ServerTests
{
    public class Orders_1_FakeOrders : AServerTester
    {
        public string SecurityNameToTrade = "ETHUSDT";

        public string SecurityClassToTrade = "Futures";

        public decimal VolumeMin;

        public decimal VolumeMax;

        public string PortfolioName;

        public override void Process()
        {
            if(Server.ServerStatus != ServerConnectStatus.Connect)
            {
                this.SetNewError("Error 1. Server Status Disconnect");
                TestEnded();
                return;
            }

            List<Security> securities = Server.Securities;

            if (securities == null &&
                securities.Count == 0)
            {
                SetNewError("Error 2. No securities in server found");
                TestEnded();
                return;
            }

            Security mySecurity = null;

            for(int i = 0;i < securities.Count;i++)
            {
                if (securities[i].Name == SecurityNameToTrade 
                    && securities[i].NameClass == SecurityClassToTrade)
                {
                    mySecurity = securities[i];
                    break;
                }
            }

            if(mySecurity == null)
            {
                SetNewError("Error 3. No securities found");
                TestEnded();
                return;
            }

            if (VolumeMin <= 0 ||
                VolumeMax <= 0)
            {
                SetNewError("Error 4. Volume is zero");
                TestEnded();
                return;
            }

            Server.NewMarketDepthEvent += Server_NewMarketDepthEvent;
            Server.NewOrderIncomeEvent += Server_NewOrderIncomeEvent;

            Server.ServerRealization.Subscribe(mySecurity);

            DateTime timeStartWait = DateTime.Now.AddMinutes(2);

            while(_md == null)
            {
                if(timeStartWait < DateTime.Now)
                {
                    SetNewError("Error 5. No market depth after 2 minutes");
                    TestEnded();
                    return;
                }
            }

            MarketDepth md = _md;

            if(md.Asks.Count == 0 ||
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

            // ордер с уменьшенным объёмом 

            decimal price = Math.Round((md.Asks[0].Price.ToDecimal() + md.Bids[0].Price.ToDecimal()) /2,mySecurity.Decimals);

            SendFakeSmallVolume(mySecurity, price);

            if(this._errors != null &&
                this._errors.Count > 0)
            {
                TestEnded();
            }

            // ордер с объёмом 0

            SendFakeVolumeZero(mySecurity, price);

            if (this._errors != null &&
                this._errors.Count > 0)
            {
                TestEnded();
            }

            // ордер с объёмом меньше нуля

            SendFakeVolumeLessZero(mySecurity, price);

            if (this._errors != null &&
                this._errors.Count > 0)
            {
                TestEnded();
            }

            // ордер с объёмом меньше нуля

            SendFakeBigVolume(mySecurity, price);

            if (this._errors != null &&
                this._errors.Count > 0)
            {
                TestEnded();
            }

            // ордер с нулевой ценой

            SendFakeZeroPrice(mySecurity, price);

            if (this._errors != null &&
                this._errors.Count > 0)
            {
                TestEnded();
            }

            // ордер с отрицательной ценой

            SendFakeLessZeroPrice(mySecurity, price);

            if (this._errors != null &&
                this._errors.Count > 0)
            {
                TestEnded();
            }

            TestEnded();
        }

        MarketDepth _md;

        private void Server_NewMarketDepthEvent(MarketDepth md)
        {
            _md = md;
        }

        Side _waitSide;

        private void SendFakeVolumeZero(Security mySec, decimal price)
        {
            // отсылаем ордер с явно заниженным объёмом

            decimal volume = 0;

            Order newOrder = CreateOrder(mySec, price, volume, Side.Sell);
            _waitSide = Side.Sell;

            Server.ExecuteOrder(newOrder);

            DateTime timeEndWait = DateTime.Now.AddMinutes(2);

            // нужно дождаться когда будет сервер
            while (true)
            {
                if (timeEndWait < DateTime.Now)
                {
                    this.SetNewError("Error 8. No reject order from server FakeVolumeZero");
                    return;
                }

                if (_ordersFail.Count != 0)
                {
                    this.SetNewServiceInfo("FakeVolumeZero Check!");
                    break;
                }
                else
                {
                    Thread.Sleep(1000);
                }
            }

            ClearOrders();
        }

        private void SendFakeVolumeLessZero(Security mySec, decimal price)
        {
            // отсылаем ордер с явно заниженным объёмом

            decimal volume = -1;

            Order newOrder = CreateOrder(mySec, price, volume, Side.Sell);
            _waitSide = Side.Sell;

            Server.ExecuteOrder(newOrder);

            DateTime timeEndWait = DateTime.Now.AddMinutes(2);

            // нужно дождаться когда будет сервер
            while (true)
            {
                if (timeEndWait < DateTime.Now)
                {
                    this.SetNewError("Error 9. No reject order from server FakeVolumeLessZero");
                    return;
                }

                if (_ordersFail.Count != 0)
                {
                    this.SetNewServiceInfo("FakeVolumeLessZero Check!");
                    break;
                }
                else
                {
                    Thread.Sleep(1000);
                }
            }

            ClearOrders();
        }

        private void SendFakeSmallVolume(Security mySec, decimal price)
        {
            // отсылаем ордер с объёмом ниже нуля

            decimal volume = VolumeMin; 

            Order newOrder = CreateOrder(mySec,price,volume, Side.Buy);
            _waitSide = Side.Buy;

            Server.ExecuteOrder(newOrder);

            DateTime timeEndWait = DateTime.Now.AddMinutes(2);

            // нужно дождаться когда будет сервер
            while(true)
            {
                if(timeEndWait < DateTime.Now)
                {
                    this.SetNewError("Error 10. No reject order from server FakeVolumeSmall");
                    return;
                }

                if(_ordersFail.Count != 0)
                {
                    this.SetNewServiceInfo("FakeVolumeSmall Check!");
                    break;
                }
                else
                {
                    Thread.Sleep(1000);
                }
            }

            ClearOrders();
        }

        private void SendFakeBigVolume(Security mySec, decimal price)
        {
            // отсылаем ордер с объёмом ниже нуля

            decimal volume = VolumeMax;

            Order newOrder = CreateOrder(mySec, price, volume, Side.Buy);
            _waitSide = Side.Buy;

            Server.ExecuteOrder(newOrder);

            DateTime timeEndWait = DateTime.Now.AddMinutes(2);

            // нужно дождаться когда будет сервер
            while (true)
            {
                if (timeEndWait < DateTime.Now)
                {
                    this.SetNewError("Error 10. No reject order from server FakeVolumeBig");
                    return;
                }

                if (_ordersFail.Count != 0)
                {
                    this.SetNewServiceInfo("FakeVolumeBig Check!");
                    break;
                }
                else
                {
                    Thread.Sleep(1000);
                }
            }

            ClearOrders();
        }

        private void SendFakeZeroPrice(Security mySec, decimal price)
        {
            // ордер с ценой 0

            decimal volume = VolumeMin;

            Order newOrder = CreateOrder(mySec, 0, volume, Side.Buy);
            _waitSide = Side.Buy;

            Server.ExecuteOrder(newOrder);

            DateTime timeEndWait = DateTime.Now.AddMinutes(2);

            // нужно дождаться когда будет сервер
            while (true)
            {
                if (timeEndWait < DateTime.Now)
                {
                    this.SetNewError("Error 11. No reject order from server FakeZeroPrice");
                    return;
                }

                if (_ordersFail.Count != 0)
                {
                    this.SetNewServiceInfo("FakeZeroPrice Check!");
                    break;
                }
                else
                {
                    Thread.Sleep(1000);
                }
            }

            ClearOrders();
        }

        private void SendFakeLessZeroPrice(Security mySec, decimal price)
        {
            // ордер с ценой ниже нуля

            decimal volume = VolumeMin;

            Order newOrder = CreateOrder(mySec, -1, volume, Side.Sell);
            _waitSide = Side.Sell;

            Server.ExecuteOrder(newOrder);

            DateTime timeEndWait = DateTime.Now.AddMinutes(2);

            // нужно дождаться когда будет сервер
            while (true)
            {
                if (timeEndWait < DateTime.Now)
                {
                    this.SetNewError("Error 12. No reject order from server FakeLessZeroPrice");
                    return;
                }

                if (_ordersFail.Count != 0)
                {
                    this.SetNewServiceInfo("FakeLessZeroPrice Check!");
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
            if(order.State == OrderStateType.None)
            {
                this.SetNewError("Error 13. Order with state NONE");
                return;
            }

            if(OrderIsNormal(order) == false)
            {
                return;
            }

            if(order.State == OrderStateType.Active)
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
            8.Volume – объём ордера
            9.State – статус ордера
            10.TimeCallBack, TimeCreate – забиваем при всех откликах от сервера по ордеру
            11.TimeDone – время сервера когда ордер получил статус Done
            12.TimeCancel – время сервера когда ордер получил статус Cancel
            */

            if(order.Side != _waitSide)
            {
                this.SetNewError("Error 14. Unexpected order side. Expected: " + _waitSide 
                    + " Side in order: " + order.Side);
                return false;
            }

            if(order.TimeCallBack == DateTime.MinValue)
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

            if (string.IsNullOrEmpty(order.SecurityClassCode))
            {
                this.SetNewError("Error 21. SecurityClassCode is null or empty");
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
                && order.Price <= 0)
            {
                this.SetNewError("Error 24. Price is zero");
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