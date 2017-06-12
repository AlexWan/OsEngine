﻿/*
 *Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System;
using System.Collections.Generic;
using System.Globalization;
using OsEngine.Market.Servers;

namespace OsEngine.Entity
{
    /// <summary>
    /// ордер
    /// </summary>
    public class Order
    {
        public Order()
        {
            State = OrderStateType.None;
            TimeCreate = DateTime.Now;
            TimeCallBack = DateTime.MinValue;
            TimeCancel = DateTime.MinValue;
            NumberMarket = "";
            Side = Side.UnKnown;
        }

        /// <summary>
        /// номер ордера в роботе
        /// </summary>
        public int NumberUser;  

        /// <summary>
        /// номер ордера на бирже
        /// </summary>
        public string NumberMarket;

        /// <summary>
        /// код инструмента по которому прошла сделка
        /// </summary>
        public string SecurityNameCode;

        /// <summary>
        /// номер счёта которому принадлежит ордер
        /// </summary>
        public string PortfolioNumber;

        /// <summary>
        /// направление
        /// </summary>
        public Side Side;

        /// <summary>
        /// цена заявки
        /// </summary>
        public decimal Price;

        /// <summary>
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
        /// объём
        /// </summary>
        public int Volume;

        /// <summary>
        /// объём исполнившийся
        /// </summary>
        public int VolumeExecute
        {
            get
            {
                if (_trades != null)
                {
                    int volumeExecute = 0;
                    foreach (var trade in _trades)
                    {
                        volumeExecute += trade.Volume;
                    }
                    return volumeExecute;
                }
                else
                {
                    return _volumeExecute;
                }

            }
            set { _volumeExecute = value; }
        }
        private int _volumeExecute;

        public List<MyTrade> MyTrades
        {
            get { return _trades; }
        } 

        /// <summary>
        /// статус ордера: None, Pending, Done, Patrial, Fail
        /// </summary>
        public OrderStateType State; 

        /// <summary>
        /// тип цены ордера. Limit, Market
        /// </summary>
        public OrderPriceType TypeOrder;

        /// <summary>
        /// комментарий пользователя
        /// </summary>
        public string Comment;

        /// <summary>
        /// время выставления ордера на биржу
        /// </summary>
        public DateTime TimeCallBack;
        
        /// <summary>
        /// время снятия ордера из системы
        /// </summary>
        public DateTime TimeCancel;

        /// <summary>
        /// время создания ордера в OsApi
        /// </summary>
        public DateTime TimeCreate;

        /// <summary>
        /// скорость выставления заявки
        /// </summary>
        public TimeSpan TimeRoundTrip
        {
            get
            {
                if (TimeCallBack == DateTime.MinValue ||
                    TimeCreate == DateTime.MinValue)
                {
                    return new TimeSpan(0,0,0,0);
                }

                return (TimeCallBack - TimeCreate);
            }
        }

        /// <summary>
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
        /// время жизни на бирже, после чего ордер надо отзывать
        /// </summary>
        public TimeSpan LifeTime;

        /// <summary>
        /// флаг,говорящий о том что этот ордер был создан для закрытия по стоп или профит приказу
        /// нужен тестеру для адекватного его исполнения
        /// </summary>
        public bool IsStopOrProfit;

        public ServerType ServerType;
 

// сделки, которыми открывался ордер и рассчёт цены исполнения ордера

        /// <summary>
        /// сделки ордера
        /// </summary>
        private List<MyTrade> _trades;

        /// <summary>
        /// проверить принадлежность сделки этому ордеру
        /// </summary>
        public void SetTrade(MyTrade trade)
        {
            if (trade.NumberOrderParent != NumberMarket)
            {
                return;
            }

            if (_trades != null)
            {
                foreach (var tradeInArray in _trades)
                {
                    if (tradeInArray.NumberTrade == trade.NumberTrade)
                    { // такая заявка уже в хранилище, глупое АПИ травит токсичными данными, выходим
                        return;
                    }
                }
            }


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
                price += _trades[i].Volume*_trades[i].Price;
                volumeExecute += _trades[i].Volume;
            }

            price = price/volumeExecute;

            return price;
        }

        /// <summary>
        /// взять время исполнения последнего трейда по ордеру
        /// </summary>
        public DateTime GetLastTradeTime()
        {
            if (_trades == null)
            {
                return DateTime.MinValue;
            }

           return _trades[_trades.Count - 1].Time;
        }

        /// <summary>
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
        /// взять строку для сохранения
        /// </summary>
        public string GetStringForSave()
        {
            string result = "";

            result += NumberUser + "@";

            result += ServerType + "@";

            result += NumberMarket.ToString(new CultureInfo("ru-RU")) + "@";
            result += Side + "@";
            result += Price.ToString(new CultureInfo("ru-RU")) + "@";
            result += PriceReal.ToString(new CultureInfo("ru-RU")) + "@";
            result += Volume.ToString(new CultureInfo("ru-RU")) + "@";
            result += VolumeExecute.ToString(new CultureInfo("ru-RU")) + "@";
            result += State + "@";
            result += TypeOrder + "@";
            result += TimeCallBack.ToString(new CultureInfo("ru-RU")) + "@";

            result += SecurityNameCode + "@";
            result += PortfolioNumber.Replace('@', '%') +"@";

            result += TimeCreate.ToString(new CultureInfo("ru-RU")) + "@";
            result += TimeCancel.ToString(new CultureInfo("ru-RU")) + "@";
            result += TimeCallBack.ToString(new CultureInfo("ru-RU")) + "@";
            result += LifeTime + "@";
        // сделки, которыми открывался ордер и рассчёт цены исполнения ордера

            if (_trades == null)
            {
                result += "null";
            }
            else
            {
                for (int i = 0; i < _trades.Count; i++)
                {
                    result += _trades[i].GetStringFofSave() + "*";
                }
            }
            result += "@";

            result += Comment;

            return result;
        }

        /// <summary>
        /// загрузить ордер из входящей строки
        /// </summary>
        public void SetOrderFromString(string saveString)
        {
            string [] saveArray = saveString.Split('@');
            NumberUser = Convert.ToInt32(saveArray[0]);

            Enum.TryParse(saveArray[1], true, out ServerType);

            NumberMarket = saveArray[2];
            Enum.TryParse(saveArray[3], true, out Side);
            Price = Convert.ToDecimal(saveArray[4]);
            Volume = Convert.ToInt32(saveArray[6]);
            VolumeExecute = Convert.ToInt32(saveArray[7]);
            Enum.TryParse(saveArray[8], true, out State);
            Enum.TryParse(saveArray[9], true, out TypeOrder);
            TimeCallBack = Convert.ToDateTime(saveArray[10]);

            SecurityNameCode = saveArray[11];
            PortfolioNumber = saveArray[12].Replace('%', '@');


            TimeCreate = Convert.ToDateTime(saveArray[13]);
            TimeCancel = Convert.ToDateTime(saveArray[14]);
            TimeCallBack = Convert.ToDateTime(saveArray[15]);
            TimeSpan.TryParse(saveArray[16],out LifeTime);
            // сделки, которыми открывался ордер и рассчёт цены исполнения ордера

            if (saveArray[17] == "null")
            {
                _trades = null;
            }
            else
            {
                string [] tradesArray = saveArray[17].Split('*');

                _trades = new List<MyTrade>();

                for (int i = 0; i < tradesArray.Length-1; i++)
                {
                    _trades.Add(new MyTrade());
                    _trades[i].SetTradeFromString(tradesArray[i]);
                }
            }
            Comment = saveArray[18];
        }
    }

    /// <summary>
    /// тип цены для ордера
    /// </summary>
    public enum OrderPriceType
    {
        /// <summary>
        /// лимитная заявка. Т.е. заявка по определённой цене
        /// </summary>
        Limit,

        /// <summary>
        /// рыночная заявка. Т.е. заявка по любой цене
        /// </summary>
        Market,

        /// <summary>
        /// айсберг заявка. Т.е. заявка объём которой полностью не виден в стакане.
        /// </summary>
        Iceberg
    }

    /// <summary>
    /// статус Ордера
    /// </summary>
    public enum OrderStateType
    {
        /// <summary>
        /// принята биржей и выставленна в систему
        /// </summary>
        Activ,
        /// <summary>
        /// отсутствует
        /// </summary>
        None,

        /// <summary>
        /// ожидает регистрации
        /// </summary>
        Pending,

        /// <summary>
        /// исполнен
        /// </summary>
        Done,

        /// <summary>
        /// исполнен частично
        /// </summary>
        Patrial,

        /// <summary>
        /// произошла ошибка
        /// </summary>
        Fail,

        /// <summary>
        /// отменён
        /// </summary>
        Cancel
    }
}
