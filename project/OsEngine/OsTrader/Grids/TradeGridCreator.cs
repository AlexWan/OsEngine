/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System;
using System.Collections.Generic;
using OsEngine.Entity;
using OsEngine.Logging;
using OsEngine.Market;

namespace OsEngine.OsTrader.Grids
{
    public class TradeGridCreator
    {
        public Side GridSide = Side.Buy;

        public decimal FirstPrice;

        public int LineCountStart;

        public TradeGridValueType TypeStep;

        public decimal LineStep;

        public decimal StepMultiplicator = 1;

        public TradeGridValueType TypeProfit;

        public decimal ProfitStep;

        public decimal ProfitMultiplicator = 1;

        public TradeGridVolumeType TypeVolume;

        public decimal StartVolume = 1;

        public string TradeAssetInPortfolio = "Prime";

        public decimal MartingaleMultiplicator = 1;

        public List<GridBotClassicLine> Lines = new List<GridBotClassicLine>();

        public string GetSaveString()
        {
            string result = "";

            result += GridSide + "@";
            result += FirstPrice + "@";
            result += LineCountStart + "@";
            result += TypeStep + "@";
            result += LineStep + "@";
            result += StepMultiplicator + "@";
            result += TypeProfit + "@";
            result += ProfitStep + "@";
            result += ProfitMultiplicator + "@";
            result += TypeVolume + "@";
            result += StartVolume + "@";
            result += MartingaleMultiplicator + "@";
            result += TradeAssetInPortfolio + "@";
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

                Enum.TryParse(values[0], out GridSide);
                FirstPrice = values[1].ToDecimal();
                LineCountStart = Convert.ToInt32(values[2]);
                Enum.TryParse(values[3], out TypeStep);
                LineStep = values[4].ToDecimal();
                StepMultiplicator = values[5].ToDecimal();
                Enum.TryParse(values[6], out TypeProfit);
                ProfitStep = values[7].ToDecimal();
                ProfitMultiplicator = values[8].ToDecimal();
                Enum.TryParse(values[9], out TypeVolume);
                StartVolume = values[10].ToDecimal();
                MartingaleMultiplicator = values[11].ToDecimal();
                TradeAssetInPortfolio = values[12];


            }
            catch (Exception e)
            {
                //SendNewLogMessage(e.ToString(),LogMessageType.Error);
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
