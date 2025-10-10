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
using OsEngine.Market.Servers;

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
        public BotManualControlUi(BotManualControl strategySettings, StartProgram startProgram, IServerPermission serverPermission)
        {
            InitializeComponent();
            OsEngine.Layout.StickyBorders.Listen(this);
            OsEngine.Layout.StartupLocation.Start_MouseInCentre(this);

            try
            {
                _strategySettings = strategySettings;

                CheckBoxLimitsMakerOnly.IsChecked = _strategySettings.LimitsMakerOnly;

                if (serverPermission != null
                    && serverPermission.HaveOnlyMakerLimitsRealization == false)
                {
                    CheckBoxLimitsMakerOnly.IsEnabled = false;
                }

                ComboBoxValuesType.Items.Add(ManualControlValuesType.MinPriceStep.ToString());
                ComboBoxValuesType.Items.Add(ManualControlValuesType.Absolute.ToString());
                ComboBoxValuesType.Items.Add(ManualControlValuesType.Percent.ToString());
                ComboBoxValuesType.SelectedItem = _strategySettings.ValuesType.ToString();

                ComboBoxOrdersTypeTime.Items.Add(OrderTypeTime.Specified.ToString());
                ComboBoxOrdersTypeTime.Items.Add(OrderTypeTime.GTC.ToString());

                if (startProgram == StartProgram.IsTester
                    || startProgram == StartProgram.IsOsOptimizer)
                {
                    ComboBoxOrdersTypeTime.Items.Add(OrderTypeTime.Day.ToString());
                    CheckBoxLimitsMakerOnly.IsEnabled = false;
                }

                ComboBoxOrdersTypeTime.SelectedItem = _strategySettings.OrderTypeTime.ToString();

                // stop
                // стоп

                CheckBoxStopIsOn.IsChecked = _strategySettings.StopIsOn;
                TextBoxStopPercentLength.Text = _strategySettings.StopDistance.ToStringWithNoEndZero();
                TextBoxSlippageStop.Text = _strategySettings.StopSlippage.ToStringWithNoEndZero();

                // profit
                // профит

                CheckBoxProfitIsOn.IsChecked = _strategySettings.ProfitIsOn;
                TextBoxProfitPercentLength.Text = _strategySettings.ProfitDistance.ToStringWithNoEndZero();
                TextBoxSlippageProfit.Text = _strategySettings.ProfitSlippage.ToStringWithNoEndZero();

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
                TextBoxSlippageDoubleExit.Text = _strategySettings.DoubleExitSlippage.ToString();

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
                LabelOrdersTypeTime.Content = OsLocalization.Trader.Label422;
                CheckBoxLimitsMakerOnly.Content = OsLocalization.Trader.Label623;

                ComboBoxOrdersTypeTime.SelectionChanged += ComboBoxOrdersTypeTime_SelectionChanged;
                ComboBoxOrdersTypeTime_SelectionChanged(null, null);
            }
            catch (Exception error)
            {
                MessageBox.Show(error.ToString());
            }

            this.Activate();
            this.Focus();
        }

        private void ComboBoxOrdersTypeTime_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            OrderTypeTime typeTime = OrderTypeTime.Specified;

            if(Enum.TryParse(ComboBoxOrdersTypeTime.SelectedItem.ToString(),out  typeTime) == false)
            {
                return;
            }

            if (typeTime == OrderTypeTime.Specified)
            {
                CheckBoxSecondToOpenIsOn.IsEnabled = true;
                CheckBoxSecondToCloseIsOn.IsEnabled = true;
                TextBoxSecondToOpen.IsEnabled = true;
                TextBoxSecondToClose.IsEnabled = true;
            }
            else if(typeTime == OrderTypeTime.GTC
                || typeTime == OrderTypeTime.Day)
            {
                CheckBoxSecondToOpenIsOn.IsEnabled = false;
                CheckBoxSecondToCloseIsOn.IsEnabled = false;
                TextBoxSecondToOpen.IsEnabled = false;
                TextBoxSecondToClose.IsEnabled = false;
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
                    TextBoxStopPercentLength.Text.ToDecimal() <= 0 ||
                    TextBoxSlippageStop.Text.ToDecimal() <= 0 ||
                    TextBoxProfitPercentLength.Text.ToDecimal() <= 0 ||
                    TextBoxSlippageProfit.Text.ToDecimal() <= 0 ||
                    TextBoxSetbackToClose.Text.ToDecimal() <= 0 ||
                    Convert.ToInt32(TextBoxSecondToOpen.Text) <= 0 ||
                    TextBoxSetbackToOpen.Text.ToDecimal() <= 0 ||
                    TextBoxSlippageDoubleExit.Text.ToDecimal() < -100)
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
                _strategySettings.StopDistance = TextBoxStopPercentLength.Text.ToDecimal();
                _strategySettings.StopSlippage =TextBoxSlippageStop.Text.ToDecimal();

                // profit
                // профит
                _strategySettings.ProfitIsOn = CheckBoxProfitIsOn.IsChecked.Value;
                _strategySettings.ProfitDistance = TextBoxProfitPercentLength.Text.ToDecimal();
                _strategySettings.ProfitSlippage = TextBoxSlippageProfit.Text.ToDecimal();

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

                _strategySettings.DoubleExitSlippage = TextBoxSlippageDoubleExit.Text.ToDecimal();

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

                Enum.TryParse(ComboBoxOrdersTypeTime.SelectedItem.ToString(), out _strategySettings.OrderTypeTime);

                _strategySettings.LimitsMakerOnly = CheckBoxLimitsMakerOnly.IsChecked.Value;

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
