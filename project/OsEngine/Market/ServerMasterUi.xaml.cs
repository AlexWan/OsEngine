/*
 *Your rights to use the code are governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 *Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System;
using System.Collections.Generic;
using System.ComponentModel;
using OsEngine.Language;
using OsEngine.Logging;
using OsEngine.Market.Servers;
using OsEngine.Market.SupportTable;

namespace OsEngine.Market
{
    /// <summary>
    /// interaction logic for ServerPrimeUi.xaml
    /// Логика взаимодействия для ServerPrimeUi.xaml
    /// </summary>
    public partial class ServerMasterUi
    {
        /// <summary>
        /// constructor
        /// конструктор
        /// </summary>
        /// <param name="isTester">shows whether the method is called from the tester / вызывается ли метод из тестера </param>
        public ServerMasterUi(bool isTester)
        {
            InitializeComponent();
            OsEngine.Layout.StickyBorders.Listen(this);
            OsEngine.Layout.StartupLocation.Start_MouseInCentre(this);

            List<IServer> servers = ServerMaster.GetServers();

            if (isTester)
            {
                servers = ServerMaster.GetServers();

                if (servers == null ||
                    servers.Find(s => s.ServerType == ServerType.Tester) == null)
                {
                    ServerMaster.CreateServer(ServerType.Tester, false);
                }

                Close();

                servers = ServerMaster.GetServers();
                servers.Find(s => s.ServerType == ServerType.Tester).ShowDialog();
                return;
            }

            Title = OsLocalization.Market.TitleServerMasterUi;
            TabItem1.Header = OsLocalization.Market.TabItem1;
            TabItem2.Header = OsLocalization.Market.TabItem2;
            CheckBoxServerAutoOpen.Content = OsLocalization.Market.Label20;
            ButtonSupportTable.Content = OsLocalization.Market.Label81;

            _painter = new ServerMasterSourcesPainter(
                HostSource,
                HostLog,
                CheckBoxServerAutoOpen,
                TextBoxSearchSource,
                ButtonRightInSearchResults,
                ButtonLeftInSearchResults,
                LabelCurrentResultShow,
                LabelCommasResultShow,
                LabelCountResultsShow);

            Closing += ServerMasterUi_Closing;

            this.Activate();
            this.Focus();
        }

        private ServerMasterSourcesPainter _painter;

        private void ServerMasterUi_Closing(object sender, CancelEventArgs e)
        {
            try
            {
                if (_painter != null)
                {
                    _painter.Dispose();
                    _painter = null;
                }

                ButtonSupportTable.Click -= ButtonSupportTable_Click;
                Closing -= ServerMasterUi_Closing;
            }
            catch (Exception ex)
            {
                ServerMaster.SendNewLogMessage(ex.ToString(), Logging.LogMessageType.Error);
            }
        }

        private void ButtonSupportTable_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            SupportTableUi supportTableUi = new SupportTableUi();
            supportTableUi.ShowDialog();
        }
    }
}
