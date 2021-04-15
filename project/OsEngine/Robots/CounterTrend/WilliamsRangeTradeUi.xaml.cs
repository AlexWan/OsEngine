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
            _strategy = strategy;

            TextBoxVolumeOne.Text = _strategy.VolumeFix.ToString();

            TextBoxSlipage.Text = _strategy.Slipage.ToString(new CultureInfo("ru-RU"));

            ComboBoxRegime.Items.Add(BotTradeRegime.Off);
            ComboBoxRegime.Items.Add(BotTradeRegime.On);
            ComboBoxRegime.Items.Add(BotTradeRegime.OnlyClosePosition);
            ComboBoxRegime.Items.Add(BotTradeRegime.OnlyLong);
            ComboBoxRegime.Items.Add(BotTradeRegime.OnlyShort);
            ComboBoxRegime.SelectedItem = _strategy.Regime;

            WillUp.Text = _strategy.Upline.Value.ToString(new CultureInfo("ru-RU"));
            WillDown.Text = _strategy.Downline.Value.ToString(new CultureInfo("ru-RU"));

            LabelRegime.Content = OsLocalization.Trader.Label115;
            LabelVolume.Content = OsLocalization.Trader.Label30;
            LabelSlippage.Content = OsLocalization.Trader.Label92;
            ButtonAccept.Content = OsLocalization.Trader.Label17;
            LabelUp.Content = OsLocalization.Trader.Label155;
            LabelLow.Content = OsLocalization.Trader.Label156;
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (Convert.ToDecimal(TextBoxVolumeOne.Text) <= 0 ||
                    Convert.ToDecimal(WillUp.Text) >= 0 ||
                    Convert.ToDecimal(WillDown.Text) >= 0 ||
                    Convert.ToDecimal(TextBoxSlipage.Text) < 0)
                {
                    throw new Exception("");
                }
            }
            catch (Exception)
            {
                MessageBox.Show(OsLocalization.Trader.Label13);
                return;
            }

            _strategy.VolumeFix = Convert.ToDecimal(TextBoxVolumeOne.Text);
            _strategy.Slipage = Convert.ToDecimal(TextBoxSlipage.Text);
            _strategy.Upline.Value = Convert.ToDecimal(WillUp.Text);
            _strategy.Downline.Value = Convert.ToDecimal(WillDown.Text);

            Enum.TryParse(ComboBoxRegime.Text, true, out _strategy.Regime);

            _strategy.Upline.Refresh();
            _strategy.Downline.Refresh();

            _strategy.Save();
            Close();
        }
    }
}
