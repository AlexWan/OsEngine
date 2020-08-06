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
using System.Windows.Shapes;
using OsEngine.Charts.CandleChart;
using OsEngine.Entity;
using OsEngine.Journal;
using OsEngine.Language;
using OsEngine.Logging;
using OsEngine.Market;
using OsEngine.Market.Connectors;
using OsEngine.OsTrader.Panels.Tab;

namespace OsEngine.OsTrader.Panels
{
    public partial class BotPanelChartUi
    {
        public BotPanelChartUi(BotPanel panel)
        {
            InitializeComponent();
            _panel = panel;

            _panel.StartPaint(GridChart, ChartHostPanel, HostGlass, HostOpenPosition,
                HostClosePosition, HostBotLog, RectChart,
                HostAllert, TabControlBotTab, TextBoxPrice, GreedChartPanel);

            LocationChanged += RobotUi_LocationChanged;
            TabControlBotsName.SizeChanged += TabControlBotsName_SizeChanged;

            Closed += delegate (object sender, EventArgs args)
            {
                _panel.StopPaint();
                _panel = null;
            };

            Local();
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
                _journalUi.Closed += delegate (object o, EventArgs args)
                {
                    _journalUi.IsErase = true;
                    _journalUi = null;
                };

                _journalUi.Show();
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        private void ButtonRiskManager_Click(object sender, RoutedEventArgs e)
        {
            _panel.ShowPanelRiskManagerDialog();
        }

        private void ButtonStrategParametr_Click(object sender, RoutedEventArgs e)
        {
            _panel.ShowParametrDialog();
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

    }
}
