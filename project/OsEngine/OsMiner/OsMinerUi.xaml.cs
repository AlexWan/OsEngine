
namespace OsEngine.OsMiner
{
    /// <summary>
    /// Логика взаимодействия для OsMinerUi.xaml
    /// </summary>
    public partial class OsMinerUi
    {
        public OsMinerUi()
        {
            InitializeComponent();
            _miner = new OsMinerMaster(HostLog, HostSets, HostPatternSets, HostChart,RectChart);
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
