/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using OsEngine.Entity;
using OsEngine.Language;
using System;
using System.Windows;

namespace OsEngine.OsTrader.Panels.Tab
{
    /// <summary>
    /// Interaction logic for BotTabPolygonCommonSettingsUi.xaml
    /// </summary>
    public partial class BotTabPolygonCommonSettingsUi : Window
    {
        BotTabPolygon _polygon;

        public BotTabPolygonCommonSettingsUi(BotTabPolygon polygon)
        {
            InitializeComponent();
            _polygon = polygon;

            OsEngine.Layout.StickyBorders.Listen(this);
            OsEngine.Layout.StartupLocation.Start_MouseInCentre(this);

            TextBoxSeparatorToSecurities.Text = polygon.SeparatorToSecurities;

            ComboBoxComissionType.Items.Add(ComissionPolygonType.None.ToString());
            ComboBoxComissionType.Items.Add(ComissionPolygonType.Percent.ToString());
            ComboBoxComissionType.SelectedItem = _polygon.ComissionType.ToString();

            TextBoxComissionValue.Text = _polygon.ComissionValue.ToString();
            CheckBoxCommisionIsSubstract.IsChecked = _polygon.ComissionIsSubstract;

            ComboBoxDelayType.Items.Add(DelayPolygonType.ByExecution.ToString());
            ComboBoxDelayType.Items.Add(DelayPolygonType.InMLS.ToString());
            ComboBoxDelayType.Items.Add(DelayPolygonType.Instantly.ToString());
            ComboBoxDelayType.SelectedItem = _polygon.DelayType.ToString();

            TextBoxDelayMls.Text = _polygon.DelayMls.ToString();
            TextBoxLimitQtyStart.Text = _polygon.QtyStart.ToString();
            TextBoxLimitSlippage.Text = _polygon.SlippagePercent.ToString();

            TextBoxProfitToSignal.Text = _polygon.ProfitToSignal.ToString();

            ComboBoxActionOnSignalType.Items.Add(PolygonActionOnSignalType.Bot_Event.ToString());
            ComboBoxActionOnSignalType.Items.Add(PolygonActionOnSignalType.All.ToString());
            ComboBoxActionOnSignalType.Items.Add(PolygonActionOnSignalType.Alert.ToString());
            ComboBoxActionOnSignalType.Items.Add(PolygonActionOnSignalType.None.ToString());
            ComboBoxActionOnSignalType.SelectedItem = _polygon.ActionOnSignalType.ToString();

            ComboBoxOrderPriceType.Items.Add(OrderPriceType.Limit.ToString());
            ComboBoxOrderPriceType.Items.Add(OrderPriceType.Market.ToString());
            ComboBoxOrderPriceType.SelectedItem = _polygon.OrderPriceType.ToString();

            // Localization

            LabelProfitToSignal.Content = OsLocalization.Trader.Label335;
            LabelActionOnSignalType.Content = OsLocalization.Trader.Label336;

            LabelStartSecutiySettings.Content = OsLocalization.Trader.Label315;
            LabelComissionSettings.Content = OsLocalization.Trader.Label316;
            LabelSeparator.Content = OsLocalization.Trader.Label319;
            LabelComissionType.Content = OsLocalization.Trader.Label320;
            LabelComissionValue.Content = OsLocalization.Trader.Label321;
            CheckBoxCommisionIsSubstract.Content = OsLocalization.Trader.Label322;

            LabelQtyStartLimit.Content = OsLocalization.Trader.Label325;
            LabelSlippageLimit.Content = OsLocalization.Trader.Label326;

            LabelExecutionSettings.Content = OsLocalization.Trader.Label329;
            LabelDelay.Content = OsLocalization.Trader.Label330;
            LabelInterval.Content = OsLocalization.Trader.Label331;

            ButtonSave.Content = OsLocalization.Trader.Label246;
            ButtonApply.Content = OsLocalization.Trader.Label247;

            LabelExecution.Content = OsLocalization.Trader.Label337;
            LabelOrderPriceType.Content = OsLocalization.Trader.Label338;

            Title = OsLocalization.Trader.Label232;

            ButtonSave.Click += ButtonSave_Click;
            ButtonApply.Click += ButtonApply_Click;

            this.Closed += BotTabPolygonCommonSettingsUi_Closed;
        }

        private void BotTabPolygonCommonSettingsUi_Closed(object sender, EventArgs e)
        {
            ButtonSave.Click -= ButtonSave_Click;
            ButtonApply.Click -= ButtonApply_Click;
            this.Closed -= BotTabPolygonCommonSettingsUi_Closed;
            _polygon = null;
        }

        private void ButtonApply_Click(object sender, RoutedEventArgs e)
        {
            SaveSettingsFromUiToBot();
            _polygon.ApplyStandartSettingsToAllSequence();
        }

        private void ButtonSave_Click(object sender, RoutedEventArgs e)
        {
            SaveSettingsFromUiToBot();
        }

        private void SaveSettingsFromUiToBot()
        {
            try
            {
                Enum.TryParse(ComboBoxOrderPriceType.SelectedItem.ToString(), out _polygon.OrderPriceType);
            }
            catch
            {
                // ignore
            }

            try
            {
                Enum.TryParse(ComboBoxActionOnSignalType.SelectedItem.ToString(), out _polygon.ActionOnSignalType);
            }
            catch
            {
                // ignore
            }

            try
            {
                _polygon.ProfitToSignal = TextBoxProfitToSignal.Text.ToString().ToDecimal();
            }
            catch
            {
                // ignore
            }

            try
            {
                _polygon.SlippagePercent = TextBoxLimitSlippage.Text.ToString().ToDecimal();
            }
            catch
            {
                // ignore
            }

            try
            {
                _polygon.QtyStart = TextBoxLimitQtyStart.Text.ToString().ToDecimal();
            }
            catch
            {
                // ignore
            }

            try
            {
                _polygon.DelayMls = Convert.ToInt32(TextBoxDelayMls.Text.ToString());
            }
            catch
            {
                // ignore
            }

            try
            {
                Enum.TryParse(ComboBoxDelayType.SelectedItem.ToString(), out _polygon.DelayType);
            }
            catch
            {
                // ignore
            }

            try
            {
                _polygon.ComissionIsSubstract = CheckBoxCommisionIsSubstract.IsChecked.Value;
            }
            catch
            {
                // ignore
            }

            try
            {
                _polygon.ComissionValue = TextBoxComissionValue.Text.ToString().ToDecimal();
            }
            catch
            {
                // ignore
            }

            try
            {
                Enum.TryParse(ComboBoxComissionType.SelectedItem.ToString(), out _polygon.ComissionType);
            }
            catch
            {
                // ignore
            }

            try
            {
                _polygon.SeparatorToSecurities = TextBoxSeparatorToSecurities.Text;
            }
            catch
            {
                // ignore
            }

            _polygon.SaveStandartSettings();
        }
    }
}