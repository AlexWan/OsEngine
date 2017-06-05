﻿/*
 *Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System;
using System.Windows;
using OsEngine.Charts.CandleChart;
using OsEngine.Market.Servers;

namespace OsEngine.OsTrader.Gui
{
    /// <summary>
    /// Логика взаимодействия для TesterOneSecurityDialog.xaml
    /// </summary>
    public partial class TesterUi
    {
        public TesterUi()
        {
            InitializeComponent();

            ServerMaster.IsTester = true;
            ServerMaster.SetHostTable(HostPositionOnBoard, HostOrdersOnBoard);
            ServerMaster.CreateServer(ServerType.Tester,false);
            ServerMaster.GetServers();

            _strategyKeeper = new OsTraderMaster(
            ChartHostPanel, HostGlass, HostOpenPosition, HostClosePosition, HostAllPosition,
            HostBotLog, HostBotLogPrime, RectChart, HostAllert, TabControlBotsName, TabControlBotTab, TextBoxPrice);

            TabControlBotsName.SizeChanged += TabControlBotsName_SizeChanged;
        }

        /// <summary>
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



        private OsTraderMaster _strategyKeeper;


 // кнопки с говорящими названиями

        private void buttonBuyFast_Click_1(object sender, RoutedEventArgs e)
        {
            int volume;
            try
            {
                volume = Convert.ToInt32(TextBoxVolumeFast.Text);
            }
            catch (Exception)
            {
                MessageBox.Show("В графе объём неправильное значение");
                return;
            }
            _strategyKeeper.BotBuyMarket(volume);
        }

        private void buttonSellFast_Click(object sender, RoutedEventArgs e)
        {
            int volume;
            try
            {
                volume = Convert.ToInt32(TextBoxVolumeFast.Text);
            }
            catch (Exception)
            {
                MessageBox.Show("В графе объём неправильное значение");
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
            int volume;
            try
            {
                volume = Convert.ToInt32(TextBoxVolumeFast.Text);
            }
            catch (Exception)
            {
                MessageBox.Show("В графе объём не правильное значение");
                return;
            }

            decimal price;

            try
            {
                price = Convert.ToDecimal(TextBoxPrice.Text);
            }
            catch (Exception)
            {
                MessageBox.Show("В графе цена не правильное значение");
                  return;
            }
            
            if (price == 0)
            {
                MessageBox.Show("В графе цена не правильное значение");
                return;
            }

            _strategyKeeper.BotBuyLimit(volume,price);
        }

        private void ButtonSellLimit_Click(object sender, RoutedEventArgs e)
        {
            int volume;
            try
            {
                volume = Convert.ToInt32(TextBoxVolumeFast.Text);
            }
            catch (Exception)
            {
                MessageBox.Show("В графе объём не правильное значение");
                return;
            }

            decimal price;

            try
            {
                price = Convert.ToDecimal(TextBoxPrice.Text);
            }
            catch (Exception)
            {
                MessageBox.Show("В графе цена не правильное значение");
                return;
            }

            if (price == 0)
            {
                MessageBox.Show("В графе цена не правильное значение");
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
            ServerMaster.ShowDialog();
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

    }
}
