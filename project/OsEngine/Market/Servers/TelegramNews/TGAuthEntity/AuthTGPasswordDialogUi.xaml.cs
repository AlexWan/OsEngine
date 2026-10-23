using OsEngine.Language;
using OsEngine.Logging;
using OsEngine.Market;
using System;
using System.Windows;

namespace OsEngine.Market.Servers.TelegramNews.TGAuthEntity
{
    public partial class AuthTGPasswordDialogUi : Window
    {
        public string Password { get; private set; }

        public AuthTGPasswordDialogUi()
        {
            InitializeComponent();
            OsEngine.Layout.StickyBorders.Listen(this);
            OsEngine.Layout.StartupLocation.Start_MouseInCentre(this);

            Title = OsLocalization.Market.TelegramAuthTitle;
            LabelPassword.Content = OsLocalization.Market.ServerParamPassword + ":";
            ButtonSend.Content = OsLocalization.Market.SendButton;
            ButtonCancel.Content = OsLocalization.Entity.ButtonCancel1;

            ButtonSend.Click += ButtonSend_Click;
            ButtonCancel.Click += ButtonCancel_Click;
            Closing += AuthTGPasswordDialogUi_Closing;

            Activate();
            PasswordBoxAuth.Focus();
        }

        private void ButtonSend_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Password = PasswordBoxAuth.Password;
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

        private void AuthTGPasswordDialogUi_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            try
            {
                ButtonSend.Click -= ButtonSend_Click;
                ButtonCancel.Click -= ButtonCancel_Click;
                Closing -= AuthTGPasswordDialogUi_Closing;
            }
            catch (Exception error)
            {
                ServerMaster.SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }
    }
}
