/*
 * Your rights to use code governed by this license http://o-s-a.net/doc/license_simple_engine.pdf
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using OsEngine.Entity;
using OsEngine.Entity.SyntheticBondEntity;
using OsEngine.Language;
using OsEngine.Market;
using System;
using System.Windows;
using System.Windows.Controls;

namespace OsEngine.OsTrader.Panels.Tab.SyntheticBondTab
{
    /// <summary>
    /// Логика взаимодействия для SyntheticBondOffsetUi.xaml
    /// </summary>
    public partial class SyntheticBondOffsetUi : Window
    {
        #region Constructor

        private SyntheticBondSeries _syntheticBondSeries;

        private SyntheticBond _syntheticBond;

        public SyntheticBondOffsetUi(SyntheticBondSeries synteticBondSeries, ref SyntheticBond syntheticBond)
        {
            InitializeComponent();

            _syntheticBondSeries = synteticBondSeries;
            _syntheticBond = syntheticBond;

            Title = OsLocalization.Trader.Label697;

            Closed += SyntheticBondOffsetUi_Closed;

            if (_syntheticBondSeries.BaseTab.Connector == null ||
                (_syntheticBondSeries.BaseTab.Connector != null && _syntheticBondSeries.BaseTab.Connector.SecurityName == null))
            {
                BaseSynteticBondOffsetsLabel.Content = "None";
            }
            else
            {
                BaseSynteticBondOffsetsLabel.Content = _syntheticBondSeries.BaseTab.Connector.SecurityName;
            }

            if (_syntheticBond.FuturesIcebergParameters.BotTab.Connector == null ||
                (_syntheticBond.FuturesIcebergParameters.BotTab.Connector != null && _syntheticBond.FuturesIcebergParameters.BotTab.Connector.SecurityName == null))
            {
                FuturesSynteticBondOffsetsLabel.Content = "None";
            }
            else
            {
                FuturesSynteticBondOffsetsLabel.Content = _syntheticBond.FuturesIcebergParameters.BotTab.Connector.SecurityName;
            }

            MultiplicatorBaseTextBox.Text = _syntheticBond.BaseMultiplicator.ToString();
            MultiplicatorBaseTextBox.TextChanged += MultiplicatorBaseTextBox_TextChanged;

            MultiplicatorFuturesTextBox.Text = _syntheticBond.FuturesMultiplicator.ToString();
            MultiplicatorFuturesTextBox.TextChanged += MultiplicatorFuturesTextBox_TextChanged;

            CreateRationingUsingAnotherToolBaseComboBox();
            RationingUsingAnotherToolBaseComboBox.SelectionChanged += RationingUsingAnotherToolBaseComboBox_SelectionChanged;

            CreateRationingUsingAnotherToolFuturesComboBox();
            RationingUsingAnotherToolFuturesComboBox.SelectionChanged += RationingUsingAnotherToolFuturesComboBox_SelectionChanged;

            RationingToolBaseButton.Click += RationingToolBaseButton_Click;
            RationingToolFuturesButton.Click += RationingToolFuturesButton_Click;

            if (_syntheticBond.BaseRationingSecurity != null
                && _syntheticBond.BaseRationingSecurity.Connector != null
                && _syntheticBond.BaseRationingSecurity.Connector.SecurityName != null)
            {
                RationingToolBaseButton.Content = _syntheticBond.BaseRationingSecurity.Connector.SecurityName;
            }

            if (_syntheticBond.FuturesRationingSecurity != null
                && _syntheticBond.FuturesRationingSecurity.Connector != null
                && _syntheticBond.FuturesRationingSecurity.Connector.SecurityName != null)
            {
                RationingToolFuturesButton.Content = _syntheticBond.FuturesRationingSecurity.Connector.SecurityName;
            }

            CreateRationingModeBaseComboBox();
            RationingModeBaseComboBox.SelectionChanged += RationingModeBaseComboBox_SelectionChanged;

            CreateRationingModeFuturesComboBox();
            RationingModeFuturesComboBox.SelectionChanged += RationingModeFuturesComboBox_SelectionChanged;

            CreateComboBoxSimbolFurmula();
            ComboBoxSimbolFurmula.SelectionChanged += ComboBoxSimbolFurmula_SelectionChanged;

            TimeOffsetBaseTextBox.Text = _syntheticBond.BaseTimeOffset.ToString();
            TimeOffsetBaseTextBox.TextChanged += TimeOffsetBaseTextBox_TextChanged;

            TimeOffsetFuturesTextBox.Text = _syntheticBond.FuturesTimeOffset.ToString();
            TimeOffsetFuturesTextBox.TextChanged += TimeOffsetFuturesTextBox_TextChanged;

            TimeOffsetBaseRationingTextBox.Text = _syntheticBond.BaseTimeOffsetRationing.ToString();
            TimeOffsetBaseRationingTextBox.TextChanged += TimeOffsetBaseRationingTextBox_TextChanged;

            TimeOffsetFuturesRationingTextBox.Text = _syntheticBond.FuturesTimeOffsetRationing.ToString();
            TimeOffsetFuturesRationingTextBox.TextChanged += TimeOffsetFuturesRationingTextBox_TextChanged;
        }

        private void CreateComboBoxSimbolFurmula()
        {
            ComboBoxSimbolFurmula.Items.Clear();

            ComboBoxSimbolFurmula.Items.Add(new ComboBoxItem
            {
                Content = OsLocalization.Trader.Label693,
                Name = RationingMode.Division.ToString(),
            });

            ComboBoxSimbolFurmula.Items.Add(new ComboBoxItem
            {
                Content = OsLocalization.Trader.Label694,
                Name = RationingMode.Multiplication.ToString(),
            });

            ComboBoxSimbolFurmula.Items.Add(new ComboBoxItem
            {
                Content = OsLocalization.Trader.Label695,
                Name = RationingMode.Difference.ToString(),
            });

            ComboBoxSimbolFurmula.Items.Add(new ComboBoxItem
            {
                Content = OsLocalization.Trader.Label696,
                Name = RationingMode.Addition.ToString(),
            });

            if (_syntheticBond.MainRationingMode == RationingMode.Division)
            {
                ComboBoxSimbolFurmula.SelectedIndex = 0;
            }
            else if (_syntheticBond.MainRationingMode == RationingMode.Multiplication)
            {
                ComboBoxSimbolFurmula.SelectedIndex = 1;
            }
            else if (_syntheticBond.MainRationingMode == RationingMode.Difference)
            {
                ComboBoxSimbolFurmula.SelectedIndex = 2;
            }
            else if (_syntheticBond.MainRationingMode == RationingMode.Addition)
            {
                ComboBoxSimbolFurmula.SelectedIndex = 3;
            }
        }

        private void CreateRationingUsingAnotherToolFuturesComboBox()
        {
            try
            {
                RationingUsingAnotherToolFuturesComboBox.Items.Clear();

                RationingUsingAnotherToolFuturesComboBox.Items.Add(new ComboBoxItem
                {
                    Content = "Off",
                    Name = "Off"
                });

                RationingUsingAnotherToolFuturesComboBox.Items.Add(new ComboBoxItem
                {
                    Content = "On",
                    Name = "On"
                });

                if (_syntheticBond.FuturesUseRationing == false)
                {
                    RationingUsingAnotherToolFuturesComboBox.SelectedIndex = 0;
                }
                else if (_syntheticBond.FuturesUseRationing == true)
                {
                    RationingUsingAnotherToolFuturesComboBox.SelectedIndex = 1;
                }
            }
            catch (Exception ex)
            {
                ServerMaster.SendNewLogMessage(ex.ToString(), Logging.LogMessageType.Error);
            }
        }

        private void CreateRationingUsingAnotherToolBaseComboBox()
        {
            try
            {
                RationingUsingAnotherToolBaseComboBox.Items.Clear();

                RationingUsingAnotherToolBaseComboBox.Items.Add(new ComboBoxItem
                {
                    Content = "Off",
                    Name = "Off"
                });

                RationingUsingAnotherToolBaseComboBox.Items.Add(new ComboBoxItem
                {
                    Content = "On",
                    Name = "On"
                });

                if (_syntheticBond.BaseUseRationing == false)
                {
                    RationingUsingAnotherToolBaseComboBox.SelectedIndex = 0;
                }
                else if (_syntheticBond.BaseUseRationing == true)
                {
                    RationingUsingAnotherToolBaseComboBox.SelectedIndex = 1;
                }
            }
            catch (Exception ex)
            {
                ServerMaster.SendNewLogMessage(ex.ToString(), Logging.LogMessageType.Error);
            }
        }

        private void CreateRationingModeFuturesComboBox()
        {
            RationingModeFuturesComboBox.Items.Clear();

            RationingModeFuturesComboBox.Items.Add(new ComboBoxItem
            {
                Content = OsLocalization.Trader.Label693,
                Name = RationingMode.Division.ToString(),
            });

            RationingModeFuturesComboBox.Items.Add(new ComboBoxItem
            {
                Content = OsLocalization.Trader.Label694,
                Name = RationingMode.Multiplication.ToString(),
            });

            RationingModeFuturesComboBox.Items.Add(new ComboBoxItem
            {
                Content = OsLocalization.Trader.Label695,
                Name = RationingMode.Difference.ToString(),
            });

            RationingModeFuturesComboBox.Items.Add(new ComboBoxItem
            {
                Content = OsLocalization.Trader.Label696,
                Name = RationingMode.Addition.ToString(),
            });

            if (_syntheticBond.FuturesRationingMode == RationingMode.Division)
            {
                RationingModeFuturesComboBox.SelectedIndex = 0;
            }
            else if (_syntheticBond.FuturesRationingMode == RationingMode.Multiplication)
            {
                RationingModeFuturesComboBox.SelectedIndex = 1;
            }
            else if (_syntheticBond.FuturesRationingMode == RationingMode.Difference)
            {
                RationingModeFuturesComboBox.SelectedIndex = 2;
            }
            else if (_syntheticBond.FuturesRationingMode == RationingMode.Addition)
            {
                RationingModeFuturesComboBox.SelectedIndex = 3;
            }
        }

        private void CreateRationingModeBaseComboBox()
        {
            RationingModeBaseComboBox.Items.Clear();

            RationingModeBaseComboBox.Items.Add(new ComboBoxItem
            {
                Content = OsLocalization.Trader.Label693,
                Name = RationingMode.Division.ToString(),
            });

            RationingModeBaseComboBox.Items.Add(new ComboBoxItem
            {
                Content = OsLocalization.Trader.Label694,
                Name = RationingMode.Multiplication.ToString(),
            });

            RationingModeBaseComboBox.Items.Add(new ComboBoxItem
            {
                Content = OsLocalization.Trader.Label695,
                Name = RationingMode.Difference.ToString(),
            });

            RationingModeBaseComboBox.Items.Add(new ComboBoxItem
            {
                Content = OsLocalization.Trader.Label696,
                Name = RationingMode.Addition.ToString(),
            });

            if (_syntheticBond.BaseRationingMode == RationingMode.Division)
            {
                RationingModeBaseComboBox.SelectedIndex = 0;
            }
            else if (_syntheticBond.BaseRationingMode == RationingMode.Multiplication)
            {
                RationingModeBaseComboBox.SelectedIndex = 1;
            }
            else if (_syntheticBond.BaseRationingMode == RationingMode.Difference)
            {
                RationingModeBaseComboBox.SelectedIndex = 2;
            }
            else if (_syntheticBond.BaseRationingMode == RationingMode.Addition)
            {
                RationingModeBaseComboBox.SelectedIndex = 2;
            }
        }

        public string Key
        {
            get
            {
                return _syntheticBond.FuturesIcebergParameters.BotTab.TabName;
            }
        }

        #endregion

        #region Events

        private void SyntheticBondOffsetUi_Closed(object sender, EventArgs e)
        {
            try
            {
                Closed -= SyntheticBondOffsetUi_Closed;
                TimeOffsetFuturesRationingTextBox.TextChanged -= TimeOffsetFuturesRationingTextBox_TextChanged;
                TimeOffsetBaseRationingTextBox.TextChanged -= TimeOffsetBaseRationingTextBox_TextChanged;
                ComboBoxSimbolFurmula.SelectionChanged -= ComboBoxSimbolFurmula_SelectionChanged;
                MultiplicatorBaseTextBox.TextChanged -= MultiplicatorBaseTextBox_TextChanged;
                RationingUsingAnotherToolBaseComboBox.SelectionChanged -= RationingUsingAnotherToolBaseComboBox_SelectionChanged;
                RationingToolBaseButton.Click -= RationingToolBaseButton_Click;
                RationingModeBaseComboBox.SelectionChanged -= RationingModeBaseComboBox_SelectionChanged;
                RationingUsingAnotherToolFuturesComboBox.SelectionChanged -= RationingUsingAnotherToolFuturesComboBox_SelectionChanged;
                MultiplicatorFuturesTextBox.TextChanged -= MultiplicatorFuturesTextBox_TextChanged;
                TimeOffsetBaseTextBox.TextChanged -= TimeOffsetBaseTextBox_TextChanged;
                RationingToolFuturesButton.Click -= RationingToolFuturesButton_Click;
                RationingModeFuturesComboBox.SelectionChanged -= RationingModeFuturesComboBox_SelectionChanged;
                TimeOffsetFuturesTextBox.TextChanged -= TimeOffsetFuturesTextBox_TextChanged;
            }
            catch
            {
                // ignore
            }
        }

        private void TimeOffsetFuturesTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            try
            {
                if (string.IsNullOrEmpty(TimeOffsetFuturesTextBox.Text))
                {
                    return;
                }

                _syntheticBond.FuturesTimeOffset = Convert.ToInt32(TimeOffsetFuturesTextBox.Text);
            }
            catch (Exception ex)
            {
                ServerMaster.SendNewLogMessage(ex.ToString(), Logging.LogMessageType.Error);
            }
        }

        private void TimeOffsetFuturesRationingTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            try
            {
                if (string.IsNullOrEmpty(TimeOffsetFuturesRationingTextBox.Text))
                {
                    return;
                }

                _syntheticBond.FuturesTimeOffsetRationing = Convert.ToInt32(TimeOffsetFuturesRationingTextBox.Text);
            }
            catch (Exception ex)
            {
                ServerMaster.SendNewLogMessage(ex.ToString(), Logging.LogMessageType.Error);
            }
        }

        private void TimeOffsetBaseRationingTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            try
            {
                if (string.IsNullOrEmpty(TimeOffsetBaseRationingTextBox.Text))
                {
                    return;
                }

                _syntheticBond.BaseTimeOffsetRationing = Convert.ToInt32(TimeOffsetBaseRationingTextBox.Text);
            }
            catch (Exception ex)
            {
                ServerMaster.SendNewLogMessage(ex.ToString(), Logging.LogMessageType.Error);
            }
        }

        private void TimeOffsetBaseTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            try
            {
                if (string.IsNullOrEmpty(TimeOffsetBaseTextBox.Text))
                {
                    return;
                }

                _syntheticBond.BaseTimeOffset = Convert.ToInt32(TimeOffsetBaseTextBox.Text);
            }
            catch (Exception ex)
            {
                ServerMaster.SendNewLogMessage(ex.ToString(), Logging.LogMessageType.Error);
            }
        }

        private void RationingModeFuturesComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            try
            {
                if (string.IsNullOrEmpty(RationingModeFuturesComboBox.Text))
                {
                    return;
                }

                if (RationingModeFuturesComboBox.SelectedIndex == 0)
                {
                    _syntheticBond.FuturesRationingMode = RationingMode.Division;
                }
                else if (RationingModeFuturesComboBox.SelectedIndex == 1)
                {
                    _syntheticBond.FuturesRationingMode = RationingMode.Multiplication;
                }
                else if (RationingModeFuturesComboBox.SelectedIndex == 2)
                {
                    _syntheticBond.FuturesRationingMode = RationingMode.Difference;
                }
                else if (RationingModeFuturesComboBox.SelectedIndex == 3)
                {
                    _syntheticBond.FuturesRationingMode = RationingMode.Addition;
                }
            }
            catch (Exception ex)
            {
                ServerMaster.SendNewLogMessage(ex.ToString(), Logging.LogMessageType.Error);
            }
        }

        private void ComboBoxSimbolFurmula_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            try
            {
                if (string.IsNullOrEmpty(ComboBoxSimbolFurmula.Text))
                {
                    return;
                }

                if (ComboBoxSimbolFurmula.SelectedIndex == 0)
                {
                    _syntheticBond.MainRationingMode = RationingMode.Division;
                }
                else if (ComboBoxSimbolFurmula.SelectedIndex == 1)
                {
                    _syntheticBond.MainRationingMode = RationingMode.Multiplication;
                }
                else if (ComboBoxSimbolFurmula.SelectedIndex == 2)
                {
                    _syntheticBond.MainRationingMode = RationingMode.Difference;
                }
                else if (ComboBoxSimbolFurmula.SelectedIndex == 3)
                {
                    _syntheticBond.MainRationingMode = RationingMode.Addition;
                }
            }
            catch (Exception ex)
            {
                ServerMaster.SendNewLogMessage(ex.ToString(), Logging.LogMessageType.Error);
            }
        }

        private void RationingModeBaseComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            try
            {
                if (string.IsNullOrEmpty(RationingModeBaseComboBox.Text))
                {
                    return;
                }

                if (RationingModeBaseComboBox.SelectedIndex == 0)
                {
                    _syntheticBond.BaseRationingMode = RationingMode.Division;
                }
                else if (RationingModeBaseComboBox.SelectedIndex == 1)
                {
                    _syntheticBond.BaseRationingMode = RationingMode.Multiplication;
                }
                else if (RationingModeBaseComboBox.SelectedIndex == 2)
                {
                    _syntheticBond.BaseRationingMode = RationingMode.Difference;
                }
                else if (RationingModeBaseComboBox.SelectedIndex == 3)
                {
                    _syntheticBond.BaseRationingMode = RationingMode.Addition;
                }
            }
            catch (Exception ex)
            {
                ServerMaster.SendNewLogMessage(ex.ToString(), Logging.LogMessageType.Error);
            }
        }

        private void RationingToolFuturesButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_syntheticBond.FuturesRationingSecurity == null)
                {
                    _syntheticBond.FuturesRationingSecurity = new BotTabSimple(_syntheticBond.FuturesIcebergParameters.BotTab.TabName + "Rationing", _syntheticBond.FuturesIcebergParameters.BotTab.StartProgram);
                    return;
                }

                _syntheticBond.FuturesRationingSecurity.ShowConnectorDialog();
                _syntheticBond.FuturesRationingSecurity.SecuritySubscribeEvent += RationingSecuritySubscribeEvent;
            }
            catch (Exception ex)
            {
                ServerMaster.SendNewLogMessage(ex.ToString(), Logging.LogMessageType.Error);
            }
        }

        private void RationingToolBaseButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_syntheticBond.BaseRationingSecurity == null)
                {
                    _syntheticBond.BaseRationingSecurity = new BotTabSimple(_syntheticBond.BaseIcebergParameters.BotTab.TabName + "Rationing", _syntheticBond.BaseIcebergParameters.BotTab.StartProgram);
                    return;
                }

                _syntheticBond.BaseRationingSecurity.ShowConnectorDialog();
                _syntheticBond.BaseRationingSecurity.SecuritySubscribeEvent += SecurityBaseSubscribeEvent;
            }
            catch (Exception ex)
            {
                ServerMaster.SendNewLogMessage(ex.ToString(), Logging.LogMessageType.Error);
            }
        }

        private void RationingSecuritySubscribeEvent(Security security)
        {
            try
            {
                if (RationingToolFuturesButton.Dispatcher.CheckAccess() == false)
                {
                    RationingToolFuturesButton.Dispatcher.Invoke(new Action<Security>(RationingSecuritySubscribeEvent), security);
                    return;
                }

                RationingToolFuturesButton.Content = security.Name.ToString();
                _syntheticBond.FuturesRationingSecurity.SecuritySubscribeEvent -= RationingSecuritySubscribeEvent;
            }
            catch (Exception ex)
            {
                ServerMaster.SendNewLogMessage(ex.ToString(), Logging.LogMessageType.Error);
            }
        }

        private void SecurityBaseSubscribeEvent(Security security)
        {
            try
            {
                if (RationingToolBaseButton.Dispatcher.CheckAccess() == false)
                {
                    RationingToolBaseButton.Dispatcher.Invoke(new Action<Security>(SecurityBaseSubscribeEvent), security);
                    return;
                }

                RationingToolBaseButton.Content = security.Name.ToString();
                _syntheticBond.BaseRationingSecurity.SecuritySubscribeEvent -= SecurityBaseSubscribeEvent;
            }
            catch (Exception ex)
            {
                ServerMaster.SendNewLogMessage(ex.ToString(), Logging.LogMessageType.Error);
            }
        }

        private void RationingUsingAnotherToolFuturesComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            try
            {
                if (string.IsNullOrEmpty(RationingUsingAnotherToolFuturesComboBox.Text))
                {
                    return;
                }

                if (RationingUsingAnotherToolFuturesComboBox.SelectedIndex == 0)
                {
                    _syntheticBond.FuturesUseRationing = false;
                }
                else if (RationingUsingAnotherToolFuturesComboBox.SelectedIndex == 1)
                {
                    _syntheticBond.FuturesUseRationing = true;
                }
            }
            catch (Exception ex)
            {
                ServerMaster.SendNewLogMessage(ex.ToString(), Logging.LogMessageType.Error);
            }
        }

        private void RationingUsingAnotherToolBaseComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            try
            {
                if (string.IsNullOrEmpty(RationingUsingAnotherToolBaseComboBox.Text))
                {
                    return;
                }

                if (RationingUsingAnotherToolBaseComboBox.SelectedIndex == 0)
                {
                    _syntheticBond.BaseUseRationing = false;
                }
                else if (RationingUsingAnotherToolBaseComboBox.SelectedIndex == 1)
                {
                    _syntheticBond.BaseUseRationing = true;
                }
            }
            catch (Exception ex)
            {
                ServerMaster.SendNewLogMessage(ex.ToString(), Logging.LogMessageType.Error);
            }
        }

        private void MultiplicatorBaseTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            try
            {
                if (string.IsNullOrEmpty(MultiplicatorBaseTextBox.Text))
                {
                    return;
                }

                _syntheticBond.BaseMultiplicator = MultiplicatorBaseTextBox.Text.ToDecimal();
            }
            catch (Exception ex)
            {
                ServerMaster.SendNewLogMessage(ex.ToString(), Logging.LogMessageType.Error);
            }
        }

        private void MultiplicatorFuturesTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            try
            {
                if (string.IsNullOrEmpty(MultiplicatorFuturesTextBox.Text))
                {
                    return;
                }

                _syntheticBond.FuturesMultiplicator = MultiplicatorFuturesTextBox.Text.ToDecimal();
            }
            catch (Exception ex)
            {
                ServerMaster.SendNewLogMessage(ex.ToString(), Logging.LogMessageType.Error);
            }
        }

        #endregion
    }
}
