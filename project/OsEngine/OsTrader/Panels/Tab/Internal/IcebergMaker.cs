/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System;
using System.Collections.Generic;
using System.Linq;
using OsEngine.Entity;
using System.Threading;
using OsEngine.Market;

namespace OsEngine.OsTrader.Panels.Tab.Internal
{
    public class IcebergMaker
    {

        /// <summary>
        /// All icebergs
        /// </summary>
        private List<Iceberg> _icebergOrders;

        /// <summary>
        /// Make a new iceberg
        /// </summary>
        /// <param name="price">price</param>
        /// <param name="lifeTime">life time</param>
        /// <param name="ordersCount">orders count</param>
        /// <param name="position">position</param>
        /// <param name="type">type</param>
        /// <param name="volume">volume</param>
        /// <param name="bot">bot</param>
        public void MakeNewIceberg(decimal price, TimeSpan lifeTime, int ordersCount, 
            Position position, IcebergType type, decimal volume, BotTabSimple bot, OrderPriceType priceType, int minMillisecondsDistanceMarketOrders)
        {
            if (_icebergOrders == null)
            {
                _icebergOrders = new List<Iceberg>();
            }

            Iceberg newIceberg  = new Iceberg(price, lifeTime, ordersCount, position, 
                bot, type, volume, priceType, minMillisecondsDistanceMarketOrders);

            newIceberg.NewOrderNeedToCancel += newIceberg_newOrderNeedToCancel;
            newIceberg.NewOrderNeedToExecute += newIceberg_newOrderNeedToExecute;
            _icebergOrders.Add(newIceberg);

        }

        /// <summary>
        /// You must execute this order event
        /// </summary>
        private void newIceberg_newOrderNeedToExecute(Order order)
        {
            if (NewOrderNeedToExecute != null)
            {
                NewOrderNeedToExecute(order);
            }
        }

        /// <summary>
        /// It is necessary to withdraw the order
        /// </summary>
        private void newIceberg_newOrderNeedToCancel(Order order)
        {
            if (NewOrderNeedToCancel != null)
            {
                NewOrderNeedToCancel(order);
            }
        }

        /// <summary>
        /// Incoming order
        /// </summary>
        public void SetNewOrder(Order order)
        {
            if (_icebergOrders == null)
            {
                return;
            }

            for (int i = 0; i < _icebergOrders.Count; i++)
            {
                _icebergOrders[i].SetNewOrder(order);
            }
        }

        /// <summary>
        /// Clear icebergs
        /// </summary>
        public void ClearIcebergs()
        {
            List<Iceberg> toClose = _icebergOrders;
            _icebergOrders = new List<Iceberg>();

            if (toClose == null ||
                toClose.Count == 0)
            {
               return;
            }

            for (int i = 0; i < toClose.Count; i++)
            {
                toClose[i].CloseAllOrder();
                toClose[i].Delete();
                toClose[i].NewOrderNeedToCancel -= newIceberg_newOrderNeedToCancel;
                toClose[i].NewOrderNeedToExecute -= newIceberg_newOrderNeedToExecute;
            }
            _icebergOrders = new List<Iceberg>();
        }

        /// <summary>
        /// Order must be executed event
        /// </summary>
        public event Action<Order> NewOrderNeedToExecute;

        /// <summary>
        /// Order must be withdraw event
        /// </summary>
        public event Action<Order> NewOrderNeedToCancel;

    }

    /// <summary>
    /// Iceberg
    /// </summary>
    public class Iceberg
    {
        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="price">price</param>
        /// <param name="lifeTime">life time</param>
        /// <param name="ordersCount">orders count</param>
        /// <param name="position">position</param>
        /// <param name="bot">robot</param>
        /// <param name="type">iceberg type</param>
        /// <param name="volume">sum volume</param>
        public Iceberg(decimal price, TimeSpan lifeTime, int ordersCount, 
            Position position, BotTabSimple bot, IcebergType type, 
            decimal volume, OrderPriceType priceType, int minMillisecondsDistanceMarketOrders)
        {        
            _bot = bot;
            _price = price;
            _lifeTime = lifeTime;
            _position = position;
            _ordersCount = ordersCount;
            _type = type;
            _volume = volume;
            _priceType = priceType;
            _minMillisecondsDistanceMarketOrders = minMillisecondsDistanceMarketOrders;

            if (type == IcebergType.Open)
            {
                CreateOpenOrders();
            }
            else if (type == IcebergType.Close)
            {
                CreateCloseOrders();
            }
            else if (type == IcebergType.ModifyBuy)
            {
                CreateModifyOrdersBuy();
            }
            else if (type == IcebergType.ModifySell)
            {
                CreateModifyOrdersSell();
            }

            Thread worker = new Thread(WorkerPlace);
            worker.Start();
        }

        private OrderPriceType _priceType;

        /// <summary>
        /// Life time
        private TimeSpan _lifeTime;

        /// <summary>
        /// Minimum time interval between orders in milliseconds
        /// </summary>
        private int _minMillisecondsDistanceMarketOrders;

        /// <summary>
        /// Position
        /// </summary>
        private Position _position;

        /// <summary>
        /// Price
        /// </summary>
        private decimal _price;

        /// <summary>
        /// Orders count
        /// </summary>
        private int _ordersCount;

        /// <summary>
        /// Sum volume
        /// </summary>
        private decimal _volume;

        /// <summary>
        /// Robot
        /// </summary>
        private BotTabSimple _bot;

        /// <summary>
        /// Order placed in the system
        /// </summary>
        private Order _ordersInSystem;

        private OrderStateType _lastOrderState;

        /// <summary>
        /// Orders that must be placed
        /// </summary>
        private List<Order> _ordersNeedToCreate;

        /// <summary>
        /// Iceberg type
        /// </summary>
        private IcebergType _type;

        /// <summary>
        /// Create an array of orders for iceberg
        /// </summary>
        private void CreateOpenOrders()
        {
            Side side = _position.Direction;

            List<decimal> volumes = GetVolumesArray(_volume, _ordersCount);

            List<Order> orders = new List<Order>();

            for (int i = 0; i < volumes.Count; i++)
            {
                orders.Add(new Order());
                orders[i].Side = side;
                orders[i].LifeTime = _lifeTime;
                orders[i].Volume = volumes[i];
                orders[i].Price = _price;
                orders[i].PortfolioNumber = _bot.Portfolio.Number;
                orders[i].SecurityNameCode = _bot.Security.Name;
                orders[i].SecurityClassCode = _bot.Security.NameClass;
                orders[i].OrderTypeTime = _bot.ManualPositionSupport.OrderTypeTime;
                orders[i].NumberUser = NumberGen.GetNumberOrder(_bot.StartProgram);
                orders[i].TypeOrder = _priceType;
              }

            _ordersNeedToCreate = orders;
        }

        /// <summary>
        /// Create an array of orders for iceberg
        /// </summary>
        private void CreateCloseOrders()
        {
            Side side;

            if (_position.Direction == Side.Buy)
            {
                 side = Side.Sell;
            }
            else
            {
                side = Side.Buy;
            }

            List<decimal> volumes = GetVolumesArray(_volume, _ordersCount);

            List<Order> orders = new List<Order>();

            for (int i = 0; i < volumes.Count; i++)
            {
                orders.Add(new Order());
                orders[i].Side = side;
                orders[i].LifeTime = _lifeTime;
                orders[i].Volume = volumes[i];
                orders[i].Price = _price;
                orders[i].PortfolioNumber = _bot.Portfolio.Number;
                orders[i].SecurityNameCode = _bot.Security.Name;
                orders[i].SecurityClassCode = _bot.Security.NameClass;
                orders[i].OrderTypeTime = _bot.ManualPositionSupport.OrderTypeTime;
                orders[i].NumberUser = NumberGen.GetNumberOrder(_bot.StartProgram);
                orders[i].TypeOrder = _priceType;
            }

            _ordersNeedToCreate = orders;

        }

        /// <summary>
        /// Create an array of orders for iceberg
        /// </summary>
        private void CreateModifyOrdersBuy()
        {
            Side side = Side.Buy;

            List<decimal> volumes = GetVolumesArray(_volume, _ordersCount);

            List<Order> orders = new List<Order>();

            for (int i = 0; i < volumes.Count; i++)
            {
                orders.Add(new Order());
                orders[i].Side = side;
                orders[i].LifeTime = _lifeTime;
                orders[i].Volume = volumes[i];
                orders[i].Price = _price;
                orders[i].PortfolioNumber = _bot.Portfolio.Number;
                orders[i].SecurityNameCode = _bot.Security.Name;
                orders[i].SecurityClassCode = _bot.Security.NameClass;
                orders[i].OrderTypeTime = _bot.ManualPositionSupport.OrderTypeTime;
                orders[i].NumberUser = NumberGen.GetNumberOrder(_bot.StartProgram);
                orders[i].TypeOrder = _priceType;
            }

            _ordersNeedToCreate = orders;
        }

        /// <summary>
        /// Create an array of orders for iceberg
        /// </summary>
        private void CreateModifyOrdersSell()
        {
            Side side = Side.Sell;

            List<decimal> volumes = GetVolumesArray(_volume, _ordersCount);

            List<Order> orders = new List<Order>();

            for (int i = 0; i < volumes.Count; i++)
            {
                orders.Add(new Order());
                orders[i].Side = side;
                orders[i].LifeTime = _lifeTime;
                orders[i].Volume = volumes[i];
                orders[i].Price = _price;
                orders[i].PortfolioNumber = _bot.Portfolio.Number;
                orders[i].SecurityNameCode = _bot.Security.Name;
                orders[i].SecurityClassCode = _bot.Security.NameClass;
                orders[i].OrderTypeTime = _bot.ManualPositionSupport.OrderTypeTime;
                orders[i].NumberUser = NumberGen.GetNumberOrder(_bot.StartProgram);
                orders[i].TypeOrder = _priceType;
            }

            _ordersNeedToCreate = orders;
        }

        private List<decimal> GetVolumesArray(decimal volume, decimal ordersCount)
        {

            // 1 бьём объём на равные части
            List<decimal> volumes = new List<decimal>();
            decimal allVolumeInArray = 0;

            for (int i = 0; i < ordersCount; i++)
            {
                decimal curVolume = volume / ordersCount;
                curVolume = Math.Round(curVolume, _bot.Security.DecimalsVolume);

                if(curVolume > 0)
                {
                    allVolumeInArray += curVolume;
                    volumes.Add(curVolume);
                }
            }

            // 2 если после разделения на части и обрезаний итоговый объём изменился, добавляем его в первую ячейку

            if (allVolumeInArray != volume)
            {
                decimal residue = volume - allVolumeInArray;

                if(volumes.Count == 0)
                {
                    volumes.Add(0);
                }

                volumes[0] = Math.Round(volumes[0] + residue, _bot.Security.DecimalsVolume);
            }

            // 3 проверяем чтобы объёмы были выше минимальных 

            for(int i = 0;i < volumes.Count;i++)
            {
                if(volumes.Count == 1)
                {
                    break;
                }

                if(i + 1 == volumes.Count)
                {
                    if (_bot.CanTradeThisVolume(volumes[i]) == false)
                    {
                        volumes[i - 1] += volumes[i];
                        volumes.RemoveAt(i);
                    }

                    break;
                }

                if (_bot.CanTradeThisVolume(volumes[i]) == false)
                {
                    volumes[i + 1] += volumes[i];
                    volumes.RemoveAt(i);
                    i--;
                }
            }

            return volumes;
        }

        /// <summary>
        /// Add order to iceberg
        /// </summary>
        public void SetNewOrder(Order order)
        {
            if (_ordersInSystem == null)
            {
                return;
            }

            
           /*ServerMaster.Log.ProcessMessage(
                "new order state " + order.State
                + "\nnum user " + order.NumberUser
                + "\nnum market " + order.NumberMarket, Logging.LogMessageType.Error);*/
            
            if (_ordersInSystem.NumberUser == order.NumberUser)
            {
                _ordersInSystem.State = order.State;

                if(_lastOrderState != OrderStateType.Done)
                {
                    _lastOrderState = order.State;
                }
            }
        }

        /// <summary>
        /// Withdraw all orders
        /// </summary>
        public void CloseAllOrder()
        {
            if (_ordersNeedToCreate != null)
            {
                _ordersNeedToCreate = new List<Order>();
            }

            if (_ordersInSystem != null)
            {
                if (NewOrderNeedToCancel != null &&
                    _ordersInSystem.State == OrderStateType.Active)
                {
                    NewOrderNeedToCancel(_ordersInSystem);
                    _ordersInSystem = null;
                }
            }
        }

        public void Delete()
        {
            _bot = null;
            _ordersInSystem = null;
            _ordersNeedToCreate = null;
            _position = null;
        }

        private void WorkerPlace()
        {
            while(true)
            {
                try
                {
                    Thread.Sleep(50);
                    Check();

                    if (_ordersNeedToCreate == null ||
                        _ordersNeedToCreate.Count == 0)
                    {
                        return;
                    }
                }
                catch(Exception e)
                {
                    ServerMaster.SendNewLogMessage("Iceberg internal error!!! " + e.ToString(), Logging.LogMessageType.Error);
                    return;
                }
            }
        }

        /// <summary>
        /// Check whether it is time to send a new order
        /// </summary>
        public void Check()
        {
            if (_ordersNeedToCreate == null ||
                _ordersNeedToCreate.Count == 0)
            {
                return;
            }

            if (_ordersInSystem == null ||
               _lastOrderState == OrderStateType.Done)
            {
                _ordersInSystem = _ordersNeedToCreate[0];
                

                if (_ordersInSystem.TypeOrder == OrderPriceType.Market 
                    && _lastSendOrderTime != DateTime.MinValue &&
                    _minMillisecondsDistanceMarketOrders != 0)
                {
                    if(_lastSendOrderTime.AddMilliseconds(_minMillisecondsDistanceMarketOrders) > DateTime.Now)
                    {
                        return;
                    }
                }

                _lastOrderState = OrderStateType.None;

                _ordersNeedToCreate.RemoveAt(0);

                if (_type == IcebergType.Open)
                {
                    _position.AddNewOpenOrder(_ordersInSystem);
                }
                else if(_type == IcebergType.Close)
                {
                    _position.AddNewCloseOrder(_ordersInSystem);
                }
                else if (_type == IcebergType.ModifyBuy)
                {
                    if ((_position.Direction == Side.Buy && _ordersInSystem.Side == Side.Buy)
                        ||
                        (_position.Direction == Side.Sell && _ordersInSystem.Side == Side.Sell))
                    {
                        _position.AddNewOpenOrder(_ordersInSystem);
                    }
                    else
                    {
                        _position.AddNewCloseOrder(_ordersInSystem);
                    }
                }
                else if (_type == IcebergType.ModifySell)
                {
                    if ((_position.Direction == Side.Buy && _ordersInSystem.Side == Side.Buy)
                        ||
                        (_position.Direction == Side.Sell && _ordersInSystem.Side == Side.Sell))
                    {
                        _position.AddNewOpenOrder(_ordersInSystem);
                    }
                    else
                    {
                        _position.AddNewCloseOrder(_ordersInSystem);
                    }
                }

                if (NewOrderNeedToExecute != null)
                {
                    NewOrderNeedToExecute(_ordersInSystem);
                }

                _lastSendOrderTime = DateTime.Now;
            }
        }

        private DateTime _lastSendOrderTime;

        /// <summary>
        /// Order must be executed
        /// </summary>
        public event Action<Order> NewOrderNeedToExecute;

        /// <summary>
        /// Order must be withdraw
        /// </summary>
        public event Action<Order> NewOrderNeedToCancel;

    }

    /// <summary>
    /// Iceberg type
    /// </summary>
    public enum IcebergType
    {
        /// <summary>
        /// To open
        /// </summary>
        Open,

        /// <summary>
        /// To close
        /// </summary>
        Close,

        /// <summary>
        /// Modification by buy
        /// </summary>
        ModifyBuy,

        /// <summary>
        /// Modification by sale
        /// </summary>
        ModifySell,
    }
}