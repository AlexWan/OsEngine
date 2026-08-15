using OsEngine.Language;
using OsEngine.Market;
using System;
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

            Closed += RobotsUiLightUnblock_Closed;
        }

        private void RobotsUiLightUnblock_Closed(object sender, EventArgs e)
        {
            try
            {
                ButtonAccept.Click -= ButtonAccept_Click;
                Closed -= RobotsUiLightUnblock_Closed;
            }
            catch (Exception ex)
            {
                ServerMaster.SendNewLogMessage(ex.ToString(), Logging.LogMessageType.Error);
            }
        }

        public bool IsUnBlocked;

        private void ButtonAccept_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string password = TextBoxPassword.Text;

                if (BlockMaster.CheckPassword(password))
                {
                    IsUnBlocked = true;
                    BlockMaster.IsBlocked = false;
                    Close();
                }
                else
                {
                    ServerMaster.SendNewLogMessage("Error password. ", Logging.LogMessageType.Error);
                    Close();
                }
            }
            catch (Exception error)
            {
                ServerMaster.SendNewLogMessage(error.ToString(), Logging.LogMessageType.Error);
            }
        }
    }
}