/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using OsEngine.Logging;
using OsEngine.Market;
using System;

namespace OsEngine.OsTrader.Grids
{
    public class TradeGridNonTradeDays
    {
        #region Service

        public bool TradeInMonday = true;

        public bool TradeInTuesday = true;

        public bool TradeInWednesday = true;

        public bool TradeInThursday = true;

        public bool TradeInFriday = true;

        public bool TradeInSaturday = true;

        public bool TradeInSunday = true;

        public TradeGridRegime NonTradeDaysRegime;

        public string GetSaveString()
        {
            string result = "";

            result += TradeInMonday + "@";
            result += TradeInTuesday + "@";
            result += TradeInWednesday + "@";
            result += TradeInThursday + "@";
            result += TradeInFriday + "@";
            result += TradeInSaturday + "@";
            result += TradeInSunday + "@";
            result += NonTradeDaysRegime + "@";
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

                TradeInMonday = Convert.ToBoolean(values[0]);
                TradeInTuesday = Convert.ToBoolean(values[1]);
                TradeInWednesday = Convert.ToBoolean(values[2]);
                TradeInThursday = Convert.ToBoolean(values[3]);
                TradeInFriday = Convert.ToBoolean(values[4]);
                TradeInSaturday = Convert.ToBoolean(values[5]);
                TradeInSunday = Convert.ToBoolean(values[6]);
                Enum.TryParse(values[7], out NonTradeDaysRegime);
            }
            catch (Exception e)
            {
                SendNewLogMessage(e.ToString(), LogMessageType.Error);
            }
        }

        #endregion

        #region Logic

        public TradeGridRegime GetNonTradeDaysRegime(DateTime curTime)
        {
            if (TradeInMonday == false
                && curTime.DayOfWeek == DayOfWeek.Monday)
            {
                return NonTradeDaysRegime;
            }

            if (TradeInTuesday == false
                && curTime.DayOfWeek == DayOfWeek.Tuesday)
            {
                return NonTradeDaysRegime;
            }

            if (TradeInWednesday == false
                && curTime.DayOfWeek == DayOfWeek.Wednesday)
            {
                return NonTradeDaysRegime;
            }

            if (TradeInThursday == false
                && curTime.DayOfWeek == DayOfWeek.Thursday)
            {
                return NonTradeDaysRegime;
            }

            if (TradeInFriday == false
                && curTime.DayOfWeek == DayOfWeek.Friday)
            {
                return NonTradeDaysRegime;
            }

            if (TradeInSaturday == false
                && curTime.DayOfWeek == DayOfWeek.Saturday)
            {
                return NonTradeDaysRegime;
            }

            if (TradeInSunday == false
                && curTime.DayOfWeek == DayOfWeek.Sunday)
            {
                return NonTradeDaysRegime;
            }

            return TradeGridRegime.On;
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
