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
            SettingsPeriod1 = new NonTradePeriods(name);

            SettingsPeriod2 = new NonTradePeriods(name + "2");
        }

        public NonTradePeriods SettingsPeriod1;

        public NonTradePeriods SettingsPeriod2;

        public void ShowDialogPeriod1()
        {
            SettingsPeriod1.ShowDialog();
        }

        public void ShowDialogPeriod2()
        {
            SettingsPeriod2.ShowDialog();
        }

        public void Delete()
        {
            SettingsPeriod1.Delete();
            SettingsPeriod2.Delete();
        }

        public TradeGridRegime NonTradePeriod1Regime = TradeGridRegime.Off;

        public TradeGridRegime NonTradePeriod2Regime = TradeGridRegime.Off;

        public string GetSaveString()
        {
            string result = "";

            result += NonTradePeriod1Regime + "@";

            result += NonTradePeriod2Regime + "@";
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
                Enum.TryParse(values[1], out NonTradePeriod2Regime);
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
            if(SettingsPeriod1.CanTradeThisTime(curTime) == false)
            {
                return NonTradePeriod1Regime;
            }

            if (SettingsPeriod2.CanTradeThisTime(curTime) == false)
            {
                return NonTradePeriod2Regime;
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
