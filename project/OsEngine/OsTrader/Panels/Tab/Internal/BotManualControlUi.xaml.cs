/*
 *Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/
using System;
using System.Globalization;
using System.Windows;
using OsEngine.Entity;

namespace OsEngine.OsTrader.Panels.Tab.Internal
{
    /// <summary>
    /// Логика взаимодействия для StrategOneSecurityManualControlDialog.xaml
    /// </summary>
    public partial class BotManualControlUi
    {

        /// <summary>
        /// настройки стратегии
        /// </summary>
        private BotManualControl _strategySettings;

        /// <summary>
        /// конструктор
        /// </summary>
        /// <param name="strategySettings">настройки которые будем настраивать</param>
        public BotManualControlUi(BotManualControl strategySettings)
        {
            InitializeComponent();

            try
            {
                _strategySettings = strategySettings;

                // стоп

                CheckBoxStopIsOn.IsChecked = _strategySettings.StopIsOn;
                TextBoxStopPercentLenght.Text = _strategySettings.StopDistance.ToString(new CultureInfo("ru-RU"));
                TextBoxSlipageStop.Text = _strategySettings.StopSlipage.ToString(new CultureInfo("ru-RU"));

                // профит

                CheckBoxProfitIsOn.IsChecked = _strategySettings.ProfitIsOn;
                TextBoxProfitPercentLenght.Text = _strategySettings.ProfitDistance.ToString(new CultureInfo("ru-RU"));
                TextBoxSlipageProfit.Text = _strategySettings.ProfitSlipage.ToString(new CultureInfo("ru-RU"));

                // закрытие позиции
                CheckBoxSecondToCloseIsOn.IsChecked = _strategySettings.SecondToCloseIsOn;
                TextBoxSecondToClose.Text = _strategySettings.SecondToClose.TotalSeconds.ToString(new CultureInfo("ru-RU"));

                CheckBoxSetbackToCloseIsOn.IsChecked = _strategySettings.SetbackToCloseIsOn;
                TextBoxSetbackToClose.Text = _strategySettings.SetbackToClosePosition.ToString();

                CheckBoxDoubleExitIsOnIsOn.IsChecked = _strategySettings.DoubleExitIsOn;
                ComboBoxTypeDoubleExitOrder.Items.Add(OrderPriceType.Limit);
                ComboBoxTypeDoubleExitOrder.Items.Add(OrderPriceType.Market);
                ComboBoxTypeDoubleExitOrder.SelectedItem = _strategySettings.TypeDoubleExitOrder;
                TextBoxSlipageDoubleExit.Text = _strategySettings.DoubleExitSlipage.ToString();

                // открытие позиции

                CheckBoxSecondToOpenIsOn.IsChecked = _strategySettings.SecondToOpenIsOn;
                TextBoxSecondToOpen.Text = _strategySettings.SecondToOpen.TotalSeconds.ToString(new CultureInfo("ru-RU"));

                CheckBoxSetbackToOpenIsOn.IsChecked = _strategySettings.SetbackToOpenIsOn;
                TextBoxSetbackToOpen.Text = _strategySettings.SetbackToOpenPosition.ToString();

            }
            catch (Exception error)
            {
                MessageBox.Show(error.ToString());
            }
        }

        /// <summary>
        /// кнопка принять
        /// </summary>
        private void ButtonAccept_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if(Convert.ToInt32(TextBoxSecondToOpen.Text) <= 0 ||
                    Convert.ToInt32(TextBoxSecondToClose.Text) <= 0 ||
                    Convert.ToInt32(TextBoxStopPercentLenght.Text) <= 0 ||
                    Convert.ToInt32(TextBoxSlipageStop.Text) <= 0 ||
                    Convert.ToInt32(TextBoxProfitPercentLenght.Text) <= 0 ||
                    Convert.ToInt32(TextBoxSlipageProfit.Text) <= 0 ||
                    Convert.ToInt32(TextBoxSetbackToClose.Text) <= 0 ||
                    Convert.ToInt32(TextBoxSecondToOpen.Text) <= 0 ||
                    Convert.ToInt32(TextBoxSetbackToOpen.Text) <= 0 ||
                    Convert.ToInt32(TextBoxSlipageDoubleExit.Text) < -100)
                {
                    throw new Exception();
                }
            }
            catch (Exception)
            {
                MessageBox.Show("В одном из полей недопустимые значения. Процесс сохранения прерван.");
                return;
            }

            try
            {
                // стоп
                _strategySettings.StopIsOn = CheckBoxStopIsOn.IsChecked.Value;
                _strategySettings.StopDistance = Convert.ToInt32(TextBoxStopPercentLenght.Text);
                _strategySettings.StopSlipage = Convert.ToInt32(TextBoxSlipageStop.Text);

                // профит
                _strategySettings.ProfitIsOn = CheckBoxProfitIsOn.IsChecked.Value;
                _strategySettings.ProfitDistance = Convert.ToInt32(TextBoxProfitPercentLenght.Text);
                _strategySettings.ProfitSlipage = Convert.ToInt32(TextBoxSlipageProfit.Text);

// закрытие позиции

                if (CheckBoxSecondToCloseIsOn.IsChecked.HasValue)
                {
                    _strategySettings.SecondToCloseIsOn = CheckBoxSecondToCloseIsOn.IsChecked.Value;
                }
                _strategySettings.SecondToClose = new TimeSpan(0, 0, 0, Convert.ToInt32(TextBoxSecondToClose.Text));

                if (CheckBoxSetbackToCloseIsOn.IsChecked.HasValue)
                {
                    _strategySettings.SetbackToCloseIsOn = CheckBoxSetbackToCloseIsOn.IsChecked.Value;
                }
                _strategySettings.SetbackToClosePosition = Convert.ToInt32(TextBoxSetbackToClose.Text);

                if (CheckBoxDoubleExitIsOnIsOn.IsChecked.HasValue)
                {
                    _strategySettings.DoubleExitIsOn = CheckBoxDoubleExitIsOnIsOn.IsChecked.Value;
                }

                Enum.TryParse(ComboBoxTypeDoubleExitOrder.SelectedItem.ToString(),
                    out _strategySettings.TypeDoubleExitOrder);

                _strategySettings.DoubleExitSlipage = Convert.ToInt32(TextBoxSlipageDoubleExit.Text);

// открытие позиции

                if (CheckBoxSecondToOpenIsOn.IsChecked.HasValue)
                {
                    _strategySettings.SecondToOpenIsOn = CheckBoxSecondToOpenIsOn.IsChecked.Value;
                }
                _strategySettings.SecondToOpen = new TimeSpan(0, 0, 0, Convert.ToInt32(TextBoxSecondToOpen.Text));

                if (CheckBoxSetbackToOpenIsOn.IsChecked.HasValue)
                {
                    _strategySettings.SetbackToOpenIsOn = CheckBoxSetbackToOpenIsOn.IsChecked.Value;
                }
                _strategySettings.SetbackToOpenPosition = Convert.ToInt32(TextBoxSetbackToOpen.Text);

                _strategySettings.Save();
            }
            catch (Exception error)
            {
                MessageBox.Show(error.ToString());
            }

            Close();
        }

        private void CheckBox_Checked(object sender, RoutedEventArgs e)
        {

        }
    }
}
