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
using OsEngine.Market;

namespace OsEngine.Journal
{
    /// <summary>
    /// journal
    /// журнал
    /// </summary>
    public class Journal
    {
        // service
        // сервис

        /// <summary>
        /// constructor
        /// конструктор
        /// </summary>
        /// <param name="name">robot name/имя робота</param>
        /// <param name="startProgram">program requesting the creation of a log/программа запрашивающая создание журнала</param>
        public Journal(string name, StartProgram startProgram)
        {
            Name = name;

            try
            {
                _positionController = new PositionController(name,startProgram);
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

        /// <summary>
        /// name
        /// имя
        /// </summary>
        public readonly string Name;

        /// <summary>
        /// delete
        /// удалить
        /// </summary>
        public void Delete()
        {
            try
            {
                _positionController.Delete();
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        /// <summary>
        /// clear the log of transactions
        /// очистить журнал от сделок
        /// </summary>
        public void Clear()
        {
            try
            {
                _positionController.Clear();
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        /// <summary>
        /// to keep the current state of positions
        /// сохранить текущее состояние позиций
        /// </summary>
        public void Save()
        {
            _positionController.Save();
        }

        /// <summary>
        /// тип комиссии для позиций
        /// </summary>
        public ComissionType ComissionType
        {
            get {return _positionController.ComissionType; }
            set { _positionController.ComissionType = value; }
        }

        /// <summary>
        /// размер комиссии
        /// </summary>
        public decimal ComissionValue
        {
            get { return _positionController.ComissionValue; }
            set { _positionController.ComissionValue = value; }
        }

        // access to transactions доступ к сделкам

        /// <summary>
        /// transaction repository
        /// хранилище сделок
        /// </summary>
        private PositionController _positionController;

        /// <summary>
        /// take out open deals
        /// взять открытые сделки
        /// </summary>
        public List<Position> OpenPositions
        {
            get
            {
                try
                {
                    return _positionController.OpenPositions;
                }
                catch (Exception error)
                {
                    SendNewLogMessage(error.ToString(), LogMessageType.Error);
                }
                return null;
            }
        }

        /// <summary>
        /// all transactions
        /// все сделки
        /// </summary>
        public List<Position> AllPosition
        {
            get
            {
                try
                {
                    return _positionController.AllPositions;
                }
                catch (Exception error)
                {
                    SendNewLogMessage(error.ToString(), LogMessageType.Error);
                }
                return null;
            }
        }

        /// <summary>
        /// to take all the closed deals
        /// взять все закрытые сделки
        /// </summary>
        public List<Position> CloseAllPositions
        {
            get
            {
                try
                {
                    return _positionController.ClosePositions;
                }
                catch (Exception error)
                {
                    SendNewLogMessage(error.ToString(), LogMessageType.Error);
                }
                return null;
            }
        }

        /// <summary>
        /// all open shorts
        /// все открытые шорты
        /// </summary>
        public List<Position> OpenAllShortPositions
        {
            get
            {
                try
                {
                    return _positionController.OpenShortPosition;
                }
                catch (Exception error)
                {
                    SendNewLogMessage(error.ToString(), LogMessageType.Error);
                }
                return null;
            }
        }

        /// <summary>
        /// all open longs
        /// все открытые лонги
        /// </summary>
        public List<Position> OpenAllLongPositions
        {
            get
            {
                try
                {
                    return _positionController.OpenLongPosition;
                }
                catch (Exception error)
                {
                    SendNewLogMessage(error.ToString(), LogMessageType.Error);
                }
                return null;
            }
        }

        /// <summary>
        /// all closed shorts
        /// все закрытые шорты
        /// </summary>
        public List<Position> CloseAllShortPositions
        {
            get
            {
                try
                {
                    return _positionController.CloseShortPosition;
                }
                catch (Exception error)
                {
                    SendNewLogMessage(error.ToString(), LogMessageType.Error);
                }
                return null;
            }
        }

        /// <summary>
        /// all closed longs
        /// все закрытые лонги
        /// </summary>
        public List<Position> CloseAllLongPositions
        {
            get
            {
                try
                {
                    return _positionController.CloseLongPosition;
                }
                catch (Exception error)
                {
                    SendNewLogMessage(error.ToString(), LogMessageType.Error);
                }
                return null;
            }
        }

        /// <summary>
        /// last position
        /// последняя позиция
        /// </summary>
        public Position LastPosition
        {
            get
            {
                try
                {
                    return _positionController.LastPosition;
                }
                catch (Exception error)
                {
                    SendNewLogMessage(error.ToString(), LogMessageType.Error);
                }
                return null;
            }
        }

        /// <summary>
        /// to get a deal on the number
        /// взять сделку по номеру
        /// </summary>
        public Position GetPositionForNumber(int number)
        {
            try
            {
                return _positionController.GetPositionForNumber(number);
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
            return null;
        }

        /// <summary>
        /// to take the profit for today.
        /// взять профит за сегодня
        /// </summary>
        /// <returns></returns>
        public decimal GetProfitFromThatDayInPersent()
        {
            try
            {
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
                    profit += dealsToDay[i].ProfitOperationPersent;
                }
                return profit;
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
            return 0;
        }

        /// <summary>
        /// Incoming event: transaction changes
        /// входящее событие: изменения сделки
        /// </summary>
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

        void _positionController_PositionNetVolumeChangeEvent(Position position)
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

        /// <summary>
        ///  the user has ordered the manipulation of the position
        /// польлзователь заказал манипулязию с позицией
        /// </summary>
        /// <param name="pos">position/позиция</param>
        /// <param name="signal">siganl/сигнал</param>
        void _positionController_UserSelectActionEvent(Position pos, SignalType signal)
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

        /// <summary>
        /// to see if the warrant was kept in this journal
        /// узнать, храниться ли ордер в этом журнале
        /// </summary>
        public Order IsMyOrder(Order order)
        {
            List<Position> positions = AllPosition;

            if (positions == null)
            {
                return null;
            }

            for (int i = positions.Count - 1; i > -1; i--)
            {
                List<Order> openOrders = positions[i].OpenOrders;

                if (openOrders != null && openOrders.Find(order1 => order1.NumberUser == order.NumberUser) != null)
                {
                    return openOrders.Find(order1 => order1.NumberUser == order.NumberUser);
                }
                List<Order> closingOrders = positions[i].CloseOrders;

                if (closingOrders != null && closingOrders.Find(order1 => order1.NumberUser == order.NumberUser) != null)
                {
                    return closingOrders.Find(order1 => order1.NumberUser == order.NumberUser);
                }
            }

            return null;
        }
        // / incoming data reception
        // приём входящих данных

        /// <summary>
        /// save an incoming order
        /// сохранить входящий ордер
        /// </summary>
        public void SetNewOrder(Order newOrder)
        {
            try
            {
                _positionController.SetUpdateOrderInPositions(newOrder);
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        /// <summary>
        /// save the incoming transaction
        /// сохранить входящую сделку
        /// </summary>
        public void SetNewDeal(Position position)
        {
            try
            {
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

        /// <summary>
        /// Remove the position from the storage
        /// удалить позицию из хранилища
        /// </summary>
        public void DeletePosition(Position position)
        {
            try
            {
                position.State = PositionStateType.Deleted;

                _positionController.DeletePosition(position);

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

        /// <summary>
        /// save the trade
        /// сохранить трейд
        /// </summary>
        public void SetNewMyTrade(MyTrade trade)
        {
            try
            {
                _positionController.SetNewTrade(trade);
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        /// <summary>
        /// upload new price data
        /// прогрузить новые данные по ценам
        /// </summary>
        public void SetNewBidAsk(decimal bid, decimal ask)
        {
            try
            {
                _positionController.SetBidAsk(bid, ask);
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }
        // Drawing the textBox for a bot
        // прорисовка текстБокса для бота

        /// <summary>
        /// to start drawing a journal
        /// начать прорисовывать журнал
        /// </summary>
        public void StartPaint(WindowsFormsHost dataGridOpenDeal,
            WindowsFormsHost dataGridCloseDeal)
        {
            try
            {
                _positionController.StartPaint(dataGridOpenDeal, dataGridCloseDeal);
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        /// <summary>
        /// to stop drawing the journal
        /// остановить прорисовывать журнал
        /// </summary>
        public void StopPaint()
        {
            try
            {
                _positionController.StopPaint();
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        /// <summary>
        /// redraw the position in the tables
        /// перерисовать позицию в таблицах
        /// </summary>
        public void PaintPosition(Position position)
        {
            try
            {
                _positionController.ProcesPosition(position);
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }
        // messages to the log 
        // сообщения в лог 

        /// <summary>
        /// send a new message to the top
        /// выслать новое сообщение на верх
        /// </summary>
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

        /// <summary>
        /// outgoing message for log
        /// исходящее сообщение для лога
        /// </summary>
        public event Action<string, LogMessageType> LogMessageEvent;
        // Outgoing events
        // события исходящие

        /// <summary>
        /// the status of the deal has changed.
        /// изменился статус сделки
        /// </summary>
        public event Action<Position> PositionStateChangeEvent;

        /// <summary>
        /// the open volume of the transaction has changed
        /// изменился открытый объём по сделке
        /// </summary>
        public event Action<Position> PositionNetVolumeChangeEvent;

        /// <summary>
        /// the user has selected an action in the pop-up menu
        /// пользователь выбрал во всплывающем меню некое действие
        /// </summary>
        public event Action<Position, SignalType> UserSelectActionEvent;
    }
}
