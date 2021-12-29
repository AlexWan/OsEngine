﻿/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System;
using System.Globalization;
using System.Windows;
using OsEngine.Charts.CandleChart;
using OsEngine.Entity;
using OsEngine.Language;
using OsEngine.Market;

namespace OsEngine.OsTrader.Gui
{
    public partial class TesterUi
    {
        public TesterUi()
        {
            InitializeComponent();
            ServerMaster.SetHostTable(HostPositionOnBoard, HostOrdersOnBoard);
            ServerMaster.CreateServer(ServerType.Tester,false);
            ServerMaster.GetServers();

            _strategyKeeper = new OsTraderMaster(GridChart,
                ChartHostPanel, HostGlass, HostOpenPosition, HostClosePosition, HostAllPosition,
                HostBotLog, HostBotLogPrime, RectChart, HostAllert, TabControlBotsName, TabControlBotTab, TextBoxPrice,
                GridChartControlPanel,StartProgram.IsTester);
            LocationChanged += TesterUi_LocationChanged;
            LabelOsa.Content = "V_" + System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;

            TabControlBotsName.SizeChanged += TabControlBotsName_SizeChanged;

            Closing += TesterUi_Closing;

            Local();
            TabControlMd.SelectedIndex = 2;
        }

        private void Local()
        {
            TabPozition.Header = OsLocalization.Trader.Label18;
            TabItemClosedPos.Header = OsLocalization.Trader.Label19;
            TabItemAllPos.Header = OsLocalization.Trader.Label20;
            TextBoxPositionBord.Header = OsLocalization.Trader.Label21;
            TextBoxPositionAllOrders.Header = OsLocalization.Trader.Label22;
            TabItemLogBot.Header = OsLocalization.Trader.Label23;
            TabItemLogPrime.Header = OsLocalization.Trader.Label24;
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
            LabelBotControl.Content = OsLocalization.Trader.Label36;
            ButtonServer.Content = OsLocalization.Trader.Label37;
            ButtonNewBot.Content = OsLocalization.Trader.Label38;
            ButtonDeleteBot.Content = OsLocalization.Trader.Label39;
            ButtonJournalCommunity.Content = OsLocalization.Trader.Label40;
            ButtonRiskManagerCommunity.Content = OsLocalization.Trader.Label41;
            CheckBoxPaintOnOff.Content = OsLocalization.Trader.Label42;
            ButtonStrategSettingsIndividual.Content = OsLocalization.Trader.Label43;
            ButtonRedactTab.Content = OsLocalization.Trader.Label44;
            ButtonStrategParametr.Content = OsLocalization.Trader.Label45;
            ButtonRiskManager.Content = OsLocalization.Trader.Label46;
            ButtonStrategSettings.Content = OsLocalization.Trader.Label47;
            ButtonUpdateBot.Content = OsLocalization.Trader.Label159;
            ButtonUpdateBot.ToolTip = OsLocalization.Trader.Label160;
        }

        void TesterUi_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            AcceptDialogUi ui = new AcceptDialogUi(OsLocalization.Trader.Label48);
            ui.ShowDialog();

            if (ui.UserAcceptActioin == false)
            {
                e.Cancel = true;
            }
        }

        /// <summary>
        /// changed the size of tabbox with the names of robots
        /// изменился размер табБокса с именами роботов
        /// </summary>
        void TabControlBotsName_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            double up = TabControlBotsName.ActualHeight - 28;

            if (up < 0)
            {
                up = 0;
            }

            GreedChartPanel.Margin = new Thickness(5, up, 315, 10);
        }

        private void TesterUi_LocationChanged(object sender, EventArgs e)
        {
            WindowCoordinate.X = Convert.ToDecimal(Left);
            WindowCoordinate.Y = Convert.ToDecimal(Top);
        }

        private OsTraderMaster _strategyKeeper;


// buttons with talking names / кнопки с говорящими названиями

        private void buttonBuyFast_Click_1(object sender, RoutedEventArgs e)
        {
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
            _strategyKeeper.BotBuyMarket(volume);
        }

        private void buttonSellFast_Click(object sender, RoutedEventArgs e)
        {
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
            _strategyKeeper.BotSellMarket(volume);
        }

        private void ButtonStrategIndividualSettings_Click(object sender, RoutedEventArgs e)
        {
            _strategyKeeper.BotIndividualSettings();
        }

        private void ButtonBuyLimit_Click(object sender, RoutedEventArgs e)
        {
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

            _strategyKeeper.BotBuyLimit(volume,price);
        }

        private void ButtonSellLimit_Click(object sender, RoutedEventArgs e)
        {
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

            _strategyKeeper.BotSellLimit(volume, price);
        }

        private void ButtonCloseLimit_Click(object sender, RoutedEventArgs e)
        {
            _strategyKeeper.CancelLimits();
        }

        private void ButtonServer_Click(object sender, RoutedEventArgs e)
        {
            ServerMaster.ShowDialog(true);
        }

        private void ButtonNewBot_Click(object sender, RoutedEventArgs e)
        {
            _strategyKeeper.CreateNewBot();
        }

        private void ButtonDeleteBot_Click(object sender, RoutedEventArgs e)
        {
            _strategyKeeper.DeleteActiv();
        }

        private void buttonStrategManualSettings_Click(object sender, RoutedEventArgs e)
        {
            _strategyKeeper.BotManualSettingsDialog();
        }

        private void ButtonUpdateBot_Click(object sender, RoutedEventArgs e)
        {
            _strategyKeeper.HotUpdateActiveBot();
        }

        private void ButtonJournalCommunity_Click(object sender, RoutedEventArgs e)
        {
            _strategyKeeper.ShowCommunityJournal();
        }

        private void ButtonRedactTab_Click(object sender, RoutedEventArgs e)
        {
            _strategyKeeper.BotTabConnectorDialog();
        }

        private void ButtonRiskManagerCommunity_Click(object sender, RoutedEventArgs e)
        {
            _strategyKeeper.ShowRiskManagerDialog();
        }

        private void ButtonRiskManager_Click(object sender, RoutedEventArgs e)
        {
            _strategyKeeper.BotShowRiskManager();
        }

        private void ButtonStrategParametr_Click(object sender, RoutedEventArgs e)
        {
            _strategyKeeper.BotShowParametrsDialog();
        }
    }
}
