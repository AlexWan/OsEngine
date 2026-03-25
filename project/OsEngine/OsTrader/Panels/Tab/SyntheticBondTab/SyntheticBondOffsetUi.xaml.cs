using OsEngine.Entity;
using OsEngine.Entity.SynteticBondEntity;
using OsEngine.Language;
using OsEngine.Market;
using OsEngine.OsTrader.Panels.Tab.SyntheticBondTab;
using System;
using System.Windows;
using System.Windows.Controls;

namespace OsEngine.OsTrader.Panels.Tab.SynteticBondTab
{
    /// <summary>
    /// Логика взаимодействия для SyntheticBondOffsetUi.xaml
    /// </summary>
    public partial class SyntheticBondOffsetUi : Window
    {
        #region Constructor

        private SyntheticBondSeries _synteticBond;

        private SyntheticBond _futuresSyntheticBond;

        public SyntheticBondOffsetUi(SyntheticBondSeries synteticBond, ref SyntheticBond modificationFuturesSyntheticBond)
        {
            InitializeComponent();

            _synteticBond = synteticBond;
            _futuresSyntheticBond = modificationFuturesSyntheticBond;

            Title = OsLocalization.Trader.Label697;

            Closed += SyntheticBondOffsetUi_Closed;

            if (_synteticBond.BaseTab.Connector == null ||
                (_synteticBond.BaseTab.Connector != null && _synteticBond.BaseTab.Connector.SecurityName == null))
            {
                BaseSynteticBondOffsetsLabel.Content = "None";
            }
            else
            {
                BaseSynteticBondOffsetsLabel.Content = _synteticBond.BaseTab.Connector.SecurityName;
            }

            if (_futuresSyntheticBond.FuturesIcebergParameters.BotTab.Connector == null ||
                (_futuresSyntheticBond.FuturesIcebergParameters.BotTab.Connector != null && _futuresSyntheticBond.FuturesIcebergParameters.BotTab.Connector.SecurityName == null))
            {
                FuturesSynteticBondOffsetsLabel.Content = "None";
            }
            else
            {
                FuturesSynteticBondOffsetsLabel.Content = _futuresSyntheticBond.FuturesIcebergParameters.BotTab.Connector.SecurityName;
            }

            MultiplicatorBaseTextBox.Text = _futuresSyntheticBond.BaseMultiplicator.ToString();
            MultiplicatorBaseTextBox.TextChanged += MultiplicatorBaseTextBox_TextChanged;

            MultiplicatorFuturesTextBox.Text = _futuresSyntheticBond.FuturesMultiplicator.ToString();
            MultiplicatorFuturesTextBox.TextChanged += MultiplicatorFuturesTextBox_TextChanged;

            CreateRationingUsingAnotherToolBaseComboBox();
            RationingUsingAnotherToolBaseComboBox.SelectionChanged += RationingUsingAnotherToolBaseComboBox_SelectionChanged;

            CreateRationingUsingAnotherToolFuturesComboBox();
            RationingUsingAnotherToolFuturesComboBox.SelectionChanged += RationingUsingAnotherToolFuturesComboBox_SelectionChanged;

            RationingToolBaseButton.Click += RationingToolBaseButton_Click;
            RationingToolFuturesButton.Click += RationingToolFuturesButton_Click;

            if (_futuresSyntheticBond.BaseRationingSecurity != null
                && _futuresSyntheticBond.BaseRationingSecurity.Connector != null
                && _futuresSyntheticBond.BaseRationingSecurity.Connector.SecurityName != null)
            {
                RationingToolBaseButton.Content = _futuresSyntheticBond.BaseRationingSecurity.Connector.SecurityName;
            }

            if (_futuresSyntheticBond.FuturesRationingSecurity != null
                && _futuresSyntheticBond.FuturesRationingSecurity.Connector != null
                && _futuresSyntheticBond.FuturesRationingSecurity.Connector.SecurityName != null)
            {
                RationingToolFuturesButton.Content = _futuresSyntheticBond.FuturesRationingSecurity.Connector.SecurityName;
            }

            CreateRationingModeBaseComboBox();
            RationingModeBaseComboBox.SelectionChanged += RationingModeBaseComboBox_SelectionChanged;

            CreateRationingModeFuturesComboBox();
            RationingModeFuturesComboBox.SelectionChanged += RationingModeFuturesComboBox_SelectionChanged;

            CreateComboBoxSimbolFurmula();
            ComboBoxSimbolFurmula.SelectionChanged += ComboBoxSimbolFurmula_SelectionChanged;

            TimeOffsetBaseTextBox.Text = _futuresSyntheticBond.BaseTimeOffset.ToString();
            TimeOffsetBaseTextBox.TextChanged += TimeOffsetBaseTextBox_TextChanged;

            TimeOffsetFuturesTextBox.Text = _futuresSyntheticBond.FuturesTimeOffset.ToString();
            TimeOffsetFuturesTextBox.TextChanged += TimeOffsetFuturesTextBox_TextChanged;

            TimeOffsetBaseRationingTextBox.Text = _futuresSyntheticBond.BaseTimeOffsetRationing.ToString();
            TimeOffsetBaseRationingTextBox.TextChanged += TimeOffsetBaseRationingTextBox_TextChanged;

            TimeOffsetFuturesRationingTextBox.Text = _futuresSyntheticBond.FuturesTimeOffsetRationing.ToString();
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

            if (_futuresSyntheticBond.MainRationingMode == RationingMode.Division)
            {
                ComboBoxSimbolFurmula.SelectedIndex = 0;
            }
            else if (_futuresSyntheticBond.MainRationingMode == RationingMode.Multiplication)
            {
                ComboBoxSimbolFurmula.SelectedIndex = 1;
            }
            else if (_futuresSyntheticBond.MainRationingMode == RationingMode.Difference)
            {
                ComboBoxSimbolFurmula.SelectedIndex = 2;
            }
            else if (_futuresSyntheticBond.MainRationingMode == RationingMode.Addition)
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

                if (_futuresSyntheticBond.FuturesUseRationing == false)
                {
                    RationingUsingAnotherToolFuturesComboBox.SelectedIndex = 0;
                }
                else if (_futuresSyntheticBond.FuturesUseRationing == true)
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

                if (_futuresSyntheticBond.BaseUseRationing == false)
                {
                    RationingUsingAnotherToolBaseComboBox.SelectedIndex = 0;
                }
                else if (_futuresSyntheticBond.BaseUseRationing == true)
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

            if (_futuresSyntheticBond.FuturesRationingMode == RationingMode.Division)
            {
                RationingModeFuturesComboBox.SelectedIndex = 0;
            }
            else if (_futuresSyntheticBond.FuturesRationingMode == RationingMode.Multiplication)
            {
                RationingModeFuturesComboBox.SelectedIndex = 1;
            }
            else if (_futuresSyntheticBond.FuturesRationingMode == RationingMode.Difference)
            {
                RationingModeFuturesComboBox.SelectedIndex = 2;
            }
            else if (_futuresSyntheticBond.FuturesRationingMode == RationingMode.Addition)
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

            if (_futuresSyntheticBond.BaseRationingMode == RationingMode.Division)
            {
                RationingModeBaseComboBox.SelectedIndex = 0;
            }
            else if (_futuresSyntheticBond.BaseRationingMode == RationingMode.Multiplication)
            {
                RationingModeBaseComboBox.SelectedIndex = 1;
            }
            else if (_futuresSyntheticBond.BaseRationingMode == RationingMode.Difference)
            {
                RationingModeBaseComboBox.SelectedIndex = 2;
            }
            else if (_futuresSyntheticBond.BaseRationingMode == RationingMode.Addition)
            {
                RationingModeBaseComboBox.SelectedIndex = 2;
            }
        }

        public string Key
        {
            get
            {
                return _futuresSyntheticBond.FuturesIcebergParameters.BotTab.TabName;
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

                _futuresSyntheticBond.FuturesTimeOffset = Convert.ToInt32(TimeOffsetFuturesTextBox.Text);
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

                _futuresSyntheticBond.FuturesTimeOffsetRationing = Convert.ToInt32(TimeOffsetFuturesRationingTextBox.Text);
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

                _futuresSyntheticBond.BaseTimeOffsetRationing = Convert.ToInt32(TimeOffsetBaseRationingTextBox.Text);
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

                _futuresSyntheticBond.BaseTimeOffset = Convert.ToInt32(TimeOffsetBaseTextBox.Text);
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
                    _futuresSyntheticBond.FuturesRationingMode = RationingMode.Division;
                }
                else if (RationingModeFuturesComboBox.SelectedIndex == 1)
                {
                    _futuresSyntheticBond.FuturesRationingMode = RationingMode.Multiplication;
                }
                else if (RationingModeFuturesComboBox.SelectedIndex == 2)
                {
                    _futuresSyntheticBond.FuturesRationingMode = RationingMode.Difference;
                }
                else if (RationingModeFuturesComboBox.SelectedIndex == 3)
                {
                    _futuresSyntheticBond.FuturesRationingMode = RationingMode.Addition;
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
                    _futuresSyntheticBond.MainRationingMode = RationingMode.Division;
                }
                else if (ComboBoxSimbolFurmula.SelectedIndex == 1)
                {
                    _futuresSyntheticBond.MainRationingMode = RationingMode.Multiplication;
                }
                else if (ComboBoxSimbolFurmula.SelectedIndex == 2)
                {
                    _futuresSyntheticBond.MainRationingMode = RationingMode.Difference;
                }
                else if (ComboBoxSimbolFurmula.SelectedIndex == 3)
                {
                    _futuresSyntheticBond.MainRationingMode = RationingMode.Addition;
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
                    _futuresSyntheticBond.BaseRationingMode = RationingMode.Division;
                }
                else if (RationingModeBaseComboBox.SelectedIndex == 1)
                {
                    _futuresSyntheticBond.BaseRationingMode = RationingMode.Multiplication;
                }
                else if (RationingModeBaseComboBox.SelectedIndex == 2)
                {
                    _futuresSyntheticBond.BaseRationingMode = RationingMode.Difference;
                }
                else if (RationingModeBaseComboBox.SelectedIndex == 3)
                {
                    _futuresSyntheticBond.BaseRationingMode = RationingMode.Addition;
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
                if (_futuresSyntheticBond.FuturesRationingSecurity == null)
                {
                    _futuresSyntheticBond.FuturesRationingSecurity = new BotTabSimple(_futuresSyntheticBond.FuturesIcebergParameters.BotTab.TabName + "Rationing", _futuresSyntheticBond.FuturesIcebergParameters.BotTab.StartProgram);
                    return;
                }

                _futuresSyntheticBond.FuturesRationingSecurity.ShowConnectorDialog();
                _futuresSyntheticBond.FuturesRationingSecurity.SecuritySubscribeEvent += RationingSecuritySubscribeEvent;
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
                if (_futuresSyntheticBond.BaseRationingSecurity == null)
                {
                    _futuresSyntheticBond.BaseRationingSecurity = new BotTabSimple(_futuresSyntheticBond.BaseIcebergParameters.BotTab.TabName + "Rationing", _futuresSyntheticBond.BaseIcebergParameters.BotTab.StartProgram);
                    return;
                }

                _futuresSyntheticBond.BaseRationingSecurity.ShowConnectorDialog();
                _futuresSyntheticBond.BaseRationingSecurity.SecuritySubscribeEvent += SecurityBaseSubscribeEvent;
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
                _futuresSyntheticBond.FuturesRationingSecurity.SecuritySubscribeEvent -= RationingSecuritySubscribeEvent;
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
                _futuresSyntheticBond.BaseRationingSecurity.SecuritySubscribeEvent -= SecurityBaseSubscribeEvent;
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
                    _futuresSyntheticBond.FuturesUseRationing = false;
                }
                else if (RationingUsingAnotherToolFuturesComboBox.SelectedIndex == 1)
                {
                    _futuresSyntheticBond.FuturesUseRationing = true;
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
                    _futuresSyntheticBond.BaseUseRationing = false;
                }
                else if (RationingUsingAnotherToolBaseComboBox.SelectedIndex == 1)
                {
                    _futuresSyntheticBond.BaseUseRationing = true;
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

                _futuresSyntheticBond.BaseMultiplicator = MultiplicatorBaseTextBox.Text.ToDecimal();
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

                _futuresSyntheticBond.FuturesMultiplicator = MultiplicatorFuturesTextBox.Text.ToDecimal();
            }
            catch (Exception ex)
            {
                ServerMaster.SendNewLogMessage(ex.ToString(), Logging.LogMessageType.Error);
            }
        }

        #endregion
    }
}
