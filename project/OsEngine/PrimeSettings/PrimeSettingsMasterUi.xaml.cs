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

            CheckBoxExtraLogWindow.IsChecked = PrimeSettingsMaster.ErrorLogMessageBoxIsActiv;
            CheckBoxExtraLogSound.IsChecked = PrimeSettingsMaster.ErrorLogBeepIsActiv;
            CheckBoxTransactionSound.IsChecked = PrimeSettingsMaster.TransactionBeepIsActiv;

            CheckBoxExtraLogWindow.Click += CheckBoxExtraLogWindow_Click;
            CheckBoxExtraLogSound.Click += CheckBoxExtraLogSound_Click;
            CheckBoxTransactionSound.Click += CheckBoxTransactionSound_Click;

            ChangeText();
            OsLocalization.LocalizationTypeChangeEvent += ChangeText;
        }

        private void ChangeText()
        {
            Title = OsLocalization.PrimeSettingsMasterUi.Title;
            LanguageLabel.Content = OsLocalization.PrimeSettingsMasterUi.LanguageLabel;
            ShowExtraLogWindowLabel.Content = OsLocalization.PrimeSettingsMasterUi.ShowExtraLogWindowLabel;
            ExtraLogSound.Content = OsLocalization.PrimeSettingsMasterUi.ExtraLogSoundLabel;
            TransactionSoundLabel.Content = OsLocalization.PrimeSettingsMasterUi.TransactionSoundLabel;
            TextBoxMessageToUsers.Text = OsLocalization.PrimeSettingsMasterUi.TextBoxMessageToUsers;
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
