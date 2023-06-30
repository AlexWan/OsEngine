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

            LabelOrderType1.Content = OsLocalization.Trader.Label103;
            LabelOrderType2.Content = OsLocalization.Trader.Label103;

            LabelSlippage1.Content = OsLocalization.Trader.Label92;
            LabelSlippage2.Content = OsLocalization.Trader.Label92;

            LabelVolume1.Content = OsLocalization.Trader.Label223;
            LabelVolume2.Content = OsLocalization.Trader.Label223;

            ButtonPositionSupport.Content = OsLocalization.Trader.Label47;

            CheckBoxSecondByMarket.Content = OsLocalization.Trader.Label245;

            LabelCorrelation.Content = OsLocalization.Trader.Label242;
            LabelCointegration.Content = OsLocalization.Trader.Label238;


            LabelCorrelationLookBack.Content = OsLocalization.Trader.Label240;
            LabelCointegrationLookBack.Content = OsLocalization.Trader.Label240;

            LabelCointegrationDeviation.Content = OsLocalization.Trader.Label239;

            ButtonSave.Content = OsLocalization.Trader.Label246;
            ButtonApply.Content = OsLocalization.Trader.Label247;
            
            // стартовые значения

            ComboBoxOrderType1.Items.Add(OrderPriceType.Limit.ToString());
            ComboBoxOrderType1.Items.Add(OrderPriceType.Market.ToString());
            ComboBoxOrderType1.SelectedItem = _tabPair.Sec1OrderPriceType.ToString();

            ComboBoxOrderType2.Items.Add(OrderPriceType.Limit.ToString());
            ComboBoxOrderType2.Items.Add(OrderPriceType.Market.ToString());
            ComboBoxOrderType2.SelectedItem = _tabPair.Sec2OrderPriceType.ToString();

            TextBoxSlippage1.Text = _tabPair.Sec1SlippagePercent.ToString();
            TextBoxSlippage2.Text = _tabPair.Sec2SlippagePercent.ToString();

            TextBoxVolume1.Text = _tabPair.Sec1Volume.ToString();
            TextBoxVolume2.Text = _tabPair.Sec2Volume.ToString();

            CheckBoxSecondByMarket.IsChecked = _tabPair.SecondByMarket;

            TextBoxCorrelationLookBack.Text = _tabPair.CorrelationLookBack.ToString();
            TextBoxCointegrationLookBack.Text = _tabPair.CointegrationLookBack.ToString();
            TextBoxCointegrationDeviation.Text = _tabPair.CointegrationDeviation.ToString();

            ButtonSave.Click += ButtonSave_Click;
            ButtonApply.Click += ButtonApply_Click;
            ButtonPositionSupport.Click += ButtonPositionSupport_Click;
            Closed += BotTabPairCommonSettingsUi_Closed;
        }

        private void BotTabPairCommonSettingsUi_Closed(object sender, EventArgs e)
        {
            ButtonSave.Click -= ButtonSave_Click;
            ButtonApply.Click -= ButtonApply_Click;
            Closed -= BotTabPairCommonSettingsUi_Closed;
            ButtonPositionSupport.Click -= ButtonPositionSupport_Click;
            _tabPair = null;
        }

        private void ButtonApply_Click(object sender, RoutedEventArgs e)
        {
            SaveSettingsFromUi();
            _tabPair.ApplySettingsFromStandartToAll();
        }

        private void ButtonSave_Click(object sender, RoutedEventArgs e)
        {
            SaveSettingsFromUi();
            Close();
        }

        private void ButtonPositionSupport_Click(object sender, RoutedEventArgs e)
        {
            _tabPair.StandartManualControl.ShowDialog();
        }

        private void SaveSettingsFromUi()
        {
            Enum.TryParse(ComboBoxOrderType1.SelectedItem.ToString(), out _tabPair.Sec1OrderPriceType);
            Enum.TryParse(ComboBoxOrderType2.SelectedItem.ToString(), out _tabPair.Sec2OrderPriceType);

            _tabPair.Sec1SlippagePercent = TextBoxSlippage1.Text.ToDecimal();
            _tabPair.Sec2SlippagePercent = TextBoxSlippage2.Text.ToDecimal();

            _tabPair.Sec1Volume = TextBoxVolume1.Text.ToDecimal();
            _tabPair.Sec2Volume = TextBoxVolume2.Text.ToDecimal();

            _tabPair.SecondByMarket = CheckBoxSecondByMarket.IsChecked.Value;

            _tabPair.CorrelationLookBack = Convert.ToInt32(TextBoxCorrelationLookBack.Text);
            _tabPair.CointegrationLookBack = Convert.ToInt32(TextBoxCointegrationLookBack.Text);
            _tabPair.CointegrationDeviation = TextBoxCointegrationDeviation.Text.ToDecimal();

            _tabPair.SaveStandartSettings();
        }
    }
}