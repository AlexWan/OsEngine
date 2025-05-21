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
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static System.Windows.Forms.VisualStyles.VisualStyleElement;

// Разные базовые сути сеток:
// 1) По каждому открытию отдельный выход. Как маркет-мейкинг инструмента в одну сторону.     // MarketMaking
// 2) Как способ открытия позиции. Возможен выход по всей сетке через общий профит и стоп.    // OpenPosition
// 3) Как способ закрытия позиции                                                             // ClosePosition

// Какие бывают общие настройки у сеток
// Объём: Мартингейл / Равномерно
// Объём в: Контракты / Валюта контракта / Процент депозита
// Размер сетки: Равномерный / с мультипликатором
// Количество ордеров в рынке: int
// Шаг сетки указывать: Абсолют / Проценты
// Не торговые периоды: Временная блокировка по не торговым периодам + торговые дни + торговля по отведённому времени.
// Способ входа в логику: Раз в N секунд / На каждом трейде
// Автоочистка журнала: вкл/выкл / кол-во закрытых позиций в журнале

// Переход сетки в режим только закрытие:
// 1) Бесконечная (Циклическая). По умолчанию. Вообще не останавливается.
// 2) В количестве сработавших ордеров / 3) Движение вверх /
// 4) Движение вниз / 5) По времени

// Переход сетки в режим выключена
// 0) При отсутствии позиции в режиме "только закрытии". По умолчанию


namespace OsEngine.OsTrader.Grids
{
    public class TradeGrid
    {
        #region Service

        public TradeGrid(StartProgram startProgram, BotTabSimple tab)
        {
            Tab = tab;

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

            GridSide = Side.Buy;

            ProfitRegime = OnOffRegime.Off;
            StopRegime = OnOffRegime.Off;

        }

        public int Number;

        public BotTabSimple Tab;

        public string GetSaveString()
        {
            string result = "";

            // settings prime
            result += Number + "@";
            result += GridType + "@";
            result += Regime + "@";
            result += ClosePositionNumber + "@";
            result += RegimeLogicEntry + "@";
            result += RegimeLogging + "@";
            result += AutoClearJournalIsOn + "@";
            result += MaxClosePositionsInJournal + "@";

            // non trade periods
            result += NonTradePeriod1OnOff + "@";
            result += NonTradePeriod1Start + "@";
            result += NonTradePeriod1End + "@";
            result += NonTradePeriod2OnOff + "@";
            result += NonTradePeriod2Start + "@";
            result += NonTradePeriod2End + "@";
            result += NonTradePeriod3OnOff + "@";
            result += NonTradePeriod3Start + "@";
            result += NonTradePeriod3End + "@";
            result += NonTradePeriod4OnOff + "@";
            result += NonTradePeriod4Start + "@";
            result += NonTradePeriod4End + "@";
            result += NonTradePeriod5OnOff + "@";
            result += NonTradePeriod5Start + "@";
            result += NonTradePeriod5End + "@";
            result += TradeInMonday + "@";
            result += TradeInTuesday + "@";
            result += TradeInWednesday + "@";
            result += TradeInThursday + "@";
            result += TradeInFriday + "@";
            result += TradeInSaturday + "@";
            result += TradeInSunday + "@";

            // stop grid by event

            result += StopGridByMoveUpIsOn + "@";
            result += StopGridByMoveUpValuePercent + "@";
            result += StopGridByMoveUpReaction + "@";

            result += StopGridByMoveDownIsOn + "@";
            result += StopGridByMoveDownValuePercent + "@";
            result += StopGridByMoveDownReaction + "@";

            result += StopGridByPositionsCountIsOn + "@";
            result += StopGridByPositionsCountValue + "@";
            result += StopGridByPositionsCountReaction + "@";

            // grid lines creation and storage
            result += GridSide + "@";
            result += FirstPrice + "@";
            result += LineCountStart + "@";
            result += MaxOrdersInMarket + "@";
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

            // stop and profit 
            result += ProfitRegime + "@";
            result += ProfitValueType + "@";
            result += ProfitValue + "@";
            result += StopRegime + "@";
            result += StopValueType + "@";
            result += StopValue + "@";

            // trailing up / down
            result += TrailingUpIsOn + "@";
            result += TrailingUpLimitValue + "@";
            result += TrailingDownIsOn + "@";
            result += TrailingDownLimitValue + "@";

            // auto start
            result += AutoStartRegime + "@";
            result += AutoStartPrice + "@";

            return result;
        }

        public void LoadFromString(string value)
        {
            try
            {
                string[] values = value.Split('@');

                // settings prime
                Number = Convert.ToInt32(values[0]);
                Enum.TryParse(values[1], out GridType);
                Enum.TryParse(values[2], out Regime);
                ClosePositionNumber = Convert.ToInt32(values[3]);
                Enum.TryParse(values[4], out RegimeLogicEntry);
                Enum.TryParse(values[5], out RegimeLogging);
                AutoClearJournalIsOn = Convert.ToBoolean(values[6]);
                MaxClosePositionsInJournal = Convert.ToInt32(values[7]);

                // non trade periods
                NonTradePeriod1OnOff = Convert.ToBoolean(values[8]);
                NonTradePeriod1Start.LoadFromString(values[9]);
                NonTradePeriod1End.LoadFromString(values[10]);

                NonTradePeriod2OnOff = Convert.ToBoolean(values[11]);
                NonTradePeriod2Start.LoadFromString(values[12]);
                NonTradePeriod2End.LoadFromString(values[13]);

                NonTradePeriod3OnOff = Convert.ToBoolean(values[14]);
                NonTradePeriod3Start.LoadFromString(values[15]);
                NonTradePeriod3End.LoadFromString(values[16]);

                NonTradePeriod4OnOff = Convert.ToBoolean(values[17]);
                NonTradePeriod4Start.LoadFromString(values[18]);
                NonTradePeriod4End.LoadFromString(values[19]);

                NonTradePeriod5OnOff = Convert.ToBoolean(values[20]);
                NonTradePeriod5Start.LoadFromString(values[21]);
                NonTradePeriod5End.LoadFromString(values[22]);

                TradeInMonday = Convert.ToBoolean(values[23]);
                TradeInTuesday = Convert.ToBoolean(values[24]);
                TradeInWednesday = Convert.ToBoolean(values[25]);
                TradeInThursday = Convert.ToBoolean(values[26]);
                TradeInFriday = Convert.ToBoolean(values[27]);
                TradeInSaturday = Convert.ToBoolean(values[28]);
                TradeInSunday = Convert.ToBoolean(values[29]);

                // stop grid by event

                StopGridByMoveUpIsOn = Convert.ToBoolean(values[30]);
                StopGridByMoveUpValuePercent = values[31].ToDecimal();
                Enum.TryParse(values[32], out StopGridByMoveUpReaction);

                StopGridByMoveDownIsOn = Convert.ToBoolean(values[33]);
                StopGridByMoveDownValuePercent = values[34].ToDecimal();
                Enum.TryParse(values[35], out StopGridByMoveDownReaction);

                StopGridByPositionsCountIsOn = Convert.ToBoolean(values[36]);
                StopGridByPositionsCountValue = Convert.ToInt32(values[37]);
                Enum.TryParse(values[38], out StopGridByPositionsCountReaction);

                // grid lines creation and storage
                Enum.TryParse(values[39], out GridSide);
                FirstPrice = values[40].ToDecimal();
                LineCountStart = Convert.ToInt32(values[41]);
                MaxOrdersInMarket = Convert.ToInt32(values[42]);
                Enum.TryParse(values[43], out TypeStep);
                LineStep = values[44].ToDecimal();
                StepMultiplicator = values[45].ToDecimal();
                Enum.TryParse(values[46], out TypeProfit);
                ProfitStep = values[47].ToDecimal();
                ProfitMultiplicator = values[48].ToDecimal();
                Enum.TryParse(values[49], out TypeVolume);
                StartVolume = values[50].ToDecimal();
                MartingaleMultiplicator = values[51].ToDecimal();
                TradeAssetInPortfolio = values[52];

                // stop and profit
                Enum.TryParse(values[53], out ProfitRegime);
                Enum.TryParse(values[54], out ProfitValueType);
                ProfitValue = values[55].ToDecimal();

                Enum.TryParse(values[56], out StopRegime);
                Enum.TryParse(values[57], out StopValueType);
                StopValue = values[58].ToDecimal();

                // trailing up / down
                TrailingUpIsOn = Convert.ToBoolean(values[59]);
                TrailingUpLimitValue = values[60].ToDecimal();
                TrailingDownIsOn = Convert.ToBoolean(values[61]);
                TrailingDownLimitValue = values[62].ToDecimal();

                // auto start
                Enum.TryParse(values[63], out AutoStartRegime);
                AutoStartPrice = values[64].ToDecimal();
            }
            catch (Exception e)
            {
                SendNewLogMessage(e.ToString(),LogMessageType.Error);
            }
        }

        public void Delete()
        {
            Tab = null;
        }

        public void Save()
        {
            if(NeedToSaveEvent != null)
            {
                NeedToSaveEvent();
            }
        }

        public event Action NeedToSaveEvent;

        #endregion

        #region Settings Prime

        public TradeGridPrimeType GridType;

        public TradeGridRegime Regime;

        public int ClosePositionNumber;

        public TradeGridLogicEntryRegime RegimeLogicEntry;

        public TradeGridLoggingRegime RegimeLogging;

        public bool AutoClearJournalIsOn;

        public int MaxClosePositionsInJournal;

        #endregion

        #region Non trade periods

        public bool NonTradePeriod1OnOff;
        public TimeOfDay NonTradePeriod1Start;
        public TimeOfDay NonTradePeriod1End;

        public bool NonTradePeriod2OnOff;
        public TimeOfDay NonTradePeriod2Start;
        public TimeOfDay NonTradePeriod2End;

        public bool NonTradePeriod3OnOff;
        public TimeOfDay NonTradePeriod3Start;
        public TimeOfDay NonTradePeriod3End;

        public bool NonTradePeriod4OnOff;
        public TimeOfDay NonTradePeriod4Start;
        public TimeOfDay NonTradePeriod4End;

        public bool NonTradePeriod5OnOff;
        public TimeOfDay NonTradePeriod5Start;
        public TimeOfDay NonTradePeriod5End;

        public bool TradeInMonday = true;
        public bool TradeInTuesday = true;
        public bool TradeInWednesday = true;
        public bool TradeInThursday = true;
        public bool TradeInFriday = true;
        public bool TradeInSaturday = false;
        public bool TradeInSunday = false;

        public bool IsBlockNonTradePeriods(DateTime curTime)
        {
            if (NonTradePeriod1OnOff == true)
            {
                if (NonTradePeriod1Start < curTime
                 && NonTradePeriod1End > curTime)
                {
                    return true;
                }

                if (NonTradePeriod1Start > NonTradePeriod1End)
                { // overnight transfer
                    if (NonTradePeriod1Start > curTime
                        || NonTradePeriod1End < curTime)
                    {
                        return true;
                    }
                }
            }

            if (NonTradePeriod2OnOff == true)
            {
                if (NonTradePeriod2Start < curTime
                 && NonTradePeriod2End > curTime)
                {
                    return true;
                }

                if (NonTradePeriod2Start > NonTradePeriod2End)
                { // overnight transfer
                    if (NonTradePeriod2Start > curTime
                        || NonTradePeriod2End < curTime)
                    {
                        return true;
                    }
                }
            }

            if (NonTradePeriod3OnOff == true)
            {
                if (NonTradePeriod3Start < curTime
                 && NonTradePeriod3End > curTime)
                {
                    return true;
                }

                if (NonTradePeriod3Start > NonTradePeriod3End)
                { // overnight transfer
                    if (NonTradePeriod3Start > curTime
                        || NonTradePeriod3End < curTime)
                    {
                        return true;
                    }
                }
            }

            if (NonTradePeriod4OnOff == true)
            {
                if (NonTradePeriod4Start < curTime
                 && NonTradePeriod4End > curTime)
                {
                    return true;
                }

                if (NonTradePeriod4Start > NonTradePeriod4End)
                { // overnight transfer
                    if (NonTradePeriod4Start > curTime
                        || NonTradePeriod4End < curTime)
                    {
                        return true;
                    }
                }
            }

            if (NonTradePeriod5OnOff == true)
            {
                if (NonTradePeriod5Start < curTime
                 && NonTradePeriod5End > curTime)
                {
                    return true;
                }

                if (NonTradePeriod5Start > NonTradePeriod5End)
                { // overnight transfer
                    if (NonTradePeriod5Start > curTime
                        || NonTradePeriod5End < curTime)
                    {
                        return true;
                    }
                }
            }

            if (TradeInMonday == false
                && curTime.DayOfWeek == DayOfWeek.Monday)
            {
                return true;
            }

            if (TradeInTuesday == false
                && curTime.DayOfWeek == DayOfWeek.Tuesday)
            {
                return true;
            }

            if (TradeInWednesday == false
                && curTime.DayOfWeek == DayOfWeek.Wednesday)
            {
                return true;
            }

            if (TradeInThursday == false
                && curTime.DayOfWeek == DayOfWeek.Thursday)
            {
                return true;
            }

            if (TradeInFriday == false
                && curTime.DayOfWeek == DayOfWeek.Friday)
            {
                return true;
            }

            if (TradeInSaturday == false
                && curTime.DayOfWeek == DayOfWeek.Saturday)
            {
                return true;
            }

            if (TradeInSunday == false
                && curTime.DayOfWeek == DayOfWeek.Sunday)
            {
                return true;
            }

            return false;
        }

        #endregion

        #region Stop Grid by Event

        public bool StopGridByMoveUpIsOn;

        public decimal StopGridByMoveUpValuePercent;

        public TradeGridRegime StopGridByMoveUpReaction = TradeGridRegime.CloseForced;

        public bool StopGridByMoveDownIsOn;

        public decimal StopGridByMoveDownValuePercent;

        public TradeGridRegime StopGridByMoveDownReaction = TradeGridRegime.CloseForced;

        public bool StopGridByPositionsCountIsOn;

        public int StopGridByPositionsCountValue;

        public TradeGridRegime StopGridByPositionsCountReaction = TradeGridRegime.CloseForced;

        public void TryStopGridByEvent()
        {
            /*if (Regime != TradeGridRegime.On)
            {
                return;
            }

            if (StopGridByPositionsCountIsOn.ValueBool == true)
            {
                if (_lastGridOpenPositions > StopGridByPositionsCountValue.ValueInt)
                { // Останавливаем сетку по кол-ву уже открытых позиций с последнего создания сетки
                    Regime.ValueString = "Only Close";

                    SendNewLogMessage(
                        "Grid stopped by open positions count. Open positions: " + _lastGridOpenPositions,
                        OsEngine.Logging.LogMessageType.System);

                    return;
                }
            }

            if (StopGridByProfitIsOn.ValueBool == true
                || StopGridByStopIsOn.ValueBool == true)
            {
                decimal lastPrice = _tab.PriceBestAsk;

                if (lastPrice == 0)
                {
                    return;
                }

                if (StopGridByProfitIsOn.ValueBool == true)
                {
                    decimal profitMove = 0;

                    if (GridSide == Side.Buy)
                    {
                        profitMove = (lastPrice - FirstPrice) / (FirstPrice / 100);
                    }
                    else if (GridSide == Side.Sell)
                    {
                        profitMove = (FirstPrice - lastPrice) / (FirstPrice / 100);
                    }

                    if (profitMove > StopGridByProfitValuePercent.ValueDecimal)
                    {
                        // Останавливаем сетку по движению вверх от первой цены сетки
                        Regime.ValueString = "Only Close";

                        SendNewLogMessage(
                            "Grid stopped by move in Profit. Open positions: " + _lastGridOpenPositions,
                            OsEngine.Logging.LogMessageType.System);

                        return;
                    }
                }

                if (StopGridByStopIsOn.ValueBool == true)
                {
                    decimal lossMove = 0;

                    if (GridSide == Side.Buy)
                    {
                        lossMove = (FirstPrice - lastPrice) / (FirstPrice / 100);
                    }
                    else if (GridSide == Side.Sell)
                    {
                        lossMove = (lastPrice - FirstPrice) / (FirstPrice / 100);
                    }

                    if (lossMove > StopGridByProfitValuePercent.ValueDecimal)
                    {
                        // Останавливаем сетку по движению вверх от первой цены сетки
                        Regime.ValueString = "Only Close";

                        SendNewLogMessage(
                            "Grid stopped by move in Loss. Open positions: " + _lastGridOpenPositions,
                            OsEngine.Logging.LogMessageType.System);

                        return;
                    }
                }
            }*/
        }

        #endregion

        #region Grid lines creation and storage

        public Side GridSide;

        public decimal FirstPrice;

        public int LineCountStart;

        public int MaxOrdersInMarket;

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

        #endregion

        #region Stop and Profit. OpenPosition regime

        public OnOffRegime ProfitRegime;

        public TradeGridValueType ProfitValueType;

        public decimal ProfitValue;

        public OnOffRegime StopRegime;

        public TradeGridValueType StopValueType;

        public decimal StopValue;

        #endregion

        #region Trailing Up / Down

        public bool TrailingUpIsOn;

        public decimal TrailingUpLimitValue;

        public bool TrailingDownIsOn;

        public decimal TrailingDownLimitValue;

        #endregion

        #region Auto start

        public TradeGridAutoStartRegime AutoStartRegime;

        public decimal AutoStartPrice;

        #endregion



        #region Trade logic







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

    public enum TradeGridPrimeType
    {
        MarketMaking,
        OpenPosition,
        ClosePosition,
    }

    public enum TradeGridCloseType
    {
        Cycle,
        ActivatedOrdersCount,
        MoveUp,
        MoveDown,
        ByTime
    }

    public enum TradeGridRegime
    {
        Off,
        On,
        CloseOnly,
        CloseForced
    }

    public enum TradeGridLogicEntryRegime
    {
        OnTrade,
        OncePerSecond
    }

    public enum TradeGridLoggingRegime
    {
        Standard,
        Debug
    }

    public enum OnOffRegime
    {
        On,
        Off
    }

    public enum TradeGridValueType
    {
        Absolute,
        Percent,
    }

    public enum TradeGridVolumeType
    {
        Contracts,
        ContractCurrency,
        DepositPercent
    }

    public enum TradeGridAutoStartRegime
    {
        Off,
        HigherOrEqual,
        LowerOrEqual
    }
}