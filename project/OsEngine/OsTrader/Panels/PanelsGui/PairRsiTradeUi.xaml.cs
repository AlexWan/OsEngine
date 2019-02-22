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
    public partial class PairRsiTradeUi
    {
        private PairRsiTrade _strategy;
        public PairRsiTradeUi(PairRsiTrade strategy)
        {
            InitializeComponent();
            _strategy = strategy;

            TextBoxVolumeOne.Text = _strategy.Volume1.ToString();
            TextBoxVolumeTwo.Text = _strategy.Volume2.ToString();

            Spread.Text = _strategy.RsiSpread.ToString(new CultureInfo("ru-RU"));

            LabelVolume1.Content = OsLocalization.Trader.Label30+1;
            LabelVolume2.Content = OsLocalization.Trader.Label30+2;
            LabelRsiDifference.Content = OsLocalization.Trader.Label138;
            ButtonAccept.Content = OsLocalization.Trader.Label132;
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (Convert.ToDecimal(TextBoxVolumeOne.Text) <= 0 ||
                    Convert.ToDecimal(TextBoxVolumeTwo.Text) <= 0 ||
                    Convert.ToInt32(Spread.Text) <=0
                    )
                {
                    throw new Exception("");
                }
                
            }
            catch (Exception)
            {
                MessageBox.Show(OsLocalization.Trader.Label13);
                return;
            }

            _strategy.Volume1 = Convert.ToDecimal(TextBoxVolumeOne.Text);
            _strategy.Volume2 = Convert.ToDecimal(TextBoxVolumeTwo.Text);
            _strategy.RsiSpread = Convert.ToInt32(Spread.Text);

            _strategy.Save();
            Close();
        }
    }
}
