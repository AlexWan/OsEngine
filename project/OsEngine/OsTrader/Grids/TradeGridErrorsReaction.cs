/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using OsEngine.Entity;
using OsEngine.Language;
using OsEngine.Logging;
using OsEngine.Market;
using OsEngine.Market.Servers;
using System;
using System.Collections.Generic;
using System.Globalization;

namespace OsEngine.OsTrader.Grids
{
    public class TradeGridErrorsReaction
    {
        #region Service

        public TradeGridErrorsReaction(TradeGrid grid)
        {
            _myGrid = grid;
        }

        private TradeGrid _myGrid;

        public void Delete()
        {
            _myGrid = null;
        }

        public bool FailOpenOrdersReactionIsOn = true;

        public int FailOpenOrdersCountToReaction = 10;

        public int FailOpenOrdersCountFact;

        public bool FailCancelOrdersReactionIsOn = true;

        public int FailCancelOrdersCountToReaction = 10;

        public int FailCancelOrdersCountFact;

        public bool WaitOnStartConnectorIsOn = true;

        public int WaitSecondsOnStartConnector = 30;

        public bool ReduceOrdersCountInMarketOnNoFundsError = true;

        public bool RestoreCloseOrdersCountInMarketOnFundsRecovery = true;

        public string GetSaveString()
        {
            string result = "";

            result += FailOpenOrdersReactionIsOn + "@";
            result += "@";
            result += FailOpenOrdersCountToReaction + "@";

            result += "@";
            result += FailCancelOrdersCountToReaction + "@";
            result += FailCancelOrdersReactionIsOn + "@";

            result += WaitOnStartConnectorIsOn + "@";
            result += WaitSecondsOnStartConnector + "@";

            result += ReduceOrdersCountInMarketOnNoFundsError + "@";

            TradeGridNoFundsRecovery.State recovery;

            lock (_recoveryLocker)
            {
                recovery = _recovery;
            }

            result += TradeGridNoFundsRecovery.ToSaveSegments(recovery); // 9–10: target@backoff@

            result += "@";
            result += "@";
            result += "@"; // три пустых поля в резерв

            return result;
        }

        public void LoadFromString(string value)
        {
            try
            {
                string[] values = value.Split('@');

                FailOpenOrdersReactionIsOn = Convert.ToBoolean(values[0]);
                //Enum.TryParse(values[1], out FailOpenOrdersReaction);
                FailOpenOrdersCountToReaction = Convert.ToInt32(values[2]);
                //Enum.TryParse(values[3], out FailCancelOrdersReaction);
                FailCancelOrdersCountToReaction = Convert.ToInt32(values[4]);
                FailCancelOrdersReactionIsOn = Convert.ToBoolean(values[5]);

                try
                {
                    WaitOnStartConnectorIsOn = Convert.ToBoolean(values[6]);
                    WaitSecondsOnStartConnector = Convert.ToInt32(values[7]);
                    ReduceOrdersCountInMarketOnNoFundsError = Convert.ToBoolean(values[8]);
                }
                catch
                {
                    WaitOnStartConnectorIsOn = true;
                    WaitSecondsOnStartConnector = 30;
                    ReduceOrdersCountInMarketOnNoFundsError = true;
                }

                try
                {
                    // восстановление после возврата средств: переживаем перезапуск,
                    // отсчёт первой попытки — от текущего момента (A11)
                    TradeGridNoFundsRecovery.State loaded = TradeGridNoFundsRecovery.FromSaveSegments(
                        values[9],
                        values[10],
                        _myGrid == null ? 0 : _myGrid.MaxCloseOrdersInMarket,
                        DateTime.Now);

                    lock (_recoveryLocker)
                    {
                        _recovery = loaded;
                    }

                    if (loaded.Target > 0)
                    {
                        string message = "NoFunds recovery resumed. Target close orders in market: " + loaded.Target
                            + ". Next retry in " + loaded.BackoffSeconds + " s";
                        SendNewLogMessage(message, LogMessageType.System);
                    }
                }
                catch
                {
                    lock (_recoveryLocker)
                    {
                        _recovery = TradeGridNoFundsRecovery.NotArmed();
                    }
                }
            }
            catch (Exception e)
            {
                SendNewLogMessage(e.ToString(), LogMessageType.Error);
            }
        }

        #endregion

        #region Errors collect

        public void PositionClosingFailEvent(Position position)
        {
            try
            {
                if (position.CloseOrders == null
                 || position.CloseOrders.Count == 0)
                {
                    return;
                }

                Order lastOrder = position.CloseOrders[^1];

                if (lastOrder.State == OrderStateType.Fail)
                {
                    FailCancelOrdersCountFact++;
                }

                TryFindNoFundsError(position, false);
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        public void PositionOpeningFailEvent(Position position)
        {
            try
            {
                if (position.OpenOrders == null
                || position.OpenOrders.Count == 0)
                {
                    return;
                }

                Order lastOrder = position.OpenOrders[^1];

                if (lastOrder == null)
                {
                    return;
                }

                if (lastOrder.State == OrderStateType.Fail)
                {
                    FailOpenOrdersCountFact++;
                }

                TryFindNoFundsError(position, true);
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        private DateTime _lastResetTime;

        public bool TryResetErrorsAtStartOfDay(DateTime time)
        {
            if (_lastResetTime.Date == time.Date)
            {
                return false;
            }

            _lastResetTime = time;

            if (FailOpenOrdersCountFact != 0
                || FailCancelOrdersCountFact != 0)
            {
                FailOpenOrdersCountFact = 0;
                FailCancelOrdersCountFact = 0;
                return true;
            }

            return false;
        }

        #endregion

        #region No funds error reaction

        private void TryFindNoFundsError(Position position, bool isOpenOrder)
        {
            try
            {
                if (_myGrid == null)
                {
                    return;
                }

                if (_myGrid.Tab.StartProgram != StartProgram.IsOsTrader)
                {
                    return;
                }

                if (ReduceOrdersCountInMarketOnNoFundsError == false)
                {
                    return;
                }

                IServer server = _myGrid.Tab.Connector.MyServer;

                if (server.ServerType != ServerType.TInvest)
                {
                    return;
                }

                // ошибка берётся из самой упавшей заявки (коннектор кладёт туда
                // текст ответа брокера), а не из общего лога коннектора:
                // чужие роботы, другие бумаги и старые ошибки не срабатывают
                Order lastOrder = null;

                if (isOpenOrder == true)
                {
                    if (position.OpenOrders != null
                        && position.OpenOrders.Count > 0)
                    {
                        lastOrder = position.OpenOrders[position.OpenOrders.Count - 1];
                    }
                }
                else
                {
                    if (position.CloseOrders != null
                        && position.CloseOrders.Count > 0)
                    {
                        lastOrder = position.CloseOrders[position.CloseOrders.Count - 1];
                    }
                }

                if (lastOrder == null
                    || lastOrder.State != OrderStateType.Fail
                    || string.IsNullOrEmpty(lastOrder.Comment))
                {
                    return;
                }

                bool haveNoFundsError = lastOrder.Comment.Contains(OsLocalization.Market.Label301);

                if (haveNoFundsError == true)
                {
                    if (isOpenOrder == true
                        && _myGrid.MaxOpenOrdersInMarket > 1)
                    {
                        _myGrid.MaxOpenOrdersInMarket--;
                        _myGrid.Save();
                        _myGrid.RePaintGrid();

                        string message = "ERROR on open order. No money on deposit \n";
                        message += "Reduce open orders in market. " + "\n";
                        message += "New value open orders in market: " + _myGrid.MaxOpenOrdersInMarket;
                        SendNewLogMessage(message, LogMessageType.Error);
                    }
                    else if (isOpenOrder == false
                        && _myGrid.MaxCloseOrdersInMarket > 1)
                    {
                        int targetBeforeReduce = _myGrid.MaxCloseOrdersInMarket;

                        _myGrid.MaxCloseOrdersInMarket--;
                        _myGrid.Save();
                        _myGrid.RePaintGrid();
                        string message = "ERROR on close order. No money on deposit \n";
                        message += "Reduce close orders in market. " + "\n";
                        message += "New value close orders in market: " + _myGrid.MaxCloseOrdersInMarket;
                        SendNewLogMessage(message, LogMessageType.Error);

                        TryHandleCloseOrdersNoFunds(targetBeforeReduce);
                    }
                }
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        #endregion

        #region Close orders recovery after funds return

        // состояние восстановления MaxCloseOrdersInMarket после NoFunds-отказа:
        // вооружается при снижении, шагает +1 через выдержку, откатывается при
        // новом NoFunds. Поле читается/пишется с потока табы (события Fail)
        // и с потока сетки (TryProcessCloseOrdersRecovery) — только под лоером
        private TradeGridNoFundsRecovery.State _recovery = TradeGridNoFundsRecovery.NotArmed();

        private readonly object _recoveryLocker = new object();

        private void TryHandleCloseOrdersNoFunds(int targetBeforeReduce)
        {
            bool needToSave = false;
            string logMessage = null;

            lock (_recoveryLocker)
            {
                if (RestoreCloseOrdersCountInMarketOnFundsRecovery == false)
                {
                    if (_recovery.Target > 0)
                    {
                        _recovery = TradeGridNoFundsRecovery.Cancel(_recovery);
                        needToSave = true;
                    }
                }
                else if (_recovery.Target > 0)
                {
                    _recovery = TradeGridNoFundsRecovery.OnNoFunds(
                        _recovery, _myGrid.MaxCloseOrdersInMarket, DateTime.Now);
                    needToSave = true;
                    logMessage = "NoFunds recovery: step rolled back. Target close orders in market: "
                        + _recovery.Target + ". Next retry in " + _recovery.BackoffSeconds + " s";
                }
                else
                {
                    _recovery = TradeGridNoFundsRecovery.Arm(
                        targetBeforeReduce, _myGrid.MaxCloseOrdersInMarket, DateTime.Now);

                    if (_recovery.Target > 0)
                    {
                        needToSave = true;
                        logMessage = "NoFunds recovery armed. Target close orders in market: "
                            + _recovery.Target + ". First retry in " + _recovery.BackoffSeconds + " s";
                    }
                }
            }

            if (needToSave == true)
            {
                _myGrid.Save();
            }

            if (logMessage != null)
            {
                SendNewLogMessage(logMessage, LogMessageType.System);
            }
        }

        public void TryProcessCloseOrdersRecovery(DateTime now)
        {
            try
            {
                if (_myGrid == null
                    || _myGrid.Tab == null
                    || _myGrid.Tab.Connector == null)
                {
                    return;
                }

                bool isOsTrader = _myGrid.Tab.StartProgram == StartProgram.IsOsTrader;

                IServer server = _myGrid.Tab.Connector.MyServer;

                bool isTInvest = server != null && server.ServerType == ServerType.TInvest;

                bool needToSave = false;
                bool needToRepaint = false;
                string logMessage = null;
                LogMessageType logType = LogMessageType.System;

                lock (_recoveryLocker)
                {
                    if (_recovery.Target <= 0)
                    {
                        return;
                    }

                    // гейты проверяются на каждом шаге: смена сервера или выключение
                    // флагов в середине сессии отменяют восстановление (A12)
                    if (TradeGridNoFundsRecovery.GatesAllow(isOsTrader, isTInvest,
                            ReduceOrdersCountInMarketOnNoFundsError,
                            RestoreCloseOrdersCountInMarketOnFundsRecovery) == false)
                    {
                        _recovery = TradeGridNoFundsRecovery.Cancel(_recovery);
                        needToSave = true;
                        logMessage = "NoFunds recovery cancelled: settings or server changed";
                        logType = LogMessageType.System;
                    }
                    else if (TradeGridNoFundsRecovery.IsManualChange(_recovery, _myGrid.MaxCloseOrdersInMarket))
                    {
                        _recovery = TradeGridNoFundsRecovery.Cancel(_recovery);
                        needToSave = true;
                        logMessage = "NoFunds recovery cancelled: manual change. Close orders in market: "
                            + _myGrid.MaxCloseOrdersInMarket;
                        logType = LogMessageType.System;
                    }
                    else if (TradeGridNoFundsRecovery.ShouldStep(_recovery, _myGrid.MaxCloseOrdersInMarket,
                            now, isOsTrader, isTInvest) == true)
                    {
                        int newLimit = _myGrid.MaxCloseOrdersInMarket + 1;

                        if (newLimit > _recovery.Target)
                        {
                            newLimit = _recovery.Target;
                        }

                        _myGrid.MaxCloseOrdersInMarket = newLimit;
                        _recovery = TradeGridNoFundsRecovery.MarkStepTaken(_recovery, newLimit, now);

                        needToSave = true;
                        needToRepaint = true;

                        if (_recovery.Target == 0)
                        {
                            logMessage = "NoFunds recovery restored. Close orders in market: " + newLimit;
                        }
                        else
                        {
                            logMessage = "NoFunds recovery step. Close orders in market: " + newLimit
                                + ". Target: " + _recovery.Target
                                + ". Next retry in " + _recovery.BackoffSeconds + " s";
                        }
                    }
                }

                if (needToSave == true)
                {
                    _myGrid.Save();
                }

                if (needToRepaint == true)
                {
                    _myGrid.RePaintGrid();
                }

                if (logMessage != null)
                {
                    SendNewLogMessage(logMessage, logType);
                }
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        public void CancelCloseOrdersRecovery()
        {
            try
            {
                lock (_recoveryLocker)
                {
                    if (_recovery.Target > 0)
                    {
                        _recovery = TradeGridNoFundsRecovery.Cancel(_recovery);
                    }
                }
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        #endregion

        #region Logic on Errors reation

        public TradeGridRegime GetReactionOnErrors(TradeGrid grid)
        {
            if (FailOpenOrdersReactionIsOn == false
                && FailCancelOrdersReactionIsOn == false)
            {
                return TradeGridRegime.On;
            }

            if (FailOpenOrdersReactionIsOn == true)
            {
                if (FailOpenOrdersCountFact >= FailOpenOrdersCountToReaction)
                {
                    string message = "ERROR on open orders. \n";
                    message += "Errors count: " + FailOpenOrdersCountFact.ToString() + "\n";
                    message += "New regime: Off";
                    SendNewLogMessage(message, LogMessageType.Error);

                    return TradeGridRegime.Off;
                }
            }

            if (FailCancelOrdersReactionIsOn == true)
            {
                if (FailCancelOrdersCountFact >= FailCancelOrdersCountToReaction)
                {
                    string message = "ERROR on cancel orders. \n";
                    message += "Errors count: " + FailCancelOrdersCountFact.ToString() + "\n";
                    message += "New regime: Off";
                    SendNewLogMessage(message, LogMessageType.Error);

                    return TradeGridRegime.Off;
                }
            }

            return TradeGridRegime.On;
        }

        #endregion

        #region Logic awaiting on start connection

        public bool AwaitOnStartConnector(AServer server)
        {
            if (WaitOnStartConnectorIsOn == false)
            {
                return false;
            }

            if (WaitSecondsOnStartConnector <= 0)
            {
                return false;
            }

            if (server.LastStartServerTime.AddSeconds(WaitSecondsOnStartConnector) > DateTime.Now)
            {
                return true;
            }

            return false;
        }

        #endregion

        #region Log

        public void SendNewLogMessage(string message, LogMessageType type)
        {
            if (LogMessageEvent != null)
            {
                LogMessageEvent(message, type);
            }
            else if (type == LogMessageType.Error)
            {
                ServerMaster.SendNewLogMessage(message, type);
            }
        }

        public event Action<string, LogMessageType> LogMessageEvent;

        #endregion

    }

    /// <summary>
    /// Чистая машина состояния восстановления MaxCloseOrdersInMarket после
    /// NoFunds-отказа на TInvest: лестница +1 с выдержкой 300..3600 с,
    /// откат и удвоение выдержки при новом NoFunds. Время инжектируется,
    /// состояния неизменяемы — контракт зафиксирован тестами FEATURE-0134.
    /// </summary>
    public static class TradeGridNoFundsRecovery
    {
        public const int BaseBackoffSeconds = 300;

        public const int MaxBackoffSeconds = 3600;

        public struct State
        {
            public int Target;

            public int BackoffSeconds;

            public DateTime NextStepTime;

            public bool StepPending;

            public int ExpectedLimit;
        }

        public static State NotArmed()
        {
            State s = new State();
            s.BackoffSeconds = BaseBackoffSeconds;
            return s;
        }

        public static bool GatesAllow(bool isOsTrader, bool isTInvest, bool reduceEnabled, bool restoreEnabled)
        {
            return isOsTrader == true && isTInvest == true && reduceEnabled == true && restoreEnabled == true;
        }

        public static State Arm(int target, int limitAfterReduce, DateTime now)
        {
            if (target <= 1 || target <= limitAfterReduce)
            {
                return NotArmed();
            }

            State s = new State();
            s.Target = target;
            s.BackoffSeconds = BaseBackoffSeconds;
            s.NextStepTime = now.AddSeconds(BaseBackoffSeconds);
            s.ExpectedLimit = limitAfterReduce;
            return s;
        }

        public static State OnNoFunds(State s, int limitAfterReduce, DateTime now)
        {
            if (s.Target <= 0)
            {
                return NotArmed();
            }

            int previous = s.BackoffSeconds;

            if (previous < BaseBackoffSeconds)
            {
                previous = BaseBackoffSeconds;
            }

            State r = new State();
            r.Target = s.Target;
            r.BackoffSeconds = Math.Min(previous * 2, MaxBackoffSeconds);
            r.NextStepTime = now.AddSeconds(r.BackoffSeconds);
            r.ExpectedLimit = limitAfterReduce;
            return r;
        }

        public static bool ShouldStep(State s, int currentLimit, DateTime now, bool isOsTrader, bool isTInvest)
        {
            if (s.Target <= 0)
            {
                return false;
            }

            if (isOsTrader == false || isTInvest == false)
            {
                return false;
            }

            if (currentLimit >= s.Target)
            {
                return false;
            }

            return now >= s.NextStepTime;
        }

        public static State MarkStepTaken(State s, int newLimit, DateTime now)
        {
            if (s.Target <= 0)
            {
                return NotArmed();
            }

            // шаг не поднимает выше target: overshoot от вызывающего
            // трактуем как завершение восстановления (A3/A7)
            if (newLimit >= s.Target)
            {
                return NotArmed();
            }

            State r = new State();
            r.Target = s.Target;
            r.BackoffSeconds = s.BackoffSeconds;
            r.NextStepTime = now.AddSeconds(r.BackoffSeconds);
            r.StepPending = true;
            r.ExpectedLimit = newLimit;
            return r;
        }

        public static bool IsManualChange(State s, int currentLimit)
        {
            return s.Target > 0 && currentLimit != s.ExpectedLimit;
        }

        public static State Cancel(State s)
        {
            return NotArmed();
        }

        public static string ToSaveSegments(State s)
        {
            return s.Target.ToString(CultureInfo.InvariantCulture) + "@"
                 + s.BackoffSeconds.ToString(CultureInfo.InvariantCulture) + "@";
        }

        public static State FromSaveSegments(string slot9, string slot10, int currentLimit, DateTime now)
        {
            int target;

            if (int.TryParse(slot9, NumberStyles.Integer, CultureInfo.InvariantCulture, out target) == false
                || target <= 0)
            {
                return NotArmed();
            }

            int backoff;

            if (int.TryParse(slot10, NumberStyles.Integer, CultureInfo.InvariantCulture, out backoff) == false)
            {
                backoff = BaseBackoffSeconds;
            }

            if (backoff < BaseBackoffSeconds)
            {
                backoff = BaseBackoffSeconds;
            }

            if (backoff > MaxBackoffSeconds)
            {
                backoff = MaxBackoffSeconds;
            }

            if (target <= currentLimit)
            {
                return NotArmed();
            }

            State s = new State();
            s.Target = target;
            s.BackoffSeconds = backoff;
            s.NextStepTime = now.AddSeconds(backoff);
            s.ExpectedLimit = currentLimit;
            return s;
        }
    }
}
