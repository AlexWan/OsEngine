using OsEngine.Language;
using OsEngine.Market;
using System.Windows;


namespace OsEngine.OsTrader.Gui.BlockInterface
{
    /// <summary>
    /// Interaction logic for RobotsUiLightUnblock.xaml
    /// </summary>
    public partial class RobotsUiLightUnblock : Window
    {
        public RobotsUiLightUnblock()
        {
            InitializeComponent();

            OsEngine.Layout.StartupLocation.Start_MouseInCentre(this);

            LabelPassword.Content = OsLocalization.Trader.Label423;
            ButtonAccept.Content = OsLocalization.Trader.Label429;
            Title = OsLocalization.Trader.Label430;
        }

        public bool IsUnBlocked;

        private void ButtonAccept_Click(object sender, RoutedEventArgs e)
        {
            string password = TextBoxPassword.Text;

            string passwordInReal = BlockMaster.Password;

            if(passwordInReal == password)
            {
                IsUnBlocked = true;
                BlockMaster.IsBlocked = false;
                Close();
            }
            else
            {
                ServerMaster.SendNewLogMessage("Error password. ",Logging.LogMessageType.Error);
                Close();
            }
        }
    }
}