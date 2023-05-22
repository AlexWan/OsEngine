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
            TabControlBotsName.SizeChanged += TabControlBotsName_SizeChanged;

            Title = panel.GetType().Name;
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
        }

        private void UpdateTabsInStopLimitViewer()
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

        private void BotPanelChartUi_Closed(object sender, EventArgs e)
        {
            Closed -= BotPanelChartUi_Closed;
            _panel.StopPaint();
            _panel.NewTabCreateEvent -= UpdateTabsInStopLimitViewer;
            _panel = null;
            LocationChanged -= RobotUi_LocationChanged;
            TabControlBotsName.SizeChanged -= TabControlBotsName_SizeChanged;

            if (_testerServer != null)
            {
                _testerServer.TestingFastEvent -= Serv_TestingFastEvent;
                _testerServer.TestingEndEvent -= _testerServer_TestingEndEvent;
                _testerServer = null;
            }

            _stopLimitsViewer.UserSelectActionEvent -= _stopLimitsViewer_UserSelectActionEvent;
            _stopLimitsViewer.LogMessageEvent -= SendNewLogMessage;
            _stopLimitsViewer.ClearDelete();
            _stopLimitsViewer = null;
        }

        // стоп - лимиты

        private void _stopLimitsViewer_UserSelectActionEvent(int limitNum, Alerts.SignalType signal)
        {
            try
            {

                List<BotTabSimple> allTabs = new List<BotTabSimple>();

                if(_panel.TabsSimple != null)
                {
                    allTabs.AddRange(_panel.TabsSimple);
                }
                if (_panel.TabsScreener != null)
                {
                    for(int i = 0;i < _panel.TabsScreener.Count;i++)
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

        // для тестирования

        TesterServer _testerServer = null;

        BuyAtStopPositionsViewer _stopLimitsViewer;

        private void Serv_TestingFastEvent()
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

        private void _testerServer_TestingEndEvent()
        {
             StartPaint();
             _panel.MoveChartToTheRight();
             _stopLimitsViewer.StartPaint();
        }

        public void StartPaint()
        {
            _panel.StartPaint(GridChart, ChartHostPanel, HostGlass, HostOpenPosition,
             HostClosePosition, HostBotLog, RectChart,
             HostAllert, TabControlBotTab, TextBoxPrice, GridChartControlPanel);
        }

        private BotPanel _panel;

        void TabControlBotsName_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            double up = TabControlBotsName.ActualHeight - 28;

            if (up < 0)
            {
                up = 0;
            }

            // GreedChartPanel.Margin = new Thickness(5, up, 315, 10);
        }

        private void RobotUi_LocationChanged(object sender, EventArgs e)
        {
            WindowCoordinate.X = Convert.ToDecimal(Left);
            WindowCoordinate.Y = Convert.ToDecimal(Top);
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

        private void buttonBuyFast_Click_1(object sender, RoutedEventArgs e)
        {
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

        private void buttonSellFast_Click(object sender, RoutedEventArgs e)
        {
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

        // ручное управление позицией

        private void ButtonBuyLimit_Click(object sender, RoutedEventArgs e)
        {
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

        private void ButtonSellLimit_Click(object sender, RoutedEventArgs e)
        {
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

        private void ButtonCloseLimit_Click(object sender, RoutedEventArgs e)
        {
            if (_panel.ActivTab.GetType().Name != "BotTabSimple")
            {
                return;
            }

            ((BotTabSimple)_panel.ActivTab).CloseAllOrderInSystem();
        }

        private JournalUi _journalUi;

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

                _journalUi = new JournalUi(panelsJournal, _panel.StartProgram);
                _journalUi.Closed += _journalUi_Closed;
                _journalUi.Show();
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        private void _journalUi_Closed(object sender, EventArgs e)
        {
            _journalUi.Closed -= _journalUi_Closed;
            _journalUi.IsErase = true;
            _journalUi = null;
        }

        private void ButtonRiskManager_Click(object sender, RoutedEventArgs e)
        {
            _panel.ShowPanelRiskManagerDialog();
        }

        private void ButtonStrategParametr_Click(object sender, RoutedEventArgs e)
        {
            _panel.ShowParametrDialog();
        }

        private void ButtonStrategIndividualSettings_Click(object sender, RoutedEventArgs e)
        {
            if (_panel == null)
            {
                return;
            }

            _panel.ShowIndividualSettingsDialog();
        }

        private void buttonStrategManualSettings_Click(object sender, RoutedEventArgs e)
        {
            if (_panel.ActivTab.GetType().Name != "BotTabSimple")
            {
                return;
            }

            ((BotTabSimple)_panel.ActivTab).ShowManualControlDialog();
        }

        private void SendNewLogMessage(string message, LogMessageType type)
        {
            if (LogMessageEvent != null)
            {
                LogMessageEvent(message, type);
            }
            else if (type == LogMessageType.Error)
            {
                MessageBox.Show(message);
            }
        }

        public event Action<string, LogMessageType> LogMessageEvent;

        private void ButtonRedactTab_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_panel == null)
                {
                    MessageBox.Show(OsLocalization.Trader.Label10);
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

        private void ButtonHideInformPanel_Click(object sender, RoutedEventArgs e)
        {
            HideInformPanel();
            SaveLeftPanelPosition();
        }

        private void ButtonShowInformPanel_Click(object sender, RoutedEventArgs e)
        {
            ShowInformPanel();
            SaveLeftPanelPosition();
        }

        private void ButtonHideShowSettingsPanel_Click(object sender, RoutedEventArgs e)
        {
            if (ButtonHideShowSettingsPanel.Content.ToString() == ">")
            {
                HideSettigsPanel();
            }
            else if (ButtonHideShowSettingsPanel.Content.ToString() == "<")
            {
                ShowSettingsPanel();
            }
            SaveLeftPanelPosition();
        }

        private void HideInformPanel()
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

        private void ShowInformPanel()
        {
            ButtonShowInformPanel.Visibility = Visibility.Hidden;

            GridPrime.RowDefinitions[1].Height = new GridLength(190);
            GreedTraderEngine.Margin = new Thickness(0, 0, 0, 182);
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

        private void HideSettigsPanel()
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

        private void ShowSettingsPanel()
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

        // сохранение и загрузка состояния схлопывающихся панелей

        private string _panelName;

        private bool _settingsPanelIsHide;

        private bool _informPanelIsHide;

        private void CheckPanels()
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
                    reader.Close();
                }
            }
            catch (Exception)
            {
                // ignore
            }

            if (_settingsPanelIsHide)
            {
                HideSettigsPanel();
            }

            if (_informPanelIsHide)
            {
                HideInformPanel();
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

                    writer.Close();
                }
            }
            catch (Exception)
            {
                // ignore
            }
        }

        private List<PositionOpenUi2> _guisOpenPos = new List<PositionOpenUi2>();

        private void ButtonMoreOpenPositionDetail_Click(object sender, RoutedEventArgs e)
        {
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

            for (int i = 0;i < _guisOpenPos.Count;i++)
            {
                if (_guisOpenPos[i].Tab.TabName == activTab.TabName)
                {
                    _guisOpenPos[i].Activate();
                    return;
                }
            }

            /*if(activTab.Connector.ServerType == ServerType.None
                || string.IsNullOrEmpty(activTab.Connector.SecurityName)
                || activTab.IsConnected == false 
                || activTab.IsReadyToTrade == false)
            {
                activTab.SetNewLogMessage(OsLocalization.Trader.Label195, LogMessageType.Error);
                return;
            }*/

            PositionOpenUi2 ui = new PositionOpenUi2(activTab);
            ui.Show();

            _guisOpenPos.Add(ui);

            ui.Closing += Ui_Closing;

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
            catch(Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }
    }
}