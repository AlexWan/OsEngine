/*
 * Your rights to use code governed by this license http://o-s-a.net/doc/license_simple_engine.pdf
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using OsEngine.Language;
using OsEngine.Market;
using OsEngine.Market.Servers;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace OsEngine.Entity
{
    /// <summary>
    /// Position
    /// </summary>
    public class Position
    {
        /// <summary>
        /// Constructor
        /// </summary>
        public Position()
        {
            State = PositionStateType.None;
        }

        /// <summary>
        /// List of orders involved in opening a position
        /// </summary>
        public List<Order> OpenOrders
        {
            get
            {
                return _openOrders;
            }
        }
        private List<Order> _openOrders;

        /// <summary>
        /// Load a new order to open a position
        /// </summary>
        /// <param name="openOrder"></param>
        public void AddNewOpenOrder(Order openOrder)
        {
            if (_openOrders == null)
            {
                _openOrders = new List<Order>();
                _openOrders.Add(openOrder);
            }
            else
            {
                if(string.IsNullOrEmpty(SecurityName) == false 
                    && SecurityName.EndsWith("TestPaper") == false)
                {
                    if(SecurityName == openOrder.SecurityNameCode)
                    {
                        _openOrders.Add(openOrder);
                    }
                }
                else
                {
                    _openOrders.Add(openOrder);
                }
            }

            State = PositionStateType.Opening;
        }

        /// <summary>
        /// List of orders involved in closing a position
        /// </summary>
        public List<Order> CloseOrders
        {
            get
            {
                return _closeOrders;
            }
        }
        private List<Order> _closeOrders;

        /// <summary>
        /// Trades of this position
        /// </summary>
        public List<MyTrade> MyTrades
        {
            get
            {
                List<MyTrade> trades = _myTrades;
                if (trades != null)
                {
                    return trades;
                }
                trades = new List<MyTrade>();

                for (int i = 0; _openOrders != null && i < _openOrders.Count; i++)
                {
                    Order order = _openOrders[i];

                    if(order == null)
                    {
                        continue;
                    }

                    List<MyTrade> newTrades = order.MyTrades;
                    if (newTrades != null &&
                        newTrades.Count != 0)
                    {
                        trades.AddRange(newTrades);
                    }
                }

                for (int i = 0; _closeOrders != null && i < _closeOrders.Count; i++)
                {
                    Order order = _closeOrders[i];

                    if (order == null)
                    {
                        continue;
                    }

                    List<MyTrade> newTrades = _closeOrders[i].MyTrades;
                    if (newTrades != null &&
                        newTrades.Count != 0)
                    {
                        trades.AddRange(newTrades);
                    }
                }

                _myTrades = trades;
                return trades;
            }
        }

        private List<MyTrade> _myTrades;

        /// <summary>
        /// Load a new order to a position
        /// </summary>
        /// <param name="closeOrder"></param>
        public void AddNewCloseOrder(Order closeOrder)
        {
            if (CloseOrders == null)
            {
                _closeOrders = new List<Order>();
                _closeOrders.Add(closeOrder);
            }
            else
            {
                if (string.IsNullOrEmpty(SecurityName) == false
                    && SecurityName.EndsWith("TestPaper") == false)
                {
                    if(SecurityName == closeOrder.SecurityNameCode)
                    {
                        _closeOrders.Add(closeOrder);
                    }
                }
                else
                {
                    _closeOrders.Add(closeOrder);
                }
            }

            State = PositionStateType.Closing;
        }

        /// <summary>
        /// Are there any active orders to open a position
        /// </summary>
        public bool OpenActive
        {
            get
            {
                if (OpenOrders == null ||
                    OpenOrders.Count == 0)
                {
                    return false;
                }

                if (OpenOrders.Find(order => order != null
                                             && (order.State == OrderStateType.Active 
                                             || order.State == OrderStateType.Pending 
                                             || order.State == OrderStateType.None
                                             || order.State == OrderStateType.Partial)) != null)
                {
                    return true;
                }
                return false;
            }
        }

        /// <summary>
        /// Are there any active orders to close a position
        /// </summary>
        public bool CloseActive
        {
            get
            {
                if (CloseOrders == null ||
                    CloseOrders.Count == 0)
                {
                    return false;
                }

                if (CloseOrders.Find(order => order != null
                                             && (order.State == OrderStateType.Active 
                                              || order.State == OrderStateType.Pending
                                              || order.State == OrderStateType.None
                                              || order.State == OrderStateType.Partial)) != null
                    )
                {
                    return true;
                }
                return false;
            }
        }

        /// <summary>
        /// Whether stop is active
        /// </summary>
        public bool StopOrderIsActive;

        /// <summary>
        /// Order price stop order
        /// </summary>
        public decimal StopOrderPrice;

        /// <summary>
        /// Stop - the price, the price after which the order will be entered into the system
        /// </summary>
        public decimal StopOrderRedLine;

        /// <summary>
        /// Whether the position will be closed by a stop using a market order
        /// </summary>
        public bool StopIsMarket;

        /// <summary>
        /// Is a profit active order
        /// </summary>
        public bool ProfitOrderIsActive;

        /// <summary>
        /// Order price order profit
        /// </summary>
        public decimal ProfitOrderPrice;

        /// <summary>
        /// Profit - the price, the price after which the order will be entered into the system
        /// </summary>
        public decimal ProfitOrderRedLine;

        /// <summary>
        /// Whether the position will be closed by a profit using a market order
        /// </summary>
        public bool ProfitIsMarket;

        /// <summary>
        /// Buy / sell direction
        /// </summary>
        public Side Direction;

        private PositionStateType _state;

        /// <summary>
        /// Transaction status Open / Close / Opening
        /// </summary>
        public PositionStateType State
        {
            get { return _state; }
            set
            {
                _state = value;
            }
        }

        /// <summary>
        /// Position number
        /// </summary>
        public int Number;

        /// <summary>
        /// Security code for which the position is open
        /// </summary>
        public string SecurityName
        {
            get
            {
                if (_openOrders != null 
                    && _openOrders.Count != 0
                    && _openOrders[0] != null)
                {
                    return _openOrders[0].SecurityNameCode;
                }
                return _securityName;
            }
            set 
            {
                if (_openOrders != null 
                    && _openOrders.Count != 0)
                {
                    return;
                }
                _securityName = value; 
            }
        }
        private string _securityName;

        /// <summary>
        /// Name of the bot who owns the deal
        /// </summary>
        public string NameBot;

        /// <summary>
        /// The name of the robot class that created the position
        /// </summary>
        public string NameBotClass;

        /// <summary>
        /// unique server name in multi-connection mode
        /// </summary>
        public string ServerName
        {
            get
            {
                if(OpenOrders == null 
                    || OpenOrders.Count == 0
                    || OpenOrders[0] == null)
                {
                    return null;
                }
                else
                {
                    return OpenOrders[0].ServerName;
                }
            }
        }

        /// <summary>
        /// The amount of profit on the operation in percent
        /// </summary>
        public decimal ProfitOperationPercent;

        /// <summary>
        /// The amount of profit on the operation in absolute terms
        /// </summary>
        public decimal ProfitOperationAbs;

        /// <summary>
        /// Comment
        /// </summary>
        public string Comment;

        /// <summary>
        /// Signal type to open
        /// </summary>
        public string SignalTypeOpen;

        /// <summary>
        /// Closing signal type
        /// </summary>
        public string SignalTypeClose;

        /// <summary>
        /// Closing signal type if a stop order is triggered
        /// </summary>
        public string SignalTypeStop;

        /// <summary>
        /// Closing signal type if a profit order is triggered
        /// </summary>
        public string SignalTypeProfit;

        /// <summary>
        /// Maximum volume by position
        /// </summary>
        public decimal MaxVolume
        {
            get
            {
                decimal value = 0;

                for (int i = 0; _openOrders != null && i < _openOrders.Count; i++)
                {
                    Order order = _openOrders[i];

                    if (order == null)
                    {
                        continue;
                    }

                    value += order.VolumeExecute;
                }

                return value;
            }
        }

        /// <summary>
        /// Number of contracts open per trade
        /// </summary>
        public decimal OpenVolume 
        {
            get
            {
                if (CloseOrders == null)
                {
                    decimal volume = 0;

                    for (int i = 0;_openOrders != null && i < _openOrders.Count; i++)
                    {
                        Order order = _openOrders[i];

                        if(order == null)
                        {
                            continue;
                        }

                        volume += _openOrders[i].VolumeExecute;
                    }
                    return volume;
                }

                decimal valueClose = 0;

                if (CloseOrders != null)
                {
                    for (int i = 0; i < CloseOrders.Count; i++)
                    {
                        Order order = CloseOrders[i];

                        if(order == null)
                        {
                            continue;
                        }

                        valueClose += order.VolumeExecute;
                    }
                }

                decimal volumeOpen = 0;

                for (int i = 0; _openOrders != null && i < _openOrders.Count; i++)
                {
                    Order order = _openOrders[i];

                    if (order == null)
                    {
                        continue;
                    }

                    volumeOpen += order.VolumeExecute;
                }

                decimal value = volumeOpen - valueClose;

                return value;
            }
        }

        /// <summary>
        /// Number of contracts awaiting opening
        /// </summary>
        public decimal WaitVolume
        {
            get
            {
                decimal volumeWait = 0;

                for (int i = 0; _openOrders != null && i < _openOrders.Count; i++)
                {
                    Order order = _openOrders[i];

                    if (order == null)
                    {
                        continue;
                    }

                    if (order.State == OrderStateType.Active ||
                        order.State == OrderStateType.Partial)
                    {
                        volumeWait += order.Volume - order.VolumeExecute;
                    }
                }

                return volumeWait;
            }
        }

        /// <summary>
        /// Position opening price
        /// </summary>
        public decimal EntryPrice
        {
            get
            {
                if (_openOrders == null ||
                    _openOrders.Count == 0)
                {
                    return 0;
                }

                decimal price = 0;
                decimal volume = 0;
                for (int i = 0; i < _openOrders.Count; i++)
                {
                    Order order = _openOrders[i];

                    if (order == null)
                    {
                        continue;
                    }

                    decimal volumeEx = order.VolumeExecute;
                    if (volumeEx != 0)
                    {
                        volume += volumeEx;
                        price += volumeEx * order.PriceReal;
                    }
                }
                if (volume == 0
                    && _openOrders[0] != null)
                {
                    return _openOrders[0].Price;
                }

                return price/volume;
            }
        }

        /// <summary>
        /// Position closing price
        /// </summary>
        public decimal ClosePrice
        {
            get
            {
                if (_closeOrders == null ||
                    _closeOrders.Count == 0)
                {
                    return 0;
                }

                decimal price = 0;
                decimal volume = 0;

                for (int i = 0; i < _closeOrders.Count; i++)
                {
                    Order order = _closeOrders[i];

                    if (order == null)
                    {
                        continue;
                    }

                    decimal volumeEx = order.VolumeExecute;
                    if (volumeEx != 0)
                    {
                        volume += order.VolumeExecute;
                        price += order.VolumeExecute * order.PriceReal;
                    }
                }
                if (volume == 0)
                {
                    return 0;
                }

                return price/volume;
            }
        }

        /// <summary>
        /// Position closing price in partial close
        /// </summary>
        private decimal ClosePriceInPartialClose(decimal curPrice)
        {
            if (_closeOrders == null ||
                _closeOrders.Count == 0)
            {
                return 0;
            }

            decimal price = 0;
            decimal volume = 0;

            for (int i = 0; i < _closeOrders.Count; i++)
            {
                Order order = _closeOrders[i];

                if (order == null)
                {
                    continue;
                }

                decimal volumeEx = order.VolumeExecute;
                if (volumeEx != 0)
                {
                    volume += order.VolumeExecute;
                    price += order.VolumeExecute * order.PriceReal;
                }
            }
            if (volume == 0)
            {
                return 0;
            }

            decimal openVol = OpenVolume;

            if(openVol != 0)
            {
                volume += openVol;
                price += openVol * curPrice;
            }

            return price / volume;
        }

        /// <summary>
        /// Multiplier for position analysis, used for the needs of the platform. IMPORTANT. Don't change the value.
        /// </summary>
        public decimal MultToJournal = 100;

        /// <summary>
        /// Check the incoming order for this transaction
        /// </summary>
        public void SetOrder(Order newOrder)
        {
            Order openOrder = null;
            if (_openOrders != null)
            {
                for (int i = 0; i < _openOrders.Count; i++)
                {
                    if(_openOrders[i] == null)
                    {
                        continue;
                    }

                    if (_openOrders[i].NumberUser == newOrder.NumberUser)
                    {
                        if ((State == PositionStateType.Done || State == PositionStateType.OpeningFail) 
                            &&
                            ((_openOrders[i].State == OrderStateType.Fail && newOrder.State == OrderStateType.Fail) ||
                            (_openOrders[i].State == OrderStateType.Cancel && newOrder.State == OrderStateType.Cancel)))
                        {
                            return;
                        }
                        openOrder = _openOrders[i];
                        break;
                    }
                }
            }

            if (openOrder != null)
            {
                if (newOrder.State == OrderStateType.Fail &&
                   (openOrder.State == OrderStateType.Partial
                   || openOrder.State == OrderStateType.Done
                   || openOrder.State == OrderStateType.Cancel))
                {// the order was definitely previously placed on the exchange
                 // and received the statuses executed.
                    return;
                }

                if (openOrder.State != OrderStateType.Done 
                    || openOrder.Volume != openOrder.VolumeExecute)    //AVP 
                {
                    openOrder.State = newOrder.State;     //AVP 
                }

                openOrder.NumberMarket = newOrder.NumberMarket;

                if (openOrder.TimeCallBack == DateTime.MinValue)
                {
                    openOrder.TimeCallBack = newOrder.TimeCallBack;
                }
                
                openOrder.TimeCancel = newOrder.TimeCancel;
  
                if (openOrder.MyTrades == null ||
                    openOrder.MyTrades.Count == 0)
                { // если трейдов ещё нет, допускается установка значение исполненного объёма по записи в ордере
                    //openOrder.VolumeExecute = newOrder.VolumeExecute;
                }

                if (openOrder.State == OrderStateType.Done 
                    && openOrder.TradesIsComing 
                    && OpenVolume != 0 && !CloseActive)
                {
                    State = PositionStateType.Open;
                }
                else if (newOrder.State == OrderStateType.Fail 
                    && newOrder.VolumeExecute == 0 
                    && OpenVolume == 0
                    && MaxVolume == 0
                    && CloseActive == false
                    && OpenActive == false)
                {
                    State = PositionStateType.OpeningFail;
                }
                else if (newOrder.State == OrderStateType.Cancel
                    && newOrder.VolumeExecute == 0 
                    && OpenVolume == 0
                    && MaxVolume == 0
                    && CloseActive == false
                    && OpenActive == false)
                {
                    State = PositionStateType.OpeningFail;
                }
                else if ((newOrder.State == OrderStateType.Cancel
                    || newOrder.State == OrderStateType.Fail)
                    && OpenVolume != 0)
                {
                    State = PositionStateType.Open;
                }
                else if (newOrder.State == OrderStateType.Done 
                    && OpenVolume == 0 
                    && CloseOrders != null 
                    && CloseOrders.Count > 0
                    && CloseActive == false
                    && OpenActive == false)
                {
                    State = PositionStateType.Done;
                }
                else if (OpenVolume == 0 
                    && CloseActive == false 
                    && OpenActive == false)
                {
                    State = PositionStateType.Done;
                }
            }

            Order closeOrder = null;

            if (CloseOrders != null)
            {
                for (int i = 0; i < CloseOrders.Count; i++)
                {
                    if(CloseOrders[i] == null)
                    {
                        continue;
                    }

                    if (CloseOrders[i].NumberUser == newOrder.NumberUser)
                    {
                        if (
                                (
                                (CloseOrders[i].State == OrderStateType.Fail && newOrder.State == OrderStateType.Fail) 
                                ||
                                (CloseOrders[i].State == OrderStateType.Cancel && newOrder.State == OrderStateType.Cancel)
                                )
                                &&
                                State == PositionStateType.ClosingFail)
                        {
                            return;
                        }
                        closeOrder = CloseOrders[i];

                        break;
                    }
                }
            }

            if (closeOrder != null)
            {
                if (closeOrder.State != OrderStateType.Done
                   || closeOrder.Volume != closeOrder.VolumeExecute)    //AVP 
                { 
                    closeOrder.State = newOrder.State;
                }

                closeOrder.NumberMarket = newOrder.NumberMarket;

                if (closeOrder.TimeCallBack == DateTime.MinValue)
                {
                    closeOrder.TimeCallBack = newOrder.TimeCallBack;
                }
                closeOrder.TimeCancel = newOrder.TimeCancel;

                if (closeOrder.MyTrades == null ||
                   closeOrder.MyTrades.Count == 0)
                { // если трейдов ещё нет, допускается установка значение исполненного объёма по записи в ордере
                    //closeOrder.VolumeExecute = newOrder.VolumeExecute;
                }

                if(OpenVolume == 0 
                    && CloseActive == false 
                    && OpenActive == false)
                {
                    State = PositionStateType.Done;
                }
                else if (closeOrder.State == OrderStateType.Fail 
                    && CloseActive == false
                    && OpenVolume != 0)
                {
                    //AlertMessageManager.ThrowAlert(null, "Fail", "");
                    State = PositionStateType.ClosingFail;
                }
                else if (closeOrder.State == OrderStateType.Cancel 
                    && CloseActive == false
                    && OpenVolume != 0)
                {
                    // if not fully closed and this is the last order in the closing orders
                    //AlertMessageManager.ThrowAlert(null, "Cancel", "");
                    State = PositionStateType.ClosingFail;
                }
                else if (closeOrder.State == OrderStateType.Done 
                    && OpenVolume < 0)
                {
                    State = PositionStateType.ClosingSurplus;
                }
            }

            if (State == PositionStateType.Done
                && CloseOrders != null)
            {
                CalculateProfitToPosition();
            }
        }

        /// <summary>
        /// calculates the values of the fields ProfitOperationPersent and ProfitOperationPunkt
        /// </summary>
        private void CalculateProfitToPosition()
        {
            decimal entryPrice = EntryPrice;
            decimal closePrice = ClosePrice;

            if (entryPrice != 0 && closePrice != 0)
            {
                if (Direction == Side.Buy)
                {
                    ProfitOperationPercent = closePrice / entryPrice * 100 - 100;
                    ProfitOperationAbs = closePrice - entryPrice;
                }
                else
                {
                    ProfitOperationAbs = entryPrice - closePrice;
                    ProfitOperationPercent = -(closePrice / entryPrice * 100 - 100);
                }
            }
        }

        /// <summary>
        /// Check incoming trade for this trade
        /// </summary>
        public void SetTrade(MyTrade trade)
        {
            _myTrades = null;

            if (_openOrders != null)
            {
                for (int i = 0; i < _openOrders.Count; i++)
                {
                    Order curOrdOpen = _openOrders[i];

                    if(curOrdOpen == null)
                    {
                        continue;
                    }

                    if (curOrdOpen.NumberMarket == trade.NumberOrderParent
                        && curOrdOpen.SecurityNameCode == trade.SecurityNameCode)
                    {
                        trade.NumberPosition = Number.ToString();
                        curOrdOpen.SetTrade(trade);

                        if (OpenVolume != 0 &&
                            State == PositionStateType.Opening)
                        {
                            State = PositionStateType.Open;
                        }
                        else if (OpenVolume == 0 
                            && OpenActive == false && CloseActive == false)
                        {
                            curOrdOpen.TimeDone = trade.Time;
                            State = PositionStateType.Done;
                        }
                    }
                }
            }

            if (_closeOrders != null)
            {
                for (int i = 0; i < _closeOrders.Count; i++)
                {
                    Order curOrdClose = _closeOrders[i];

                    if(curOrdClose == null)
                    {
                        continue;
                    }

                    if (curOrdClose.NumberMarket == trade.NumberOrderParent
                        && curOrdClose.SecurityNameCode == trade.SecurityNameCode)
                    {
                        trade.NumberPosition = Number.ToString();
                        curOrdClose.SetTrade(trade);

                        if (OpenVolume == 0
                            && OpenActive == false && CloseActive == false)
                        {
                            State = PositionStateType.Done;
                            curOrdClose.TimeDone = trade.Time;
                        }
                        else if (OpenVolume < 0)
                        {
                            State = PositionStateType.ClosingSurplus;
                        }
                    }
                }
            }

            if (State == PositionStateType.Done && CloseOrders != null)
            {
                CalculateProfitToPosition();
            }
        }

        /// <summary>
        /// Load bid with ask into the trade to recalculate the profit
        /// </summary>
        public void SetBidAsk(decimal bid, decimal ask)
        {
            if (State == PositionStateType.Open
                || State == PositionStateType.Closing
                || State == PositionStateType.ClosingFail)
            {
                if (_openOrders == null ||
                    _openOrders.Count == 0)
                {
                    return;
                }

                if (ClosePrice != 0
                    && OpenVolume == 0)
                {
                    return;
                }

                decimal entryPrice = EntryPrice;

                if(entryPrice == 0)
                {
                    return;
                }

                if (ClosePrice == 0)
                {
                    if (Direction == Side.Buy &&
                        bid != 0)
                    {
                        ProfitOperationPercent = bid / entryPrice * 100 - 100;
                        ProfitOperationAbs = bid - entryPrice;
                    }
                    else if (Direction == Side.Sell
                        && ask != 0)
                    {
                        ProfitOperationPercent = -(ask / entryPrice * 100 - 100);
                        ProfitOperationAbs = entryPrice - ask;
                    }
                }
                else
                {
                    decimal closePrice = 0;

                    if (Direction == Side.Buy &&
                       bid != 0)
                    {
                        closePrice = ClosePriceInPartialClose(bid);
                    }
                    else if (Direction == Side.Sell
                        && ask != 0)
                    {
                        closePrice = ClosePriceInPartialClose(ask);
                    }

                    if (entryPrice != 0 && closePrice != 0)
                    {
                        if (Direction == Side.Buy)
                        {
                            ProfitOperationPercent = closePrice / entryPrice * 100 - 100;
                            ProfitOperationAbs = closePrice - entryPrice;
                        }
                        else
                        {
                            ProfitOperationAbs = entryPrice - closePrice;
                            ProfitOperationPercent = -(closePrice / entryPrice * 100 - 100);
                        }
                    }
                }

            }
        }

        /// <summary>
        /// Take the string to save
        /// </summary>
        public StringBuilder GetStringForSave()
        {
            StringBuilder result = new StringBuilder();

            result.Append(Direction + "#");

            result.Append(State + "#");

            result.Append( NameBot + "#");

            result.Append(ProfitOperationPercent.ToString(new CultureInfo("ru-RU")) + "#");

            result.Append(ProfitOperationAbs.ToString(new CultureInfo("ru-RU")) + "#");

            if (OpenOrders == null)
            {
                result.Append("null" + "#");
            }
            else
            {
                for(int i = 0;i < OpenOrders.Count;i++)
                {
                    if (OpenOrders[i] == null)
                    {
                        continue;
                    }
                    result.Append(OpenOrders[i].GetStringForSave() + "^");
                }
                result.Append("#");
            }

            result.Append(Number + "#");

            result.Append(Comment + "#");

            result.Append(StopOrderIsActive + "#");
            result.Append(StopOrderPrice + "#");
            result.Append(StopOrderRedLine + "#");

            result.Append(ProfitOrderIsActive + "#");
            result.Append(ProfitOrderPrice + "#");

            result.Append(Lots + "#");
            result.Append(PriceStepCost + "#");
            result.Append(PriceStep + "#");
            result.Append(PortfolioValueOnOpenPosition + "#");

            result.Append(ProfitOrderRedLine + "#");
            result.Append(SignalTypeOpen + "#");
            result.Append(SignalTypeClose + "#");

            result.Append(CommissionValue + "#");
            result.Append(CommissionType);

            if (CloseOrders != null)
            {
                for (int i = 0; i < CloseOrders.Count; i++)
                {
                    if (CloseOrders[i] == null)
                    {
                        continue;
                    }
                    result.Append("#" + CloseOrders[i].GetStringForSave());
                }
            }

            result.Append("#" + StopIsMarket);
            result.Append("#" + ProfitIsMarket);
            result.Append("#" + SecurityName);

            return result;
        }

        /// <summary>
        /// Load trade from incoming line
        /// </summary>
        public void SetDealFromString(string save)
        {
            string[] arraySave = save.Split('#');

            Enum.TryParse(arraySave[0], true, out Direction);

            NameBot = arraySave[2];

            ProfitOperationPercent = arraySave[3].ToDecimal();

            ProfitOperationAbs = arraySave[4].ToDecimal();

            if(arraySave[5] == null)
            {
                return;
            }

            if (arraySave[5] != "null")
            {
                string[] ordersOpen = arraySave[5].Split('^');
                if (ordersOpen.Length != 1)
                {
                    _openOrders = new List<Order>();
                    for (int i = 0; i < ordersOpen.Length - 1; i++)
                    {
                        _openOrders.Add(new Order());
                        _openOrders[i].SetOrderFromString(ordersOpen[i]);
                    }
                }
            }

            Number = Convert.ToInt32(arraySave[6]);
            Comment = arraySave[7];

            StopOrderIsActive = Convert.ToBoolean(arraySave[8]);
            StopOrderPrice = arraySave[9].ToDecimal();
            StopOrderRedLine = arraySave[10].ToDecimal();

            ProfitOrderIsActive = Convert.ToBoolean(arraySave[11]);
            ProfitOrderPrice = arraySave[12].ToDecimal();

            Lots = arraySave[13].ToDecimal();
            PriceStepCost = arraySave[14].ToDecimal();
            PriceStep = arraySave[15].ToDecimal();
            PortfolioValueOnOpenPosition = arraySave[16].ToDecimal();

            ProfitOrderRedLine = arraySave[17].ToDecimal();

            SignalTypeOpen = arraySave[18];
            SignalTypeClose = arraySave[19];

            CommissionValue = arraySave[20].ToDecimal();
            Enum.TryParse(arraySave[21], out CommissionType);

            for (int i = 22; i < arraySave.Length - 3; i++)
            {
                if(i == arraySave.Length - 3)
                {
                    break;
                }
                string saveOrd = arraySave[i];

                if (saveOrd.Split('@').Length < 3)
                {
                    continue;
                }

                Order newOrder = new Order();
                newOrder.SetOrderFromString(saveOrd);
                AddNewCloseOrder(newOrder);
            }

            if(arraySave[arraySave.Length - 3] == "True" 
                || arraySave[arraySave.Length - 3] == "False"
                || arraySave[arraySave.Length - 3] == "true"
                || arraySave[arraySave.Length - 3] == "false")
            {
                StopIsMarket = Convert.ToBoolean(arraySave[arraySave.Length - 3]);
                ProfitIsMarket = Convert.ToBoolean(arraySave[arraySave.Length - 2]);
            }

            SecurityName = arraySave[arraySave.Length - 1];

            PositionStateType state;
            Enum.TryParse(arraySave[1], true, out state);
            State = state;
        }

        /// <summary>
        /// Position creation time
        /// </summary>
        public DateTime TimeCreate
        {
            get
            {
                if (_timeCreate == DateTime.MinValue &&
                    _openOrders != null
                    && _openOrders.Count > 0)
                {
                    _timeCreate = _openOrders[0].GetLastTradeTime();
                }

                return _timeCreate;
            }
        }

        private DateTime _timeCreate;

        /// <summary>
        /// Position closing time
        /// </summary>
        public DateTime TimeClose
        {
            get
            {
                if (CloseOrders != null 
                    && CloseOrders.Count != 0)
                {
                    for (int i = CloseOrders.Count-1; i > -1 && i < CloseOrders.Count; i--)
                    {
                        Order closeOrder = CloseOrders[i];

                        if(closeOrder == null)
                        {
                            continue;
                        }

                        if (closeOrder.State != OrderStateType.Done
                            && closeOrder.State != OrderStateType.Partial)
                        {
                            continue;
                        }

                        DateTime time = closeOrder.GetLastTradeTime();
                        if (time != DateTime.MinValue)
                        {
                            return time;
                        }
                    }
                }
                return TimeCreate;
            }
        }

        /// <summary>
        /// Position opening time. The time when the first transaction on our position passed on the exchange
        /// if the transaction is not open yet, it will return the time to create the position
        /// </summary>
        public DateTime TimeOpen
        {
            get
            {
                if (OpenOrders == null || OpenOrders.Count == 0)
                {
                    return TimeCreate;
                }

                DateTime timeOpen = DateTime.MaxValue;

                for (int i = 0; i < OpenOrders.Count; i++)
                {
                    if(OpenOrders[i] == null)
                    {
                        continue;
                    }
                    if (OpenOrders[i].TradesIsComing &&
                        OpenOrders[i].TimeExecuteFirstTrade < timeOpen)
                    {
                        timeOpen = OpenOrders[i].TimeExecuteFirstTrade;
                    }
                }

                if (timeOpen == DateTime.MaxValue)
                {
                    return TimeCreate;
                }

                return TimeCreate;
            }
        }

        public string PositionSpecification
        {
            get
            {
                string result = "";

                result += OsLocalization.Trader.Label225 + ": " + Number + ", " 
                    + OsLocalization.Trader.Label224 + ": " + State + ", "
                    + OsLocalization.Trader.Label228 + ": " + Direction + "\n";

                result += OsLocalization.Trader.Label102 + ": " + SecurityName + "\n";

                if(ProfitPortfolioAbs != 0)
                {
                    decimal profit = Math.Round(ProfitPortfolioAbs, 10);

                    result += OsLocalization.Trader.Label404 + ": " + profit.ToStringWithNoEndZero() + "\n";
                }
                
                if(State != PositionStateType.OpeningFail)
                {
                    decimal entryPrice = Math.Round(EntryPrice, 10);

                    result += OsLocalization.Trader.Label400 + ": " + entryPrice.ToStringWithNoEndZero();

                    if (State == PositionStateType.Done)
                    {
                        decimal closePrice = Math.Round(ClosePrice, 10);

                        result += ", " + OsLocalization.Trader.Label401 + ": " + closePrice.ToStringWithNoEndZero() + " ";
                    }

                    result += "\n";

                    result += OsLocalization.Trader.Label421 + ": " + TimeOpen.ToString(OsLocalization.CurCulture);

                    if (State == PositionStateType.Done)
                    {
                        result += ", " + OsLocalization.Trader.Label420 + ": " + TimeClose.ToString(OsLocalization.CurCulture) + " ";
                    }

                    result += "\n";


                }

                if (OpenVolume == 0)
                {
                    result += OsLocalization.Trader.Label402 + ": " + MaxVolume + "\n";
                }
                else
                {
                    result += OsLocalization.Trader.Label403 + ": " + OpenVolume + "\n";
                }

                if (string.IsNullOrEmpty(SignalTypeOpen) == false)
                {
                    result += OsLocalization.Trader.Label405 + ": " + SignalTypeOpen + "\n";
                }

                if (State == PositionStateType.Done
                    && string.IsNullOrEmpty(SignalTypeClose) == false)
                {
                    result += OsLocalization.Trader.Label406 + ": " + SignalTypeClose + "\n";
                }

                return result;
            }
        }

        // profit for the portfolio
        
        /// <summary>
        /// The amount of profit relative to the portfolio in percentage
        /// </summary>
        public decimal ProfitPortfolioPercent
        {
            get
            {
                if (PortfolioValueOnOpenPosition == 0)
                {
                    return 0;
                }

                return ProfitPortfolioAbs / PortfolioValueOnOpenPosition*100;
            }
        }

        /// <summary>
        /// Commission type for the position
        /// </summary>
        public CommissionType CommissionType;

        /// <summary>
        /// commission value
        /// </summary>
        public decimal CommissionValue;

        /// <summary>
        /// the amount of profit relative to the portfolio in absolute terms
        /// taking into account the commission and the price step
        /// </summary>
        public decimal ProfitPortfolioAbs
        {
            get
            {
                decimal volume = 0;

                for (int i = 0; _openOrders != null && i < _openOrders.Count; i++)
                {
                    if (_openOrders[i] == null)
                    {
                        continue;
                    }
                    volume += _openOrders[i].VolumeExecute;
                }

                if(PriceStepCost == 0)
                {
                    PriceStepCost = 1;
                }

                if (volume == 0 ||
                    PriceStepCost == 0 ||
                    MaxVolume == 0)
                {
                    return 0;
                }

                if (Lots == 0)
                {
                    Lots = 1;
                }

                if(ProfitOperationAbs == 0)
                {
                    CalculateProfitToPosition();
                }

                if (IsLotServer())
                {
                    if(PriceStep != 0)
                    {
                        return (ProfitOperationAbs / PriceStep) * PriceStepCost * MaxVolume * Lots - CommissionTotal();
                    }
                    else
                    {
                        return (ProfitOperationAbs) * PriceStepCost * MaxVolume * Lots - CommissionTotal();
                    }
                        
                }
                else if(PriceStep != 0)
                {
                    if (PriceStep != 0)
                    {
                        return (ProfitOperationAbs / PriceStep) * PriceStepCost * MaxVolume - CommissionTotal();
                    }
                    else
                    {
                        return (ProfitOperationAbs) * PriceStepCost * MaxVolume - CommissionTotal();
                    }
                }
                else
                {
                    return (ProfitOperationAbs) * PriceStepCost * MaxVolume - CommissionTotal();
                }
            }
        }

        /// <summary>
        /// Determines whether the exchange supports multiple securities in one lot.
        /// </summary>
        private bool IsLotServer()
        {
            if (OpenOrders != null && OpenOrders.Count > 0)
            {
                ServerType serverType = OpenOrders[0].ServerType;

                if(serverType == ServerType.Tester ||
                    serverType == ServerType.None ||
                    serverType == ServerType.Optimizer ||
                    serverType == ServerType.Miner)
                {
                    return true;
                }

                if(serverType == ServerType.QuikLua
                    || serverType == ServerType.Plaza)
                {
                    return true;
                }

                IServerPermission permission = ServerMaster.GetServerPermission(serverType);

                if(permission == null)
                {
                    return false;
                }

                return permission.IsUseLotToCalculateProfit;

            }
            return false;
        }

        /// <summary>
        /// The amount of total position's commission
        /// </summary>
        public decimal CommissionTotal()
        {
            decimal commissionTotal = 0;

            if (CommissionType != CommissionType.None && CommissionValue != 0)
            {
                decimal volume = MaxVolume;

                if(Lots != 0 
                    && IsLotServer())
                {
                    volume = volume * Lots;
                }

                if (CommissionType == CommissionType.Percent)
                {
                    if (EntryPrice != 0 && ClosePrice == 0)
                    {
                        commissionTotal = volume * EntryPrice * (CommissionValue / 100);
                    }
                    else if (EntryPrice != 0 && ClosePrice != 0)
                    {
                        commissionTotal = volume * EntryPrice * (CommissionValue / 100) +
                                          volume * ClosePrice * (CommissionValue / 100);
                    }
                }

                if (CommissionType == CommissionType.OneLotFix)
                {
                    if (EntryPrice != 0 && ClosePrice == 0)
                    {
                        commissionTotal = volume * CommissionValue;
                    }
                    else if (EntryPrice != 0 && ClosePrice != 0)
                    {
                        commissionTotal = volume * CommissionValue * 2;
                    }
                }
            }

            return commissionTotal;
        }

        /// <summary>
        /// The number of lots in one transaction
        /// </summary>
        public decimal Lots;

        /// <summary>
        /// Price step cost
        /// </summary>
        public decimal PriceStepCost;

        /// <summary>
        /// Price step
        /// </summary>
        public decimal PriceStep;

        /// <summary>
        /// Portfolio size at the time of opening the portfolio
        /// </summary>
        public decimal PortfolioValueOnOpenPosition;

        public string PortfolioName
        {
            get
            {
                if( OpenOrders!= null 
                    && OpenOrders.Count> 0)
                {
                    return OpenOrders[0].PortfolioNumber;
                }

                return null;
            }

        }

        #region Obsolete

        [Obsolete("Obsolete. Use ProfitPortfolioAbs")]
        public decimal ProfitPortfolioPunkt
        {
            get
            {
                return ProfitPortfolioAbs;
            }
        }

        [Obsolete("Obsolete. Use ProfitPortfolioPercent")]
        public decimal ProfitOperationPersent
        {
            get
            {
                return ProfitPortfolioPercent;
            }
        }

        [Obsolete("Obsolete. Use StopOrderIsActive")]
        public bool StopOrderIsActiv
        {
            get
            {
                return StopOrderIsActive;
            }
            set
            {
                StopOrderIsActive = value;
            }
        }

        [Obsolete("Obsolete. Use ProfitOrderIsActive")]
        public bool ProfitOrderIsActiv
        {
            get { return ProfitOrderIsActive; }
            set { ProfitOrderIsActive = value; }
        }

        [Obsolete("Obsolete. Use CloseActive")]
        public bool CloseActiv
        {
            get
            {
                return CloseActive;
            }
        }

        [Obsolete("Obsolete. Use OpenActive")]
        public bool OpenActiv
        {
            get
            {
                return OpenActive;
            }
        }

        #endregion
    }

    /// <summary>
    /// Way to open a deal
    /// </summary>
    public enum PositionOpenType
    {
        /// <summary>
        /// Bid at a certain price
        /// </summary>
        Limit,

        /// <summary>
        /// Application at any price
        /// </summary>
        Market,

        /// <summary>
        /// Iceberg application. Application consisting of several limit orders
        /// </summary>
        Iceberg
    }

    /// <summary>
    /// Transaction status
    /// </summary>
    public enum PositionStateType
    {
        /// <summary>
        /// None
        /// </summary>
        None,

        /// <summary>
        /// Opening
        /// </summary>
        Opening,

        /// <summary>
        /// Closed
        /// </summary>
        Done,

        /// <summary>
        /// Error
        /// </summary>
        OpeningFail,

        /// <summary>
        /// Opened
        /// </summary>
        Open,

        /// <summary>
        /// Closing
        /// </summary>
        Closing,

        /// <summary>
        /// Closing fail
        /// </summary>
        ClosingFail,

        /// <summary>
        /// Brute force during closing
        /// </summary>
        ClosingSurplus,

        /// <summary>
        /// Deleted
        /// </summary>
        Deleted
    }

    /// <summary>
    /// Transaction direction
    /// </summary>
    public enum Side
    {
        /// <summary>
        /// None
        /// </summary>
        None,

        /// <summary>
        /// Buy
        /// </summary>
        Buy,

        /// <summary>
        /// Sell
        /// </summary>
        Sell
    }

    /// <summary>
    /// Commission type
    /// </summary>
    public enum CommissionType
    {
        /// <summary>
        /// None
        /// </summary>
        None,

        /// <summary>
        /// In percentage terms
        /// </summary>
        Percent,

        /// <summary>
        /// Fixed value per lot
        /// </summary>
        OneLotFix
    }
}
