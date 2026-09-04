/*
 * Your rights to use code governed by this license http://o-s-a.net/doc/license_simple_engine.pdf
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using OsEngine.Entity;
using OsEngine.Language;
using OsEngine.Market.Connectors;
using OsEngine.OsTrader.Panels;
using OsEngine.OsTrader.Panels.Attributes;
using OsEngine.OsTrader.Panels.Tab;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows.Forms;
using System.Windows.Forms.Integration;

namespace OsEngine.Market.AutoFollow
{
    [Bot("PortfolioStateCopyBot")]
    public class PortfolioStateCopyBot : BotPanel
    {
        private BotTabSimple _mainTab;

        private BotTabScreener _posTabs;

        private StrategyParameterString _regime;

        private StrategyParameterString _fullLog;

        private List<string> _defaultIgnoredSec = ["RUB", "Rub", "rub", "USDT", "USD", "Usd", "Eur", "EUR"];

        private List<Tuple<Security, Side, decimal>> _notIgnoredSec = [];

        public PortfolioStateCopyBot(string name, StartProgram startProgram) : base(name, startProgram)
        {
            // non trade periods
            _tradePeriodsSettings = new NonTradePeriods(name);

            _tradePeriodsSettings.NonTradePeriodGeneral.NonTradePeriod1Start = new TimeOfDay() { Hour = 0, Minute = 0 };
            _tradePeriodsSettings.NonTradePeriodGeneral.NonTradePeriod1End = new TimeOfDay() { Hour = 8, Minute = 05 };
            _tradePeriodsSettings.NonTradePeriodGeneral.NonTradePeriod1OnOff = true;

            _tradePeriodsSettings.NonTradePeriodGeneral.NonTradePeriod2Start = new TimeOfDay() { Hour = 13, Minute = 54 };
            _tradePeriodsSettings.NonTradePeriodGeneral.NonTradePeriod2End = new TimeOfDay() { Hour = 14, Minute = 6 };
            _tradePeriodsSettings.NonTradePeriodGeneral.NonTradePeriod2OnOff = false;

            _tradePeriodsSettings.NonTradePeriodGeneral.NonTradePeriod3Start = new TimeOfDay() { Hour = 18, Minute = 1 };
            _tradePeriodsSettings.NonTradePeriodGeneral.NonTradePeriod3End = new TimeOfDay() { Hour = 23, Minute = 58 };
            _tradePeriodsSettings.NonTradePeriodGeneral.NonTradePeriod3OnOff = true;

            _tradePeriodsSettings.TradeInSunday = false;
            _tradePeriodsSettings.TradeInSaturday = false;

            _tradePeriodsSettings.Load();

            _mainTab = TabCreate<BotTabSimple>();
            _posTabs = TabCreate<BotTabScreener>();

            _regime = CreateParameter(OsLocalization.Logging.Label5, "Off", new[] { "Off", "On" });

            _fullLog = CreateParameter("Full log", "Off", new[] { "Off", "On" });

            _tradePeriodsShowDialogButton = CreateParameterButton(OsLocalization.Market.ServerParam14);
            _tradePeriodsShowDialogButton.UserClickOnButtonEvent += _tradePeriodsShowDialogButton_UserClickOnButtonEvent;
  
            CustomTabToParametersUi customTab = ParamGuiSettings.CreateCustomTab(OsLocalization.Market.LabelIgnorSec);

            LoadIgnoredPos();

            customTab.AddChildren(_host);

            DeleteEvent += DeleteBotEvent;
    
            _grid.Click += _grid_Click;

            Thread worker = new Thread(WorkerPlace);
            worker.Start();
        }

        private void LoadIgnoredPos()
        {
            try
            {
                if (!File.Exists(@"Engine\" + NameStrategyUniq + "-IgnoredPos.txt"))
                {
                    CreateTable();

                    string allPos = "";

                    for (int i = 0; i < _grid.Rows.Count - 1; i++)
                    {
                        allPos += _grid.Rows[i].Cells[0].Value.ToString() + "%";
                    }

                    allPos = allPos.TrimEnd('%');

                    File.WriteAllText(@"Engine\" + NameStrategyUniq + "-IgnoredPos.txt", allPos);

                    return;
                }

                _defaultIgnoredSec = File.ReadAllText(@"Engine\" + NameStrategyUniq + @"-IgnoredPos.txt").Split('%').ToList();

                CreateTable();
            }
            catch (Exception ex)
            {
                SendNewLogMessage(ex.Message, Logging.LogMessageType.Error);
            }
        }

        private void DeleteBotEvent()
        {
            if (File.Exists(@"Engine\" + NameStrategyUniq + "-IgnoredPos.txt"))
            {
                File.Delete(@"Engine\" + NameStrategyUniq + "-IgnoredPos.txt");
            }

            if (_grid != null)
            {
                _grid.DataError -= Grid_DataError;
            }

            _botIsDelete = true;
        }

        #region Work with grid

        private WindowsFormsHost _host;

        private DataGridView _grid;

        private void CreateTable()
        {
            if (MainWindow.GetDispatcher.CheckAccess() == false)
            {
                MainWindow.GetDispatcher.Invoke(new Action(CreateTable));
                return;
            }

            _host = new WindowsFormsHost();

            DataGridView newGrid =
                DataGridFactory.GetDataGridView(DataGridViewSelectionMode.CellSelect,
                DataGridViewAutoSizeRowsMode.AllCells);

            newGrid.DataError += Grid_DataError;

            newGrid.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            DataGridViewTextBoxCell cell0 = new DataGridViewTextBoxCell();
            cell0.Style = newGrid.DefaultCellStyle;

            DataGridViewColumn colum0 = new DataGridViewColumn();
            colum0.CellTemplate = cell0;
            colum0.HeaderText = "";
            colum0.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
            colum0.ReadOnly = false;
            colum0.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            newGrid.Columns.Add(colum0);

            DataGridViewButtonColumn colum1 = new DataGridViewButtonColumn();
            colum1.ReadOnly = true;
            colum1.HeaderText = "";
            colum1.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            newGrid.Columns.Add(colum1);

            for (int i = 0; i < _defaultIgnoredSec.Count; i++)
            {
                DataGridViewRow row = new DataGridViewRow();

                row.Cells.Add(new DataGridViewTextBoxCell());
                row.Cells[0].Value = _defaultIgnoredSec[i];
                row.Cells[0].ReadOnly = true;

                row.Cells.Add(new DataGridViewButtonCell());
                row.Cells[1].Value = OsLocalization.Market.Label47;

                newGrid.Rows.Add(row);
            }

            DataGridViewRow row1 = new DataGridViewRow();
            row1.Cells.Add(new DataGridViewTextBoxCell());
            row1.Cells[0].Value = "";

            row1.Cells.Add(new DataGridViewButtonCell());
            row1.Cells[1].Value = OsLocalization.Market.Label48;

            newGrid.Rows.Add(row1); 


            _host.Child = newGrid;
            _grid = newGrid;
        }

        private void Grid_DataError(object sender, DataGridViewDataErrorEventArgs e)
        {
            e.ThrowException = false;
            SendNewLogMessage(e.Exception.ToString(), Logging.LogMessageType.Error);
        }

        private void _grid_Click(object sender, EventArgs e)
        {
            try
            {
                if (_grid.SelectedCells.Count == 0)
                {
                    return;
                }

                int columnIndex = _grid.SelectedCells[0].ColumnIndex;
                int rowIndex = _grid.SelectedCells[0].RowIndex;

                if (columnIndex == 1) // buttons
                {
                    if (rowIndex == _grid.Rows.Count - 1)
                    {
                        // add sec
                        string newSec = _grid.Rows[rowIndex].Cells[0].Value.ToString();

                        if (string.IsNullOrEmpty(newSec))
                            return;

                        if (_notIgnoredSec.Count > 0)
                        {
                            var sec = _notIgnoredSec.Find(s => s.Item1.Name == newSec);

                            if (sec != null)
                            {
                               string[] attMsg = OsLocalization.Market.Message105.Split('#');

                                AcceptDialogUi ui = new AcceptDialogUi(attMsg[0] + newSec + attMsg[1]);
                                ui.ShowDialog();

                                if (ui.UserAcceptAction == false)
                                {
                                    return;
                                }
                            }

                            _notIgnoredSec.Remove(sec);

                            // удалить позицию и вкладку

                            BotTabSimple tab = _posTabs.Tabs.Find(t => t.Connector.SecurityName.StartsWith(newSec));

                            if (tab != null && tab.PositionsOpenAll.Count == 1)   // у бота есть позиция с таким инструментом
                            {
                                tab.CloseAtFake(tab.PositionsOpenAll[0], tab.PositionsOpenAll[0].MaxVolume, tab.PriceBestAsk, DateTime.Now);

                                _posTabs.RemoveTabBySecurityName(sec.Item1.Name, sec.Item1.NameClass);

                                _posTabs.SaveSettings();
                                _posTabs.NeedToReloadTabs = true;
                            }    
                        }

                        _defaultIgnoredSec.Add(newSec);

                        DataGridViewRow row = new DataGridViewRow();

                        row.Cells.Add(new DataGridViewTextBoxCell());
                        row.Cells[0].Value = newSec;
                        row.Cells[0].ReadOnly = true;

                        row.Cells.Add(new DataGridViewButtonCell());
                        row.Cells[1].Value = OsLocalization.Market.Label47;

                        _grid.Rows.Insert(_grid.Rows.Count - 1, row);

                        _grid.Rows[^1].Cells[0].Value = "";
                    }
                    else
                    {
                        // delete
                        string[] attMsg = OsLocalization.Market.Message106.Split('#');

                        AcceptDialogUi ui = new AcceptDialogUi(attMsg[0] + _grid.Rows[rowIndex].Cells[0].Value + attMsg[1]);
                        ui.ShowDialog();

                        if (ui.UserAcceptAction == false)
                        {
                            return;
                        }

                        List<Security> _securitiesAll = _mainTab.Connector.MyServer.Securities;

                        if (_securitiesAll == null)
                        {
                            CustomMessageBoxUi boxUi = new CustomMessageBoxUi(OsLocalization.Market.Message113);
                            boxUi.ShowDialog();
                            return;
                        }

                        _defaultIgnoredSec.Remove(_grid.Rows[rowIndex].Cells[0].Value.ToString());
   
                        _grid.Rows.RemoveAt(rowIndex);
                    }

                    string allPos = "";

                    for (int i = 0; i < _grid.Rows.Count - 1; i++)
                    {
                        allPos += _grid.Rows[i].Cells[0].Value.ToString() + "%";
                    }

                    allPos = allPos.TrimEnd('%');

                    File.WriteAllText(@"Engine\" + NameStrategyUniq + "-IgnoredPos.txt", allPos);
                }
            }
            catch (Exception ex)
            {
                SendNewLogMessage(ex.Message, Logging.LogMessageType.Error);
            }
        }

        #endregion

        #region Worker place

        private bool _botIsDelete = false;

        private class PendingPositionState
        {
            public string SecurityName;
            public bool NeedClose;
            public Side Side;
            public decimal Volume;
            public DateTime LastChangeTime;
        }

        private List<PendingPositionState> _pendingPositionStates = new List<PendingPositionState>();

        private int _stabilizationSeconds = 5;

        private bool IsStateStabilized(string securityName, bool needClose, Side side, decimal volume)
        {
            PendingPositionState pending = _pendingPositionStates.Find(p => p.SecurityName == securityName);

            if (pending == null)
            {
                pending = new PendingPositionState();
                pending.SecurityName = securityName;
                pending.NeedClose = needClose;
                pending.Side = side;
                pending.Volume = volume;
                pending.LastChangeTime = DateTime.Now;
                _pendingPositionStates.Add(pending);

                LogIfFull($"Pending state registered: {securityName} needClose={needClose} side={side} volume={volume}");
                return false;
            }

            if (pending.NeedClose != needClose
                || pending.Side != side
                || pending.Volume != volume)
            {
                pending.NeedClose = needClose;
                pending.Side = side;
                pending.Volume = volume;
                pending.LastChangeTime = DateTime.Now;

                LogIfFull($"Pending state changed: {securityName} needClose={needClose} side={side} volume={volume}");
                return false;
            }

            return DateTime.Now >= pending.LastChangeTime.AddSeconds(_stabilizationSeconds);
        }

        private void RemovePendingState(string securityName)
        {
            PendingPositionState pending = _pendingPositionStates.Find(p => p.SecurityName == securityName);

            if (pending != null)
            {
                _pendingPositionStates.Remove(pending);
            }
        }

        private void LogIfFull(string message)
        {
            if (_fullLog.ValueString == "On")
            {
                SendNewLogMessage(message, Logging.LogMessageType.System);
            }
        }

        public void WorkerPlace()
        {
            while (true)
            {
                Thread.Sleep(1000);

                try
                {
                    if (_botIsDelete == true)
                    {
                        return;
                    }

                    if (_regime.ValueString == "Off")
                    {
                        Thread.Sleep(1000);
                        continue;
                    }

                    if (_mainTab.IsConnected == false
                        || _mainTab.IsReadyToTrade == false)
                    {
                        Thread.Sleep(1000);
                        continue;
                    }

                    if (_tradePeriodsSettings.CanTradeThisTime(_mainTab.TimeServerCurrent) == false)
                    {
                        continue;
                    }

                    List<Security> _securitiesAll = _mainTab.Connector.MyServer.Securities;

                    if (_securitiesAll == null)
                    {
                        continue;
                    }

                    // 1 модерация позиций в существующих источниках

                    List<PositionOnBoard> positionsOnExchange = _mainTab.Portfolio.PositionOnBoard;

                    List<Position> botPositions = _posTabs.PositionsOpenAll;

                    List<ActivatedSecurity> securitiesToScreener = new List<ActivatedSecurity>();

                    for (int i = 0; i < positionsOnExchange.Count; i++)
                    {
                        PositionOnBoard positionOnEx = positionsOnExchange[i];

                        bool isIgnored = false;

                        for (int j = 0; j < _defaultIgnoredSec.Count; j++)
                        {
                            if (_defaultIgnoredSec[j] == positionOnEx.SecurityNameCode)
                            {
                                isIgnored = true;
                                break;
                            }
                        }

                        if (isIgnored)
                        {
                            continue;
                        }

                        Side posExchangeDirection = positionOnEx.ValueCurrent < 0 ? Side.Sell : Side.Buy;

                        Security security = null;

                        if (string.IsNullOrEmpty(positionOnEx.SecurityNameClass) == false)
                        {
                            security = _securitiesAll.Find(s =>
                            s.Name.Equals(positionOnEx.SecurityNameCode) &&
                             s.NameClass.Equals(positionOnEx.SecurityNameClass)
                            );
                        }
                        else
                        {
                            security = _securitiesAll.Find(s => s.Name.Equals(positionOnEx.SecurityNameCode));
                        }

                        if (security == null)
                        {
                            SendNewLogMessage(positionOnEx.SecurityNameCode + OsLocalization.Market.Message107, Logging.LogMessageType.Error);
                            continue;
                        }

                        BotTabSimple tab = _posTabs.Tabs.Find(t => t.Connector.SecurityName.Equals(positionOnEx.SecurityNameCode));

                        if (tab != null && tab.PositionsOpenAll.Count > 0)   // у бота есть позиция с таким инструментом
                        {
                            bool needClose = positionOnEx.ValueCurrent == 0 // бумаги нет в портфеле
                                || positionOnEx.PortfolioName != _mainTab.Portfolio.Number; // ведущий портфель изменился

                            bool needRecreate = needClose == false
                                && (tab.PositionsOpenAll[0].Direction != posExchangeDirection
                                    || tab.PositionsOpenAll[0].OpenVolume != Math.Abs(positionOnEx.ValueCurrent));      // проверка направления и объема

                            if (needClose == false && needRecreate == false)
                            {
                                // состояние на бирже совпадает с позицией — отложенная реакция не нужна
                                if (_pendingPositionStates.Find(p => p.SecurityName == positionOnEx.SecurityNameCode) != null)
                                {
                                    LogIfFull($"State returned to current position, pending cancelled: {positionOnEx.SecurityNameCode}");
                                }
                                RemovePendingState(positionOnEx.SecurityNameCode);
                                continue;
                            }

                            // биржа может отдавать промежуточные значения портфеля — реагируем только на устоявшееся состояние
                            if (IsStateStabilized(positionOnEx.SecurityNameCode, needClose, posExchangeDirection, positionOnEx.ValueCurrent) == false)
                            {
                                continue;
                            }

                            RemovePendingState(positionOnEx.SecurityNameCode);

                            if (needClose)
                            {
                                LogIfFull($"Close position, exchange volume is zero: {security.Name}");

                                tab.CloseAtFake(tab.PositionsOpenAll[0], tab.PositionsOpenAll[0].MaxVolume, tab.PriceBestAsk, DateTime.Now);

                                _mainTab.Portfolio.PositionOnBoard.Remove(positionsOnExchange[i]);

                                continue;
                            }

                            Tuple<Security, Side, decimal> changedSec = _notIgnoredSec.Find(s => s.Item1.Name == security.Name);

                            if (changedSec != null)
                            {
                                _notIgnoredSec.Remove(changedSec);

                                _notIgnoredSec.Add(new Tuple<Security, Side, decimal>(security, posExchangeDirection, positionOnEx.ValueCurrent));
                            }

                            Position position = tab.PositionsOpenAll[0];
                            decimal botVolume = position.OpenVolume;
                            decimal targetVolume = Math.Abs(positionOnEx.ValueCurrent);

                            if (position.Direction != posExchangeDirection)
                            {
                                // разворот позиции — только закрытие старой и создание новой
                                LogIfFull($"Recreate position, direction changed: {security.Name} bot={botVolume} exchange={targetVolume} side={posExchangeDirection}");

                                tab.CloseAtFake(position, position.MaxVolume, tab.PriceBestAsk, DateTime.Now);

                                Position newDeal = tab._dealCreator.CreatePosition(tab.TabName, posExchangeDirection, posExchangeDirection == Side.Buy ? tab.PriceBestAsk : tab.PriceBestBid,
                                                   targetVolume, OrderPriceType.Market, tab.ManualPositionSupport.SecondToOpen, security, _mainTab.Portfolio,
                                                   tab.StartProgram, tab.ManualPositionSupport.OrderTypeTime, tab.ManualPositionSupport.LimitsMakerOnly);

                                newDeal.NameBotClass = tab.BotClassName;

                                tab._journal.SetNewDeal(newDeal);

                                tab.OrderFakeExecute(newDeal.OpenOrders[0], DateTime.Now);

                                continue;
                            }

                            if (targetVolume > botVolume)
                            {
                                // докупаем дельту к существующей позиции, не закрывая её
                                LogIfFull($"Increase position volume: {security.Name} {botVolume} -> {targetVolume}");

                                Order addOrder = tab._dealCreator.CreateOrder(security, posExchangeDirection,
                                    posExchangeDirection == Side.Buy ? tab.PriceBestAsk : tab.PriceBestBid,
                                    targetVolume - botVolume, OrderPriceType.Market, tab.ManualPositionSupport.SecondToOpen,
                                    tab.StartProgram, OrderPositionConditionType.Open, tab.ManualPositionSupport.OrderTypeTime,
                                    _mainTab.Portfolio.ServerUniqueName, tab.ManualPositionSupport.LimitsMakerOnly, position.Number);

                                addOrder.PortfolioNumber = position.PortfolioName;

                                position.AddNewOpenOrder(addOrder);

                                tab.OrderFakeExecute(addOrder, DateTime.Now);
                            }
                            else
                            {
                                // частично закрываем дельту — позиция остаётся открытой
                                LogIfFull($"Decrease position volume: {security.Name} {botVolume} -> {targetVolume}");

                                tab.CloseAtFake(position, botVolume - targetVolume, tab.PriceBestAsk, DateTime.Now);
                            }

                            continue;

                        }
                        else // добавить позицию
                        {
                            if (positionOnEx.ValueCurrent == 0) // ещё сделки нет
                            {
                                if (positionOnEx.ValueBlocked == 0)
                                    _mainTab.Portfolio.PositionOnBoard.Remove(positionOnEx);

                                continue;
                            }

                            ActivatedSecurity secToScreener = new ActivatedSecurity();
                            secToScreener.SecurityClass = security.NameClass;
                            secToScreener.SecurityName = security.Name;
                            secToScreener.IsOn = true;
                            securitiesToScreener.Add(secToScreener);

                            _notIgnoredSec.Add(new Tuple<Security, Side, decimal>(security, posExchangeDirection, positionOnEx.ValueCurrent));
                        }
                    }

                    // 2 создать новые ИСТОЧНИКИ

                    if (securitiesToScreener.Count > 0)
                    {
                        for (int i = 0; i < securitiesToScreener.Count; i++)
                        {
                            bool isInArray = false;

                            for (int i2 = 0; i2 < _posTabs.SecuritiesNames.Count; i2++)
                            {
                                if (_posTabs.SecuritiesNames[i2].SecurityName.Equals(securitiesToScreener[i].SecurityName))
                                {
                                    isInArray = true;

                                    if(_posTabs.SecuritiesNames[i2].IsOn == false)
                                    {
                                        _posTabs.SecuritiesNames[i2].IsOn = true;
                                    }

                                    break;
                                }
                            }
                            if (isInArray == false)
                            {
                                LogIfFull($"Create tab: {securitiesToScreener[i].SecurityName}");
                                _posTabs.SecuritiesNames.Add(securitiesToScreener[i]);
                            }
                        }

                        if(_posTabs.SecuritiesNames.Count > 20)
                        {

                        }

                        _posTabs.ServerType = _mainTab.Connector.ServerType;
                        _posTabs.ServerName = _mainTab.Connector.ServerFullName;
                        _posTabs.PortfolioName = _mainTab.Portfolio.Number;
                        _posTabs.SaveSettings();
                        _posTabs.NeedToReloadTabs = true;

                        WaitTabsDownload();

                        for (int j = 0; j < securitiesToScreener.Count; j++)
                        {
                            ActivatedSecurity currentSecurity = securitiesToScreener[j];

                            Tuple<Security, Side, decimal> sec = _notIgnoredSec.Find(s => s.Item1.Name == currentSecurity.SecurityName);

                            BotTabSimple tab = _posTabs.Tabs.Find(t => t.Connector.SecurityName == currentSecurity.SecurityName);

                            if (tab != null
                                && tab.Security != null
                                && tab.IsConnected
                                && tab.CandlesAll != null
                                && tab.CandlesAll.Count > 0)
                            {
                                decimal bestAsk = tab.PriceBestAsk;
                                decimal bestBid = tab.PriceBestBid;

                                if (bestAsk == 0 && bestBid == 0)
                                {
                                    // стакана нет (например у ETF) — цена для фейкового исполнения из последней свечи
                                    bestAsk = tab.CandlesAll[tab.CandlesAll.Count - 1].Close;
                                    bestBid = bestAsk;
                                }

                                LogIfFull($"Open new position: {currentSecurity.SecurityName} side={sec.Item2} volume={sec.Item3}");

                                Position newDeal = tab._dealCreator.CreatePosition(tab.TabName, sec.Item2, sec.Item2 == Side.Buy ? bestAsk : bestBid,
                                                   Math.Abs(sec.Item3), OrderPriceType.Market, tab.ManualPositionSupport.SecondToOpen, sec.Item1, tab.Portfolio,
                                                   tab.StartProgram, tab.ManualPositionSupport.OrderTypeTime, tab.ManualPositionSupport.LimitsMakerOnly);

                                newDeal.NameBotClass = tab.BotClassName;

                                tab._journal.SetNewDeal(newDeal);

                                tab.OrderFakeExecute(newDeal.OpenOrders[0], DateTime.Now);
                            }
                            else
                            {
                                LogIfFull($"Skip opening position {currentSecurity.SecurityName}: tab not ready (tab={(tab != null)}, security={(tab != null && tab.Security != null)}, connected={(tab != null && tab.IsConnected)}, candles={(tab != null && tab.CandlesAll != null ? tab.CandlesAll.Count : 0)})");
                            }
                        }
                    }

                    // 3 проверка удаления Источников, если по ним нет позиций на бирже

                    for (int i = 0; i < _posTabs.Tabs.Count; i++)
                    {
                        BotTabSimple tabCurrent = _posTabs.Tabs[i];

                        List<Position> openPositionsInTab = tabCurrent.PositionsOpenAll;

                        if (openPositionsInTab.Count == 0)
                        {
                            LogIfFull($"Remove tab (no positions in tab): {tabCurrent.Connector.SecurityName}");
                            _posTabs.RemoveTabBySecurityName(tabCurrent.Connector.SecurityName, tabCurrent.Connector.SecurityClass);
                            Thread.Sleep(1000);
                            break;
                        }

                        if (openPositionsInTab.Count > 0)
                        {
                            bool haveOnExchange = false;

                            for (int i2 = 0; i2 < positionsOnExchange.Count; i2++)
                            {
                                PositionOnBoard positionOnEx = positionsOnExchange[i2];

                                bool mySecurity = false;

                                if(string.IsNullOrEmpty(positionOnEx.SecurityNameClass) == false)
                                {
                                    if(positionOnEx.SecurityNameCode == tabCurrent.Connector.SecurityName
                                        && positionOnEx.SecurityNameClass == tabCurrent.Connector.SecurityClass)
                                    {
                                        mySecurity = true;
                                    }
                                }
                                else
                                {
                                    if (positionOnEx.SecurityNameCode == tabCurrent.Connector.SecurityName)
                                    {
                                        mySecurity = true;
                                    }
                                }

                                if(mySecurity == true
                                    && positionOnEx.ValueCurrent != 0)
                                    
                                {
                                    haveOnExchange = true;
                                }
                            }

                            if(haveOnExchange == false)
                            {
                                LogIfFull($"Remove tab (no position on exchange): {tabCurrent.Connector.SecurityName}");
                                _posTabs.RemoveTabBySecurityName(tabCurrent.Connector.SecurityName, tabCurrent.Connector.SecurityClass);
                                Thread.Sleep(1000);
                                break;
                            }
                        }
                    }

                    // 4 проверяем дубли позиций. Если в источнике позиций больше одной

                    for (int i = 0; i < _posTabs.Tabs.Count; i++)
                    {
                        BotTabSimple tabCurrent = _posTabs.Tabs[i];

                        List<Position> openPositionsInTab = tabCurrent.PositionsOpenAll;

                        if (openPositionsInTab.Count > 1)
                        {
                            Position[] poses = openPositionsInTab.ToArray();

                            for(int i2 = 1;i2 < poses.Length;i2++)
                            {
                                tabCurrent._journal.DeletePosition(poses[i2]);
                            }

                            Thread.Sleep(3000);
                            break;
                        }
                    }

                    // 5 проверяем экспирированные бумаги. Если Security равен null - удаляем такую вкладку

                    for (int i = 0; i < _posTabs.Tabs.Count; i++)
                    {
                        BotTabSimple tabCurrent = _posTabs.Tabs[i];

                        if (tabCurrent.Security == null)
                        {
                            LogIfFull($"Remove tab (security is null): {tabCurrent.Connector.SecurityName}");
                            _posTabs.RemoveTabBySecurityName(tabCurrent.Connector.SecurityName, tabCurrent.Connector.SecurityClass);
                            Thread.Sleep(1000);
                            break;
                        }
                    }
                }
                catch (Exception ex)
                {
                    SendNewLogMessage(ex.ToString(), Logging.LogMessageType.Error);
                    Thread.Sleep(5000);
                }
            }
        }

        private void WaitTabsDownload()
        {
            DateTime startWait = DateTime.Now;

            while (_posTabs.Tabs.Count != _notIgnoredSec.Count)
            {
                if (DateTime.Now > startWait.AddSeconds(10))
                {
                    break;
                }
            }

            for (int k = 0; k < _posTabs.Tabs.Count; k++)
            {
                if (_posTabs.Tabs[k].Security == null || _posTabs.Tabs[k].PriceBestAsk == 0 || _posTabs.Tabs[k].PriceBestBid == 0)
                {
                    k--;
                }

                if (DateTime.Now > startWait.AddSeconds(50))
                {
                    break;
                }
            }
        }

        #endregion

        #region No trade periods

        private StrategyParameterButton _tradePeriodsShowDialogButton;

        private NonTradePeriods _tradePeriodsSettings;

        private void _tradePeriodsShowDialogButton_UserClickOnButtonEvent()
        {
            _tradePeriodsSettings.ShowDialog();
        }

        #endregion

    }
}