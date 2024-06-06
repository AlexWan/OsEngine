/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System.Windows;
using OsEngine.Entity;
using OsEngine.Language;
using OsEngine.Market;
using OsEngine.Layout;
using OsEngine.Market.SupportTable;

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
            ServerMaster.SetHostTable(HostPortfolios, HostActiveOrders, HostHistoricalOrders);
            ServerMaster.GetServers();

            _strategyKeeper = new OsTraderMaster(null,
                null, null, null, null,
                null, HostBotLogPrime, null, null, null, null, null,
                null, StartProgram.IsOsTrader);

            _strategyKeeper.CreateGlobalPositionController(HostActivePoses, HostHistoricalPoses);
            _strategyKeeper.CreateBuyAtStopPosViewer(HostStopLimitPoses);

            LabelOsa.Content = "V_" + System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;

            Closing += TesterUi_Closing;
            Local();

            _painter = new BotTabsPainter(_strategyKeeper, BotsHost);

            _painterServer = new ServerMasterSourcesPainter(HostServers, HostServerLog, CheckBoxServerAutoOpen);

            this.Activate();
            this.Focus();

            GlobalGUILayout.Listen(this, "botStationLightUi");

            IsRobotUiLightStart = true;

            rectToMove.MouseEnter += GreedChartPanel_MouseEnter;
            rectToMove.MouseLeave += GreedChartPanel_MouseLeave;
            rectToMove.MouseDown += GreedChartPanel_MouseDown;
        }

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
        }

        void TesterUi_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            AcceptDialogUi ui = new AcceptDialogUi(OsLocalization.Trader.Label48);
            ui.ShowDialog();

            if (ui.UserAcceptActioin == false)
            {
                e.Cancel = true;
                return;
            }

            _painterServer.Dispose();
            _painter = null;
        }

        private OsTraderMaster _strategyKeeper;

        public static bool IsRobotUiLightStart = false;

        // смещение областей

        private void GreedChartPanel_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (GreedChartPanel.Cursor == System.Windows.Input.Cursors.ScrollN)
            {
                GridPrime.RowDefinitions[1].Height = new GridLength(500, GridUnitType.Pixel);
            }
            else if (GreedChartPanel.Cursor == System.Windows.Input.Cursors.ScrollS)
            {
                GridPrime.RowDefinitions[1].Height = new GridLength(190, GridUnitType.Pixel);
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
    }
}