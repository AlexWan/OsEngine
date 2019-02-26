/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System;
using System.Globalization;
using System.Windows;
using OsEngine.Language;

namespace OsEngine.OsTrader.Panels.PanelsGui
{
    public partial class PriceChannelVolatilityUi
    {
        private PriceChannelVolatility _strategy;
        public PriceChannelVolatilityUi(PriceChannelVolatility strategy)
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

            TextBoxVolumeOne.Text = _strategy.VolumeFix1.ToString(new CultureInfo("ru-RU"));
            TextBoxVolumeTwo.Text = _strategy.VolumeFix2.ToString(new CultureInfo("ru-RU"));
            TextBoxLengthAtr.Text = _strategy.LengthAtr.ToString(new CultureInfo("ru-RU"));
            TextBoxKofAtr.Text = _strategy.KofAtr.ToString(new CultureInfo("ru-RU"));
            TextBoxPcUp.Text = _strategy.LengthUp.ToString(new CultureInfo("ru-RU"));
            TextBoxPcDown.Text = _strategy.LengthDown.ToString(new CultureInfo("ru-RU"));

            LabelRegime.Content = OsLocalization.Trader.Label115;
            LabelSlippage.Content = OsLocalization.Trader.Label92;
            ButtonAccept.Content = OsLocalization.Trader.Label132;
            LabelVolume1.Content = OsLocalization.Trader.Label30 + 1;
            LabelVolume2.Content = OsLocalization.Trader.Label30 + 2;
            LabelAtrPeriod.Content = OsLocalization.Trader.Label139;
            LabelAtrCoefficient.Content = OsLocalization.Trader.Label140;
            
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            try
            {

                if (Convert.ToDecimal(TextBoxVolumeOne.Text) <= 0 ||
                    Convert.ToDecimal(TextBoxVolumeTwo.Text) <= 0 ||
                    Convert.ToDecimal(TextBoxKofAtr.Text) < 0 ||
                    Convert.ToInt32(TextBoxLengthAtr.Text) <0 ||
                     Convert.ToInt32(TextBoxPcUp.Text) < 0 ||
                    Convert.ToInt32(TextBoxPcDown.Text) < 0)
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
            _strategy.VolumeFix1 = Convert.ToDecimal(TextBoxVolumeOne.Text);
            _strategy.VolumeFix2 = Convert.ToDecimal(TextBoxVolumeTwo.Text);
            _strategy.LengthAtr = Convert.ToInt32(TextBoxLengthAtr.Text);
            _strategy.KofAtr = Convert.ToDecimal(TextBoxKofAtr.Text);
            _strategy.LengthUp = Convert.ToInt32(TextBoxPcUp.Text);
            _strategy.LengthDown = Convert.ToInt32(TextBoxPcDown.Text);
            _strategy.Save();
            Close();
        }
    }
}