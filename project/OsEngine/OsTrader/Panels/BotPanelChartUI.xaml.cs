/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms.Integration;
using System.Windows.Threading;
using OsEngine.Charts.CandleChart;
using OsEngine.Charts.MarketDepthChart;
using OsEngine.Entity;
using OsEngine.IndicatorMarketDepth;
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
            OsEngine.Layout.StartupLocation.Start_FitHeightToWorkArea(this);
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

            _startTitle = Title;

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

            TabControlBotTab.SelectionChanged += TabControlBotTab_SelectionChanged;
            TabControlBotTab_SelectionChanged(null, null);

            TabControlMarketDepth.SelectionChanged += TabControlMarketDepth_SelectionChanged;

            _mdRepaintTimer = new DispatcherTimer();
            _mdRepaintTimer.Interval = TimeSpan.FromMilliseconds(700);
            _mdRepaintTimer.Tick += _mdRepaintTimer_Tick;
            _mdRepaintTimer.Start();
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

                TabControlBotTab.SelectionChanged -= TabControlBotTab_SelectionChanged;
                TabControlMarketDepth.SelectionChanged -= TabControlMarketDepth_SelectionChanged;

                if (_mdRepaintTimer != null)
                {
                    _mdRepaintTimer.Stop();
                    _mdRepaintTimer.Tick -= _mdRepaintTimer_Tick;
                    _mdRepaintTimer = null;
                }

                ClearMarketDepthIndicatorTabs();

                UnsubscribeAllMdRepaint();

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

                if (HostGlass != null)
                {
                    HostGlass.Child = null;
                }
                if (HostAlert != null)
                {
                    HostAlert.Child = null;
                }
                if (HostGrids != null)
                {
                    HostGrids.Child = null;
                }
                if (HostOpenPosition != null)
                {
                    HostOpenPosition.Child = null;
                }
                if (HostStopLimits != null)
                {
                    HostStopLimits.Child = null;
                }
                if (HostClosePosition != null)
                {
                    HostClosePosition.Child = null;
                }
                if (HostBotLog != null)
                {
                    HostBotLog.Child = null;
                }
                if (ChartHostPanel != null)
                {
                    ChartHostPanel.Child = null;
                }
            }
            catch (Exception ex)
            {
                SendNewLogMessage(ex.ToString(), LogMessageType.Error);
            }
        }

        private void Local()
        {
            if (!TabPosition.CheckAccess())
            {
                TabPosition.Dispatcher.Invoke(new Action(Local));
                return;
            }

            TabPosition.Header = OsLocalization.Trader.Label18;
            TabItemClosedPos.Header = OsLocalization.Trader.Label19;
            TabItemLogBot.Header = OsLocalization.Trader.Label23;
            TabItemMarketDepth.Header = OsLocalization.Trader.Label25;
            TabItemMarketDepthBase.Header = OsLocalization.ConvertToLocString("Eng:Base_Ru:База_");
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
            ButtonStrategyParameter.Content = OsLocalization.Trader.Label45;
            ButtonRiskManager.Content = OsLocalization.Trader.Label46;
            ButtonStrategySettings.Content = OsLocalization.Trader.Label47;
            ButtonStrategySettingsIndividual.Content = OsLocalization.Trader.Label43;
            ButtonRedactTab.Content = OsLocalization.Trader.Label44;
            ButtonMoreOpenPositionDetail.Content = OsLocalization.Trader.Label197;
            ButtonAddVisualAlert.Content = OsLocalization.Trader.Label440;
            ButtonAddPriceAlert.Content = OsLocalization.Trader.Label441;
            TabItemGrids.Header = OsLocalization.Trader.Label437;
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
                _panel?.SendNewLogMessage(ex.ToString(), LogMessageType.Error);
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
                HostAlert, TabControlBotTab, TextBoxPrice, GridChartControlPanel, 
                TextBoxVolumeFast, TabControlControl,HostGrids);
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

        #region Title managment

        private void TabControlBotTab_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            try
            {
                IIBotTab currentTab = _panel.ActiveTab;

                if(currentTab == null)
                {
                    return;
                }

                if (currentTab.EventsIsOn == false)
                {
                    Title = _startTitle + " / " + OsLocalization.Trader.Label435;
                }
                else
                {
                    Title = _startTitle;
                }

                RebuildMarketDepthIndicatorTabs();
            }
            catch (Exception ex)
            {
                _panel.SendNewLogMessage(ex.ToString(), LogMessageType.Error); 
            }
        }

        private string _startTitle;

        #endregion

        #region Market depth indicators

        private DispatcherTimer _mdRepaintTimer;

        private Dictionary<AMarketDepth, MarketDepthIndicatorChart> _mdIndicatorCharts = new Dictionary<AMarketDepth, MarketDepthIndicatorChart>();

        private HashSet<MarketDepthIndicatorsMaster> _mdRepaintSubscribedMasters = new HashSet<MarketDepthIndicatorsMaster>();

        private bool _ignoreMdTabSelection;

        private BotTabSimple GetActiveSimpleTab()
        {
            if (_panel == null || _panel.ActiveTab == null)
            {
                return null;
            }

            if (_panel.ActiveTab.GetType().Name != "BotTabSimple")
            {
                return null;
            }

            return (BotTabSimple)_panel.ActiveTab;
        }

        private void ClearMarketDepthIndicatorTabs()
        {
            for (int i = TabControlMarketDepth.Items.Count - 1; i >= 0; i--)
            {
                if (TabControlMarketDepth.Items[i] == TabItemMarketDepthBase)
                {
                    continue;
                }

                if (TabControlMarketDepth.Items[i] == TabItemMarketDepthPlus)
                {
                    continue;
                }

                TabControlMarketDepth.Items.RemoveAt(i);
            }

            foreach (KeyValuePair<AMarketDepth, MarketDepthIndicatorChart> pair in _mdIndicatorCharts)
            {
                pair.Value.StopPaint();
            }

            _mdIndicatorCharts.Clear();
        }

        private void RebuildMarketDepthIndicatorTabs()
        {
            _ignoreMdTabSelection = true;

            ClearMarketDepthIndicatorTabs();
            UnsubscribeAllMdRepaint();

            BotTabSimple tab = GetActiveSimpleTab();

            if (tab != null && tab.MarketDepthIndicators != null)
            {
            List<AMarketDepth> indicators = tab.MarketDepthIndicators.Indicators;

            SubscribeMdRepaint(tab.MarketDepthIndicators);

            for (int i = 0; i < indicators.Count; i++)
                {
                    AddMarketDepthIndicatorTab(indicators[i]);
                }
            }

            _ignoreMdTabSelection = false;

            TabControlMarketDepth.SelectedItem = TabItemMarketDepthBase;
        }

        private TabItem AddMarketDepthIndicatorTab(AMarketDepth indicator)
        {
            TabItem tab = new TabItem();
            tab.Header = indicator.Name;
            tab.Tag = indicator;

            Grid grid = new Grid();
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

            Grid topRow = new Grid();
            topRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            topRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            TextBlock typeText = new TextBlock();
            typeText.Text = indicator.GetType().Name;
            typeText.VerticalAlignment = VerticalAlignment.Center;
            typeText.HorizontalAlignment = HorizontalAlignment.Left;
            typeText.Margin = new Thickness(2, 0, 8, 0);
            typeText.Foreground = Themes.ThemeManager.GetBrush("LabelForegroundBrush");

            StackPanel buttonsPanel = new StackPanel();
            buttonsPanel.Orientation = Orientation.Horizontal;
            buttonsPanel.Margin = new Thickness(0, 2, 2, 2);

            Button editButton = new Button();
            editButton.Content = OsLocalization.ConvertToLocString("Eng:Edit_Ru:Ред_");
            editButton.Tag = indicator;
            editButton.Click += SettingsMarketDepthIndicatorButton_Click;
            editButton.Margin = new Thickness(0, 0, 4, 0);
            editButton.Width = 46;
            editButton.Height = 22;

            Button deleteButton = new Button();
            deleteButton.Content = OsLocalization.ConvertToLocString("Eng:Delete_Ru:Удал_");
            deleteButton.Tag = indicator;
            deleteButton.Click += RemoveMarketDepthIndicatorButton_Click;
            deleteButton.Width = 46;
            deleteButton.Height = 22;

            buttonsPanel.Children.Add(editButton);
            buttonsPanel.Children.Add(deleteButton);

            Grid.SetColumn(typeText, 0);
            Grid.SetColumn(buttonsPanel, 1);

            topRow.Children.Add(typeText);
            topRow.Children.Add(buttonsPanel);

            WindowsFormsHost host = new WindowsFormsHost();

            MarketDepthIndicatorChart chart = new MarketDepthIndicatorChart();
            chart.StartPaint(host, indicator.Name);
            _mdIndicatorCharts[indicator] = chart;

            Grid.SetRow(topRow, 0);
            Grid.SetRow(host, 1);

            grid.Children.Add(topRow);
            grid.Children.Add(host);

            tab.Content = grid;

            int plusIndex = TabControlMarketDepth.Items.Count - 1;
            TabControlMarketDepth.Items.Insert(plusIndex, tab);

            return tab;
        }

        private void RemoveMarketDepthIndicatorButton_Click(object sender, RoutedEventArgs e)
        {
            Button button = (Button)sender;
            AMarketDepth indicator = (AMarketDepth)button.Tag;

            AcceptDialogUi ui = new AcceptDialogUi(OsLocalization.ConvertToLocString(
                "Eng:Are you sure you want to delete the indicator?_Ru:Вы уверены что хотите удалить индикатор?_"));
            ui.ShowDialog();

            if (ui.UserAcceptAction == false)
            {
                return;
            }

            BotTabSimple tab = GetActiveSimpleTab();

            if (tab == null || tab.MarketDepthIndicators == null)
            {
                return;
            }

            tab.MarketDepthIndicators.DeleteIndicator(indicator);

            RebuildMarketDepthIndicatorTabs();
        }

        private void SettingsMarketDepthIndicatorButton_Click(object sender, RoutedEventArgs e)
        {
            Button button = (Button)sender;
            AMarketDepth indicator = (AMarketDepth)button.Tag;

            indicator.ShowDialog();
        }

        private void TabControlMarketDepth_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_ignoreMdTabSelection)
            {
                return;
            }

            if (TabControlMarketDepth.SelectedItem != TabItemMarketDepthPlus)
            {
                return;
            }

            BotTabSimple tab = GetActiveSimpleTab();

            if (tab == null || tab.MarketDepthIndicators == null)
            {
                ResetMdTabSelection();
                return;
            }

            List<string> existingNames = new List<string>();

            for (int i = 0; i < tab.MarketDepthIndicators.Indicators.Count; i++)
            {
                existingNames.Add(tab.MarketDepthIndicators.Indicators[i].Name);
            }

            MarketDepthIndicatorCreateUi ui = new MarketDepthIndicatorCreateUi(existingNames);
            ui.ShowDialog();

            if (ui.IsAccepted == false)
            {
                ResetMdTabSelection();
                return;
            }

            AMarketDepth indicator = tab.MarketDepthIndicators.CreateIndicator(ui.IndicatorType, ui.IndicatorName);

            TabItem createdTab = null;

            if (indicator != null)
            {
                createdTab = AddMarketDepthIndicatorTab(indicator);
            }

            if (createdTab != null)
            {
                SelectMdTab(createdTab);
            }
            else
            {
                ResetMdTabSelection();
            }
        }

        private void SelectMdTab(TabItem tab)
        {
            if (tab == null)
            {
                return;
            }

            Dispatcher.BeginInvoke(new Action(() =>
            {
                TabControlMarketDepth.SelectedItem = tab;
            }));
        }

        private void ResetMdTabSelection()
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                TabControlMarketDepth.SelectedItem = TabItemMarketDepthBase;
            }));
        }

        private void SubscribeMdRepaint(MarketDepthIndicatorsMaster master)
        {
            if (master == null)
            {
                return;
            }

            if (_mdRepaintSubscribedMasters.Add(master))
            {
                master.NeedToRePaintEvent += MarketDepthIndicators_NeedToRePaintEvent;
            }
        }

        private void UnsubscribeAllMdRepaint()
        {
            foreach (MarketDepthIndicatorsMaster master in _mdRepaintSubscribedMasters)
            {
                master.NeedToRePaintEvent -= MarketDepthIndicators_NeedToRePaintEvent;
            }

            _mdRepaintSubscribedMasters.Clear();
        }

        private bool IsMarketDepthAreaVisible()
        {
            if (IsVisible == false)
            {
                return false;
            }

            if (WindowState == WindowState.Minimized)
            {
                return false;
            }

            if (TabControlControl == null || TabControlMarketDepth == null)
            {
                return false;
            }

            if (TabControlControl.SelectedItem != TabItemMarketDepth)
            {
                return false;
            }

            object selected = TabControlMarketDepth.SelectedItem;

            if (selected == null || selected == TabItemMarketDepthBase || selected == TabItemMarketDepthPlus)
            {
                return false;
            }

            return true;
        }

        private void MarketDepthIndicators_NeedToRePaintEvent()
        {
            if (IsMarketDepthAreaVisible() == false)
            {
                return;
            }

            foreach (KeyValuePair<AMarketDepth, MarketDepthIndicatorChart> pair in _mdIndicatorCharts)
            {
                pair.Value.PaintIndicator(pair.Key);
            }
        }

        private void _mdRepaintTimer_Tick(object sender, EventArgs e)
        {
            if (IsMarketDepthAreaVisible() == false)
            {
                return;
            }

            foreach (KeyValuePair<AMarketDepth, MarketDepthIndicatorChart> pair in _mdIndicatorCharts)
            {
                pair.Value.PaintIndicator(pair.Key);
            }
        }

        #endregion

        #region Journal

        private void ButtonJournalCommunity_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                _panel.ShowJournalDialog();
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        #endregion

        #region Trading

        private void buttonBuyFast_Click_1(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_panel.ActiveTab == null)
                {
                    return;
                }

                if (_panel.ActiveTab.GetType().Name != "BotTabSimple")
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
             ((BotTabSimple)_panel.ActiveTab).BuyAtMarket(volume);
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
                if (_panel.ActiveTab == null)
                {
                    return;
                }
                if (_panel.ActiveTab.GetType().Name != "BotTabSimple")
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
            ((BotTabSimple)_panel.ActiveTab).SellAtMarket(volume);
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
                if (_panel.ActiveTab == null)
                {
                    return;
                }
                if (_panel.ActiveTab.GetType().Name != "BotTabSimple")
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
            ((BotTabSimple)_panel.ActiveTab).BuyAtLimit(volume, price);
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
                if (_panel.ActiveTab == null)
                {
                    return;
                }
                if (_panel.ActiveTab.GetType().Name != "BotTabSimple")
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

            ((BotTabSimple)_panel.ActiveTab).SellAtLimit(volume, price);
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
                if (_panel.ActiveTab == null)
                {
                    return;
                }
                if (_panel.ActiveTab.GetType().Name != "BotTabSimple")
                {
                    return;
                }

            ((BotTabSimple)_panel.ActiveTab).CloseAllOrderInSystem();
            }
            catch (Exception ex)
            {
                SendNewLogMessage(ex.ToString(), LogMessageType.Error);
            }
        }

        #endregion

        #region Alert

        private void ButtonAddVisualAlert_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_panel.ActiveTab == null)
                {
                    SendNewLogMessage(OsLocalization.Trader.Label438, LogMessageType.Error);
                    return;
                }

                if (_panel.ActiveTab is BotTabSimple)
                {
                    BotTabSimple tab = (BotTabSimple)_panel.ActiveTab;

                    if(tab.IsConnected == false)
                    {
                        SendNewLogMessage(OsLocalization.Trader.Label442, LogMessageType.Error);
                        return;
                    }

                    tab._alerts.ShowAlertNewDialog(AlertType.ChartAlert);
                }
                else
                {
                    SendNewLogMessage(OsLocalization.Trader.Label439, LogMessageType.Error);
                }
            }
            catch (Exception ex)
            {
                _panel.SendNewLogMessage(ex.ToString(), LogMessageType.Error);  
            }
        }

        private void ButtonAddPriceAlert_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_panel.ActiveTab == null)
                {
                    SendNewLogMessage(OsLocalization.Trader.Label438, LogMessageType.Error);
                    return;
                }

                if (_panel.ActiveTab is BotTabSimple)
                {
                    BotTabSimple tab = (BotTabSimple)_panel.ActiveTab;

                    if (tab.IsConnected == false)
                    {
                        SendNewLogMessage(OsLocalization.Trader.Label442, LogMessageType.Error);
                        return;
                    }

                    tab._alerts.ShowAlertNewDialog(AlertType.PriceAlert);
                }
                else
                {
                    SendNewLogMessage(OsLocalization.Trader.Label439, LogMessageType.Error);
                }
            }
            catch (Exception ex)
            {
                _panel.SendNewLogMessage(ex.ToString(), LogMessageType.Error);
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
                _panel.ShowParameterDialog();
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
                if (_panel.ActiveTab == null)
                {
                    return;
                }
                if (_panel.ActiveTab.GetType().Name == "BotTabSimple")
                {
                    ((BotTabSimple)_panel.ActiveTab).ShowManualControlDialog();
                }
                else if (_panel.ActiveTab.GetType().Name == "BotTabScreener")
                {
                    ((BotTabScreener)_panel.ActiveTab).ShowManualControlDialog();
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
                if (_panel.ActiveTab == null)
                {
                    return;
                }

                if (_panel.ActiveTab.GetType().Name == "BotTabSimple")
                {
                    ((BotTabSimple)_panel.ActiveTab).ShowConnectorDialog();
                }
                else if (_panel.ActiveTab != null &&
                    _panel.ActiveTab.GetType().Name == "BotTabIndex")
                {
                    ((BotTabIndex)_panel.ActiveTab).ShowDialog();
                }
                else if (_panel.ActiveTab != null &&
                         _panel.ActiveTab.GetType().Name == "BotTabCluster")
                {
                    ((BotTabCluster)_panel.ActiveTab).ShowDialog();
                }
                else if (_panel.ActiveTab != null &&
                        _panel.ActiveTab.GetType().Name == "BotTabScreener")
                {
                    ((BotTabScreener)_panel.ActiveTab).ShowDialog();
                }
                else if (_panel.ActiveTab != null &&
                 _panel.ActiveTab.GetType().Name == "BotTabNews")
                {
                    ((BotTabNews)_panel.ActiveTab).ShowDialog();
                }
                else if (_panel.ActiveTab != null &&
                 _panel.ActiveTab.GetType().Name == "BotTabOptions")
                {
                    ((BotTabOptions)_panel.ActiveTab).ShowDialog();
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
                if (_panel.ActiveTab == null)
                {
                    return;
                }

                BotTabSimple activTab = null;

                try
                {
                    if (_panel.ActiveTab.GetType().Name != "BotTabSimple")
                    {
                        return;
                    }

                    activTab = (BotTabSimple)_panel.ActiveTab;
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
                        if (_guisOpenPos[i].WindowState == System.Windows.WindowState.Minimized)
                        {
                            _guisOpenPos[i].WindowState = System.Windows.WindowState.Normal;
                        }

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
                    TabControlBotsName.Margin = new Thickness(28, 0, 315, 0);
                }
                else
                {
                    GreedChartPanel.Margin = new Thickness(0, 26, 0, 0);
                    TabControlBotsName.Margin = new Thickness(28, 0, 7, 0);
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
                GreedPositionLogHost.Height = 167;

                TabControlPrime.Visibility = Visibility.Visible;

                //GreedChartPanel.Margin = new Thickness(0, 26, 308, 10);

                if (GreedTraderEngine.Visibility == Visibility.Visible)
                {
                    GreedChartPanel.Margin = new Thickness(0, 26, 308, 10);
                    TabControlBotsName.Margin = new Thickness(28, 0, 315, 0);
                }
                else
                {
                    GreedChartPanel.Margin = new Thickness(0, 26, 0, 10);
                    TabControlBotsName.Margin = new Thickness(28, 0, 7, 0);
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
                    TabControlBotsName.Margin = new Thickness(28, 0, 7, 0);
                }
                else
                {
                    GreedChartPanel.Margin = new Thickness(0, 26, 0, 0);
                    TabControlBotsName.Margin = new Thickness(28, 0, 7, 0);
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
                    TabControlBotsName.Margin = new Thickness(28, 0, 315, 0);
                }
                else
                {
                    GreedChartPanel.Margin = new Thickness(0, 26, 308, 0);
                    TabControlBotsName.Margin = new Thickness(28, 0, 315, 0);
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
                if (GreedPositionLogHost.Cursor == System.Windows.Input.Cursors.ScrollN)
                {
                    DoBigLowPanel();
                    _lowPanelIsBig = true;
                    SaveLeftPanelPosition();
                }
                else if (GreedPositionLogHost.Cursor == System.Windows.Input.Cursors.ScrollS)
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
                GreedPositionLogHost.Cursor = System.Windows.Input.Cursors.Arrow;
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
                    GreedPositionLogHost.Cursor = System.Windows.Input.Cursors.ScrollN;
                }
                if (GridPrime.RowDefinitions[1].Height.Value == 500)
                {
                    GreedPositionLogHost.Cursor = System.Windows.Input.Cursors.ScrollS;
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
            GreedPositionLogHost.Height = 475;
            this.MinHeight = 600;
        }

        private void DoSmallLowPanel()
        {
            GridPrime.RowDefinitions[1].Height = new GridLength(190, GridUnitType.Pixel);
            GreedTraderEngine.Margin = new Thickness(0, 0, 0, 182);
            GreedPositionLogHost.Height = 167;
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
