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

namespace OsEngine.OsTrader.Panels
{
    public partial class BotPanelChartUi
    {
        public BotPanelChartUi(BotPanel panel)
        {
            InitializeComponent();
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
                }

            }

            LocationChanged += RobotUi_LocationChanged;
            TabControlBotsName.SizeChanged += TabControlBotsName_SizeChanged;

            Title = panel.GetType().Name;
            TabControlBotsName.Items[0] = panel.NameStrategyUniq;
            ButtonShowInformPanel.Visibility = Visibility.Hidden;

            this.Activate();
            this.Focus();
        }

        // для тестирования

        TesterServer _testerServer = null;

        private void Serv_TestingFastEvent()
        {
            if (_testerServer.TestingFastIsActivate == true)
            {
                _panel.StopPaint();
            }
            else if (_testerServer.TestingFastIsActivate == false)
            {
                StartPaint();
                _panel.MoveChartToTheRight();
            }
        }

        private void BotPanelChartUi_Closed(object sender, EventArgs e)
        {
            Closed -= BotPanelChartUi_Closed;
            _panel.StopPaint();
            _panel = null;
            LocationChanged -= RobotUi_LocationChanged;
            TabControlBotsName.SizeChanged -= TabControlBotsName_SizeChanged;

            if (_testerServer != null)
            {
                _testerServer.TestingFastEvent -= Serv_TestingFastEvent;
                _testerServer = null;
            }
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

        // manual control of the position
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

        /// <summary>
        /// journal window
        /// </summary>
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

        /// <summary>
        /// send a new message 
        /// выслать новое сообщение на верх
        /// </summary>
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

        /// <summary>
        /// outgoing message for log
        /// исходящее сообщение для лога
        /// </summary>
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
        }

        private void ButtonShowInformPanel_Click(object sender, RoutedEventArgs e)
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

        }

        private void ButtonHideShowSettingsPanel_Click(object sender, RoutedEventArgs e)
        {
            if (ButtonHideShowSettingsPanel.Content.ToString() == ">")
            {
                HideSettigsPanel();
                ButtonHideShowSettingsPanel.Content = "<";
            }
            else if (ButtonHideShowSettingsPanel.Content.ToString() == "<")
            {
                ShowSettingsPanel();
                ButtonHideShowSettingsPanel.Content = ">";
            }

        }

        private void HideSettigsPanel()
        {
            GreedTraderEngine.Visibility = Visibility.Hidden;

            if (TabControlPrime.Visibility == Visibility.Visible)
            {
                GreedChartPanel.Margin = new Thickness(0, 26, 0, 10);
            }
            else
            {
                GreedChartPanel.Margin = new Thickness(0, 26, 0, 0);
            }

        }

        private void ShowSettingsPanel()
        {
            GreedTraderEngine.Visibility = Visibility.Visible;

            if (TabControlPrime.Visibility == Visibility.Visible)
            {
                GreedChartPanel.Margin = new Thickness(0, 26, 308, 10);
            }
            else
            {
                GreedChartPanel.Margin = new Thickness(0, 26, 308, 0);
            }
        }
    }
}
