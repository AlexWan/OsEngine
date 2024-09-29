﻿/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System;
using System.Collections.Generic;
using System.Threading;
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
            OsEngine.Layout.StickyBorders.Listen(this);
            OsEngine.Layout.StartupLocation.Start_MouseInCentre(this);

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
                    Thread.CurrentThread.CurrentCulture = OsLocalization.CurCulture;
                }
            };

            ComboBoxTimeFormat.Items.Add("H:mm:ss");
            ComboBoxTimeFormat.Items.Add("h:mm:ss tt");
            ComboBoxTimeFormat.SelectedItem = OsLocalization.LongTimePattern;
            ComboBoxTimeFormat.SelectionChanged += delegate
            {
                OsLocalization.LongTimePattern = ComboBoxTimeFormat.SelectedItem.ToString();
                Thread.CurrentThread.CurrentCulture = OsLocalization.CurCulture;
            };

            ComboBoxDateFormat.Items.Add("dd.MM.yyyy");
            ComboBoxDateFormat.Items.Add("M/d/yyyy");
            ComboBoxDateFormat.SelectedItem = OsLocalization.ShortDatePattern;
            ComboBoxDateFormat.SelectionChanged += delegate
            {
                OsLocalization.ShortDatePattern = ComboBoxDateFormat.SelectedItem.ToString();
                Thread.CurrentThread.CurrentCulture = OsLocalization.CurCulture;
            };

            CheckBoxExtraLogWindow.IsChecked = PrimeSettingsMaster.ErrorLogMessageBoxIsActive;
            CheckBoxExtraLogSound.IsChecked = PrimeSettingsMaster.ErrorLogBeepIsActive;
            CheckBoxTransactionSound.IsChecked = PrimeSettingsMaster.TransactionBeepIsActive;
            TextBoxBotHeader.Text = PrimeSettingsMaster.LabelInHeaderBotStation;
            CheckBoxRebootTradeUiLigth.IsChecked = PrimeSettingsMaster.RebootTradeUiLight;
            CheckBoxReportCriticalErrors.IsChecked = PrimeSettingsMaster.ReportCriticalErrors;

            CheckBoxExtraLogWindow.Click += CheckBoxExtraLogWindow_Click;
            CheckBoxExtraLogSound.Click += CheckBoxExtraLogSound_Click;
            CheckBoxTransactionSound.Click += CheckBoxTransactionSound_Click;
            TextBoxBotHeader.TextChanged += TextBoxBotHeader_TextChanged;
            CheckBoxRebootTradeUiLigth.Click += RebootTradeUiLight_Click;
            CheckBoxReportCriticalErrors.Click += CheckBoxReportCriticalErrors_Click;

            ChangeText();
            OsLocalization.LocalizationTypeChangeEvent += ChangeText;

            this.Activate();
            this.Focus();
        }

        private void ChangeText()
        {
            LanguageLabel.Content = OsLocalization.PrimeSettings.LanguageLabel;
            LabelTimeFormat.Content = OsLocalization.PrimeSettings.TimeFormat;
            LabelDateFormat.Content = OsLocalization.PrimeSettings.DateFormat;
            ShowExtraLogWindowLabel.Content = OsLocalization.PrimeSettings.ShowExtraLogWindowLabel;
            ExtraLogSound.Content = OsLocalization.PrimeSettings.ExtraLogSoundLabel;
            TransactionSoundLabel.Content = OsLocalization.PrimeSettings.TransactionSoundLabel;
            TextBoxMessageToUsers.Text = OsLocalization.PrimeSettings.TextBoxMessageToUsers;

            LabelHeader.Content = OsLocalization.PrimeSettings.LabelBotHeader;
            LabelRebootTradeUiLigth.Content = OsLocalization.PrimeSettings.LabelLightReboot;
            LabelReportCriticalErrors.Content = OsLocalization.PrimeSettings.ReportErrorsOnServer;
        }

        private void TextBoxBotHeader_TextChanged(object sender, TextChangedEventArgs e)
        {
            PrimeSettingsMaster.LabelInHeaderBotStation = TextBoxBotHeader.Text;
        }

        private void CheckBoxTransactionSound_Click(object sender, RoutedEventArgs e)
        {
            if (CheckBoxTransactionSound.IsChecked != null)
                PrimeSettingsMaster.TransactionBeepIsActive = CheckBoxTransactionSound.IsChecked.Value;
        }

        private void CheckBoxExtraLogSound_Click(object sender, RoutedEventArgs e)
        {
            if (CheckBoxExtraLogSound.IsChecked != null)
                PrimeSettingsMaster.ErrorLogBeepIsActive = CheckBoxExtraLogSound.IsChecked.Value;
        }

        private void CheckBoxExtraLogWindow_Click(object sender, RoutedEventArgs e)
        {
            if (CheckBoxExtraLogWindow.IsChecked != null)
                PrimeSettingsMaster.ErrorLogMessageBoxIsActive = CheckBoxExtraLogWindow.IsChecked.Value;
        }

        private void RebootTradeUiLight_Click(object sender, RoutedEventArgs e)
        {
            if (CheckBoxRebootTradeUiLigth.IsChecked != null)
                PrimeSettingsMaster.RebootTradeUiLight = CheckBoxRebootTradeUiLigth.IsChecked.Value;
        }

        private void CheckBoxReportCriticalErrors_Click(object sender, RoutedEventArgs e)
        {
            if (CheckBoxReportCriticalErrors.IsChecked != null)
                PrimeSettingsMaster.ReportCriticalErrors = CheckBoxReportCriticalErrors.IsChecked.Value;
        }
    }
}
