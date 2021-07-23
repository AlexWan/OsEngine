using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using OsEngine.OsTrader;

namespace OsEngine.Robots.FoundBots
{
    /// <summary>
    /// Interaction logic for FreeStylikBreakoutUi.xaml
    /// </summary>
    public partial class FreeStylikBreakoutUi
    {
        private StrategyBreakout _strategy;

        public FreeStylikBreakoutUi(StrategyBreakout strategy)
        {
            InitializeComponent();
            _strategy = strategy;

            CultureInfo culture = new CultureInfo("ru-RU");

            CheckBoxIsOn.IsChecked = _strategy.IsOn;
            CheckBoxAletrIsOn.IsChecked = _strategy.AlertIsOn;

            TextBoxVolume.Text = _strategy.Volume.ToString();
            TextBoxSlipageCloseSecond.Text = _strategy.SlipageCloseSecond.ToString(culture);
            TextBoxSlipageOpenSecond.Text = _strategy.SlipageOpenSecond.ToString(culture);

            TextBoxTimeFrom.Text = _strategy.TimeFrom.ToString();
            TextBoxTimeTo.Text = _strategy.TimeTo.ToString();
            TextBoxSlipageCloseFirst.Text = _strategy.SlipageCloseFirst.ToString(culture);
            TextBoxSlipageOpenFirst.Text = _strategy.SlipageOpenFirst.ToString(culture);
            TextBoxLagPunctToOpenClose.Text = _strategy.LagPunctToOpenClose.ToString(culture);
            TextBoxLagTimeToOpenClose.Text = _strategy.LagTimeToOpenClose.TotalSeconds.ToString(culture);

            TextBoxSlipageSecondClose.Text = _strategy.SlipageReversClose.ToString(culture);
            TextBoxSlipageSecondOpen.Text = _strategy.SlipageReversOpen.ToString(culture);

            TextBoxAdxHigh.Text = _strategy.AdxHigh.ToString(culture);
            TextBoxLookback.Text = _strategy.Lookback.ToString(culture);
            TextBoxTrailBars.Text = _strategy.TrailBars.ToString(culture);

            TextBoxSlipageToAlert.Text = _strategy.SlipageToAlert.ToString();
            CheckBoxEmulatorIsOn.IsChecked = _strategy.EmulatorIsOn;

            CheckBoxPaintEmulator.IsChecked = _strategy.NeadToPaintEmu;

        }

        private void Button_Click_1(object sender, RoutedEventArgs e)
        {
            try
            {
                if (Convert.ToDecimal(TextBoxTimeFrom.Text) < 0 ||
                    Convert.ToDecimal(TextBoxTimeTo.Text) < 0 ||
                    Convert.ToDecimal(TextBoxSlipageCloseSecond.Text) < 0 ||
                    Convert.ToDecimal(TextBoxSlipageOpenSecond.Text) < 0 ||
                    Convert.ToDecimal(TextBoxVolume.Text) <= 0 ||
                    Convert.ToInt32(TextBoxLagTimeToOpenClose.Text) < 5 ||
                    Convert.ToDecimal(TextBoxLagPunctToOpenClose.Text) <= 0 ||
                    Convert.ToInt32(TextBoxSlipageSecondClose.Text) < 0 ||
                    Convert.ToInt32(TextBoxSlipageSecondOpen.Text) < 0 ||
                    Convert.ToDecimal(TextBoxAdxHigh.Text) <= 0 ||
                    Convert.ToDecimal(TextBoxLookback.Text) <= 0 ||
                    Convert.ToDecimal(TextBoxTrailBars.Text) <= 0 ||
                    Convert.ToInt32(TextBoxSlipageToAlert.Text) < 0 ||
                    Convert.ToInt32(TextBoxSlipageCloseFirst.Text) < 0 ||
                    Convert.ToInt32(TextBoxSlipageOpenFirst.Text) < 0)
                {
                    throw new Exception();
                }
            }
            catch (Exception)
            {
                MessageBox.Show("Операция прервана, т.к. в одном из полей недопустимое значение.");
                return;
            }
            _strategy.Volume = Convert.ToDecimal(TextBoxVolume.Text);

            if (CheckBoxIsOn.IsChecked.HasValue)
            {
                _strategy.IsOn = CheckBoxIsOn.IsChecked.Value;
            }

            if (CheckBoxAletrIsOn.IsChecked.HasValue)
            {
                _strategy.AlertIsOn = CheckBoxAletrIsOn.IsChecked.Value;
            }

            _strategy.SlipageCloseSecond = Convert.ToInt32(TextBoxSlipageCloseSecond.Text);
            _strategy.SlipageOpenSecond = Convert.ToInt32(TextBoxSlipageOpenSecond.Text);
            _strategy.TimeFrom = Convert.ToInt32(TextBoxTimeFrom.Text);
            _strategy.TimeTo = Convert.ToInt32(TextBoxTimeTo.Text);
            _strategy.LagPunctToOpenClose = Convert.ToDecimal(TextBoxLagPunctToOpenClose.Text);
            _strategy.LagTimeToOpenClose = new TimeSpan(0, 0, 0, Convert.ToInt32(TextBoxLagTimeToOpenClose.Text));
            _strategy.SlipageCloseFirst = Convert.ToInt32(TextBoxSlipageCloseFirst.Text);
            _strategy.SlipageOpenFirst = Convert.ToInt32(TextBoxSlipageOpenFirst.Text);
            _strategy.SlipageReversClose = Convert.ToInt32(TextBoxSlipageSecondClose.Text);
            _strategy.SlipageReversOpen = Convert.ToInt32(TextBoxSlipageSecondOpen.Text);

            _strategy.AdxHigh = Convert.ToInt32(TextBoxAdxHigh.Text);
            _strategy.Lookback = Convert.ToInt32(TextBoxLookback.Text);
            _strategy.TrailBars = Convert.ToInt32(TextBoxTrailBars.Text);

            _strategy.SlipageToAlert = Convert.ToInt32(TextBoxSlipageToAlert.Text);

            if (CheckBoxEmulatorIsOn.IsChecked.HasValue)
            {
                _strategy.EmulatorIsOn = CheckBoxEmulatorIsOn.IsChecked.Value;
            }
            if (CheckBoxPaintEmulator.IsChecked.HasValue)
            {
                _strategy.NeadToPaintEmu = CheckBoxPaintEmulator.IsChecked.Value;
            }
            _strategy.Save();
            Close();
        }
    }
}
