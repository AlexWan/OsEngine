/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System;
using System.Globalization;
using System.Windows;
using OsEngine.Language;
using OsEngine.OsTrader.Panels;

namespace OsEngine.Robots.CounterTrend
{
    public partial class StrategyBollingerUi
    {
        private StrategyBollinger _strategy;

        public StrategyBollingerUi(StrategyBollinger strategy)
        {
            InitializeComponent();
            OsEngine.Layout.StickyBorders.Listen(this);
            OsEngine.Layout.StartupLocation.Start_MouseInCentre(this);
            _strategy = strategy;

            TextBoxAssetInPortfolio.Text = "Prime";
            TextBoxVolumeOne.Text = _strategy._volume.ToString();
            TextBoxSlippage.Text = _strategy._slippage.ToString(new CultureInfo("ru-RU"));

            ComboBoxRegime.Items.Add(BotTradeRegime.Off);
            ComboBoxRegime.Items.Add(BotTradeRegime.On);
            ComboBoxRegime.Items.Add(BotTradeRegime.OnlyClosePosition);
            ComboBoxRegime.Items.Add(BotTradeRegime.OnlyLong);
            ComboBoxRegime.Items.Add(BotTradeRegime.OnlyShort);
            ComboBoxRegime.SelectedItem = _strategy._regime;

            ComboBoxVolumeType.Items.Add("Deposit percent");
            ComboBoxVolumeType.Items.Add("Contracts");
            ComboBoxVolumeType.Items.Add("Contract currency");
            ComboBoxVolumeType.SelectedItem = _strategy._volumeType;

            LabelVolume.Content = OsLocalization.Trader.Label30;
            LabelRegime.Content = OsLocalization.Trader.Label115;
            LabelSlippage.Content = OsLocalization.Trader.Label92;
            ButtonAccept.Content = OsLocalization.Trader.Label17;
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

            _strategy._volumeType = Convert.ToString(ComboBoxVolumeType.Text);
            _strategy._tradeAssetInPortfolio = Convert.ToString(TextBoxAssetInPortfolio.Text);
            _strategy._volume = Convert.ToDecimal(TextBoxVolumeOne.Text);
            _strategy._slippage = Convert.ToDecimal(TextBoxSlippage.Text);

            Enum.TryParse(ComboBoxRegime.Text, true, out _strategy._regime);

            _strategy.Save();
            Close();
        }

        private void ButtonAbout_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show(OsLocalization.Trader.Label129);
        }
    }
}
