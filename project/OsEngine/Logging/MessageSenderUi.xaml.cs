/*
 *Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System.Windows;

namespace OsEngine.Logging
{
    /// <summary>
    /// Окно настроек рассылки СМС сообщений
    /// </summary>
    public partial class MessageSenderUi
    {
        private readonly MessageSender _sender; // менедженр рассылки

        public MessageSenderUi(MessageSender sender) // конструктор
        {
            _sender = sender;
            InitializeComponent();
            LoadDateOnForm();
        }

        private void LoadDateOnForm() // выгрузить настройки на форму
        {
            if (_sender.MailSendOn)
            {
                ComboBoxModeMail.Text = "Включен";
            }
            else
            {
                ComboBoxModeMail.Text = "Отключен";
            }

            CheckBoxMailSignal.IsChecked = _sender.MailSignalSendOn;
            CheckBoxMailTrade.IsChecked = _sender.MailTradeSendOn;
            CheckBoxMailError.IsChecked = _sender.MailErrorSendOn;
            CheckBoxMailSystem.IsChecked = _sender.MailSystemSendOn;
            CheckBoxMailConnect.IsChecked = _sender.MailConnectSendOn;



            if (_sender.SmsSendOn)
            {
                ComboBoxModeSms.Text = "Включен";
            }
            else
            {
                ComboBoxModeSms.Text = "Отключен";
            }

            CheckBoxSmsSignal.IsChecked = _sender.SmsSignalSendOn;
            CheckBoxSmsTrade.IsChecked = _sender.SmsTradeSendOn;
            CheckBoxSmsError.IsChecked = _sender.SmsErrorSendOn;
            CheckBoxSmsSystem.IsChecked = _sender.SmsSystemSendOn;
            CheckBoxSmsConnect.IsChecked = _sender.SmsConnectSendOn;
        }

        private void Save() // сохранить
        {
            if (ComboBoxModeMail.Text == "Включен")
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

            if (ComboBoxModeSms.Text == "Включен" )
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

        private void ButtonAccept_Click(object sender, RoutedEventArgs e) // кнопка принять
        {
            Save();
            Close();
        }

        private void ButtonMailGlobeSet_Click(object sender, RoutedEventArgs e) // кнопка настроить сервер почтовой рассылки
        {
            ServerMail.GetServer().ShowDialog();
        }

        private void ButtonSmsGlobeSet_Click(object sender, RoutedEventArgs e) // кнопка настроить сервер Смс рассылки
        {
            ServerSms.GetSmsServer().ShowDialog();
        }
    }
}
