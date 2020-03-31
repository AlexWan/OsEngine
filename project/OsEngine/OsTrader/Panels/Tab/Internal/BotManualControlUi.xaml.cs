/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using OsEngine.Entity;
using OsEngine.Language;

namespace OsEngine.OsTrader.Panels.Tab.Internal
{
    public partial class BotManualControlUi
    {

        /// <summary>
        /// strategy settings /
        /// настройки стратегии
        /// </summary>
        private BotManualControl _strategySettings;

        /// <summary>
        /// constructor /
        /// конструктор
        /// </summary>
        public BotManualControlUi(BotManualControl strategySettings)
        {
            InitializeComponent();

            try
            {
                _strategySettings = strategySettings;

                ComboBoxValuesType.Items.Add(ManualControlValuesType.MinPriceStep.ToString());
                ComboBoxValuesType.Items.Add(ManualControlValuesType.Absolute.ToString());
                ComboBoxValuesType.Items.Add(ManualControlValuesType.Percent.ToString());
                ComboBoxValuesType.SelectedItem = _strategySettings.ValuesType.ToString();

                // stop
                // стоп

                CheckBoxStopIsOn.IsChecked = _strategySettings.StopIsOn;
                TextBoxStopPercentLenght.Text = _strategySettings.StopDistance.ToString(new CultureInfo("ru-RU"));
                TextBoxSlipageStop.Text = _strategySettings.StopSlipage.ToString(new CultureInfo("ru-RU"));

                // profit
                // профит

                CheckBoxProfitIsOn.IsChecked = _strategySettings.ProfitIsOn;
                TextBoxProfitPercentLenght.Text = _strategySettings.ProfitDistance.ToString(new CultureInfo("ru-RU"));
                TextBoxSlipageProfit.Text = _strategySettings.ProfitSlipage.ToString(new CultureInfo("ru-RU"));

                // closing position
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

                // opening position
                // открытие позиции

                CheckBoxSecondToOpenIsOn.IsChecked = _strategySettings.SecondToOpenIsOn;
                TextBoxSecondToOpen.Text = _strategySettings.SecondToOpen.TotalSeconds.ToString(new CultureInfo("ru-RU"));

                CheckBoxSetbackToOpenIsOn.IsChecked = _strategySettings.SetbackToOpenIsOn;
                TextBoxSetbackToOpen.Text = _strategySettings.SetbackToOpenPosition.ToString();

                Title = OsLocalization.Trader.Label85;
                LabelStop.Content = OsLocalization.Trader.Label86;
                LabelProfit.Content = OsLocalization.Trader.Label87;
                LabelPositionClosing.Content = OsLocalization.Trader.Label88;
                LabelPositionOpening.Content = OsLocalization.Trader.Label89;
                LabelCloseOrderReject.Content = OsLocalization.Trader.Label90;
                CheckBoxStopIsOn.Content = OsLocalization.Trader.Label91;
                CheckBoxProfitIsOn.Content = OsLocalization.Trader.Label91;
                LabelSlippage1.Content = OsLocalization.Trader.Label92;
                LabelSlippage2.Content = OsLocalization.Trader.Label92;
                LabelSlippage3.Content = OsLocalization.Trader.Label92;
                LabelFromEntryToStop.Content = OsLocalization.Trader.Label93;
                LabelFromEntryToProfit.Content = OsLocalization.Trader.Label94;
                CheckBoxSecondToCloseIsOn.Content = OsLocalization.Trader.Label95;
                CheckBoxSetbackToCloseIsOn.Content = OsLocalization.Trader.Label96;
                CheckBoxSetbackToOpenIsOn.Content = OsLocalization.Trader.Label96;
                CheckBoxSecondToOpenIsOn.Content = OsLocalization.Trader.Label97;
                ButtonAccept.Content = OsLocalization.Trader.Label17;
                CheckBoxDoubleExitIsOnIsOn.Content = OsLocalization.Trader.Label99;
                LabelValuesType.Content = OsLocalization.Trader.Label158;
            }
            catch (Exception error)
            {
                MessageBox.Show(error.ToString());
            }
        }

        /// <summary>
        /// button accept
        /// кнопка принять
        /// </summary>
        private void ButtonAccept_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (Convert.ToInt32(TextBoxSecondToOpen.Text) <= 0 ||
                    Convert.ToInt32(TextBoxSecondToClose.Text) <= 0 ||
                    TextBoxStopPercentLenght.Text.ToDecimal() <= 0 ||
                    TextBoxSlipageStop.Text.ToDecimal() <= 0 ||
                    TextBoxProfitPercentLenght.Text.ToDecimal() <= 0 ||
                    TextBoxSlipageProfit.Text.ToDecimal() <= 0 ||
                    TextBoxSetbackToClose.Text.ToDecimal() <= 0 ||
                    Convert.ToInt32(TextBoxSecondToOpen.Text) <= 0 ||
                    TextBoxSetbackToOpen.Text.ToDecimal() <= 0 ||
                    TextBoxSlipageDoubleExit.Text.ToDecimal() < -100)
                {
                    throw new Exception();
                }
            }
            catch (Exception)
            {
                MessageBox.Show(OsLocalization.Trader.Label13);
                return;
            }

            try
            {
                // stop
                // стоп
                _strategySettings.StopIsOn = CheckBoxStopIsOn.IsChecked.Value;
                _strategySettings.StopDistance = TextBoxStopPercentLenght.Text.ToDecimal();
                _strategySettings.StopSlipage =TextBoxSlipageStop.Text.ToDecimal();

                // profit
                // профит
                _strategySettings.ProfitIsOn = CheckBoxProfitIsOn.IsChecked.Value;
                _strategySettings.ProfitDistance = TextBoxProfitPercentLenght.Text.ToDecimal();
                _strategySettings.ProfitSlipage = TextBoxSlipageProfit.Text.ToDecimal();

                // closing position
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
                _strategySettings.SetbackToClosePosition = TextBoxSetbackToClose.Text.ToDecimal();

                if (CheckBoxDoubleExitIsOnIsOn.IsChecked.HasValue)
                {
                    _strategySettings.DoubleExitIsOn = CheckBoxDoubleExitIsOnIsOn.IsChecked.Value;
                }

                Enum.TryParse(ComboBoxTypeDoubleExitOrder.SelectedItem.ToString(),
                    out _strategySettings.TypeDoubleExitOrder);

                _strategySettings.DoubleExitSlipage = TextBoxSlipageDoubleExit.Text.ToDecimal();

                // opening position
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
                _strategySettings.SetbackToOpenPosition = TextBoxSetbackToOpen.Text.ToDecimal();

                Enum.TryParse(ComboBoxValuesType.SelectedItem.ToString(), out _strategySettings.ValuesType);

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
