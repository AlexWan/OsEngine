/*
 *Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System;
using System.Globalization;
using System.Windows;

namespace OsEngine.OsTrader.Panels.PanelsGui
{
    /// <summary>
    /// Логика взаимодействия для CCITrade.xaml
    /// </summary>
    public partial class CciTradeUi
    {
        private CciTrade _strategy;
        public CciTradeUi(CciTrade strategy)
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

            CciUp.Text = _strategy.Upline.Value.ToString(new CultureInfo("ru-RU"));
            CciDown.Text = _strategy.Downline.Value.ToString(new CultureInfo("ru-RU"));

        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            try
            {

                if (Convert.ToDecimal(TextBoxVolumeOne.Text) <= 0 ||
                    Convert.ToInt32(CciUp.Text) <= 0 ||
                    Convert.ToInt32(CciDown.Text) >= 0)
                {
                    throw new Exception("");
                }
                Convert.ToDecimal(TextBoxSlipage.Text);
            }
            catch (Exception)
            {
                MessageBox.Show("В одном из полей недопустимые значения. Процесс сохранения прерван");
                return;
            }

            _strategy.VolumeFix = Convert.ToDecimal(TextBoxVolumeOne.Text);
            _strategy.Slipage = Convert.ToDecimal(TextBoxSlipage.Text);


            _strategy.Upline.Value = Convert.ToDecimal(CciUp.Text);
            _strategy.Downline.Value = Convert.ToDecimal(CciDown.Text);

            Enum.TryParse(ComboBoxRegime.Text, true, out _strategy.Regime);

            _strategy.Upline.Refresh();
            _strategy.Downline.Refresh();

            _strategy.Save();
            Close();
        }
    }
}
