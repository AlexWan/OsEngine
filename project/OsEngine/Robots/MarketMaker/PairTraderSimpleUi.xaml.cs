using System;
using System.Globalization;
using System.Windows;
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
            _strategy = strategy;

            CultureInfo culture = new CultureInfo("ru-RU");

            TextBoxSlipage1.Text = _strategy.Slipage1.ToString(culture);
            TextBoxSlipage2.Text = _strategy.Slipage2.ToString(culture);

            TextBoxVolume1.Text = _strategy.Volume1.ToString(culture);
            TextBoxVolume2.Text = _strategy.Volume2.ToString(culture);

            ComboBoxRegime.Items.Add(BotTradeRegime.Off);
            ComboBoxRegime.Items.Add(BotTradeRegime.On);
            ComboBoxRegime.Items.Add(BotTradeRegime.OnlyClosePosition);
            ComboBoxRegime.SelectedItem = _strategy.Regime;

            TextBoxCandleCount.Text = _strategy.CountCandles.ToString(culture);
            TextBoxDivergention.Text = _strategy.SpreadDeviation.ToString(culture);

            TextBoxLoss1.Text = _strategy.Loss.ToString(culture);
            TextBoxProfit1.Text = _strategy.Profit.ToString(culture);

            LabelRegime.Content = OsLocalization.Trader.Label115;
            ButtonAccept.Content = OsLocalization.Trader.Label132;
            LabelVolume1.Content = OsLocalization.Trader.Label30 + 1;
            LabelSlippage1.Content = OsLocalization.Trader.Label92 + 1;
            LabelVolume1.Content = OsLocalization.Trader.Label30 + 2;
            LabelSlippage1.Content = OsLocalization.Trader.Label92 + 2;
            LabelCandlesCount.Content = OsLocalization.Trader.Label143;
            LabelSpreadExpansion.Content = OsLocalization.Trader.Label144;
            LabelProfit.Content = OsLocalization.Trader.Label145;
            LabelLoss.Content = OsLocalization.Trader.Label146;

        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (Convert.ToDecimal(TextBoxSlipage1.Text) < 0 ||
                    Convert.ToDecimal(TextBoxSlipage2.Text) < 0 ||
                    Convert.ToDecimal(TextBoxVolume1.Text) < 0 ||
                    Convert.ToDecimal(TextBoxVolume2.Text) < 0 ||
                    Convert.ToDecimal(TextBoxCandleCount.Text) < 0 ||
                    Convert.ToDecimal(TextBoxDivergention.Text) < 0)
                {
                    throw new Exception();
                }
            }
            catch (Exception)
            {
                MessageBox.Show(OsLocalization.Trader.Label13);
                return;
            }

            _strategy.Slipage1 = Convert.ToDecimal(TextBoxSlipage1.Text);
            _strategy.Slipage2 = Convert.ToDecimal(TextBoxSlipage2.Text);
            Enum.TryParse(ComboBoxRegime.Text, true, out _strategy.Regime);
            _strategy.CountCandles = Convert.ToInt32(TextBoxCandleCount.Text);

            _strategy.Volume2 = Convert.ToDecimal(TextBoxVolume2.Text);
            _strategy.Volume1 = Convert.ToDecimal(TextBoxVolume1.Text);

            _strategy.SpreadDeviation = Convert.ToDecimal(TextBoxDivergention.Text);

            _strategy.Loss = Convert.ToDecimal(TextBoxLoss1.Text);
            _strategy.Profit = Convert.ToDecimal(TextBoxProfit1.Text);

            _strategy.Save();
            Close();
        }

        private void ButtonAbout_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show(OsLocalization.Trader.Label147);
        }
    }
}
