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
    /// class to create iceberg
    /// класс для создания айсберг заявок
    /// </summary>
    public class AcebergMaker
    {

        /// <summary>
        /// all icebergs
        /// все айсберги
        /// </summary>
        private List<Aceberg> _acebergOrders;

        /// <summary>
        /// make a new iceberg
        /// сделать новый айсберг
        /// </summary>
        /// <param name="price">price / цена</param>
        /// <param name="lifiTime">life time / время жизни</param>
        /// <param name="ordersCount">orders count / количество ордеров</param>
        /// <param name="position">position / позиция</param>
        /// <param name="type">type / тип</param>
        /// <param name="volume">volume / объём</param>
        /// <param name="bot">bot / робот</param>
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
        /// you must execute this order event
        /// необходимо исполнить этот ордер
        /// </summary>
        private void newAceberg_newOrderNeadToExecute(Order order)
        {
            if (NewOrderNeadToExecute != null)
            {
                NewOrderNeadToExecute(order);
            }
        }

        /// <summary>
        /// it is necessary to withdraw the order
        /// необходимо отозвать ордер
        /// </summary>
        private void newAceberg_newOrderNeadToCansel(Order order)
        {
            if (NewOrderNeadToCansel != null)
            {
                NewOrderNeadToCansel(order);
            }
        }

        /// <summary>
        /// incoming order
        /// входящий ордер
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
        /// clear icebergs
        /// очистить айсберги
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
            }
        }

        /// <summary>
        /// order must be executed event
        /// необходимо исполнить ордер
        /// </summary>
        public event Action<Order> NewOrderNeadToExecute;

        /// <summary>
        /// order must be withdraw event
        /// необходимо отозвать ордер
        /// </summary>
        public event Action<Order> NewOrderNeadToCansel;

    }

    /// <summary>
    /// iceberg
    /// айсберг
    /// </summary>
    public class Aceberg
    {
        /// <summary>
        /// constructor
        /// конструктор
        /// </summary>
        /// <param name="price">price / цена</param>
        /// <param name="lifiTime">life time / время жизни заявок</param>
        /// <param name="ordersCount">orders count / кол-во ордеров</param>
        /// <param name="position">position / позиция</param>
        /// <param name="bot">robot / робот</param>
        /// <param name="type">iceberg type / тип айсберга</param>
        /// <param name="volume">sum volume / общий объём</param>
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
        /// life time
        /// время жизни заявок
        /// </summary>
        private TimeSpan _lifiTime;

        /// <summary>
        /// position
        /// позиция
        /// </summary>
        private Position _position;

        /// <summary>
        /// price
        /// цена
        /// </summary>
        private decimal _price;

        /// <summary>
        /// orders count
        /// количество ордеров
        /// </summary>
        private int _ordersCount;

        /// <summary>
        /// sum volume
        /// общий объём
        /// </summary>
        private decimal _volume;

        /// <summary>
        /// robot
        /// робот
        /// </summary>
        private BotTabSimple _bot;

        /// <summary>
        /// order placed in the system
        /// выставленный в систему ордер
        /// </summary>
        private Order _ordersInSystem;

        /// <summary>
        /// orders that must be placed
        /// ордера которые должны быть выставленны
        /// </summary>
        private List<Order> _ordersNeadToCreate;

        /// <summary>
        /// iceberg type
        /// тип айсберга
        /// </summary>
        private AcebergType _type;

        /// <summary>
        /// create an array of orders for iceberg
        /// создать массив ордеров для айсберга
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
        /// create an array of orders for iceberg
        /// создать массив ордеров для айсберга
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
        /// create an array of orders for iceberg
        /// создать массив ордеров для айсберга
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
        /// create an array of orders for iceberg
        /// создать массив ордеров для айсберга
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
        /// add order to iceberg
        /// добавить ордер в айсберг
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
        /// withdraw all orders
        /// отозвать все ордера
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

        /// <summary>
        /// check whether it is time to send a new order
        /// проверить, не пора ли высылать новую заявку
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
        /// order must be executed
        /// необходимо исполнить ордер
        /// </summary>
        public event Action<Order> NewOrderNeadToExecute;

        /// <summary>
        /// order must be withdraw
        /// необходимо отозвать ордер
        /// </summary>
        public event Action<Order> NewOrderNeadToCansel;

    }

    /// <summary>
    /// iceberg type /
    /// тип айсберга
    /// </summary>
    public enum AcebergType
    {
        /// <summary>
        /// to open /
        /// на открытие
        /// </summary>
        Open,

        /// <summary>
        /// to close /
        /// на закрытие
        /// </summary>
        Close,

        /// <summary>
        /// modification by buy/
        /// модификация через покупку
        /// </summary>
        ModificateBuy,

        /// <summary>
        /// modification by sale /
        /// модификация через продажу
        /// </summary>
        ModificateSell,
    }
}
