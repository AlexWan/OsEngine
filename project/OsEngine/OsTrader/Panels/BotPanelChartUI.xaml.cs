/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using OsEngine.Charts.CandleChart;
using OsEngine.Entity;
using OsEngine.Journal;
using OsEngine.Language;
using OsEngine.Logging;
using OsEngine.Market;
using OsEngine.OsTrader.Panels.Tab;
using OsEngine.Market.Servers;
using OsEngine.Market.Servers.Tester;
using OsEngine.Layout;
using System.IO;
using OsEngine.OsTrader.Panels.Tab.Internal;
using OsEngine.Alerts;

namespace OsEngine.OsTrader.Panels
{
    public partial class BotPanelChartUi
    {

        #region Constructor

        public BotPanelChartUi(BotPanel panel)
        {
            InitializeComponent();
            OsEngine.Layout.StickyBorders.Listen(this);
            _panel = panel;
            StartPaint();
            Local();

            Closed += BotPanelChartUi_Closed;

            if (panel.StartProgram == StartProgram.IsTester)
            {
                List<IServer> servers = ServerMaster.GetServers();

                for (int i = 0; servers != null && i < servers.Count; i++)
                {
                    if (servers[i].ServerType == ServerType.Tester)
                    {
                        _testerServer = (TesterServer)servers[i];
                        break;
                    }
                }

                if (_testerServer != null)
                {
                    _testerServer.TestingFastEvent += Serv_TestingFastEvent;
                    _testerServer.TestingEndEvent += _testerServer_TestingEndEvent;
                }

            }

            LocationChanged += RobotUi_LocationChanged;

            if (string.IsNullOrEmpty(panel.PublicName) == false)
            {
                Title = panel.GetType().Name + " / " + panel.PublicName;
            }
            else
            {
                Title = panel.GetType().Name + " / " + panel.NameStrategyUniq;
            }

            TabControlBotsName.Items[0] = panel.NameStrategyUniq;
            ButtonShowInformPanel.Visibility = Visibility.Hidden;

            this.Activate();
            this.Focus();

            _panelName = panel.NameStrategyUniq;
            CheckPanels();

            GlobalGUILayout.Listen(this, "botPanel_" + panel.NameStrategyUniq);

            _stopLimitsViewer = new BuyAtStopPositionsViewer(HostStopLimits, panel.StartProgram);
            _stopLimitsViewer.UserSelectActionEvent += _stopLimitsViewer_UserSelectActionEvent;
            _stopLimitsViewer.LogMessageEvent += SendNewLogMessage;

            UpdateTabsInStopLimitViewer();
            panel.NewTabCreateEvent += UpdateTabsInStopLimitViewer;

            rectToMove.MouseEnter += RectToMove_MouseEnter;
            rectToMove.MouseLeave += RectToMove_MouseLeave;
            rectToMove.MouseDown += RectToMove_MouseDown;
        }

        private BotPanel _panel;

        private void BotPanelChartUi_Closed(object sender, EventArgs e)
        {
            try
            {
                Closed -= BotPanelChartUi_Closed;

                rectToMove.MouseEnter -= RectToMove_MouseEnter;
                rectToMove.MouseLeave -= RectToMove_MouseLeave;
                rectToMove.MouseDown -= RectToMove_MouseDown;

                LocationChanged -= RobotUi_LocationChanged;

                if (_panel != null)
                {
                    _panel.StopPaint();
                    _panel.NewTabCreateEvent -= UpdateTabsInStopLimitViewer;
                    _panel = null;
                }

                if (_testerServer != null)
                {
                    _testerServer.TestingFastEvent -= Serv_TestingFastEvent;
                    _testerServer.TestingEndEvent -= _testerServer_TestingEndEvent;
                    _testerServer = null;
                }

                if (_stopLimitsViewer != null)
                {
                    _stopLimitsViewer.UserSelectActionEvent -= _stopLimitsViewer_UserSelectActionEvent;
                    _stopLimitsViewer.LogMessageEvent -= SendNewLogMessage;
                    _stopLimitsViewer.ClearDelete();
                    _stopLimitsViewer = null;
                }
            }
            catch
            {
                // ignore
            }
        }

        private void Local()
        {
            if (!TabPozition.CheckAccess())
            {
                TabPozition.Dispatcher.Invoke(new Action(Local));
                return;
            }

            TabPozition.Header = OsLocalization.Trader.Label18;
            TabItemClosedPos.Header = OsLocalization.Trader.Label19;
            TabItemLogBot.Header = OsLocalization.Trader.Label23;
            TabItemMarketDepth.Header = OsLocalization.Trader.Label25;
            TabItemAlerts.Header = OsLocalization.Trader.Label26;
            TabItemControl.Header = OsLocalization.Trader.Label27;
            TabItemStopLimits.Header = OsLocalization.Trader.Label193;
            ButtonBuyFast.Content = OsLocalization.Trader.Label28;
            ButtonSellFast.Content = OsLocalization.Trader.Label29;
            TextBoxVolumeInterText.Text = OsLocalization.Trader.Label30;
            TextBoxPriceText.Text = OsLocalization.Trader.Label31;
            ButtonBuyLimit.Content = OsLocalization.Trader.Label32;
            ButtonSellLimit.Content = OsLocalization.Trader.Label33;
            ButtonCloseLimit.Content = OsLocalization.Trader.Label34;
            LabelGeneralSettings.Content = OsLocalization.Trader.Label35;
            ButtonJournalCommunity.Content = OsLocalization.Trader.Label40;
            ButtonStrategParametr.Content = OsLocalization.Trader.Label45;
            ButtonRiskManager.Content = OsLocalization.Trader.Label46;
            ButtonStrategSettings.Content = OsLocalization.Trader.Label47;
            ButtonStrategSettingsIndividual.Content = OsLocalization.Trader.Label43;
            ButtonRedactTab.Content = OsLocalization.Trader.Label44;
            ButtonMoreOpenPositionDetail.Content = OsLocalization.Trader.Label197;
        }

        #endregion

        #region Stop-Limits

        private void UpdateTabsInStopLimitViewer()
        {
            try
            {
                List<BotTabSimple> allTabs = new List<BotTabSimple>();

                if (_panel.TabsSimple != null)
                {
                    allTabs.AddRange(_panel.TabsSimple);
                }
                if (_panel.TabsScreener != null)
                {
                    for (int i = 0; i < _panel.TabsScreener.Count; i++)
                    {
                        if (_panel.TabsScreener[i].Tabs != null)
                        {
                            allTabs.AddRange(_panel.TabsScreener[i].Tabs);
                        }
                    }
                }

                _stopLimitsViewer.LoadTabToWatch(allTabs);
            }
            catch (Exception ex)
            {
                _panel.SendNewLogMessage(ex.ToString(), LogMessageType.Error);
            }
        }

        private BuyAtStopPositionsViewer _stopLimitsViewer;

        private void _stopLimitsViewer_UserSelectActionEvent(int limitNum, Alerts.SignalType signal)
        {
            try
            {

                List<BotTabSimple> allTabs = new List<BotTabSimple>();

                if (_panel.TabsSimple != null)
                {
                    allTabs.AddRange(_panel.TabsSimple);
                }
                if (_panel.TabsScreener != null)
                {
                    for (int i = 0; i < _panel.TabsScreener.Count; i++)
                    {
                        if (_panel.TabsScreener[i].Tabs != null)
                        {
                            allTabs.AddRange(_panel.TabsScreener[i].Tabs);
                        }
                    }
                }

                for (int i = 0; i < allTabs.Count; i++)
                {
                    if (signal == SignalType.DeleteAllPoses)
                    {
                        allTabs[i].BuyAtStopCancel();
                        allTabs[i].SellAtStopCancel();
                    }
                    else
                    {
                        for (int i2 = 0; i2 < allTabs[i].PositionOpenerToStop.Count; i2++)
                        {
                            if (allTabs[i].PositionOpenerToStop[i2].Number == limitNum)
                            {
                                allTabs[i].PositionOpenerToStop.RemoveAt(i2);
                                allTabs[i].UpdateStopLimits();
                                return;
                            }
                        }
                    }
                }
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        #endregion

        #region Tester

        private TesterServer _testerServer = null;

        private void Serv_TestingFastEvent()
        {
            try
            {
                if (_testerServer.TestingFastIsActivate == true)
                {
                    _panel.StopPaint();
                    _stopLimitsViewer.StopPaint();
                }
                else if (_testerServer.TestingFastIsActivate == false)
                {
                    StartPaint();
                    _panel.MoveChartToTheRight();
                    _stopLimitsViewer.StartPaint();
                }
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        private void _testerServer_TestingEndEvent()
        {
            try
            {
                StartPaint();
                _panel.MoveChartToTheRight();
                _stopLimitsViewer.StartPaint();
            }
            catch (Exception ex)
            {
                SendNewLogMessage(ex.ToString(), LogMessageType.Error);
            }
        }

        #endregion

        #region Location

        public void StartPaint()
        {
            try
            {
                _panel.StartPaint(GridChart, ChartHostPanel, HostGlass, HostOpenPosition,
                HostClosePosition, HostBotLog, RectChart,
                HostAllert, TabControlBotTab, TextBoxPrice, GridChartControlPanel, TextBoxVolumeFast);
            }
            catch (Exception ex)
            {
                SendNewLogMessage(ex.ToString(), LogMessageType.Error);
            }
        }

        private void RobotUi_LocationChanged(object sender, EventArgs e)
        {
            WindowCoordinate.X = Convert.ToDecimal(Left);
            WindowCoordinate.Y = Convert.ToDecimal(Top);
        }

        #endregion

        #region Journal

        private JournalUi2 _journalUi;

        private void ButtonJournalCommunity_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_journalUi != null)
                {
                    _journalUi.Activate();
                    return;
                }

                List<BotPanelJournal> panelsJournal = new List<BotPanelJournal>();

                List<Journal.Journal> journals = _panel.GetJournals();


                BotPanelJournal botPanel = new BotPanelJournal();
                botPanel.BotName = _panel.NameStrategyUniq;
                botPanel.BotClass = _panel.GetNameStrategyType();

                botPanel._Tabs = new List<BotTabJournal>();

                for (int i2 = 0; journals != null && i2 < journals.Count; i2++)
                {
                    BotTabJournal botTabJournal = new BotTabJournal();
                    botTabJournal.TabNum = i2;
                    botTabJournal.Journal = journals[i2];
                    botPanel._Tabs.Add(botTabJournal);
                }

                panelsJournal.Add(botPanel);

                _journalUi = new JournalUi2(panelsJournal, _panel.StartProgram);
                _journalUi.Closed += _journalUi_Closed;
                _journalUi.LogMessageEvent += _journalUi_LogMessageEvent;
                _journalUi.Show();
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        private void _journalUi_LogMessageEvent(string message, LogMessageType type)
        {
            try
            {
                if (_panel == null)
                {
                    return;
                }
                _panel.SendNewLogMessage(message, type);
            }
            catch (Exception ex)
            {
                SendNewLogMessage(ex.ToString(), LogMessageType.Error);
            }
        }

        private void _journalUi_Closed(object sender, EventArgs e)
        {
            try
            {
                _journalUi.Closed -= _journalUi_Closed;
                _journalUi.LogMessageEvent -= _journalUi_LogMessageEvent;
                _journalUi.IsErase = true;
                _journalUi = null;
            }
            catch (Exception ex)
            {
                SendNewLogMessage(ex.ToString(), LogMessageType.Error);
            }
        }

        #endregion

        #region Trading

        private void buttonBuyFast_Click_1(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_panel.ActivTab == null)
                {
                    return;
                }

                if (_panel.ActivTab.GetType().Name != "BotTabSimple")
                {
                    return;
                }

                decimal volume;

                try
                {
                    volume = TextBoxVolumeFast.Text.ToDecimal();
                }
                catch (Exception)
                {
                    MessageBox.Show(OsLocalization.Trader.Label49);
                    return;
                }
             ((BotTabSimple)_panel.ActivTab).BuyAtMarket(volume);
            }
            catch (Exception ex)
            {
                SendNewLogMessage(ex.ToString(), LogMessageType.Error);
            }
        }

        private void buttonSellFast_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_panel.ActivTab == null)
                {
                    return;
                }
                if (_panel.ActivTab.GetType().Name != "BotTabSimple")
                {
                    return;
                }
                decimal volume;
                try
                {
                    volume = TextBoxVolumeFast.Text.ToDecimal();
                }
                catch (Exception)
                {
                    MessageBox.Show(OsLocalization.Trader.Label49);
                    return;
                }
            ((BotTabSimple)_panel.ActivTab).SellAtMarket(volume);
            }
            catch (Exception ex)
            {
                SendNewLogMessage(ex.ToString(), LogMessageType.Error);
            }
        }

        private void ButtonBuyLimit_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_panel.ActivTab == null)
                {
                    return;
                }
                if (_panel.ActivTab.GetType().Name != "BotTabSimple")
                {
                    return;
                }
                decimal volume;
                try
                {
                    volume = Decimal.Parse(TextBoxVolumeFast.Text.Replace(",",
                            CultureInfo.InvariantCulture.NumberFormat.NumberDecimalSeparator),
                        CultureInfo.InvariantCulture);
                }
                catch (Exception)
                {
                    MessageBox.Show(OsLocalization.Trader.Label49);
                    return;
                }

                decimal price;

                try
                {
                    price = TextBoxPrice.Text.ToDecimal();
                }
                catch (Exception)
                {
                    MessageBox.Show(OsLocalization.Trader.Label50);
                    return;
                }

                if (price == 0)
                {
                    MessageBox.Show(OsLocalization.Trader.Label50);
                    return;
                }
            ((BotTabSimple)_panel.ActivTab).BuyAtLimit(volume, price);
            }
            catch (Exception ex)
            {
                SendNewLogMessage(ex.ToString(), LogMessageType.Error);
            }
        }

        private void ButtonSellLimit_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_panel.ActivTab == null)
                {
                    return;
                }
                if (_panel.ActivTab.GetType().Name != "BotTabSimple")
                {
                    return;
                }
                decimal volume;
                try
                {
                    volume = TextBoxVolumeFast.Text.ToDecimal();
                }
                catch (Exception)
                {
                    MessageBox.Show(OsLocalization.Trader.Label49);
                    return;
                }

                decimal price;

                try
                {
                    price = TextBoxPrice.Text.ToDecimal();
                }
                catch (Exception)
                {
                    MessageBox.Show(OsLocalization.Trader.Label50);
                    return;
                }

                if (price == 0)
                {
                    MessageBox.Show(OsLocalization.Trader.Label50);
                    return;
                }

            ((BotTabSimple)_panel.ActivTab).SellAtLimit(volume, price);
            }
            catch (Exception ex)
            {
                SendNewLogMessage(ex.ToString(), LogMessageType.Error);
            }
        }

        private void ButtonCloseLimit_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_panel.ActivTab == null)
                {
                    return;
                }
                if (_panel.ActivTab.GetType().Name != "BotTabSimple")
                {
                    return;
                }

            ((BotTabSimple)_panel.ActivTab).CloseAllOrderInSystem();
            }
            catch (Exception ex)
            {
                SendNewLogMessage(ex.ToString(), LogMessageType.Error);
            }
        }

        #endregion

        #region Bot managment

        private void ButtonRiskManager_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                _panel.ShowPanelRiskManagerDialog();
            }
            catch (Exception ex)
            {
                SendNewLogMessage(ex.ToString(), LogMessageType.Error);
            }
        }

        private void ButtonStrategParametr_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                _panel.ShowParametrDialog();
            }
            catch (Exception ex)
            {
                SendNewLogMessage(ex.ToString(), LogMessageType.Error);
            }
        }

        private void ButtonStrategIndividualSettings_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_panel == null)
                {
                    return;
                }

                _panel.ShowIndividualSettingsDialog();
            }
            catch (Exception ex)
            {
                SendNewLogMessage(ex.ToString(), LogMessageType.Error);
            }
        }

        private void ButtonStrategManualSettings_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_panel.ActivTab == null)
                {
                    return;
                }
                if (_panel.ActivTab.GetType().Name == "BotTabSimple")
                {
                    ((BotTabSimple)_panel.ActivTab).ShowManualControlDialog();
                }
                else if (_panel.ActivTab.GetType().Name == "BotTabScreener")
                {
                    ((BotTabScreener)_panel.ActivTab).ShowManualControlDialog();
                }
            }
            catch (Exception ex)
            {
                SendNewLogMessage(ex.ToString(), LogMessageType.Error);
            }
        }

        private void ButtonRedactTab_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_panel == null)
                {
                    MessageBox.Show(OsLocalization.Trader.Label10);
                    return;
                }
                if (_panel.ActivTab == null)
                {
                    return;
                }

                if (_panel.ActivTab.GetType().Name == "BotTabSimple")
                {
                    ((BotTabSimple)_panel.ActivTab).ShowConnectorDialog();
                }
                else if (_panel.ActivTab != null &&
                    _panel.ActivTab.GetType().Name == "BotTabIndex")
                {
                    ((BotTabIndex)_panel.ActivTab).ShowDialog();
                }
                else if (_panel.ActivTab != null &&
                         _panel.ActivTab.GetType().Name == "BotTabCluster")
                {
                    ((BotTabCluster)_panel.ActivTab).ShowDialog();
                }
                else if (_panel.ActivTab != null &&
                        _panel.ActivTab.GetType().Name == "BotTabScreener")
                {
                    ((BotTabScreener)_panel.ActivTab).ShowDialog();
                }
                else
                {
                    MessageBox.Show(OsLocalization.Trader.Label11);
                }
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        #endregion

        #region Open position GUI

        private List<PositionOpenUi2> _guisOpenPos = new List<PositionOpenUi2>();

        private void ButtonMoreOpenPositionDetail_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_panel.ActivTab == null)
                {
                    return;
                }

                BotTabSimple activTab = null;

                try
                {
                    if (_panel.ActivTab.GetType().Name != "BotTabSimple")
                    {
                        return;
                    }

                    activTab = (BotTabSimple)_panel.ActivTab;
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.Message);
                    return;
                }

                for (int i = 0; i < _guisOpenPos.Count; i++)
                {
                    if (_guisOpenPos[i].Tab.TabName == activTab.TabName)
                    {
                        _guisOpenPos[i].Activate();
                        return;
                    }
                }

                PositionOpenUi2 ui = new PositionOpenUi2(activTab);
                ui.Show();

                _guisOpenPos.Add(ui);

                ui.Closing += Ui_Closing;
            }
            catch (Exception ex)
            {
                SendNewLogMessage(ex.ToString(), LogMessageType.Error);
            }
        }

        private void Ui_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            try
            {
                PositionOpenUi2 myUi = (PositionOpenUi2)sender;

                for (int i = 0; i < _guisOpenPos.Count; i++)
                {
                    if (_guisOpenPos[i].Tab.TabName == myUi.Tab.TabName)
                    {
                        _guisOpenPos.RemoveAt(i);
                        return;
                    }
                }
            }
            catch (Exception ex)
            {
                SendNewLogMessage(ex.ToString(), LogMessageType.Error);
            }
        }

        #endregion

        #region Resizing areas

        private string _panelName;

        private bool _settingsPanelIsHide;

        private bool _informPanelIsHide;

        private bool _lowPanelIsBig;

        private void CheckPanels()
        {
            try
            {
                if (!File.Exists(@"Engine\LayoutRobotUi" + _panelName + ".txt"))
                {
                    return;
                }

                try
                {
                    using (StreamReader reader = new StreamReader(@"Engine\LayoutRobotUi" + _panelName + ".txt"))
                    {
                        _settingsPanelIsHide = Convert.ToBoolean(reader.ReadLine());
                        _informPanelIsHide = Convert.ToBoolean(reader.ReadLine());
                        _lowPanelIsBig = Convert.ToBoolean(reader.ReadLine());
                        reader.Close();
                    }
                }
                catch (Exception)
                {
                    // ignore
                }

                if (_settingsPanelIsHide)
                {
                    HideSettingsPanel();
                }

                if (_informPanelIsHide)
                {
                    HideInformPanel();
                }

                if (_informPanelIsHide == false &&
                    _lowPanelIsBig)
                {
                    DoBigLowPanel();
                }
            }
            catch (Exception ex)
            {
                SendNewLogMessage(ex.ToString(), LogMessageType.Error);
            }
        }

        private void SaveLeftPanelPosition()
        {
            try
            {
                using (StreamWriter writer = new StreamWriter(@"Engine\LayoutRobotUi" + _panelName + ".txt", false))
                {
                    writer.WriteLine(_settingsPanelIsHide);
                    writer.WriteLine(_informPanelIsHide);
                    writer.WriteLine(_lowPanelIsBig);
                    writer.Close();
                }
            }
            catch (Exception ex)
            {
                SendNewLogMessage(ex.ToString(), LogMessageType.Error);
            }
        }

        private void ButtonHideInformPanel_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                HideInformPanel();
                SaveLeftPanelPosition();
            }
            catch (Exception ex)
            {
                SendNewLogMessage(ex.ToString(), LogMessageType.Error);
            }
        }

        private void ButtonShowInformPanel_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                ShowInformPanel();
                SaveLeftPanelPosition();
            }
            catch (Exception ex)
            {
                SendNewLogMessage(ex.ToString(), LogMessageType.Error);
            }
        }

        private void ButtonHideShowSettingsPanel_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (ButtonHideShowSettingsPanel.Content.ToString() == ">")
                {
                    HideSettingsPanel();
                }
                else if (ButtonHideShowSettingsPanel.Content.ToString() == "<")
                {
                    ShowSettingsPanel();
                }
                SaveLeftPanelPosition();
            }
            catch (Exception ex)
            {
                SendNewLogMessage(ex.ToString(), LogMessageType.Error);
            }
        }

        private void HideInformPanel()
        {
            try
            {
                TabControlPrime.Visibility = Visibility.Hidden;
                GridPrime.RowDefinitions[1].Height = new GridLength(0);
                GreedTraderEngine.Margin = new Thickness(0, 0, 0, 0);
                ButtonShowInformPanel.Visibility = Visibility.Visible;

                //GreedChartPanel.Margin = new Thickness(0, 26, 308, 0);

                if (GreedTraderEngine.Visibility == Visibility.Visible)
                {
                    GreedChartPanel.Margin = new Thickness(0, 26, 308, 0);
                }
                else
                {
                    GreedChartPanel.Margin = new Thickness(0, 26, 0, 0);
                }
                _informPanelIsHide = true;
            }
            catch (Exception ex)
            {
                SendNewLogMessage(ex.ToString(), LogMessageType.Error);
            }
        }

        private void ShowInformPanel()
        {
            try
            {
                ButtonShowInformPanel.Visibility = Visibility.Hidden;

                GridPrime.RowDefinitions[1].Height = new GridLength(190);
                GreedTraderEngine.Margin = new Thickness(0, 0, 0, 182);
                GreedPozitonLogHost.Height = 167;

                TabControlPrime.Visibility = Visibility.Visible;

                //GreedChartPanel.Margin = new Thickness(0, 26, 308, 10);

                if (GreedTraderEngine.Visibility == Visibility.Visible)
                {
                    GreedChartPanel.Margin = new Thickness(0, 26, 308, 10);
                }
                else
                {
                    GreedChartPanel.Margin = new Thickness(0, 26, 0, 10);
                }
                _informPanelIsHide = false;
            }
            catch (Exception ex)
            {
                SendNewLogMessage(ex.ToString(), LogMessageType.Error);
            }
        }

        private void HideSettingsPanel()
        {
            try
            {
                ButtonHideShowSettingsPanel.Content = "<";
                GreedTraderEngine.Visibility = Visibility.Hidden;

                if (TabControlPrime.Visibility == Visibility.Visible)
                {
                    GreedChartPanel.Margin = new Thickness(0, 26, 0, 10);
                }
                else
                {
                    GreedChartPanel.Margin = new Thickness(0, 26, 0, 0);
                }
                _settingsPanelIsHide = true;
            }
            catch (Exception ex)
            {
                SendNewLogMessage(ex.ToString(), LogMessageType.Error);
            }
        }

        private void ShowSettingsPanel()
        {
            try
            {
                ButtonHideShowSettingsPanel.Content = ">";
                GreedTraderEngine.Visibility = Visibility.Visible;

                if (TabControlPrime.Visibility == Visibility.Visible)
                {
                    GreedChartPanel.Margin = new Thickness(0, 26, 308, 10);
                }
                else
                {
                    GreedChartPanel.Margin = new Thickness(0, 26, 308, 0);
                }
                _settingsPanelIsHide = false;
            }
            catch (Exception ex)
            {
                SendNewLogMessage(ex.ToString(), LogMessageType.Error);
            }
        }

        #endregion

        #region Increasing the log area 

        private void RectToMove_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            try
            {
                if (GreedPozitonLogHost.Cursor == System.Windows.Input.Cursors.ScrollN)
                {
                    DoBigLowPanel();
                    _lowPanelIsBig = true;
                    SaveLeftPanelPosition();
                }
                else if (GreedPozitonLogHost.Cursor == System.Windows.Input.Cursors.ScrollS)
                {
                    DoSmallLowPanel();
                    _lowPanelIsBig = false;
                    SaveLeftPanelPosition();
                }
            }
            catch (Exception ex)
            {
                _panel.SendNewLogMessage(ex.ToString(), LogMessageType.Error);
            }
        }

        private void RectToMove_MouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
        {
            try
            {
                GreedPozitonLogHost.Cursor = System.Windows.Input.Cursors.Arrow;
            }
            catch (Exception ex)
            {
                _panel.SendNewLogMessage(ex.ToString(), LogMessageType.Error);
            }
        }

        private void RectToMove_MouseEnter(object sender, System.Windows.Input.MouseEventArgs e)
        {
            try
            {
                if (GridPrime.RowDefinitions[1].Height.Value == 190)
                {
                    GreedPozitonLogHost.Cursor = System.Windows.Input.Cursors.ScrollN;
                }
                if (GridPrime.RowDefinitions[1].Height.Value == 500)
                {
                    GreedPozitonLogHost.Cursor = System.Windows.Input.Cursors.ScrollS;
                }
            }
            catch (Exception ex)
            {
                _panel.SendNewLogMessage(ex.ToString(), LogMessageType.Error);
            }
        }

        private void DoBigLowPanel()
        {
            GridPrime.RowDefinitions[1].Height = new GridLength(500, GridUnitType.Pixel);
            GreedTraderEngine.Margin = new Thickness(0, 0, 0, 492);
            GreedPozitonLogHost.Height = 475;
            this.MinHeight = 600;
        }

        private void DoSmallLowPanel()
        {
            GridPrime.RowDefinitions[1].Height = new GridLength(190, GridUnitType.Pixel);
            GreedTraderEngine.Margin = new Thickness(0, 0, 0, 182);
            GreedPozitonLogHost.Height = 167;
            this.MinHeight = 300;

        }

        #endregion

        #region Logging

        private void SendNewLogMessage(string message, LogMessageType type)
        {
            if (LogMessageEvent != null)
            {
                LogMessageEvent(message, type);
            }
            else if(_panel != null)
            {
                _panel.SendNewLogMessage(message, type);
            }
            else if (type == LogMessageType.Error)
            {
                MessageBox.Show(message);
            }
        }

        public event Action<string, LogMessageType> LogMessageEvent;

        #endregion
    }
}