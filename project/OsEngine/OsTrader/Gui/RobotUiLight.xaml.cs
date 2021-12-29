/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System.Windows;
using System.ComponentModel;
using OsEngine.Entity;
using OsEngine.Language;
using OsEngine.Market;

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
            ServerMaster.SetHostTable(HostPositionOnBoard, HostOrdersOnBoard);
            ServerMaster.GetServers();

            _strategyKeeper = new OsTraderMaster(null,
                null, null, null, null, HostAllPosition,
                null, HostBotLogPrime, null, null, null, null, null,
                null, StartProgram.IsOsTrader);
            LabelOsa.Content = "V_" + System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;

            Closing += TesterUi_Closing;
            Local();

            BotTabsPainter painter = new BotTabsPainter(_strategyKeeper, BotsHost);

            ServerMasterPainter painterServer = new ServerMasterPainter(HostServers, HostServerLog, CheckBoxServerAutoOpen);

            Closing += delegate (object sender, CancelEventArgs args)
            {
                painterServer.Dispose();
                painter = null;
            };
        }

        private void Local()
        {
            TabItemAllPos.Header = OsLocalization.Trader.Label20;
            TextBoxPositionBord.Header = OsLocalization.Trader.Label21;
            TextBoxPositionAllOrders.Header = OsLocalization.Trader.Label22;
            TabItemLogPrime.Header = OsLocalization.Trader.Label24;
            TabItemControl.Header = OsLocalization.Trader.Label37;
            CheckBoxServerAutoOpen.Content = OsLocalization.Market.Label20;
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

    }
}