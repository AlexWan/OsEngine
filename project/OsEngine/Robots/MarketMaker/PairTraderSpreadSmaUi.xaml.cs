/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System;
using System.Globalization;
using System.Windows;
using OsEngine.Language;
using OsEngine.OsTrader.Panels;

namespace OsEngine.Robots.MarketMaker
{
    public partial class PairTraderSpreadSmaUi
    {
        private PairTraderSpreadSma _strategy;

         public PairTraderSpreadSmaUi(PairTraderSpreadSma strategy)
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

            LabelRegime.Content = OsLocalization.Trader.Label115;
            ButtonAccept.Content = OsLocalization.Trader.Label132;
            LabelVolume1.Content = OsLocalization.Trader.Label30 + 1;
            LabelSlippage1.Content = OsLocalization.Trader.Label92 + 1;
            LabelVolume1.Content = OsLocalization.Trader.Label30 + 2;
            LabelSlippage1.Content = OsLocalization.Trader.Label92 + 2;
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (Convert.ToDecimal(TextBoxSlipage1.Text) < 0 ||
                    Convert.ToDecimal(TextBoxSlipage2.Text) < 0 ||
                    Convert.ToDecimal(TextBoxVolume1.Text) < 0 ||
                    Convert.ToDecimal(TextBoxVolume2.Text) < 0)
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

            _strategy.Volume2 = Convert.ToDecimal(TextBoxVolume2.Text);
            _strategy.Volume1 = Convert.ToDecimal(TextBoxVolume1.Text);

            _strategy.Save();
            Close();
        }

        private void ButtonAbout_Click_1(object sender, RoutedEventArgs e)
        {
            MessageBox.Show(OsLocalization.Trader.Label148);
        }
    }
}
