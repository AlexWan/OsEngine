/*
 *Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System;
using System.Globalization;
using System.Windows;

namespace OsEngine.OsTrader.Panels.PanelsGui
{
    /// <summary>
    /// Логика взаимодействия для TwoLegArbitration.xaml
    /// </summary>
    public partial class TwoLegArbitrationUi
    {
        private TwoLegArbitration _strategy;

        public TwoLegArbitrationUi(TwoLegArbitration strategy)
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
            TextBoxupline.Text = _strategy.Upline.ToString(new CultureInfo("ru-RU"));
            TextBoxdownline.Text = _strategy.Downline.ToString(new CultureInfo("ru-RU"));

        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            try
            {

                if (Convert.ToInt32(TextBoxVolumeOne.Text) <= 0 ||
                    Convert.ToInt32(TextBoxVolumeOne.Text) <= 0 ||
                    Convert.ToInt32(TextBoxupline.Text) <= 0 ||
                    Convert.ToInt32(TextBoxdownline.Text) <= 0)
                {
                    throw new Exception("");
                }
            }
            catch (Exception)
            {
                MessageBox.Show("В одном из полей недопустимые значения. Процесс сохранения прерван");
                return;
            }

            Enum.TryParse(ComboBoxRegime.Text, true, out _strategy.Regime);
            _strategy.Slipage = Convert.ToDecimal(TextBoxSlipage.Text);
            _strategy.VolumeFix = Convert.ToInt32(TextBoxVolumeOne.Text);
            _strategy.Upline = Convert.ToInt32(TextBoxupline.Text);
            _strategy.Downline = Convert.ToInt32(TextBoxdownline.Text);

            _strategy.Save();
            Close();
        }
    }
}

