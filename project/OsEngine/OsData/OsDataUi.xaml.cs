/*
 *Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using OsEngine.Market.Servers;

namespace OsEngine.OsData
{
    /// <summary>
    /// Логика взаимодействия для OsDataUi.xaml
    /// </summary>
    public partial class OsDataUi
    {
        public OsDataUi()
        {
            InitializeComponent();
            ServerMaster.IsOsData = true;
            new OsDataMaster(ChartHostPanel, HostLog, HostSource, HostSet, ComboBoxSecurity,ComboBoxTimeFrame,RectChart);
            LabelOsa.Content = "V_" + System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
        }

    }
}
