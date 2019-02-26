/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System;
using System.Globalization;
using System.Windows;
using OsEngine.Language;
using OsEngine.OsTrader.Panels;

namespace OsEngine.Robots.Patterns
{

    public partial class ThreeSoldierUi
    {
        private ThreeSoldier _strategy;
        public ThreeSoldierUi(ThreeSoldier strategy)
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
            TextBoxVolumeOne.Text = _strategy.VolumeFix.ToString(new CultureInfo("ru-RU"));
            TextBoxheightSoldiers.Text = _strategy.HeightSoldiers.ToString(new CultureInfo("ru-RU"));
            TextBoxminHeightSoldier.Text = _strategy.MinHeightSoldier.ToString(new CultureInfo("ru-RU"));
            TextBoxprocHeightTake.Text = _strategy.ProcHeightTake.ToString(new CultureInfo("ru-RU"));
            TextBoxprocHeightStop.Text = _strategy.ProcHeightStop.ToString(new CultureInfo("ru-RU"));

            LabelRegime.Content = OsLocalization.Trader.Label115;
            LabelVolume.Content = OsLocalization.Trader.Label30;
            LabelSlippage.Content = OsLocalization.Trader.Label92;
            ButtonAccept.Content = OsLocalization.Trader.Label132;
            LabelPatternHeight.Content = OsLocalization.Trader.Label152;
            LabelCandleHeigth.Content = OsLocalization.Trader.Label153;
            LabelProfit.Content = OsLocalization.Trader.Label87 + " %";
            LabelStop.Content = OsLocalization.Trader.Label86 + " %";

        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            try
            {

                if (Convert.ToDecimal(TextBoxVolumeOne.Text) <= 0 ||
                    Convert.ToDecimal(TextBoxSlipage.Text) <= 0 ||
                    Convert.ToInt32(TextBoxheightSoldiers.Text) <= 0 ||
                    Convert.ToInt32(TextBoxprocHeightTake.Text) <= 0 ||
                    Convert.ToInt32(TextBoxprocHeightStop.Text) <= 0)
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
            _strategy.HeightSoldiers = Convert.ToInt32(TextBoxheightSoldiers.Text);
            _strategy.MinHeightSoldier = Convert.ToInt32(TextBoxminHeightSoldier.Text);
            _strategy.ProcHeightTake = Convert.ToInt32(TextBoxprocHeightTake.Text);
            _strategy.ProcHeightStop = Convert.ToInt32(TextBoxprocHeightStop.Text);

            _strategy.Save();
            Close();
        }
    }
}