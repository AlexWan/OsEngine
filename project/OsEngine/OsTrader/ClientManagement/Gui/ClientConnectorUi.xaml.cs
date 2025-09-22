/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using OsEngine.Market;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Collections.Generic;

namespace OsEngine.OsTrader.ClientManagement.Gui
{
    /// <summary>
    /// Interaction logic for ClientConnector.xaml
    /// </summary>
    public partial class ClientConnectorUi : Window
    {
        public int ClientNumber;

        public ClientConnectorUi(TradeClientConnector connectorSettings)
        {
            InitializeComponent();

            ClientNumber = connectorSettings.Number;

            List<string> serverTypes = ServerMaster.ServersTypesStringSorted;

            ComboBoxServerType.Items.Add(ServerType.None.ToString());

            for (int i = 0;i < serverTypes.Count;i++)
            {
                ComboBoxServerType.Items.Add(serverTypes[i].ToString());
            }

            ComboBoxServerType.SelectedItem = connectorSettings.ServerType.ToString();
            ComboBoxServerType.SelectionChanged += ComboBoxServerType_SelectionChanged;

        }

        private void ComboBoxServerType_SelectionChanged(object sender, SelectionChangedEventArgs e)
        { 
            try
            {





            }
            catch
            {
                // ignore
            }
        }
    }
}
