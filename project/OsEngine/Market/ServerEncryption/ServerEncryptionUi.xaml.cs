using OsEngine.Entity;
using OsEngine.Language;
using OsEngine.Logging;
using System;
using System.Windows;
using System.Windows.Controls;

namespace OsEngine.Market.ServerEncryption
{
    /// <summary>
    /// Interaction logic for ServerEncryptionUi.xaml
    /// </summary>
    public partial class ServerEncryptionUi : Window
    {
        public ServerEncryptionUi(bool unlockMode)
        {
            InitializeComponent();
            OsEngine.Layout.StartupLocation.Start_MouseInCentre(this);

            _unlockMode = unlockMode;

            Title = OsLocalization.Market.Label342;

            TabItemSetup.Header = OsLocalization.Market.Label364;
            TabItemUnlock.Header = OsLocalization.Market.Label365;
            TabItemChange.Header = OsLocalization.Market.Label366;
            TabItemDisable.Header = OsLocalization.Market.Label367;

            LabelNewPasswordSetup.Content = OsLocalization.Market.Label345;
            LabelRepeatPasswordSetup.Content = OsLocalization.Market.Label346;
            LabelForgotNoteSetup.Content = OsLocalization.Market.Label362;
            ButtonSetup.Content = OsLocalization.Market.Label347;

            LabelCaptionUnlock.Content = OsLocalization.Market.Label358;
            LabelCurrentPasswordUnlock.Content = OsLocalization.Market.Label344;
            LabelForgotNoteUnlock.Content = OsLocalization.Market.Label362;
            ButtonUnlock.Content = OsLocalization.Market.Label356;

            LabelCurrentPasswordChange.Content = OsLocalization.Market.Label344;
            LabelNewPasswordChange.Content = OsLocalization.Market.Label345;
            LabelRepeatPasswordChange.Content = OsLocalization.Market.Label346;
            ButtonChange.Content = OsLocalization.Market.Label349;

            LabelWarningDisable.Content = OsLocalization.Market.Label368;
            LabelCurrentPasswordDisable.Content = OsLocalization.Market.Label344;
            ButtonDisable.Content = OsLocalization.Market.Label348;

            ButtonClose.Content = OsLocalization.Market.Label357;

            ServerEncryptionStatus status = ServerEncryptionMaster.GetStatus();

            if (status == ServerEncryptionStatus.Encrypted)
            {
                TabItemSetup.Visibility = Visibility.Collapsed;

                if (ServerEncryptionMaster.IsUnlocked)
                {
                    if (unlockMode == false)
                    {
                        // шифрователь уже разблокирован - вкладка разблокировки не нужна
                        TabItemUnlock.Visibility = Visibility.Collapsed;
                    }

                    TabItemChange.IsSelected = true;
                }
                else
                {
                    // до ввода мастер-пароля смена и отключение заблокированы
                    TabItemChange.IsEnabled = false;
                    TabItemDisable.IsEnabled = false;

                    TabItemUnlock.IsSelected = true;
                }
            }
            else
            {
                TabItemUnlock.Visibility = Visibility.Collapsed;
                TabItemChange.Visibility = Visibility.Collapsed;
                TabItemDisable.Visibility = Visibility.Collapsed;

                TabItemSetup.IsSelected = true;
            }

            Closed += ServerEncryptionUi_Closed;
        }

        private readonly bool _unlockMode;

        private void ServerEncryptionUi_Closed(object sender, EventArgs e)
        {
            try
            {
                ButtonSetup.Click -= ButtonSetup_Click;
                ButtonUnlock.Click -= ButtonUnlock_Click;
                ButtonChange.Click -= ButtonChange_Click;
                ButtonDisable.Click -= ButtonDisable_Click;
                ButtonClose.Click -= ButtonClose_Click;
                ButtonAboutSetup.Click -= ButtonAboutSetup_Click;
                ButtonAboutUnlock.Click -= ButtonAboutUnlock_Click;
                ButtonAboutChange.Click -= ButtonAboutChange_Click;
                ButtonAboutDisable.Click -= ButtonAboutDisable_Click;

                Closed -= ServerEncryptionUi_Closed;
            }
            catch (Exception ex)
            {
                ServerMaster.SendNewLogMessage(ex.ToString(), LogMessageType.Error);
            }
        }

        private void ButtonSetup_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string newPassword = TextBoxNewPasswordSetup.Text;

                if (ValidateNewPassword(newPassword, TextBoxRepeatPasswordSetup.Text, LabelErrorSetup) == false)
                {
                    return;
                }

                if (ServerEncryptionMaster.Enable(newPassword))
                {
                    Close();
                }
            }
            catch (Exception ex)
            {
                ServerMaster.SendNewLogMessage(ex.ToString(), LogMessageType.Error);
            }
        }

        private void ButtonUnlock_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (ServerEncryptionMaster.TryUnlock(TextBoxCurrentPasswordUnlock.Text))
                {
                    if (_unlockMode)
                    {
                        Close();
                    }
                    else
                    {
                        // ручная разблокировка - открываем доступ к смене и отключению
                        TabItemChange.IsEnabled = true;
                        TabItemDisable.IsEnabled = true;
                        TabItemChange.IsSelected = true;
                    }
                }
                else
                {
                    ShowError(LabelErrorUnlock, OsLocalization.Market.Label350);
                    TextBoxCurrentPasswordUnlock.Text = "";
                }
            }
            catch (Exception ex)
            {
                ServerMaster.SendNewLogMessage(ex.ToString(), LogMessageType.Error);
            }
        }

        private void ButtonChange_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string newPassword = TextBoxNewPasswordChange.Text;

                if (ValidateNewPassword(newPassword, TextBoxRepeatPasswordChange.Text, LabelErrorChange) == false)
                {
                    return;
                }

                if (ServerEncryptionMaster.TryUnlock(TextBoxCurrentPasswordChange.Text) == false)
                {
                    ShowError(LabelErrorChange, OsLocalization.Market.Label350);
                    TextBoxCurrentPasswordChange.Text = "";
                    return;
                }

                if (ServerEncryptionMaster.ChangePassword(TextBoxCurrentPasswordChange.Text, newPassword))
                {
                    Close();
                }
                else
                {
                    ShowError(LabelErrorChange, OsLocalization.Market.Label369);
                }
            }
            catch (Exception ex)
            {
                ServerMaster.SendNewLogMessage(ex.ToString(), LogMessageType.Error);
            }
        }

        private void ButtonDisable_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (ServerEncryptionMaster.TryUnlock(TextBoxCurrentPasswordDisable.Text) == false)
                {
                    ShowError(LabelErrorDisable, OsLocalization.Market.Label350);
                    TextBoxCurrentPasswordDisable.Text = "";
                    return;
                }

                if (ServerEncryptionMaster.Disable(TextBoxCurrentPasswordDisable.Text))
                {
                    Close();
                }
                else
                {
                    ShowError(LabelErrorDisable, OsLocalization.Market.Label369);
                }
            }
            catch (Exception ex)
            {
                ServerMaster.SendNewLogMessage(ex.ToString(), LogMessageType.Error);
            }
        }

        private void ButtonClose_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_unlockMode)
                {
                    ServerEncryptionMaster.SetUnlockDeclined();
                }

                Close();
            }
            catch (Exception ex)
            {
                ServerMaster.SendNewLogMessage(ex.ToString(), LogMessageType.Error);
            }
        }

        private void ButtonAboutSetup_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                CustomMessageBoxUi ui = new CustomMessageBoxUi(OsLocalization.Market.Label370);
                ui.ShowDialog();
            }
            catch (Exception ex)
            {
                ServerMaster.SendNewLogMessage(ex.ToString(), LogMessageType.Error);
            }
        }

        private void ButtonAboutUnlock_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                CustomMessageBoxUi ui = new CustomMessageBoxUi(OsLocalization.Market.Label371);
                ui.ShowDialog();
            }
            catch (Exception ex)
            {
                ServerMaster.SendNewLogMessage(ex.ToString(), LogMessageType.Error);
            }
        }

        private void ButtonAboutChange_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                CustomMessageBoxUi ui = new CustomMessageBoxUi(OsLocalization.Market.Label372);
                ui.ShowDialog();
            }
            catch (Exception ex)
            {
                ServerMaster.SendNewLogMessage(ex.ToString(), LogMessageType.Error);
            }
        }

        private void ButtonAboutDisable_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                CustomMessageBoxUi ui = new CustomMessageBoxUi(OsLocalization.Market.Label373);
                ui.ShowDialog();
            }
            catch (Exception ex)
            {
                ServerMaster.SendNewLogMessage(ex.ToString(), LogMessageType.Error);
            }
        }

        private bool ValidateNewPassword(string newPassword, string repeatPassword, Label errorLabel)
        {
            if (string.IsNullOrEmpty(newPassword)
                || newPassword.Length < 8)
            {
                ShowError(errorLabel, OsLocalization.Market.Label351);
                return false;
            }

            if (newPassword != repeatPassword)
            {
                ShowError(errorLabel, OsLocalization.Market.Label352);
                return false;
            }

            return true;
        }

        private void ShowError(Label errorLabel, string message)
        {
            errorLabel.Content = message;
        }
    }
}
