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
    /// <summary>
    /// settings window of telegram server
    /// Окно настроек сервера телеграм
    /// </summary>
    public partial class ServerTelegramDeliveryUi
    {
        public ServerTelegramDeliveryUi() // constructor / конструктор
        {
            InitializeComponent();
            OsEngine.Layout.StickyBorders.Listen(this);
            OsEngine.Layout.StartupLocation.Start_MouseInCentre(this);

            ServerTelegram serverTelegram = ServerTelegram.GetServer();

            TextBoxMyBotToken.Text = serverTelegram.BotToken;
            TextBoxChatId.Text = serverTelegram.ChatId.ToString();
            TextBoxProxy.Text = serverTelegram.Proxy;
            CheckBoxTelegramProcessingCommand.IsChecked = serverTelegram.ProcessingCommand;

            ComboBoxProxyType.Items.Add("None");
            ComboBoxProxyType.Items.Add("Auto");
            ComboBoxProxyType.Items.Add("Manual");

            if (string.IsNullOrEmpty(serverTelegram.ProxyType))
            {
                ComboBoxProxyType.SelectedItem = "None";
            }
            else
            {
                ComboBoxProxyType.SelectedItem = serverTelegram.ProxyType;
            }

            Title = OsLocalization.Logging.Label22;
            ButtonAccept.Content = OsLocalization.Logging.Button1;
            ButtonProxy.Content = OsLocalization.Market.Label172;
            Label23.Content = OsLocalization.Logging.Label23;
            Label24.Content = OsLocalization.Logging.Label24;
            Label25.Content = OsLocalization.Logging.Label25;
            LabelProxyType.Content = OsLocalization.Market.Label171;
            LabelProxy.Content = OsLocalization.Market.Label172;
            Label25.Visibility = Visibility.Collapsed;
            this.Activate();
            this.Focus();
        }

        private void buttonAccept_Click(object sender, RoutedEventArgs e) // accept / принять
        {
            try
            {
                ServerTelegram serverTelegram = ServerTelegram.GetServer();
                serverTelegram.BotToken = TextBoxMyBotToken.Text;

                if (long.TryParse(TextBoxChatId.Text, out serverTelegram.ChatId))
                {
                    serverTelegram.ProcessingCommand = CheckBoxTelegramProcessingCommand.IsChecked == true;

                    string proxyType = ComboBoxProxyType.SelectedItem == null ? "None" : ComboBoxProxyType.SelectedItem.ToString();
                    string proxy = TextBoxProxy.Text;

                    serverTelegram.ApplyProxySettings(proxyType, proxy);
                    serverTelegram.Save();

                    Close();
                }
                else
                {
                    Label25.Visibility = Visibility.Visible;
                }
            }
            catch (Exception error)
            {
                ServerMaster.SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        private void ButtonProxy_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                ServerMaster.ShowProxyDialog();
            }
            catch (Exception error)
            {
                ServerMaster.SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }
    }
}
