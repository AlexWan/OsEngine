/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using OsEngine.Entity;
using OsEngine.Logging;
using OsEngine.Market;
using OsEngine.Market.Servers;
using OsEngine.OsTrader.Panels.Tab;
using System;

namespace OsEngine.OsTrader.Grids
{
    public class TradeGridErrorsReaction
    {
        #region Service

        public TradeGridErrorsReaction(TradeGrid grid)
        {
            _tab = grid.Tab;
            _tab.PositionOpeningFailEvent += _tab_PositionOpeningFailEvent;
            _tab.PositionClosingFailEvent += _tab_PositionClosingFailEvent;
        }

        public void Delete()
        {
            _tab.PositionOpeningFailEvent -= _tab_PositionOpeningFailEvent;
            _tab.PositionClosingFailEvent -= _tab_PositionClosingFailEvent;
            _tab = null;
        }

        public bool FailOpenOrdersReactionIsOn = true;

        public TradeGridRegime FailOpenOrdersReaction = TradeGridRegime.Off;

        public int FailOpenOrdersCountToReaction = 10;

        public int FailOpenOrdersCountFact;

        public bool FailCancelOrdersReactionIsOn = true;

        public TradeGridRegime FailCancelOrdersReaction = TradeGridRegime.Off;

        public int FailCancelOrdersCountToReaction = 10;

        public int FailCancelOrdersCountFact;

        public bool WaitOnStartConnectorIsOn = true;

        public int WaitSecondsOnStartConnector = 30;

        public string GetSaveString()
        {
            string result = "";

            result += FailOpenOrdersReactionIsOn + "@";
            result += FailOpenOrdersReaction + "@";
            result += FailOpenOrdersCountToReaction + "@";

            result += FailCancelOrdersReaction + "@";
            result += FailCancelOrdersCountToReaction + "@";
            result += FailCancelOrdersReactionIsOn + "@";

            result += WaitOnStartConnectorIsOn + "@";
            result += WaitSecondsOnStartConnector + "@";

            result += "@";
            result += "@";
            result += "@";
            result += "@";
            result += "@"; // пять пустых полей в резерв

            return result;
        }

        public void LoadFromString(string value)
        {
            try
            {
                string[] values = value.Split('@');

                FailOpenOrdersReactionIsOn = Convert.ToBoolean(values[0]);
                Enum.TryParse(values[1], out FailOpenOrdersReaction);
                FailOpenOrdersCountToReaction = Convert.ToInt32(values[2]);
                Enum.TryParse(values[3], out FailCancelOrdersReaction);
                FailCancelOrdersCountToReaction = Convert.ToInt32(values[4]);
                FailCancelOrdersReactionIsOn = Convert.ToBoolean(values[5]);

                try
                {
                    WaitOnStartConnectorIsOn = Convert.ToBoolean(values[6]);
                    WaitSecondsOnStartConnector = Convert.ToInt32(values[7]);
                }
                catch
                {
                    WaitOnStartConnectorIsOn = true;
                    WaitSecondsOnStartConnector = 30;
                }
            }
            catch (Exception e)
            {
                SendNewLogMessage(e.ToString(), LogMessageType.Error);
            }
        }

        #endregion

        #region Errors collect

        private BotTabSimple _tab;

        private void _tab_PositionClosingFailEvent(Position position)
        {
            if(position.CloseOrders == null
                || position.CloseOrders.Count == 0)
            {
                return;
            }

            Order lastOrder = position.CloseOrders[^1];

            if(lastOrder.State == OrderStateType.Fail)
            {
                FailCancelOrdersCountFact++;
            }
        }

        private void _tab_PositionOpeningFailEvent(Position position)
        {
            if (position.OpenOrders == null
                || position.OpenOrders.Count == 0)
            {
                return;
            }

            Order lastOrder = position.OpenOrders[^1];

            if(lastOrder == null)
            {
                return;
            }

            if (lastOrder.State == OrderStateType.Fail)
            {
                FailOpenOrdersCountFact++;
            }
        }

        #endregion

        #region Logic on Errors reation

        public TradeGridRegime GetReactionOnErrors(TradeGrid grid)
        {
            if(FailOpenOrdersReactionIsOn == false 
                && FailCancelOrdersReactionIsOn == false)
            {
                return TradeGridRegime.On;
            }

            if(FailOpenOrdersReactionIsOn == true)
            {
                if(FailOpenOrdersCountFact >= FailOpenOrdersCountToReaction)
                {
                    string message = "ERROR on open orders. \n";
                    message += "Errors count: " + FailOpenOrdersCountFact.ToString() + "\n";
                    message += "New regime: " + FailOpenOrdersReaction ;
                    SendNewLogMessage(message, LogMessageType.Error);

                    return FailOpenOrdersReaction;
                }
            }

            if(FailCancelOrdersReactionIsOn == true)
            {
                if (FailCancelOrdersCountFact >= FailCancelOrdersCountToReaction)
                {
                    string message = "ERROR on cancel orders. \n";
                    message += "Errors count: " + FailCancelOrdersCountFact.ToString() + "\n";
                    message += "New regime: " + FailCancelOrdersReaction;
                    SendNewLogMessage(message, LogMessageType.Error);

                    return FailCancelOrdersReaction;
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

            if(WaitSecondsOnStartConnector <= 0)
            {
                return false;
            }

            if(server.LastStartServerTime.AddSeconds(WaitSecondsOnStartConnector) > DateTime.Now)
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
}
