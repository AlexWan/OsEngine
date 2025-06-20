/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System;
using System.Globalization;
using System.Windows;
using OsEngine.Language;
using OsEngine.OsTrader.Panels;

namespace OsEngine.Robots.Trend
{
    public partial class SmaStochasticUi
    {
        private SmaStochastic _strategy;
        public SmaStochasticUi(SmaStochastic strategy)
        {
            InitializeComponent();
            OsEngine.Layout.StickyBorders.Listen(this);
            OsEngine.Layout.StartupLocation.Start_MouseInCentre(this);
            _strategy = strategy;

            TextBoxVolumeOne.Text = _strategy.Volume.ToString();
            TextBoxAssetInPortfolio.Text = "Prime";

            TextBoxSlippage.Text = _strategy.Slippage.ToString(new CultureInfo("ru-RU"));
            ComboBoxRegime.Items.Add(BotTradeRegime.Off);
            ComboBoxRegime.Items.Add(BotTradeRegime.On);
            ComboBoxRegime.Items.Add(BotTradeRegime.OnlyClosePosition);
            ComboBoxRegime.Items.Add(BotTradeRegime.OnlyLong);
            ComboBoxRegime.Items.Add(BotTradeRegime.OnlyShort);
            ComboBoxRegime.SelectedItem = _strategy.Regime;

            ComboBoxVolumeType.Items.Add("Deposit percent");
            ComboBoxVolumeType.Items.Add("Contracts");
            ComboBoxVolumeType.Items.Add("Contract currency");
            ComboBoxVolumeType.SelectedItem = _strategy.VolumeType;

            StochUp.Text = _strategy.Upline.ToString(new CultureInfo("ru-RU"));
            StochDown.Text = _strategy.Downline.ToString(new CultureInfo("ru-RU"));
            Step.Text = _strategy.Step.ToString(new CultureInfo("ru-RU"));

            LabelRegime.Content = OsLocalization.Trader.Label115;
            LabelVolume.Content = OsLocalization.Trader.Label30;
            LabelSlippage.Content = OsLocalization.Trader.Label92;
            ButtonAccept.Content = OsLocalization.Trader.Label17;
            LabelStohasticUp.Content = OsLocalization.Trader.Label149;
            LabelStochasticLow.Content = OsLocalization.Trader.Label150;
            LabelStep.Content = OsLocalization.Trader.Label151;
            LabelVolumeType.Content = OsLocalization.Trader.Label554;
            LabelAssetInPortfolio.Content = OsLocalization.Trader.Label555;

            this.Activate();
            this.Focus();
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            try
            {

                if (Convert.ToDecimal(TextBoxVolumeOne.Text) <= 0 ||
                    Convert.ToInt32(StochUp.Text) <= 0 ||
                    Convert.ToInt32(StochDown.Text) <= 0 ||
                    Convert.ToInt32(Step.Text) <= 0 ||
                     Convert.ToDecimal(TextBoxSlippage.Text) < 0)
                {
                    throw new Exception("");
                }
            }
            catch (Exception)
            {
                MessageBox.Show(OsLocalization.Trader.Label13);
                return;
            }

            _strategy.VolumeType = Convert.ToString(ComboBoxVolumeType.Text);
            _strategy.TradeAssetInPortfolio = Convert.ToString(TextBoxAssetInPortfolio.Text);
            _strategy.Slippage = Convert.ToDecimal(TextBoxSlippage.Text);
            _strategy.Volume = Convert.ToDecimal(TextBoxVolumeOne.Text);
            _strategy.Upline = Convert.ToDecimal(StochUp.Text);
            _strategy.Downline = Convert.ToDecimal(StochDown.Text);
            _strategy.Step = Convert.ToDecimal(Step.Text);

            Enum.TryParse(ComboBoxRegime.Text, true, out _strategy.Regime);

            _strategy.Save();
            Close();
        }
    }
}
