/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using OsEngine.Entity;
using OsEngine.Language;
using OsEngine.Market;
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
        /// server permission
        /// </summary>
        private IServerPermission _serverPermission;

        /// <summary>
        /// constructor
        /// </summary>
        public BotManualControlUi(BotManualControl strategySettings, StartProgram startProgram, IServerPermission serverPermission)
        {
            InitializeComponent();
            OsEngine.Layout.StickyBorders.Listen(this);
            OsEngine.Layout.StartupLocation.Start_MouseInCentre(this);

            try
            {
                _strategySettings = strategySettings;
                _serverPermission = serverPermission;

                CheckBoxLimitsMakerOnly.IsChecked = _strategySettings.LimitsMakerOnly;

                if (serverPermission != null
                    && serverPermission.HaveOnlyMakerLimitsRealization == false)
                {
                    CheckBoxLimitsMakerOnly.IsEnabled = false;
                }

                // Values type
                ComboBoxValuesType.Items.Add(ManualControlValuesType.MinPriceStep.ToString());
                ComboBoxValuesType.Items.Add(ManualControlValuesType.Absolute.ToString());
                ComboBoxValuesType.Items.Add(ManualControlValuesType.Percent.ToString());
                ComboBoxValuesType.SelectedItem = _strategySettings.ValuesType.ToString();

                // Order lifetime type - заполняем на основе возможностей коннектора
                FillOrderLifeTimeComboBox();

                // stop
                CheckBoxStopIsOn.IsChecked = _strategySettings.StopIsOn;
                TextBoxStopPercentLength.Text = _strategySettings.StopDistance.ToStringWithNoEndZero();
                TextBoxSlippageStop.Text = _strategySettings.StopSlippage.ToStringWithNoEndZero();

                // profit
                CheckBoxProfitIsOn.IsChecked = _strategySettings.ProfitIsOn;
                TextBoxProfitPercentLength.Text = _strategySettings.ProfitDistance.ToStringWithNoEndZero();
                TextBoxSlippageProfit.Text = _strategySettings.ProfitSlippage.ToStringWithNoEndZero();

                // closing position
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
                CheckBoxSecondToOpenIsOn.IsChecked = _strategySettings.SecondToOpenIsOn;
                TextBoxSecondToOpen.Text = _strategySettings.SecondToOpen.TotalSeconds.ToString(new CultureInfo("ru-RU"));

                CheckBoxSetbackToOpenIsOn.IsChecked = _strategySettings.SetbackToOpenIsOn;
                TextBoxSetbackToOpen.Text = _strategySettings.SetbackToOpenPosition.ToString();

                // localization
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

            if (InteractiveInstructions.TesterLightPosts.AllInstructionsInClass == null
            || InteractiveInstructions.TesterLightPosts.AllInstructionsInClass.Count == 0)
            {
                ButtonBotManualControl.Visibility = Visibility.Visible;
            }

            StartButtonBlinkAnimation();
        }

        /// <summary>
        /// Fill Order Lifetime ComboBox based on server permissions
        /// </summary>
        private void FillOrderLifeTimeComboBox()
        {
            try
            {
                ComboBoxOrdersTypeTime.Items.Clear();

                if (_serverPermission == null || _serverPermission.OrdersLifeTimeRealization == null)
                {
                    // If no permission info, add only Specified 
                    ComboBoxOrdersTypeTime.Items.Add(OrderTypeTime.Specified.ToString());
                    ComboBoxOrdersTypeTime.SelectedIndex = 0;
                    ComboBoxOrdersTypeTime.IsEnabled = false;
                    return;
                }

                OrderLifeTimePermission permission = _serverPermission.OrdersLifeTimeRealization;

                // Add only supported types
                if (permission.GtcIsReady)
                {
                    ComboBoxOrdersTypeTime.Items.Add(OrderTypeTime.GTC.ToString());
                }

                if (permission.SpecifiedIsReady)
                {
                    ComboBoxOrdersTypeTime.Items.Add(OrderTypeTime.Specified.ToString());
                }

                if (permission.DayIsReady)
                {
                    ComboBoxOrdersTypeTime.Items.Add(OrderTypeTime.Day.ToString());
                }

                // If nothing was added, add Specified as default
                if (ComboBoxOrdersTypeTime.Items.Count == 0)
                {
                    ComboBoxOrdersTypeTime.Items.Add(OrderTypeTime.Specified.ToString());
                    ComboBoxOrdersTypeTime.IsEnabled = false;
                }
                else
                {
                    ComboBoxOrdersTypeTime.IsEnabled = true;
                }

                // Load saved value
                LoadSelectedOrderLifeTime();
            }
            catch (Exception ex)
            {
                // On error, add Specified and disable
                ComboBoxOrdersTypeTime.Items.Clear();
                ComboBoxOrdersTypeTime.Items.Add(OrderTypeTime.Specified.ToString());
                ComboBoxOrdersTypeTime.IsEnabled = false;
                ServerMaster.SendNewLogMessage(ex.ToString(), Logging.LogMessageType.Error);
            }
        }

        /// <summary>
        /// Load selected order lifetime from settings
        /// </summary>
        private void LoadSelectedOrderLifeTime()
        {
            try
            {
                if (_strategySettings == null)
                {
                    if (ComboBoxOrdersTypeTime.Items.Count > 0)
                    {
                        ComboBoxOrdersTypeTime.SelectedIndex = 0;
                    }
                    return;
                }

                string savedValue = _strategySettings.OrderTypeTime.ToString();

                // Find the saved value in combobox
                int index = -1;
                for (int i = 0; i < ComboBoxOrdersTypeTime.Items.Count; i++)
                {
                    if (ComboBoxOrdersTypeTime.Items[i].ToString() == savedValue)
                    {
                        index = i;
                        break;
                    }
                }

                if (index >= 0)
                {
                    ComboBoxOrdersTypeTime.SelectedIndex = index;
                }
                else
                {
                    // If saved value not supported, select first available
                    if (ComboBoxOrdersTypeTime.Items.Count > 0)
                    {
                        ComboBoxOrdersTypeTime.SelectedIndex = 0;

                        // Update settings to avoid future mismatch
                        string newValue = ComboBoxOrdersTypeTime.Items[0].ToString();
                        OrderTypeTime newType;
                        if (Enum.TryParse(newValue, out newType))
                        {
                            _strategySettings.OrderTypeTime = newType;
                            _strategySettings.Save();
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                if (ComboBoxOrdersTypeTime.Items.Count > 0)
                {
                    ComboBoxOrdersTypeTime.SelectedIndex = 0;
                }
                ServerMaster.SendNewLogMessage(ex.ToString(), Logging.LogMessageType.Error);
            }
        }

        private void StartButtonBlinkAnimation()
        {
            try
            {
                DispatcherTimer timer = new DispatcherTimer();
                int blinkCount = 0;
                bool isGreenVisible = true;

                timer.Interval = TimeSpan.FromMilliseconds(300);
                timer.Tick += (s, e) =>
                {
                    try
                    {
                        if (blinkCount >= 20)
                        {
                            timer.Stop();
                            PostGreenBotManualControl.Opacity = 1;
                            PostWhiteBotManualControl.Opacity = 0;
                            return;
                        }

                        if (isGreenVisible)
                        {
                            PostGreenBotManualControl.Opacity = 0;
                            PostWhiteBotManualControl.Opacity = 1;
                        }
                        else
                        {
                            PostGreenBotManualControl.Opacity = 1;
                            PostWhiteBotManualControl.Opacity = 0;
                        }

                        isGreenVisible = !isGreenVisible;
                        blinkCount++;
                    }
                    catch (Exception ex)
                    {
                        ServerMaster.SendNewLogMessage(ex.ToString(), Logging.LogMessageType.Error);
                        timer.Stop();
                    }
                };

                timer.Start();
            }
            catch (Exception ex)
            {
                ServerMaster.SendNewLogMessage(ex.ToString(), Logging.LogMessageType.Error);
            }
        }

        private void ComboBoxOrdersTypeTime_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            try
            {
                if (ComboBoxOrdersTypeTime.SelectedItem == null)
                {
                    return;
                }

                OrderTypeTime typeTime = OrderTypeTime.GTC;

                if (Enum.TryParse(ComboBoxOrdersTypeTime.SelectedItem.ToString(), out typeTime) == false)
                {
                    return;
                }

                // Enable/disable time-based settings based on order lifetime type
                if (typeTime == OrderTypeTime.Specified)
                {
                    CheckBoxSecondToOpenIsOn.IsEnabled = true;
                    CheckBoxSecondToCloseIsOn.IsEnabled = true;
                    TextBoxSecondToOpen.IsEnabled = true;
                    TextBoxSecondToClose.IsEnabled = true;
                }
                else
                {
                    CheckBoxSecondToOpenIsOn.IsEnabled = false;
                    CheckBoxSecondToCloseIsOn.IsEnabled = false;
                    TextBoxSecondToOpen.IsEnabled = false;
                    TextBoxSecondToClose.IsEnabled = false;

                    // Auto-uncheck if they were checked
                    if (CheckBoxSecondToOpenIsOn.IsChecked == true)
                    {
                        CheckBoxSecondToOpenIsOn.IsChecked = false;
                    }
                    if (CheckBoxSecondToCloseIsOn.IsChecked == true)
                    {
                        CheckBoxSecondToCloseIsOn.IsChecked = false;
                    }
                }
            }
            catch (Exception ex)
            {
                ServerMaster.SendNewLogMessage(ex.ToString(), Logging.LogMessageType.Error);
            }
        }

        /// <summary>
        /// button accept
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
                _strategySettings.StopSlippage = TextBoxSlippageStop.Text.ToDecimal();

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

                // Save order lifetime type with validation
                SaveOrderLifeTime();

                _strategySettings.LimitsMakerOnly = CheckBoxLimitsMakerOnly.IsChecked.Value;

                _strategySettings.Save();
            }
            catch (Exception error)
            {
                MessageBox.Show(error.ToString());
            }

            Close();
        }

        /// <summary>
        /// Save order lifetime type with validation
        /// </summary>
        private void SaveOrderLifeTime()
        {
            try
            {
                if (ComboBoxOrdersTypeTime.SelectedItem == null)
                {
                    _strategySettings.OrderTypeTime = OrderTypeTime.Specified;
                    return;
                }

                string selectedValue = ComboBoxOrdersTypeTime.SelectedItem.ToString();
                OrderTypeTime selectedType;

                if (Enum.TryParse(selectedValue, out selectedType))
                {
                    // Validate that selected type is supported by connector
                    if (IsLifeTimeSupported(selectedType))
                    {
                        _strategySettings.OrderTypeTime = selectedType;
                    }
                    else
                    {
                        // If not supported, fallback to GTC
                        _strategySettings.OrderTypeTime = OrderTypeTime.Specified;
                    }
                }
                else
                {
                    _strategySettings.OrderTypeTime = OrderTypeTime.Specified;
                }
            }
            catch (Exception ex)
            {
                _strategySettings.OrderTypeTime = OrderTypeTime.Specified;
                ServerMaster.SendNewLogMessage(ex.ToString(), Logging.LogMessageType.Error);
            }
        }

        /// <summary>
        /// Check if order lifetime type is supported by connector
        /// </summary>
        private bool IsLifeTimeSupported(OrderTypeTime type)
        {
            try
            {
                if (_serverPermission == null || _serverPermission.OrdersLifeTimeRealization == null)
                {
                    return type == OrderTypeTime.Specified;
                }

                OrderLifeTimePermission permission = _serverPermission.OrdersLifeTimeRealization;

                switch (type)
                {
                    case OrderTypeTime.GTC:
                        return permission.GtcIsReady;
                    case OrderTypeTime.Specified:
                        return permission.SpecifiedIsReady;
                    case OrderTypeTime.Day:
                        return permission.DayIsReady;
                    default:
                        return false;
                }
            }
            catch
            {
                return false;
            }
        }

        private void CheckBox_Checked(object sender, RoutedEventArgs e)
        {

        }

        #region Posts collection

        private void ButtonBotManualControl_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                InteractiveInstructions.TesterLightPosts.Link10.ShowLinkInBrowser();
            }
            catch (Exception ex)
            {
                ServerMaster.SendNewLogMessage(ex.ToString(), Logging.LogMessageType.Error);
            }
        }

        #endregion
    }
}
