using OsEngine.Language;
using OsEngine.Logging;
using OsEngine.Market;
using System;
using System.Windows;

namespace OsEngine.Market.Servers.TelegramNews.TGAuthEntity
{
    public partial class AuthTGCodeDialogUi : Window
    {
        public string VerificationCode { get; private set; }

        public AuthTGCodeDialogUi()
        {
            InitializeComponent();
            OsEngine.Layout.StickyBorders.Listen(this);
            OsEngine.Layout.StartupLocation.Start_MouseInCentre(this);

            Title = OsLocalization.Market.TelegramAuthTitle;
            LabelCode.Content = OsLocalization.Market.AuthorizationCode + ":";
            ButtonSend.Content = OsLocalization.Market.SendButton;
            ButtonCancel.Content = OsLocalization.Entity.ButtonCancel1;

            ButtonSend.Click += ButtonSend_Click;
            ButtonCancel.Click += ButtonCancel_Click;
            Closing += AuthTGCodeDialogUi_Closing;

            Activate();
            TextBoxCode.Focus();
        }

        private void ButtonSend_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                VerificationCode = TextBoxCode.Text;
                DialogResult = true;
            }
            catch (Exception error)
            {
                ServerMaster.SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        private void ButtonCancel_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                DialogResult = false;
            }
            catch (Exception error)
            {
                ServerMaster.SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        private void AuthTGCodeDialogUi_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            try
            {
                ButtonSend.Click -= ButtonSend_Click;
                ButtonCancel.Click -= ButtonCancel_Click;
                Closing -= AuthTGCodeDialogUi_Closing;
            }
            catch (Exception error)
            {
                ServerMaster.SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }
    }
}
