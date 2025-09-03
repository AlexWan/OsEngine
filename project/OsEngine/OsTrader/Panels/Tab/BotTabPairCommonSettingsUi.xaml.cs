/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using OsEngine.Language;
using System;
using System.Windows;
using OsEngine.Entity;


namespace OsEngine.OsTrader.Panels.Tab
{
    /// <summary>
    /// Interaction logic for BotTabPairCommonSettingsUi.xaml
    /// </summary>
    public partial class BotTabPairCommonSettingsUi : Window
    {
        BotTabPair _tabPair;

        public BotTabPairCommonSettingsUi(BotTabPair tabPair)
        {
            InitializeComponent();

            OsEngine.Layout.StickyBorders.Listen(this);
            OsEngine.Layout.StartupLocation.Start_MouseInCentre(this);

            _tabPair = tabPair;

            // локализация

            Title = OsLocalization.Trader.Label248;

            LabelOrdersPlacement.Content = OsLocalization.Trader.Label244;
            LabelIndicators.Content = OsLocalization.Trader.Label251;

            LabelSecurity1.Content = OsLocalization.Trader.Label102 + " 1";
            LabelSecurity2.Content = OsLocalization.Trader.Label102 + " 2";

            LabelSlippage1.Content = OsLocalization.Trader.Label92;
            LabelSlippage2.Content = OsLocalization.Trader.Label92;

            LabelVolume1.Content = OsLocalization.Trader.Label223;
            LabelVolume2.Content = OsLocalization.Trader.Label223;

            ButtonPositionSupport.Content = OsLocalization.Trader.Label47;

            LabelRegime1.Content = OsLocalization.Trader.Label115;
            LabelRegime2.Content = OsLocalization.Trader.Label115;

            LabelCorrelation.Content = OsLocalization.Trader.Label242;
            LabelCointegration.Content = OsLocalization.Trader.Label238;


            LabelCorrelationLookBack.Content = OsLocalization.Trader.Label240;
            LabelCointegrationLookBack.Content = OsLocalization.Trader.Label240;

            LabelCointegrationDeviation.Content = OsLocalization.Trader.Label239;

            ButtonSave.Content = OsLocalization.Trader.Label246;
            ButtonApply.Content = OsLocalization.Trader.Label247;

            CheckBoxCointegrationAutoIsOn.Content = OsLocalization.Trader.Label309;
            CheckBoxCorrelationAutoIsOn.Content = OsLocalization.Trader.Label309;

            // стартовые значения

            ComboBoxSlippageTypeSec1.Items.Add(PairTraderSlippageType.Absolute.ToString());
            ComboBoxSlippageTypeSec1.Items.Add(PairTraderSlippageType.Percent.ToString());
            ComboBoxSlippageTypeSec1.SelectedItem = _tabPair.Sec1SlippageType.ToString();

            ComboBoxVolumeTypeSec1.Items.Add(PairTraderVolumeType.Contract.ToString());
            ComboBoxVolumeTypeSec1.Items.Add(PairTraderVolumeType.Currency.ToString());
            ComboBoxVolumeTypeSec1.SelectedItem = _tabPair.Sec1VolumeType.ToString();

            ComboBoxSlippageTypeSec2.Items.Add(PairTraderSlippageType.Absolute.ToString());
            ComboBoxSlippageTypeSec2.Items.Add(PairTraderSlippageType.Percent.ToString());
            ComboBoxSlippageTypeSec2.SelectedItem = _tabPair.Sec2SlippageType.ToString();

            ComboBoxVolumeTypeSec2.Items.Add(PairTraderVolumeType.Contract.ToString());
            ComboBoxVolumeTypeSec2.Items.Add(PairTraderVolumeType.Currency.ToString());
            ComboBoxVolumeTypeSec2.SelectedItem = _tabPair.Sec2VolumeType.ToString();

            ComboBoxRegime1.Items.Add(PairTraderSecurityTradeRegime.Off.ToString());
            ComboBoxRegime1.Items.Add(PairTraderSecurityTradeRegime.Limit.ToString());
            ComboBoxRegime1.Items.Add(PairTraderSecurityTradeRegime.Market.ToString());
            ComboBoxRegime1.Items.Add(PairTraderSecurityTradeRegime.Second.ToString());
            ComboBoxRegime1.SelectedItem = _tabPair.Sec1TradeRegime.ToString();

            ComboBoxRegime2.Items.Add(PairTraderSecurityTradeRegime.Off.ToString());
            ComboBoxRegime2.Items.Add(PairTraderSecurityTradeRegime.Limit.ToString());
            ComboBoxRegime2.Items.Add(PairTraderSecurityTradeRegime.Market.ToString());
            ComboBoxRegime2.Items.Add(PairTraderSecurityTradeRegime.Second.ToString());
            ComboBoxRegime2.SelectedItem = _tabPair.Sec2TradeRegime.ToString();

            TextBoxSlippage1.Text = _tabPair.Sec1Slippage.ToString();
            TextBoxSlippage2.Text = _tabPair.Sec2Slippage.ToString();

            TextBoxVolume1.Text = _tabPair.Sec1Volume.ToString();
            TextBoxVolume2.Text = _tabPair.Sec2Volume.ToString();

            TextBoxCorrelationLookBack.Text = _tabPair.CorrelationLookBack.ToString();
            TextBoxCointegrationLookBack.Text = _tabPair.CointegrationLookBack.ToString();
            TextBoxCointegrationDeviation.Text = _tabPair.CointegrationDeviation.ToString();

            CheckBoxCorrelationAutoIsOn.IsChecked = _tabPair.AutoRebuildCorrelation;
            CheckBoxCointegrationAutoIsOn.IsChecked = _tabPair.AutoRebuildCointegration;

            ButtonSave.Click += ButtonSave_Click;
            ButtonApply.Click += ButtonApply_Click;
            ButtonPositionSupport.Click += ButtonPositionSupport_Click;
            Closed += BotTabPairCommonSettingsUi_Closed;
        }

        private void BotTabPairCommonSettingsUi_Closed(object sender, EventArgs e)
        {
            try
            {
                ButtonSave.Click -= ButtonSave_Click;
                ButtonApply.Click -= ButtonApply_Click;
                Closed -= BotTabPairCommonSettingsUi_Closed;
                ButtonPositionSupport.Click -= ButtonPositionSupport_Click;

                _tabPair = null;
            }
            catch (Exception error)
            {
                MessageBox.Show(error.ToString());
            }
        }

        private void ButtonApply_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                SaveSettingsFromUi();
                _tabPair.ApplySettingsFromStandardToAll();
            }
            catch (Exception error)
            {
                MessageBox.Show(error.ToString());
            }
        }

        private void ButtonSave_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                SaveSettingsFromUi();
                Close();
            }
            catch (Exception error)
            {
                MessageBox.Show(error.ToString());
            }
        }

        private void ButtonPositionSupport_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                _tabPair.StandardManualControl.ShowDialog(_tabPair.StartProgram);
            }
            catch (Exception error)
            {
                MessageBox.Show(error.ToString());
            }
        }

        private void SaveSettingsFromUi()
        {
            try
            {
                Enum.TryParse(ComboBoxSlippageTypeSec1.SelectedItem.ToString(), out _tabPair.Sec1SlippageType);
                Enum.TryParse(ComboBoxVolumeTypeSec1.SelectedItem.ToString(), out _tabPair.Sec1VolumeType);

                Enum.TryParse(ComboBoxSlippageTypeSec2.SelectedItem.ToString(), out _tabPair.Sec2SlippageType);
                Enum.TryParse(ComboBoxVolumeTypeSec2.SelectedItem.ToString(), out _tabPair.Sec2VolumeType);

                Enum.TryParse(ComboBoxRegime1.SelectedItem.ToString(), out _tabPair.Sec1TradeRegime);
                Enum.TryParse(ComboBoxRegime2.SelectedItem.ToString(), out _tabPair.Sec2TradeRegime);

                _tabPair.Sec1Slippage = TextBoxSlippage1.Text.ToDecimal();
                _tabPair.Sec2Slippage = TextBoxSlippage2.Text.ToDecimal();

                _tabPair.Sec1Volume = TextBoxVolume1.Text.ToDecimal();
                _tabPair.Sec2Volume = TextBoxVolume2.Text.ToDecimal();

                _tabPair.CorrelationLookBack = Convert.ToInt32(TextBoxCorrelationLookBack.Text);
                _tabPair.CointegrationLookBack = Convert.ToInt32(TextBoxCointegrationLookBack.Text);
                _tabPair.CointegrationDeviation = TextBoxCointegrationDeviation.Text.ToDecimal();

                _tabPair.AutoRebuildCointegration = CheckBoxCointegrationAutoIsOn.IsChecked.Value;
                _tabPair.AutoRebuildCorrelation = CheckBoxCorrelationAutoIsOn.IsChecked.Value;

                _tabPair.SaveStandartSettings();
            }
            catch(Exception error)
            {
                MessageBox.Show(error.ToString());
            }
        }
    }
}