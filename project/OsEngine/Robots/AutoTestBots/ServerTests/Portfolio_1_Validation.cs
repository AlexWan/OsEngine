using OsEngine.Entity;
using System;
using System.Collections.Generic;
using System.Threading;

namespace OsEngine.Robots.AutoTestBots.ServerTests
{
    public class Portfolio_1_Validation : AServerTester
    {
        public string SecurityNameToTrade = "ETHUSDT";

        public string SecurityClassToTrade = "Futures";

        public string AssetInPortfolio = "ETH";

        public decimal VolumeToTrade;

        public string PortfolioName;

        public override void Process()
        {
            // 1 смотрим есть ли вообще портфели

            List<Portfolio> portfolious = Server.Portfolios;

            if(portfolious == null ||
                portfolious.Count == 0)
            {
                SetNewError("Error 1. No Portfolio found");
                TestEnded();
                return;
            }

            for(int i = 0;i < portfolious.Count; i++)
            {
                if (portfolious[i].Number != PortfolioName)
                {
                    continue;
                }

                CheckPortfolio(portfolious[i]);
            }

            // 2 берём бумаги и выбираем ту которую будем торговать

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

            // 3 берём стакан котировок

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


            if(OpenAndCloseLongPositionLogicTest(mySecurity,md) == false)
            {
                TestEnded();
                return;
            }
            
            
            if(OpenAndCloseShortPositionLogicTest(mySecurity,md) == false)
            {
               TestEnded();
               return;
            }

            TestEnded();
        }

        private bool OpenAndCloseLongPositionLogicTest(Security mySecurity, MarketDepth md)
        {
            // 1 берём текущее состояние портфеля

            decimal currentValue = GetCurValueBySecurity(mySecurity);

            this.SetNewServiceInfo("LONG POS. Start asset value: " + currentValue.ToString());

            // 2 ордер на покупку

            SendBuyOrder(mySecurity, md.Asks[0].Price.ToDecimal());

            if (this._errors != null &&
                this._errors.Count > 0)
            {
                return false;
            }

            // 3 ждём когда состояние портфеля изменится

            DateTime endWaitTime = DateTime.Now.AddSeconds(15);

            while (true)
            {
                if (endWaitTime < DateTime.Now)
                {
                    SetNewError("Error 8. Portfolio not change");
                    return false;
                }

                decimal value = GetCurValueBySecurity(mySecurity);

                if (value != currentValue)
                {
                    currentValue = value;
                    break;
                }
            }

            if(currentValue <= 0)
            {
                SetNewError("Error 9. Current volume <= 0");
                return false;
            }

            this.SetNewServiceInfo("Value asset after Buy order: " + currentValue.ToString());

            // 4 ордер на продажу

            if (this._errors != null &&
                this._errors.Count > 0)
            {
                return false;
            }

            SendSellOrder(mySecurity, md.Bids[0].Price.ToDecimal());

            // 5 ждём когда состояние портфеля изменится

            endWaitTime = DateTime.Now.AddSeconds(15);

            while (true)
            {
                if (endWaitTime < DateTime.Now)
                {
                    SetNewError("Error 10. Portfolio not change");
                    return false;
                }

                decimal value = GetCurValueBySecurity(mySecurity);

                if (value != currentValue)
                {
                    currentValue = value;
                    break;
                }
            }

            this.SetNewServiceInfo("Value asset after Sell order: " + currentValue.ToString());

            this.SetNewServiceInfo("LONG POS. test was successful");

            return true;
        }

        private bool OpenAndCloseShortPositionLogicTest(Security mySecurity, MarketDepth md)
        {
            // 1 берём текущее состояние портфеля

            decimal currentValue = GetCurValueBySecurity(mySecurity);

            this.SetNewServiceInfo("SHORT POS. Start asset value: " + currentValue.ToString());

            // 2 ордер на покупку

            SendSellOrder(mySecurity, md.Bids[0].Price.ToDecimal());
            
            if (this._errors != null &&
                this._errors.Count > 0)
            {
                return false;
            }

            // 3 ждём когда состояние портфеля изменится

            DateTime endWaitTime = DateTime.Now.AddSeconds(15);

            while (true)
            {
                if (endWaitTime < DateTime.Now)
                {
                    SetNewError("Error 11. Portfolio not change");
                    return false;
                }

                decimal value = GetCurValueBySecurity(mySecurity);

                if (value != currentValue)
                {
                    currentValue = value;
                    break;
                }
            }

            if(currentValue >= 0)
            {
                SetNewError("Error 12. Value in portfolio >= 0");
                return false;
            }

            this.SetNewServiceInfo("Value asset after Sell order: " + currentValue.ToString());

            // 4 ордер на продажу

            if (this._errors != null &&
                this._errors.Count > 0)
            {
                return false;
            }

            SendBuyOrder(mySecurity, md.Asks[0].Price.ToDecimal());

            // 5 ждём когда состояние портфеля изменится

            endWaitTime = DateTime.Now.AddSeconds(15);

            while (true)
            {
                if (endWaitTime < DateTime.Now)
                {
                    SetNewError("Error 13. Portfolio not change");
                    return false;
                }

                decimal value = GetCurValueBySecurity(mySecurity);

                if (value != currentValue)
                {
                    currentValue = value;
                    break;
                }
            }

            this.SetNewServiceInfo("Value asset after Buy order: " + currentValue.ToString());

            this.SetNewServiceInfo("SHORT POS. test was successful");

            return true;
        }

        private decimal GetCurValueBySecurity(Security sec)
        {
            List<Portfolio> portfolious = Server.Portfolios;

            if (portfolious == null ||
                portfolious.Count == 0)
            {
                SetNewError("Error 14. No Portfolio found");
                TestEnded();
            }

            Portfolio myPorftolio = null;

            for(int i = 0;i < portfolious.Count;i++)
            {
                if (portfolious[i].Number == PortfolioName)
                {
                    myPorftolio = portfolious[i];
                    break;
                }
            }

            if(myPorftolio == null)
            {
                SetNewError("Error 15. Portfolio not found");
                return 0;
            }

            List<PositionOnBoard> myPositions = myPorftolio.GetPositionOnBoard();

            PositionOnBoard myPositionOnBoard = null;

            for(int i = 0;i < myPositions.Count;i++)
            {
                if (myPositions[i].SecurityNameCode.Equals(SecurityNameToTrade) 
                    || myPositions[i].SecurityNameCode == AssetInPortfolio)
                {
                    myPositionOnBoard = myPositions[i];
                    break;
                }
            }

            if(myPositionOnBoard == null)
            {
                return 0;
            }

            return myPositionOnBoard.ValueCurrent;
        }

        private void SendBuyOrder(Security mySec, decimal price)
        {
            decimal volume = VolumeToTrade;

            price = Math.Round(price + price * 0.01m, mySec.Decimals); // Проскальзывание 1%

            Order newOrder = CreateOrder(mySec, price, volume, Side.Buy);
            _waitSide = Side.Buy;

            Server.ExecuteOrder(newOrder);

            DateTime timeEndWait = DateTime.Now.AddMinutes(2);

            // нужно дождаться когда будет Active order
            while (true)
            {
                if (timeEndWait < DateTime.Now)
                {
                    this.SetNewError("Error 16. No Active order from server BuyLimit");
                    return;
                }

                if (_ordersFail.Count != 0)
                {
                    this.SetNewError("Error 17. Order FAIL found from server BuyLimit");
                    return;
                }

                if (_ordersActive.Count != 0)
                {
                    this.SetNewServiceInfo("BuyLimit Active order income Check!");
                    break;
                }
                if (_ordersDone.Count != 0)
                {
                    this.SetNewServiceInfo("BuyLimit Active order income Check!");
                    break;
                }
                else
                {
                    Thread.Sleep(1000);
                }
            }

            timeEndWait = DateTime.Now.AddMinutes(2);

            // нужно дождаться когда будет Done order
            while (true)
            {
                if (timeEndWait < DateTime.Now)
                {
                    this.SetNewError("Error 18. No Done order from server BuyLimit");
                    return;
                }

                if (_ordersFail.Count != 0)
                {
                    this.SetNewError("Error 19. Order FAIL found from server BuyLimit");
                    return;
                }

                if (_ordersDone.Count != 0)
                {
                    this.SetNewServiceInfo("BuyLimit Done order income Check!");
                    break;
                }
                else
                {
                    Thread.Sleep(1000);
                }
            }

            timeEndWait = DateTime.Now.AddMinutes(2);

            // нужно дождаться когда будет придёт MyTrade
            while (true)
            {
                if (timeEndWait < DateTime.Now)
                {
                    this.SetNewError("Error 20. No MyTrade BuyLimit");
                    return;
                }

                if (_myTrades.Count != 0)
                {
                    this.SetNewServiceInfo("BuyLimit Done myTrade income Check!");
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

            price = Math.Round(price - price * 0.01m, mySec.Decimals); // Проскальзывание 1%

            Order newOrder = CreateOrder(mySec, price, volume, Side.Sell);
            _waitSide = Side.Sell;

            Server.ExecuteOrder(newOrder);

            DateTime timeEndWait = DateTime.Now.AddMinutes(2);

            // нужно дождаться когда будет Active order
            while (true)
            {
                if (timeEndWait < DateTime.Now)
                {
                    this.SetNewError("Error 21. No reject order from server SellLimit");
                    return;
                }

                if (_ordersFail.Count != 0)
                {
                    this.SetNewError("Error 22. Order FAIL found from server SellLimit");
                    return;
                }

                if (_ordersActive.Count != 0)
                {
                    this.SetNewServiceInfo("SellLimit Active order income Check!");
                    break;
                }
                if (_ordersDone.Count != 0)
                {
                    this.SetNewServiceInfo("SellLimit Active order income Check!");
                    break;
                }
                else
                {
                    Thread.Sleep(1000);
                }
            }

            timeEndWait = DateTime.Now.AddMinutes(2);

            // нужно дождаться когда будет Done order
            while (true)
            {
                if (timeEndWait < DateTime.Now)
                {
                    this.SetNewError("Error 23. No reject order from server SellLimit");
                    return;
                }

                if (_ordersFail.Count != 0)
                {
                    this.SetNewError("Error 24. Order FAIL found from server SellLimit");
                    return;
                }

                if (_ordersDone.Count != 0)
                {
                    this.SetNewServiceInfo("SellLimit Done order income Check!");
                    break;
                }
                else
                {
                    Thread.Sleep(1000);
                }
            }

            // нужно дождаться когда будет придёт MyTrade

            timeEndWait = DateTime.Now.AddMinutes(2);

            while (true)
            {
                if (timeEndWait < DateTime.Now)
                {
                    this.SetNewError("Error 25. No MyTrade SellLimit");
                    return;
                }

                if (_myTrades.Count != 0)
                {
                    this.SetNewServiceInfo("SellLimit Done myTrade income Check!");
                    break;
                }
                else
                {
                    Thread.Sleep(1000);
                }
            }

            ClearOrders();
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

        public void CheckPortfolio(Portfolio portfolio)
        {
            /*
            Обязательные поля в портфеле
            1.Number – по сути это название портфеля.На Moex это очень длинный номер, а вернее их почти всегда N.И на разных номерах счетов лежат разное кол-во денег и возможны разные валюты.В крипте – это обычно просто название коннектора + «Portfolio». 
            2.ValueBegin – актуально для Moex.По сути атавизм, однако сохраняемый нами, т.к.на MOEX существует единый брокерский рублёвый счёт.В коннекторах по Крипте здесь должна быть единица.
            3.ValueCurrent – тоже самое что и предыдущий пункт
            4.Profit – тоже самое что и предыдущий пункт. Для крипты не актуально.
            5.List < PositionOnBoard > GetPositionOnBoard() – место хранения позиций.И активов, в случае если это не MOEX.
            */

            if(string.IsNullOrEmpty(portfolio.Number))
            {
                SetNewError("Error 26. Number portfolio is null");
                return;
            }

            if (portfolio.ValueBegin == 0)
            {
                SetNewError("Error 27. ValueBegin of portfolio is zero");
                return;
            }

            List<PositionOnBoard> positions = portfolio.GetPositionOnBoard();

            for(int i = 0;i < positions.Count;i++)
            {
                CheckPositionOnBoard(positions[i]);
            }
        }

        private void CheckPositionOnBoard(PositionOnBoard position)
        {
            /*
            1.Отображаются бумаги со СПОТА
            2.Отображаются бумаги в портфеле по ФЬЮЧЕРСАМ
            3.Шортовые позиции – отмечаются минусом в PositionOnBoard
            4.Когда позиция закрывается – должен измениться VolumeNow на 0
            5.Когда по позиции открыты ордера – должен измениться VolumeBlock
            */

            if(string.IsNullOrEmpty(position.SecurityNameCode) == true)
            {
                SetNewError("Error 28. Name security position on board is null");
                return;
            }
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

        Side _waitSide;

        private void ClearOrders()
        {
            _ordersActive.Clear();
            _ordersCancel.Clear();
            _ordersDone.Clear();
            _ordersFail.Clear();
            _ordersPartial.Clear();
            _ordersPending.Clear();
            _myTrades.Clear();
        }

        private void Server_NewOrderIncomeEvent(Order order)
        {
            if (order.State == OrderStateType.None)
            {
                this.SetNewError("Error 29. Order with state NONE");
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
            8.Volume – объём ордера
            9.State – статус ордера
            10.TimeCallBack, TimeCreate – забиваем при всех откликах от сервера по ордеру
            11.TimeDone – время сервера когда ордер получил статус Done
            12.TimeCancel – время сервера когда ордер получил статус Cancel
            */

            if (order.Side != _waitSide)
            {
                this.SetNewError("Error 30. Wait side note equal. Wait: " + _waitSide
                    + " Side in order: " + order.Side);
                return false;
            }

            if (order.TypeOrder != OrderPriceType.Limit)
            {
                this.SetNewError("Error 31. Order Type is note LIMIT. Real type: " + order.TypeOrder);
                return false;
            }

            if (order.TimeCallBack == DateTime.MinValue)
            {
                this.SetNewError("Error 32. TimeCallBack is MinValue");
                return false;
            }

            DateTime now = DateTime.Now;

            if (order.TimeCallBack.AddDays(-1) > now)
            {
                this.SetNewError("Error 33. Order. TimeCallBack is to big. Time: " + order.TimeCallBack.ToString());
                return false;
            }

            if (order.TimeCallBack.AddDays(1) < now)
            {
                this.SetNewError("Error 34. Order. TimeCallBack is to small. Time: " + order.TimeCallBack.ToString());
                return false;
            }

            if(order.State == OrderStateType.Done)
            {
                if (order.TimeDone == DateTime.MinValue)
                {
                    this.SetNewError("Error 35. Order Done, TimeDone is MinValue");
                    return false;
                }
                if (order.TimeDone.AddDays(-1) > now)
                {
                    this.SetNewError("Error 36. Order. TimeDone is to big. Time: " + order.TimeDone.ToString());
                    return false;
                }

                if (order.TimeDone.AddDays(1) < now)
                {
                    this.SetNewError("Error 37. Order. TimeDone is to small. Time: " + order.TimeDone.ToString());
                    return false;
                }
            }

            if(order.State == OrderStateType.Cancel)
            {
                if (order.TimeCancel == DateTime.MinValue)
                {
                    this.SetNewError("Error 38. Order Cancel, buy TimeCancel is MinValue");
                    return false;
                }
                if (order.TimeCancel.AddDays(-1) > now)
                {
                    this.SetNewError("Error 39. Order. TimeCancel is to big. Time: " + order.TimeCancel.ToString());
                    return false;
                }

                if (order.TimeCancel.AddDays(1) < now)
                {
                    this.SetNewError("Error 40. Order. TimeCancel is to small. Time: " + order.TimeCancel.ToString());
                    return false;
                }
            }

            if (order.NumberUser == 0)
            {
                this.SetNewError("Error 41. NumberUser is zero");
                return false;
            }

            if (order.State != OrderStateType.Fail
                && string.IsNullOrEmpty(order.NumberMarket))
            {
                this.SetNewError("Error 42. NumberMarket is null or empty");
                return false;
            }

            if (string.IsNullOrEmpty(order.SecurityNameCode))
            {
                this.SetNewError("Error 43. SecurityNameCode is null or empty");
                return false;
            }

            if (string.IsNullOrEmpty(order.PortfolioNumber))
            {
                this.SetNewError("Error 44. PortfolioNumber is null or empty");
                return false;
            }

            if (order.Side == Side.None)
            {
                this.SetNewError("Error 45. Side is None");
                return false;
            }

            if (order.State != OrderStateType.Fail
                && order.Price <= 0)
            {
                this.SetNewError("Error 46. Price is zero");
                return false;
            }

            if (order.State != OrderStateType.Fail &&
                order.State != OrderStateType.Cancel &&
                order.Volume <= 0)
            {
                this.SetNewError("Error 47. Volume is zero");
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

            if (myTrade.Side != _waitSide)
            {
                this.SetNewError("Error 48. MyTrade. Wait side note equal. Wait: " + _waitSide
                  + " Side in order: " + myTrade.Side);
                return false;
            }

            if (myTrade.Volume <= 0)
            {
                this.SetNewError("Error 49. MyTrade. Volume is zero");
                return false;
            }

            if (myTrade.Price <= 0)
            {
                this.SetNewError("Error 50. MyTrade. Price is zero");
                return false;
            }

            if (string.IsNullOrEmpty(myTrade.SecurityNameCode))
            {
                this.SetNewError("Error 51. MyTrade. SecurityNameCode is null or empty");
                return false;
            }

            if (string.IsNullOrEmpty(myTrade.NumberOrderParent))
            {
                this.SetNewError("Error 52. MyTrade. NumberOrderParent is null or empty");
                return false;
            }

            if (string.IsNullOrEmpty(myTrade.NumberTrade))
            {
                this.SetNewError("Error 53. MyTrade. NumberTrade is null or empty");
                return false;
            }

            if (myTrade.Time == DateTime.MinValue)
            {
                this.SetNewError("Error 54. MyTrade. Time is min value");
                return false;
            }

            DateTime now = DateTime.Now;

            if (myTrade.Time.AddDays(-1) > now)
            {
                this.SetNewError("Error 55. MyTrade. Time is to big. Time: " + myTrade.Time.ToString());
                return false;
            }

            if (myTrade.Time.AddDays(1) < now)
            {
                this.SetNewError("Error 56. MyTrade. Time is to small. Time: " + myTrade.Time.ToString());
                return false;
            }

            return true;
        }
    }
}