/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using OsEngine.Entity;
using OsEngine.Language;
using OsEngine.Layout;
using OsEngine.Market;
using OsEngine.Market.SupportTable;
using OsEngine.OsTrader.Gui.BlockInterface;
using OsEngine.OsTrader.ServerAvailability;
using OsEngine.OsTrader.SystemAnalyze;
using System;
using System.Windows;

namespace OsEngine.OsTrader.Gui
{
    /// <summary>
    /// Логика взаимодействия для RobotUiLight.xaml
    /// </summary>
    public partial class RobotUiLight : Window
    {
        public RobotUiLight()
        {
            InitializeComponent();
            OsEngine.Layout.StickyBorders.Listen(this);
            ServerMaster.SetHostTable(HostPortfolios, HostActiveOrders, HostHistoricalOrders, StartUiToPainter.IsOsTraderLight, 
                ComboBoxQuantityPerPageActive, BackButtonActiveList, NextButtonActiveList, ComboBoxQuantityPerPageHistorical, 
                BackButtonHistoricalList, NextButtonHistoricalList);

            ServerMaster.GetServers();

            _strategyKeeper = new OsTraderMaster(null,
                null, null, null, null,
                null, HostBotLogPrime, null, null, null, null, null,
                null, StartProgram.IsOsTrader, null, null);

            _strategyKeeper.CreateGlobalPositionController(HostActivePoses, HostHistoricalPoses);
            _strategyKeeper.CreateBuyAtStopPosViewer(HostStopLimitPoses);

            LabelOsa.Content = "V_" + System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;

            Closing += TesterUi_Closing;
            Local();

            _painter = new BotTabsPainter(_strategyKeeper, BotsHost);

            _painterServer = new ServerMasterSourcesPainter(
                HostServers, 
                HostServerLog, 
                CheckBoxServerAutoOpen,
                TextBoxSearchSource,
                ButtonRightInSearchResults,
                ButtonLeftInSearchResults,
                LabelCurrentResultShow,
                LabelCommasResultShow,
                LabelCountResultsShow);

            this.Activate();
            this.Focus();

            GlobalGUILayout.Listen(this, "botStationLightUi");

            IsRobotUiLightStart = true;

            rectToMove.MouseEnter += GreedChartPanel_MouseEnter;
            rectToMove.MouseLeave += GreedChartPanel_MouseLeave;
            rectToMove.MouseDown += GreedChartPanel_MouseDown;

            ImagePadlock.MouseEnter += ImagePadlock_MouseEnter;
            ImagePadlock.MouseLeave += ImagePadlock_MouseLeave;
            ImagePadlock.MouseDown += ImagePadlock_MouseDown;

            ImagePadlockOpen.MouseEnter += ImagePadlockOpen_MouseEnter;
            ImagePadlockOpen.MouseLeave += ImagePadlockOpen_MouseLeave;
            ImagePadlockOpen.MouseDown += ImagePadlockOpen_MouseDown;

            HistoricalListPanel.Visibility = Visibility.Collapsed;
            ActiveListPanel.Visibility = Visibility.Collapsed;

            _ordersPainter = ServerMaster._ordersStorage;

            HostActiveOrders.Margin = new Thickness(0, 0, 0, 0);
            HostHistoricalOrders.Margin = new Thickness(0, 0, 0, 0);

            UnBlockInterface();
            this.Closing += RobotsUiLightUnblock_Closing;

            Instance = this;            
        }

        public static RobotUiLight Instance;

        private ServerMasterOrdersPainter _ordersPainter;

        ServerMasterSourcesPainter _painterServer;

        BotTabsPainter _painter;

        private void Local()
        {
            Title = Title + " " + OsEngine.PrimeSettings.PrimeSettingsMaster.LabelInHeaderBotStation;
            TabItemAllPos.Header = OsLocalization.Trader.Label20 ;
            TabPortfolios.Header = OsLocalization.Trader.Label21;
            TabAllOrders.Header = OsLocalization.Trader.Label22;
            TabItemLogPrime.Header = OsLocalization.Trader.Label24;
            TabItemControl.Header = OsLocalization.Trader.Label37;
            CheckBoxServerAutoOpen.Content = OsLocalization.Market.Label20;
            TabActivePos.Header = OsLocalization.Trader.Label187;
            TabHistoricalPos.Header = OsLocalization.Trader.Label188;
            TabActiveOrders.Header = OsLocalization.Trader.Label189;
            TabHistoricalOrders.Header = OsLocalization.Trader.Label190;
            TabStopLimitPoses.Header = OsLocalization.Trader.Label193;
            ButtonSupportTable.Content = OsLocalization.Market.Label81;
            ButtonProxy.Content = OsLocalization.Market.Label172;
            ButtonSystemStress.Content = OsLocalization.Trader.Label560;
            ButtonServerAvailability.Content = OsLocalization.Trader.Label605;
            LabelPageActive.Content = OsLocalization.Trader.Label576;
            LabelFromActive.Content = OsLocalization.Trader.Label577;
            LabelCountActive.Content = OsLocalization.Trader.Label578;
            LabelPageHistorical.Content = OsLocalization.Trader.Label576;
            LabelFromHistorical.Content = OsLocalization.Trader.Label577;
            LabelCountHistorical.Content = OsLocalization.Trader.Label578;
        }

        void TesterUi_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            AcceptDialogUi ui = new AcceptDialogUi(OsLocalization.Trader.Label48);
            ui.ShowDialog();

            if (ui.UserAcceptAction == false)
            {
                e.Cancel = true;
                return;
            }

            _painterServer.Dispose();
            _painter = null;
        }

        private OsTraderMaster _strategyKeeper;

        public static bool IsRobotUiLightStart = false;

        private void ButtonProxy_Click(object sender, RoutedEventArgs e)
        {
            ServerMaster.ShowProxyDialog();
        }

        private void ButtonSystemStress_Click(object sender, RoutedEventArgs e)
        {
            SystemUsageAnalyzeMaster.ShowDialog();
        }

        private void ButtonServerAvailability_Click(object sender, RoutedEventArgs e)
        {
            ServerAvailabilityMaster.ShowDialog();
        }

        // смещение областей

        private void GreedChartPanel_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (GreedChartPanel.Cursor == System.Windows.Input.Cursors.ScrollN)
            {
                GridPrime.RowDefinitions[1].Height = new GridLength(500, GridUnitType.Pixel);

                HistoricalListPanel.Visibility = Visibility.Visible;
                ActiveListPanel.Visibility = Visibility.Visible;

                HostActiveOrders.Margin = new Thickness(0, 28, 0, 0);
                HostHistoricalOrders.Margin = new Thickness(0, 28, 0, 0);
            }
            else if (GreedChartPanel.Cursor == System.Windows.Input.Cursors.ScrollS)
            {
                GridPrime.RowDefinitions[1].Height = new GridLength(190, GridUnitType.Pixel);

                HistoricalListPanel.Visibility = Visibility.Collapsed;
                ActiveListPanel.Visibility = Visibility.Collapsed;

                HostActiveOrders.Margin = new Thickness(0, 0, 0, 0);
                HostHistoricalOrders.Margin = new Thickness(0, 0, 0, 0);
            }
        }

        private void GreedChartPanel_MouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
        {
            GreedChartPanel.Cursor = System.Windows.Input.Cursors.Arrow;
        }

        private void GreedChartPanel_MouseEnter(object sender, System.Windows.Input.MouseEventArgs e)
        {
            if (GridPrime.RowDefinitions[1].Height.Value == 190)
            {
                GreedChartPanel.Cursor = System.Windows.Input.Cursors.ScrollN;
            }
            if (GridPrime.RowDefinitions[1].Height.Value == 500)
            {
                GreedChartPanel.Cursor = System.Windows.Input.Cursors.ScrollS;
            }
        }

        private void ButtonSupportTable_Click(object sender, RoutedEventArgs e)
        {
            SupportTableUi supportTableUi = new SupportTableUi();
            supportTableUi.ShowDialog();
        }

        // Block interface

        private void ImagePadlock_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            RobotUiLightBlock ui = new RobotUiLightBlock();
            ui.ShowDialog();

            if(ui.InterfaceIsBlock == true)
            {
                BlockInterface();

            }
        }

        private void ImagePadlock_MouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
        {
            ImagePadlock.Cursor = System.Windows.Input.Cursors.Arrow;
        }

        private void ImagePadlock_MouseEnter(object sender, System.Windows.Input.MouseEventArgs e)
        {
            ImagePadlock.Cursor = System.Windows.Input.Cursors.Hand;
        }

        private void BlockInterface()
        {
            GreedChartPanel.IsEnabled = false;
            TabControlPrime.IsEnabled = false;
            ImagePadlock.Visibility = Visibility.Hidden;
            ImagePadlockOpen.Visibility = Visibility.Visible;
        }

        // UnBlock interface

        private void ImagePadlockOpen_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            RobotsUiLightUnblock ui = new RobotsUiLightUnblock();

            ui.ShowDialog();

            if (ui.IsUnBlocked == true)
            {
                UnBlockInterface();
            }
        }

        private void ImagePadlockOpen_MouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
        {
            ImagePadlockOpen.Cursor = System.Windows.Input.Cursors.Arrow;
        }

        private void ImagePadlockOpen_MouseEnter(object sender, System.Windows.Input.MouseEventArgs e)
        {
            ImagePadlockOpen.Cursor = System.Windows.Input.Cursors.Hand;
        }

        private void UnBlockInterface()
        {
            GreedChartPanel.IsEnabled = true;
            TabControlPrime.IsEnabled = true;
            ImagePadlock.Visibility = Visibility.Visible;
            ImagePadlockOpen.Visibility = Visibility.Hidden;
        }

        private void RobotsUiLightUnblock_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (BlockMaster.IsBlocked == true)
            {
                ServerMaster.SendNewLogMessage("User block interface. Unblock it. ", Logging.LogMessageType.Error);
                e.Cancel = true;
            }
        }

    }
}