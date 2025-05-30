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

            StartProgram = startProgram;

            NonTradePeriods = new TradeGridNonTradePeriods();
            NonTradePeriods.LogMessageEvent += SendNewLogMessage;

            NonTradeDays = new TradeGridNonTradeDays();
            NonTradeDays.LogMessageEvent += SendNewLogMessage;

            StopBy = new TradeGridStopBy();
            StopBy.LogMessageEvent += SendNewLogMessage;

            StopAndProfit = new TradeGridStopAndProfit();
            StopAndProfit.LogMessageEvent += SendNewLogMessage;

            AutoStarter = new TradeGridAutoStarter();
            AutoStarter.LogMessageEvent += SendNewLogMessage;

            Trailing = new TradeGridTrailing();
            Trailing.LogMessageEvent += SendNewLogMessage;

            GridCreator = new TradeGridCreator();
            GridCreator.LogMessageEvent += SendNewLogMessage;
        }

        public StartProgram StartProgram;

        public int Number;

        public BotTabSimple Tab;

        public TradeGridNonTradePeriods NonTradePeriods;

        public TradeGridNonTradeDays NonTradeDays;

        public TradeGridStopBy StopBy;

        public TradeGridStopAndProfit StopAndProfit;

        public TradeGridAutoStarter AutoStarter;

        public TradeGridTrailing Trailing;

        public TradeGridCreator GridCreator;

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
            result += MaxOpenOrdersInMarket + "@";
            result += MaxCloseOrdersInMarket + "@";

            result += "%";

            // non trade periods
            result += NonTradePeriods.GetSaveString();
            result += "%";

            // trade days
            result += NonTradeDays.GetSaveString();
            result += "%";

            // stop grid by event
            result += StopBy.GetSaveString();
            result += "%";

            // grid lines creation and storage
            result += GridCreator.GetSaveString();
            result += "%";

            // stop and profit 
            result += StopAndProfit.GetSaveString();
            result += "%";

            // trailing up / down
            result += Trailing.GetSaveString();
            result += "%";

            // auto start
            result += AutoStarter.GetSaveString();
            result += "%";

            return result;
        }

        public void LoadFromString(string value)
        {
            try
            {
                string[] array = value.Split('%');

                string[] values = array[0].Split('@');

                // settings prime
                
                Number = Convert.ToInt32(values[0]);
                Enum.TryParse(values[1], out GridType);
                Enum.TryParse(values[2], out Regime);
                ClosePositionNumber = Convert.ToInt32(values[3]);
                Enum.TryParse(values[4], out RegimeLogicEntry);
                Enum.TryParse(values[5], out RegimeLogging);
                AutoClearJournalIsOn = Convert.ToBoolean(values[6]);
                MaxClosePositionsInJournal = Convert.ToInt32(values[7]);
                MaxOpenOrdersInMarket = Convert.ToInt32(values[8]);
                MaxCloseOrdersInMarket = Convert.ToInt32(values[9]);

                // non trade periods
                NonTradePeriods.LoadFromString(array[1]);

                // trade days
                NonTradeDays.LoadFromString(array[2]);

                // stop grid by event
                StopBy.LoadFromString(array[3]);

                // grid lines creation and storage
                GridCreator.LoadFromString(array[4]);

                // stop and profit 
                StopAndProfit.LoadFromString(array[5]);

                // trailing up / down
                Trailing.LoadFromString(array[6]);

                // auto start
                AutoStarter.LoadFromString(array[7]);

            }
            catch (Exception e)
            {
                SendNewLogMessage(e.ToString(),LogMessageType.Error);
            }
        }

        public void Delete()
        {
            Tab = null;

            if (NonTradePeriods != null)
            {
                NonTradePeriods.LogMessageEvent -= SendNewLogMessage;
                NonTradePeriods = null;
            }

            if (NonTradeDays != null)
            {
                NonTradeDays.LogMessageEvent -= SendNewLogMessage;
                NonTradeDays = null;
            }

            if (StopBy != null)
            {
                StopBy.LogMessageEvent -= SendNewLogMessage;
                StopBy = null;
            }

            if (StopAndProfit != null)
            {
                StopAndProfit.LogMessageEvent -= SendNewLogMessage;
                StopAndProfit = null;
            }

            if (AutoStarter != null)
            {
                AutoStarter.LogMessageEvent -= SendNewLogMessage;
                AutoStarter = null;
            }

            if(Trailing != null)
            {
                Trailing.LogMessageEvent -= SendNewLogMessage;
                Trailing = null;
            }

            if(GridCreator != null)
            {
                GridCreator.LogMessageEvent -= SendNewLogMessage;
                GridCreator = null;
            }

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

        public int MaxClosePositionsInJournal = 100;

        public int MaxOpenOrdersInMarket = 5;

        public int MaxCloseOrdersInMarket = 5;

        #endregion

        public void CreateNewGridSafe()
        {
            try
            {
                if (Regime != TradeGridRegime.Off &&
                    GridCreator.Lines != null
                    && GridCreator.Lines.Count > 0)
                {
                    // Сетка включена. Есть линии. Запрет
                    CustomMessageBoxUi ui = new CustomMessageBoxUi(OsLocalization.Trader.Label510);
                    ui.Show();
                    return;
                }
                if (GridCreator.HaveOpenPositionsByGrid == true)
                {
                    // По сетке есть открытые позиции. Запрет
                    CustomMessageBoxUi ui = new CustomMessageBoxUi(OsLocalization.Trader.Label511);
                    ui.Show();
                    return;
                }

                if (Tab.IsConnected == false
                    || Tab.IsReadyToTrade == false)
                {
                    // По сетке не подключены данные. Запрет
                    CustomMessageBoxUi ui = new CustomMessageBoxUi(OsLocalization.Trader.Label512);
                    ui.Show();
                    return;
                }

                if(GridCreator.FirstPrice <= 0)
                {
                    // Первая цена не установлена. Запрет
                    CustomMessageBoxUi ui = new CustomMessageBoxUi(OsLocalization.Trader.Label513);
                    ui.Show();
                    return;
                }

                if (GridCreator.LineCountStart <= 0)
                {
                    // Количество линий в сетке не установлено. Запрет
                    CustomMessageBoxUi ui = new CustomMessageBoxUi(OsLocalization.Trader.Label514);
                    ui.Show();
                    return;
                }

                if (GridCreator.LineStep <= 0)
                {
                    // Шаг сетки не указан. Запрет
                    CustomMessageBoxUi ui = new CustomMessageBoxUi(OsLocalization.Trader.Label515);
                    ui.Show();
                    return;
                }

                if(GridType == TradeGridPrimeType.MarketMaking
                    && GridCreator.ProfitStep <= 0)
                {
                    // Шаг сетки для профита не указан. Запрет
                    CustomMessageBoxUi ui = new CustomMessageBoxUi(OsLocalization.Trader.Label516);
                    ui.Show();
                    return;
                }

                if (GridCreator.StartVolume <= 0)
                {
                    // Стартовый объём не указан. Запрет
                    CustomMessageBoxUi ui = new CustomMessageBoxUi(OsLocalization.Trader.Label517);
                    ui.Show();
                    return;
                }

                if (GridCreator.StepMultiplicator <= 0)
                {
                    // Мультипликатор шага ноль. Запрет
                    CustomMessageBoxUi ui = new CustomMessageBoxUi(OsLocalization.Trader.Label518);
                    ui.Show();
                    return;
                }

                if (GridType == TradeGridPrimeType.MarketMaking
                    && GridCreator.ProfitMultiplicator <= 0)
                {
                    // Мультипликатор профита ноль. Запрет
                    CustomMessageBoxUi ui = new CustomMessageBoxUi(OsLocalization.Trader.Label519);
                    ui.Show();
                    return;
                }

                if (GridCreator.MartingaleMultiplicator <= 0)
                {
                    // Мультипликатор объёма ноль. Запрет
                    CustomMessageBoxUi ui = new CustomMessageBoxUi(OsLocalization.Trader.Label520);
                    ui.Show();
                    return;
                }

                if(GridCreator.Lines.Count > 0)
                {
                    AcceptDialogUi ui = new AcceptDialogUi(OsLocalization.Trader.Label522);

                    ui.ShowDialog();

                    if (ui.UserAcceptAction == false)
                    {
                        return;
                    }
                }

                GridCreator.CreateNewGrid(Tab, GridType);
                Save();
            }
            catch (Exception e)
            {
                SendNewLogMessage(e.ToString(),LogMessageType.Error);
            }
        }

        public void DeleteGrid()
        {
            try
            {
                if (GridCreator.HaveOpenPositionsByGrid == true)
                {
                    CustomMessageBoxUi ui = new CustomMessageBoxUi(OsLocalization.Trader.Label524);
                    ui.Show();
                    return;
                }

                GridCreator.DeleteGrid();
                Save();
            }
            catch (Exception e)
            {
                SendNewLogMessage(e.ToString(), LogMessageType.Error);
            }
        }

        public void CreateNewLine()
        {
            try
            {
                GridCreator.CreateNewLine();

                Save();
            }
            catch (Exception e)
            {
                SendNewLogMessage(e.ToString(), LogMessageType.Error);
            }
        }

        public void RemoveSelected(List<int> numbers)
        {
            try
            {
                GridCreator.RemoveSelected(numbers);
                Save();
            }
            catch (Exception e)
            {
                SendNewLogMessage(e.ToString(), LogMessageType.Error);
            }
        }

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