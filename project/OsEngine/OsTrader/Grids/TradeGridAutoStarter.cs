/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using OsEngine.Entity;
using OsEngine.Logging;
using OsEngine.Market;
using OsEngine.OsTrader.Panels.Tab;
using System;
using System.Collections.Generic;

namespace OsEngine.OsTrader.Grids
{
    public class TradeGridAutoStarter
    {
        public TradeGridAutoStartRegime AutoStartRegime;

        public decimal AutoStartPrice;

        public string GetSaveString()
        {
            string result = "";

            result += AutoStartRegime + "@";
            result += AutoStartPrice + "@";
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

                // auto start
                Enum.TryParse(values[0], out AutoStartRegime);
                AutoStartPrice = values[1].ToDecimal();

                // other

               

            }
            catch (Exception e)
            {
                SendNewLogMessage(e.ToString(),LogMessageType.Error);
            }
        }

        public bool HaveEventToStart(TradeGrid grid, BotTabSimple tab)
        {
            if(AutoStartRegime == TradeGridAutoStartRegime.Off)
            {
                return false;
            }

            List<Candle> candles = tab.CandlesAll;

            if(candles == null 
                || candles.Count == 0)
            {
                return false;
            }

            decimal price = candles[candles.Count - 1].Close;

            if(price == 0)
            {
                return false;
            }

            if(AutoStartRegime == TradeGridAutoStartRegime.HigherOrEqual
                && price >= AutoStartPrice)
            {
                string message = "Auto-start grid. \n";
                message += "Auto-starter regime: " + AutoStartRegime.ToString() + "\n";
                message += "Auto-starter price: " + AutoStartPrice + "\n";
                message += "Market price: " + price;

                SendNewLogMessage(message, LogMessageType.Signal);
                return true;
            }
            else if(AutoStartRegime == TradeGridAutoStartRegime.LowerOrEqual
                && price <= AutoStartPrice)
            {
                string message = "Auto-start grid. \n";
                message += "Auto-starter regime: " + AutoStartRegime.ToString() + "\n";
                message += "Auto-starter price: " + AutoStartPrice + "\n";
                message += "Market price: " + price;
                SendNewLogMessage(message, LogMessageType.Signal);

                return true;
            }

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

    public enum TradeGridAutoStartRegime
    {
        Off,
        HigherOrEqual,
        LowerOrEqual
    }
}
