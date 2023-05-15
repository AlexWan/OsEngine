/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System;
using System.Collections.Generic;
using System.Linq;
using OsEngine.Entity;

namespace OsEngine.OsTrader.Panels.Tab.Internal
{

    /// <summary>
    /// Class to create iceberg
    /// </summary>
    public class AcebergMaker
    {

        /// <summary>
        /// All icebergs
        /// </summary>
        private List<Aceberg> _acebergOrders;

        /// <summary>
        /// Make a new iceberg
        /// </summary>
        /// <param name="price">price</param>
        /// <param name="lifiTime">life time</param>
        /// <param name="ordersCount">orders count</param>
        /// <param name="position">position</param>
        /// <param name="type">type</param>
        /// <param name="volume">volume</param>
        /// <param name="bot">bot</param>
        public void MakeNewAceberg(decimal price, TimeSpan lifiTime, int ordersCount, Position position, AcebergType type, decimal volume, BotTabSimple bot)
        {
            if (_acebergOrders == null)
            {
                _acebergOrders = new List<Aceberg>();
            }

            Aceberg newAceberg = new Aceberg(price, lifiTime, ordersCount, position, bot,type,volume);

            newAceberg.NewOrderNeadToCansel += newAceberg_newOrderNeadToCansel;
            newAceberg.NewOrderNeadToExecute += newAceberg_newOrderNeadToExecute;

            _acebergOrders.Add(newAceberg);
            newAceberg.Check();
        }

        /// <summary>
        /// You must execute this order event
        /// </summary>
        private void newAceberg_newOrderNeadToExecute(Order order)
        {
            if (NewOrderNeadToExecute != null)
            {
                NewOrderNeadToExecute(order);
            }
        }

        /// <summary>
        /// It is necessary to withdraw the order
        /// </summary>
        private void newAceberg_newOrderNeadToCansel(Order order)
        {
            if (NewOrderNeadToCansel != null)
            {
                NewOrderNeadToCansel(order);
            }
        }

        /// <summary>
        /// Incoming order
        /// </summary>
        public void SetNewOrder(Order order)
        {
            if (_acebergOrders == null)
            {
                return;
            }

            for (int i = 0; i < _acebergOrders.Count; i++)
            {
                _acebergOrders[i].SetNewOrder(order);
                _acebergOrders[i].Check();
            }
        }

        /// <summary>
        /// Clear icebergs
        /// </summary>
        public void ClearAcebergs()
        {
            List<Aceberg> toClose = _acebergOrders;
            _acebergOrders = new List<Aceberg>();

            if (toClose == null ||
                toClose.Count == 0)
            {
               return;
            }

            for (int i = 0; i < toClose.Count; i++)
            {
                toClose[i].CloseAllOrder();
                toClose[i].Delete();
                toClose[i].NewOrderNeadToCansel -= newAceberg_newOrderNeadToCansel;
                toClose[i].NewOrderNeadToExecute -= newAceberg_newOrderNeadToExecute;
            }
            _acebergOrders = new List<Aceberg>();
        }

        /// <summary>
        /// Order must be executed event
        /// </summary>
        public event Action<Order> NewOrderNeadToExecute;

        /// <summary>
        /// Order must be withdraw event
        /// </summary>
        public event Action<Order> NewOrderNeadToCansel;

    }

    /// <summary>
    /// Iceberg
    /// </summary>
    public class Aceberg
    {
        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="price">price</param>
        /// <param name="lifiTime">life time</param>
        /// <param name="ordersCount">orders count</param>
        /// <param name="position">position</param>
        /// <param name="bot">robot</param>
        /// <param name="type">iceberg type</param>
        /// <param name="volume">sum volume</param>
        public Aceberg(decimal price, TimeSpan lifiTime, int ordersCount, Position position, BotTabSimple bot, AcebergType type, decimal volume)
        {        
            _bot = bot;
            _price = price;
            _lifiTime = lifiTime;
            _position = position;
            _ordersCount = ordersCount;
            _type = type;
            _volume = volume;

            if (type == AcebergType.Open)
            {
                CreateOpenOrders();
            }
            else if (type == AcebergType.Close)
            {
                CreateCloseOrders();
            }
            else if (type == AcebergType.ModificateBuy)
            {
                CreateModificateOrdersBuy();
            }
            else if (type == AcebergType.ModificateSell)
            {
                CreateModificateOrdersSell();
            }
        }

        /// <summary>
        /// Life time
        private TimeSpan _lifiTime;

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

        /// <summary>
        /// Orders that must be placed
        /// </summary>
        private List<Order> _ordersNeadToCreate;

        /// <summary>
        /// Iceberg type
        /// </summary>
        private AcebergType _type;

        /// <summary>
        /// Create an array of orders for iceberg
        /// </summary>
        private void CreateOpenOrders()
        {
            Side side = _position.Direction;

            int realCountOrders = _ordersCount;

            if (realCountOrders > _volume)
            {
                realCountOrders = Convert.ToInt32(_volume);
            }

            Order[] orders = new Order[realCountOrders];

            for (int i = 0; i < orders.Length; i++)
            {
                orders[i] = new Order();
                orders[i].Side = side;
                orders[i].LifeTime = _lifiTime;
                orders[i].Volume = Convert.ToInt32(Math.Round(Convert.ToDecimal(_volume) / Convert.ToDecimal(realCountOrders)));
                orders[i].Price = _price;
                orders[i].PortfolioNumber = _bot.Portfolio.Number;
                orders[i].SecurityNameCode = _bot.Securiti.Name;
                orders[i].SecurityClassCode = _bot.Securiti.NameClass;
                orders[i].NumberUser = NumberGen.GetNumberOrder(_bot.StartProgram);
  
                if (i + 1 == orders.Length)
                {
                    decimal realVolume = 0;

                    for (int i2 = 0; i2 < orders.Length - 1; i2++)
                    {
                        realVolume += orders[i2].Volume;
                    }
                    orders[i].Volume = _volume - realVolume;
                }
            }

            _ordersNeadToCreate = orders.ToList();
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
           

            int realCountOrders = _ordersCount;

            if (realCountOrders > _volume)
            {
                realCountOrders = Convert.ToInt32(_volume);
            }

            Order[] orders = new Order[realCountOrders];

            for (int i = 0; i < orders.Length; i++)
            {
                orders[i] = new Order();
                orders[i].Side = side;
                orders[i].LifeTime = _lifiTime;
                orders[i].Volume = Convert.ToInt32(Math.Round(Convert.ToDecimal(_volume) / Convert.ToDecimal(realCountOrders)));
                orders[i].Price = _price;
                orders[i].PortfolioNumber = _bot.Portfolio.Number;
                orders[i].SecurityNameCode = _bot.Securiti.Name;
                orders[i].SecurityClassCode = _bot.Securiti.NameClass;
                orders[i].NumberUser = NumberGen.GetNumberOrder(_bot.StartProgram);

                if (i + 1 == orders.Length)
                {
                    decimal realVolume = 0;

                    for (int i2 = 0; i2 < orders.Length - 1; i2++)
                    {
                        realVolume += orders[i2].Volume;
                    }
                    orders[i].Volume = _volume - realVolume;
                }
            }

            _ordersNeadToCreate = orders.ToList();
        }

        /// <summary>
        /// Create an array of orders for iceberg
        /// </summary>
        private void CreateModificateOrdersBuy()
        {
            Side side = Side.Buy;

            int realCountOrders = _ordersCount;

            if (realCountOrders > _volume)
            {
                realCountOrders = Convert.ToInt32(_volume);
            }

            Order[] orders = new Order[realCountOrders];

            for (int i = 0; i < orders.Length; i++)
            {
                orders[i] = new Order();
                orders[i].Side = side;
                orders[i].LifeTime = _lifiTime;
                orders[i].Volume = Convert.ToInt32(Math.Round(Convert.ToDecimal(_volume) / Convert.ToDecimal(realCountOrders)));
                orders[i].Price = _price;
                orders[i].PortfolioNumber = _bot.Portfolio.Number;
                orders[i].SecurityNameCode = _bot.Securiti.Name;
                orders[i].NumberUser = NumberGen.GetNumberOrder(_bot.StartProgram);

                if (i + 1 == orders.Length)
                {
                    decimal realVolume = 0;

                    for (int i2 = 0; i2 < orders.Length - 1; i2++)
                    {
                        realVolume += orders[i2].Volume;
                    }
                    orders[i].Volume = _volume - realVolume;
                }
            }

            _ordersNeadToCreate = orders.ToList();
        }

        /// <summary>
        /// Create an array of orders for iceberg
        /// </summary>
        private void CreateModificateOrdersSell()
        {
            Side side = Side.Sell;

            int realCountOrders = _ordersCount;

            if (realCountOrders > _volume)
            {
                realCountOrders = Convert.ToInt32(_volume);
            }

            Order[] orders = new Order[realCountOrders];

            for (int i = 0; i < orders.Length; i++)
            {
                orders[i] = new Order();
                orders[i].Side = side;
                orders[i].LifeTime = _lifiTime;
                orders[i].Volume = Convert.ToInt32(Math.Round(Convert.ToDecimal(_volume) / Convert.ToDecimal(realCountOrders)));
                orders[i].Price = _price;
                orders[i].PortfolioNumber = _bot.Portfolio.Number;
                orders[i].SecurityNameCode = _bot.Securiti.Name;
                orders[i].NumberUser = NumberGen.GetNumberOrder(_bot.StartProgram);

                if (i + 1 == orders.Length)
                {
                    decimal realVolume = 0;

                    for (int i2 = 0; i2 < orders.Length - 1; i2++)
                    {
                        realVolume += orders[i2].Volume;
                    }
                    orders[i].Volume = _volume - realVolume;
                }
            }

            _ordersNeadToCreate = orders.ToList();
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

            if (_ordersInSystem.NumberMarket == order.NumberMarket)
            {
                _ordersInSystem = order;
            }
        }

        /// <summary>
        /// Withdraw all orders
        /// </summary>
        public void CloseAllOrder()
        {
            if (_ordersNeadToCreate != null)
            {
                _ordersNeadToCreate = new List<Order>();
            }

            if (_ordersInSystem != null)
            {
                if (NewOrderNeadToCansel != null &&
                    _ordersInSystem.State == OrderStateType.Activ)
                {
                    NewOrderNeadToCansel(_ordersInSystem);
                    _ordersInSystem = null;
                }
            }
        }

        public void Delete()
        {
            _bot = null;
            _ordersInSystem = null;
            _ordersNeadToCreate = null;
            _position = null;
        }

        /// <summary>
        /// Check whether it is time to send a new order
        /// </summary>
        public void Check()
        {
            if (_ordersNeadToCreate == null ||
                _ordersNeadToCreate.Count == 0)
            {
                return;
            }

            if (_ordersInSystem == null ||
                _ordersInSystem.State == OrderStateType.Done)
            {
                _ordersInSystem = _ordersNeadToCreate[0];
                _ordersNeadToCreate.Remove(_ordersInSystem);

                if (_type == AcebergType.Open)
                {
                    _position.AddNewOpenOrder(_ordersInSystem);
                }
                else if(_type == AcebergType.Close)
                {
                    _position.AddNewCloseOrder(_ordersInSystem);
                }
                else if (_type == AcebergType.ModificateBuy)
                {
                    if (_position.Direction == Side.Buy && _ordersInSystem.Side == Side.Buy||
                        _position.Direction == Side.Sell && _ordersInSystem.Side == Side.Sell)
                    {
                        _position.AddNewOpenOrder(_ordersInSystem);
                    }
                    else
                    {
                        _position.AddNewCloseOrder(_ordersInSystem);
                    }
                }
                else if (_type == AcebergType.ModificateSell)
                {
                    if (_position.Direction == Side.Buy && _ordersInSystem.Side == Side.Buy ||
                        _position.Direction == Side.Sell && _ordersInSystem.Side == Side.Sell)
                    {
                        _position.AddNewOpenOrder(_ordersInSystem);
                    }
                    else
                    {
                        _position.AddNewCloseOrder(_ordersInSystem);
                    }
                }

                if (NewOrderNeadToExecute != null)
                {
                    NewOrderNeadToExecute(_ordersInSystem);
                }
            }
        }

        /// <summary>
        /// Order must be executed
        /// </summary>
        public event Action<Order> NewOrderNeadToExecute;

        /// <summary>
        /// Order must be withdraw
        /// </summary>
        public event Action<Order> NewOrderNeadToCansel;

    }

    /// <summary>
    /// Iceberg type
    /// </summary>
    public enum AcebergType
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
        ModificateBuy,

        /// <summary>
        /// Modification by sale
        /// </summary>
        ModificateSell,
    }
}
