/*
 *Your rights to use the code are governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 *Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System.Windows;
using OsEngine.Language;

namespace OsEngine.Logging
{
    public partial class ServerVkUi
    {
        public ServerVkUi()
        {
            InitializeComponent();
            OsEngine.Layout.StickyBorders.Listen(this);
            OsEngine.Layout.StartupLocation.Start_MouseInCentre(this);

            ServerVk serverVk = ServerVk.GetServer();

            TextBoxAccessToken.Text = serverVk.AccessToken;
            TextBoxUserId.Text = serverVk.UserId.ToString();
            CheckBoxProcessingCommand.IsChecked = serverVk.ProcessingCommand;

            Title = OsLocalization.Logging.Label32;
            ButtonAccept.Content = OsLocalization.Logging.Button1;
            Label33.Content = OsLocalization.Logging.Label33;
            Label34.Content = OsLocalization.Logging.Label34;
            Label35.Content = OsLocalization.Logging.Label35;
            Label35.Visibility = Visibility.Collapsed;

            this.Activate();
            this.Focus();
        }

        private void buttonAccept_Click(object sender, RoutedEventArgs e)
        {
            ServerVk serverVk = ServerVk.GetServer();
            serverVk.AccessToken = TextBoxAccessToken.Text;

            if (long.TryParse(TextBoxUserId.Text, out serverVk.UserId))
            {
                serverVk.ProcessingCommand = CheckBoxProcessingCommand.IsChecked == true;
                serverVk.Save();
                Close();
            }
            else
            {
                Label35.Visibility = Visibility.Visible;
            }
        }
    }
}