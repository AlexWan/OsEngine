/*
 *Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System;
using System.Globalization;
using System.Windows;

namespace OsEngine.OsTrader.Panels.PanelsGui
{
    /// <summary>
    /// Логика взаимодействия для LevermorUi.xaml
    /// </summary>
    public partial class LevermorUi 
    {
        private StrategyLevermor _strategy;

        public LevermorUi(StrategyLevermor strategy)
        {
            InitializeComponent();
            _strategy = strategy;

            CultureInfo culture = new CultureInfo("ru-RU");

            TextBoxSlipage.Text = _strategy.Slipage.ToString(culture);

            ComboBoxRegime.Items.Add(BotTradeRegime.Off);
            ComboBoxRegime.Items.Add(BotTradeRegime.On);
            ComboBoxRegime.Items.Add(BotTradeRegime.OnlyClosePosition);
            ComboBoxRegime.Items.Add(BotTradeRegime.OnlyLong);
            ComboBoxRegime.Items.Add(BotTradeRegime.OnlyShort);
            ComboBoxRegime.SelectedItem = _strategy.Regime;


            TextBoxStopLong.Text = _strategy.LongStop.ToString(culture);
            TextBoxStopShort.Text = _strategy.ShortStop.ToString(culture);
            TextBoxMaximumPositions.Text = _strategy.MaximumPosition.ToString();
            TextBoxBuyAgainPersent.Text = _strategy.PersentDopBuy.ToString(culture);
            TextBoxSellAganePersent.Text = _strategy.PersentDopSell.ToString(culture);
            TextBoxVolume.Text = _strategy.Volume.ToString();
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (Convert.ToDecimal(TextBoxStopLong.Text) < 0 ||
                    Convert.ToDecimal(TextBoxStopShort.Text) < 0 ||
                    Convert.ToDecimal(TextBoxMaximumPositions.Text) < 0 ||
                    Convert.ToDecimal(TextBoxBuyAgainPersent.Text) < 0 ||
                    Convert.ToDecimal(TextBoxSellAganePersent.Text) < 0 ||
                    Convert.ToDecimal(TextBoxSlipage.Text) < 0 ||
                    Convert.ToInt32(TextBoxVolume.Text) <= 0)
                {
                    throw new Exception();
                }
            }
            catch (Exception)
            {
                MessageBox.Show("Операция прервана, т.к. в одном из полей недопустимое значение.");
                return;
            }

            _strategy.Slipage = Convert.ToDecimal(TextBoxSlipage.Text);
            Enum.TryParse(ComboBoxRegime.Text, true, out _strategy.Regime);


            _strategy.MaximumPosition = Convert.ToInt32(TextBoxMaximumPositions.Text);
            _strategy.PersentDopBuy = Convert.ToDecimal(TextBoxBuyAgainPersent.Text);
            _strategy.PersentDopSell = Convert.ToDecimal(TextBoxSellAganePersent.Text);

            _strategy.LongStop = Convert.ToDecimal(TextBoxStopLong.Text);
            _strategy.ShortStop = Convert.ToDecimal(TextBoxStopShort.Text);
            _strategy.Volume = Convert.ToInt32(TextBoxVolume.Text);
            _strategy.Save();
            Close();



        }

        private void ButtonAbout_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show(
                "Трендовая стратегия описанная в книге Эдвина Лафевра: Воспоминания биржевого спекулянта. Подробнее: http://o-s-a.net/posts/34-bad-quant-edvin-lefevr-vospominanija-birzhevogo-spekuljanta.html");
        }
    }
}
