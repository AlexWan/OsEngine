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
    public partial class RsiContrtrendUi
    {
        private RsiContrtrend _strategy;
        public RsiContrtrendUi(RsiContrtrend strategy)
        {
            InitializeComponent();
            _strategy = strategy;

            ComboBoxRegime.Items.Add(BotTradeRegime.Off);
            ComboBoxRegime.Items.Add(BotTradeRegime.On);
            ComboBoxRegime.Items.Add(BotTradeRegime.OnlyClosePosition);
            ComboBoxRegime.Items.Add(BotTradeRegime.OnlyLong);
            ComboBoxRegime.Items.Add(BotTradeRegime.OnlyShort);
            ComboBoxRegime.SelectedItem = _strategy.Regime;

            TextBoxSlipage.Text = _strategy.Slipage.ToString(new CultureInfo("ru-RU"));

            TextBoxVolumeOne.Text = _strategy.VolumeFix.ToString();
            RsiUp.Text = _strategy.Upline.Value.ToString(new CultureInfo("ru-RU"));
            RsiDown.Text = _strategy.Downline.Value.ToString(new CultureInfo("ru-RU"));

            LabelRegime.Content = OsLocalization.Trader.Label115;
            LabelVolume.Content = OsLocalization.Trader.Label30;
            LabelSlippage.Content = OsLocalization.Trader.Label92;
            ButtonAccept.Content = OsLocalization.Trader.Label17;
            LabelRsiOverbought.Content = OsLocalization.Trader.Label141;
            LabelRsiOversold.Content = OsLocalization.Trader.Label142;
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (Convert.ToDecimal(TextBoxVolumeOne.Text) <= 0 || 
                    Convert.ToInt32(RsiUp.Text) <= 0 || 
                    Convert.ToInt32(RsiDown.Text) <= 0 ||
                    Convert.ToDecimal(TextBoxSlipage.Text) < 0 ||
                    Convert.ToInt32(TextBoxVolumeOne.Text) <= 0)
                {
                    throw new Exception("");
                }
                
            }
            catch (Exception)
            {
                MessageBox.Show(OsLocalization.Trader.Label13);
                return;
            }

            Enum.TryParse(ComboBoxRegime.Text, true, out _strategy.Regime);
            _strategy.Slipage = Convert.ToDecimal(TextBoxSlipage.Text);
            _strategy.VolumeFix = Convert.ToDecimal(TextBoxVolumeOne.Text);
            _strategy.Upline.Value = Convert.ToDecimal(RsiUp.Text);
            _strategy.Downline.Value = Convert.ToDecimal(RsiDown.Text);

            _strategy.Upline.Refresh();
            _strategy.Downline.Refresh();

            _strategy.Save();
            Close();
        }
    }
}
