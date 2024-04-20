﻿/*
 * Your rights to use code governed by this license http://o-s-a.net/doc/license_simple_engine.pdf
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using OsEngine.Market;

namespace OsEngine.Entity
{
    /// <summary>
    /// Order
    /// </summary>
    public class Order
    {
        public Order()
        {
            State = OrderStateType.None;
            TimeCreate = DateTime.MinValue;
            TimeCallBack = DateTime.MinValue;
            TimeCancel = DateTime.MinValue;
            TimeDone =  DateTime.MinValue;
            NumberMarket = "";
            Side = Side.None;
        }

        /// <summary>
        /// Order number in the robot
        /// </summary>
        public int NumberUser;

        /// <summary>
        /// Order number on the exchange
        /// </summary>
        public string NumberMarket;

        /// <summary>
        /// Instrument code for which the transaction took place
        /// </summary>
        public string SecurityNameCode;

        /// <summary>
        /// Code of the class to which the security belongs
        /// </summary>
        public string SecurityClassCode;

        /// <summary>
        /// Account number to which the order belongs
        /// </summary>
        public string PortfolioNumber;

        /// <summary>
        /// Direction
        /// </summary>
        public Side Side;

        /// <summary>
        /// Bid price
        /// </summary>
        public decimal Price;

        /// <summary>
        /// Real price
        /// </summary>
        public decimal PriceReal
        {
            get
            {
                if((State == OrderStateType.None 
                    || State == OrderStateType.Activ
                    || State == OrderStateType.Cancel)
                    && _trades == null)
                {
                    return 0;
                }

                return GetMidlePrice();
            }
        }

        /// <summary>
        /// Volume
        /// </summary>
        public decimal Volume;

        /// <summary>
        /// Execute volume
        /// </summary>
        public decimal VolumeExecute
        {
            get
            {
                if (_trades != null && (_volumeExecute == 0 || _volumeExecuteChange))
                {
                    _volumeExecute = 0;
                    
                    for(int i = 0;i < _trades.Count;i++)
                    {
                        if(_trades[i] == null)
                        {
                            continue;
                        }

                        _volumeExecute += _trades[i].Volume;
                    }
                    
                    _volumeExecuteChange = false;
                    return _volumeExecute;
                }
                else
                {
                    if (_volumeExecute == 0 && State == OrderStateType.Done)
                    {
                        return Volume;
                    }
                    return _volumeExecute;
                }

            }
            set { _volumeExecute = value; }
        }
        private decimal _volumeExecute;
        private bool _volumeExecuteChange;

        /// <summary>
        /// My trades belonging to this order
        /// </summary>
        public List<MyTrade> MyTrades
        {
            get { return _trades; }
        }

        /// <summary>
        /// Order status: None, Pending, Done, Patrial, Fail
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
        /// Order price type. Limit, Market
        /// </summary>
        public OrderPriceType TypeOrder;

        /// <summary>
        /// Why the order was created in the context of the position. Open is the opening order. Close is the closing order
        /// </summary>
        public OrderPositionConditionType PositionConditionType;

        /// <summary>
        /// User comment
        /// </summary>
        public string Comment;

        /// <summary>
        /// Time of the first response from the stock exchange on the order. Server time
        /// </summary>
        public DateTime TimeCallBack;

        /// <summary>
        /// Time of order removal from the system. Server time
        /// </summary>
        public DateTime TimeCancel;

        /// <summary>
        /// Order execution time. Server time
        /// </summary>
        public DateTime TimeDone;

        /// <summary>
        /// Order creation time in OsApi. Server time
        /// </summary>
        public DateTime TimeCreate;

        /// <summary>
        /// Bidding rate
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
        /// Time when the order was the first transaction
        /// if there are no deals on the order yet, it will return the time to create the order
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

                if (MyTrades[0] != null)
                {
                    return MyTrades[0].Time;
                }

                return TimeCreate;
            }
        }

        /// <summary>
        /// Lifetime on the exchange, after which the order must be withdrawn
        /// </summary>
        public TimeSpan LifeTime;

        /// <summary>
        /// Flag saying that this order was created to close by stop or profit order
        /// the tester needs to perform it adequately
        /// </summary>
        public bool IsStopOrProfit;

        public ServerType ServerType;

        public TimeFrame TimeFrameInTester;

        // deals with which the order was opened and calculation of the order execution price

        /// <summary>
        /// Order trades
        /// </summary>
        private List<MyTrade> _trades;

        /// <summary>
        /// Heck the ownership of the transaction to this order
        /// </summary>
        public void SetTrade(MyTrade trade)
        {
            if (trade.NumberOrderParent != NumberMarket)
            {
                return;
            }

            if (_trades != null)
            {
                for (int i = 0; i < _trades.Count; i++)
                {
                    if (_trades[i] == null)
                    {
                        continue;
                    }
                    if (_trades[i].NumberTrade == trade.NumberTrade)
                    {
                        return;
                    }
                }
            }

            if (_trades == null)
            {
                _trades = new List<MyTrade>();
            }

            _trades.Add(trade);

            _volumeExecuteChange = true;

            if (Volume == VolumeExecute)
            {
                State = OrderStateType.Done;
            }
        }

        /// <summary>
        /// Take the average order execution price
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
                if(_trades[i] == null)
                {
                    continue;
                }

                price += _trades[i].Volume*_trades[i].Price;
                volumeExecute += _trades[i].Volume;
            }

            if (volumeExecute == 0)
            {
                return Price;
            }

            price = price/volumeExecute;

            return price;
        }

        /// <summary>
        /// Take the time of execution of the last trade on the order
        /// </summary>
        public DateTime GetLastTradeTime()
        {
            if (_trades == null)
            {
                return TimeCallBack;
            }
            if (_trades[0] == null)
            {
                return TimeCallBack;
            }
            return _trades[_trades.Count - 1].Time;
        }

        /// <summary>
        /// Whether the trades of this order came to the array
        /// </summary>
        public bool TradesIsComing
        {
            get
            {
                if (_trades == null 
                    || _trades.Count == 0)
                {
                    return false;
                }
                else
                {
                    return true;
                }              
            }
        }

        private static readonly CultureInfo CultureInfo = new CultureInfo("ru-RU");

        /// <summary>
        /// Take the string to save
        /// </summary>
        public StringBuilder GetStringForSave()
        {
            if (_saveString != null)
            {
                return _saveString;
            }

            StringBuilder result = new StringBuilder();

            result.Append(NumberUser + "@");

            result.Append(ServerType + "@");

            result.Append(NumberMarket.ToString(CultureInfo) + "@");
            result.Append(Side + "@");
            result.Append(Price.ToString(CultureInfo) + "@");
            result.Append(PriceReal.ToString(CultureInfo) + "@");
            result.Append(Volume.ToString(CultureInfo) + "@");
            result.Append(VolumeExecute.ToString(CultureInfo) + "@");
            result.Append(State + "@");
            result.Append(TypeOrder + "@");
            result.Append(TimeCallBack.ToString(CultureInfo) + "@");
            result.Append(SecurityNameCode + "@");

            if(PortfolioNumber != null)
            {
                result.Append(PortfolioNumber.Replace('@', '%') + "@");
            }
            else
            {
                result.Append("" + "@");
            }

            result.Append(TimeCreate.ToString(CultureInfo) + "@");
            result.Append(TimeCancel.ToString(CultureInfo) + "@");
            result.Append(TimeCallBack.ToString(CultureInfo) + "@");

            result.Append(LifeTime + "@");

            // deals with which the order was opened and the order execution price was calculated

            if (_trades == null)
            {
                result.Append("null");
            }
            else
            {
                for (int i = 0; i < _trades.Count; i++)
                {
                    if(_trades[i] == null)
                    {
                        continue;
                    }

                    result.Append(_trades[i].GetStringFofSave() + "*");
                }
            }
            result.Append("@");

            result.Append(Comment + "@");

            result.Append(TimeDone.ToString(CultureInfo) + "@");

            if (State == OrderStateType.Done && Volume == VolumeExecute &&
                _trades != null && _trades.Count > 0)
            {
                _saveString = result;
            }

            return result;
        }

        private StringBuilder _saveString;

        /// <summary>
        /// Load order from incoming line
        /// </summary>
        public void SetOrderFromString(string saveString)
        {
            string[] saveArray = saveString.Split('@');
            NumberUser = Convert.ToInt32(saveArray[0]);

            Enum.TryParse(saveArray[1], true, out ServerType);

            NumberMarket = saveArray[2];
            Enum.TryParse(saveArray[3], true, out Side);
            Price = saveArray[4].ToDecimal();

            Volume = saveArray[6].ToDecimal();
            VolumeExecute = saveArray[7].ToDecimal();

            Enum.TryParse(saveArray[8], true, out _state);
            Enum.TryParse(saveArray[9], true, out TypeOrder);
            TimeCallBack = Convert.ToDateTime(saveArray[10], CultureInfo);

            SecurityNameCode = saveArray[11];
            PortfolioNumber = saveArray[12].Replace('%', '@');


            TimeCreate = Convert.ToDateTime(saveArray[13], CultureInfo);
            TimeCancel = Convert.ToDateTime(saveArray[14], CultureInfo);
            TimeCallBack = Convert.ToDateTime(saveArray[15], CultureInfo);

            TimeSpan.TryParse(saveArray[16], out LifeTime);

            // deals with which the order was opened and the order execution price was calculated

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
            TimeDone = Convert.ToDateTime(saveArray[19], CultureInfo);
        }
    }

    /// <summary>
    /// Price type for order
    /// </summary>
    public enum OrderPriceType
    {
        /// <summary>
        /// Limit order. Those. bid at a certain price
        /// </summary>
        Limit,

        /// <summary>
        /// Market application. Those. application at any price
        /// </summary>
        Market,

        /// <summary>
        /// Iceberg application. Those. An application whose volume is not fully visible in the glass.
        /// </summary>
        Iceberg
    }

    /// <summary>
    /// Order status
    /// </summary>
    public enum OrderStateType
    {
        /// <summary>
        /// None
        /// </summary>
        None,

        /// <summary>
        /// Accepted by the exchange and exhibited in the system
        /// </summary>
        Activ,

        /// <summary>
        /// Waiting for registration
        /// </summary>
        Pending,

        /// <summary>
        /// Done
        /// </summary>
        Done,

        /// <summary>
        /// Partitial done
        /// </summary>
        Patrial,

        /// <summary>
        /// Error
        /// </summary>
        Fail,

        /// <summary>
        /// Cancel
        /// </summary>
        Cancel,

        /// <summary>
        /// Status did not change after Active. Possible error
        /// </summary>
        LostAfterActive
    }

    /// <summary>
    /// The purpose of the order, opening or closing a position
    /// </summary>
    public enum OrderPositionConditionType
    {
        None,
        Open,
        Close
    }
}
