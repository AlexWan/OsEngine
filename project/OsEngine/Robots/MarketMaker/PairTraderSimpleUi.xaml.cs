using System;
using System.Globalization;
using System.Windows;
using OsEngine.Entity;
using OsEngine.Language;
using OsEngine.OsTrader.Panels;

namespace OsEngine.Robots.MarketMaker
{
    /// <summary>
    /// Interaction logic for PairTraderSimpleUi.xaml
    /// </summary>
    public partial class PairTraderSimpleUi
    {
        private PairTraderSimple _strategy;

        public PairTraderSimpleUi(PairTraderSimple strategy)
        {
            InitializeComponent();
            OsEngine.Layout.StickyBorders.Listen(this);
            OsEngine.Layout.StartupLocation.Start_MouseInCentre(this);
            _strategy = strategy;

            CultureInfo culture = new CultureInfo("ru-RU");

            TextBoxSlippage1.Text = _strategy.Slippage1.ToString(culture);
            TextBoxSlippage2.Text = _strategy.Slippage2.ToString(culture);

            TextBoxVolume1.Text = _strategy.Volume1.ToString(culture);
            TextBoxVolume2.Text = _strategy.Volume2.ToString(culture);

            TextBoxAssetInPortfolio1.Text = "Prime";
            TextBoxAssetInPortfolio2.Text = "Prime";

            ComboBoxRegime.Items.Add(BotTradeRegime.Off);
            ComboBoxRegime.Items.Add(BotTradeRegime.On);
            ComboBoxRegime.Items.Add(BotTradeRegime.OnlyClosePosition);
            ComboBoxRegime.SelectedItem = _strategy.Regime;

            ComboBoxVolumeType1.Items.Add("Deposit percent");
            ComboBoxVolumeType1.Items.Add("Contracts");
            ComboBoxVolumeType1.Items.Add("Contract currency");
            ComboBoxVolumeType1.SelectedItem = _strategy.VolumeType1;

            ComboBoxVolumeType2.Items.Add("Deposit percent");
            ComboBoxVolumeType2.Items.Add("Contracts");
            ComboBoxVolumeType2.Items.Add("Contract currency");
            ComboBoxVolumeType2.SelectedItem = _strategy.VolumeType2;

            TextBoxCandleCount.Text = _strategy.CountCandles.ToString(culture);
            TextBoxDivergention.Text = _strategy.SpreadDeviation.ToString(culture);

            TextBoxLoss1.Text = _strategy.Loss.ToString(culture);
            TextBoxProfit1.Text = _strategy.Profit.ToString(culture);

            LabelRegime.Content = OsLocalization.Trader.Label115;
            ButtonAccept.Content = OsLocalization.Trader.Label17;
            LabelVolume1.Content = OsLocalization.Trader.Label30 + 1;
            LabelSlippage1.Content = OsLocalization.Trader.Label92 + 1;
            LabelVolume2.Content = OsLocalization.Trader.Label30 + 2;
            LabelSlippage2.Content = OsLocalization.Trader.Label92 + 2;
            LabelCandlesCount.Content = OsLocalization.Trader.Label143;
            LabelSpreadExpansion.Content = OsLocalization.Trader.Label144;
            LabelProfit.Content = OsLocalization.Trader.Label145;
            LabelLoss.Content = OsLocalization.Trader.Label146;
            LabelVolumeType1.Content = OsLocalization.Trader.Label554 + 1;
            LabelAssetInPortfolio1.Content = OsLocalization.Trader.Label555 + 1;
            LabelVolumeType2.Content = OsLocalization.Trader.Label554 + 2;
            LabelAssetInPortfolio2.Content = OsLocalization.Trader.Label555 + 2;

            this.Activate();
            this.Focus();
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                try
                {
                    if (TextBoxSlippage1.Text.ToDecimal() < 0 ||
                        TextBoxSlippage2.Text.ToDecimal() < 0 ||
                        TextBoxVolume1.Text.ToDecimal() < 0 ||
                        TextBoxVolume2.Text.ToDecimal() < 0 ||
                        Convert.ToInt32(TextBoxCandleCount.Text) < 0 ||
                        TextBoxDivergention.Text.ToDecimal() < 0)
                    {
                        throw new Exception();
                    }
                }
                catch (Exception)
                {
                    MessageBox.Show(OsLocalization.Trader.Label13);
                    return;
                }
                _strategy.VolumeType1 = Convert.ToString(ComboBoxVolumeType1.Text);
                _strategy.TradeAssetInPortfolio1 = Convert.ToString(TextBoxAssetInPortfolio1.Text);
                _strategy.VolumeType2 = Convert.ToString(ComboBoxVolumeType2.Text);
                _strategy.TradeAssetInPortfolio2 = Convert.ToString(TextBoxAssetInPortfolio2.Text);
                _strategy.Slippage1 = TextBoxSlippage1.Text.ToDecimal();
                _strategy.Slippage2 = TextBoxSlippage2.Text.ToDecimal();
                Enum.TryParse(ComboBoxRegime.Text, true, out _strategy.Regime);
                _strategy.CountCandles = Convert.ToInt32(TextBoxCandleCount.Text);

                _strategy.Volume2 = TextBoxVolume2.Text.ToDecimal();
                _strategy.Volume1 = TextBoxVolume1.Text.ToDecimal();

                _strategy.SpreadDeviation = TextBoxDivergention.Text.ToDecimal();

                _strategy.Loss = TextBoxLoss1.Text.ToDecimal();
                _strategy.Profit = TextBoxProfit1.Text.ToDecimal();

                _strategy.Save();
                Close();
            }
            catch(Exception error)
            {
                _strategy.SendNewLogMessage(error.ToString(), Logging.LogMessageType.Error);
            }
        }

        private void ButtonAbout_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show(OsLocalization.Trader.Label147);
        }
    }
}
