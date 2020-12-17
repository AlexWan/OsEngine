/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using OsEngine.Language;


namespace OsEngine.PrimeSettings
{
    public partial class PrimeSettingsMasterUi
    {
        public PrimeSettingsMasterUi()
        {
            InitializeComponent();

            List<OsLocalization.OsLocalType> localizations = OsLocalization.GetExistLocalizationTypes();

            for (int i = 0; i < localizations.Count; i++)
            {
                ComboBoxLocalization.Items.Add(localizations[i].ToString());
            }

            ComboBoxLocalization.SelectedItem = OsLocalization.CurLocalization.ToString();
            ComboBoxLocalization.SelectionChanged += delegate
            {
                OsLocalization.OsLocalType newType;

                if (Enum.TryParse(ComboBoxLocalization.SelectedItem.ToString(), out newType))
                {
                    OsLocalization.CurLocalization = newType;
                }
            };

            CheckBoxServerTestingIsActive.IsChecked = PrimeSettingsMaster.ServerTestingIsActive;
            CheckBoxExtraLogWindow.IsChecked = PrimeSettingsMaster.ErrorLogMessageBoxIsActiv;
            CheckBoxExtraLogSound.IsChecked = PrimeSettingsMaster.ErrorLogBeepIsActiv;
            CheckBoxTransactionSound.IsChecked = PrimeSettingsMaster.TransactionBeepIsActiv;
            TextBoxToken.Text = PrimeSettingsMaster.Token;
            TextBoxIp.Text = PrimeSettingsMaster.Ip;
            TextBoxPort.Text = PrimeSettingsMaster.Port;
            AutoStartChb.IsChecked = PrimeSettingsMaster.AutoStartApi;

            CheckBoxExtraLogWindow.Click += CheckBoxExtraLogWindow_Click;
            CheckBoxExtraLogSound.Click += CheckBoxExtraLogSound_Click;
            CheckBoxTransactionSound.Click += CheckBoxTransactionSound_Click;
            CheckBoxServerTestingIsActive.Click += CheckBoxServerTestingIsActive_Click;
            AutoStartChb.Click += AutoStartChb_Click;

            ChangeText();
            OsLocalization.LocalizationTypeChangeEvent += ChangeText;
        }

        private void ChangeText()
        {
            MainItem.Header = OsLocalization.PrimeSettings.Title;
            LanguageLabel.Content = OsLocalization.PrimeSettings.LanguageLabel;
            ShowExtraLogWindowLabel.Content = OsLocalization.PrimeSettings.ShowExtraLogWindowLabel;
            ExtraLogSound.Content = OsLocalization.PrimeSettings.ExtraLogSoundLabel;
            TransactionSoundLabel.Content = OsLocalization.PrimeSettings.TransactionSoundLabel;
            TextBoxMessageToUsers.Text = OsLocalization.PrimeSettings.TextBoxMessageToUsers;
            LabelServerTestingIsActive.Content = OsLocalization.PrimeSettings.LabelServerTestingIsActive;
            LabelServerTestingIsActive.ToolTip = OsLocalization.PrimeSettings.LabelServerTestingToopTip;
            CheckBoxServerTestingIsActive.ToolTip = OsLocalization.PrimeSettings.LabelServerTestingToopTip;
            AdminItem.Header = OsLocalization.PrimeSettings.Title2;

            LabelState.Content = OsLocalization.PrimeSettings.LblState;
            LabelToken.Content = OsLocalization.PrimeSettings.LblToken;
            LabelIp.Content = OsLocalization.PrimeSettings.LblIp;
            LabelPort.Content = OsLocalization.PrimeSettings.LblPort;

            LabelConfirm.Content = OsLocalization.PrimeSettings.LblAdminPanel;
        }

        private void CheckBoxServerTestingIsActive_Click(object sender, RoutedEventArgs e)
        {
            if (CheckBoxServerTestingIsActive.IsChecked != null)
                PrimeSettingsMaster.ServerTestingIsActive = CheckBoxServerTestingIsActive.IsChecked.Value;
        }

        private void CheckBoxTransactionSound_Click(object sender, RoutedEventArgs e)
        {
            if (CheckBoxTransactionSound.IsChecked != null)
                PrimeSettingsMaster.TransactionBeepIsActiv = CheckBoxTransactionSound.IsChecked.Value;
        }

        private void CheckBoxExtraLogSound_Click(object sender, RoutedEventArgs e)
        {
            if (CheckBoxExtraLogSound.IsChecked != null)
                PrimeSettingsMaster.ErrorLogBeepIsActiv = CheckBoxExtraLogSound.IsChecked.Value;
        }

        private void CheckBoxExtraLogWindow_Click(object sender, RoutedEventArgs e)
        {
            if (CheckBoxExtraLogWindow.IsChecked != null)
                PrimeSettingsMaster.ErrorLogMessageBoxIsActiv = CheckBoxExtraLogWindow.IsChecked.Value;
        }

        private void AutoStartChb_Click(object sender, RoutedEventArgs e)
        {
            if (AutoStartChb.IsChecked != null)
                PrimeSettingsMaster.AutoStartApi = AutoStartChb.IsChecked.Value;
        }

        private void BtnGenerateToken_OnClick(object sender, RoutedEventArgs e)
        {
            string token = Guid.NewGuid().ToString();
            TextBoxToken.Text = token;
            PrimeSettingsMaster.Token = TextBoxToken.Text;
        }

        private void TextBoxIp_OnTextChanged(object sender, TextChangedEventArgs e)
        {
            PrimeSettingsMaster.Ip = TextBoxIp.Text;
        }

        private void TextBoxPort_OnTextChanged(object sender, TextChangedEventArgs e)
        {
            PrimeSettingsMaster.Port = TextBoxPort.Text;
        }
    }
}
