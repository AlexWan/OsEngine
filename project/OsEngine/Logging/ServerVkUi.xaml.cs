/*
 *Your rights to use the code are governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 *Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System;
using System.Windows;
using OsEngine.Language;
using OsEngine.Market;

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
            TextBoxUserId.Text = string.Join(", ", serverVk.UserIds);
            CheckBoxProcessingCommand.IsChecked = serverVk.ProcessingCommand;

            Title = OsLocalization.Logging.Label32;
            ButtonAccept.Content = OsLocalization.Logging.Button1;
            ButtonDisconnect.Content = OsLocalization.Logging.Button5;
            Label33.Content = OsLocalization.Logging.Label33;
            Label34.Content = OsLocalization.Logging.Label34;
            Label35.Content = OsLocalization.Logging.Label35;
            Label35.Visibility = Visibility.Collapsed;

            this.Activate();
            this.Focus();
        }

        private void buttonDisconnect_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                ServerVk.GetServer().Disconnect();
                Close();
            }
            catch (Exception error)
            {
                ServerMaster.SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        private void buttonAccept_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                ServerVk serverVk = ServerVk.GetServer();
                serverVk.AccessToken = TextBoxAccessToken.Text;
                serverVk.UserIds = serverVk.ParseAndResolveUserIds(TextBoxUserId.Text, out System.Collections.Generic.List<string> unresolvedNames);

                if (unresolvedNames.Count > 0)
                {
                    Label35.Content = $"Screen names not found: {string.Join(", ", unresolvedNames)}";
                    Label35.Visibility = Visibility.Visible;
                    return;
                }

                if (serverVk.UserIds.Count == 0)
                {
                    Label35.Content = OsLocalization.Logging.Label35;
                    Label35.Visibility = Visibility.Visible;
                    return;
                }

                if (serverVk.ValidateTokenAndUserIds(serverVk.UserIds, out string validationError) == false)
                {
                    Label35.Content = validationError;
                    Label35.Visibility = Visibility.Visible;
                    return;
                }

                serverVk.ProcessingCommand = CheckBoxProcessingCommand.IsChecked == true;
                serverVk.Save();
                Close();
            }
            catch (Exception error)
            {
                ServerMaster.SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }
    }
}