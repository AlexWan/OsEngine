using System;
using System.Collections.Generic;
using System.Windows;
using OsEngine.Language;


namespace OsEngine.PrimeSettings
{
    /// <summary>
    /// Логика взаимодействия для PrimeSettingsMasterUi.xaml
    /// </summary>
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

            CheckBoxExtraLogWindow.Click += CheckBoxExtraLogWindow_Click;
            CheckBoxExtraLogSound.Click += CheckBoxExtraLogSound_Click;
            CheckBoxTransactionSound.Click += CheckBoxTransactionSound_Click;
            CheckBoxServerTestingIsActive.Click += CheckBoxServerTestingIsActive_Click;

            ChangeText();
            OsLocalization.LocalizationTypeChangeEvent += ChangeText;
        }

        private void ChangeText()
        {
            Title = OsLocalization.PrimeSettings.Title;
            LanguageLabel.Content = OsLocalization.PrimeSettings.LanguageLabel;
            ShowExtraLogWindowLabel.Content = OsLocalization.PrimeSettings.ShowExtraLogWindowLabel;
            ExtraLogSound.Content = OsLocalization.PrimeSettings.ExtraLogSoundLabel;
            TransactionSoundLabel.Content = OsLocalization.PrimeSettings.TransactionSoundLabel;
            TextBoxMessageToUsers.Text = OsLocalization.PrimeSettings.TextBoxMessageToUsers;
            LabelServerTestingIsActive.Content = OsLocalization.PrimeSettings.LabelServerTestingIsActive;
            LabelServerTestingIsActive.ToolTip = OsLocalization.PrimeSettings.LabelServerTestingToopTip;
            CheckBoxServerTestingIsActive.ToolTip = OsLocalization.PrimeSettings.LabelServerTestingToopTip;
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
    }
}
