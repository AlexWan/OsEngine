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
            UpdateServerCombobox();
            ServerMaster.ServerCreateEvent += ServerMaster_ServerCreateEvent;
        }

        private void ServerMaster_ServerCreateEvent(IServer obj)
        {
            UpdateServerCombobox();
        }

        private void UpdateServerCombobox()
        {
            if (ComboBoxServer.Dispatcher.CheckAccess() == false )
            {
                ComboBoxServer.Dispatcher.Invoke(UpdateServerCombobox);
                return;
            }
            ComboBoxServer.Items.Clear();

            List<IServer> allServers = ServerMaster.GetServers();
            for (int i = 0; allServers !=null && i < allServers.Count; i++)
            {
                ComboBoxServer.Items.Add(allServers[i].ServerType.ToString());
            }
            UpdateBoxes();
        }

        private void UpdateBoxes()
        {
            ComboBoxPortfolio.Items.Clear();
            ComboBoxSecurites.Items.Clear();

            if (_bot.Securities != null)
            {
                for (int i = 0; i < _bot.Securities.Count; i++)
                {
                    ComboBoxSecurites.Items.Add(_bot.Securities[i].Name);
                }
            }
            for (int i = 0; _bot.Portfolios != null && i < _bot.Portfolios.Count; i++)
            {
                ComboBoxPortfolio.Items.Add(_bot.Portfolios[i].Number);
            }
        }

        private void ButtonBay_Click(object sender, RoutedEventArgs e)
        {
            OpenOrder(Side.Buy);
        }

        private void ButtonSell_Click(object sender, RoutedEventArgs e)
        {
            OpenOrder(Side.Sell);
        }

        private void OpenOrder(Side orderSide)
        {
            if (ComboBoxSecurites.SelectedItem == null ||
                ComboBoxSecurites.SelectedItem.ToString() == "" ||
                ComboBoxPortfolio.SelectedItem == null ||
                ComboBoxPortfolio.SelectedItem.ToString() == "" ||
                ComboBoxServer.SelectedItem == null ||
                ComboBoxServer.SelectedItem.ToString() == "")
            {
                MessageBox.Show("ошибка чтения входных данных");
                return;
            }

            string security = ComboBoxSecurites.SelectedItem.ToString();
            string portfolio = ComboBoxPortfolio.SelectedItem.ToString();
            ServerType serverType;
            Enum.TryParse(ComboBoxServer.SelectedItem.ToString(), out serverType);

            decimal price;
            decimal volume;
            try
            {
                price = Convert.ToDecimal(TextBoxPrice.Text);
                volume = Convert.ToDecimal(TextBoxVolum.Text);
            }
            catch
            {
                MessageBox.Show("ошибка чтения входных данных");
                return;
            }

            _bot.SendOrder(serverType, security, portfolio, price, volume, orderSide);
        }
    }
}
