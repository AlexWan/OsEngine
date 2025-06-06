/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using OsEngine.Entity;
using OsEngine.Logging;
using OsEngine.Market;
using System;
using System.Collections.Generic;

namespace OsEngine.OsTrader.Grids
{
    public class TradeGridStopAndProfit
    {
        public OnOffRegime ProfitRegime = OnOffRegime.Off;

        public TradeGridValueType ProfitValueType;

        public decimal ProfitValue;

        public OnOffRegime StopRegime = OnOffRegime.Off;

        public TradeGridValueType StopValueType;

        public decimal StopValue;

        public string GetSaveString()
        {
            string result = "";

            result += ProfitRegime + "@";
            result += ProfitValueType + "@";
            result += ProfitValue + "@";
            result += StopRegime + "@";
            result += StopValueType + "@";
            result += StopValue + "@";
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

                Enum.TryParse(values[0], out ProfitRegime);
                Enum.TryParse(values[1], out ProfitValueType);
                ProfitValue = values[2].ToDecimal();

                Enum.TryParse(values[3], out StopRegime);
                Enum.TryParse(values[4], out StopValueType);
                StopValue = values[5].ToDecimal();
            }
            catch (Exception e)
            {
                SendNewLogMessage(e.ToString(),LogMessageType.Error);
            }
        }

        public void Process(TradeGrid grid)
        {
            if(ProfitRegime == OnOffRegime.Off
                && StopRegime == OnOffRegime.Off)
            {
                return;
            }

            List<TradeGridLine> linesWithOpenPositions = grid.GetLinesWithOpenPosition();

            // 1 если уже есть позиции со статусом Done. Ничего не делаем

            for(int i = 0;i < linesWithOpenPositions.Count;i++)
            {



            }

            // 2 считаем среднюю цену входа

            decimal middleEntryPrice = 0;
            decimal allVolume = 0;

            for(int i = 0;i < linesWithOpenPositions.Count;i++)
            {
              



            }

            decimal profitPrice = 0;
            decimal stopPrice = 0;
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
