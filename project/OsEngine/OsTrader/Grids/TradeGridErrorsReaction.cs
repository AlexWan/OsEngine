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
            catch(Exception error)
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

                TryFindNoFundsError(position,true);
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        private DateTime _lastResetTime;

        public bool TryResetErrorsAtStartOfDay(DateTime time)
        {
            if(_lastResetTime.Date == time.Date)
            {
                return false;
            }

            _lastResetTime = time;

            if(FailOpenOrdersCountFact != 0 
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

                if(_myGrid.Tab.StartProgram != StartProgram.IsOsTrader)
                {
                    return;
                }

                if(ReduceOrdersCountInMarketOnNoFundsError == false)
                {
                    return;
                }

                IServer server = _myGrid.Tab.Connector.MyServer;

                if (server.ServerType != ServerType.TInvest)
                {
                    return;
                }

                AServer tInvest = (AServer)server;

                List<LogMessage> messages = tInvest.Log.LastErrorMessages;

                bool haveNoFundsError = false;

                for (int i = 0; i < messages.Count; i++)
                {
                    string message = messages[i].Message;

                    if(message.Contains(OsLocalization.Market.Label301))
                    {
                        haveNoFundsError = true;
                        break;
                    }
                }

                if(haveNoFundsError == true)
                {
                    if(isOpenOrder == true 
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
                    else if( isOpenOrder == false
                        && _myGrid.MaxCloseOrdersInMarket > 1)
                    {
                        _myGrid.MaxCloseOrdersInMarket--;
                        _myGrid.Save();
                        _myGrid.RePaintGrid();
                        string message = "ERROR on close order. No money on deposit \n";
                        message += "Reduce close orders in market. " + "\n";
                        message += "New value close orders in market: " + _myGrid.MaxCloseOrdersInMarket;
                        SendNewLogMessage(message, LogMessageType.Error);
                    }
                }
            }
            catch(Exception error)
            {
                SendNewLogMessage(error.ToString(),LogMessageType.Error);
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
                    message += "New regime: Off";
                    SendNewLogMessage(message, LogMessageType.Error);

                    return TradeGridRegime.Off;
                }
            }

            if(FailCancelOrdersReactionIsOn == true)
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
