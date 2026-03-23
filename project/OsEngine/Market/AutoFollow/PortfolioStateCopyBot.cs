/*
 * Your rights to use code governed by this license http://o-s-a.net/doc/license_simple_engine.pdf
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using OsEngine.Candles.Series;
using OsEngine.Entity;
using OsEngine.Language;
using OsEngine.Market.Connectors;
using OsEngine.Market.Servers;
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

        private WindowsFormsHost _host;
        private DataGridView _grid;

        private StrategyParameterString _regime;

        private NonTradePeriods _tradePeriodsSettings;
        private StrategyParameterButton _tradePeriodsShowDialogButton;

        private List<string> _defaultIgnoredSec = ["RUB", "Rub", "rub", "USDT", "USD", "Usd", "Eur", "EUR"];

        private List<Tuple<Security, Side, decimal>> _notIgnoredSec = [];
        private List<Security> _securitiesAll;
        private bool _hasBotStarted = false;

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

            _tradePeriodsShowDialogButton = CreateParameterButton(OsLocalization.Market.ServerParam14);
            _tradePeriodsShowDialogButton.UserClickOnButtonEvent += _tradePeriodsShowDialogButton_UserClickOnButtonEvent;
  
            CustomTabToParametersUi customTab = ParamGuiSettings.CreateCustomTab(OsLocalization.Market.LabelIgnorSec);

            LoadIgnoredPos();

            customTab.AddChildren(_host);

            DeleteEvent += DeleteBotEvent;
    
            _grid.Click += _grid_Click;

            _mainTab.SecuritySubscribeEvent += _mainTab_SecuritySubscribeEvent;

            Thread worker = new Thread(WorkerPlace);
            worker.Start();
        }

        private void _mainTab_SecuritySubscribeEvent(Security sec)
        {
            try
            {
                _securitiesAll = _mainTab.Connector.MyServer.Securities;

                if (_securitiesAll == null
                    || _securitiesAll.Count == 0)
                {
                    SendNewLogMessage(OsLocalization.Market.Message112, Logging.LogMessageType.Error);
                    return;
                }


                // поиск не игнорируемых бумаг
                List<PositionOnBoard> positionsOnExchange = _mainTab.Portfolio.PositionOnBoard;

                if (positionsOnExchange == null
                   || positionsOnExchange.Count == 0)
                {
                    SendNewLogMessage(OsLocalization.Market.Message111, Logging.LogMessageType.Error);
                    return;
                }

                for (int i = 0; i < positionsOnExchange.Count; i++)
                {
                    bool isIgnored = false;

                    for (int j = 0; j < _defaultIgnoredSec.Count; j++)
                    {
                        if (_defaultIgnoredSec[j] == positionsOnExchange[i].SecurityNameCode)
                        {
                            isIgnored = true;
                            break;
                        }
                    }

                    if (isIgnored)
                        continue;

                    Security secInServer = _securitiesAll.Find(s => s.Name.StartsWith(positionsOnExchange[i].SecurityNameCode));

                    if (secInServer == null) // в инструментах коннектора не нашли инструмент из портфеля
                    {
                        SendNewLogMessage(positionsOnExchange[i].SecurityNameCode + OsLocalization.Market.Message107 + OsLocalization.Market.Message108, Logging.LogMessageType.Error);
                        continue;
                    }

                    Side posExchangeDirection = positionsOnExchange[i].ValueCurrent < 0 ? Side.Sell : Side.Buy;

                    _notIgnoredSec.Add(new Tuple<Security, Side, decimal>(secInServer, posExchangeDirection, positionsOnExchange[i].ValueCurrent));
                }


                if (_posTabs.Tabs.Count > 0 && _posTabs.PositionsOpenAll.Count > 0)
                {
                    // Позиции восстановлены после перезапуска
                    _hasBotStarted = true;
                    return;
                }

                IServer sourceServer = _mainTab.Connector.MyServer;

                // настраиваем скринер
                _posTabs.SecuritiesClass = sec.NameClass;
                _posTabs.TimeFrame = TimeFrame.Hour1;
                _posTabs.PortfolioName = _mainTab.Portfolio.Number;
                _posTabs.ServerType = sourceServer.ServerType;
                _posTabs.ServerName = sourceServer.ServerNameAndPrefix;

                _posTabs.CandleCreateMethodType = CandleCreateMethodType.Simple.ToString();
                ((Simple)_posTabs.CandleSeriesRealization).TimeFrame = TimeFrame.Hour1;
                ((Simple)_posTabs.CandleSeriesRealization).TimeFrameParameter.ValueString = TimeFrame.Hour1.ToString();

                // добавление бумаг в скринер
                List<ActivatedSecurity> securitiesToScreener = new List<ActivatedSecurity>();

                for (int i = 0; i < _notIgnoredSec.Count; i++)
                {
                    ActivatedSecurity secToScreener = new ActivatedSecurity();
                    secToScreener.SecurityClass = _notIgnoredSec[i].Item1.NameClass;
                    secToScreener.SecurityName = _notIgnoredSec[i].Item1.Name;
                    secToScreener.IsOn = true;
                    securitiesToScreener.Add(secToScreener);
                }

                for (int i = 0; i < securitiesToScreener.Count; i++)
                {
                    if (_posTabs.SecuritiesNames.Find(s => s.SecurityName == securitiesToScreener[i].SecurityName) == null)
                    {
                        _posTabs.SecuritiesNames.Add(securitiesToScreener[i]);
                    }
                }

                _posTabs.SaveSettings();
                _posTabs.NeedToReloadTabs = true;

                WaitTabsDownload();

                // создаём позиции из того, что уже есть в портфеле
                for (int j = 0; j < _notIgnoredSec.Count; j++)
                {
                    BotTabSimple tab = _posTabs.Tabs.Find(t => t.Security.Name == _notIgnoredSec[j].Item1.Name);

                    if (tab != null)
                    {
                        Position newDeal = tab._dealCreator.CreatePosition(tab.TabName, _notIgnoredSec[j].Item2, _notIgnoredSec[j].Item2 == Side.Buy ? tab.PriceBestAsk : tab.PriceBestBid,
                                           Math.Abs(_notIgnoredSec[j].Item3), OrderPriceType.Market, tab.ManualPositionSupport.SecondToOpen, _notIgnoredSec[j].Item1, tab.Portfolio,
                                           tab.StartProgram, tab.ManualPositionSupport.OrderTypeTime, tab.ManualPositionSupport.LimitsMakerOnly);

                        newDeal.NameBotClass = tab.BotClassName;

                        tab._journal.SetNewDeal(newDeal);

                        tab.OrderFakeExecute(newDeal.OpenOrders[0], DateTime.Now);
                    }
                }

                _hasBotStarted = true;
            }
            catch (Exception ex)
            {
                SendNewLogMessage(ex.Message, Logging.LogMessageType.Error);
            }
        }

        private void WaitTabsDownload()
        {
            DateTime startWait = DateTime.Now;

            while (_posTabs.Tabs.Count != _notIgnoredSec.Count)
            {
                if(DateTime.Now > startWait.AddSeconds(10))
                {
                    SendNewLogMessage(OsLocalization.Market.Message110, Logging.LogMessageType.Error);
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
                    SendNewLogMessage(OsLocalization.Market.Message110, Logging.LogMessageType.Error);
                    break;
                }
            }
        }

        #region work with grid

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

                            _hasBotStarted = false;

                            BotTabSimple tab = _posTabs.Tabs.Find(t => t.Security.Name.StartsWith(newSec));

                            if (tab != null && tab.PositionsOpenAll.Count == 1)   // у бота есть позиция с таким инструментом
                            {
                                tab.CloseAtFake(tab.PositionsOpenAll[0], tab.PositionsOpenAll[0].MaxVolume, tab.PriceBestAsk, DateTime.Now);

                                _posTabs.RemoveTabBySecurityName(sec.Item1.Name, sec.Item1.NameClass);

                                _posTabs.SaveSettings();
                                _posTabs.NeedToReloadTabs = true;
                            }    

                            _hasBotStarted = true;
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

                        if (_securitiesAll != null)
                        {
                            Security secInServer = _securitiesAll.Find(s => s.Name.StartsWith(_grid.Rows[rowIndex].Cells[0].Value.ToString()));

                            if (secInServer == null)
                            {
                                CustomMessageBoxUi boxUi = new CustomMessageBoxUi(_grid.Rows[rowIndex].Cells[0].Value + OsLocalization.Market.Message107 + OsLocalization.Market.Message109);
                                boxUi.ShowDialog();
                                return;
                            }
                        }
                        else
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


        // Loading saved table data
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

        #endregion

        // Worker place

        private bool _botIsDelete = false;

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

                    if (!_mainTab.IsConnected)
                    {
                        Thread.Sleep(1000);
                        continue;
                    }

                    if (!_hasBotStarted)
                    {
                        Thread.Sleep(1000);
                        continue;
                    }

                    if (_tradePeriodsSettings.CanTradeThisTime(_mainTab.TimeServerCurrent) == false)
                    {
                        continue;
                    }

                    // отслеживание изменений в портфеле
                    List<PositionOnBoard> positionsOnExchange = _mainTab.Portfolio.PositionOnBoard;
     
                    List<Position> botPositions = _posTabs.PositionsOpenAll;

                    List<ActivatedSecurity> securitiesToScreener = new List<ActivatedSecurity>();

                    for (int i = 0; i < positionsOnExchange.Count; i++)
                    {
                        bool isIgnored = false;

                        for (int j = 0; j < _defaultIgnoredSec.Count; j++)
                        {
                            if (_defaultIgnoredSec[j] == positionsOnExchange[i].SecurityNameCode)
                            {
                                isIgnored = true;
                                break;
                            }
                        }

                        if (isIgnored)
                            continue;
   
                        Side posExchangeDirection = positionsOnExchange[i].ValueCurrent < 0 ? Side.Sell : Side.Buy;

                        Security security = _securitiesAll.Find(s => s.Name.StartsWith(positionsOnExchange[i].SecurityNameCode));

                        if (security == null)
                        {
                            SendNewLogMessage(positionsOnExchange[i].SecurityNameCode + OsLocalization.Market.Message107, Logging.LogMessageType.Error);
                            continue;
                        }
  
                        BotTabSimple tab = _posTabs.Tabs.Find(t => t.Security.Name.StartsWith(positionsOnExchange[i].SecurityNameCode));

                        if (tab != null && tab.PositionsOpenAll.Count == 1)   // у бота есть позиция с таким инструментом
                        {
                            if(positionsOnExchange[i].ValueCurrent == 0) // бумаги нет в портфеле
                            {
                                tab.CloseAtFake(tab.PositionsOpenAll[0], tab.PositionsOpenAll[0].MaxVolume, tab.PriceBestAsk, DateTime.Now);
                                _mainTab.Portfolio.PositionOnBoard.Remove(positionsOnExchange[i]);
                                continue;
                            }
                            else if (tab.PositionsOpenAll[0].Direction != posExchangeDirection 
                                || tab.PositionsOpenAll[0].OpenVolume != Math.Abs(positionsOnExchange[i].ValueCurrent))      // проверка направления и объема
                            {
                                tab.CloseAtFake(tab.PositionsOpenAll[0], tab.PositionsOpenAll[0].MaxVolume, tab.PriceBestAsk, DateTime.Now);

                                Tuple<Security, Side, decimal> changedSec = _notIgnoredSec.Find(s => s.Item1.Name == security.Name);

                                if (changedSec != null)
                                {
                                    _notIgnoredSec.Remove(changedSec);

                                    _notIgnoredSec.Add(new Tuple<Security, Side, decimal>(security, posExchangeDirection, positionsOnExchange[i].ValueCurrent));
                                }
                    
                                Position newDeal = tab._dealCreator.CreatePosition(tab.TabName, posExchangeDirection, posExchangeDirection == Side.Buy ? tab.PriceBestAsk : tab.PriceBestBid,
                                                   Math.Abs(positionsOnExchange[i].ValueCurrent), OrderPriceType.Market, tab.ManualPositionSupport.SecondToOpen, security, tab.Portfolio,
                                                   tab.StartProgram, tab.ManualPositionSupport.OrderTypeTime, tab.ManualPositionSupport.LimitsMakerOnly);

                                newDeal.NameBotClass = tab.BotClassName;

                                tab._journal.SetNewDeal(newDeal);

                                tab.OrderFakeExecute(newDeal.OpenOrders[0], DateTime.Now);
                            }
      
                            continue;

                        }
                        else // добавить инструмент 
                        {
                            if (positionsOnExchange[i].ValueCurrent == 0) // ещё сделки нет
                            {
                                if (positionsOnExchange[i].ValueBlocked == 0)
                                    _mainTab.Portfolio.PositionOnBoard.Remove(positionsOnExchange[i]);

                                continue;
                            }

                            ActivatedSecurity secToScreener = new ActivatedSecurity();
                            secToScreener.SecurityClass = security.NameClass;
                            secToScreener.SecurityName = security.Name;
                            secToScreener.IsOn = true;
                            securitiesToScreener.Add(secToScreener);

                            _notIgnoredSec.Add(new Tuple<Security, Side, decimal>(security, posExchangeDirection, positionsOnExchange[i].ValueCurrent));
                        }
                    }

                    if(securitiesToScreener.Count > 0) // создать новые позиции
                    {
                        _posTabs.SecuritiesNames.AddRange(securitiesToScreener);

                        _posTabs.SaveSettings();
                        _posTabs.NeedToReloadTabs = true;

                        WaitTabsDownload();

                        for (int j = 0; j < securitiesToScreener.Count; j++)
                        {
                            Tuple<Security, Side, decimal> sec = _notIgnoredSec.Find(s => s.Item1.Name == securitiesToScreener[j].SecurityName);

                            BotTabSimple tab = _posTabs.Tabs.Find(t => t.Security.Name == securitiesToScreener[j].SecurityName);

                            if (tab != null)
                            {
                                Position newDeal = tab._dealCreator.CreatePosition(tab.TabName, sec.Item2, sec.Item2 == Side.Buy ? tab.PriceBestAsk : tab.PriceBestBid,
                                                   Math.Abs(sec.Item3), OrderPriceType.Market, tab.ManualPositionSupport.SecondToOpen, sec.Item1, tab.Portfolio,
                                                   tab.StartProgram, tab.ManualPositionSupport.OrderTypeTime, tab.ManualPositionSupport.LimitsMakerOnly);

                                newDeal.NameBotClass = tab.BotClassName;

                                tab._journal.SetNewDeal(newDeal);

                                tab.OrderFakeExecute(newDeal.OpenOrders[0], DateTime.Now);
                            }
                        }
                    }

                    // проверка удаления бумаг
                    for (int k = 0; k < _notIgnoredSec.Count; k++)
                    {
                        PositionOnBoard removedSec = positionsOnExchange.Find(p => p.SecurityNameCode == _notIgnoredSec[k].Item1.Name);

                        if (removedSec == null) //  этой бумаги уже нет в портфеле
                        {
                            _posTabs.RemoveTabBySecurityName(_notIgnoredSec[k].Item1.Name, _notIgnoredSec[k].Item1.NameClass);

                            _notIgnoredSec.Remove(_notIgnoredSec[k]);
                        }
                    }
    
                    if (_notIgnoredSec.Count == 0 && _posTabs.Tabs.Count > 0)
                    {
                        for (int i = 0; i < _posTabs.Tabs.Count; i++)
                        {
                            if (_posTabs.Tabs[i].PositionsOpenAll.Count == 0)
                            {
                                _posTabs.RemoveTabBySecurityName(_posTabs.Tabs[i].Security.Name, _posTabs.Tabs[i].Security.NameClass);
                                _posTabs.Tabs.Remove(_posTabs.Tabs[i]);
                            }

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

        // Delete Bot
        private void DeleteBotEvent()
        {
            if (File.Exists(@"Engine\" + NameStrategyUniq + "-IgnoredPos.txt"))
            {
                File.Delete(@"Engine\" + NameStrategyUniq + "-IgnoredPos.txt");
            }

            _botIsDelete = true;
        }

        private void _tradePeriodsShowDialogButton_UserClickOnButtonEvent()
        {
            _tradePeriodsSettings.ShowDialog();
        }

        // The name of the robot in OsEngine
        public override string GetNameStrategyType()
        {
            return "PortfolioStateCopyBot";
        }
    }
}
