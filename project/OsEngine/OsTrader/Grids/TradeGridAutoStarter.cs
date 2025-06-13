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
        #region Service

        public TradeGridAutoStartRegime AutoStartRegime;

        public decimal AutoStartPrice;

        public OnOffRegime RebuildGridRegime;

        public decimal ShiftFirstPrice;

        public string GetSaveString()
        {
            string result = "";

            result += AutoStartRegime + "@";
            result += AutoStartPrice + "@";
            result += RebuildGridRegime + "@";
            result += ShiftFirstPrice + "@";
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

                Enum.TryParse(values[2], out RebuildGridRegime);
                ShiftFirstPrice = values[3].ToDecimal();

            }
            catch (Exception e)
            {
                SendNewLogMessage(e.ToString(),LogMessageType.Error);
            }
        }

        #endregion

        #region Logic

        public bool HaveEventToStart(TradeGrid grid)
        {
            if(AutoStartRegime == TradeGridAutoStartRegime.Off)
            {
                return false;
            }

            List<Candle> candles = grid.Tab.CandlesAll;

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
                AutoStartRegime = TradeGridAutoStartRegime.Off;
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
                AutoStartRegime = TradeGridAutoStartRegime.Off;

                return true;
            }

            return false;
        }

        public decimal GetNewGridPriceStart(TradeGrid grid)
        {
            BotTabSimple tab = grid.Tab;

            List<Candle> candles = tab.CandlesAll;

            if(candles == null 
                || candles.Count == 0)
            {
                return 0;
            }

            decimal lastPrice = candles[^1].Close;

            if(lastPrice == 0)
            {
                return 0;
            }

            decimal result = lastPrice;

            if(ShiftFirstPrice != 0)
            {
                result = result + result * (ShiftFirstPrice / 100);

                result = tab.RoundPrice(result,tab.Security,grid.GridCreator.GridSide);
            }

            return result;
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

    public enum TradeGridAutoStartRegime
    {
        Off,
        HigherOrEqual,
        LowerOrEqual
    }
}
