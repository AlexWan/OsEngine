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

        public TradeGridNonTradePeriods()
        {
            NonTradePeriod1Start = new TimeOfDay();

            NonTradePeriod1End = new TimeOfDay() { Hour = 7, Minute = 0 };

            NonTradePeriod2Start = new TimeOfDay() { Hour = 9, Minute = 0 };
            NonTradePeriod2End = new TimeOfDay() { Hour = 10, Minute = 5 };

            NonTradePeriod3Start = new TimeOfDay() { Hour = 13, Minute = 54 };
            NonTradePeriod3End = new TimeOfDay() { Hour = 14, Minute = 6 };

            NonTradePeriod4Start = new TimeOfDay() { Hour = 18, Minute = 40 };
            NonTradePeriod4End = new TimeOfDay() { Hour = 19, Minute = 5 };

            NonTradePeriod5Start = new TimeOfDay() { Hour = 23, Minute = 40 };
            NonTradePeriod5End = new TimeOfDay() { Hour = 23, Minute = 59 };

        }

        public bool NonTradePeriod1OnOff;
        public TimeOfDay NonTradePeriod1Start;
        public TimeOfDay NonTradePeriod1End;
        public TradeGridRegime NonTradePeriod1Regime = TradeGridRegime.Off; 

        public bool NonTradePeriod2OnOff;
        public TimeOfDay NonTradePeriod2Start;
        public TimeOfDay NonTradePeriod2End;
        public TradeGridRegime NonTradePeriod2Regime = TradeGridRegime.Off;

        public bool NonTradePeriod3OnOff;
        public TimeOfDay NonTradePeriod3Start;
        public TimeOfDay NonTradePeriod3End;
        public TradeGridRegime NonTradePeriod3Regime = TradeGridRegime.Off;

        public bool NonTradePeriod4OnOff;
        public TimeOfDay NonTradePeriod4Start;
        public TimeOfDay NonTradePeriod4End;
        public TradeGridRegime NonTradePeriod4Regime = TradeGridRegime.Off;

        public bool NonTradePeriod5OnOff;
        public TimeOfDay NonTradePeriod5Start;
        public TimeOfDay NonTradePeriod5End;
        public TradeGridRegime NonTradePeriod5Regime = TradeGridRegime.Off;

        public string GetSaveString()
        {
            string result = "";

            result += NonTradePeriod1OnOff + "@";
            result += NonTradePeriod1Start + "@";
            result += NonTradePeriod1End + "@";
            result += NonTradePeriod1Regime + "@";
            result += NonTradePeriod2OnOff + "@";
            result += NonTradePeriod2Start + "@";
            result += NonTradePeriod2End + "@";
            result += NonTradePeriod2Regime + "@";
            result += NonTradePeriod3OnOff + "@";
            result += NonTradePeriod3Start + "@";
            result += NonTradePeriod3End + "@";
            result += NonTradePeriod3Regime + "@";
            result += NonTradePeriod4OnOff + "@";
            result += NonTradePeriod4Start + "@";
            result += NonTradePeriod4End + "@";
            result += NonTradePeriod4Regime + "@";
            result += NonTradePeriod5OnOff + "@";
            result += NonTradePeriod5Start + "@";
            result += NonTradePeriod5End + "@";
            result += NonTradePeriod5Regime + "@";

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

                NonTradePeriod1OnOff = Convert.ToBoolean(values[0]);
                NonTradePeriod1Start.LoadFromString(values[1]);
                NonTradePeriod1End.LoadFromString(values[2]);
                Enum.TryParse(values[3], out NonTradePeriod1Regime);

                NonTradePeriod2OnOff = Convert.ToBoolean(values[4]);
                NonTradePeriod2Start.LoadFromString(values[5]);
                NonTradePeriod2End.LoadFromString(values[6]);
                Enum.TryParse(values[7], out NonTradePeriod2Regime);

                NonTradePeriod3OnOff = Convert.ToBoolean(values[8]);
                NonTradePeriod3Start.LoadFromString(values[9]);
                NonTradePeriod3End.LoadFromString(values[10]);
                Enum.TryParse(values[11], out NonTradePeriod3Regime);

                NonTradePeriod4OnOff = Convert.ToBoolean(values[12]);
                NonTradePeriod4Start.LoadFromString(values[13]);
                NonTradePeriod4End.LoadFromString(values[14]);
                Enum.TryParse(values[15], out NonTradePeriod4Regime);

                NonTradePeriod5OnOff = Convert.ToBoolean(values[16]);
                NonTradePeriod5Start.LoadFromString(values[17]);
                NonTradePeriod5End.LoadFromString(values[18]);
                Enum.TryParse(values[19], out NonTradePeriod5Regime);

            }
            catch (Exception e)
            {
                SendNewLogMessage(e.ToString(),LogMessageType.Error);
            }
        }

        #endregion

        #region Logic

        public TradeGridRegime GetNonTradePeriodsRegime(DateTime curTime)
        {
            if (NonTradePeriod1OnOff == true)
            {
                if (NonTradePeriod1Start < curTime
                 && NonTradePeriod1End > curTime)
                {
                    return NonTradePeriod1Regime;
                }
            }

            if (NonTradePeriod2OnOff == true)
            {
                if (NonTradePeriod2Start < curTime
                 && NonTradePeriod2End > curTime)
                {
                    return NonTradePeriod2Regime;
                }
            }

            if (NonTradePeriod3OnOff == true)
            {
                if (NonTradePeriod3Start < curTime
                 && NonTradePeriod3End > curTime)
                {
                    return NonTradePeriod3Regime;
                }
            }

            if (NonTradePeriod4OnOff == true)
            {
                if (NonTradePeriod4Start < curTime
                 && NonTradePeriod4End > curTime)
                {
                    return NonTradePeriod4Regime;
                }
            }

            if (NonTradePeriod5OnOff == true)
            {
                if (NonTradePeriod5Start < curTime
                 && NonTradePeriod5End > curTime)
                {
                    return NonTradePeriod5Regime;
                }
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
