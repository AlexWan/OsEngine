/*
 *Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System;
using System.Globalization;
using System.Windows;

namespace OsEngine.OsTrader.Panels.PanelsGui
{
    /// <summary>
    /// Логика взаимодействия для WilliamsUi.xaml
    /// </summary>
    public partial class WilliamsUi
    {
        private StrategyBillWilliams _strategy;

        public WilliamsUi(StrategyBillWilliams strategy)
        {
            InitializeComponent();
            _strategy = strategy;

            TextBoxVolumeOne.Text = _strategy.VolumeFirst.ToString();
            TextBoxVolumeTwo.Text = _strategy.VolumeSecond.ToString();

            TextBoxSlipage.Text = _strategy.Slipage.ToString(new CultureInfo("ru-RU"));
            TextBoxMaxPositions.Text = _strategy.MaximumPositions.ToString();

            ComboBoxRegime.Items.Add(BotTradeRegime.Off);
            ComboBoxRegime.Items.Add(BotTradeRegime.On);
            ComboBoxRegime.Items.Add(BotTradeRegime.OnlyClosePosition);
            ComboBoxRegime.Items.Add(BotTradeRegime.OnlyLong);
            ComboBoxRegime.Items.Add(BotTradeRegime.OnlyShort);
            ComboBoxRegime.SelectedItem = _strategy.Regime;
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            try
            {

                if (Convert.ToInt32(TextBoxVolumeOne.Text) <= 0 ||
                    Convert.ToInt32(TextBoxVolumeTwo.Text) <= 0 ||
                    Convert.ToInt32(TextBoxMaxPositions.Text) <= 0 ||
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

            _strategy.VolumeFirst = Convert.ToInt32(TextBoxVolumeOne.Text);
            _strategy.VolumeSecond = Convert.ToInt32(TextBoxVolumeTwo.Text);
            _strategy.Slipage = Convert.ToDecimal(TextBoxSlipage.Text);
            _strategy.MaximumPositions = Convert.ToInt32(TextBoxMaxPositions.Text);

            Enum.TryParse(ComboBoxRegime.Text, true, out _strategy.Regime);

            _strategy.Save();
            Close();
        }

        private void ButtonAbout_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show(
                "Стратегия Билла Вильямса из книги Новые измерения. подробрее здесь: http://o-s-a.net/posts/29-bad-quant-bill-viljams-novye-izmerenija-v-birzhevoi-torgovle.html");
        }
    }
}