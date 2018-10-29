/*
 *Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System;
using System.Collections.Generic;
using System.Globalization;
using OsEngine.Entity;

namespace OsEngine.Market.Connectors
{

    /// <summary>
    /// Эмулятор исполнения заявок биржи. Используется при реальных торгах, для эмуляции торговли 
    /// </summary>
    public class OrderExecutionEmulator
    {
        public OrderExecutionEmulator()
        {
            ordersOnBoard = new List<Order>();
        }

// менеджмент ордеров

        /// <summary>
        /// выставить ордер на биржу
        /// </summary>
        public void OrderExecute(Order order) 
        {
            order.SecurityNameCode = "TestPaper";
            order.PortfolioNumber = "TestPortfolio";
            ordersOnBoard.Add(order);

            ActivateSimple(order);
            CheckExecution(true, order);
        }

        /// <summary>
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

            if (OrderChangeEvent != null)
            {
                OrderChangeEvent(newOrder);
            }
        }

        /// <summary>
        /// мои ордера выставленные на бирже
        /// </summary>
        private List<Order> ordersOnBoard;

        /// <summary>
        /// проверить исполнился ли какой-нибудь ордер
        /// </summary>
        /// <param name="isFirstTime">когда проверяем ордер. если true, то проверяем исполнение сразу после выставления</param>
        /// <param name="order">проверяемый на исполнение ордер</param>
        private bool CheckExecution(bool isFirstTime, Order order)
        {
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
              order.Price > _bestSell)
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
                         order.Price < _bestBuy)
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
        /// выслать наверх оповещение об исполнении ордера на бирже
        /// </summary>
        /// <param name="order">ордер</param>
        /// <param name="price">цена исполнения</param>
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

            if (OrderChangeEvent != null)
            {
                OrderChangeEvent(newOrder);
            }

            MyTrade trade = new MyTrade();
            trade.Volume = order.Volume;
            trade.Time = _serverTime;
            trade.Price = price;
            trade.SecurityNameCode = order.SecurityNameCode;
            trade.NumberTrade = "emu" + order.NumberMarket;
            trade.Side = order.Side;
            trade.NumberOrderParent = newOrder.NumberMarket;

            if (MyTradeEvent != null)
            {
                MyTradeEvent(trade);
            }

        }

        /// <summary>
        /// выслать наверх оповещение о выставлении ордера на биржу
        /// </summary>
        /// <param name="order">ордер</param>
        private void ActivateSimple(Order order)
        {
            Order newOrder = new Order();
            newOrder.NumberMarket = DateTime.Now.ToString(new CultureInfo("ru-RU")) + order.NumberUser;
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

// сервер нужно прогружать новыми данными, чтобы исполнялись стопы и профиты

        /// <summary>
        /// цена покупки
        /// </summary>
        private decimal _bestBuy;

        /// <summary>
        /// цена продажи
        /// </summary>
        private decimal _bestSell;

        /// <summary>
        /// время сервера
        /// </summary>
        private DateTime _serverTime;

        /// <summary>
        /// провести новые последние цены
        /// </summary>
        /// <param name="sell">лучшая цена продажи</param>
        /// <param name="buy">лучшая цена покупки</param>
        /// <param name="time">время</param>
        public void ProcessBidAsc(decimal sell, decimal buy, DateTime time)
        {
            _bestBuy = buy;
            _bestSell = sell;
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
        /// изменились мои сделки
        /// </summary>
        public event Action<MyTrade> MyTradeEvent;

        /// <summary>
        /// изменились ордера
        /// </summary>
        public event Action<Order> OrderChangeEvent;
    }
}
