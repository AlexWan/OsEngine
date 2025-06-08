/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using OsEngine.Entity;
using OsEngine.Logging;
using OsEngine.Market;
using System;


namespace OsEngine.OsTrader.Grids
{
    public class TrailingUp
    {
        public bool TrailingUpIsOn;

        public decimal TrailingUpStep;

        public decimal TrailingUpLimit;

        public bool TrailingDownIsOn;

        public decimal TrailingDownStep;

        public decimal TrailingDownLimit;

        public string GetSaveString()
        {
            string result = "";

            result += TrailingUpIsOn + "@";
            result += TrailingUpStep + "@";
            result += TrailingUpLimit + "@";

            result += TrailingDownIsOn + "@";
            result += TrailingDownStep + "@";
            result += TrailingDownLimit + "@";
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

                TrailingUpIsOn = Convert.ToBoolean(values[0]);
                TrailingUpStep = values[1].ToDecimal();
                TrailingUpLimit = values[2].ToDecimal();

                TrailingDownIsOn = Convert.ToBoolean(values[3]);
                TrailingDownStep = values[4].ToDecimal();
                TrailingDownLimit = values[5].ToDecimal();
            }
            catch (Exception e)
            {
                SendNewLogMessage(e.ToString(), LogMessageType.Error);
            }
        }

        public bool TryTrailingGrid(TradeGrid grid)
        {


            return false;
        }

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
