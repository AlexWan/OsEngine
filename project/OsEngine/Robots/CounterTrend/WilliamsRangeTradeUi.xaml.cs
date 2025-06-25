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
    public partial class WilliamsRangeTradeUi
    {
        private WilliamsRangeTrade _strategy;
        public WilliamsRangeTradeUi(WilliamsRangeTrade strategy)
        {
            InitializeComponent();
            OsEngine.Layout.StickyBorders.Listen(this);
            OsEngine.Layout.StartupLocation.Start_MouseInCentre(this);
            _strategy = strategy;

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

            TextBoxVolumeOne.Text = _strategy._volume.ToString();
            TextBoxSlippage.Text = _strategy._slippage.ToString(new CultureInfo("ru-RU"));
            TextBoxAssetInPortfolio.Text = "Prime";
            WillUp.Text = _strategy._upline.Value.ToString(new CultureInfo("ru-RU"));
            WillDown.Text = _strategy._downline.Value.ToString(new CultureInfo("ru-RU"));

            LabelRegime.Content = OsLocalization.Trader.Label115;
            LabelVolume.Content = OsLocalization.Trader.Label30;
            LabelSlippage.Content = OsLocalization.Trader.Label92;
            ButtonAccept.Content = OsLocalization.Trader.Label17;
            LabelUp.Content = OsLocalization.Trader.Label155;
            LabelLow.Content = OsLocalization.Trader.Label156;
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
                    Convert.ToDecimal(WillUp.Text) >= 0 ||
                    Convert.ToDecimal(WillDown.Text) >= 0 ||
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
            _strategy._upline.Value = Convert.ToDecimal(WillUp.Text);
            _strategy._downline.Value = Convert.ToDecimal(WillDown.Text);

            Enum.TryParse(ComboBoxRegime.Text, true, out _strategy._regime);

            _strategy._upline.Refresh();
            _strategy._downline.Refresh();

            _strategy.Save();
            Close();
        }
    }
}
