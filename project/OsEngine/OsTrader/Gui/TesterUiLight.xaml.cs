/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System.Windows;
using OsEngine.Entity;
using OsEngine.Language;
using OsEngine.Market;
using OsEngine.Layout;

namespace OsEngine.OsTrader.Gui
{
    /// <summary>
    /// Логика взаимодействия для TesterUiLight.xaml
    /// </summary>
    public partial class TesterUiLight : Window
    {
        public TesterUiLight()
        {
            InitializeComponent();
            OsEngine.Layout.StickyBorders.Listen(this);

            ServerMaster.SetHostTable(HostPositionOnBoard, 
                HostActiveOrders, HostHistoricalOrders, StartUiToPainter.IsTesterLight,
                ComboBoxQuantityPerPageActive, BackButtonActiveList, NextButtonActiveList, ComboBoxQuantityPerPageHistorical,
                BackButtonHistoricalList, NextButtonHistoricalList);

            ServerMaster.CreateServer(ServerType.Tester, false);
            ServerMaster.GetServers();

            _strategyKeeper = new OsTraderMaster(null,
                null, null, null, null,
                null, HostBotLogPrime, null, null, null, null, null,
                null, StartProgram.IsTester, null, null);

            _strategyKeeper.CreateGlobalPositionController(HostActivePoses, HostHistoricalPoses);
            _strategyKeeper.CreateBuyAtStopPosViewer(HostStopLimitPoses);

            LabelOsa.Content = "V_" + System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;

            Closing += TesterUi_Closing;
            Local();

            BotTabsPainter painter = new BotTabsPainter(_strategyKeeper, BotsHost);

            TabControlPrime.SelectionChanged += TabControlPrime_SelectionChanged;
            TabControlPrime.MouseEnter += TabControlPrime_MouseEnter;
            TabControlPrime.MouseLeave += TabControlPrime_MouseLeave;

            HistoricalListPanel.Visibility = Visibility.Collapsed;
            ActiveListPanel.Visibility = Visibility.Collapsed;

            this.Activate();
            this.Focus();
            GlobalGUILayout.Listen(this, "testerUiLight");

            rectToMove.MouseEnter += GreedChartPanel_MouseEnter;
            rectToMove.MouseLeave += GreedChartPanel_MouseLeave;
            rectToMove.MouseDown += GreedChartPanel_MouseDown;

            Instance = this;
            _ordersPainter = ServerMaster._ordersStorage;
        }

        public static TesterUiLight Instance;

        private ServerMasterOrdersPainter _ordersPainter;

        private void Local()
        {
            TabItemAllPos.Header = OsLocalization.Trader.Label20;
            TabPortfolios.Header = OsLocalization.Trader.Label21;
            TabOrders.Header = OsLocalization.Trader.Label22;
            TabItemLogPrime.Header = OsLocalization.Trader.Label24;
            TabItemControl.Header = OsLocalization.Trader.Label37;
            TabActivePos.Header =  OsLocalization.Trader.Label187;
            TabHistoricalPos.Header =  OsLocalization.Trader.Label188;
            TabActiveOrders.Header = OsLocalization.Trader.Label189;
            TabHistoricalOrders.Header = OsLocalization.Trader.Label190;
            TabStopLimitPoses.Header = OsLocalization.Trader.Label193;
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
            }
        }

        private OsTraderMaster _strategyKeeper;

        private void TabControlPrime_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            TabControlPrime.SelectionChanged -= TabControlPrime_SelectionChanged;

            int index = TabControlPrime.SelectedIndex;

            if (index == 4 && _mouseOnTabControl)
            {
                TabControlPrime.SelectedIndex = 0;
                TabControlPrime.SelectedItem = TabControlPrime.Items[0];
                ServerMaster.ShowDialog(true);
            }
            else if (index == 4 && _mouseOnTabControl == false)
            {
                TabControlPrime.SelectedIndex = 0;
                TabControlPrime.SelectedItem = TabControlPrime.Items[0];
            }

            TabControlPrime.SelectionChanged += TabControlPrime_SelectionChanged;
        }

        bool _mouseOnTabControl = false;

        private void TabControlPrime_MouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
        {
            _mouseOnTabControl = false;
        }

        private void TabControlPrime_MouseEnter(object sender, System.Windows.Input.MouseEventArgs e)
        {
            _mouseOnTabControl = true;
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
            if(GridPrime.RowDefinitions[1].Height.Value == 190)
            {
                GreedChartPanel.Cursor = System.Windows.Input.Cursors.ScrollN;
            }
            if (GridPrime.RowDefinitions[1].Height.Value == 500)
            {
                GreedChartPanel.Cursor = System.Windows.Input.Cursors.ScrollS;
            }
        }
    }
}