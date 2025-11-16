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
    public class TradeGridNonTradePeriods
    {
        #region Service

        public TradeGridNonTradePeriods(string name)
        {
            Settings = new NonTradePeriods(name);
        }

        public NonTradePeriods Settings;

        public void ShowDialog()
        {
            Settings.ShowDialog();
        }

        public void Delete()
        {
            Settings.Delete();
        }

        public TradeGridRegime NonTradePeriod1Regime = TradeGridRegime.Off;

        public string GetSaveString()
        {
            string result = "";

            result += NonTradePeriod1Regime + "@";

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
                Enum.TryParse(values[0], out NonTradePeriod1Regime);
            }
            catch (Exception e)
            {
                SendNewLogMessage(e.ToString(), LogMessageType.Error);
            }
        }

        #endregion

        #region Logic

        public TradeGridRegime GetNonTradePeriodsRegime(DateTime curTime)
        {
            if(Settings.CanTradeThisTime(curTime) == false)
            {
                return NonTradePeriod1Regime;
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
