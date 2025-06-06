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
using System.Globalization;
using System.Threading;
using static System.Windows.Forms.VisualStyles.VisualStyleElement;


// Разные базовые сути сеток:
// 1) По каждому открытию отдельный выход. Как маркет-мейкинг инструмента в одну сторону.     // MarketMaking
// 2) Как способ открытия позиции. Возможен выход по всей сетке через общий профит и стоп.    // OpenPosition

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

            if(Tab.ManualPositionSupport != null)
            {
                Tab.ManualPositionSupport.DisableManualSupport();
            }
           
            Tab.NewTickEvent += Tab_NewTickEvent;
            Tab.PositionOpeningSuccesEvent += Tab_PositionOpeningSuccesEvent;
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

            GridCreator = new TradeGridCreator();
            GridCreator.LogMessageEvent += SendNewLogMessage;

            if(StartProgram == StartProgram.IsOsTrader)
            {
                Thread worker = new Thread(ThreadWorkerPlace);
                worker.Start();

                RegimeLogicEntry = TradeGridLogicEntryRegime.OncePerSecond;
            }
            else
            {
                RegimeLogicEntry = TradeGridLogicEntryRegime.OnTrade;
            }
        }

        public StartProgram StartProgram;

        public int Number;

        public BotTabSimple Tab;

        public TradeGridNonTradePeriods NonTradePeriods;

        public TradeGridNonTradeDays NonTradeDays;

        public TradeGridStopBy StopBy;

        public TradeGridStopAndProfit StopAndProfit;

        public TradeGridAutoStarter AutoStarter;

        public TradeGridCreator GridCreator;

        public string GetSaveString()
        {
            string result = "";

            // settings prime

            result += Number + "@";
            result += GridType + "@";
            result += Regime + "@";
            result += RegimeLogicEntry + "@";
            result += AutoClearJournalIsOn + "@";
            result += MaxClosePositionsInJournal + "@";
            result += MaxOpenOrdersInMarket + "@";
            result += MaxCloseOrdersInMarket + "@";
            result += _firstTradePrice + "@";
            result +=  _openPositionsBySession + "@";
            result += _firstTradeTime.ToString(CultureInfo.InvariantCulture) + "@";

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
                Enum.TryParse(values[3], out RegimeLogicEntry);
                AutoClearJournalIsOn = Convert.ToBoolean(values[4]);
                MaxClosePositionsInJournal = Convert.ToInt32(values[5]);
                MaxOpenOrdersInMarket = Convert.ToInt32(values[6]);
                MaxCloseOrdersInMarket = Convert.ToInt32(values[7]);
                _firstTradePrice = Convert.ToInt32(values[8]);
                _openPositionsBySession = Convert.ToInt32(values[9]);
                _firstTradeTime = Convert.ToDateTime(values[10], CultureInfo.InvariantCulture);

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

                // auto start
                AutoStarter.LoadFromString(array[6]);

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

        public void RePaintGrid()
        {
            if (RePaintSettingsEvent != null)
            {
                RePaintSettingsEvent();
            }
        }

        public event Action NeedToSaveEvent;

        public event Action RePaintSettingsEvent;

        #endregion

        #region Settings Prime

        public TradeGridPrimeType GridType;

        public TradeGridRegime Regime;

        public TradeGridLogicEntryRegime RegimeLogicEntry;

        public bool AutoClearJournalIsOn;

        public int MaxClosePositionsInJournal = 100;

        public int MaxOpenOrdersInMarket = 5;

        public int MaxCloseOrdersInMarket = 5;

        #endregion

        #region Grid managment

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
                if (HaveOpenPositionsByGrid == true)
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
                if (HaveOpenPositionsByGrid == true)
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

        #endregion

        #region Trade logic. Entry in logic

        private void Tab_NewTickEvent(Trade trade)
        {
            if(RegimeLogicEntry == TradeGridLogicEntryRegime.OnTrade)
            {
                Process();
            }
        }

        private void ThreadWorkerPlace()
        {
            while(true)
            {
                try
                {
                    Thread.Sleep(1000);

                    if(RegimeLogicEntry == TradeGridLogicEntryRegime.OncePerSecond)
                    {
                        Process();
                    }

                    if(_needToSave)
                    {
                        _needToSave = false;
                        Save();
                    }
                }
                catch(Exception e)
                {
                    SendNewLogMessage(e.ToString(),LogMessageType.Error);
                }
            }
        }

        private void Tab_PositionOpeningSuccesEvent(Position position)
        {
            if (Regime == TradeGridRegime.On)
            {
                _openPositionsBySession++;
                _needToSave = true;
            }
        }

        private bool _needToSave;

        #endregion

        #region Trade logic. Main logic tree

        private DateTime _vacationTime;

        private void Process()
        {
            if (Tab.IsConnected == false 
                || Tab.IsReadyToTrade == false)
            {
                return;
            }

            if(Tab.CandlesAll == null
                || Tab.CandlesAll.Count == 0)
            {
                return;
            }

            if(GridCreator.Lines == null 
                || GridCreator.Lines.Count == 0)
            {
                return;
            }

            TradeGridRegime baseRegime = Regime;

            // 1 Авто-старт сетки, если выключено
            if (baseRegime == TradeGridRegime.Off)
            {

                if (_openPositionsBySession != 0)
                {
                    _openPositionsBySession = 0;
                    _needToSave = true;
                }
                if (_firstTradePrice != 0)
                {
                    _firstTradePrice = 0;
                    _needToSave = true;
                }

                if (_firstTradeTime != DateTime.MinValue)
                {
                    _firstTradeTime = DateTime.MinValue;
                    _needToSave = true;
                }

                if (AutoStarter.AutoStartRegime == TradeGridAutoStartRegime.Off)
                {
                    return;
                }

                DateTime serverTime = Tab.TimeServerCurrent;

                TradeGridRegime tradeDaysRegime = NonTradeDays.GetNonTradeDaysRegime(serverTime);
                TradeGridRegime nonTradePeriodsRegime = NonTradePeriods.GetNonTradePeriodsRegime(serverTime);

                if(tradeDaysRegime != TradeGridRegime.On 
                    || nonTradePeriodsRegime != TradeGridRegime.On)
                { // авто-старт не может быть включен, если сейчас не торговый период
                    return;
                }

                if (AutoStarter.HaveEventToStart(this,Tab))
                {
                    baseRegime = TradeGridRegime.On;
                    Regime = TradeGridRegime.On;
                    Save();
                    RePaintGrid();
                }
                else
                {
                    return;
                }
            }

            // 2 проверяем ожидание в бою. Только что были отозваны или выставлены N кол-во ордеров

            if(StartProgram == StartProgram.IsOsTrader)
            {
                if(_vacationTime > DateTime.Now)
                {
                    return;
                }
            }

            // 3 проверяем наличие ордеров без номеров в маркете. Для медленных подключений

            if (StartProgram == StartProgram.IsOsTrader)
            {
                if (HaveOrdersWithNoMarketOrders())
                {
                    return;
                }
            }

            // 4 попытка смены режима если блокировано по времени или по дням

            if (baseRegime == TradeGridRegime.On)
            {
                DateTime serverTime = Tab.TimeServerCurrent;

                TradeGridRegime tradeDaysRegime = NonTradeDays.GetNonTradeDaysRegime(serverTime);
                TradeGridRegime nonTradePeriodsRegime = NonTradePeriods.GetNonTradePeriodsRegime(serverTime);

                if(nonTradePeriodsRegime != TradeGridRegime.On)
                {
                    baseRegime = nonTradePeriodsRegime;
                }
                if(tradeDaysRegime != TradeGridRegime.On)
                {
                    baseRegime = tradeDaysRegime;
                }
            }

            // 5 попытка смены режима по остановке торгов

            if (baseRegime == TradeGridRegime.On)
            {
                TradeGridRegime stopByRegime = StopBy.GetRegime(this, Tab);

                if(stopByRegime != TradeGridRegime.On)
                {
                    baseRegime = stopByRegime;
                    Regime = stopByRegime;
                    Save();
                    RePaintGrid();
                }
            }

            // 6 сверям позиции в журнале и в сетке

            CheckPositionsAndLines();

            // 7 удаляем ордера стоящие не на своём месте

            int countRejectOrders = TryRemoveWrongOrders();

            if (countRejectOrders > 0)
            {
                _vacationTime = DateTime.Now.AddSeconds(1 + countRejectOrders);
                return;
            }

            // 8 торговая логика 

            if (baseRegime == TradeGridRegime.On)
            {
                // 1 пытаемся почистить журнал от лишних сделок
                TryFreeJournal();

                // 2 проверяем выставлены ли ордера на открытие
                TrySetOpenOrders();

                // 3 проверяем выставлены ли закрытия
                TrySetClosingOrders();
            }
            else
            {
                countRejectOrders = TryCancelOpeningOrders();

                if (countRejectOrders > 0)
                {
                    _vacationTime = DateTime.Now.AddSeconds(1 + countRejectOrders);
                    return;
                }

                if (_openPositionsBySession != 0)
                {
                    _openPositionsBySession = 0;
                    _needToSave = true;
                }
                if(_firstTradePrice != 0)
                {
                    _firstTradePrice = 0;
                    _needToSave = true;
                }
                if (_firstTradeTime != DateTime.MinValue)
                {
                    _firstTradeTime = DateTime.MinValue;
                    _needToSave = true;
                }

                if (baseRegime == TradeGridRegime.CloseOnly)
                {
                    // закрываем позиции штатно
                    TrySetClosingOrders();
                }
                else if (baseRegime == TradeGridRegime.CloseForced)
                {
                    countRejectOrders = TryCancelClosingOrders();

                    if (countRejectOrders > 0)
                    {
                        _vacationTime = DateTime.Now.AddSeconds(1 + countRejectOrders);
                        return;
                    }

                    // закрываем позиции насильно
                    TryForcedCloseGrid();
                }
            }
        }

        private void CheckPositionsAndLines()
        {
            List<TradeGridLine> lines = GridCreator.Lines;

            List<Position> positions = Tab.PositionsAll;

            for(int i = 0;i < lines.Count;i++)
            {
                TradeGridLine line = lines[i];

                // проблема 1. Номер позиции есть - самой позиции нет. 
                // произошёл перезапуск терминала. Ищем позу в журнале
                if(line.PositionNum != -1 
                    && line.Position == null)
                {
                    bool isInArray = false;

                    for(int j = 0;j < positions.Count;j++)
                    {
                        if (positions[j].Number == line.PositionNum)
                        {
                            isInArray = true;
                            line.Position = positions[j];
                            break;
                        }
                    }

                    if(isInArray == false)
                    {
                        line.Position = null;
                        line.PositionNum = -1;
                    }
                }

                if (GridType == TradeGridPrimeType.MarketMaking 
                    && line.Position != null)
                {// если мы маркетим
                 // проблема 2. Позиция была закрыта
                 // проблема 3. Открывающий ордер был отозван
                    if (line.Position.State == PositionStateType.Done
                        || 
                        (line.Position.State == PositionStateType.OpeningFail 
                        && line.Position.OpenActive == false))
                    {
                        line.Position = null;
                        line.PositionNum = -1;
                    }
                }
            }
        }

        private int TryRemoveWrongOrders()
        {
            List<Candle> candles = Tab.CandlesAll;

            if (candles == null || candles.Count == 0)
            {
                return 0;
            }

            decimal lastPrice = candles[candles.Count - 1].Close;

            List<TradeGridLine> linesAll = GridCreator.Lines;

            // 1 убираем ордера на открытие и закрытие с неправильной ценой.

            List<Order> ordersToCancelBadPrice = GetOrdersBadPriceToGrid();

            if(ordersToCancelBadPrice != null 
                && ordersToCancelBadPrice.Count > 0)
            {
                for(int i = 0;i < ordersToCancelBadPrice.Count;i++)
                {
                    Tab.CloseOrder(ordersToCancelBadPrice[i]);
                }

                return ordersToCancelBadPrice.Count;
            }

            // 2 убираем ордера лишние на открытие. Когда в сетке больше ордеров чем указал пользователь

            List<Order> ordersToCancelBadLines = GetOrdersBadLinesMaxCount();

            if (ordersToCancelBadLines != null 
                && ordersToCancelBadLines.Count > 0)
            {
                for (int i = 0; i < ordersToCancelBadLines.Count; i++)
                {
                    Tab.CloseOrder(ordersToCancelBadLines[i]);
                }

                return ordersToCancelBadLines.Count;
            }

            // 3 убираем ордера на открытие, если имеет место дыра в сетке

            List<Order> ordersToCancelOpenOrders = GetOpenOrdersGridHole();

            if (ordersToCancelOpenOrders != null 
                && ordersToCancelOpenOrders.Count > 0)
            {
                for (int i = 0; i < ordersToCancelOpenOrders.Count; i++)
                {
                    Tab.CloseOrder(ordersToCancelOpenOrders[i]);
                }

                return ordersToCancelOpenOrders.Count;
            }

            // 4 убираем ордера лишние на закрытие.
            // Когда в сетке больше ордеров чем указал пользователь
            // И когда объём на закрытие не совпадает с тем что в ордере закрывающем

            List<Order> ordersToCancelCloseOrders = GetCloseOrdersGridHole();

            if (ordersToCancelCloseOrders != null
                && ordersToCancelCloseOrders.Count > 0)
            {
                for (int i = 0; i < ordersToCancelCloseOrders.Count; i++)
                {
                    Tab.CloseOrder(ordersToCancelCloseOrders[i]);
                }

                return ordersToCancelCloseOrders.Count;
            }

            return 0;
        }

        private List<Order> GetOrdersBadPriceToGrid()
        {
            // 1 смотрим совпадение цен у ордера на открытие с ценой открытия линии 
            // 2 смотрим совпадиние цен у ордера на закрытие с ценой закрытия линии

            List<Order> ordersToCancel = new List<Order>();

            List<TradeGridLine> linesWithOrdersToOpenFact = GetLinesWithOpenOrdersFact();

            for (int i = 0; linesWithOrdersToOpenFact != null && i < linesWithOrdersToOpenFact.Count; i++)
            {
                Position position = linesWithOrdersToOpenFact[i].Position;
                TradeGridLine currentLine = linesWithOrdersToOpenFact[i];

                if (position.OpenActive)
                {
                    Order openOrder = position.OpenOrders[^1];

                    if (openOrder.Price != currentLine.PriceEnter)
                    {
                        ordersToCancel.Add(openOrder);
                    }
                }

                if (position.CloseActive)
                {
                    Order closeOrder = position.CloseOrders[^1];

                    if (closeOrder.Price != currentLine.PriceExit)
                    {
                        ordersToCancel.Add(closeOrder);
                    }
                }
            }

            return ordersToCancel;
        }

        private List<Order> GetOrdersBadLinesMaxCount()
        {
            List<TradeGridLine> linesWithOrdersToOpenFact = GetLinesWithOpenOrdersFact();

            List<Order> ordersToCancel = new List<Order>();

            // 1 Открытие. Смотрим чтобы не было ордеров больше чем указал пользователь

            for (int i = MaxOpenOrdersInMarket; i < linesWithOrdersToOpenFact.Count; i++)
            {
                Position curPosition = linesWithOrdersToOpenFact[i].Position;
                ordersToCancel.Add(curPosition.OpenOrders[^1]);
            }

            return ordersToCancel;
        }

        private List<Order> GetOpenOrdersGridHole()
        {
            List<Candle> candles = Tab.CandlesAll;

            if (candles == null || candles.Count == 0)
            {
                return null;
            }

            decimal lastPrice = candles[candles.Count - 1].Close;

            List<TradeGridLine> linesAll = GridCreator.Lines;

            // 1 берём текущие линии с позициями

            List<TradeGridLine> linesWithOrdersToOpenNeed = GetLinesWithOpenOrdersNeed(lastPrice);

            List<TradeGridLine> linesWithOrdersToOpenFact = GetLinesWithOpenOrdersFact();

            if(linesWithOrdersToOpenFact == null ||
                linesWithOrdersToOpenFact.Count == 0)
            {
                return null;
            }

            if (linesWithOrdersToOpenNeed == null ||
                linesWithOrdersToOpenNeed.Count == 0)
            {
                return null;
            }

            List<Order> ordersToCancel = new List<Order>();

            // 2 смотрим, Стоит ли первый ордер на своём месте

            TradeGridLine firstLineFirstNeed = linesWithOrdersToOpenNeed[0];
            TradeGridLine firstLineFirstFact = linesWithOrdersToOpenFact[0];

            TradeGridLine firstLineLastNeed = linesWithOrdersToOpenNeed[^1];
            TradeGridLine firstLineLastFact = linesWithOrdersToOpenFact[^1];

            if (firstLineFirstFact.PriceEnter == firstLineFirstNeed.PriceEnter
                && firstLineLastFact.PriceEnter == firstLineLastNeed.PriceEnter)
            {// всё в порядке
                return null;
            }

            if (linesWithOrdersToOpenFact.Count >= linesWithOrdersToOpenNeed.Count)
            {
                ordersToCancel.Add(linesWithOrdersToOpenFact[^1].Position.OpenOrders[^1]);
            }

            return ordersToCancel;
        }

        private List<Order> GetCloseOrdersGridHole()
        {
            List<TradeGridLine> linesOpenPoses = GetLinesWithOpenPosition();

            List<Order> ordersToCancel = new List<Order>();

            // 1 отправляем на отзыв ордера которые за пределами желаемого пользователем кол-ва

            for (int i = 0; i < linesOpenPoses.Count - MaxCloseOrdersInMarket; i++)
            {
                Position pos = linesOpenPoses[i].Position;
                TradeGridLine line = linesOpenPoses[i];

                if (pos.CloseActive == true)
                {
                    ordersToCancel.Add(pos.CloseOrders[^1]);
                }
            }

            // 2 отправляем на отзыв ордера которые с не верным объёмом

            for (int i = 0; i < linesOpenPoses.Count; i++)
            {
                Position pos = linesOpenPoses[i].Position;
                TradeGridLine line = linesOpenPoses[i];

                if (pos.CloseActive == false)
                {
                    continue;
                }

                Order orderToClose = pos.CloseOrders[^1];

                if(orderToClose.Volume != pos.OpenVolume)
                {
                    bool isInArray = false;

                    for(int j = 0;j < ordersToCancel.Count;j++)
                    {
                        if (ordersToCancel[j].NumberUser == orderToClose.NumberUser)
                        {
                            isInArray = true;
                            break;
                        }
                    }
                    if(isInArray == false)
                    {
                        ordersToCancel.Add(orderToClose);
                    }
                }
            }

            return ordersToCancel;
        }

        private void TrySetOpenOrders()
        {
            List<Candle> candles = Tab.CandlesAll;

            if (candles == null || candles.Count == 0)
            {
                return;
            }

            decimal lastPrice = candles[candles.Count - 1].Close;

            List<TradeGridLine> linesAll = GridCreator.Lines;

            // 1 берём текущие линии с позициями

            List<TradeGridLine> linesWithOrdersToOpenNeed = GetLinesWithOpenOrdersNeed(lastPrice);

            List<TradeGridLine> linesWithOrdersToOpenFact = GetLinesWithOpenOrdersFact();

            // 2 ничего не делаем если уже кол-во ордеров максимально

            if(linesWithOrdersToOpenFact.Count >= MaxOpenOrdersInMarket)
            {
                return;
            }

            // 3 открываемся по новой схеме

            for(int i = 0;i < linesWithOrdersToOpenNeed.Count;i++)
            {
                TradeGridLine curLineNeed = linesWithOrdersToOpenNeed[i];

                if(curLineNeed.Position != null)
                {
                    continue;
                }

                // открываемся. Позиции по линии нет

                decimal volume = GridCreator.GetVolume(curLineNeed, Tab);

                Position newPosition = null;

                if (curLineNeed.Side == Side.Buy)
                {
                    newPosition = Tab.BuyAtLimit(volume, curLineNeed.PriceEnter);
                }
                else if (curLineNeed.Side == Side.Sell)
                {
                    newPosition = Tab.SellAtLimit(volume, curLineNeed.PriceEnter);
                }

                if (newPosition != null)
                {
                    curLineNeed.Position = newPosition;
                    curLineNeed.PositionNum = newPosition.Number;

                    if (_firstTradePrice == 0)
                    {
                        _firstTradePrice = curLineNeed.PriceEnter;
                        _needToSave = true;
                    }

                    if (_firstTradeTime == DateTime.MinValue)
                    {
                        _firstTradeTime = Tab.TimeServerCurrent;
                        _needToSave = true;
                    }
                }

                linesWithOrdersToOpenFact = GetLinesWithOpenOrdersFact();

                if (linesWithOrdersToOpenFact.Count >= MaxOpenOrdersInMarket)
                {
                    return;
                }
            }
        }

        private int TryCancelOpeningOrders()
        {
            List<TradeGridLine> lines = GetLinesWithOpenOrdersFact();

            int cancelledOrders = 0;

            for (int i = 0; i < lines.Count; i++)
            {
                TradeGridLine line = lines[i];

                if (line.Position == null 
                    || line.Position.OpenActive == false)
                {
                    continue;
                }

                Order order = lines[i].Position.OpenOrders[^1];

                Tab.CloseOrder(order);
                cancelledOrders++;
            }

            return cancelledOrders;
        }

        private void TrySetClosingOrders()
        {
            if(GridType == TradeGridPrimeType.MarketMaking)
            {
                List<TradeGridLine> linesOpenPoses = GetLinesWithOpenPosition();

                int startIndex = linesOpenPoses.Count - MaxCloseOrdersInMarket;

                if (startIndex < 0)
                {
                    startIndex = 0;
                }

                for (int i = startIndex; i < linesOpenPoses.Count; i++)
                {
                    Position pos = linesOpenPoses[i].Position;
                    TradeGridLine line = linesOpenPoses[i];

                    if (pos.CloseActive == true)
                    {
                        continue;
                    }

                    Tab.CloseAtLimit(pos, line.PriceExit, pos.OpenVolume);
                }
            }
            else if(GridType == TradeGridPrimeType.OpenPosition)
            {
                StopAndProfit.Process(this);
            }
        }

        private int TryCancelClosingOrders()
        {
            List<TradeGridLine> lines = GetLinesWithOpenPosition();

            int cancelledOrders = 0;

            for (int i = 0; i < lines.Count; i++)
            {
                TradeGridLine line = lines[i];

                if (line.Position == null
                    || line.Position.CloseActive == false)
                {
                    continue;
                }

                Order order = lines[i].Position.CloseOrders[^1];

                Tab.CloseOrder(order);
                cancelledOrders++;
            }

            return cancelledOrders;
        }

        private void TryForcedCloseGrid()
        {
            List<TradeGridLine> lines = GetLinesWithOpenPosition();

            bool havePositions = false;

            for (int i = 0; i < lines.Count; i++)
            {
                TradeGridLine line = lines[i];

                if (line.Position == null)
                {
                    continue;
                }

                havePositions = true;

                Position pos = line.Position;

                if(pos.State == PositionStateType.Done
                    || line.Position.OpenVolume <= 0)
                {
                    continue;
                }

                Tab.CloseAtMarket(pos, pos.OpenVolume);
            }

            if(Regime == TradeGridRegime.CloseForced 
                && havePositions == false)
            {
                string message = "Close Forced regime ended. No positions \n";
                message += "New regime: Off";
                SendNewLogMessage(message, LogMessageType.Signal);
                Regime = TradeGridRegime.Off;
                RePaintGrid();
            }
        }

        private bool HaveOrdersWithNoMarketOrders()
        {
            // 1 берём все уровни с позициями
            List<TradeGridLine> linesAll = GridCreator.Lines;

            for (int i = 0; linesAll != null && i < linesAll.Count; i++)
            {
                if (linesAll[i].Position != null)
                {
                    Position position = linesAll[i].Position;

                    if(position.OpenActive)
                    {
                        if (string.IsNullOrEmpty(position.OpenOrders[^1].NumberMarket))
                        {
                            return true;
                        }
                    }

                    if (position.CloseActive)
                    {
                        if (string.IsNullOrEmpty(position.CloseOrders[^1].NumberMarket))
                        {
                            return true;
                        }
                    }
                }
            }

            return false;
        }

        public List<TradeGridLine> GetLinesWithOpenPosition()
        {
            List<TradeGridLine> linesAll = GridCreator.Lines;

            List<TradeGridLine> linesWithPositionFact = new List<TradeGridLine>();

            for (int i = 0; linesAll != null && i < linesAll.Count; i++)
            {
                if (linesAll[i].Position != null
                    && linesAll[i].Position.OpenVolume != 0)
                {
                    linesWithPositionFact.Add(linesAll[i]);
                }
            }
            return linesWithPositionFact;
        }

        public List<TradeGridLine> GetLinesWithOpenOrdersNeed(decimal lastPrice)
        {
            List<TradeGridLine> linesAll = GridCreator.Lines;

            List<TradeGridLine> linesWithOrdersToOpenNeed = new List<TradeGridLine>();

            if (GridCreator.GridSide == Side.Buy)
            {
                for (int i = 0; i < linesAll.Count; i++)
                {
                    TradeGridLine curLine = linesAll[i];

                    if (curLine.PriceEnter < lastPrice)
                    {
                        linesWithOrdersToOpenNeed.Add(curLine);
                    }

                    if (linesWithOrdersToOpenNeed.Count >= MaxOpenOrdersInMarket)
                    {
                        break;
                    }
                }
            }
            else if (GridCreator.GridSide == Side.Sell)
            {
                for (int i = 0; i < linesAll.Count; i++)
                {
                    TradeGridLine curLine = linesAll[i];

                    if (curLine.PriceEnter > lastPrice)
                    {
                        linesWithOrdersToOpenNeed.Add(curLine);
                    }

                    if (linesWithOrdersToOpenNeed.Count >= MaxOpenOrdersInMarket)
                    {
                        break;
                    }
                }
            }
            return linesWithOrdersToOpenNeed;
        }

        public List<TradeGridLine> GetLinesWithOpenOrdersFact()
        {
            List<TradeGridLine> linesAll = GridCreator.Lines;

            List<TradeGridLine> linesWithOpenOrder = new List<TradeGridLine>();

            for (int i = 0; linesAll != null && i < linesAll.Count; i++)
            {
                if (linesAll[i].Position != null
                    && linesAll[i].Position.OpenActive)
                {
                    linesWithOpenOrder.Add(linesAll[i]);
                }
            }
            return linesWithOpenOrder;
        }

        private DateTime _lastCheckJournalTime = DateTime.MinValue;

        private void TryFreeJournal()
        {
            if (AutoClearJournalIsOn == false)
            {
                return;
            }

            if (_lastCheckJournalTime.AddSeconds(10)> DateTime.Now)
            {
                return;
            }

            _lastCheckJournalTime = DateTime.Now;

            List<Position> positions = Tab.PositionsAll;

            // 1 удаляем позиции с OpeningFail без всяких условий

            for (int i = 0; i < positions.Count; i++)
            {
                Position pos = positions[i];
                if (pos.State == PositionStateType.OpeningFail)
                {
                    Tab._journal.DeletePosition(pos);
                    i--;
                }
            }

            // 2 удаляем позиции со статусом Done, если пользователь это включил        

            int curDonePosInJournal = 0;

            for (int i = positions.Count - 1; i >= 0; i--)
            {
                Position pos = positions[i];

                if (pos.State != PositionStateType.Done)
                {
                    continue;
                }

                curDonePosInJournal++;

                if (curDonePosInJournal > MaxClosePositionsInJournal)
                {
                    Tab._journal.DeletePosition(pos);
                    i--;
                }
            }
        }

        #endregion

        public bool HaveOpenPositionsByGrid
        {
            get
            {
                List<TradeGridLine> linesWithPositions = GetLinesWithOpenPosition();

                if(linesWithPositions != null &&
                    linesWithPositions.Count != 0)
                {
                    return true;
                }

                return false;
            }
        }

        public decimal FirstPriceReal
        {
            get
            {
                return _firstTradePrice;
            }
        }
        private decimal _firstTradePrice;

        public int OpenPositionsCount
        {
            get
            {

                return _openPositionsBySession;
            }
        }
        private int _openPositionsBySession;

        public DateTime FirstTradeTime
        {
            get
            {
                return _firstTradeTime;
            }
        }
        private DateTime _firstTradeTime;

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
        OpenPosition
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
}