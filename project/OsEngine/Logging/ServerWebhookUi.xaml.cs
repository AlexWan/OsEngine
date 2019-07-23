/*
 *Your rights to use the code are governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 *Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System;
using System.Windows;
using OsEngine.Language;

namespace OsEngine.Logging
{
    /// <summary>
    /// window of webhook server settings
    /// Окно настроек сервера рассылки сообщений по вебхукам
    /// </summary>
    public partial class ServerWebhookDeliveryUi
    {
         public ServerWebhookDeliveryUi() // constructor / конструктор
        {
            InitializeComponent();

            ServerWebhook serverWebhook = ServerWebhook.GetServer();

            TextBoxSlackBotToken.Text = serverWebhook.SlackBotToken;
            CheckBoxSendScreenshots.IsChecked = serverWebhook.MustSendChartScreenshots;
            TextBoxWebhooks.Text = "";

            if (serverWebhook.Webhooks != null)
            {
                for (int i = 0; i < serverWebhook.Webhooks.Length; i++)
                {
                    TextBoxWebhooks.Text += serverWebhook.Webhooks[i] + "\r\n";
                }
            }

            Title = OsLocalization.Logging.TitleEmailServer;
            ButtonAccept.Content = OsLocalization.Logging.Button1;
            Label16.Content = OsLocalization.Logging.Label16;
            Label17.Content = OsLocalization.Logging.Label17;
            CheckBoxSendScreenshots.Content = OsLocalization.Logging.Label18;
        }

        private void buttonAccept_Click(object sender, RoutedEventArgs e) // accept / принять
        {
            ServerWebhook serverWebhook = ServerWebhook.GetServer();
            serverWebhook.SlackBotToken = TextBoxSlackBotToken.Text;
            serverWebhook.MustSendChartScreenshots = CheckBoxSendScreenshots.IsChecked.Value;

            string[] lockal = TextBoxWebhooks.Text.Split('\r');


            string shit = "";

            for (int i = 0; i < lockal.Length; i++)
            {
                shit += lockal[i];
            }
            lockal = shit.Split('\n');
            string[] lockal2 = null;
            for (int i = 0, ii = 0; i < lockal.Length; i++)
            {
                if (lockal[i] != "")
                {
                    if (lockal2 == null)
                    {
                        lockal2 = new string[1];
                        lockal2[0] = lockal[i];
                        ii++;
                    }
                    else
                    {
                        string[] newLock = new string[lockal2.Length + 1];
                        for (int iii = 0; iii < lockal2.Length; iii++)
                        {
                            newLock[iii] = lockal2[iii];
                        }
                        newLock[ii] = lockal[i];
                        lockal2 = newLock;
                        ii++;
                    }
                }
            }

            serverWebhook.Webhooks = lockal2;
            serverWebhook.Save();

            Close();
        }
    }
}
