/*
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
    /// <summary>
    /// Логика взаимодействия для TesterUiLight.xaml
    /// </summary>
    public partial class TesterUiLight : Window
    {
        public TesterUiLight()
        {
            InitializeComponent();
            ServerMaster.SetHostTable(HostPositionOnBoard, HostOrdersOnBoard);
            ServerMaster.CreateServer(ServerType.Tester, false);
            ServerMaster.GetServers();

            _strategyKeeper = new OsTraderMaster(null,
                null, null, null, null, HostAllPosition,
                null, HostBotLogPrime, null, null, null, null, null,
                null, StartProgram.IsTester);
            LabelOsa.Content = "V_" + System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;

            Closing += TesterUi_Closing;
            Local();

            BotTabsPainter painter = new BotTabsPainter(_strategyKeeper,BotsHost);

            TabControlPrime.SelectionChanged += TabControlPrime_SelectionChanged;
            TabControlPrime.MouseEnter += TabControlPrime_MouseEnter;
            TabControlPrime.MouseLeave += TabControlPrime_MouseLeave;

            this.Activate();
            this.Focus();
        }

        private void Local()
        {
            TabItemAllPos.Header = OsLocalization.Trader.Label20;
            TextBoxPositionBord.Header = OsLocalization.Trader.Label21;
            TextBoxPositionAllOrders.Header = OsLocalization.Trader.Label22;
            TabItemLogPrime.Header = OsLocalization.Trader.Label24;
            TabItemControl.Header = OsLocalization.Trader.Label37;
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
    }
}