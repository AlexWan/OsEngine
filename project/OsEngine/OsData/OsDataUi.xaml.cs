/*
 *Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

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

            new OsDataMaster(ChartHostPanel, HostLog, HostSource, HostSet, ComboBoxSecurity,ComboBoxTimeFrame,RectChart);
            LabelOsa.Content = "V_" + System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
        }

    }
}
