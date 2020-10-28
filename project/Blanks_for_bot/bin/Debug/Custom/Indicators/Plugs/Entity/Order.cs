/*
 * Your rights to use code governed by this license http://o-s-a.net/doc/license_simple_engine.pdf
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace OsEngine.Entity
{
    /// <summary>
    /// order
    /// ордер
    /// </summary>
    public class Order
    {
        public Order()
        {
            State = OrderStateType.None;
            TimeCreate = DateTime.MinValue;
            TimeCallBack = DateTime.MinValue;
            TimeCancel = DateTime.MinValue;
            TimeDone = DateTime.MinValue;
            NumberMarket = "";
            Side = Side.None;
        }

        /// <summary>
        /// order number in the robot
        /// номер ордера в роботе
        /// </summary>
        public int NumberUser;

        /// <summary>
        /// order number on the exchange
        /// номер ордера на бирже
        /// </summary>
        public string NumberMarket;

        /// <summary>
        /// instrument code for which the transaction took place
        /// код инструмента по которому прошла сделка
        /// </summary>
        public string SecurityNameCode;

        /// <summary>
        /// account number to which the order belongs
        /// номер счёта которому принадлежит ордер
        /// </summary>
        public string PortfolioNumber;

        /// <summary>
        /// direction
        /// направление
        /// </summary>
        public Side Side;

        /// <summary>
        /// bid price
        /// цена заявки
        /// </summary>
        public decimal Price;

        /// <summary>
        /// real price
        /// цена исполнения
        /// </summary>
        public decimal PriceReal
        {
            get
            {
                return GetMidlePrice();
            }
        }

        /// <summary>
        /// volume
        /// объём
        /// </summary>
        public decimal Volume;

        /// <summary>
        /// execute volume
        /// объём исполнившийся
        /// </summary>
        public decimal VolumeExecute
        {
            get
            {
                if (_trades != null && (_volumeExecute == 0 || _volumeExecuteChange))
                {
                    _volumeExecute = _trades.Sum(trade => trade.Volume);
                    _volumeExecuteChange = false;
                    return _volumeExecute;
                }
                else
                {
                    return _volumeExecute;
                }

            }
            set { _volumeExecute = value; }
        }
        private decimal _volumeExecute;
        private bool _volumeExecuteChange;

        public List<MyTrade> MyTrades
        {
            get { return _trades; }
        }

        /// <summary>
        /// order status: None, Pending, Done, Patrial, Fail
        /// статус ордера: None, Pending, Done, Patrial, Fail
        /// </summary>
        public OrderStateType State
        {
            get { return _state; }
            set
            {
                _state = value;
            }
        }

        private OrderStateType _state;
        /// <summary>
        /// order price type. Limit, Market
        /// тип цены ордера. Limit, Market
        /// </summary>
        public OrderPriceType TypeOrder;

        /// <summary>
        /// user comment
        /// комментарий пользователя
        /// </summary>
        public string Comment;

        /// <summary>
        /// time of the first response from the stock exchange on the order. Server time
        /// время первого отклика от биржи по ордеру. Время севрера.
        /// </summary>
        public DateTime TimeCallBack;

        /// <summary>
        /// time of order removal from the system. Server time
        /// время снятия ордера из системы. Время сервера
        /// </summary>
        public DateTime TimeCancel;

        /// <summary>
        /// order execution time. Server time
        /// время исполнения ордера. Время сервера
        /// </summary>
        public DateTime TimeDone;

        /// <summary>
        /// order creation time in OsApi. Server time
        /// время создания ордера в OsApi. Время сервера
        /// </summary>
        public DateTime TimeCreate;

        /// <summary>
        /// bidding rate
        /// скорость выставления заявки
        /// </summary>
        public TimeSpan TimeRoundTrip
        {
            get
            {
                if (TimeCallBack == DateTime.MinValue ||
                    TimeCreate == DateTime.MinValue)
                {
                    return new TimeSpan(0, 0, 0, 0);
                }

                return (TimeCallBack - TimeCreate);
            }
        }

        /// <summary>
        /// /// time when the order was the first transaction
        /// if there are no deals on the order yet, it will return the time to create the order
        /// время когда по ордеру прошла первая сделка
        /// если сделок по ордеру ещё нет, вернёт время создания ордера
        /// </summary>
        public DateTime TimeExecuteFirstTrade
        {
            get
            {
                if (MyTrades == null ||
                    MyTrades.Count == 0)
                {
                    return TimeCreate;
                }

                return MyTrades[0].Time;
            }
        }

        /// <summary>
        /// lifetime on the exchange, after which the order must be withdrawn
        /// время жизни на бирже, после чего ордер надо отзывать
        /// </summary>
        public TimeSpan LifeTime;

        /// <summary>
        /// /// flag saying that this order was created to close by stop or profit order
        /// the tester needs to perform it adequately
        /// флаг,говорящий о том что этот ордер был создан для закрытия по стоп или профит приказу
        /// нужен тестеру для адекватного его исполнения
        /// </summary>
        public bool IsStopOrProfit;


        /// <summary>
        /// order trades
        /// сделки ордера
        /// </summary>
        private List<MyTrade> _trades;

        /// <summary>
        /// heck the ownership of the transaction to this order
        /// проверить принадлежность сделки этому ордеру
        /// </summary>
        public void SetTrade(MyTrade trade)
        {
            if ((trade.NumberOrderParent != NumberMarket &&
                 trade.NumberOrderParent != NumberMarket &&
                trade.NumberOrderParent != NumberUser.ToString()))
            {
                return;
            }

            if (_trades != null)
            {
                foreach (var tradeInArray in _trades)
                {
                    if (tradeInArray.NumberTrade == trade.NumberTrade)
                    {
                        // / such an application is already in storage, a stupid API is poisoning with toxic data, we exit
                        // такая заявка уже в хранилище, глупое АПИ травит токсичными данными, выходим
                        return;
                    }
                }
            }

            _volumeExecuteChange = true;

            if (_trades == null)
            {
                _trades = new List<MyTrade>();
            }

            _trades.Add(trade);

            if (Volume != VolumeExecute)
            {
                State = OrderStateType.Patrial;
            }
            else
            {
                State = OrderStateType.Done;
            }
        }

        /// <summary>
        /// take the average order execution price
        /// взять среднюю цену исполнения ордера
        /// </summary>
        public decimal GetMidlePrice()
        {
            if (_trades == null)
            {
                return Price;
            }
            decimal price = 0;

            decimal volumeExecute = 0;

            for (int i = 0; i < _trades.Count; i++)
            {
                price += _trades[i].Volume * _trades[i].Price;
                volumeExecute += _trades[i].Volume;
            }

            if (volumeExecute == 0)
            {
                return Price;
            }

            price = price / volumeExecute;

            return price;
        }

        /// <summary>
        /// take the time of execution of the last trade on the order
        /// взять время исполнения последнего трейда по ордеру
        /// </summary>
        public DateTime GetLastTradeTime()
        {
            if (_trades == null)
            {
                return TimeCallBack;
            }

            return _trades[_trades.Count - 1].Time;
        }

        /// <summary>
        /// whether the trades of this order came to the array
        /// пришли ли трейды этого ордера в массив
        /// </summary>
        public bool TradesIsComing
        {
            get
            {
                if (_trades == null || _trades.Count == 0)
                {
                    return false;
                }
                else
                {
                    return true;
                }

            }
        }

        /// <summary>
        /// take the string to save
        /// взять строку для сохранения
        /// </summary>
        public StringBuilder GetStringForSave()
        {
            StringBuilder result = new StringBuilder();

            result.Append(NumberUser + "@");


            result.Append(NumberMarket.ToString(new CultureInfo("ru-RU")) + "@");
            result.Append(Side + "@");
            result.Append(Price.ToString(new CultureInfo("ru-RU")) + "@");
            result.Append(PriceReal.ToString(new CultureInfo("ru-RU")) + "@");
            result.Append(Volume.ToString(new CultureInfo("ru-RU")) + "@");
            result.Append(VolumeExecute.ToString(new CultureInfo("ru-RU")) + "@");
            result.Append(State + "@");
            result.Append(TypeOrder + "@");
            result.Append(TimeCallBack.ToString(new CultureInfo("ru-RU")) + "@");

            result.Append(SecurityNameCode + "@");
            result.Append(PortfolioNumber.Replace('@', '%') + "@");

            result.Append(TimeCreate.ToString(new CultureInfo("ru-RU")) + "@");
            result.Append(TimeCancel.ToString(new CultureInfo("ru-RU")) + "@");
            result.Append(TimeCallBack.ToString(new CultureInfo("ru-RU")) + "@");

            result.Append(LifeTime + "@");
            // deals with which the order was opened and the order execution price was calculated
            // сделки, которыми открывался ордер и рассчёт цены исполнения ордера

            if (_trades == null)
            {
                result.Append("null");
            }
            else
            {
                for (int i = 0; i < _trades.Count; i++)
                {
                    result.Append(_trades[i].GetStringFofSave() + "*");
                }
            }
            result.Append("@");

            result.Append(Comment + "@");

            result.Append(TimeDone.ToString(new CultureInfo("ru-RU")) + "@");

            return result;
        }

        /// <summary>
        /// load order from incoming line
        /// загрузить ордер из входящей строки
        /// </summary>
        public void SetOrderFromString(string saveString)
        {
            string[] saveArray = saveString.Split('@');
            NumberUser = Convert.ToInt32(saveArray[0]);


            NumberMarket = saveArray[2];
            Enum.TryParse(saveArray[3], true, out Side);
            Price = saveArray[4].ToDecimal();

            Volume = saveArray[6].ToDecimal();
            VolumeExecute = saveArray[7].ToDecimal();

            Enum.TryParse(saveArray[8], true, out _state);
            Enum.TryParse(saveArray[9], true, out TypeOrder);
            TimeCallBack = Convert.ToDateTime(saveArray[10]);

            SecurityNameCode = saveArray[11];
            PortfolioNumber = saveArray[12].Replace('%', '@');


            TimeCreate = Convert.ToDateTime(saveArray[13]);
            TimeCancel = Convert.ToDateTime(saveArray[14]);
            TimeCallBack = Convert.ToDateTime(saveArray[15]);

            TimeSpan.TryParse(saveArray[16], out LifeTime);
            // deals with which the order was opened and the order execution price was calculated
            // сделки, которыми открывался ордер и рассчёт цены исполнения ордера

            if (saveArray[17] == "null")
            {
                _trades = null;
            }
            else
            {
                string[] tradesArray = saveArray[17].Split('*');

                _trades = new List<MyTrade>();

                for (int i = 0; i < tradesArray.Length - 1; i++)
                {
                    _trades.Add(new MyTrade());
                    _trades[i].SetTradeFromString(tradesArray[i]);
                }
            }
            Comment = saveArray[18];
            TimeDone = Convert.ToDateTime(saveArray[19]);
        }
    }


    /// <summary>
    /// price type for order
    /// тип цены для ордера
    /// </summary>
    public enum OrderPriceType
    {
        /// <summary>
        /// limit order. Those. bid at a certain price
        /// лимитная заявка. Т.е. заявка по определённой цене
        /// </summary>
        Limit,

        /// <summary>
        /// market application. Those. application at any price
        /// рыночная заявка. Т.е. заявка по любой цене
        /// </summary>
        Market,

        /// <summary>
        /// iceberg application. Those. An application whose volume is not fully visible in the glass.
        /// айсберг заявка. Т.е. заявка объём которой полностью не виден в стакане.
        /// </summary>
        Iceberg
    }

    /// <summary>
    /// Order status
    /// статус Ордера
    /// </summary>
    public enum OrderStateType
    {
        /// <summary>
        /// none
        /// отсутствует
        /// </summary>
        None,

        /// <summary>
        /// accepted by the exchange and exhibited in the system
        /// принята биржей и выставленна в систему
        /// </summary>
        Activ,

        /// <summary>
        /// waiting for registration
        /// ожидает регистрации
        /// </summary>
        Pending,

        /// <summary>
        /// done
        /// исполнен
        /// </summary>
        Done,

        /// <summary>
        /// partitial done
        /// исполнен частично
        /// </summary>
        Patrial,

        /// <summary>
        /// error
        /// произошла ошибка
        /// </summary>
        Fail,

        /// <summary>
        /// cancel
        /// отменён
        /// </summary>
        Cancel
    }
}
