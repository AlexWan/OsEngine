/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using OsEngine.Entity;
using OsEngine.Language;
using OsEngine.Logging;
using OsEngine.Market;
using OsEngine.OsTrader.Panels.Tab;
using System;
using System.Collections.Generic;

namespace OsEngine.OsTrader.Grids
{
    public class TradeGridStopBy
    {
        #region Service

        public bool StopGridByMoveUpIsOn = false;
        public decimal StopGridByMoveUpValuePercent = 2.5m;
        public TradeGridRegime StopGridByMoveUpReaction = TradeGridRegime.CloseForced;

        public bool StopGridByMoveDownIsOn = false;
        public decimal StopGridByMoveDownValuePercent = 2.5m;
        public TradeGridRegime StopGridByMoveDownReaction = TradeGridRegime.CloseForced;

        public bool StopGridByPositionsCountIsOn = false;
        public int StopGridByPositionsCountValue = 200;
        public TradeGridRegime StopGridByPositionsCountReaction = TradeGridRegime.CloseForced;

        public bool StopGridByLifeTimeIsOn = false;
        public int StopGridByLifeTimeSecondsToLife = 600;
        public TradeGridRegime StopGridByLifeTimeReaction = TradeGridRegime.CloseForced;

        public bool StopGridByTimeOfDayIsOn = false;
        public int StopGridByTimeOfDayHour = 14;
        public int StopGridByTimeOfDayMinute = 15;
        public int StopGridByTimeOfDaySecond = 0;
        public TradeGridRegime StopGridByTimeOfDayReaction = TradeGridRegime.CloseForced;

        // цена 0 = условие выключено, старых конфигов этих полей просто нет
        public decimal StopGridByPriceUpValue = 0;
        public TradeGridRegime StopGridByPriceUpReaction = TradeGridRegime.CloseForced;

        public decimal StopGridByPriceDownValue = 0;
        public TradeGridRegime StopGridByPriceDownReaction = TradeGridRegime.CloseForced;

        public string GetSaveString()
        {
            string result = "";

            result += StopGridByMoveUpIsOn + "@";
            result += StopGridByMoveUpValuePercent + "@";
            result += StopGridByMoveUpReaction + "@";

            result += StopGridByMoveDownIsOn + "@";
            result += StopGridByMoveDownValuePercent + "@";
            result += StopGridByMoveDownReaction + "@";

            result += StopGridByPositionsCountIsOn + "@";
            result += StopGridByPositionsCountValue + "@";
            result += StopGridByPositionsCountReaction + "@";

            result += StopGridByLifeTimeIsOn + "@";
            result += StopGridByLifeTimeSecondsToLife + "@";
            result += StopGridByLifeTimeReaction + "@";

            result += StopGridByTimeOfDayIsOn + "@";
            result += StopGridByTimeOfDayHour + "@";
            result += StopGridByTimeOfDayMinute + "@";
            result += StopGridByTimeOfDaySecond + "@";
            result += StopGridByTimeOfDayReaction + "@";

            result += StopGridByPriceUpValue + "@";
            result += StopGridByPriceUpReaction + "@";
            result += StopGridByPriceDownValue + "@";
            result += StopGridByPriceDownReaction + "@";
            result += "@"; // пустой резервный слот

            return result;
        }

        public void LoadFromString(string value)
        {
            try
            {
                string[] values = value.Split('@');

                // stop grid by event

                StopGridByMoveUpIsOn = Convert.ToBoolean(values[0]);
                StopGridByMoveUpValuePercent = values[1].ToDecimal();
                Enum.TryParse(values[2], out StopGridByMoveUpReaction);

                StopGridByMoveDownIsOn = Convert.ToBoolean(values[3]);
                StopGridByMoveDownValuePercent = values[4].ToDecimal();
                Enum.TryParse(values[5], out StopGridByMoveDownReaction);

                StopGridByPositionsCountIsOn = Convert.ToBoolean(values[6]);
                StopGridByPositionsCountValue = Convert.ToInt32(values[7]);
                Enum.TryParse(values[8], out StopGridByPositionsCountReaction);

                StopGridByLifeTimeIsOn = Convert.ToBoolean(values[9]);
                StopGridByLifeTimeSecondsToLife = Convert.ToInt32(values[10]);
                Enum.TryParse(values[11], out StopGridByLifeTimeReaction);

                StopGridByTimeOfDayIsOn = Convert.ToBoolean(values[12]);
                StopGridByTimeOfDayHour = Convert.ToInt32(values[13]);
                StopGridByTimeOfDayMinute = Convert.ToInt32(values[14]);
                StopGridByTimeOfDaySecond = Convert.ToInt32(values[15]);
                Enum.TryParse(values[16], out StopGridByTimeOfDayReaction);

                // слоты 17..20 добавлены позже остальных, в старых файлах их нет или они пусты.
                // Битый текст цены -> 0 (ToDecimal-конвенция), условие выключено.
                // Пустой слот реакции не перечитывается (остаётся начальное CloseForced);
                // битый непустой текст реакции -> 0 (Off), так работает Enum.TryParse

                if (values.Length > 17
                    && string.IsNullOrEmpty(values[17]) == false)
                {
                    StopGridByPriceUpValue = values[17].ToDecimal();
                }
                else
                {
                    StopGridByPriceUpValue = 0;
                }

                if (values.Length > 18
                    && string.IsNullOrEmpty(values[18]) == false)
                {
                    Enum.TryParse(values[18], out StopGridByPriceUpReaction);
                }

                if (values.Length > 19
                    && string.IsNullOrEmpty(values[19]) == false)
                {
                    StopGridByPriceDownValue = values[19].ToDecimal();
                }
                else
                {
                    StopGridByPriceDownValue = 0;
                }

                if (values.Length > 20
                    && string.IsNullOrEmpty(values[20]) == false)
                {
                    Enum.TryParse(values[20], out StopGridByPriceDownReaction);
                }
            }
            catch (Exception e)
            {
                SendNewLogMessage(e.ToString(),LogMessageType.Error);
            }
        }

        #endregion

        #region Logic

        /// <summary>
        /// Срез данных сетки и вкладки, нужный условиям авто-стопа. Собран BuildStopContext
        /// </summary>
        public struct StopByContext
        {
            public int OpenPositionsCount;
            public decimal FirstGridPrice;
            public DateTime FirstTradeTime;
            public bool HasCandles;
            public decimal LastPrice;
            public DateTime ServerTime;
        }

        public TradeGridRegime GetRegime(TradeGrid grid, BotTabSimple tab)
        {
            if (AllStopConditionsAreOff() == true)
            {
                return TradeGridRegime.On;
            }

            return GetRegime(BuildStopContext(grid, tab));
        }

        /// <summary>
        /// Единственное место, читающее grid и tab. Виртуален для подмены в тестах
        /// </summary>
        protected virtual StopByContext BuildStopContext(TradeGrid grid, BotTabSimple tab)
        {
            StopByContext ctx = new StopByContext();

            ctx.OpenPositionsCount = grid.OpenPositionsCount;
            ctx.FirstGridPrice = grid.FirstPriceReal;
            ctx.FirstTradeTime = grid.FirstTradeTime;

            List<Candle> candles = tab.CandlesAll;
            ctx.HasCandles = candles != null
                && candles.Count > 0;

            ctx.LastPrice = 0;

            if (ctx.HasCandles == true)
            {
                ctx.LastPrice = candles[candles.Count - 1].Close;
            }

            ctx.ServerTime = tab.TimeServerCurrent;

            return ctx;
        }

        public TradeGridRegime GetRegime(StopByContext ctx)
        {
            if (AllStopConditionsAreOff() == true)
            {
                return TradeGridRegime.On;
            }

            // 1 смена режима по кол-ву закрытых позиций
            if (StopGridByPositionsCountIsOn == true)
            {
                int openPositionsCount = ctx.OpenPositionsCount;

                if (openPositionsCount >= StopGridByPositionsCountValue)
                {
                    string message = "Auto-stop grid by positions count. \n";
                    message += "Open positions in grid: " + openPositionsCount + "\n";
                    message += "Max open positions: " + StopGridByPositionsCountValue + "\n";
                    message += "New regime: " + StopGridByPositionsCountReaction;
                    SendNewLogMessage(message, LogMessageType.Signal);

                    return StopGridByPositionsCountReaction;
                }
            }

            // 2 смена режима по движению от первой цены сетки
            if (StopGridByMoveUpIsOn == true
                || StopGridByMoveDownIsOn == true)
            {
                if (ctx.HasCandles == false)
                {
                    return TradeGridRegime.On;
                }

                decimal lastSecurityPrice = ctx.LastPrice;

                decimal firstGridPrice = ctx.FirstGridPrice;

                if (lastSecurityPrice != 0
                    && firstGridPrice != 0)
                {
                    if (StopGridByMoveUpIsOn)
                    {
                        decimal upLimit = firstGridPrice + firstGridPrice * (StopGridByMoveUpValuePercent / 100);

                        if (lastSecurityPrice >= upLimit)
                        {
                            string message = "Auto-stop grid by move Up. \n";
                            message += "First real price in grid: " + firstGridPrice + "\n";
                            message += "Up limit in %: " + StopGridByMoveUpValuePercent + "\n";
                            message += "Price limit: " + upLimit + "\n";
                            message += "New regime: " + StopGridByMoveUpReaction;
                            SendNewLogMessage(message, LogMessageType.Signal);

                            return StopGridByMoveUpReaction;
                        }
                    }

                    if (StopGridByMoveDownIsOn)
                    {
                        decimal downLimit = firstGridPrice - firstGridPrice * (StopGridByMoveDownValuePercent / 100);

                        if (lastSecurityPrice <= downLimit)
                        {
                            string message = "Auto-stop grid by move Down. \n";
                            message += "First real price in grid: " + firstGridPrice + "\n";
                            message += "Down limit in %: " + StopGridByMoveDownValuePercent + "\n";
                            message += "Price limit: " + downLimit + "\n";
                            message += "New regime: " + StopGridByMoveDownReaction;
                            SendNewLogMessage(message, LogMessageType.Signal);

                            return StopGridByMoveDownReaction;
                        }
                    }
                }
            }

            // 2.1 смена режима по абсолютному уровню цены вверх
            if (StopGridByPriceUpValue > 0
                && ctx.HasCandles == true
                && ctx.LastPrice != 0
                && ctx.LastPrice >= StopGridByPriceUpValue)
            {
                string message = "Auto-stop grid by price Up. \n";
                message += "Up price limit: " + StopGridByPriceUpValue + "\n";
                message += "Last price: " + ctx.LastPrice + "\n";
                message += "New regime: " + StopGridByPriceUpReaction;
                SendNewLogMessage(message, LogMessageType.Signal);

                return StopGridByPriceUpReaction;
            }

            // 2.2 смена режима по абсолютному уровню цены вниз
            if (StopGridByPriceDownValue > 0
                && ctx.HasCandles == true
                && ctx.LastPrice != 0
                && ctx.LastPrice <= StopGridByPriceDownValue)
            {
                string message = "Auto-stop grid by price Down. \n";
                message += "Down price limit: " + StopGridByPriceDownValue + "\n";
                message += "Last price: " + ctx.LastPrice + "\n";
                message += "New regime: " + StopGridByPriceDownReaction;
                SendNewLogMessage(message, LogMessageType.Signal);

                return StopGridByPriceDownReaction;
            }

            // 3 смена режима по времени жизни
            if (StopGridByLifeTimeIsOn
                && ctx.FirstTradeTime != DateTime.MinValue)
            {
                DateTime time = ctx.ServerTime;

                if (ctx.FirstTradeTime.AddSeconds(StopGridByLifeTimeSecondsToLife) < time)
                {
                    string message = "Auto-stop grid by life time. \n";
                    message += "First order time in grid: " + ctx.FirstTradeTime.ToString() + "\n";
                    message += "Seconds to life: " + StopGridByLifeTimeSecondsToLife + "\n";
                    message += "Time now: " + time.ToString(OsLocalization.CurCulture) + "\n";
                    message += "New regime: " + StopGridByLifeTimeReaction;
                    SendNewLogMessage(message, LogMessageType.Signal);

                    return StopGridByLifeTimeReaction;
                }

            }

            // 4 смена режима по времени внутри дня
            if (StopGridByTimeOfDayIsOn)
            {
                DateTime time = ctx.ServerTime;

                bool isActivate = false;

                if (time.Hour == StopGridByTimeOfDayHour
                    && time.Minute == StopGridByTimeOfDayMinute
                    && time.Second >= StopGridByTimeOfDaySecond)
                {
                    isActivate = true;
                }
                else if (time.Hour == StopGridByTimeOfDayHour
                    && time.Minute > StopGridByTimeOfDayMinute)
                {
                    isActivate = true;
                }
                else if (time.Hour > StopGridByTimeOfDayHour)
                {
                    isActivate = true;
                }

                if (isActivate == true)
                {
                    string message = "Auto-stop grid by time of day. \n";
                    message += "Current server time: " + time.ToString() + "\n";
                    message += "New regime: " + StopGridByTimeOfDayReaction;
                    SendNewLogMessage(message, LogMessageType.Signal);

                    return StopGridByTimeOfDayReaction;
                }
            }

            return TradeGridRegime.On;
        }

        private bool AllStopConditionsAreOff()
        {
            if (StopGridByMoveUpIsOn == false
                && StopGridByMoveDownIsOn == false
                && StopGridByPositionsCountIsOn == false
                && StopGridByLifeTimeIsOn == false
                && StopGridByTimeOfDayIsOn == false
                && StopGridByPriceUpValue == 0
                && StopGridByPriceDownValue == 0)
            {
                return true;
            }

            return false;
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
