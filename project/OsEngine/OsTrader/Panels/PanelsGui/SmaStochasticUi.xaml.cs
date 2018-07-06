/*
 *Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System;
using System.Globalization;
using System.Windows;

namespace OsEngine.OsTrader.Panels.PanelsGui
{
    /// <summary>
    /// Логика взаимодействия для SmaStochasticUi.xaml
    /// </summary>
    public partial class SmaStochasticUi
    {
        private SmaStochastic _strategy;
        public SmaStochasticUi(SmaStochastic strategy)
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

            StochUp.Text = _strategy.Upline.ToString(new CultureInfo("ru-RU"));
            StochDown.Text = _strategy.Downline.ToString(new CultureInfo("ru-RU"));
            Step.Text = _strategy.Step.ToString(new CultureInfo("ru-RU"));


        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            try
            {

                if (Convert.ToDecimal(TextBoxVolumeOne.Text) <= 0 ||
                    Convert.ToInt32(StochUp.Text) <= 0 ||
                    Convert.ToInt32(StochDown.Text) <= 0 ||
                    Convert.ToInt32(Step.Text) <= 0 ||
                     Convert.ToDecimal(TextBoxSlipage.Text) < 0)
                {
                    throw new Exception("");
                }
            }
            catch (Exception)
            {
                MessageBox.Show("В одном из полей недопустимые значения. Процесс сохранения прерван");
                return;
            }

            _strategy.VolumeFix = Convert.ToDecimal(TextBoxVolumeOne.Text);
            _strategy.Slipage = Convert.ToDecimal(TextBoxSlipage.Text);

            _strategy.Upline = Convert.ToDecimal(StochUp.Text);
            _strategy.Downline = Convert.ToDecimal(StochDown.Text);
            _strategy.Step = Convert.ToDecimal(Step.Text);

            Enum.TryParse(ComboBoxRegime.Text, true, out _strategy.Regime);



            _strategy.Save();
            Close();
        }
    }
}
