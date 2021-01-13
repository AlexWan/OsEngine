using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using OsEngine.OsTrader.Panels;
using OsEngine.Entity;
using OsEngine.OsTrader.Panels.Tab;
using OsEngine.Charts.CandleChart.Indicators;
using System.Threading;
using OsEngine.Market;
using OsEngine.Market.Servers;


namespace OsEngine.Robots.MoiRoboti
{
    /// <summary>
    /// Логика взаимодействия для HftoneUi.xaml
    /// </summary>
    public partial class HftoneUi : Window
    {
        Hftone _bot;
        public HftoneUi(Hftone bot)
        {
            InitializeComponent();
            _bot = bot;
            UpdateBoxes();
            ServerMaster.ServerCreateEvent += ServerMaster_ServerCreateEvent;
        }

        private void ServerMaster_ServerCreateEvent(IServer obj)
        {
            UpdateServerCombobox();
        }

        private void UpdateServerCombobox()
        {
            CoboBoxServer.Items.Clear();

            List<IServer> allServers = ServerMaster.GetServers();
            for (int i = 0; allServers !=null && i < allServers.Count; i++)
            {
                CoboBoxServer.Items.Add(allServers[i].ServerType.ToString());
            }
        }

        private void UpdateBoxes()
        {
            CoboBoxPortfolio.Items.Clear();
            CoboBoxSecurites.Items.Clear();

            if (_bot.Securities != null)
            {
                for (int i = 0; i < _bot.Securities.Count; i++)
                {
                    CoboBoxSecurites.Items.Add(_bot.Securities[i].Name);
                }
            }
            for (int i = 0; _bot.Portfolios != null && i < _bot.Portfolios.Count; i++)
            {
                CoboBoxPortfolio.Items.Add(_bot.Portfolios[i].Number);
            }
        }

        private void ButtonBay_Click(object sender, RoutedEventArgs e)
        {

        }

        private void ButtonSell_Click(object sender, RoutedEventArgs e)
        {

        }
    }
}
