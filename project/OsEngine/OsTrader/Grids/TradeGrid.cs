/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using OsEngine.Entity;
using OsEngine.Language;
using OsEngine.Logging;
using OsEngine.Market;
using OsEngine.Market.Servers;
using OsEngine.Market.Servers.Atp;
using OsEngine.OsTrader.Panels.Tab;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;

namespace OsEngine.OsTrader.Grids
{
    public class TradeGrid
    {
        #region Service

        public TradeGrid(StartProgram startProgram, BotTabSimple tab, int number)
        {
            Tab = tab;
            Number = number;

            if(Tab.ManualPositionSupport != null)
            {
                Tab.ManualPositionSupport.DisableManualSupport();
            }
           
            Tab.NewTickEvent += Tab_NewTickEvent;
            Tab.PositionOpeningSuccesEvent += Tab_PositionOpeningSuccesEvent; 
            Tab.PositionStopActivateEvent += Tab_PositionStopActivateEvent;
            Tab.PositionProfitActivateEvent += Tab_PositionProfitActivateEvent;
            Tab.Connector.TestStartEvent += Connector_TestStartEvent;

            Tab.PositionOpeningFailEvent += Tab_PositionOpeningFailEvent;
            Tab.PositionClosingFailEvent += Tab_PositionClosingFailEvent;

            StartProgram = startProgram;

            NonTradePeriods = new TradeGridNonTradePeriods(tab.TabName+"Grid"+number);
            NonTradePeriods.LogMessageEvent += SendNewLogMessage;

            StopBy = new TradeGridStopBy();
            StopBy.LogMessageEvent += SendNewLogMessage;

            StopAndProfit = new TradeGridStopAndProfit();
            StopAndProfit.LogMessageEvent += SendNewLogMessage;

            AutoStarter = new TradeGridAutoStarter();
            AutoStarter.LogMessageEvent += SendNewLogMessage;

            GridCreator = new TradeGridCreator();
            GridCreator.LogMessageEvent += SendNewLogMessage;

            ErrorsReaction = new TradeGridErrorsReaction(this);
            ErrorsReaction.LogMessageEvent += SendNewLogMessage;

            TrailingUp = new TrailingUp(this);
            TrailingUp.LogMessageEvent += SendNewLogMessage;

            if (StartProgram == StartProgram.IsOsTrader)
            {
                Thread worker = new Thread(ThreadWorkerPlace);
                worker.Start();

                RegimeLogicEntry = TradeGridLogicEntryRegime.OncePerSecond;
                AutoClearJournalIsOn = true;
            }
            else
            {
                RegimeLogicEntry = TradeGridLogicEntryRegime.OnTrade;
                AutoClearJournalIsOn = false;
            }
        }

        public StartProgram StartProgram;

        public int Number;

        public BotTabSimple Tab;

        public TradeGridNonTradePeriods NonTradePeriods;

        public TradeGridStopBy StopBy;

        public TradeGridStopAndProfit StopAndProfit;

        public TradeGridAutoStarter AutoStarter;

        public TradeGridCreator GridCreator;

        public TradeGridErrorsReaction ErrorsReaction;

        public TrailingUp TrailingUp;

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
            result += DelayInReal + "@";
            result += CheckMicroVolumes + "@";
            result += MaxDistanceToOrdersPercent + "@";
            result += "@";

            result += "%";

            // non trade periods
            result += NonTradePeriods.GetSaveString();
            result += "%";

            // trade days
            result += "";
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

            // errors reaction
            result += ErrorsReaction.GetSaveString();
            result += "%";

            // trailing up / down
            result += TrailingUp.GetSaveString();
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
                Enum.TryParse(values[2], out _regime);
                Enum.TryParse(values[3], out RegimeLogicEntry);
                AutoClearJournalIsOn = Convert.ToBoolean(values[4]);
                MaxClosePositionsInJournal = Convert.ToInt32(values[5]);
                MaxOpenOrdersInMarket = Convert.ToInt32(values[6]);
                MaxCloseOrdersInMarket = Convert.ToInt32(values[7]);
                _firstTradePrice = values[8].ToDecimal();
                _openPositionsBySession = Convert.ToInt32(values[9]);
                _firstTradeTime = Convert.ToDateTime(values[10], CultureInfo.InvariantCulture);
                
                try
                {
                    DelayInReal = Convert.ToInt32(values[11]);
                    CheckMicroVolumes = Convert.ToBoolean(values[12]);
                }
                catch
                {
                    DelayInReal = 500;
                    CheckMicroVolumes = true;
                }

                try
                {
                    MaxDistanceToOrdersPercent = values[13].ToDecimal();
                }
                catch
                {
                    MaxDistanceToOrdersPercent = 1.5m;
                }

                // non trade periods
                NonTradePeriods.LoadFromString(array[1]);

                // trade days
                // removed

                // stop grid by event
                StopBy.LoadFromString(array[3]);

                // grid lines creation and storage
                GridCreator.LoadFromString(array[4]);

                // stop and profit 
                StopAndProfit.LoadFromString(array[5]);

                // auto start
                AutoStarter.LoadFromString(array[6]);

                // errors reaction
                ErrorsReaction.LoadFromString(array[7]);

                // trailing up / down
                TrailingUp.LoadFromString(array[8]);
            }
            catch (Exception e)
            {
                SendNewLogMessage(e.ToString(),LogMessageType.Error);
            }
        }

        public void Delete()
        {
            _isDeleted = true;

            if (Tab != null)
            {
                Tab.NewTickEvent -= Tab_NewTickEvent;
                Tab.PositionOpeningSuccesEvent -= Tab_PositionOpeningSuccesEvent;
                Tab.PositionStopActivateEvent -= Tab_PositionStopActivateEvent;
                Tab.PositionProfitActivateEvent -= Tab_PositionProfitActivateEvent;
                Tab.Connector.TestStartEvent -= Connector_TestStartEvent;
                Tab.PositionOpeningFailEvent -= Tab_PositionOpeningFailEvent;
                Tab.PositionClosingFailEvent -= Tab_PositionClosingFailEvent;

                Tab = null;
            }

            if (NonTradePeriods != null)
            {
                NonTradePeriods.LogMessageEvent -= SendNewLogMessage;
                NonTradePeriods.Delete();
                NonTradePeriods = null;
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

            if (ErrorsReaction != null)
            {
                ErrorsReaction.LogMessageEvent -= SendNewLogMessage;
                ErrorsReaction.Delete();
                ErrorsReaction = null;
            }

            if(TrailingUp != null)
            {
                TrailingUp.LogMessageEvent -= SendNewLogMessage;
                TrailingUp.Delete();
                TrailingUp = null;
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

        public void FullRePaintGrid()
        {
            if(FullRePaintGridEvent != null)
            {
                FullRePaintGridEvent();
            }
        }

        private void Connector_TestStartEvent()
        {
            try
            {
                List<TradeGridLine> lines = GridCreator.Lines;

                if (lines == null)
                {
                    return;
                }

                for (int i = 0; i < lines.Count; i++)
                {
                    lines[i].Position = null;
                    lines[i].PositionNum = 0;
                }
            }
            catch (Exception e)
            {
                SendNewLogMessage(e.ToString(), LogMessageType.Error);
            }
        }

        private void Tab_PositionClosingFailEvent(Position position)
        {
            try
            {
                if (Regime != TradeGridRegime.Off)
                {
                    bool isInArray = false;

                    for (int i = 0; i < GridCreator.Lines.Count; i++)
                    {
                        TradeGridLine line = GridCreator.Lines[i];

                        if (line.Position != null
                            && line.Position.Number == position.Number)
                        {
                            isInArray = true;
                            break;
                        }
                    }

                    if (isInArray)
                    {
                        ErrorsReaction.PositionClosingFailEvent(position);
                    }
                }
            }
            catch (Exception e)
            {
                SendNewLogMessage(e.ToString(), LogMessageType.Error);
            }
        }

        private void Tab_PositionOpeningFailEvent(Position position)
        {
            try
            {
                if (Regime != TradeGridRegime.Off)
                {
                    bool isInArray = false;

                    for (int i = 0; i < GridCreator.Lines.Count; i++)
                    {
                        TradeGridLine line = GridCreator.Lines[i];

                        if (line.Position != null
                            && line.Position.Number == position.Number)
                        {
                            isInArray = true;
                            break;
                        }
                    }

                    if (isInArray)
                    {
                        ErrorsReaction.PositionOpeningFailEvent(position);
                    }
                }
            }
            catch (Exception e)
            {
                SendNewLogMessage(e.ToString(), LogMessageType.Error);
            }
        }

        public event Action NeedToSaveEvent;

        public event Action RePaintSettingsEvent;

        public event Action FullRePaintGridEvent;

        #endregion

        #region Settings Prime

        public TradeGridPrimeType GridType;

        public TradeGridRegime Regime
        {
            get
            {
                return _regime;
            }
            set
            {
                if(_regime == value)
                {
                    return;
                }

                _regime = value;

                if(FullRePaintGridEvent != null)
                {
                    FullRePaintGridEvent();
                }
                
                if(RePaintSettingsEvent != null)
                {
                    RePaintSettingsEvent();
                }
            }
        }
        private TradeGridRegime _regime;

        public TradeGridLogicEntryRegime RegimeLogicEntry;

        public bool AutoClearJournalIsOn;

        public int MaxClosePositionsInJournal = 100;

        public int MaxOpenOrdersInMarket = 5;

        public int MaxCloseOrdersInMarket = 5;

        public int DelayInReal = 500;

        public bool CheckMicroVolumes = true;

        public decimal MaxDistanceToOrdersPercent = 0;

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
                if (HaveOpenPositionsByGrid == true 
                    && StartProgram == StartProgram.IsOsTrader)
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

        private bool _isDeleted;

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
            if (_isDeleted == true)
            {
                return;
            }
            if (RegimeLogicEntry == TradeGridLogicEntryRegime.OnTrade)
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

                    if(_isDeleted == true)
                    {
                        return;
                    }

                    if(Tab == null)
                    {
                        return;
                    }

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
            if (Regime != TradeGridRegime.Off)
            {
                bool isInArray = false;

                for(int i = 0;i < GridCreator.Lines.Count;i++)
                {
                    TradeGridLine line = GridCreator.Lines[i];

                    if(line.Position != null 
                        && line.Position.Number == position.Number)
                    {
                        isInArray = true;
                        break;
                    }
                }

                if(isInArray)
                {
                    _openPositionsBySession++;
                    _needToSave = true;
                }
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

            if(Tab.EventsIsOn == false)
            {
                return;
            }

            if(MainWindow.ProccesIsWorked == false)
            {
                return;
            }

            if(StartProgram == StartProgram.IsOsTrader)
            {
                if (Tab.IsNonTradePeriodInConnector == true)
                {
                    return;
                }
            }

            if(StartProgram == StartProgram.IsOsTrader 
               && ErrorsReaction.WaitOnStartConnectorIsOn == true)
            {
                IServer server = Tab.Connector.MyServer;

                if(server.GetType().BaseType.Name == "AServer")
                {
                    AServer aServer = (AServer)server;
                    if (ErrorsReaction.AwaitOnStartConnector(aServer) == true)
                    {
                        return;
                    }
                }
            }

            if (StartProgram == StartProgram.IsOsTrader)
            {// сбрасываем кол-во ошибок по утрам и на старте сессии

                if(ErrorsReaction.TryResetErrorsAtStartOfDay(Tab.TimeServerCurrent) == true)
                {
                    Save();
                }
            }

            TradeGridRegime baseRegime = Regime;

            // 1 Авто-старт сетки, если выключено
            if (baseRegime == TradeGridRegime.Off ||
                baseRegime == TradeGridRegime.OffAndCancelOrders)
            {
                if (StartProgram == StartProgram.IsOsTrader)
                {
                    if (_vacationTime > DateTime.Now)
                    {
                        return;
                    }
                }

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

                _firstStopIsActivate = false;

                if(ErrorsReaction.FailCancelOrdersCountFact != 0 
                    || ErrorsReaction.FailOpenOrdersCountFact != 0)
                {
                    ErrorsReaction.FailCancelOrdersCountFact = 0;
                    ErrorsReaction.FailOpenOrdersCountFact = 0;
                    _needToSave = true;
                }

                if (GridType == TradeGridPrimeType.OpenPosition)
                {
                    TryDeleteDonePositions();
                }

                // отзываем ордера с рынка

                if (HaveOrdersTryToCancelLastSecond())
                {
                    return;
                }

                if (baseRegime == TradeGridRegime.OffAndCancelOrders)
                {
                    int countRejectOrders = TryCancelClosingOrders();

                    if (countRejectOrders > 0)
                    {
                        _vacationTime = DateTime.Now.AddMilliseconds(DelayInReal * countRejectOrders);
                        return;
                    }

                    countRejectOrders = TryCancelOpeningOrders();

                    if (countRejectOrders > 0)
                    {
                        _vacationTime = DateTime.Now.AddMilliseconds(DelayInReal * countRejectOrders);
                        return;
                    }
                }

                // проверяем работу авто-стартера, если он включен

                if (AutoStarter.AutoStartRegime == TradeGridAutoStartRegime.Off)
                {
                    return;
                }

                DateTime serverTime = Tab.TimeServerCurrent;

                TradeGridRegime nonTradePeriodsRegime = NonTradePeriods.GetNonTradePeriodsRegime(serverTime);

                if(nonTradePeriodsRegime != TradeGridRegime.On)
                { // авто-старт не может быть включен, если сейчас не торговый период
                    return;
                }

                if (AutoStarter.HaveEventToStart(this))
                {
                    if(AutoStarter.RebuildGridRegime == OnOffRegime.On)
                    {// пересобираем сетку
                        decimal newPriceStart = AutoStarter.GetNewGridPriceStart(this);
                        
                        if(newPriceStart != 0)
                        {
                            GridCreator.FirstPrice = newPriceStart;
                            GridCreator.CreateNewGrid(Tab, GridType);
                            Save();
                            FullRePaintGrid();
                        }
                    }

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

            // 2 проверяем ошибки и реагируем на них

            if (StartProgram == StartProgram.IsOsTrader)
            {
                TradeGridRegime reaction = ErrorsReaction.GetReactionOnErrors(this);

                if (reaction != TradeGridRegime.On)
                {
                    ErrorsReaction.FailCancelOrdersCountFact = 0;
                    ErrorsReaction.FailOpenOrdersCountFact = 0;
                    baseRegime = reaction;
                    Regime = reaction;
                    Save();
                    RePaintGrid();
                }
            }

            // 3 проверяем ожидание в бою. Только что были отозваны или выставлены N кол-во ордеров

            if (StartProgram == StartProgram.IsOsTrader)
            {
                if(_vacationTime > DateTime.Now)
                {
                    return;
                }
            }

            // 4 проверяем наличие ордеров без номеров в маркете. Для медленных подключений

            if (StartProgram == StartProgram.IsOsTrader)
            {
                if (HaveOrdersWithNoMarketOrders())
                {
                    return;
                }

                if(HaveOrdersTryToCancelLastSecond())
                {
                    return;
                }
            }

            // 5 попытка смены режима если блокировано по времени или по дням

            if (baseRegime == TradeGridRegime.On)
            {
                DateTime serverTime = Tab.TimeServerCurrent;

                TradeGridRegime nonTradePeriodsRegime = NonTradePeriods.GetNonTradePeriodsRegime(serverTime);

                if(nonTradePeriodsRegime != TradeGridRegime.On)
                {
                    baseRegime = nonTradePeriodsRegime;

                    if (baseRegime == TradeGridRegime.CloseForced)
                    {
                        Regime = baseRegime;
                        Save();
                        RePaintGrid();
                    }
                }
            }

            // 6 попытка смены режима по остановке торгов

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

            // 7 попытка сместить сетку

            if (baseRegime == TradeGridRegime.On)
            {
                if(TrailingUp.TryTrailingGrid())
                {
                    _needToSave = true;
                    RePaintGrid();
                    FullRePaintGrid();
                }
            }

            // 8 вход в различную логику различных сеток

            if(baseRegime == TradeGridRegime.On
                || baseRegime == TradeGridRegime.CloseOnly
                || baseRegime == TradeGridRegime.CloseForced)
            {
                if (GridType == TradeGridPrimeType.MarketMaking)
                {
                    GridTypeMarketMakingLogic(baseRegime);
                }
                else if (GridType == TradeGridPrimeType.OpenPosition)
                {
                    GridTypeOpenPositionLogic(baseRegime);
                }
            }
            else if(baseRegime == TradeGridRegime.OffAndCancelOrders)
            {
                int countRejectOrders = TryCancelClosingOrders();

                if (countRejectOrders > 0)
                {
                    _vacationTime = DateTime.Now.AddMilliseconds(DelayInReal * countRejectOrders);
                    return;
                }

                countRejectOrders = TryCancelOpeningOrders();

                if (countRejectOrders > 0)
                {
                    _vacationTime = DateTime.Now.AddMilliseconds(DelayInReal * countRejectOrders);
                    return;
                }
            }
        }

        #endregion

        #region Open Position end logic

        private void GridTypeOpenPositionLogic(TradeGridRegime baseRegime)
        {
            // 1 сверям позиции в журнале и в сетке

            TryFindPositionsInJournalAfterReconnect();
            TryDeleteOpeningFailPositions();

            // 2 удаляем ордера стоящие не на своём месте

            int countRejectOrders = TryRemoveWrongOrders();

            if (countRejectOrders > 0)
            {
                _vacationTime = DateTime.Now.AddMilliseconds(DelayInReal * countRejectOrders);
                return;
            }

            // 3 торговая логика 

            if (baseRegime == TradeGridRegime.On)
            {
                if(_firstStopIsActivate == false)
                {
                    // 1 пытаемся почистить журнал от лишних сделок
                    TryFreeJournal();

                    // 2 проверяем выставлены ли ордера на открытие
                    TrySetOpenOrders();

                    // 3 проверяем выставлены ли закрытия
                    TrySetStopAndProfit();
                }
                else if(_firstStopIsActivate == true)
                {
                    if(_firstStopActivateTime.AddSeconds(5) <DateTime.Now)
                    {
                        string message = "First stop by grid is activate. \n";
                        message += "Stop trading" + "\n";
                        message += "New regime: CloseForced";

                        SendNewLogMessage(message, LogMessageType.Signal);

                        Regime = TradeGridRegime.CloseForced;
                        Save();
                        RePaintGrid();
                        _firstStopIsActivate = false;
                    }
                    else
                    {
                        return;
                    }
                }
            }
            else
            {
                countRejectOrders = TryCancelOpeningOrders();

                if (countRejectOrders > 0)
                {
                    _vacationTime = DateTime.Now.AddMilliseconds(DelayInReal * countRejectOrders);
                    return;
                }

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

                if (baseRegime == TradeGridRegime.CloseOnly)
                {
                    // закрываем позиции штатно
                    TrySetStopAndProfit();
                }
                else if (baseRegime == TradeGridRegime.CloseForced)
                {
                    countRejectOrders = TryCancelClosingOrders();

                    if (countRejectOrders > 0)
                    {
                        _vacationTime = DateTime.Now.AddMilliseconds(DelayInReal * countRejectOrders);
                        return;
                    }

                    // закрываем позиции насильно
                    TryForcedCloseGrid();
                }
            }
        }

        private void TrySetStopAndProfit()
        {
            if(StopAndProfit.ProfitRegime == OnOffRegime.Off
                && StopAndProfit.StopRegime == OnOffRegime.Off
                && StopAndProfit.TrailStopRegime == OnOffRegime.Off)
            {
                return;
            }

            StopAndProfit.Process(this);
        }

        private void TryDeleteOpeningFailPositions()
        {
            List<TradeGridLine> lines = GridCreator.Lines;

            if (lines == null)
            {
                return;
            }

            for (int i = 0; i < lines.Count; i++)
            {
                TradeGridLine line = lines[i];

                if (line.Position != null)
                {
                    // Открывающий ордер был отозван
                    if (line.Position.State == PositionStateType.OpeningFail
                        && line.Position.OpenActive == false)
                    {
                        line.Position = null;
                        line.PositionNum = -1;
                    }
                }
            }
        }

        private bool _firstStopIsActivate = false;

        private DateTime _firstStopActivateTime;

        private void Tab_PositionProfitActivateEvent(Position obj)
        {
            if (_firstStopIsActivate == false)
            {
                _firstStopIsActivate = true;
                _firstStopActivateTime = Tab.TimeServerCurrent;
            }
        }

        private void Tab_PositionStopActivateEvent(Position obj)
        {
            if(_firstStopIsActivate == false)
            {
                _firstStopIsActivate = true;
                _firstStopActivateTime = Tab.TimeServerCurrent;
            }
        }

        #endregion

        #region MarketMaking end logic

        private void GridTypeMarketMakingLogic(TradeGridRegime baseRegime)
        {
            // 1 сверям позиции в журнале и в сетке

            TryFindPositionsInJournalAfterReconnect();
            TryDeleteDonePositions();

            // 2 удаляем ордера стоящие не на своём месте

            int countRejectOrders = TryRemoveWrongOrders();

            if (countRejectOrders > 0)
            {
                _vacationTime = DateTime.Now.AddMilliseconds(DelayInReal * countRejectOrders);
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
                TrySetClosingOrders(Tab.PriceBestAsk);
            }
            else
            {
                countRejectOrders = TryCancelOpeningOrders();

                if (countRejectOrders > 0)
                {
                    _vacationTime = DateTime.Now.AddMilliseconds(DelayInReal * countRejectOrders);
                    return;
                }

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

                if (baseRegime == TradeGridRegime.CloseOnly)
                {
                    // закрываем позиции штатно
                    TrySetClosingOrders(Tab.PriceBestAsk);
                }
                else if (baseRegime == TradeGridRegime.CloseForced)
                {
                    countRejectOrders = TryCancelClosingOrders();

                    if (countRejectOrders > 0)
                    {
                        _vacationTime = DateTime.Now.AddMilliseconds(DelayInReal * countRejectOrders);
                        return;
                    }

                    // закрываем позиции насильно 
                    TryForcedCloseGrid();
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

            if (ordersToCancelBadPrice != null
                && ordersToCancelBadPrice.Count > 0)
            {
                for (int i = 0; i < ordersToCancelBadPrice.Count; i++)
                {
                    //Tab.SetNewLogMessage("Отзыв ордера по не правильной цене", LogMessageType.Error);
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
                   // Tab.SetNewLogMessage("Отзыв ордера по количеству", LogMessageType.Error);
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
                    //Tab.SetNewLogMessage("Отзыв ордера по дыре в сетке", LogMessageType.Error);
                    Tab.CloseOrder(ordersToCancelOpenOrders[i]);
                }

                return ordersToCancelOpenOrders.Count;
            }

            // 4 убираем ордера лишние на закрытие.
            // Когда в сетке больше ордеров чем указал пользователь
            // И когда объём на закрытие не совпадает с тем что в ордере закрывающем

            if(GridType == TradeGridPrimeType.MarketMaking)
            {
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
            }

            List<TradeGridLine> linesWithOrdersToCloseFact = GetLinesWithClosingOrdersFact();

            for (int i = 0; linesWithOrdersToCloseFact != null && i < linesWithOrdersToCloseFact.Count; i++)
            {
                Position position = linesWithOrdersToCloseFact[i].Position;
                TradeGridLine currentLine = linesWithOrdersToCloseFact[i];

                if (position.CloseActive 
                    && currentLine.CanReplaceExitOrder == true) 
                {
                    Order closeOrder = position.CloseOrders[^1];

                    if (closeOrder.Price != currentLine.PriceExit
                        && closeOrder.TypeOrder != OrderPriceType.Market)
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

            if (linesWithOrdersToOpenFact == null ||
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

                if (orderToClose.Volume != pos.OpenVolume)
                {
                    bool isInArray = false;

                    for (int j = 0; j < ordersToCancel.Count; j++)
                    {
                        if (ordersToCancel[j].NumberUser == orderToClose.NumberUser)
                        {
                            isInArray = true;
                            break;
                        }
                    }
                    if (isInArray == false)
                    {
                        ordersToCancel.Add(orderToClose);
                    }
                }
            }

            return ordersToCancel;
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

                if(order.NumberMarket != null)
                {
                    Tab.CloseOrder(order);
                    cancelledOrders++;
                }
            }

            return cancelledOrders;
        }

        private void TrySetClosingOrders(decimal lastPrice)
        {
            CheckWrongCloseOrders();

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

                decimal volume = pos.OpenVolume;

                if (CheckMicroVolumes == true 
                    && Tab.CanTradeThisVolume(volume) == false)
                {
                    continue;
                }

                if (Tab.Security.PriceLimitHigh != 0
                 && Tab.Security.PriceLimitLow != 0)
                {
                    if (line.PriceExit > Tab.Security.PriceLimitHigh
                        || line.PriceExit < Tab.Security.PriceLimitLow)
                    {
                        continue;
                    }
                }

                if (Tab.StartProgram == StartProgram.IsOsTrader
                    && MaxDistanceToOrdersPercent != 0
                    && lastPrice != 0)
                {
                    decimal maxPriceUp = lastPrice + lastPrice * (MaxDistanceToOrdersPercent / 100);
                    decimal minPriceDown = lastPrice - lastPrice * (MaxDistanceToOrdersPercent / 100);

                    if (line.PriceExit > maxPriceUp
                     || line.PriceExit < minPriceDown)
                    {
                        continue;
                    }
                }

                Tab.CloseAtLimit(pos, line.PriceExit, volume);
            }
        }


        private void CheckWrongCloseOrders()
        {
            if(Tab.StartProgram != StartProgram.IsOsTrader)
            {
                return;
            }

            List<TradeGridLine> linesAll = GridCreator.Lines;

            for (int i = 0; i < linesAll.Count; i++)
            {
                TradeGridLine curLine = linesAll[i];
                Position pos = curLine.Position;
                
                if (pos == null)
                {
                    continue;
                }

                decimal volumePosOpen = pos.OpenVolume;

                if (pos.CloseActive == true)
                {
                    Order orderToClose = pos.CloseOrders[^1];
                    decimal volumeCloseOrder = orderToClose.Volume;
                    decimal volumeExecuteCloseOrder = orderToClose.VolumeExecute;

                    if (volumePosOpen != (volumeCloseOrder - volumeExecuteCloseOrder))
                    {
                        Tab.CloseOrder(orderToClose);
                    }
                }
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

                if(order.NumberMarket != null 
                   && order.TypeOrder != OrderPriceType.Market)
                {
                    Tab.CloseOrder(order);
                    cancelledOrders++;
                }
            }

            return cancelledOrders;
        }

        private void TrySetOpenOrders()
        {
            List<Candle> candles = Tab.CandlesAll;

            if (candles == null || candles.Count == 0)
            {
                return;
            }

            decimal lastPrice = candles[candles.Count - 1].Close;

            if(lastPrice == 0)
            {
                return;
            }

            if(Tab.PriceBestAsk == 0
                || Tab.PriceBestBid == 0)
            {
                return;
            }

            List<TradeGridLine> linesAll = GridCreator.Lines;

            // 1 берём текущие линии с позициями

            List<TradeGridLine> linesWithOrdersToOpenNeed = GetLinesWithOpenOrdersNeed(lastPrice);

            List<TradeGridLine> linesWithOrdersToOpenFact = GetLinesWithOpenOrdersFact();

            // 2 ничего не делаем если уже кол-во ордеров максимально

            if (linesWithOrdersToOpenFact.Count >= MaxOpenOrdersInMarket)
            {
                return;
            }

            // 3 открываемся по новой схеме

            for (int i = 0; i < linesWithOrdersToOpenNeed.Count; i++)
            {
                TradeGridLine curLineNeed = linesWithOrdersToOpenNeed[i];

                if (curLineNeed.Position != null)
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
                    }

                    if (_firstTradeTime == DateTime.MinValue)
                    {
                        _firstTradeTime = Tab.TimeServerCurrent;
                    }

                    _needToSave = true;
                }

                linesWithOrdersToOpenFact = GetLinesWithOpenOrdersFact();

                if (linesWithOrdersToOpenFact.Count >= MaxOpenOrdersInMarket)
                {
                    return;
                }
            }
        }

        private DateTime _lastCheckJournalTime = DateTime.MinValue;

        private void TryFreeJournal()
        {
            if (AutoClearJournalIsOn == false)
            {
                return;
            }

            if (_lastCheckJournalTime.AddSeconds(10) > DateTime.Now)
            {
                return;
            }

            _lastCheckJournalTime = DateTime.Now;

            Position[] positions = Tab.PositionsAll.ToArray();

            // 1 удаляем позиции с OpeningFail без всяких условий

            for (int i = 0; i < positions.Length; i++)
            {
                Position pos = positions[i];

                if(pos == null)
                {
                    continue;
                }

                if (pos.State == PositionStateType.OpeningFail
                    && pos.OpenVolume == 0
                    && pos.OpenActive == false
                    && pos.CloseActive == false)
                {
                    TryDeletePositionsFromJournal(pos);
                }
            }

            // 2 удаляем позиции со статусом Done, если пользователь это включил        

            int curDonePosInJournal = 0;

            for (int i = positions.Length - 1; i >= 0; i--)
            {
                Position pos = positions[i];

                if (pos == null)
                {
                    continue;
                }

                if (pos.State != PositionStateType.Done)
                {
                    continue;
                }

                if (pos.OpenVolume != 0)
                {
                    continue;
                }

                if(pos.OpenActive == true
                    || pos.CloseActive == true)
                {
                    continue;
                }

                curDonePosInJournal++;

                if (curDonePosInJournal > MaxClosePositionsInJournal)
                {
                    TryDeletePositionsFromJournal(pos);
                }
            }
        }

        private void TryDeletePositionsFromJournal(Position position)
        {
            List<TradeGridLine> lines = GridCreator.Lines;

            bool isInGridNow = false;

            for(int i = 0;lines != null && i < lines.Count;i++)
            {
                if (lines[i].PositionNum == position.Number)
                {
                    isInGridNow = true;
                    break;
                }
            }

            if(isInGridNow == false)
            {
                Tab._journal.DeletePosition(position);
            }
        }

        private void TryDeleteDonePositions()
        {
            List<TradeGridLine> lines = GridCreator.Lines;

            if(lines == null)
            {
                return;
            }

            for (int i = 0; i < lines.Count; i++)
            {
                TradeGridLine line = lines[i];

                if (line.Position != null)
                {
                 // Позиция была закрыта
                 // Открывающий ордер был отозван
                    if (line.Position.State == PositionStateType.Done
                        ||
                        (line.Position.State == PositionStateType.OpeningFail
                        && line.Position.OpenActive == false))
                    {
                        line.Position = null;
                        line.PositionNum = -1;
                    }

                    else if(line.Position.State == PositionStateType.Deleted)
                    {
                        line.Position = null;
                        line.PositionNum = -1;
                    }
                }
            }
        }

        private void TryFindPositionsInJournalAfterReconnect()
        {
            List<TradeGridLine> lines = GridCreator.Lines;

            List<Position> positions = Tab.PositionsAll;

            for (int i = 0; i < lines.Count; i++)
            {
                TradeGridLine line = lines[i];

                // проблема 1. Номер позиции есть - самой позиции нет. 
                // произошёл перезапуск терминала. Ищем позу в журнале
                if (line.PositionNum != -1
                    && line.Position == null)
                {
                    bool isInArray = false;

                    for (int j = 0; j < positions.Count; j++)
                    {
                        if (positions[j].Number == line.PositionNum)
                        {
                            isInArray = true;
                            line.Position = positions[j];
                            break;
                        }
                    }

                    if (isInArray == false)
                    {
                        line.Position = null;
                        line.PositionNum = -1;
                    }
                }
            }
        }

        #endregion

        #region Forced Close regime logic

        private void TryForcedCloseGrid()
        {
            List<TradeGridLine> lines = GetLinesWithOpenPosition();

            bool havePositions = false;

            for (int i = 0; i < lines.Count; i++)
            {
                TradeGridLine line = lines[i];

                if (line.Position == null
                    || line.Position.CloseActive == true)
                {
                    continue;
                }

                Position pos = line.Position;

                if (pos.State != PositionStateType.Done
                    || pos.OpenVolume >= 0)
                {
                    if (CheckMicroVolumes == true
                    && Tab.CanTradeThisVolume(pos.OpenVolume) == false)
                    {
                        string message = "Micro volume detected. Position deleted \n";
                        message += "Position volume: " + pos.OpenVolume + "\n";
                        message += "Security name: " + pos.SecurityName;
                        SendNewLogMessage(message, LogMessageType.Error);

                        line.Position = null;
                        line.PositionNum = -1;
                        continue;
                    }

                    Tab.CloseAtMarket(pos, pos.OpenVolume);
                    havePositions = true;
                }
            }

            if (Regime == TradeGridRegime.CloseForced
                && havePositions == false)
            {
                string message = "Close Forced regime ended. No positions \n";
                message += "New regime: Off";
                SendNewLogMessage(message, LogMessageType.Signal);
                Regime = TradeGridRegime.Off;
                RePaintGrid();
                _needToSave = true;
            }
        }

        #endregion

        #region Public interface

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

        public bool HaveOrdersWithNoMarketOrders()
        {
            // 1 берём все уровни с позициями
            List<TradeGridLine> linesAll = GridCreator.Lines;

            for (int i = 0; linesAll != null && i < linesAll.Count; i++)
            {
                if (linesAll[i].Position != null)
                {
                    Position position = linesAll[i].Position;

                    if (position.OpenActive)
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

        public bool HaveOrdersTryToCancelLastSecond()
        {
            // возвращает true - если есть ордер который уже отослан на отзыв но всё ещё в статусе Active. За последние 3 секунды.
            // если true - значит последние операции ещё не завершены по снятию ордеров

            List<TradeGridLine> linesAll = GridCreator.Lines;

            for (int i = 0; linesAll != null && i < linesAll.Count; i++)
            {
                if (linesAll[i].Position != null)
                {
                    Position position = linesAll[i].Position;

                    if (position.OpenActive)
                    {
                        if (position.OpenOrders[^1].State == OrderStateType.Active 
                            && position.OpenOrders[^1].IsSendToCancel == true)
                        {
                            if (position.OpenOrders[^1].LastCancelTryLocalTime.AddSeconds(3) > DateTime.Now)
                            {
                                return true;
                            }
                        }
                    }

                    if (position.CloseActive)
                    {
                        if (position.CloseOrders[^1].State == OrderStateType.Active
                            && position.CloseOrders[^1].IsSendToCancel == true)
                        {
                            if(position.CloseOrders[^1].LastCancelTryLocalTime.AddSeconds(3) > DateTime.Now)
                            {
                                return true;
                            }
                        }
                    }
                }
            }

            return false;
        }

        public bool HaveCloseOrders
        {
            get
            {
                // 1 если уже есть позиции с ордерами на закрытие. Ничего не делаем

                List<TradeGridLine> linesWithOpenPositions = GetLinesWithOpenPosition();

                for (int i = 0; i < linesWithOpenPositions.Count; i++)
                {
                    Position pos = linesWithOpenPositions[i].Position;

                    if (pos == null)
                    {
                        continue;
                    }

                    if (pos.CloseActive == true)
                    {
                        return true;
                    }
                }

                return false;
            }
        }

        public bool HaveOpenPositionsByGrid
        {
            get
            {
                List<TradeGridLine> linesWithPositions = GetLinesWithOpenPosition();

                if (linesWithPositions != null &&
                    linesWithPositions.Count != 0)
                {
                    return true;
                }

                return false;
            }
        }

        public decimal MiddleEntryPrice
        {
            get
            {
                // 1 берём позиции по сетке

                List<Position> positions = GetPositionByGrid();

                if (positions == null 
                    || positions.Count == 0)
                {
                    return 0;
                }

                // 2 берём из позиций все MyTrade по открывающим ордерам

                List<MyTrade> tradesOpenPos = new List<MyTrade>();

                for (int i = 0; i < positions.Count; i++)
                {
                    List<Order> orders = positions[i].OpenOrders;

                    for (int j = 0; j < orders.Count; j++)
                    {
                        List<MyTrade> myTrades = orders[j].MyTrades;

                        if (myTrades == null || myTrades.Count == 0)
                        {
                            continue;
                        }
                        tradesOpenPos.AddRange(myTrades);
                    }
                }

                if (tradesOpenPos.Count == 0)
                {
                    return 0;
                }

                // 3 считаем среднюю цену входа

                decimal summ = 0;
                decimal volume = 0;

                for (int i = 0; i < tradesOpenPos.Count; i++)
                {
                    MyTrade trade = tradesOpenPos[i];

                    if (trade == null)
                    {
                        continue;
                    }

                    volume += trade.Volume;
                    summ += trade.Volume * trade.Price;
                }

                decimal result = summ / volume;

                return result;
            }
        }

        public decimal MaxGridPrice
        {
            get
            {
                try
                {
                    return TrailingUp.MaxGridPrice;
                }
                catch(Exception e)
                {
                    SendNewLogMessage(e.ToString(),LogMessageType.Error);
                    return 0;
                }
            }
        }

        public decimal MinGridPrice
        {
            get
            {
                try
                {
                    return TrailingUp.MinGridPrice;
                }
                catch (Exception e)
                {
                    SendNewLogMessage(e.ToString(), LogMessageType.Error);
                    return 0;
                }
            }
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

        public List<Position> GetPositionByGrid()
        {
            List<TradeGridLine> linesAll = GridCreator.Lines;

            List<Position> positions = new List<Position>();

            if (linesAll == null ||
                linesAll.Count == 0)
            {
                return positions;
            }

            for (int i = 0; linesAll != null && i < linesAll.Count; i++)
            {
                Position position = linesAll[i].Position;

                if (position != null)
                {
                    positions.Add(position);
                }
            }
            return positions;
        }

        public List<TradeGridLine> GetLinesWithOpenOrdersNeed(decimal lastPrice)
        {
            List<TradeGridLine> linesAll = GridCreator.Lines;

            List<TradeGridLine> linesWithOrdersToOpenNeed = new List<TradeGridLine>();

            decimal maxPriceUp = 0;
            decimal minPriceDown = 0;

            if(Tab.StartProgram == StartProgram.IsOsTrader
                && MaxDistanceToOrdersPercent != 0)
            {
                maxPriceUp = lastPrice + lastPrice * (MaxDistanceToOrdersPercent/100);
                minPriceDown = lastPrice - lastPrice * (MaxDistanceToOrdersPercent / 100);
            }

            if (GridCreator.GridSide == Side.Buy)
            {
                for (int i = 0; i < linesAll.Count; i++)
                {
                    TradeGridLine curLine = linesAll[i];

                    Position position = curLine.Position;

                    if(position != null 
                        && position.OpenVolume > 0
                        && position.OpenActive == false)
                    {
                        continue;
                    }

                    if(Tab.Security.PriceLimitHigh != 0 
                        && Tab.Security.PriceLimitLow != 0)
                    {
                        if(curLine.PriceEnter > Tab.Security.PriceLimitHigh 
                            || curLine.PriceEnter <  Tab.Security.PriceLimitLow)
                        {
                            continue;
                        }
                    }

                    if(maxPriceUp != 0 
                        && minPriceDown != 0)
                    {
                        if (curLine.PriceEnter > maxPriceUp
                         || curLine.PriceEnter < minPriceDown)
                        {
                            continue;
                        }
                    }

                    if (curLine.PriceEnter <= lastPrice)
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

                    Position position = curLine.Position;

                    if (position != null
                        && position.OpenVolume > 0
                        && position.OpenActive == false)
                    {
                        continue;
                    }

                    if (Tab.Security.PriceLimitHigh != 0
                        && Tab.Security.PriceLimitLow != 0)
                    {
                        if (curLine.PriceEnter > Tab.Security.PriceLimitHigh
                            || curLine.PriceEnter < Tab.Security.PriceLimitLow)
                        {
                            continue;
                        }
                    }

                    if (maxPriceUp != 0
                        && minPriceDown != 0)
                    {
                        if (curLine.PriceEnter > maxPriceUp
                         || curLine.PriceEnter < minPriceDown)
                        {
                            continue;
                        }
                    }

                    if (curLine.PriceEnter >= lastPrice)
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

        public List<TradeGridLine> GetLinesWithClosingOrdersFact()
        {
            List<TradeGridLine> linesAll = GridCreator.Lines;

            List<TradeGridLine> linesWithCloseOrder = new List<TradeGridLine>();

            for (int i = 0; linesAll != null && i < linesAll.Count; i++)
            {
                if (linesAll[i].Position != null
                    && linesAll[i].Position.CloseActive)
                {
                    linesWithCloseOrder.Add(linesAll[i]);
                }
            }
            return linesWithCloseOrder;
        }

        #endregion

        #region Log

        public void SendNewLogMessage(string message, LogMessageType type)
        {
            if(type == LogMessageType.Error)
            {
                message = "Grid error. Bot: " + this.Tab.NameStrategy + "\n"
                + "Security name: " + this.Tab.Connector.SecurityName + "\n"
                + message;
            }

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
        OffAndCancelOrders,
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