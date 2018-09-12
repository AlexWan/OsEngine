/*
 *Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System;
using System.Windows;
using OsEngine.Charts.CandleChart;
using OsEngine.Entity;
using OsEngine.Market.Servers;

namespace OsEngine.OsTrader.Gui
{

    /// <summary>
    /// ГУИ робота
    /// </summary>
    public partial class RobotUi
    {
        public RobotUi()
        {
            InitializeComponent();
            ServerMaster.SetHostTable(HostPositionOnBoard, HostOrdersOnBoard);
            _strategyKeeper = new OsTraderMaster( ChartHostPanel, HostGlass, HostOpenPosition, HostClosePosition, HostAllPosition,
                                         HostBotLog, HostBotLogPrime, RectChart, HostAllert, TabControlBotsName,TabControlBotTab,TextBoxPrice,GridChartControlPanel);
            Closing += RobotUi_Closing;
           

            LocationChanged += RobotUi_LocationChanged;

            CheckBoxPaintOnOff.IsChecked = true;
            CheckBoxPaintOnOff.Click += CheckBoxPaintOnOff_Click;
            LabelOsa.Content = "V_" + System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;

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

        void CheckBoxPaintOnOff_Click(object sender, RoutedEventArgs e)
        {
            if (CheckBoxPaintOnOff.IsChecked.HasValue &&
                CheckBoxPaintOnOff.IsChecked.Value)
            {
                 _strategyKeeper.StartPaint();
            }
            else
            {
                _strategyKeeper.StopPaint();
            }
        }

        private void RobotUi_LocationChanged(object sender, EventArgs e)
        {
            WindowCoordinate.X = Convert.ToDecimal(Left);
            WindowCoordinate.Y = Convert.ToDecimal(Top);
        }

        void RobotUi_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            AcceptDialogUi ui = new AcceptDialogUi("Вы собираетесь закрыть программу. Вы уверены?");
            ui.ShowDialog();

            if (ui.UserAcceptActioin == false)
            {
                e.Cancel = true;
                return;
            }

            ServerMaster.AbortAll();
        }

        private OsTraderMaster _strategyKeeper;

// главное меню 

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

// управление отдельным ботом
        private void buttonStrategManualSettings_Click(object sender, RoutedEventArgs e)
        {
            _strategyKeeper.BotManualSettingsDialog();
        }

// привод

        private void buttonBuyFast_Click_1(object sender, RoutedEventArgs e)
        {
            decimal volume; 
            try
            {
                volume = Convert.ToDecimal(TextBoxVolumeFast.Text.Replace(".", ","));
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
            decimal volume;
            try
            {
                volume = Convert.ToDecimal(TextBoxVolumeFast.Text.Replace(".", ","));
            }
            catch (Exception)
            {
                MessageBox.Show("В графе объём неправильное значение");
                return;
            }
            _strategyKeeper.BotSellMarket(volume);
        }


// ручное управление позицией

        private void ButtonStrategIndividualSettings_Click(object sender, RoutedEventArgs e)
        {
            _strategyKeeper.BotIndividualSettings();
        }

        private void ButtonBuyLimit_Click(object sender, RoutedEventArgs e)
        {
            decimal volume;
            try
            {
                volume = Decimal.Parse(TextBoxVolumeFast.Text.Replace(".",","));
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

            _strategyKeeper.BotBuyLimit(volume, price);
        }

        private void ButtonSellLimit_Click(object sender, RoutedEventArgs e)
        {
            decimal volume;
            try
            {
                volume = Convert.ToDecimal(TextBoxVolumeFast.Text.Replace(".", ","));
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
