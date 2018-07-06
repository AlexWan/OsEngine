/*
 *Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/
using System;
using System.Globalization;
using System.Windows;

namespace OsEngine.OsTrader.Panels.PanelsGui
{
    /// <summary>
    /// Логика взаимодействия для PairTraderSpreadSmaUi.xaml
    /// </summary>
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
                MessageBox.Show("Операция прервана, т.к. в одном из полей недопустимое значение.");
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
            string str = "";
            str += "Робот смотрит на график спреда между инструментами. На нём есть короткая и длинная машка. ";
            str += "Когда короткая пересекает длинную это служит сигналом для входа в позицию. Закрытие тоже по пробою";
            MessageBox.Show(str);
        }
    }
}
