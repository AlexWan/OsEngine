/*
 *Your rights to use the code are governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 *Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;
using OsEngine.Entity;

namespace OsEngine.Market.Connectors
{

    /// <summary>
    /// emulator execution of exchange orders. It's used in real trading to emulate trading
    /// Эмулятор исполнения заявок биржи. Используется при реальных торгах, для эмуляции торговли 
    /// </summary>
    public class OrderExecutionEmulator
    {
        private static List<OrderExecutionEmulator> _emulators = new List<OrderExecutionEmulator>();
        
        private static void Listen(OrderExecutionEmulator emulator)
        {
            _emulators.Add(emulator);

            if (_emulators.Count == 1)
            {
                Task task = new Task(WatcherThread);
                task.Start();
            }
        }

        private static async void WatcherThread()
        {
            while(true)
            {
               await Task.Delay(250);

                if (MainWindow.ProccesIsWorked == false)
                {
                    return;
                }

                for (int i = 0; i < _emulators.Count; i++)
                {
                    if (_emulators[i] == null)
                    {
                        continue;
                    }
                    _emulators[i].CheckOrders();
                }
            }
        }

        public OrderExecutionEmulator()
        {
            ordersOnBoard = new List<Order>();
            Listen(this);
        }

// order management
// менеджмент ордеров

        /// <summary>
        /// place order to the exchange
        /// выставить ордер на биржу
        /// </summary>
        public void OrderExecute(Order order) 
        {
            order.SecurityNameCode = "TestPaper";
            order.PortfolioNumber = "TestPortfolio";
  
            ActivateSimple(order);
            ordersOnBoard.Add(order);
            CheckExecution(true, order);
        }

        /// <summary>
        /// cancel order from the exchange
        /// отозвать ордер с биржи
        /// </summary>
        public void OrderCancel(Order order)
        {
            ordersOnBoard.Remove(order);

            Order newOrder = new Order();
            newOrder.NumberMarket = order.NumberMarket;
            newOrder.NumberUser = order.NumberUser;
            newOrder.State = OrderStateType.Cancel;
            newOrder.Volume = order.Volume;
            newOrder.VolumeExecute = order.VolumeExecute;
            newOrder.Price = order.Price;
            newOrder.TypeOrder = order.TypeOrder;
            newOrder.TimeCallBack = _serverTime;

            _ordersToSend.Enqueue(newOrder);
        }

        private ConcurrentQueue<Order> _ordersToSend = new ConcurrentQueue<Order>();

        private ConcurrentQueue<MyTrade> _myTradesToSend = new ConcurrentQueue<MyTrade>();

        private void CheckOrders()
        {
            while (_ordersToSend.IsEmpty == false)
            {
                Order order;

                _ordersToSend.TryDequeue(out order);

                if (order == null)
                {
                    continue;
                }

                if (OrderChangeEvent != null)
                {
                    OrderChangeEvent(order);
                }
            }

            while (_myTradesToSend.IsEmpty == false)
            {
                MyTrade trade;

                _myTradesToSend.TryDequeue(out trade);

                if (trade == null)
                {
                    continue;
                }

                if (MyTradeEvent != null)
                {
                    MyTradeEvent(trade);
                }
            }
        }

        /// <summary>
        /// my orders placed on the exchange
        /// мои ордера выставленные на бирже
        /// </summary>
        private List<Order> ordersOnBoard;

        /// <summary>
        /// check whether any orders are executed 
        /// проверить исполнился ли какой-нибудь ордер
        /// </summary>
        /// <param name="isFirstTime"> if value == true then we check the execution immediately after placing / когда проверяем ордер. если true, то проверяем исполнение сразу после выставления </param>
        /// <param name="order"> execution order / проверяемый на исполнение ордер </param>
        private bool CheckExecution(bool isFirstTime, Order order)
        {
            if (order.NumberMarket == "")
            {
                return false;
            }

            if (order.TypeOrder == OrderPriceType.Market)
            {
                if (order.Side == Side.Buy)
                {
                    decimal price = _bestSell;

                    ExecuteSimple(order, price);
                    ordersOnBoard.Remove(order);
                    return true;
                }
                else if (order.Side == Side.Sell )
                {
                    decimal price = _bestBuy;
                    
                    ExecuteSimple(order, price);
                    ordersOnBoard.Remove(order);
                    return true;
                }
            }
            else //if (order.TypeOrder == OrderPriceType.Limit)
            {
                if (order.Side == Side.Buy &&
              order.Price >= _bestSell)
                {
                    decimal price;

                    if (isFirstTime)
                    {
                        price = _bestSell;
                    }
                    else
                    {
                        price = order.Price;
                    }

                    ExecuteSimple(order, price);
                    ordersOnBoard.Remove(order);
                    return true;
                }
                else if (order.Side == Side.Sell &&
                         order.Price <= _bestBuy)
                {
                    decimal price;

                    if (isFirstTime)
                    {
                        price = _bestBuy;
                    }
                    else
                    {
                        price = order.Price;
                    }
                    ExecuteSimple(order, price);
                    ordersOnBoard.Remove(order);
                    return true;
                }
            }
          
            return false;
        }

        /// <summary>
        /// send up the order execution notification on exchange 
        /// выслать наверх оповещение об исполнении ордера на бирже
        /// </summary>
        /// <param name="order"> order / ордер </param>
        /// <param name="price"> execution price / цена исполнения </param>
        private void ExecuteSimple(Order order, decimal price)
        {
            Order newOrder = new Order();
            newOrder.NumberMarket = order.NumberMarket;
            newOrder.NumberUser = order.NumberUser;
            newOrder.State = OrderStateType.Done;
            newOrder.Volume = order.Volume;
            newOrder.VolumeExecute = order.Volume;
            newOrder.Price = order.Price;
            newOrder.TimeCallBack = _serverTime;
            newOrder.Side = order.Side;
            newOrder.SecurityNameCode = order.SecurityNameCode;

            _ordersToSend.Enqueue(newOrder);

            MyTrade trade = new MyTrade();
            trade.Volume = order.Volume;
            trade.Time = _serverTime;
            trade.Price = price;
            trade.SecurityNameCode = order.SecurityNameCode;
            trade.NumberTrade = "emu" + order.NumberMarket;
            trade.Side = order.Side;
            trade.NumberOrderParent = newOrder.NumberMarket;

            _myTradesToSend.Enqueue(trade);

        }

        /// <summary>
        /// send up the order execution notification on exchange
        /// выслать наверх оповещение о выставлении ордера на биржу
        /// </summary>
        /// <param name="order"> order / ордер </param>
        private void ActivateSimple(Order order)
        {
            Order newOrder = new Order();
            newOrder.NumberMarket = DateTime.Now.ToString(new CultureInfo("ru-RU")) + order.NumberUser;
            order.NumberMarket = newOrder.NumberMarket;
            newOrder.NumberUser = order.NumberUser;
            newOrder.State = OrderStateType.Activ;
            newOrder.Volume = order.Volume;
            newOrder.VolumeExecute = 0;
            newOrder.Price = order.Price;
            newOrder.TimeCallBack = _serverTime;
            newOrder.Side = order.Side;
            newOrder.SecurityNameCode = order.SecurityNameCode;

            if (OrderChangeEvent != null)
            {
                OrderChangeEvent(newOrder);
            }
        }

// server needs to be loaded with new data to execute stop- and profit-orders
// сервер нужно прогружать новыми данными, чтобы исполнялись стопы и профиты

        /// <summary>
        /// buy price
        /// цена покупки
        /// </summary>
        private decimal _bestBuy;

        /// <summary>
        /// sell price
        /// цена продажи
        /// </summary>
        private decimal _bestSell;

        /// <summary>
        /// server time
        /// время сервера
        /// </summary>
        private DateTime _serverTime;

        /// <summary>
        /// get new last prices
        /// провести новые последние цены
        /// </summary>
        /// <param name="sell"> best sell price / лучшая цена продажи </param>
        /// <param name="buy"> best buy price / лучшая цена покупки </param>
        /// <param name="time"> time / время </param>
        public void ProcessBidAsc(decimal sell, decimal buy, DateTime time)
        {
            if (buy > sell)
            {
                _bestBuy = sell;
                _bestSell = buy;
            }
            else
            {
                _bestBuy = buy;
                _bestSell = sell;
            }

            _serverTime = time;

            for (int i = 0;ordersOnBoard != null && i < ordersOnBoard.Count; i++)
            {
                if (CheckExecution(false, ordersOnBoard[i]))
                {
                    i--;
                }
            }
        }

        /// <summary>
        /// my trades are changed
        /// изменились мои сделки
        /// </summary>
        public event Action<MyTrade> MyTradeEvent;

        /// <summary>
        /// orders are changed
        /// изменились ордера
        /// </summary>
        public event Action<Order> OrderChangeEvent;
    }
}
