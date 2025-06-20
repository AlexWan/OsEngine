/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System;
using System.Globalization;
using System.Windows;
using OsEngine.Language;
using OsEngine.OsTrader.Panels;
using OsEngine.Entity;

namespace OsEngine.Robots.Trend
{
    public partial class PriceChannelTradeUi
    {
        private PriceChannelTrade _strategy;
        public PriceChannelTradeUi(PriceChannelTrade strategy)
        {
            InitializeComponent();
            OsEngine.Layout.StickyBorders.Listen(this);
            OsEngine.Layout.StartupLocation.Start_MouseInCentre(this);
            _strategy = strategy;

            TextBoxVolumeOne.Text = _strategy.Volume.ToString();
            TextBoxAssetInPortfolio.Text = "Prime";
            TextBoxSlippage.Text = _strategy.Slippage.ToString();

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

            LabelRegime.Content = OsLocalization.Trader.Label115;
            LabelVolume.Content = OsLocalization.Trader.Label30;
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

                if (TextBoxVolumeOne.Text.ToDecimal() <= 0 ||
                     TextBoxSlippage.Text.ToDecimal() < 0)
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
            _strategy.Volume = Convert.ToDecimal(TextBoxVolumeOne.Text);
            _strategy.Slippage = TextBoxSlippage.Text.ToDecimal();

            Enum.TryParse(ComboBoxRegime.Text, true, out _strategy.Regime);

            _strategy.Save();
            Close();
        }
    }
}
