/*
 *Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System;
using System.Windows;

namespace OsEngine.OsTrader.Panels.SingleRobots
{
    public partial class MarketMakerBotUi
    {
        private MarketMakerBot _strategy;

        public MarketMakerBotUi(MarketMakerBot strategy)
        {
            InitializeComponent();
            _strategy = strategy;

            ComboBoxRegime.Items.Add(BotTradeRegime.Off);
            ComboBoxRegime.Items.Add(BotTradeRegime.On);
            ComboBoxRegime.Items.Add(BotTradeRegime.OnlyClosePosition);
            ComboBoxRegime.Items.Add(BotTradeRegime.OnlyLong);
            ComboBoxRegime.Items.Add(BotTradeRegime.OnlyShort);
            ComboBoxRegime.SelectedItem = _strategy.Regime;
            TextBoxVolumeOne.Text = _strategy.Volume.ToString();
            TextBoxSpreadBeetwenLine.Text = _strategy.PersentToSpreadLines.ToString();

            CheckBoxNeadToPaint.IsChecked = _strategy.PaintOn;
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (Convert.ToDecimal(TextBoxVolumeOne.Text) <= 0||
                    Convert.ToDecimal(TextBoxSpreadBeetwenLine.Text) <= 0)
                {
                    throw new Exception("");
                }
            }
            catch (Exception)
            {
                MessageBox.Show("В одном из полей недопустимые значения. Процесс сохранения прерван");
                return;
            }

            _strategy.Volume = Convert.ToDecimal(TextBoxVolumeOne.Text);
            _strategy.PersentToSpreadLines = Convert.ToDecimal(TextBoxSpreadBeetwenLine.Text);
            Enum.TryParse(ComboBoxRegime.Text, true, out _strategy.Regime);

            if (CheckBoxNeadToPaint.IsChecked.HasValue)
            {
                _strategy.PaintOn = CheckBoxNeadToPaint.IsChecked.Value;
            }

            _strategy.Save();
            Close();
        }

        private void ButtonAbout_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("В пятницу все позиции закрываются. Каждый понедельник строются 20 уровней вверх и 20 уровней вниз. " 
                +"Через расстояние указываемое в настройках. При пересечении этих уровней в противоположную сторону открывается позиция   http://y-dav.livejournal.com/8877.html");
        }
    }
}
