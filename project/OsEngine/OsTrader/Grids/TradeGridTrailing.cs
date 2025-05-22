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
    public class TradeGridTrailing
    {
        public bool TrailingUpIsOn;

        public decimal TrailingUpLimitValue;

        public bool TrailingDownIsOn;

        public decimal TrailingDownLimitValue;


        public string GetSaveString()
        {
            string result = "";

            result += TrailingUpIsOn + "@";
            result += TrailingUpLimitValue + "@";
            result += TrailingDownIsOn + "@";
            result += TrailingDownLimitValue + "@";
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
                TrailingUpLimitValue = values[1].ToDecimal();
                TrailingDownIsOn = Convert.ToBoolean(values[2]);
                TrailingDownLimitValue = values[3].ToDecimal();


            }
            catch (Exception e)
            {
                SendNewLogMessage(e.ToString(),LogMessageType.Error);
            }
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
