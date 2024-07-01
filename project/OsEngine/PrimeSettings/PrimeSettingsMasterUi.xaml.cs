/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System;
using System.Collections.Generic;
using System.Globalization;
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

            CheckBoxExtraLogWindow.IsChecked = PrimeSettingsMaster.ErrorLogMessageBoxIsActiv;
            CheckBoxExtraLogSound.IsChecked = PrimeSettingsMaster.ErrorLogBeepIsActiv;
            CheckBoxTransactionSound.IsChecked = PrimeSettingsMaster.TransactionBeepIsActiv;
            TextBoxToken.Text = PrimeSettingsMaster.Token;
            TextBoxIp.Text = PrimeSettingsMaster.Ip;
            TextBoxPort.Text = PrimeSettingsMaster.Port;
            AutoStartChb.IsChecked = PrimeSettingsMaster.AutoStartApi;
            TextBoxBotHeader.Text = PrimeSettingsMaster.LabelInHeaderBotStation;
            CheckBoxRebootTradeUiLigth.IsChecked = PrimeSettingsMaster.RebootTradeUiLigth;
            CheckBoxReportCriticalErrors.IsChecked = PrimeSettingsMaster.ReportCriticalErrors;

            CheckBoxExtraLogWindow.Click += CheckBoxExtraLogWindow_Click;
            CheckBoxExtraLogSound.Click += CheckBoxExtraLogSound_Click;
            CheckBoxTransactionSound.Click += CheckBoxTransactionSound_Click;
            AutoStartChb.Click += AutoStartChb_Click;
            TextBoxBotHeader.TextChanged += TextBoxBotHeader_TextChanged;
            CheckBoxRebootTradeUiLigth.Click += RebootTradeUiLigth_Click;
            CheckBoxReportCriticalErrors.Click += CheckBoxReportCriticalErrors_Click;

            ChangeText();
            OsLocalization.LocalizationTypeChangeEvent += ChangeText;

            this.Activate();
            this.Focus();
        }

        private void ChangeText()
        {
            MainItem.Header = OsLocalization.PrimeSettings.Title;
            LanguageLabel.Content = OsLocalization.PrimeSettings.LanguageLabel;
            LabelTimeFormat.Content = OsLocalization.PrimeSettings.TimeFormat;
            LabelDateFormat.Content = OsLocalization.PrimeSettings.DateFormat;
            ShowExtraLogWindowLabel.Content = OsLocalization.PrimeSettings.ShowExtraLogWindowLabel;
            ExtraLogSound.Content = OsLocalization.PrimeSettings.ExtraLogSoundLabel;
            TransactionSoundLabel.Content = OsLocalization.PrimeSettings.TransactionSoundLabel;
            TextBoxMessageToUsers.Text = OsLocalization.PrimeSettings.TextBoxMessageToUsers;
            AdminItem.Header = OsLocalization.PrimeSettings.Title2;

            LabelState.Content = OsLocalization.PrimeSettings.LblState;
            LabelToken.Content = OsLocalization.PrimeSettings.LblToken;
            LabelIp.Content = OsLocalization.PrimeSettings.LblIp;
            LabelPort.Content = OsLocalization.PrimeSettings.LblPort;

            LabelConfirm.Content = OsLocalization.PrimeSettings.LblAdminPanel;
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


        private void RebootTradeUiLigth_Click(object sender, RoutedEventArgs e)
        {
            if (CheckBoxRebootTradeUiLigth.IsChecked != null)
                PrimeSettingsMaster.RebootTradeUiLigth = CheckBoxRebootTradeUiLigth.IsChecked.Value;
        }

        private void CheckBoxReportCriticalErrors_Click(object sender, RoutedEventArgs e)
        {
            if (CheckBoxReportCriticalErrors.IsChecked != null)
                PrimeSettingsMaster.ReportCriticalErrors = CheckBoxReportCriticalErrors.IsChecked.Value;
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
