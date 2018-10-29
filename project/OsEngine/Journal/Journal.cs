/*
 *Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
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
    /// журнал
    /// </summary>
    public class Journal
    {

// сервис

        /// <summary>
        /// конструктор
        /// </summary>
        /// <param name="name">имя робота</param>
        /// <param name="startProgram">программа запрашивающая создание журнала</param>
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
        /// имя
        /// </summary>
        public readonly string Name;

        /// <summary>
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
        /// сохранить текущее состояние позиций
        /// </summary>
        public void Save()
        {
            _positionController.Save();
        }

// доступ к сделкам

        /// <summary>
        /// хранилище сделок
        /// </summary>
        private PositionController _positionController;

        /// <summary>
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
        /// польлзователь заказал манипулязию с позицией
        /// </summary>
        /// <param name="pos">позиция</param>
        /// <param name="signal">сигнал</param>
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
        /// узнать, храниться ли ордер в этом журнале
        /// </summary>
        public bool IsMyOrder(Order order)
        {
            List<Position> positions = AllPosition;

            if (positions == null)
            {
                return false;
            }

            for (int i = positions.Count - 1; i > -1; i--)
            {
                List<Order> openOrders = positions[i].OpenOrders;

                if (openOrders != null && openOrders.Find(order1 => order1.NumberUser == order.NumberUser) != null)
                {
                    return true;
                }
                List<Order> closingOrders = positions[i].CloseOrders;

                if (closingOrders != null && closingOrders.Find(order1 => order1.NumberUser == order.NumberUser) != null)
                {
                    return true;
                }
            }

            return false;
        }

// приём входящих данных

        /// <summary>
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
        /// сохранить входящую сделку
        /// </summary>
        public void SetNewDeal(Position position)
        {
            try
            {
                _positionController.SetNewPosition(position);
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        /// <summary>
        /// удалить позицию из хранилища
        /// </summary>
        public void DeletePosition(Position position)
        {
            try
            {
                _positionController.DeletePosition(position);
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        /// <summary>
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
        /// время когда последний раз перерисовывали прибыль на форме
        /// </summary>
        private DateTime _lastProfitUpdateTime = DateTime.MinValue;

        /// <summary>
        /// прогрузить новые данные по ценам
        /// </summary>
        public void SetNewBidAsk(decimal bid, decimal ask)
        {
            try
            {
                _positionController.SetBidAsk(bid, ask);

                if (_lastProfitUpdateTime == DateTime.MinValue ||
                    _lastProfitUpdateTime.AddSeconds(10) < DateTime.Now)
                {
                    _lastProfitUpdateTime = DateTime.Now;
                }
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

// прорисовка текстБокса для бота

        /// <summary>
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

// сообщения в лог 

        /// <summary>
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
        /// исходящее сообщение для лога
        /// </summary>
        public event Action<string, LogMessageType> LogMessageEvent;

// события исходящие

        /// <summary>
        /// изменился статус сделки
        /// </summary>
        public event Action<Position> PositionStateChangeEvent;

        /// <summary>
        /// изменился открытый объём по сделке
        /// </summary>
        public event Action<Position> PositionNetVolumeChangeEvent;

        /// <summary>
        /// пользователь выбрал во всплывающем меню некое действие
        /// </summary>
        public event Action<Position, SignalType> UserSelectActionEvent;
    }
}
