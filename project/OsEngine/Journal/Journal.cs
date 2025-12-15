/*
 * Your rights to use code governed by this license http://o-s-a.net/doc/license_simple_engine.pdf
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System;
using System.Collections.Generic;
using System.Windows.Forms.Integration;
using OsEngine.Alerts;
using OsEngine.Entity;
using OsEngine.Journal.Internal;
using OsEngine.Logging;
using OsEngine.Market.Servers.GateIo.GateIoFutures.Entities.Response;

namespace OsEngine.Journal
{
    public class Journal
    {
        #region Service

        public Journal(string name, StartProgram startProgram)
        {
            Name = name;

            try
            {
                _positionController = new PositionController(name, startProgram);
                _positionController.PositionStateChangeEvent += _positionController_DealStateChangeEvent;
                _positionController.PositionNetVolumeChangeEvent += _positionController_PositionNetVolumeChangeEvent;
                _positionController.UserSelectActionEvent += _positionController_UserSelectActionEvent;
                _positionController.LogMessageEvent += SendNewLogMessage;
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        public readonly string Name;

        public void Delete()
        {
            try
            {
                if (_positionController != null)
                {
                    _positionController.Delete();
                    _positionController.PositionStateChangeEvent -= _positionController_DealStateChangeEvent;
                    _positionController.PositionNetVolumeChangeEvent -= _positionController_PositionNetVolumeChangeEvent;
                    _positionController.UserSelectActionEvent -= _positionController_UserSelectActionEvent;
                    _positionController.LogMessageEvent -= SendNewLogMessage;
                    _positionController = null;
                }
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        public void Clear()
        {
            try
            {
                if (_positionController == null)
                {
                    return;
                }
                _positionController.Clear();
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        public void Save()
        {
            if (_positionController == null)
            {
                return;
            }
            _positionController.Save();
        }

        public CommissionType CommissionType
        {
            get
            {
                if (_positionController == null)
                {
                    return CommissionType.None;
                }
                return _positionController.CommissionType;
            }
            set
            {
                if (_positionController == null)
                {
                    return;
                }
                _positionController.CommissionType = value;
            }
        }

        public decimal CommissionValue
        {
            get
            {
                if (_positionController == null)
                {
                    return 0;
                }
                return _positionController.CommissionValue;
            }
            set
            {
                if (_positionController == null)
                {
                    return;
                }
                _positionController.CommissionValue = value;
            }
        }

        #endregion

        #region Access to transactions

        private PositionController _positionController;

        public void NeedToUpdateStatePositions()
        {
            if (_positionController == null)
            {
                return;
            }
            _positionController.NeedToUpdateStatePositions();
        }

        public List<Position> OpenPositions
        {
            get
            {
                try
                {
                    if (_positionController == null)
                    {
                        return new List<Position>();
                    }
                    return _positionController.OpenPositions;
                }
                catch (Exception error)
                {
                    SendNewLogMessage(error.ToString(), LogMessageType.Error);
                }
                return null;
            }
        }

        public List<Position> AllPosition
        {
            get
            {
                try
                {
                    if (_positionController == null)
                    {
                        return new List<Position>();
                    }
                    return _positionController.AllPositions;
                }
                catch (Exception error)
                {
                    SendNewLogMessage(error.ToString(), LogMessageType.Error);
                }
                return null;
            }
        }

        public List<Position> CloseAllPositions
        {
            get
            {
                try
                {
                    if (_positionController == null)
                    {
                        return new List<Position>();
                    }

                    return _positionController.ClosePositions;
                }
                catch (Exception error)
                {
                    SendNewLogMessage(error.ToString(), LogMessageType.Error);
                }
                return null;
            }
        }

        public List<Position> OpenAllShortPositions
        {
            get
            {
                try
                {
                    if (_positionController == null)
                    {
                        return new List<Position>();
                    }
                    return _positionController.OpenShortPosition;
                }
                catch (Exception error)
                {
                    SendNewLogMessage(error.ToString(), LogMessageType.Error);
                }
                return null;
            }
        }

        public List<Position> OpenAllLongPositions
        {
            get
            {
                try
                {
                    if (_positionController == null)
                    {
                        return new List<Position>();
                    }
                    return _positionController.OpenLongPosition;
                }
                catch (Exception error)
                {
                    SendNewLogMessage(error.ToString(), LogMessageType.Error);
                }
                return null;
            }
        }

        public List<Position> CloseAllShortPositions
        {
            get
            {
                try
                {
                    if (_positionController == null)
                    {
                        return new List<Position>();
                    }
                    return _positionController.CloseShortPosition;
                }
                catch (Exception error)
                {
                    SendNewLogMessage(error.ToString(), LogMessageType.Error);
                }
                return null;
            }
        }

        public List<Position> CloseAllLongPositions
        {
            get
            {
                try
                {
                    if (_positionController == null)
                    {
                        return new List<Position>();
                    }
                    return _positionController.CloseLongPosition;
                }
                catch (Exception error)
                {
                    SendNewLogMessage(error.ToString(), LogMessageType.Error);
                }
                return null;
            }
        }

        public Position LastPosition
        {
            get
            {
                try
                {
                    if (_positionController == null)
                    {
                        return null;
                    }
                    return _positionController.LastPosition;
                }
                catch (Exception error)
                {
                    SendNewLogMessage(error.ToString(), LogMessageType.Error);
                }
                return null;
            }
        }

        public Position GetPositionForNumber(int number)
        {
            try
            {
                if (_positionController == null)
                {
                    return null;
                }
                return _positionController.GetPositionForNumber(number);
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
            return null;
        }

        public List<Order> GetLastOrdersToPositions(int count)
        {
            List<Order> orders = new List<Order>();

            List<Position> position = AllPosition;

            for (int i = position.Count - 1; i > -1; i--)
            {
                List<Order> openOrders = position[i].OpenOrders;
                List<Order> closeOrders = position[i].CloseOrders;

                if (openOrders != null && openOrders.Count != 0)
                {
                    orders.AddRange(openOrders);
                }

                if (closeOrders != null && closeOrders.Count != 0)
                {
                    orders.AddRange(closeOrders);
                }

                if (orders.Count > count)
                {
                    break;
                }
            }

            if (orders.Count > 1)
            { // Ура, пузырик!

                for (int i = 0; i < orders.Count; i++)
                {
                    for (int i2 = 1; i2 < orders.Count; i2++)
                    {
                        if (orders[i2] != null
                            && orders[i2 - 1] != null
                            && orders[i2].NumberUser < orders[i2 - 1].NumberUser)
                        {
                            Order order = orders[i2];
                            orders[i2] = orders[i2 - 1];
                            orders[i2 - 1] = order;
                        }
                    }
                }
            }

            return orders;
        }

        public decimal GetProfitFromThatDayInPercent()
        {
            try
            {
                if (_positionController == null)
                {
                    return 0;
                }

                List<Position> deals = _positionController.AllPositions;

                if (deals == null)
                {
                    return 0;
                }

                List<Position> dealsToDay = deals.FindAll(
                    position => position.OpenOrders[0].TimeCreate.Day == DateTime.Now.Day
                    );

                decimal profit = 0;

                for (int i = 0; i < dealsToDay.Count; i++)
                {
                    profit += dealsToDay[i].ProfitOperationPercent;
                }
                return profit;
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
            return 0;
        }

        private void _positionController_DealStateChangeEvent(Position position)
        {
            try
            {
                if (PositionStateChangeEvent != null)
                {
                    PositionStateChangeEvent(position);
                }
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        private void _positionController_PositionNetVolumeChangeEvent(Position position)
        {
            try
            {
                if (PositionNetVolumeChangeEvent != null)
                {
                    PositionNetVolumeChangeEvent(position);
                }
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        private void _positionController_UserSelectActionEvent(Position pos, SignalType signal)
        {
            try
            {
                if (UserSelectActionEvent != null)
                {
                    UserSelectActionEvent(pos, signal);
                }
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        public Order IsMyOrder(Order order)
        {
            // open positions. Look All

            List<Position> positionsOpen = this.OpenPositions;

            if (positionsOpen != null)
            {
                for (int i = positionsOpen.Count - 1; i > -1; i--)
                {
                    Position positionCurrent = positionsOpen[i];

                    if (positionCurrent == null)
                    {
                        continue;
                    }

                    if (positionCurrent.SecurityName != order.SecurityNameCode)
                    {
                        continue;
                    }

                    List<Order> openOrders = positionCurrent.OpenOrders;

                    for(int j = 0; openOrders != null && j < openOrders.Count; j++)
                    {
                        Order openOrder = openOrders[j];

                        if(openOrder == null)
                        {
                            continue;
                        }

                        if (openOrder.NumberUser == order.NumberUser
                         && string.IsNullOrEmpty(openOrder.NumberMarket))
                        {
                            return openOrder;
                        }
                        else if (openOrder.NumberUser == order.NumberUser
                          && openOrder.NumberMarket == order.NumberMarket)
                        {
                            return openOrder;
                        }
                    }

                    List<Order> closingOrders = positionCurrent.CloseOrders;

                    for (int j = 0; closingOrders != null && j < closingOrders.Count; j++)
                    {
                        Order closeOrder = closingOrders[j];

                        if (closeOrder == null)
                        {
                            continue;
                        }

                        if (closeOrder.NumberUser == order.NumberUser
                         && string.IsNullOrEmpty(closeOrder.NumberMarket))
                        {
                            return closeOrder;
                        }
                        else if (closeOrder.NumberUser == order.NumberUser
                          && closeOrder.NumberMarket == order.NumberMarket)
                        {
                            return closeOrder;
                        }
                    }
                }
            }

            // historical positions. Look last 100

            List<Position> positions = AllPosition;

            if (positions == null)
            {
                return null;
            }

            for (int i = positions.Count - 1; i > -1 && i > positions.Count - 100; i--)
            {
                Position positionCurrent = positions[i];

                if (positionCurrent == null)
                {
                    continue;
                }

                if (positionCurrent.SecurityName != order.SecurityNameCode)
                {
                    continue;
                }

                List<Order> openOrders = positionCurrent.OpenOrders;

                for (int j = 0; openOrders != null && j < openOrders.Count; j++)
                {
                    Order openOrder = openOrders[j];

                    if (openOrder == null)
                    {
                        continue;
                    }

                    if (openOrder.NumberUser == order.NumberUser
                    && string.IsNullOrEmpty(openOrder.NumberMarket))
                    {
                        return openOrder;
                    }
                    else if (openOrder.NumberUser == order.NumberUser
                      && openOrder.NumberMarket == order.NumberMarket)
                    {
                        return openOrder;
                    }
                }

                List<Order> closingOrders = positionCurrent.CloseOrders;

                for (int j = 0; closingOrders != null && j < closingOrders.Count; j++)
                {
                    Order closeOrder = closingOrders[j];

                    if (closeOrder == null)
                    {
                        continue;
                    }

                    if (closeOrder.NumberUser == order.NumberUser
                       && string.IsNullOrEmpty(closeOrder.NumberMarket))
                    {
                        return closeOrder;
                    }
                    else if (closeOrder.NumberUser == order.NumberUser
                      && closeOrder.NumberMarket == order.NumberMarket)
                    {
                        return closeOrder;
                    }
                }
            }

            return null;
        }

        #endregion

        #region Incoming data reception

        public void SetNewOrder(Order newOrder)
        {
            try
            {
                if (_positionController == null)
                {
                    return;
                }
                _positionController.SetUpdateOrderInPositions(newOrder);
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        public void SetNewDeal(Position position)
        {
            try
            {
                if (_positionController == null)
                {
                    return;
                }
                _positionController.SetNewPosition(position);
                if (PositionStateChangeEvent != null)
                {
                    PositionStateChangeEvent(position);
                }
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        public void DeletePosition(Position position)
        {
            try
            {
                if (_positionController == null)
                {
                    return;
                }
                position.State = PositionStateType.Deleted;

                _positionController.DeletePosition(position);
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        public bool SetNewMyTrade(MyTrade trade)
        {
            try
            {
                if (_positionController == null)
                {
                    return false;
                }
                return _positionController.SetNewTrade(trade);
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
            return false;
        }

        public void SetNewBidAsk(decimal bid, decimal ask)
        {
            try
            {
                if (_positionController == null)
                {
                    return;
                }
                _positionController.SetBidAsk(bid, ask);
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        public void SetStopLimits(List<PositionOpenerToStopLimit> stopLimits)
        {
            if (_positionController == null)
            {
                return;
            }

            _positionController.SetStopLimits(stopLimits);
        }

        public List<PositionOpenerToStopLimit> LoadStopLimits()
        {
            if (_positionController == null)
            {
                return null;
            }

            return _positionController.LoadStopLimits();
        }

        #endregion

        #region Interface drawing

        public void StartPaint(WindowsFormsHost dataGridOpenDeal,
            WindowsFormsHost dataGridCloseDeal)
        {
            try
            {
                if (_positionController == null)
                {
                    return;
                }
                _positionController.StartPaint(dataGridOpenDeal, dataGridCloseDeal);
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        public void StopPaint()
        {
            try
            {
                if (_positionController == null)
                {
                    return;
                }
                _positionController.StopPaint();
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        public void PaintPosition(Position position)
        {
            try
            {
                if (_positionController == null)
                {
                    return;
                }
                _positionController.ProcessPosition(position);
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        public bool CanShowToolStripMenu
        {
            get
            {
                return _positionController.CanShowToolStripMenu;
            }
            set
            {
                _positionController.CanShowToolStripMenu = value;
            }
        }


        #endregion

        #region Log 

        private void SendNewLogMessage(string message, LogMessageType type)
        {
            if (LogMessageEvent != null)
            {
                LogMessageEvent(message, type);
            }
            else if (type == LogMessageType.Error)
            {
                System.Windows.MessageBox.Show(message);
            }
        }

        public event Action<string, LogMessageType> LogMessageEvent;

        #endregion

        #region Outgoing events

        public event Action<Position> PositionStateChangeEvent;

        public event Action<Position> PositionNetVolumeChangeEvent;

        public event Action<Position, SignalType> UserSelectActionEvent;

        #endregion
    }
}