/*
 *Your rights to use the code are governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 *Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System.Windows;
using OsEngine.Language;

namespace OsEngine.Logging
{
    /// <summary>
    /// Messaging settings window
    /// Окно настроек рассылки сообщений
    /// </summary>
    public partial class MessageSenderUi
    {
        /// <summary>
        /// distribution manager
        /// менедженр рассылки
        /// </summary>
        private readonly MessageSender _sender; 

        public MessageSenderUi(MessageSender sender) // constructor / конструктор
        {
            _sender = sender;
            InitializeComponent();
            LoadDateOnForm();

            Title = OsLocalization.Logging.TitleMessageSenderUi;

            Label3.Content = OsLocalization.Logging.Label3;
            Label4.Content = OsLocalization.Logging.Label4;
            Label19.Content = OsLocalization.Logging.Label19;
            Label5.Content = OsLocalization.Logging.Label5;
            Label52.Content = OsLocalization.Logging.Label5;
            Label53.Content = OsLocalization.Logging.Label5;
            ButtonAccept.Content = OsLocalization.Logging.Button1;
            ButtonSmsGlobeSet.Content = OsLocalization.Logging.Button2;
            ButtonMailGlobeSet.Content = OsLocalization.Logging.Button2;
            ButtonWebhookGlobeSet.Content = OsLocalization.Logging.Button2;

            CheckBoxSmsSignal.Content = OsLocalization.Logging.Label6;
            CheckBoxSmsTrade.Content = OsLocalization.Logging.Label7;
            CheckBoxSmsError.Content = OsLocalization.Logging.Label8;
            CheckBoxSmsSystem.Content = OsLocalization.Logging.Label9;
            CheckBoxSmsConnect.Content = OsLocalization.Logging.Label10;

            CheckBoxMailSignal.Content = OsLocalization.Logging.Label6;
            CheckBoxMailTrade.Content = OsLocalization.Logging.Label7;
            CheckBoxMailError.Content = OsLocalization.Logging.Label8;
            CheckBoxMailSystem.Content = OsLocalization.Logging.Label9;
            CheckBoxMailConnect.Content = OsLocalization.Logging.Label10;

            CheckBoxWebhookSignal.Content = OsLocalization.Logging.Label6;
            CheckBoxWebhookTrade.Content = OsLocalization.Logging.Label7;
            CheckBoxWebhookError.Content = OsLocalization.Logging.Label8;
            CheckBoxWebhookSystem.Content = OsLocalization.Logging.Label9;
            CheckBoxWebhookConnect.Content = OsLocalization.Logging.Label10;
        }

        /// <summary>
        /// upload settings to the form
        /// выгрузить настройки на форму
        /// </summary>
        private void LoadDateOnForm()
        {
            ComboBoxModeWebhook.Items.Add(OsLocalization.Logging.Label1);
            ComboBoxModeWebhook.Items.Add(OsLocalization.Logging.Label2);

            if (_sender.WebhookSendOn)
            {
                ComboBoxModeWebhook.Text = OsLocalization.Logging.Label1;
            }
            else
            {
                ComboBoxModeWebhook.Text = OsLocalization.Logging.Label2;
            }

            CheckBoxWebhookSignal.IsChecked = _sender.WebhookSignalSendOn;
            CheckBoxWebhookTrade.IsChecked = _sender.WebhookTradeSendOn;
            CheckBoxWebhookError.IsChecked = _sender.WebhookErrorSendOn;
            CheckBoxWebhookSystem.IsChecked = _sender.WebhookSystemSendOn;
            CheckBoxWebhookConnect.IsChecked = _sender.WebhookConnectSendOn;


            ComboBoxModeMail.Items.Add(OsLocalization.Logging.Label1);
            ComboBoxModeMail.Items.Add(OsLocalization.Logging.Label2);

            if (_sender.MailSendOn)
            {
                ComboBoxModeMail.Text = OsLocalization.Logging.Label1;
            }
            else
            {
                ComboBoxModeMail.Text = OsLocalization.Logging.Label2;
            }

            CheckBoxMailSignal.IsChecked = _sender.MailSignalSendOn;
            CheckBoxMailTrade.IsChecked = _sender.MailTradeSendOn;
            CheckBoxMailError.IsChecked = _sender.MailErrorSendOn;
            CheckBoxMailSystem.IsChecked = _sender.MailSystemSendOn;
            CheckBoxMailConnect.IsChecked = _sender.MailConnectSendOn;


            ComboBoxModeSms.Items.Add(OsLocalization.Logging.Label1);
            ComboBoxModeSms.Items.Add(OsLocalization.Logging.Label2);

            if (_sender.SmsSendOn)
            {
                ComboBoxModeSms.Text = OsLocalization.Logging.Label1;
            }
            else
            {
                ComboBoxModeSms.Text = OsLocalization.Logging.Label2;
            }

            CheckBoxSmsSignal.IsChecked = _sender.SmsSignalSendOn;
            CheckBoxSmsTrade.IsChecked = _sender.SmsTradeSendOn;
            CheckBoxSmsError.IsChecked = _sender.SmsErrorSendOn;
            CheckBoxSmsSystem.IsChecked = _sender.SmsSystemSendOn;
            CheckBoxSmsConnect.IsChecked = _sender.SmsConnectSendOn;
        }

        /// <summary>
        /// save
        /// сохранить
        /// </summary>
        private void Save()
        {
            if (ComboBoxModeWebhook.Text == OsLocalization.Logging.Label1)
            {
                _sender.WebhookSendOn = true;
            }
            else
            {
                _sender.WebhookSendOn = false;
            }

            _sender.WebhookSignalSendOn = CheckBoxWebhookSignal.IsChecked.Value;
            _sender.WebhookTradeSendOn = CheckBoxWebhookTrade.IsChecked.Value;
            _sender.WebhookErrorSendOn = CheckBoxWebhookError.IsChecked.Value;
            _sender.WebhookSystemSendOn = CheckBoxWebhookSystem.IsChecked.Value;
            _sender.WebhookConnectSendOn = CheckBoxWebhookConnect.IsChecked.Value;


            if (ComboBoxModeMail.Text == OsLocalization.Logging.Label1)
            {
                _sender.MailSendOn = true;
            }
            else
            {
                _sender.MailSendOn = false;
            }

           _sender.MailSignalSendOn =  CheckBoxMailSignal.IsChecked.Value;
           _sender.MailTradeSendOn = CheckBoxMailTrade.IsChecked.Value;
           _sender.MailErrorSendOn = CheckBoxMailError.IsChecked.Value;
           _sender.MailSystemSendOn = CheckBoxMailSystem.IsChecked.Value;
           _sender.MailConnectSendOn = CheckBoxMailConnect.IsChecked.Value;


            if (ComboBoxModeSms.Text == OsLocalization.Logging.Label1)
            {
                _sender.SmsSendOn = true;
            }
            else
            {
                _sender.SmsSendOn = false;
            }

            _sender.SmsSignalSendOn = CheckBoxSmsSignal.IsChecked.Value;
            _sender.SmsTradeSendOn = CheckBoxSmsTrade.IsChecked.Value;
            _sender.SmsErrorSendOn = CheckBoxSmsError.IsChecked.Value;
            _sender.SmsSystemSendOn = CheckBoxSmsSystem.IsChecked.Value;
            _sender.SmsConnectSendOn = CheckBoxSmsConnect.IsChecked.Value;
            _sender.Save();
        }

        private void ButtonAccept_Click(object sender, RoutedEventArgs e) // accept button / кнопка принять
        {
            Save();
            Close();
        }

        private void ButtonWebhookGlobeSet_Click(object sender, RoutedEventArgs e) // button to configure the webhook server / кнопка настроить сервер рассылки сообщений через вебхуки
        {
            ServerWebhook.GetServer().ShowDialog();
        }

        private void ButtonMailGlobeSet_Click(object sender, RoutedEventArgs e) // button to configure the mailing server / кнопка настроить сервер почтовой рассылки
        {
            ServerMail.GetServer().ShowDialog();
        }

        private void ButtonSmsGlobeSet_Click(object sender, RoutedEventArgs e) // button to configure the SMS messaging server / кнопка настроить сервер Смс рассылки
        {
            ServerSms.GetSmsServer().ShowDialog();
        }
    }
}
