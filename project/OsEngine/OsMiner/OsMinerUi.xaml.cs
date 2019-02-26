
using OsEngine.Language;

namespace OsEngine.OsMiner
{
    /// <summary>
    /// Interaction Logic for OsMinerUi.xaml
    /// Логика взаимодействия для OsMinerUi.xaml
    /// </summary>
    public partial class OsMinerUi
    {
        public OsMinerUi()
        {
            InitializeComponent();
            _miner = new OsMinerMaster(HostLog, HostSets, HostPatternSets, HostChart,RectChart);

            Label5.Header = OsLocalization.Miner.Label5;
            Label6.Header = OsLocalization.Miner.Label6;
            Label52.Content = OsLocalization.Miner.Label5;
            Label7.Content = OsLocalization.Miner.Label7;
        }

        private OsMinerMaster _miner;

        private void ButtonGoLeft_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            _miner.GoLeft();
        }

        private void ButtonGoRight_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            _miner.GoRight();
        }
    }
}
