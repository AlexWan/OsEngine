/*
 * Your rights to use code governed by this license http://o-s-a.net/doc/license_simple_engine.pdf
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using OsEngine.Entity;
using OsEngine.Entity.SyntheticBondEntity;
using OsEngine.Language;
using OsEngine.Logging;
using OsEngine.Market;
using OsEngine.OsTrader.Iceberg;
using OsEngine.OsTrader.Panels.Tab.SyntheticBondTab;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms;
using System.Windows.Media;
using System.Windows.Threading;
using ComboBox = System.Windows.Controls.ComboBox;
using TextBox = System.Windows.Controls.TextBox;

namespace OsEngine.OsTrader.Panels.Tab.SynteticBondTab
{
    /// <summary>
    /// Логика взаимодействия для SynteticBondTradeUi.xaml
    /// </summary>
    public partial class SyntheticBondTradeUi : Window
    {
        #region Constructor

        public SyntheticBondTradeUi(SyntheticBondSeries syntheticBondSeries, ref SyntheticBond syntheticBond)
        {
            InitializeComponent();

            _syntheticBondSeries = syntheticBondSeries;

            _syntheticBond = syntheticBond;

            if (_syntheticBond.ActiveScenarios.Count > 0)
            {
                _selectedScenario = _syntheticBond.ActiveScenarios[0];
            }

            CurrentModeLabel.Content = OsLocalization.Trader.Label698;
            NonTradePeriodButton.Content = OsLocalization.Trader.Label473;
            ServerTimeLabel.Content = OsLocalization.Trader.Label722;
            TimeShiftLabel.Content = OsLocalization.Trader.Label746;
            CurrentSpreadLabel.Content = OsLocalization.Trader.Label700 + " (%)";
            MinSpreadLabel.Content = OsLocalization.Trader.Label718 + " (%)";
            MaxSpreadLabel.Content = OsLocalization.Trader.Label719 + " (%)";
            LabelCointegrationDeviation.Content = OsLocalization.Trader.Label713;
            LabelCointegrationLookBack.Content = OsLocalization.Trader.Label714;
            //LabelContangoLookBack.Content = OsLocalization.Trader.Label716;
            //LabelScenario.Content = OsLocalization.Trader.Label731;
            //CreateScriptButton.Content = OsLocalization.Trader.Label732;

            EnterPositionTabItem.Header = OsLocalization.Trader.Label703;
            ExitFromPositionTabItem.Header = OsLocalization.Trader.Label704;
            OpenPositionTabItem.Header = OsLocalization.Trader.Label18;
            ClosePositionTabItem.Header = OsLocalization.Trader.Label19;

            string baseSecurityLabel;
            string futuresSecurityLabel;

            if (_syntheticBondSeries.PatternBaseTab == null ||
                (_syntheticBondSeries.PatternBaseTab != null &&
                (_syntheticBondSeries.PatternBaseTab.Connector == null ||
                (_syntheticBondSeries.PatternBaseTab.Connector != null ||
                string.IsNullOrEmpty(_syntheticBondSeries.PatternBaseTab.Connector.SecurityName)))))
            {
                baseSecurityLabel = OsLocalization.Trader.Label684 + " | None";
            }
            else
            {
                baseSecurityLabel = OsLocalization.Trader.Label684 + " | " + _syntheticBondSeries.PatternBaseTab.Connector.SecurityName.ToString();
            }


            if (_syntheticBond == null ||
            (_syntheticBond.PatternFuturesTab == null ||
            (_syntheticBond.PatternFuturesTab != null &&
            (_syntheticBond.PatternFuturesTab.Connector == null ||
            (_syntheticBond.PatternFuturesTab.Connector != null &&
            (string.IsNullOrEmpty(_syntheticBond.PatternFuturesTab.Connector.SecurityName)))))))
            {
                futuresSecurityLabel = OsLocalization.Trader.Label685 + " | None";
            }
            else
            {
                futuresSecurityLabel = OsLocalization.Trader.Label685 + " | " + _syntheticBond.PatternFuturesTab.Connector.SecurityName.ToString();
            }

            EnterBaseNameLabel.Content = baseSecurityLabel;
            EnterFuturesNameLabel.Content = futuresSecurityLabel;
            EnterVolumeTypeSec1Label.Content = OsLocalization.Trader.Label723;
            EnterVolumeTypeSec2Label.Content = OsLocalization.Trader.Label723;
            EnterTotalVolumeSec1Label.Content = OsLocalization.Trader.Label706;
            EnterTotalVolumeSec2Label.Content = OsLocalization.Trader.Label706;
            EnterOneOrderSec1.Content = OsLocalization.Trader.Label707;
            EnterOneOrderSec2.Content = OsLocalization.Trader.Label707;
            EnterLifetimeOrderSec1Label.Content = OsLocalization.Trader.Label708;
            EnterLifetimeOrderSec2Label.Content = OsLocalization.Trader.Label708;
            EnterOrderFrequencySec1Label.Content = OsLocalization.Trader.Label721;
            EnterOrderFrequencySec2Label.Content = OsLocalization.Trader.Label721;
            EnterPositionSec1Label.Content = OsLocalization.Trader.Label709;
            EnterPositionSec2Label.Content = OsLocalization.Trader.Label709;
            EnterCurrentVolumeSec1Label.Content = OsLocalization.Trader.Label702;
            EnterCurrentVolumeSec2Label.Content = OsLocalization.Trader.Label702;
            StartButton.Content = OsLocalization.Trader.Label455;
            PauseButton.Content = OsLocalization.Trader.Label712;
            DeleteScenarioButton.Content = OsLocalization.Trader.Label39;
            EnterSlippageSec1Label.Content = OsLocalization.Trader.Label715;
            EnterSlippageSec2Label.Content = OsLocalization.Trader.Label715;
            EnterOrderTypeSec1Label.Content = OsLocalization.Trader.Label103;
            EnterOrderTypeSec2Label.Content = OsLocalization.Trader.Label103;

            ExitBaseNameLabel.Content = baseSecurityLabel;
            ExitFuturesNameLabel.Content = futuresSecurityLabel;

            ExitVolumeTypeSec1Label.Content = OsLocalization.Trader.Label723;
            ExitVolumeTypeSec2Label.Content = OsLocalization.Trader.Label723;
            ExitTotalVolumeSec1Label.Content = OsLocalization.Trader.Label706;
            ExitTotalVolumeSec2Label.Content = OsLocalization.Trader.Label706;
            ExitOneOrderSec1.Content = OsLocalization.Trader.Label707;
            ExitOneOrderSec2.Content = OsLocalization.Trader.Label707;
            ExitLifetimeOrderSec1Label.Content = OsLocalization.Trader.Label708;
            ExitLifetimeOrderSec2Label.Content = OsLocalization.Trader.Label708;
            ExitOrderFrequencySec1Label.Content = OsLocalization.Trader.Label721;
            ExitOrderFrequencySec2Label.Content = OsLocalization.Trader.Label721;
            ExitPositionSec1Label.Content = OsLocalization.Trader.Label709;
            ExitPositionSec2Label.Content = OsLocalization.Trader.Label709;
            ExitCurrentVolumeSec1Label.Content = OsLocalization.Trader.Label702;
            ExitCurrentVolumeSec2Label.Content = OsLocalization.Trader.Label702;
            ExitSlippageSec1Label.Content = OsLocalization.Trader.Label715;
            ExitSlippageSec2Label.Content = OsLocalization.Trader.Label715;
            ExitOrderTypeSec1Label.Content = OsLocalization.Trader.Label103;
            ExitOrderTypeSec2Label.Content = OsLocalization.Trader.Label103;

            LogLabel.Content = OsLocalization.Trader.Label332;

            CreateScenarioComboBox();
            //ScenarioComboBox.SelectionChanged += ScenarioComboBox_SelectionChanged;
            UpdateScenarioTextBoxDefault();

            CreateTradeModeComboBox();
            TradeModeComboBox.SelectionChanged += TradeModeComboBox_SelectionChanged;

            CreateEnterVolumeTypeSec1ComboBox();
            EnterVolumeTypeSec1ComboBox.SelectionChanged += EnterVolumeTypeSec1ComboBox_SelectionChanged;

            CreateEnterVolumeTypeSec2ComboBox();
            EnterVolumeTypeSec2ComboBox.SelectionChanged += EnterVolumeTypeSec2ComboBox_SelectionChanged;

            CreateExitVolumeTypeSec1ComboBox();
            ExitVolumeTypeSec1ComboBox.IsEnabled = false;
            ExitVolumeTypeSec1ComboBox.SelectionChanged += ExitVolumeTypeSec1ComboBox_SelectionChanged;

            CreateExitVolumeTypeSec2ComboBox();
            ExitVolumeTypeSec2ComboBox.IsEnabled = false;
            ExitVolumeTypeSec2ComboBox.SelectionChanged += ExitVolumeTypeSec2ComboBox_SelectionChanged;

            CreateEnterOrderTypeSec1ComboBox();
            EnterOrderTypeSec1ComboBox.SelectionChanged += EnterOrderTypeSec1ComboBox_SelectionChanged;

            CreateExitOrderTypeSec1ComboBox();
            ExitOrderTypeSec1ComboBox.SelectionChanged += ExitOrderTypeSec1ComboBox_SelectionChanged;

            CreateEnterOrderTypeSec2ComboBox();
            EnterOrderTypeSec2ComboBox.SelectionChanged += EnterOrderTypeSec2ComboBox_SelectionChanged;

            CreateExitOrderTypeSec2ComboBox();
            ExitOrderTypeSec2ComboBox.SelectionChanged += ExitOrderTypeSec2ComboBox_SelectionChanged;

            CreateEnterOrderPositionSec1ComboBox();
            EnterOrderPositionSec1ComboBox.SelectionChanged += EnterOrderPositionSec1ComboBox_SelectionChanged;

            CreateExitOrderPositionSec1ComboBox();
            ExitOrderPositionSec1ComboBox.SelectionChanged += ExitOrderPositionSec1ComboBox_SelectionChanged;

            CreateEnterOrderPositionSec2ComboBox();
            EnterOrderPositionSec2ComboBox.SelectionChanged += EnterOrderPositionSec2ComboBox_SelectionChanged;

            CreateExitOrderPositionSec2ComboBox();
            ExitOrderPositionSec2ComboBox.SelectionChanged += ExitOrderPositionSec2ComboBox_SelectionChanged;

            MaxSpreadTextBox.Text = _selectedScenario != null ? _selectedScenario.MaxSpread.ToString() : "0";
            MaxSpreadTextBox.TextChanged += MaxSpreadTextBox_TextChanged;

            MinSpreadTextBox.Text = _selectedScenario != null ? _selectedScenario.MinSpread.ToString() : "0";
            MinSpreadTextBox.TextChanged += MinSpreadTextBox_TextChanged;

            //TextBoxContangoLookBack.Text = _syntheticBond.SeparationLength.ToString();
            //TextBoxContangoLookBack.TextChanged += TextBoxContangoLookBack_TextChanged;

            TimeShiftTextBox.Text = _syntheticBond.SelectedScenario.ArbitrationIceberg.TimeShift.ToString();
            TimeShiftTextBox.TextChanged += TimeShiftTextBox_TextChanged;

            TextBoxCointegrationDeviation.Text = _syntheticBond.CointegrationBuilder.CointegrationDeviation.ToString();
            TextBoxCointegrationDeviation.TextChanged += TextBoxCointegrationDeviation_TextChanged;

            TextBoxCointegrationLookBack.Text = _syntheticBond.CointegrationBuilder.CointegrationLookBack.ToString();
            TextBoxCointegrationLookBack.TextChanged += TextBoxCointegrationLookBack_TextChanged;

            EnterTextBoxAssetPortfolioSec1.Text = _selectedScenario?.ArbitrationIceberg.MainLegs[0].AssetPortfolio ?? string.Empty;
            ExitTextBoxAssetPortfolioSec1.Text = EnterTextBoxAssetPortfolioSec1.Text;
            EnterTextBoxAssetPortfolioSec1.TextChanged += EnterTextBoxAssetPortfolioSec1_TextChanged;

            EnterTextBoxAssetPortfolioSec2.Text = _selectedScenario?.ArbitrationIceberg.SecondaryLegs[0].AssetPortfolio ?? string.Empty;
            ExitTextBoxAssetPortfolioSec2.Text = EnterTextBoxAssetPortfolioSec2.Text;
            EnterTextBoxAssetPortfolioSec2.TextChanged += EnterTextBoxAssetPortfolioSec2_TextChanged;

            EnterTotalVolumeSec1TextBox.Text = _selectedScenario?.ArbitrationIceberg.MainLegs[0].EnterOneOrderVolume.ToString() ?? "0";
            ExitTotalVolumeSec1TextBox.Text = EnterTotalVolumeSec1TextBox.Text;
            EnterTotalVolumeSec1TextBox.TextChanged += EnterTotalVolumeSec1TextBox_TextChanged;

            EnterTotalVolumeSec2TextBox.Text = _selectedScenario?.ArbitrationIceberg.SecondaryLegs[0].EnterOneOrderVolume.ToString() ?? "0";
            ExitTotalVolumeSec2TextBox.Text = EnterTotalVolumeSec2TextBox.Text;
            EnterTotalVolumeSec2TextBox.TextChanged += EnterTotalVolumeSec2TextBox_TextChanged;

            EnterOneOrderSec1TextBox.Text = _selectedScenario?.ArbitrationIceberg.MainLegs[0].EnterOneOrderVolume.ToString() ?? "0";
            EnterOneOrderSec1TextBox.TextChanged += EnterOneOrderSec1TextBox_TextChanged;

            ExitOneOrderSec1TextBox.Text = _selectedScenario?.ArbitrationIceberg.MainLegs[0].ExitOneOrderVolume.ToString() ?? "0";
            ExitOneOrderSec1TextBox.TextChanged += ExitOneOrderSec1TextBox_TextChanged;

            EnterOneOrderSec2TextBox.Text = _selectedScenario?.ArbitrationIceberg.SecondaryLegs[0].EnterOneOrderVolume.ToString() ?? "0";
            EnterOneOrderSec2TextBox.TextChanged += EnterOneOrderSec2TextBox_TextChanged;

            ExitOneOrderSec2TextBox.Text = _selectedScenario?.ArbitrationIceberg.SecondaryLegs[0].ExitOneOrderVolume.ToString() ?? "0";
            ExitOneOrderSec2TextBox.TextChanged += ExitOneOrderSec2TextBox_TextChanged;

            EnterSlippageSec1TextBox.Text = _selectedScenario?.ArbitrationIceberg.MainLegs[0].EnterSlippage.ToString() ?? "0";
            EnterSlippageSec1TextBox.TextChanged += EnterSlippageSec1TextBox_TextChanged;

            ExitSlippageSec1TextBox.Text = _selectedScenario?.ArbitrationIceberg.MainLegs[0].ExitSlippage.ToString() ?? "0";
            ExitSlippageSec1TextBox.TextChanged += ExitSlippageSec1TextBox_TextChanged;

            EnterSlippageSec2TextBox.Text = _selectedScenario?.ArbitrationIceberg.SecondaryLegs[0].EnterSlippage.ToString() ?? "0";
            EnterSlippageSec2TextBox.TextChanged += EnterSlippageSec2TextBox_TextChanged;

            ExitSlippageSec2TextBox.Text = _selectedScenario?.ArbitrationIceberg.SecondaryLegs[0].ExitSlippage.ToString() ?? "0";
            ExitSlippageSec2TextBox.TextChanged += ExitSlippageSec2TextBox_TextChanged;

            EnterLifetimeOrderSec1TextBox.Text = _selectedScenario?.ArbitrationIceberg.MainLegs[0].EnterLifetimeOrder.ToString() ?? "0";
            EnterLifetimeOrderSec1TextBox.TextChanged += EnterLifetimeOrderSec1TextBox_TextChanged;

            ExitLifetimeOrderSec1TextBox.Text = _selectedScenario?.ArbitrationIceberg.MainLegs[0].ExitLifetimeOrder.ToString() ?? "0";
            ExitLifetimeOrderSec1TextBox.TextChanged += ExitLifetimeOrderSec1TextBox_TextChanged;

            EnterLifetimeOrderSec2TextBox.Text = _selectedScenario?.ArbitrationIceberg.SecondaryLegs[0].EnterLifetimeOrder.ToString() ?? "0";
            EnterLifetimeOrderSec2TextBox.TextChanged += EnterLifetimeOrderSec2TextBox_TextChanged;

            ExitLifetimeOrderSec2TextBox.Text = _selectedScenario?.ArbitrationIceberg.SecondaryLegs[0].ExitLifetimeOrder.ToString() ?? "0";
            ExitLifetimeOrderSec2TextBox.TextChanged += ExitLifetimeOrderSec2TextBox_TextChanged;

            EnterOrderFrequencySec1TextBox.Text = _selectedScenario?.ArbitrationIceberg.MainLegs[0].EnterOrderFrequency.ToString() ?? "0";
            EnterOrderFrequencySec1TextBox.TextChanged += EnterOrderFrequencySec1TextBox_TextChanged;

            EnterOrderFrequencySec2TextBox.Text = _selectedScenario?.ArbitrationIceberg.SecondaryLegs[0].EnterOrderFrequency.ToString() ?? "0";
            EnterOrderFrequencySec2TextBox.TextChanged += EnterOrderFrequencySec2TextBox_TextChanged;

            ExitOrderFrequencySec1TextBox.Text = _selectedScenario?.ArbitrationIceberg.MainLegs[0].ExitOrderFrequency.ToString() ?? "0";
            ExitOrderFrequencySec1TextBox.TextChanged += ExitOrderFrequencySec1TextBox_TextChanged;

            ExitOrderFrequencySec2TextBox.Text = _selectedScenario?.ArbitrationIceberg.SecondaryLegs[0].ExitOrderFrequency.ToString() ?? "0";
            ExitOrderFrequencySec2TextBox.TextChanged += ExitOrderFrequencySec2TextBox_TextChanged;

            NonTradePeriodButton.Click += NonTradePeriodButton_Click;

            CreatePositionStepsDataGrids();

            SubscribeToScenarioEvents();

            Closed += SyntheticBondOffsetUi_Closed;

            _updateTimer = new DispatcherTimer();
            _updateTimer.Interval = TimeSpan.FromSeconds(1);
            _updateTimer.Tick += UpdateTimer_Tick;
            _updateTimer.Start();

            UpdateTabControlsLockState();

            ValidatePositiveValue(EnterTotalVolumeSec1TextBox);
            ValidatePositiveValue(EnterTotalVolumeSec2TextBox);

            ValidateOneOrderVolume(EnterOneOrderSec1TextBox, EnterTotalVolumeSec1TextBox);
            ValidateOneOrderVolume(EnterOneOrderSec2TextBox, EnterTotalVolumeSec2TextBox);

            ValidateOneOrderVolume(ExitOneOrderSec1TextBox, ExitTotalVolumeSec1TextBox);
            ValidateOneOrderVolume(ExitOneOrderSec2TextBox, ExitTotalVolumeSec2TextBox);

            ValidateLifetimeAndFrequency(EnterOrderTypeSec1ComboBox, EnterLifetimeOrderSec1TextBox, EnterOrderFrequencySec1TextBox);
            ValidateLifetimeAndFrequency(EnterOrderTypeSec2ComboBox, EnterLifetimeOrderSec2TextBox, EnterOrderFrequencySec2TextBox);
            ValidateLifetimeAndFrequency(ExitOrderTypeSec1ComboBox, ExitLifetimeOrderSec1TextBox, ExitOrderFrequencySec1TextBox);
            ValidateLifetimeAndFrequency(ExitOrderTypeSec2ComboBox, ExitLifetimeOrderSec2TextBox, ExitOrderFrequencySec2TextBox);
        }

        private void UpdateTimer_Tick(object sender, EventArgs e)
        {
            try
            {
                decimal sec1Volume = _selectedScenario?.ArbitrationIceberg?.MainLegs[0]?.ArbitrationLegStatistic.CurrentPosition?.OpenVolume ?? 0;
                decimal sec2Volume = _selectedScenario?.ArbitrationIceberg?.SecondaryLegs[0]?.ArbitrationLegStatistic.CurrentPosition?.OpenVolume ?? 0;

                EnterCurrentVolumeSec1TextBox.Text = sec1Volume.ToString();
                EnterCurrentVolumeSec2TextBox.Text = sec2Volume.ToString();
                ExitCurrentVolumeSec1TextBox.Text = sec1Volume.ToString();
                ExitCurrentVolumeSec2TextBox.Text = sec2Volume.ToString();

                if (_syntheticBond.PercentSeparationCandles.Count > 0)
                {
                    CurrentSpreadTextBox.Text = _syntheticBond.PercentSeparationCandles[^1].Value.ToString();
                }
                else
                {
                    CurrentSpreadTextBox.Text = "None";
                }

                if (_syntheticBond == null ||
                    (_syntheticBond != null &&
                   (_syntheticBond.SelectedScenario.ArbitrationIceberg.CurrentTimeServer == DateTime.MinValue ||
                   _syntheticBond.SelectedScenario.ArbitrationIceberg.CurrentTimeServer == DateTime.UnixEpoch)))
                {
                    ServerTimeTextBox.Text = "None";
                    ServerTimeTextBox.Foreground = Brushes.Red;
                }
                else
                {
                    ServerTimeTextBox.Text = _syntheticBond.SelectedScenario.ArbitrationIceberg.CurrentTimeServer.ToString();
                    ServerTimeTextBox.ClearValue(TextBox.ForegroundProperty);
                }

                UpdateTabControlsLockState();
                UpdatePositionStepsDataGrids();

                if (_selectedScenario.ArbitrationIceberg.CurrentStatus == ArbitrationStatus.On)
                {
                    StartButton.Background = Brushes.DarkGreen;
                    PauseButton.Background = null;
                }
                else if (_selectedScenario.ArbitrationIceberg.CurrentStatus == ArbitrationStatus.Pause)
                {
                    StartButton.Background = null;
                    PauseButton.Background = Brushes.DarkOrange;
                }

                RefreshParametersFromIceberg();
            }
            catch (Exception ex)
            {
                ServerMaster.SendNewLogMessage(ex.ToString(), Logging.LogMessageType.Error);
            }
        }

        private void RefreshParametersFromIceberg()
        {
            if (_selectedScenario == null || _selectedScenario.ArbitrationIceberg == null)
            {
                return;
            }

            ArbitrationLeg mainLEg = _selectedScenario.ArbitrationIceberg.MainLegs[0];
            ArbitrationLeg secondaryLeg = _selectedScenario.ArbitrationIceberg.SecondaryLegs[0];

            bool needUpdate = false;

            // Проверяем TextBox-ы
            if (EnterTotalVolumeSec1TextBox.Text != mainLEg.TotalVolume.ToString()
                || EnterTotalVolumeSec2TextBox.Text != secondaryLeg.TotalVolume.ToString()
                || EnterOneOrderSec1TextBox.Text != mainLEg.EnterOneOrderVolume.ToString()
                || EnterOneOrderSec2TextBox.Text != secondaryLeg.EnterOneOrderVolume.ToString()
                || ExitOneOrderSec1TextBox.Text != mainLEg.ExitOneOrderVolume.ToString()
                || ExitOneOrderSec2TextBox.Text != secondaryLeg.ExitOneOrderVolume.ToString()
                || EnterSlippageSec1TextBox.Text != mainLEg.EnterSlippage.ToString()
                || EnterSlippageSec2TextBox.Text != secondaryLeg.EnterSlippage.ToString()
                || ExitSlippageSec1TextBox.Text != mainLEg.ExitSlippage.ToString()
                || ExitSlippageSec2TextBox.Text != secondaryLeg.ExitSlippage.ToString()
                || EnterLifetimeOrderSec1TextBox.Text != mainLEg.EnterLifetimeOrder.ToString()
                || EnterLifetimeOrderSec2TextBox.Text != secondaryLeg.EnterLifetimeOrder.ToString()
                || ExitLifetimeOrderSec1TextBox.Text != mainLEg.ExitLifetimeOrder.ToString()
                || ExitLifetimeOrderSec2TextBox.Text != secondaryLeg.ExitLifetimeOrder.ToString()
                || EnterOrderFrequencySec1TextBox.Text != mainLEg.EnterOrderFrequency.ToString()
                || EnterOrderFrequencySec2TextBox.Text != secondaryLeg.EnterOrderFrequency.ToString()
                || ExitOrderFrequencySec1TextBox.Text != mainLEg.ExitOrderFrequency.ToString()
                || ExitOrderFrequencySec2TextBox.Text != secondaryLeg.ExitOrderFrequency.ToString()
                || EnterTextBoxAssetPortfolioSec1.Text != (mainLEg.AssetPortfolio ?? string.Empty)
                || EnterTextBoxAssetPortfolioSec2.Text != (secondaryLeg.AssetPortfolio ?? string.Empty))
            {
                needUpdate = true;
            }

            // Проверяем ComboBox-ы OrderType
            int enterOrderTypeSec1Index = mainLEg.EnterOrderType == OrderPriceType.Market ? 0 : 1;
            int enterOrderTypeSec2Index = secondaryLeg.EnterOrderType == OrderPriceType.Market ? 0 : 1;
            int exitOrderTypeSec1Index = mainLEg.ExitOrderType == OrderPriceType.Market ? 0 : 1;
            int exitOrderTypeSec2Index = secondaryLeg.ExitOrderType == OrderPriceType.Market ? 0 : 1;

            if (EnterOrderTypeSec1ComboBox.SelectedIndex != enterOrderTypeSec1Index
                || EnterOrderTypeSec2ComboBox.SelectedIndex != enterOrderTypeSec2Index
                || ExitOrderTypeSec1ComboBox.SelectedIndex != exitOrderTypeSec1Index
                || ExitOrderTypeSec2ComboBox.SelectedIndex != exitOrderTypeSec2Index)
            {
                needUpdate = true;
            }

            // Проверяем ComboBox-ы VolumeType
            int enterVolumeTypeSec1Index = GetVolumeTypeIndex(mainLEg.VolumeType);
            int enterVolumeTypeSec2Index = GetVolumeTypeIndex(secondaryLeg.VolumeType);

            if (EnterVolumeTypeSec1ComboBox.SelectedIndex != enterVolumeTypeSec1Index
                || EnterVolumeTypeSec2ComboBox.SelectedIndex != enterVolumeTypeSec2Index)
            {
                needUpdate = true;
            }

            // Проверяем ComboBox-ы OrderPosition
            int enterOrderPosSec1Index = GetOrderPositionIndex(mainLEg.EnterOrderPosition);
            int enterOrderPosSec2Index = GetOrderPositionIndex(secondaryLeg.EnterOrderPosition);
            int exitOrderPosSec1Index = GetOrderPositionIndex(mainLEg.ExitOrderPosition);
            int exitOrderPosSec2Index = GetOrderPositionIndex(secondaryLeg.ExitOrderPosition);

            if (EnterOrderPositionSec1ComboBox.SelectedIndex != enterOrderPosSec1Index
                || EnterOrderPositionSec2ComboBox.SelectedIndex != enterOrderPosSec2Index
                || ExitOrderPositionSec1ComboBox.SelectedIndex != exitOrderPosSec1Index
                || ExitOrderPositionSec2ComboBox.SelectedIndex != exitOrderPosSec2Index)
            {
                needUpdate = true;
            }

            // Проверяем ComboBox TradeMode
            int tradeModeIndex = GetTradeModeIndex(_selectedScenario.ArbitrationIceberg.CurrentMode);

            if (TradeModeComboBox.SelectedIndex != tradeModeIndex)
            {
                needUpdate = true;
            }

            if (needUpdate == false)
            {
                return;
            }

            _isUpdatingUi = true;

            // TextBox-ы
            EnterTotalVolumeSec1TextBox.Text = mainLEg.TotalVolume.ToString();
            ExitTotalVolumeSec1TextBox.Text = mainLEg.TotalVolume.ToString();
            EnterTotalVolumeSec2TextBox.Text = secondaryLeg.TotalVolume.ToString();
            ExitTotalVolumeSec2TextBox.Text = secondaryLeg.TotalVolume.ToString();

            EnterOneOrderSec1TextBox.Text = mainLEg.EnterOneOrderVolume.ToString();
            ExitOneOrderSec1TextBox.Text = mainLEg.ExitOneOrderVolume.ToString();
            EnterOneOrderSec2TextBox.Text = secondaryLeg.EnterOneOrderVolume.ToString();
            ExitOneOrderSec2TextBox.Text = secondaryLeg.ExitOneOrderVolume.ToString();

            EnterSlippageSec1TextBox.Text = mainLEg.EnterSlippage.ToString();
            ExitSlippageSec1TextBox.Text = mainLEg.ExitSlippage.ToString();
            EnterSlippageSec2TextBox.Text = secondaryLeg.EnterSlippage.ToString();
            ExitSlippageSec2TextBox.Text = secondaryLeg.ExitSlippage.ToString();

            EnterLifetimeOrderSec1TextBox.Text = mainLEg.EnterLifetimeOrder.ToString();
            ExitLifetimeOrderSec1TextBox.Text = mainLEg.ExitLifetimeOrder.ToString();
            EnterLifetimeOrderSec2TextBox.Text = secondaryLeg.EnterLifetimeOrder.ToString();
            ExitLifetimeOrderSec2TextBox.Text = secondaryLeg.ExitLifetimeOrder.ToString();

            EnterOrderFrequencySec1TextBox.Text = mainLEg.EnterOrderFrequency.ToString();
            ExitOrderFrequencySec1TextBox.Text = mainLEg.ExitOrderFrequency.ToString();
            EnterOrderFrequencySec2TextBox.Text = secondaryLeg.EnterOrderFrequency.ToString();
            ExitOrderFrequencySec2TextBox.Text = secondaryLeg.ExitOrderFrequency.ToString();

            EnterTextBoxAssetPortfolioSec1.Text = mainLEg.AssetPortfolio ?? string.Empty;
            ExitTextBoxAssetPortfolioSec1.Text = mainLEg.AssetPortfolio ?? string.Empty;
            EnterTextBoxAssetPortfolioSec2.Text = secondaryLeg.AssetPortfolio ?? string.Empty;
            ExitTextBoxAssetPortfolioSec2.Text = secondaryLeg.AssetPortfolio ?? string.Empty;

            // ComboBox-ы OrderType
            EnterOrderTypeSec1ComboBox.SelectedIndex = enterOrderTypeSec1Index;
            EnterOrderTypeSec2ComboBox.SelectedIndex = enterOrderTypeSec2Index;
            ExitOrderTypeSec1ComboBox.SelectedIndex = exitOrderTypeSec1Index;
            ExitOrderTypeSec2ComboBox.SelectedIndex = exitOrderTypeSec2Index;

            // ComboBox-ы VolumeType
            EnterVolumeTypeSec1ComboBox.SelectedIndex = enterVolumeTypeSec1Index;
            ExitVolumeTypeSec1ComboBox.SelectedIndex = enterVolumeTypeSec1Index;
            EnterVolumeTypeSec2ComboBox.SelectedIndex = enterVolumeTypeSec2Index;
            ExitVolumeTypeSec2ComboBox.SelectedIndex = enterVolumeTypeSec2Index;

            // ComboBox-ы OrderPosition
            EnterOrderPositionSec1ComboBox.SelectedIndex = enterOrderPosSec1Index;
            EnterOrderPositionSec2ComboBox.SelectedIndex = enterOrderPosSec2Index;
            ExitOrderPositionSec1ComboBox.SelectedIndex = exitOrderPosSec1Index;
            ExitOrderPositionSec2ComboBox.SelectedIndex = exitOrderPosSec2Index;

            // ComboBox TradeMode
            TradeModeComboBox.SelectedIndex = tradeModeIndex;

            _isUpdatingUi = false;

            // Валидация цветов
            ValidatePositiveValue(EnterTotalVolumeSec1TextBox);
            ValidatePositiveValue(EnterTotalVolumeSec2TextBox);

            ValidateOneOrderVolume(EnterOneOrderSec1TextBox, EnterTotalVolumeSec1TextBox);
            ValidateOneOrderVolume(EnterOneOrderSec2TextBox, EnterTotalVolumeSec2TextBox);
            ValidateOneOrderVolume(ExitOneOrderSec1TextBox, ExitTotalVolumeSec1TextBox);
            ValidateOneOrderVolume(ExitOneOrderSec2TextBox, ExitTotalVolumeSec2TextBox);

            ValidateLifetimeAndFrequency(EnterOrderTypeSec1ComboBox, EnterLifetimeOrderSec1TextBox, EnterOrderFrequencySec1TextBox);
            ValidateLifetimeAndFrequency(EnterOrderTypeSec2ComboBox, EnterLifetimeOrderSec2TextBox, EnterOrderFrequencySec2TextBox);
            ValidateLifetimeAndFrequency(ExitOrderTypeSec1ComboBox, ExitLifetimeOrderSec1TextBox, ExitOrderFrequencySec1TextBox);
            ValidateLifetimeAndFrequency(ExitOrderTypeSec2ComboBox, ExitLifetimeOrderSec2TextBox, ExitOrderFrequencySec2TextBox);
        }

        private int GetVolumeTypeIndex(VolumeType volumeType)
        {
            if (volumeType == VolumeType.ContractCurrency)
            {
                return 1;
            }

            if (volumeType == VolumeType.DepositPercent)
            {
                return 2;
            }

            return 0;
        }

        private int GetOrderPositionIndex(SynteticBondOrderPosition orderPosition)
        {
            if (orderPosition == SynteticBondOrderPosition.Bid)
            {
                return 1;
            }

            if (orderPosition == SynteticBondOrderPosition.Middle)
            {
                return 2;
            }

            return 0;
        }

        private int GetTradeModeIndex(ArbitrationMode mode)
        {
            if (mode == ArbitrationMode.OpenSellFirstBuySecond)
            {
                return 1;
            }

            if (mode == ArbitrationMode.CloseScript)
            {
                return 2;
            }

            if (mode == ArbitrationMode.CloseAllScripts)
            {
                return 3;
            }

            return 0;
        }

        private void UpdateTabControlsLockState()
        {
            bool isLocked = _selectedScenario?.ArbitrationIceberg?.CurrentStatus == ArbitrationStatus.On;

            ExitTextBoxAssetPortfolioSec1.IsEnabled = false;
            ExitTextBoxAssetPortfolioSec2.IsEnabled = false;
            EnterCurrentVolumeSec1TextBox.IsEnabled = false;
            EnterCurrentVolumeSec2TextBox.IsEnabled = false;
            ExitCurrentVolumeSec1TextBox.IsEnabled = false;
            ExitCurrentVolumeSec2TextBox.IsEnabled = false;
            ExitTotalVolumeSec1TextBox.IsEnabled = false;
            ExitTotalVolumeSec2TextBox.IsEnabled = false;

            bool enterSec1IsLimit = EnterOrderTypeSec1ComboBox.SelectedIndex == 1;
            bool enterSec2IsLimit = EnterOrderTypeSec2ComboBox.SelectedIndex == 1;
            bool exitSec1IsLimit = ExitOrderTypeSec1ComboBox.SelectedIndex == 1;
            bool exitSec2IsLimit = ExitOrderTypeSec2ComboBox.SelectedIndex == 1;

            EnterTextBoxAssetPortfolioSec1.IsEnabled = !isLocked;
            EnterTextBoxAssetPortfolioSec2.IsEnabled = !isLocked;
            EnterVolumeTypeSec1ComboBox.IsEnabled = !isLocked;
            EnterTotalVolumeSec1TextBox.IsEnabled = !isLocked;
            EnterOneOrderSec1TextBox.IsEnabled = !isLocked;
            EnterOrderTypeSec1ComboBox.IsEnabled = !isLocked;
            EnterOrderPositionSec1ComboBox.IsEnabled = !isLocked && enterSec1IsLimit;
            EnterSlippageSec1TextBox.IsEnabled = !isLocked && enterSec1IsLimit;
            EnterLifetimeOrderSec1TextBox.IsEnabled = !isLocked && enterSec1IsLimit;
            EnterOrderFrequencySec1TextBox.IsEnabled = !isLocked;

            EnterVolumeTypeSec2ComboBox.IsEnabled = !isLocked;
            EnterTotalVolumeSec2TextBox.IsEnabled = !isLocked;
            EnterOneOrderSec2TextBox.IsEnabled = !isLocked;
            EnterOrderTypeSec2ComboBox.IsEnabled = !isLocked;
            EnterOrderPositionSec2ComboBox.IsEnabled = !isLocked && enterSec2IsLimit;
            EnterSlippageSec2TextBox.IsEnabled = !isLocked && enterSec2IsLimit;
            EnterLifetimeOrderSec2TextBox.IsEnabled = !isLocked && enterSec2IsLimit;
            EnterOrderFrequencySec2TextBox.IsEnabled = !isLocked;

            ExitOneOrderSec1TextBox.IsEnabled = !isLocked;
            ExitOrderTypeSec1ComboBox.IsEnabled = !isLocked;
            ExitOrderPositionSec1ComboBox.IsEnabled = !isLocked && exitSec1IsLimit;
            ExitSlippageSec1TextBox.IsEnabled = !isLocked && exitSec1IsLimit;
            ExitLifetimeOrderSec1TextBox.IsEnabled = !isLocked && exitSec1IsLimit;
            ExitOrderFrequencySec1TextBox.IsEnabled = !isLocked;

            ExitOneOrderSec2TextBox.IsEnabled = !isLocked;
            ExitOrderTypeSec2ComboBox.IsEnabled = !isLocked;
            ExitOrderPositionSec2ComboBox.IsEnabled = !isLocked && exitSec2IsLimit;
            ExitSlippageSec2TextBox.IsEnabled = !isLocked && exitSec2IsLimit;
            ExitLifetimeOrderSec2TextBox.IsEnabled = !isLocked && exitSec2IsLimit;
            ExitOrderFrequencySec2TextBox.IsEnabled = !isLocked;
        }


        private void CreateExitOrderPositionSec2ComboBox()
        {
            ExitOrderPositionSec2ComboBox.Items.Clear();

            ExitOrderPositionSec2ComboBox.Items.Add(new ComboBoxItem
            {
                Content = SynteticBondOrderPosition.Ask.ToString(),
                Name = SynteticBondOrderPosition.Ask.ToString()
            });

            ExitOrderPositionSec2ComboBox.Items.Add(new ComboBoxItem
            {
                Content = SynteticBondOrderPosition.Bid.ToString(),
                Name = SynteticBondOrderPosition.Bid.ToString()
            });

            ExitOrderPositionSec2ComboBox.Items.Add(new ComboBoxItem
            {
                Content = SynteticBondOrderPosition.Middle.ToString(),
                Name = SynteticBondOrderPosition.Middle.ToString()
            });

            if (_selectedScenario.ArbitrationIceberg.SecondaryLegs[0].ExitOrderPosition == SynteticBondOrderPosition.Ask)
            {
                ExitOrderPositionSec2ComboBox.SelectedIndex = 0;
            }
            else if (_selectedScenario.ArbitrationIceberg.SecondaryLegs[0].ExitOrderPosition == SynteticBondOrderPosition.Bid)
            {
                ExitOrderPositionSec2ComboBox.SelectedIndex = 1;
            }
            else if (_selectedScenario.ArbitrationIceberg.SecondaryLegs[0].ExitOrderPosition == SynteticBondOrderPosition.Middle)
            {
                ExitOrderPositionSec2ComboBox.SelectedIndex = 2;
            }
        }

        private void CreateExitOrderPositionSec1ComboBox()
        {
            ExitOrderPositionSec1ComboBox.Items.Clear();

            ExitOrderPositionSec1ComboBox.Items.Add(new ComboBoxItem
            {
                Content = SynteticBondOrderPosition.Ask.ToString(),
                Name = SynteticBondOrderPosition.Ask.ToString()
            });

            ExitOrderPositionSec1ComboBox.Items.Add(new ComboBoxItem
            {
                Content = SynteticBondOrderPosition.Bid.ToString(),
                Name = SynteticBondOrderPosition.Bid.ToString()
            });

            ExitOrderPositionSec1ComboBox.Items.Add(new ComboBoxItem
            {
                Content = SynteticBondOrderPosition.Middle.ToString(),
                Name = SynteticBondOrderPosition.Middle.ToString()
            });

            if (_selectedScenario.ArbitrationIceberg.MainLegs[0].ExitOrderPosition == SynteticBondOrderPosition.Ask)
            {
                ExitOrderPositionSec1ComboBox.SelectedIndex = 0;
            }
            else if (_selectedScenario.ArbitrationIceberg.MainLegs[0].ExitOrderPosition == SynteticBondOrderPosition.Bid)
            {
                ExitOrderPositionSec1ComboBox.SelectedIndex = 1;
            }
            else if (_selectedScenario.ArbitrationIceberg.MainLegs[0].ExitOrderPosition == SynteticBondOrderPosition.Middle)
            {
                ExitOrderPositionSec1ComboBox.SelectedIndex = 2;
            }

            if (_selectedScenario.ArbitrationIceberg.CurrentStatus == ArbitrationStatus.On)
            {
                StartButton.Background = Brushes.DarkGreen;
                PauseButton.Background = null;
            }
            else if (_selectedScenario.ArbitrationIceberg.CurrentStatus == ArbitrationStatus.Pause)
            {
                StartButton.Background = null;
                PauseButton.Background = Brushes.DarkOrange;
            }
        }

        private void CreateEnterOrderPositionSec2ComboBox()
        {
            EnterOrderPositionSec2ComboBox.Items.Clear();

            EnterOrderPositionSec2ComboBox.Items.Add(new ComboBoxItem
            {
                Content = SynteticBondOrderPosition.Ask.ToString(),
                Name = SynteticBondOrderPosition.Ask.ToString()
            });

            EnterOrderPositionSec2ComboBox.Items.Add(new ComboBoxItem
            {
                Content = SynteticBondOrderPosition.Bid.ToString(),
                Name = SynteticBondOrderPosition.Bid.ToString()
            });

            EnterOrderPositionSec2ComboBox.Items.Add(new ComboBoxItem
            {
                Content = SynteticBondOrderPosition.Middle.ToString(),
                Name = SynteticBondOrderPosition.Middle.ToString()
            });

            if (_selectedScenario.ArbitrationIceberg.SecondaryLegs[0].EnterOrderPosition == SynteticBondOrderPosition.Ask)
            {
                EnterOrderPositionSec2ComboBox.SelectedIndex = 0;
            }
            else if (_selectedScenario.ArbitrationIceberg.SecondaryLegs[0].EnterOrderPosition == SynteticBondOrderPosition.Bid)
            {
                EnterOrderPositionSec2ComboBox.SelectedIndex = 1;
            }
            else if (_selectedScenario.ArbitrationIceberg.SecondaryLegs[0].EnterOrderPosition == SynteticBondOrderPosition.Middle)
            {
                EnterOrderPositionSec2ComboBox.SelectedIndex = 2;
            }
        }

        private void CreateEnterOrderPositionSec1ComboBox()
        {
            EnterOrderPositionSec1ComboBox.Items.Clear();

            EnterOrderPositionSec1ComboBox.Items.Add(new ComboBoxItem
            {
                Content = SynteticBondOrderPosition.Ask.ToString(),
                Name = SynteticBondOrderPosition.Ask.ToString()
            });

            EnterOrderPositionSec1ComboBox.Items.Add(new ComboBoxItem
            {
                Content = SynteticBondOrderPosition.Bid.ToString(),
                Name = SynteticBondOrderPosition.Bid.ToString()
            });

            EnterOrderPositionSec1ComboBox.Items.Add(new ComboBoxItem
            {
                Content = SynteticBondOrderPosition.Middle.ToString(),
                Name = SynteticBondOrderPosition.Middle.ToString()
            });

            if (_selectedScenario.ArbitrationIceberg.MainLegs[0].EnterOrderPosition == SynteticBondOrderPosition.Ask)
            {
                EnterOrderPositionSec1ComboBox.SelectedIndex = 0;
            }
            else if (_selectedScenario.ArbitrationIceberg.MainLegs[0].EnterOrderPosition == SynteticBondOrderPosition.Bid)
            {
                EnterOrderPositionSec1ComboBox.SelectedIndex = 1;
            }
            else if (_selectedScenario.ArbitrationIceberg.MainLegs[0].EnterOrderPosition == SynteticBondOrderPosition.Middle)
            {
                EnterOrderPositionSec1ComboBox.SelectedIndex = 2;
            }
        }

        private void CreateExitOrderTypeSec2ComboBox()
        {
            ExitOrderTypeSec2ComboBox.Items.Clear();

            ExitOrderTypeSec2ComboBox.Items.Add(new ComboBoxItem
            {
                Content = OrderPriceType.Market.ToString(),
                Name = OrderPriceType.Market.ToString()
            });

            ExitOrderTypeSec2ComboBox.Items.Add(new ComboBoxItem
            {
                Content = OrderPriceType.Limit.ToString(),
                Name = OrderPriceType.Limit.ToString()
            });

            if (_selectedScenario.ArbitrationIceberg.SecondaryLegs[0].ExitOrderType == OrderPriceType.Market)
            {
                ExitOrderTypeSec2ComboBox.SelectedIndex = 0;
            }
            else if (_selectedScenario.ArbitrationIceberg.SecondaryLegs[0].ExitOrderType == OrderPriceType.Limit)
            {
                ExitOrderTypeSec2ComboBox.SelectedIndex = 1;
            }
        }

        private void CreateExitOrderTypeSec1ComboBox()
        {
            ExitOrderTypeSec1ComboBox.Items.Clear();

            ExitOrderTypeSec1ComboBox.Items.Add(new ComboBoxItem
            {
                Content = OrderPriceType.Market.ToString(),
                Name = OrderPriceType.Market.ToString()
            });

            ExitOrderTypeSec1ComboBox.Items.Add(new ComboBoxItem
            {
                Content = OrderPriceType.Limit.ToString(),
                Name = OrderPriceType.Limit.ToString()
            });

            if (_selectedScenario.ArbitrationIceberg.MainLegs[0].ExitOrderType == OrderPriceType.Market)
            {
                ExitOrderTypeSec1ComboBox.SelectedIndex = 0;
            }
            else if (_selectedScenario.ArbitrationIceberg.MainLegs[0].ExitOrderType == OrderPriceType.Limit)
            {
                ExitOrderTypeSec1ComboBox.SelectedIndex = 1;
            }
        }

        private void CreateEnterOrderTypeSec2ComboBox()
        {
            EnterOrderTypeSec2ComboBox.Items.Clear();

            EnterOrderTypeSec2ComboBox.Items.Add(new ComboBoxItem
            {
                Content = OrderPriceType.Market.ToString(),
                Name = OrderPriceType.Market.ToString()
            });

            EnterOrderTypeSec2ComboBox.Items.Add(new ComboBoxItem
            {
                Content = OrderPriceType.Limit.ToString(),
                Name = OrderPriceType.Limit.ToString()
            });

            if (_selectedScenario.ArbitrationIceberg.SecondaryLegs[0].EnterOrderType == OrderPriceType.Market)
            {
                EnterOrderTypeSec2ComboBox.SelectedIndex = 0;
            }
            else if (_selectedScenario.ArbitrationIceberg.SecondaryLegs[0].EnterOrderType == OrderPriceType.Limit)
            {
                EnterOrderTypeSec2ComboBox.SelectedIndex = 1;
            }
        }

        private void CreateEnterOrderTypeSec1ComboBox()
        {
            EnterOrderTypeSec1ComboBox.Items.Clear();

            EnterOrderTypeSec1ComboBox.Items.Add(new ComboBoxItem
            {
                Content = OrderPriceType.Market.ToString(),
                Name = OrderPriceType.Market.ToString()
            });

            EnterOrderTypeSec1ComboBox.Items.Add(new ComboBoxItem
            {
                Content = OrderPriceType.Limit.ToString(),
                Name = OrderPriceType.Limit.ToString()
            });

            if (_selectedScenario.ArbitrationIceberg.MainLegs[0].EnterOrderType == OrderPriceType.Market)
            {
                EnterOrderTypeSec1ComboBox.SelectedIndex = 0;
            }
            else if (_selectedScenario.ArbitrationIceberg.MainLegs[0].EnterOrderType == OrderPriceType.Limit)
            {
                EnterOrderTypeSec1ComboBox.SelectedIndex = 1;
            }
        }

        private void CreateEnterVolumeTypeSec1ComboBox()
        {
            EnterVolumeTypeSec1ComboBox.Items.Clear();

            EnterVolumeTypeSec1ComboBox.Items.Add(new ComboBoxItem
            {
                Content = OsLocalization.Trader.Label724,
                Name = VolumeType.Contracts.ToString()
            });

            EnterVolumeTypeSec1ComboBox.Items.Add(new ComboBoxItem
            {
                Content = OsLocalization.Trader.Label725,
                Name = VolumeType.ContractCurrency.ToString()
            });

            EnterVolumeTypeSec1ComboBox.Items.Add(new ComboBoxItem
            {
                Content = OsLocalization.Trader.Label726,
                Name = VolumeType.DepositPercent.ToString()
            });

            if (_selectedScenario.ArbitrationIceberg.MainLegs[0].VolumeType == VolumeType.Contracts)
            {
                EnterVolumeTypeSec1ComboBox.SelectedIndex = 0;
            }
            else if (_selectedScenario.ArbitrationIceberg.MainLegs[0].VolumeType == VolumeType.ContractCurrency)
            {
                EnterVolumeTypeSec1ComboBox.SelectedIndex = 1;
            }
            else if (_selectedScenario.ArbitrationIceberg.MainLegs[0].VolumeType == VolumeType.DepositPercent)
            {
                EnterVolumeTypeSec1ComboBox.SelectedIndex = 2;
            }
        }

        private void CreateEnterVolumeTypeSec2ComboBox()
        {
            EnterVolumeTypeSec2ComboBox.Items.Clear();

            EnterVolumeTypeSec2ComboBox.Items.Add(new ComboBoxItem
            {
                Content = OsLocalization.Trader.Label724,
                Name = VolumeType.Contracts.ToString()
            });

            EnterVolumeTypeSec2ComboBox.Items.Add(new ComboBoxItem
            {
                Content = OsLocalization.Trader.Label725,
                Name = VolumeType.ContractCurrency.ToString()
            });

            EnterVolumeTypeSec2ComboBox.Items.Add(new ComboBoxItem
            {
                Content = OsLocalization.Trader.Label726,
                Name = VolumeType.DepositPercent.ToString()
            });

            if (_selectedScenario.ArbitrationIceberg.SecondaryLegs[0].VolumeType == VolumeType.Contracts)
            {
                EnterVolumeTypeSec2ComboBox.SelectedIndex = 0;
            }
            else if (_selectedScenario.ArbitrationIceberg.SecondaryLegs[0].VolumeType == VolumeType.ContractCurrency)
            {
                EnterVolumeTypeSec2ComboBox.SelectedIndex = 1;
            }
            else if (_selectedScenario.ArbitrationIceberg.SecondaryLegs[0].VolumeType == VolumeType.DepositPercent)
            {
                EnterVolumeTypeSec2ComboBox.SelectedIndex = 2;
            }
        }

        private void CreateExitVolumeTypeSec1ComboBox()
        {
            ExitVolumeTypeSec1ComboBox.Items.Clear();

            ExitVolumeTypeSec1ComboBox.Items.Add(new ComboBoxItem
            {
                Content = OsLocalization.Trader.Label724,
                Name = VolumeType.Contracts.ToString()
            });

            ExitVolumeTypeSec1ComboBox.Items.Add(new ComboBoxItem
            {
                Content = OsLocalization.Trader.Label725,
                Name = VolumeType.ContractCurrency.ToString()
            });

            ExitVolumeTypeSec1ComboBox.Items.Add(new ComboBoxItem
            {
                Content = OsLocalization.Trader.Label726,
                Name = VolumeType.DepositPercent.ToString()
            });

            if (_selectedScenario.ArbitrationIceberg.MainLegs[0].VolumeType == VolumeType.Contracts)
            {
                ExitVolumeTypeSec1ComboBox.SelectedIndex = 0;
            }
            else if (_selectedScenario.ArbitrationIceberg.MainLegs[0].VolumeType == VolumeType.ContractCurrency)
            {
                ExitVolumeTypeSec1ComboBox.SelectedIndex = 1;
            }
            else if (_selectedScenario.ArbitrationIceberg.MainLegs[0].VolumeType == VolumeType.DepositPercent)
            {
                ExitVolumeTypeSec1ComboBox.SelectedIndex = 2;
            }
        }

        private void CreateExitVolumeTypeSec2ComboBox()
        {
            ExitVolumeTypeSec2ComboBox.Items.Clear();

            ExitVolumeTypeSec2ComboBox.Items.Add(new ComboBoxItem
            {
                Content = OsLocalization.Trader.Label724,
                Name = VolumeType.Contracts.ToString()
            });

            ExitVolumeTypeSec2ComboBox.Items.Add(new ComboBoxItem
            {
                Content = OsLocalization.Trader.Label725,
                Name = VolumeType.ContractCurrency.ToString()
            });

            ExitVolumeTypeSec2ComboBox.Items.Add(new ComboBoxItem
            {
                Content = OsLocalization.Trader.Label726,
                Name = VolumeType.DepositPercent.ToString()
            });

            if (_selectedScenario.ArbitrationIceberg.SecondaryLegs[0].VolumeType == VolumeType.Contracts)
            {
                ExitVolumeTypeSec2ComboBox.SelectedIndex = 0;
            }
            else if (_selectedScenario.ArbitrationIceberg.SecondaryLegs[0].VolumeType == VolumeType.ContractCurrency)
            {
                ExitVolumeTypeSec2ComboBox.SelectedIndex = 1;
            }
            else if (_selectedScenario.ArbitrationIceberg.SecondaryLegs[0].VolumeType == VolumeType.DepositPercent)
            {
                ExitVolumeTypeSec2ComboBox.SelectedIndex = 2;
            }
        }

        private void CreateTradeModeComboBox()
        {
            try
            {
                TradeModeComboBox.Items.Clear();

                TradeModeComboBox.Items.Add(new ComboBoxItem
                {
                    Content = OsLocalization.Trader.Label727,
                    Name = ArbitrationMode.OpenBuyFirstSellSecond.ToString()
                });

                TradeModeComboBox.Items.Add(new ComboBoxItem
                {
                    Content = OsLocalization.Trader.Label728,
                    Name = ArbitrationMode.OpenSellFirstBuySecond.ToString()
                });

                TradeModeComboBox.Items.Add(new ComboBoxItem
                {
                    Content = OsLocalization.Trader.Label729,
                    Name = ArbitrationMode.CloseScript.ToString()
                });

                TradeModeComboBox.Items.Add(new ComboBoxItem
                {
                    Content = OsLocalization.Trader.Label730,
                    Name = ArbitrationMode.CloseAllScripts.ToString()
                });

                if (_selectedScenario.ArbitrationIceberg.CurrentMode == ArbitrationMode.OpenBuyFirstSellSecond)
                {
                    TradeModeComboBox.SelectedIndex = 0;
                }
                else if (_selectedScenario.ArbitrationIceberg.CurrentMode == ArbitrationMode.OpenSellFirstBuySecond)
                {
                    TradeModeComboBox.SelectedIndex = 1;
                }
                else if (_selectedScenario.ArbitrationIceberg.CurrentMode == ArbitrationMode.CloseScript)
                {
                    TradeModeComboBox.SelectedIndex = 2;
                }
                else if (_selectedScenario.ArbitrationIceberg.CurrentMode == ArbitrationMode.CloseAllScripts)
                {
                    TradeModeComboBox.SelectedIndex = 3;
                }
            }
            catch (Exception ex)
            {
                ServerMaster.SendNewLogMessage(ex.ToString(), Logging.LogMessageType.Error);
            }
        }

        private void SyntheticBondOffsetUi_Closed(object sender, EventArgs e)
        {
            try
            {
                Closed -= SyntheticBondOffsetUi_Closed;

                _updateTimer.Stop();
                _updateTimer.Tick -= UpdateTimer_Tick;
                //ScenarioComboBox.SelectionChanged -= ScenarioComboBox_SelectionChanged;
                TimeShiftTextBox.TextChanged -= TimeShiftTextBox_TextChanged;
                MinSpreadTextBox.TextChanged -= MinSpreadTextBox_TextChanged;
                MaxSpreadTextBox.TextChanged -= MaxSpreadTextBox_TextChanged;
                //TextBoxContangoLookBack.TextChanged -= TextBoxContangoLookBack_TextChanged;
                TextBoxCointegrationLookBack.TextChanged -= TextBoxCointegrationLookBack_TextChanged;
                TextBoxCointegrationDeviation.TextChanged -= TextBoxCointegrationDeviation_TextChanged;
                EnterTextBoxAssetPortfolioSec1.TextChanged -= EnterTextBoxAssetPortfolioSec1_TextChanged;
                EnterTextBoxAssetPortfolioSec2.TextChanged -= EnterTextBoxAssetPortfolioSec2_TextChanged;
                NonTradePeriodButton.Click -= NonTradePeriodButton_Click;
                TradeModeComboBox.SelectionChanged -= TradeModeComboBox_SelectionChanged;
                EnterVolumeTypeSec1ComboBox.SelectionChanged -= EnterVolumeTypeSec1ComboBox_SelectionChanged;
                EnterVolumeTypeSec2ComboBox.SelectionChanged -= EnterVolumeTypeSec2ComboBox_SelectionChanged;
                ExitVolumeTypeSec1ComboBox.SelectionChanged -= ExitVolumeTypeSec1ComboBox_SelectionChanged;
                ExitVolumeTypeSec2ComboBox.SelectionChanged -= ExitVolumeTypeSec2ComboBox_SelectionChanged;
                EnterTotalVolumeSec1TextBox.TextChanged -= EnterTotalVolumeSec1TextBox_TextChanged;
                EnterTotalVolumeSec2TextBox.TextChanged -= EnterTotalVolumeSec2TextBox_TextChanged;
                EnterOneOrderSec1TextBox.TextChanged -= EnterOneOrderSec1TextBox_TextChanged;
                EnterOneOrderSec2TextBox.TextChanged -= EnterOneOrderSec2TextBox_TextChanged;
                EnterOrderTypeSec1ComboBox.SelectionChanged -= EnterOrderTypeSec1ComboBox_SelectionChanged;
                ExitOneOrderSec1TextBox.TextChanged -= ExitOneOrderSec1TextBox_TextChanged;
                ExitOneOrderSec2TextBox.TextChanged -= ExitOneOrderSec2TextBox_TextChanged;
                EnterOrderTypeSec2ComboBox.SelectionChanged -= EnterOrderTypeSec2ComboBox_SelectionChanged;
                ExitOrderTypeSec1ComboBox.SelectionChanged -= ExitOrderTypeSec1ComboBox_SelectionChanged;
                ExitOrderTypeSec2ComboBox.SelectionChanged -= ExitOrderTypeSec2ComboBox_SelectionChanged;
                EnterOrderPositionSec1ComboBox.SelectionChanged -= EnterOrderPositionSec1ComboBox_SelectionChanged;
                EnterOrderPositionSec2ComboBox.SelectionChanged -= EnterOrderPositionSec2ComboBox_SelectionChanged;
                ExitOrderPositionSec1ComboBox.SelectionChanged -= ExitOrderPositionSec1ComboBox_SelectionChanged;
                ExitOrderPositionSec2ComboBox.SelectionChanged -= ExitOrderPositionSec2ComboBox_SelectionChanged;
                EnterSlippageSec1TextBox.TextChanged -= EnterSlippageSec1TextBox_TextChanged;
                EnterSlippageSec2TextBox.TextChanged -= EnterSlippageSec2TextBox_TextChanged;
                ExitSlippageSec1TextBox.TextChanged -= ExitSlippageSec1TextBox_TextChanged;
                ExitSlippageSec2TextBox.TextChanged -= ExitSlippageSec2TextBox_TextChanged;
                EnterLifetimeOrderSec1TextBox.TextChanged -= EnterLifetimeOrderSec1TextBox_TextChanged;
                EnterLifetimeOrderSec2TextBox.TextChanged -= EnterLifetimeOrderSec2TextBox_TextChanged;
                ExitLifetimeOrderSec1TextBox.TextChanged -= ExitLifetimeOrderSec1TextBox_TextChanged;
                ExitLifetimeOrderSec2TextBox.TextChanged -= ExitLifetimeOrderSec2TextBox_TextChanged;
                EnterOrderFrequencySec1TextBox.TextChanged -= EnterOrderFrequencySec1TextBox_TextChanged;
                EnterOrderFrequencySec2TextBox.TextChanged -= EnterOrderFrequencySec2TextBox_TextChanged;
                ExitOrderFrequencySec1TextBox.TextChanged -= ExitOrderFrequencySec1TextBox_TextChanged;
                ExitOrderFrequencySec2TextBox.TextChanged -= ExitOrderFrequencySec2TextBox_TextChanged;

                _gridOpenStepsStatisticBase.CellClick -= _gridOpenStepsStatisticBase_CellClick;
                _gridOpenStepsStatisticFutures.CellClick -= _gridOpenStepsStatisticFutures_CellClick;
                _gridCloseStepsStatisticBase.CellClick -= _gridCloseStepsStatisticBase_CellClick;
                _gridCloseStepsStatisticFutures.CellClick -= _gridCloseStepsStatisticFutures_CellClick;
            }
            catch
            {
                // ignore
            }
        }

        public string Key
        {
            get
            {
                return _selectedScenario.ArbitrationIceberg.MainLegs[0].BotTab.TabName;
            }
        }

        #endregion

        #region Helpers

        private void ClearAllValidationStyles()
        {
            MaxSpreadTextBox.ClearValue(TextBox.ForegroundProperty);
            MinSpreadTextBox.ClearValue(TextBox.ForegroundProperty);
            EnterTotalVolumeSec1TextBox.ClearValue(TextBox.ForegroundProperty);
            EnterTotalVolumeSec2TextBox.ClearValue(TextBox.ForegroundProperty);
            EnterOneOrderSec1TextBox.ClearValue(TextBox.ForegroundProperty);
            EnterOneOrderSec2TextBox.ClearValue(TextBox.ForegroundProperty);
            ExitOneOrderSec1TextBox.ClearValue(TextBox.ForegroundProperty);
            ExitOneOrderSec2TextBox.ClearValue(TextBox.ForegroundProperty);
            EnterSlippageSec1TextBox.ClearValue(TextBox.ForegroundProperty);
            EnterSlippageSec2TextBox.ClearValue(TextBox.ForegroundProperty);
            ExitSlippageSec1TextBox.ClearValue(TextBox.ForegroundProperty);
            ExitSlippageSec2TextBox.ClearValue(TextBox.ForegroundProperty);
            EnterLifetimeOrderSec1TextBox.ClearValue(TextBox.ForegroundProperty);
            EnterLifetimeOrderSec2TextBox.ClearValue(TextBox.ForegroundProperty);
            ExitLifetimeOrderSec1TextBox.ClearValue(TextBox.ForegroundProperty);
            ExitLifetimeOrderSec2TextBox.ClearValue(TextBox.ForegroundProperty);
            EnterOrderFrequencySec1TextBox.ClearValue(TextBox.ForegroundProperty);
            EnterOrderFrequencySec2TextBox.ClearValue(TextBox.ForegroundProperty);
            ExitOrderFrequencySec1TextBox.ClearValue(TextBox.ForegroundProperty);
            ExitOrderFrequencySec2TextBox.ClearValue(TextBox.ForegroundProperty);
        }

        private bool TryParseDecimal(TextBox textBox, out decimal result)
        {
            result = 0;
            string text = textBox.Text.Replace(',', '.');
            if (!decimal.TryParse(text, NumberStyles.Any, CultureInfo.InvariantCulture, out result))
            {
                textBox.Foreground = Brushes.Red;
                return false;
            }
            textBox.ClearValue(TextBox.ForegroundProperty);
            return true;
        }

        private bool TryParseInt(TextBox textBox, out int result)
        {
            result = 0;
            if (!int.TryParse(textBox.Text, out result))
            {
                textBox.Foreground = Brushes.Red;
                return false;
            }
            textBox.ClearValue(TextBox.ForegroundProperty);
            return true;
        }

        private void ValidateOneOrderVolume(TextBox oneOrderTextBox, TextBox totalVolumeTextBox)
        {
            string oneOrderText = oneOrderTextBox.Text.Replace(',', '.');
            string totalText = totalVolumeTextBox.Text.Replace(',', '.');

            decimal oneOrder;
            decimal totalVolume;

            if (!decimal.TryParse(oneOrderText, NumberStyles.Any, CultureInfo.InvariantCulture, out oneOrder))
            {
                return;
            }

            if (!decimal.TryParse(totalText, NumberStyles.Any, CultureInfo.InvariantCulture, out totalVolume))
            {
                return;
            }

            if (oneOrder <= 0 || oneOrder > totalVolume)
            {
                oneOrderTextBox.Foreground = Brushes.Red;
            }
            else
            {
                oneOrderTextBox.ClearValue(TextBox.ForegroundProperty);
            }
        }

        private void ValidatePositiveValue(TextBox textBox)
        {
            string text = textBox.Text.Replace(',', '.');
            decimal value;
            if (!decimal.TryParse(text, NumberStyles.Any, CultureInfo.InvariantCulture, out value))
            {
                return;
            }
            if (value <= 0)
            {
                textBox.Foreground = Brushes.Red;
            }
            else
            {
                textBox.ClearValue(TextBox.ForegroundProperty);
            }
        }

        private void ValidateLifetimeAndFrequency(ComboBox orderTypeComboBox, TextBox lifetimeTextBox, TextBox frequencyTextBox)
        {
            if (orderTypeComboBox.SelectedIndex != 1)
            {
                lifetimeTextBox.ClearValue(TextBox.ForegroundProperty);
            }
            else
            {
                int lifetime;
                if (int.TryParse(lifetimeTextBox.Text, out lifetime))
                {
                    if (lifetime <= 0)
                    {
                        lifetimeTextBox.Foreground = Brushes.Red;
                    }
                    else
                    {
                        lifetimeTextBox.ClearValue(TextBox.ForegroundProperty);
                    }
                }
            }

            int frequency;
            if (int.TryParse(frequencyTextBox.Text, out frequency))
            {
                if (frequency <= 0)
                {
                    frequencyTextBox.Foreground = Brushes.Red;
                }
                else
                {
                    frequencyTextBox.ClearValue(TextBox.ForegroundProperty);
                }
            }
        }

        private bool CheckAllParametersValid()
        {
            TextBox[] textBoxesToCheck = new TextBox[]
            {
                MaxSpreadTextBox, MinSpreadTextBox,
                //TextBoxContangoLookBack, TextBoxCointegrationDeviation, TextBoxCointegrationLookBack,
                EnterTotalVolumeSec1TextBox, EnterTotalVolumeSec2TextBox,
                EnterOneOrderSec1TextBox, EnterOneOrderSec2TextBox,
                ExitOneOrderSec1TextBox, ExitOneOrderSec2TextBox,
                EnterSlippageSec1TextBox, EnterSlippageSec2TextBox,
                ExitSlippageSec1TextBox, ExitSlippageSec2TextBox,
                EnterLifetimeOrderSec1TextBox, EnterLifetimeOrderSec2TextBox,
                ExitLifetimeOrderSec1TextBox, ExitLifetimeOrderSec2TextBox,
                EnterOrderFrequencySec1TextBox, EnterOrderFrequencySec2TextBox,
                ExitOrderFrequencySec1TextBox, ExitOrderFrequencySec2TextBox
            };

            foreach (TextBox tb in textBoxesToCheck)
            {
                if (tb.Foreground == Brushes.Red)
                {
                    return false;
                }
            }

            return true;
        }

        #endregion

        #region Events

        private void TimeShiftTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            try
            {
                if (_isUpdatingUi) return;

                if (string.IsNullOrEmpty(TimeShiftTextBox.Text))
                {
                    return;
                }

                int result;
                if (!int.TryParse(TimeShiftTextBox.Text, out result))
                {
                    return;
                }

                _selectedScenario.ArbitrationIceberg.TimeShift = result;
            }
            catch (Exception ex)
            {
                ServerMaster.SendNewLogMessage(ex.ToString(), Logging.LogMessageType.Error);
            }
        }

        private void MinSpreadTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            try
            {
                if (_isUpdatingUi) return;

                if (string.IsNullOrEmpty(MinSpreadTextBox.Text))
                {
                    return;
                }

                decimal result;
                if (!TryParseDecimal(MinSpreadTextBox, out result))
                {
                    return;
                }

                _selectedScenario.MinSpread = result;
            }
            catch (Exception ex)
            {
                ServerMaster.SendNewLogMessage(ex.ToString(), Logging.LogMessageType.Error);
            }
        }

        private void MaxSpreadTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            try
            {
                if (_isUpdatingUi) return;

                if (string.IsNullOrEmpty(MaxSpreadTextBox.Text))
                {
                    return;
                }

                decimal result;
                if (!TryParseDecimal(MaxSpreadTextBox, out result))
                {
                    return;
                }

                _selectedScenario.MaxSpread = result;
            }
            catch (Exception ex)
            {
                ServerMaster.SendNewLogMessage(ex.ToString(), Logging.LogMessageType.Error);
            }
        }

        private void TextBoxContangoLookBack_TextChanged(object sender, TextChangedEventArgs e)
        {
            try
            {
                //if (string.IsNullOrEmpty(TextBoxContangoLookBack.Text))
                //{
                //    return;
                //}

                //int result;
                //if (!TryParseInt(TextBoxContangoLookBack, out result))
                //{
                //    return;
                //}

                //_syntheticBond.SeparationLength = result;
            }
            catch (Exception ex)
            {
                ServerMaster.SendNewLogMessage(ex.ToString(), Logging.LogMessageType.Error);
            }
        }

        private void TextBoxCointegrationLookBack_TextChanged(object sender, TextChangedEventArgs e)
        {
            try
            {
                if (string.IsNullOrEmpty(TextBoxCointegrationLookBack.Text))
                {
                    return;
                }

                int result;
                if (!TryParseInt(TextBoxCointegrationLookBack, out result))
                {
                    return;
                }

                _syntheticBond.CointegrationBuilder.CointegrationLookBack = result;
            }
            catch (Exception ex)
            {
                ServerMaster.SendNewLogMessage(ex.ToString(), Logging.LogMessageType.Error);
            }
        }

        private void TextBoxCointegrationDeviation_TextChanged(object sender, TextChangedEventArgs e)
        {
            try
            {
                if (string.IsNullOrEmpty(TextBoxCointegrationDeviation.Text))
                {
                    return;
                }

                decimal result;
                if (!TryParseDecimal(TextBoxCointegrationDeviation, out result))
                {
                    return;
                }

                _syntheticBond.CointegrationBuilder.CointegrationDeviation = result;
            }
            catch (Exception ex)
            {
                ServerMaster.SendNewLogMessage(ex.ToString(), Logging.LogMessageType.Error);
            }
        }

        private void EnterTextBoxAssetPortfolioSec1_TextChanged(object sender, TextChangedEventArgs e)
        {
            try
            {
                if (_isUpdatingUi)
                {
                    return;
                }

                _selectedScenario.ArbitrationIceberg.MainLegs[0].AssetPortfolio = EnterTextBoxAssetPortfolioSec1.Text;
                ExitTextBoxAssetPortfolioSec1.Text = EnterTextBoxAssetPortfolioSec1.Text;
                _syntheticBondSeries.OnSettingsChanged();
            }
            catch (Exception ex)
            {
                ServerMaster.SendNewLogMessage(ex.ToString(), Logging.LogMessageType.Error);
            }
        }

        private void EnterTextBoxAssetPortfolioSec2_TextChanged(object sender, TextChangedEventArgs e)
        {
            try
            {
                if (_isUpdatingUi)
                {
                    return;
                }

                _selectedScenario.ArbitrationIceberg.SecondaryLegs[0].AssetPortfolio = EnterTextBoxAssetPortfolioSec2.Text;
                ExitTextBoxAssetPortfolioSec2.Text = EnterTextBoxAssetPortfolioSec2.Text;
                _syntheticBondSeries.OnSettingsChanged();
            }
            catch (Exception ex)
            {
                ServerMaster.SendNewLogMessage(ex.ToString(), Logging.LogMessageType.Error);
            }
        }

        private void ExitLifetimeOrderSec2TextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            try
            {
                if (_isUpdatingUi)
                {
                    return;
                }

                if (string.IsNullOrEmpty(ExitLifetimeOrderSec2TextBox.Text))
                {
                    return;
                }

                int result;
                if (!TryParseInt(ExitLifetimeOrderSec2TextBox, out result))
                {
                    return;
                }

                _selectedScenario.ArbitrationIceberg.SecondaryLegs[0].ExitLifetimeOrder = result;
                _syntheticBondSeries.OnSettingsChanged();
                ValidateLifetimeAndFrequency(ExitOrderTypeSec2ComboBox, ExitLifetimeOrderSec2TextBox, ExitOrderFrequencySec2TextBox);
            }
            catch (Exception ex)
            {
                ServerMaster.SendNewLogMessage(ex.ToString(), Logging.LogMessageType.Error);
            }
        }

        private void ExitLifetimeOrderSec1TextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            try
            {
                if (_isUpdatingUi)
                {
                    return;
                }

                if (string.IsNullOrEmpty(ExitLifetimeOrderSec1TextBox.Text))
                {
                    return;
                }

                int result;
                if (!TryParseInt(ExitLifetimeOrderSec1TextBox, out result))
                {
                    return;
                }

                _selectedScenario.ArbitrationIceberg.MainLegs[0].ExitLifetimeOrder = result;
                _syntheticBondSeries.OnSettingsChanged();
                ValidateLifetimeAndFrequency(ExitOrderTypeSec1ComboBox, ExitLifetimeOrderSec1TextBox, ExitOrderFrequencySec1TextBox);
            }
            catch (Exception ex)
            {
                ServerMaster.SendNewLogMessage(ex.ToString(), Logging.LogMessageType.Error);
            }
        }

        private void EnterLifetimeOrderSec2TextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            try
            {
                if (_isUpdatingUi)
                {
                    return;
                }

                if (string.IsNullOrEmpty(EnterLifetimeOrderSec2TextBox.Text))
                {
                    return;
                }

                int result;
                if (!TryParseInt(EnterLifetimeOrderSec2TextBox, out result))
                {
                    return;
                }

                _selectedScenario.ArbitrationIceberg.SecondaryLegs[0].EnterLifetimeOrder = result;
                _syntheticBondSeries.OnSettingsChanged();
                ValidateLifetimeAndFrequency(EnterOrderTypeSec2ComboBox, EnterLifetimeOrderSec2TextBox, EnterOrderFrequencySec2TextBox);
            }
            catch (Exception ex)
            {
                ServerMaster.SendNewLogMessage(ex.ToString(), Logging.LogMessageType.Error);
            }
        }

        private void EnterLifetimeOrderSec1TextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            try
            {
                if (_isUpdatingUi)
                {
                    return;
                }

                if (string.IsNullOrEmpty(EnterLifetimeOrderSec1TextBox.Text))
                {
                    return;
                }

                int result;
                if (!TryParseInt(EnterLifetimeOrderSec1TextBox, out result))
                {
                    return;
                }

                _selectedScenario.ArbitrationIceberg.MainLegs[0].EnterLifetimeOrder = result;
                _syntheticBondSeries.OnSettingsChanged();
                ValidateLifetimeAndFrequency(EnterOrderTypeSec1ComboBox, EnterLifetimeOrderSec1TextBox, EnterOrderFrequencySec1TextBox);
            }
            catch (Exception ex)
            {
                ServerMaster.SendNewLogMessage(ex.ToString(), Logging.LogMessageType.Error);
            }
        }

        private void EnterOrderFrequencySec1TextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            try
            {
                if (_isUpdatingUi)
                {
                    return;
                }

                if (string.IsNullOrEmpty(EnterOrderFrequencySec1TextBox.Text))
                {
                    return;
                }

                int result;
                if (!TryParseInt(EnterOrderFrequencySec1TextBox, out result))
                {
                    return;
                }

                _selectedScenario.ArbitrationIceberg.MainLegs[0].EnterOrderFrequency = result;
                _syntheticBondSeries.OnSettingsChanged();
                ValidateLifetimeAndFrequency(EnterOrderTypeSec1ComboBox, EnterLifetimeOrderSec1TextBox, EnterOrderFrequencySec1TextBox);
            }
            catch (Exception ex)
            {
                ServerMaster.SendNewLogMessage(ex.ToString(), Logging.LogMessageType.Error);
            }
        }

        private void EnterOrderFrequencySec2TextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            try
            {
                if (_isUpdatingUi)
                {
                    return;
                }

                if (string.IsNullOrEmpty(EnterOrderFrequencySec2TextBox.Text))
                {
                    return;
                }

                int result;
                if (!TryParseInt(EnterOrderFrequencySec2TextBox, out result))
                {
                    return;
                }

                _selectedScenario.ArbitrationIceberg.SecondaryLegs[0].EnterOrderFrequency = result;
                _syntheticBondSeries.OnSettingsChanged();
                ValidateLifetimeAndFrequency(EnterOrderTypeSec2ComboBox, EnterLifetimeOrderSec2TextBox, EnterOrderFrequencySec2TextBox);
            }
            catch (Exception ex)
            {
                ServerMaster.SendNewLogMessage(ex.ToString(), Logging.LogMessageType.Error);
            }
        }

        private void ExitOrderFrequencySec1TextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            try
            {
                if (_isUpdatingUi)
                {
                    return;
                }

                if (string.IsNullOrEmpty(ExitOrderFrequencySec1TextBox.Text))
                {
                    return;
                }

                int result;
                if (!TryParseInt(ExitOrderFrequencySec1TextBox, out result))
                {
                    return;
                }

                _selectedScenario.ArbitrationIceberg.MainLegs[0].ExitOrderFrequency = result;
                _syntheticBondSeries.OnSettingsChanged();
                ValidateLifetimeAndFrequency(ExitOrderTypeSec1ComboBox, ExitLifetimeOrderSec1TextBox, ExitOrderFrequencySec1TextBox);
            }
            catch (Exception ex)
            {
                ServerMaster.SendNewLogMessage(ex.ToString(), Logging.LogMessageType.Error);
            }
        }

        private void ExitOrderFrequencySec2TextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            try
            {
                if (_isUpdatingUi)
                {
                    return;
                }

                if (string.IsNullOrEmpty(ExitOrderFrequencySec2TextBox.Text))
                {
                    return;
                }

                int result;
                if (!TryParseInt(ExitOrderFrequencySec2TextBox, out result))
                {
                    return;
                }

                _selectedScenario.ArbitrationIceberg.SecondaryLegs[0].ExitOrderFrequency = result;
                _syntheticBondSeries.OnSettingsChanged();
                ValidateLifetimeAndFrequency(ExitOrderTypeSec2ComboBox, ExitLifetimeOrderSec2TextBox, ExitOrderFrequencySec2TextBox);
            }
            catch (Exception ex)
            {
                ServerMaster.SendNewLogMessage(ex.ToString(), Logging.LogMessageType.Error);
            }
        }

        private void ExitSlippageSec2TextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            try
            {
                if (_isUpdatingUi)
                {
                    return;
                }

                if (string.IsNullOrEmpty(ExitSlippageSec2TextBox.Text))
                {
                    return;
                }

                decimal result;
                if (!TryParseDecimal(ExitSlippageSec2TextBox, out result))
                {
                    return;
                }

                _selectedScenario.ArbitrationIceberg.SecondaryLegs[0].ExitSlippage = result;
                _syntheticBondSeries.OnSettingsChanged();
            }
            catch (Exception ex)
            {
                ServerMaster.SendNewLogMessage(ex.ToString(), Logging.LogMessageType.Error);
            }
        }

        private void EnterSlippageSec2TextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            try
            {
                if (_isUpdatingUi)
                {
                    return;
                }

                if (string.IsNullOrEmpty(EnterSlippageSec2TextBox.Text))
                {
                    return;
                }

                decimal result;
                if (!TryParseDecimal(EnterSlippageSec2TextBox, out result))
                {
                    return;
                }

                _selectedScenario.ArbitrationIceberg.SecondaryLegs[0].EnterSlippage = result;
                _syntheticBondSeries.OnSettingsChanged();
            }
            catch (Exception ex)
            {
                ServerMaster.SendNewLogMessage(ex.ToString(), Logging.LogMessageType.Error);
            }
        }

        private void ExitSlippageSec1TextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            try
            {
                if (_isUpdatingUi)
                {
                    return;
                }

                if (string.IsNullOrEmpty(ExitSlippageSec1TextBox.Text))
                {
                    return;
                }

                decimal result;
                if (!TryParseDecimal(ExitSlippageSec1TextBox, out result))
                {
                    return;
                }

                _selectedScenario.ArbitrationIceberg.MainLegs[0].ExitSlippage = result;
                _syntheticBondSeries.OnSettingsChanged();
            }
            catch (Exception ex)
            {
                ServerMaster.SendNewLogMessage(ex.ToString(), Logging.LogMessageType.Error);
            }
        }

        private void EnterSlippageSec1TextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            try
            {
                if (_isUpdatingUi)
                {
                    return;
                }

                if (string.IsNullOrEmpty(EnterSlippageSec1TextBox.Text))
                {
                    return;
                }

                decimal result;
                if (!TryParseDecimal(EnterSlippageSec1TextBox, out result))
                {
                    return;
                }

                _selectedScenario.ArbitrationIceberg.MainLegs[0].EnterSlippage = result;
                _syntheticBondSeries.OnSettingsChanged();
            }
            catch (Exception ex)
            {
                ServerMaster.SendNewLogMessage(ex.ToString(), Logging.LogMessageType.Error);
            }
        }

        private void ExitOrderPositionSec2ComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            try
            {
                if (_isUpdatingUi) return;

                if (string.IsNullOrEmpty(ExitOrderPositionSec2ComboBox.Text))
                {
                    return;
                }

                if (ExitOrderPositionSec2ComboBox.SelectedIndex == 0)
                {
                    _selectedScenario.ArbitrationIceberg.SecondaryLegs[0].ExitOrderPosition = SynteticBondOrderPosition.Ask;
                }
                else if (ExitOrderPositionSec2ComboBox.SelectedIndex == 1)
                {
                    _selectedScenario.ArbitrationIceberg.SecondaryLegs[0].ExitOrderPosition = SynteticBondOrderPosition.Bid;
                }
                else if (ExitOrderPositionSec2ComboBox.SelectedIndex == 2)
                {
                    _selectedScenario.ArbitrationIceberg.SecondaryLegs[0].ExitOrderPosition = SynteticBondOrderPosition.Middle;
                }

                _syntheticBondSeries.OnSettingsChanged();
            }
            catch (Exception ex)
            {
                ServerMaster.SendNewLogMessage(ex.ToString(), Logging.LogMessageType.Error);
            }
        }

        private void ExitOrderPositionSec1ComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            try
            {
                if (_isUpdatingUi)
                {
                    return;
                }

                if (string.IsNullOrEmpty(ExitOrderPositionSec1ComboBox.Text))
                {
                    return;
                }

                if (ExitOrderPositionSec1ComboBox.SelectedIndex == 0)
                {
                    _selectedScenario.ArbitrationIceberg.MainLegs[0].ExitOrderPosition = SynteticBondOrderPosition.Ask;
                }
                else if (ExitOrderPositionSec1ComboBox.SelectedIndex == 1)
                {
                    _selectedScenario.ArbitrationIceberg.MainLegs[0].ExitOrderPosition = SynteticBondOrderPosition.Bid;
                }
                else if (ExitOrderPositionSec1ComboBox.SelectedIndex == 2)
                {
                    _selectedScenario.ArbitrationIceberg.MainLegs[0].ExitOrderPosition = SynteticBondOrderPosition.Middle;
                }

                _syntheticBondSeries.OnSettingsChanged();
            }
            catch (Exception ex)
            {
                ServerMaster.SendNewLogMessage(ex.ToString(), Logging.LogMessageType.Error);
            }
        }

        private void EnterOrderPositionSec2ComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            try
            {
                if (_isUpdatingUi)
                {
                    return;
                }

                if (string.IsNullOrEmpty(EnterOrderPositionSec2ComboBox.Text))
                {
                    return;
                }

                if (EnterOrderPositionSec2ComboBox.SelectedIndex == 0)
                {
                    _selectedScenario.ArbitrationIceberg.SecondaryLegs[0].EnterOrderPosition = SynteticBondOrderPosition.Ask;
                }
                else if (EnterOrderPositionSec2ComboBox.SelectedIndex == 1)
                {
                    _selectedScenario.ArbitrationIceberg.SecondaryLegs[0].EnterOrderPosition = SynteticBondOrderPosition.Bid;
                }
                else if (EnterOrderPositionSec2ComboBox.SelectedIndex == 2)
                {
                    _selectedScenario.ArbitrationIceberg.SecondaryLegs[0].EnterOrderPosition = SynteticBondOrderPosition.Middle;
                }

                _syntheticBondSeries.OnSettingsChanged();
            }
            catch (Exception ex)
            {
                ServerMaster.SendNewLogMessage(ex.ToString(), Logging.LogMessageType.Error);
            }
        }

        private void EnterOrderPositionSec1ComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            try
            {
                if (_isUpdatingUi)
                {
                    return;
                }

                if (string.IsNullOrEmpty(EnterOrderPositionSec1ComboBox.Text))
                {
                    return;
                }

                if (EnterOrderPositionSec1ComboBox.SelectedIndex == 0)
                {
                    _selectedScenario.ArbitrationIceberg.MainLegs[0].EnterOrderPosition = SynteticBondOrderPosition.Ask;
                }
                else if (EnterOrderPositionSec1ComboBox.SelectedIndex == 1)
                {
                    _selectedScenario.ArbitrationIceberg.MainLegs[0].EnterOrderPosition = SynteticBondOrderPosition.Bid;
                }
                else if (EnterOrderPositionSec1ComboBox.SelectedIndex == 2)
                {
                    _selectedScenario.ArbitrationIceberg.MainLegs[0].EnterOrderPosition = SynteticBondOrderPosition.Middle;
                }

                _syntheticBondSeries.OnSettingsChanged();
            }
            catch (Exception ex)
            {
                ServerMaster.SendNewLogMessage(ex.ToString(), Logging.LogMessageType.Error);
            }
        }

        private void ExitOrderTypeSec2ComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            try
            {
                if (_isUpdatingUi)
                {
                    return;
                }

                if (string.IsNullOrEmpty(ExitOrderTypeSec2ComboBox.Text))
                {
                    return;
                }

                if (ExitOrderTypeSec2ComboBox.SelectedIndex == 0)
                {
                    _selectedScenario.ArbitrationIceberg.SecondaryLegs[0].ExitOrderType = OrderPriceType.Market;
                }
                else if (ExitOrderTypeSec2ComboBox.SelectedIndex == 1)
                {
                    _selectedScenario.ArbitrationIceberg.SecondaryLegs[0].ExitOrderType = OrderPriceType.Limit;
                }

                ValidateLifetimeAndFrequency(ExitOrderTypeSec2ComboBox, ExitLifetimeOrderSec2TextBox, ExitOrderFrequencySec2TextBox);
                UpdateTabControlsLockState();
                _syntheticBondSeries.OnSettingsChanged();
            }
            catch (Exception ex)
            {
                ServerMaster.SendNewLogMessage(ex.ToString(), Logging.LogMessageType.Error);
            }
        }

        private void EnterOrderTypeSec2ComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            try
            {
                if (_isUpdatingUi)
                {
                    return;
                }

                if (string.IsNullOrEmpty(EnterOrderTypeSec2ComboBox.Text))
                {
                    return;
                }

                if (EnterOrderTypeSec2ComboBox.SelectedIndex == 0)
                {
                    _selectedScenario.ArbitrationIceberg.SecondaryLegs[0].EnterOrderType = OrderPriceType.Market;
                }
                else if (EnterOrderTypeSec2ComboBox.SelectedIndex == 1)
                {
                    _selectedScenario.ArbitrationIceberg.SecondaryLegs[0].EnterOrderType = OrderPriceType.Limit;
                }

                ValidateLifetimeAndFrequency(EnterOrderTypeSec2ComboBox, EnterLifetimeOrderSec2TextBox, EnterOrderFrequencySec2TextBox);
                UpdateTabControlsLockState();
                _syntheticBondSeries.OnSettingsChanged();
            }
            catch (Exception ex)
            {
                ServerMaster.SendNewLogMessage(ex.ToString(), Logging.LogMessageType.Error);
            }
        }

        private void ExitOrderTypeSec1ComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            try
            {
                if (_isUpdatingUi)
                {
                    return;
                }

                if (string.IsNullOrEmpty(ExitOrderTypeSec1ComboBox.Text))
                {
                    return;
                }

                if (ExitOrderTypeSec1ComboBox.SelectedIndex == 0)
                {
                    _selectedScenario.ArbitrationIceberg.MainLegs[0].ExitOrderType = OrderPriceType.Market;
                }
                else if (ExitOrderTypeSec1ComboBox.SelectedIndex == 1)
                {
                    _selectedScenario.ArbitrationIceberg.MainLegs[0].ExitOrderType = OrderPriceType.Limit;
                }

                ValidateLifetimeAndFrequency(ExitOrderTypeSec1ComboBox, ExitLifetimeOrderSec1TextBox, ExitOrderFrequencySec1TextBox);
                UpdateTabControlsLockState();
                _syntheticBondSeries.OnSettingsChanged();
            }
            catch (Exception ex)
            {
                ServerMaster.SendNewLogMessage(ex.ToString(), Logging.LogMessageType.Error);
            }
        }

        private void EnterOrderTypeSec1ComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            try
            {
                if (_isUpdatingUi)
                {
                    return;
                }

                if (string.IsNullOrEmpty(EnterOrderTypeSec1ComboBox.Text))
                {
                    return;
                }

                if (EnterOrderTypeSec1ComboBox.SelectedIndex == 0)
                {
                    _selectedScenario.ArbitrationIceberg.MainLegs[0].EnterOrderType = OrderPriceType.Market;
                }
                else if (EnterOrderTypeSec1ComboBox.SelectedIndex == 1)
                {
                    _selectedScenario.ArbitrationIceberg.MainLegs[0].EnterOrderType = OrderPriceType.Limit;
                }

                ValidateLifetimeAndFrequency(EnterOrderTypeSec1ComboBox, EnterLifetimeOrderSec1TextBox, EnterOrderFrequencySec1TextBox);
                UpdateTabControlsLockState();
                _syntheticBondSeries.OnSettingsChanged();
            }
            catch (Exception ex)
            {
                ServerMaster.SendNewLogMessage(ex.ToString(), Logging.LogMessageType.Error);
            }
        }

        private void ExitOneOrderSec2TextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            try
            {
                if (_isUpdatingUi)
                {
                    return;
                }

                if (string.IsNullOrEmpty(ExitOneOrderSec2TextBox.Text))
                {
                    return;
                }

                decimal result;
                if (!TryParseDecimal(ExitOneOrderSec2TextBox, out result))
                {
                    return;
                }

                _selectedScenario.ArbitrationIceberg.SecondaryLegs[0].ExitOneOrderVolume = result;
                ValidateOneOrderVolume(ExitOneOrderSec2TextBox, ExitTotalVolumeSec2TextBox);
                _syntheticBondSeries.OnSettingsChanged();
            }
            catch (Exception ex)
            {
                ServerMaster.SendNewLogMessage(ex.ToString(), Logging.LogMessageType.Error);
            }
        }

        private void EnterOneOrderSec2TextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            try
            {
                if (_isUpdatingUi)
                {
                    return;
                }

                if (string.IsNullOrEmpty(EnterOneOrderSec2TextBox.Text))
                {
                    return;
                }

                decimal result;
                if (!TryParseDecimal(EnterOneOrderSec2TextBox, out result))
                {
                    return;
                }

                _selectedScenario.ArbitrationIceberg.SecondaryLegs[0].EnterOneOrderVolume = result;
                ValidateOneOrderVolume(EnterOneOrderSec2TextBox, EnterTotalVolumeSec2TextBox);
                _syntheticBondSeries.OnSettingsChanged();
            }
            catch (Exception ex)
            {
                ServerMaster.SendNewLogMessage(ex.ToString(), Logging.LogMessageType.Error);
            }
        }

        private void ExitOneOrderSec1TextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            try
            {
                if (_isUpdatingUi)
                {
                    return;
                }

                if (string.IsNullOrEmpty(ExitOneOrderSec1TextBox.Text))
                {
                    return;
                }

                decimal result;
                if (!TryParseDecimal(ExitOneOrderSec1TextBox, out result))
                {
                    return;
                }

                _selectedScenario.ArbitrationIceberg.MainLegs[0].ExitOneOrderVolume = result;
                ValidateOneOrderVolume(ExitOneOrderSec1TextBox, ExitTotalVolumeSec1TextBox);
                _syntheticBondSeries.OnSettingsChanged();
            }
            catch (Exception ex)
            {
                ServerMaster.SendNewLogMessage(ex.ToString(), Logging.LogMessageType.Error);
            }
        }

        private void EnterOneOrderSec1TextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            try
            {
                if (_isUpdatingUi)
                {
                    return;
                }

                if (string.IsNullOrEmpty(EnterOneOrderSec1TextBox.Text))
                {
                    return;
                }

                decimal result;
                if (!TryParseDecimal(EnterOneOrderSec1TextBox, out result))
                {
                    return;
                }

                _selectedScenario.ArbitrationIceberg.MainLegs[0].EnterOneOrderVolume = result;
                ValidateOneOrderVolume(EnterOneOrderSec1TextBox, EnterTotalVolumeSec1TextBox);
                _syntheticBondSeries.OnSettingsChanged();
            }
            catch (Exception ex)
            {
                ServerMaster.SendNewLogMessage(ex.ToString(), Logging.LogMessageType.Error);
            }
        }

        private void EnterTotalVolumeSec2TextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            try
            {
                if (_isUpdatingUi)
                {
                    return;
                }

                if (string.IsNullOrEmpty(EnterTotalVolumeSec2TextBox.Text))
                {
                    return;
                }

                decimal result;
                if (!TryParseDecimal(EnterTotalVolumeSec2TextBox, out result))
                {
                    return;
                }

                _selectedScenario.ArbitrationIceberg.SecondaryLegs[0].TotalVolume = result;
                ExitTotalVolumeSec2TextBox.Text = EnterTotalVolumeSec2TextBox.Text;
                ValidatePositiveValue(EnterTotalVolumeSec2TextBox);
                ValidateOneOrderVolume(EnterOneOrderSec2TextBox, EnterTotalVolumeSec2TextBox);
                ValidateOneOrderVolume(ExitOneOrderSec2TextBox, ExitTotalVolumeSec2TextBox);
                _syntheticBondSeries.OnSettingsChanged();
            }
            catch (Exception ex)
            {
                ServerMaster.SendNewLogMessage(ex.ToString(), Logging.LogMessageType.Error);
            }
        }

        private void EnterTotalVolumeSec1TextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            try
            {
                if (_isUpdatingUi)
                {
                    return;
                }

                if (string.IsNullOrEmpty(EnterTotalVolumeSec1TextBox.Text))
                {
                    return;
                }

                decimal result;
                if (!TryParseDecimal(EnterTotalVolumeSec1TextBox, out result))
                {
                    return;
                }

                _selectedScenario.ArbitrationIceberg.MainLegs[0].TotalVolume = result;
                ExitTotalVolumeSec1TextBox.Text = EnterTotalVolumeSec1TextBox.Text;
                ValidatePositiveValue(EnterTotalVolumeSec1TextBox);
                ValidateOneOrderVolume(EnterOneOrderSec1TextBox, EnterTotalVolumeSec1TextBox);
                ValidateOneOrderVolume(ExitOneOrderSec1TextBox, ExitTotalVolumeSec1TextBox);
                _syntheticBondSeries.OnSettingsChanged();
            }
            catch (Exception ex)
            {
                ServerMaster.SendNewLogMessage(ex.ToString(), Logging.LogMessageType.Error);
            }
        }

        private void EnterVolumeTypeSec1ComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            try
            {
                if (_isUpdatingUi)
                {
                    return;
                }

                if (EnterVolumeTypeSec1ComboBox.SelectedIndex == 0)
                {
                    _selectedScenario.ArbitrationIceberg.MainLegs[0].VolumeType = VolumeType.Contracts;
                }
                else if (EnterVolumeTypeSec1ComboBox.SelectedIndex == 1)
                {
                    _selectedScenario.ArbitrationIceberg.MainLegs[0].VolumeType = VolumeType.ContractCurrency;
                }
                else if (EnterVolumeTypeSec1ComboBox.SelectedIndex == 2)
                {
                    _selectedScenario.ArbitrationIceberg.MainLegs[0].VolumeType = VolumeType.DepositPercent;
                }

                ExitVolumeTypeSec1ComboBox.SelectedIndex = EnterVolumeTypeSec1ComboBox.SelectedIndex;

                _syntheticBondSeries.OnSettingsChanged();
            }
            catch (Exception ex)
            {
                ServerMaster.SendNewLogMessage(ex.ToString(), Logging.LogMessageType.Error);
            }
        }

        private void EnterVolumeTypeSec2ComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            try
            {
                if (_isUpdatingUi)
                {
                    return;
                }

                if (EnterVolumeTypeSec2ComboBox.SelectedIndex == 0)
                {
                    _selectedScenario.ArbitrationIceberg.SecondaryLegs[0].VolumeType = VolumeType.Contracts;
                }
                else if (EnterVolumeTypeSec2ComboBox.SelectedIndex == 1)
                {
                    _selectedScenario.ArbitrationIceberg.SecondaryLegs[0].VolumeType = VolumeType.ContractCurrency;
                }
                else if (EnterVolumeTypeSec2ComboBox.SelectedIndex == 2)
                {
                    _selectedScenario.ArbitrationIceberg.SecondaryLegs[0].VolumeType = VolumeType.DepositPercent;
                }

                ExitVolumeTypeSec2ComboBox.SelectedIndex = EnterVolumeTypeSec2ComboBox.SelectedIndex;

                _syntheticBondSeries.OnSettingsChanged();
            }
            catch (Exception ex)
            {
                ServerMaster.SendNewLogMessage(ex.ToString(), Logging.LogMessageType.Error);
            }
        }

        private void ExitVolumeTypeSec1ComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // Read-only: значение синхронизируется из EnterVolumeTypeSec1ComboBox
        }

        private void ExitVolumeTypeSec2ComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // Read-only: значение синхронизируется из EnterVolumeTypeSec2ComboBox
        }

        private void TradeModeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            try
            {
                if (_isUpdatingUi)
                {
                    return;
                }

                if (string.IsNullOrEmpty(TradeModeComboBox.Text))
                {
                    return;
                }

                if (TradeModeComboBox.SelectedIndex == 0)
                {
                    _selectedScenario.ArbitrationIceberg.CurrentMode = ArbitrationMode.OpenBuyFirstSellSecond;
                }
                else if (TradeModeComboBox.SelectedIndex == 1)
                {
                    _selectedScenario.ArbitrationIceberg.CurrentMode = ArbitrationMode.OpenSellFirstBuySecond;
                }
                else if (TradeModeComboBox.SelectedIndex == 2)
                {
                    _selectedScenario.ArbitrationIceberg.CurrentMode = ArbitrationMode.CloseScript;
                }
                else if (TradeModeComboBox.SelectedIndex == 2)
                {
                    _selectedScenario.ArbitrationIceberg.CurrentMode = ArbitrationMode.CloseAllScripts;
                }

                StartButton.Background = null;
                PauseButton.Background = Brushes.DarkOrange;

                //_selectedScenario.ArbitrationIceberg.Pause();
            }
            catch (Exception ex)
            {
                ServerMaster.SendNewLogMessage(ex.ToString(), Logging.LogMessageType.Error);
            }
        }

        private void NonTradePeriodButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                _selectedScenario.NonTradePeriods.ShowDialog();
                _selectedScenario.NonTradePeriods.Save();
            }
            catch (Exception ex)
            {
                ServerMaster.SendNewLogMessage(ex.ToString(), Logging.LogMessageType.Error);
            }
        }

        private void StartButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_selectedScenario.ArbitrationIceberg.CurrentStatus == ArbitrationStatus.On)
                {
                    return;
                }

                if (!CheckAllParametersValid())
                {
                    string message = "Параметры настроек содержат ошибки. Торговля невозможна.";
                    AddLogMessage(message, LogMessageType.System);
                    ServerMaster.SendNewLogMessage(message, Logging.LogMessageType.Error);
                    return;
                }

                if (_selectedScenario.ArbitrationIceberg.CheckTradingReady() == false)
                {
                    string message = "Синтетическая облигация не готова к торговле";
                    AddLogMessage(message, LogMessageType.System);
                    ServerMaster.SendNewLogMessage(message, Logging.LogMessageType.Error);
                    return;
                }

                StartButton.Background = Brushes.DarkGreen;
                PauseButton.Background = null;

                _selectedScenario.ArbitrationIceberg.Start(_selectedScenario.ArbitrationIceberg.CurrentMode);
            }
            catch (Exception ex)
            {
                ServerMaster.SendNewLogMessage(ex.ToString(), Logging.LogMessageType.Error);
            }
        }

        private void PauseButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_selectedScenario.ArbitrationIceberg.CurrentStatus == ArbitrationStatus.Pause)
                {
                    return;
                }

                if (!CheckAllParametersValid())
                {
                    ServerMaster.SendNewLogMessage("Параметры настроек содержат ошибки. Торговля невозможна.", Logging.LogMessageType.Error);
                    return;
                }

                if (_selectedScenario.ArbitrationIceberg.CheckTradingReady() == false)
                {
                    ServerMaster.SendNewLogMessage("Синтетическая облигация не готова к торговле", Logging.LogMessageType.Error);
                    return;
                }

                StartButton.Background = null;
                PauseButton.Background = Brushes.DarkOrange;

                _selectedScenario.ArbitrationIceberg.CurrentStatus = ArbitrationStatus.Pause;
            }
            catch (Exception ex)
            {
                ServerMaster.SendNewLogMessage(ex.ToString(), Logging.LogMessageType.Error);
            }
        }

        private void DeleteScenarioButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_selectedScenario == null)
                {
                    return;
                }

                if (_selectedScenario.ArbitrationIceberg.CurrentStatus == ArbitrationStatus.On)
                {
                    ServerMaster.SendNewLogMessage(
                        "Невозможно удалить активный сценарий. Сначала остановите торговлю.",
                        Logging.LogMessageType.Error);
                    return;
                }

                AcceptDialogUi acceptDialog = new AcceptDialogUi(OsLocalization.Trader.Label734);
                acceptDialog.ShowDialog();

                if (acceptDialog.UserAcceptAction == false)
                {
                    return;
                }

                BondScenario scenarioToDelete = _selectedScenario;

                for (int i = 0; i < _syntheticBond.ActiveScenarios.Count; i++)
                {
                    if (scenarioToDelete.ScenarioNumber == _syntheticBond.ActiveScenarios[i].ScenarioNumber)
                    {
                        _syntheticBond.ActiveScenarios.RemoveAt(i);
                        break;
                    }
                }

                _syntheticBond.DeletedScenarios.Add(scenarioToDelete);

                if (_syntheticBond.ActiveScenarios.Count == 0)
                {
                    int number = _syntheticBond.GetAvailableScenarioNumber();
                    BondScenario scenario = _syntheticBond.CreateNewScenario("Script " + number);
                    scenario.IsActiveScenario = true;
                    _syntheticBond.SelectedScenario = scenario;
                }
                else
                {
                    _syntheticBond.ActiveScenarios[0].IsActiveScenario = true;
                    _selectedScenario = _syntheticBond.ActiveScenarios[0];
                }

                CreateScenarioComboBox();

                UpdateScenarioTextBoxDefault();

                _syntheticBond.Save();
            }
            catch (Exception ex)
            {
                ServerMaster.SendNewLogMessage(ex.ToString(), Logging.LogMessageType.Error);
            }
        }

        private void CreateScriptButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                //string scenarioName = ScenarioTextBox.Text.Trim();

                //if (string.IsNullOrEmpty(scenarioName))
                //{
                //    int number = _syntheticBond.GetAvailableScenarioNumber();
                //    scenarioName = "Script " + number.ToString();
                //}

                //for (int i = 0; i < _syntheticBond.ActiveScenarios.Count; i++)
                //{
                //    if (_syntheticBond.ActiveScenarios[i].UniqueName == scenarioName)
                //    {
                //        ServerMaster.SendNewLogMessage(
                //            "Сценарий с именем '" + scenarioName + "' уже существует.",
                //            Logging.LogMessageType.Error);
                //        return;
                //    }
                //}

                //AcceptDialogUi ui = new AcceptDialogUi(OsLocalization.Trader.Label733);
                //ui.ShowDialog();

                //if (ui.UserAcceptAction == false)
                //{
                //    return;
                //}

                //BondScenario newScenario = _syntheticBond.CreateNewScenario(scenarioName);
                //SubscribeToScenario(newScenario);

                //ScenarioComboBox.Items.Add(new ComboBoxItem
                //{
                //    Content = scenarioName,
                //    Name = scenarioName.Replace(" ", "_")
                //});

                //ScenarioComboBox.SelectedIndex = ScenarioComboBox.Items.Count - 1;

                //UpdateScenarioTextBoxDefault();

                //_syntheticBondSeries.OnSettingsChanged();
            }
            catch (Exception ex)
            {
                ServerMaster.SendNewLogMessage(ex.ToString(), Logging.LogMessageType.Error);
            }
        }

        #endregion

        #region Fields

        private SyntheticBondSeries _syntheticBondSeries;

        private SyntheticBond _syntheticBond;

        private BondScenario _selectedScenario;

        private bool _isUpdatingUi;

        private DispatcherTimer _updateTimer;

        private DataGridView _gridOpenStepsBase;

        private DataGridView _gridOpenStepsStatisticBase;

        private DataGridView _gridOpenStepsFutures;

        private DataGridView _gridOpenStepsStatisticFutures;

        private DataGridView _gridCloseStepsBase;

        private DataGridView _gridCloseStepsStatisticBase;

        private DataGridView _gridCloseStepsFutures;

        private DataGridView _gridCloseStepsStatisticFutures;

        #endregion

        #region Scenario management

        private void CreateScenarioComboBox()
        {
            //ScenarioComboBox.Items.Clear();

            //for (int i = 0; i < _syntheticBond.ActiveScenarios.Count; i++)
            //{
            //    string name = _syntheticBond.ActiveScenarios[i].ScriptName;
            //    ScenarioComboBox.Items.Add(new ComboBoxItem
            //    {
            //        Content = name,
            //        Name = name.Replace(" ", "_")
            //    });
            //}

            //if (ScenarioComboBox.Items.Count > 0)
            //{
            //    ScenarioComboBox.SelectedIndex = 0;
            //}
        }

        private void UpdateScenarioTextBoxDefault()
        {
            //int number = _syntheticBond.GetAvailableScenarioNumber() + 1;
            //ScenarioTextBox.Text = "Script " + number.ToString();
        }

        private void ScenarioComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            try
            {
                //int index = ScenarioComboBox.SelectedIndex;
                //if (index < 0 || index >= _syntheticBond.ActiveScenarios.Count)
                //{
                //    return;
                //}

                //_selectedScenario = _syntheticBond.ActiveScenarios[index];
                //_syntheticBond.SelectedScenario = _selectedScenario;
                //RefreshScenarioUi();
            }
            catch (Exception ex)
            {
                ServerMaster.SendNewLogMessage(ex.ToString(), Logging.LogMessageType.Error);
            }
        }

        private void RefreshScenarioUi()
        {
            if (_selectedScenario == null)
            {
                return;
            }

            _isUpdatingUi = true;

            ClearAllValidationStyles();

            MaxSpreadTextBox.Text = _selectedScenario.MaxSpread.ToString();
            MinSpreadTextBox.Text = _selectedScenario.MinSpread.ToString();

            EnterTotalVolumeSec1TextBox.Text = _selectedScenario.ArbitrationIceberg.MainLegs[0].TotalVolume.ToString();
            ExitTotalVolumeSec1TextBox.Text = EnterTotalVolumeSec1TextBox.Text;

            EnterTotalVolumeSec2TextBox.Text = _selectedScenario.ArbitrationIceberg.SecondaryLegs[0].TotalVolume.ToString();
            ExitTotalVolumeSec2TextBox.Text = EnterTotalVolumeSec2TextBox.Text;

            EnterOneOrderSec1TextBox.Text = _selectedScenario.ArbitrationIceberg.MainLegs[0].EnterOneOrderVolume.ToString();
            ExitOneOrderSec1TextBox.Text = _selectedScenario.ArbitrationIceberg.MainLegs[0].ExitOneOrderVolume.ToString();
            EnterOneOrderSec2TextBox.Text = _selectedScenario.ArbitrationIceberg.SecondaryLegs[0].EnterOneOrderVolume.ToString();
            ExitOneOrderSec2TextBox.Text = _selectedScenario.ArbitrationIceberg.SecondaryLegs[0].ExitOneOrderVolume.ToString();

            EnterSlippageSec1TextBox.Text = _selectedScenario.ArbitrationIceberg.MainLegs[0].EnterSlippage.ToString();
            ExitSlippageSec1TextBox.Text = _selectedScenario.ArbitrationIceberg.MainLegs[0].ExitSlippage.ToString();
            EnterSlippageSec2TextBox.Text = _selectedScenario.ArbitrationIceberg.SecondaryLegs[0].EnterSlippage.ToString();
            ExitSlippageSec2TextBox.Text = _selectedScenario.ArbitrationIceberg.SecondaryLegs[0].ExitSlippage.ToString();

            EnterLifetimeOrderSec1TextBox.Text = _selectedScenario.ArbitrationIceberg.MainLegs[0].EnterLifetimeOrder.ToString();
            ExitLifetimeOrderSec1TextBox.Text = _selectedScenario.ArbitrationIceberg.MainLegs[0].ExitLifetimeOrder.ToString();
            EnterLifetimeOrderSec2TextBox.Text = _selectedScenario.ArbitrationIceberg.SecondaryLegs[0].EnterLifetimeOrder.ToString();
            ExitLifetimeOrderSec2TextBox.Text = _selectedScenario.ArbitrationIceberg.SecondaryLegs[0].ExitLifetimeOrder.ToString();

            EnterOrderFrequencySec1TextBox.Text = _selectedScenario.ArbitrationIceberg.MainLegs[0].EnterOrderFrequency.ToString();
            ExitOrderFrequencySec1TextBox.Text = _selectedScenario.ArbitrationIceberg.MainLegs[0].ExitOrderFrequency.ToString();
            EnterOrderFrequencySec2TextBox.Text = _selectedScenario.ArbitrationIceberg.SecondaryLegs[0].EnterOrderFrequency.ToString();
            ExitOrderFrequencySec2TextBox.Text = _selectedScenario.ArbitrationIceberg.SecondaryLegs[0].ExitOrderFrequency.ToString();

            EnterTextBoxAssetPortfolioSec1.Text = _selectedScenario.ArbitrationIceberg.MainLegs[0].AssetPortfolio ?? string.Empty;
            ExitTextBoxAssetPortfolioSec1.Text = EnterTextBoxAssetPortfolioSec1.Text;
            EnterTextBoxAssetPortfolioSec2.Text = _selectedScenario.ArbitrationIceberg.SecondaryLegs[0].AssetPortfolio ?? string.Empty;
            ExitTextBoxAssetPortfolioSec2.Text = EnterTextBoxAssetPortfolioSec2.Text;

            CreateTradeModeComboBox();
            CreateEnterVolumeTypeSec1ComboBox();
            CreateEnterVolumeTypeSec2ComboBox();
            CreateExitVolumeTypeSec1ComboBox();
            CreateExitVolumeTypeSec2ComboBox();
            CreateEnterOrderTypeSec1ComboBox();
            CreateEnterOrderTypeSec2ComboBox();
            CreateExitOrderTypeSec1ComboBox();
            CreateExitOrderTypeSec2ComboBox();
            CreateEnterOrderPositionSec1ComboBox();
            CreateEnterOrderPositionSec2ComboBox();
            CreateExitOrderPositionSec1ComboBox();
            CreateExitOrderPositionSec2ComboBox();

            UpdateTabControlsLockState();

            ValidatePositiveValue(EnterTotalVolumeSec1TextBox);
            ValidatePositiveValue(EnterTotalVolumeSec2TextBox);

            ValidateOneOrderVolume(EnterOneOrderSec1TextBox, EnterTotalVolumeSec1TextBox);
            ValidateOneOrderVolume(EnterOneOrderSec2TextBox, EnterTotalVolumeSec2TextBox);

            ValidateOneOrderVolume(ExitOneOrderSec1TextBox, ExitTotalVolumeSec1TextBox);
            ValidateOneOrderVolume(ExitOneOrderSec2TextBox, ExitTotalVolumeSec2TextBox);

            ValidateLifetimeAndFrequency(EnterOrderTypeSec1ComboBox, EnterLifetimeOrderSec1TextBox, EnterOrderFrequencySec1TextBox);
            ValidateLifetimeAndFrequency(EnterOrderTypeSec2ComboBox, EnterLifetimeOrderSec2TextBox, EnterOrderFrequencySec2TextBox);
            ValidateLifetimeAndFrequency(ExitOrderTypeSec1ComboBox, ExitLifetimeOrderSec1TextBox, ExitOrderFrequencySec1TextBox);
            ValidateLifetimeAndFrequency(ExitOrderTypeSec2ComboBox, ExitLifetimeOrderSec2TextBox, ExitOrderFrequencySec2TextBox);

            UpdatePositionStepsDataGrids();

            _isUpdatingUi = false;
        }

        #endregion

        #region Step DataGrids

        private DataGridView CreateDataGrid(int columnCount)
        {
            DataGridView grid = DataGridFactory.GetDataGridView(
                DataGridViewSelectionMode.CellSelect,
                DataGridViewAutoSizeRowsMode.AllCells,
                false);
            grid.ScrollBars = ScrollBars.Vertical;

            DataGridViewTextBoxCell cellTemplate = new DataGridViewTextBoxCell();
            cellTemplate.Style = grid.DefaultCellStyle;

            for (int i = 0; i < columnCount; i++)
            {
                DataGridViewColumn column = new DataGridViewColumn
                {
                    CellTemplate = cellTemplate,
                    HeaderText = "",
                    ReadOnly = true,
                    AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill
                };

                grid.Columns.Add(column);
            }

            return grid;
        }

        private void CreatePositionStepsDataGrids()
        {
            const int stepsColumnCount = 5;
            const int statisticColumnCount = 4;

            // Open steps
            _gridOpenStepsBase = CreateDataGrid(stepsColumnCount);
            OpenStepsBaseHost.Child = _gridOpenStepsBase;

            _gridOpenStepsStatisticBase = CreateDataGrid(statisticColumnCount);
            OpenStepsBaseStatisticHost.Child = _gridOpenStepsStatisticBase;
            _gridOpenStepsStatisticBase.CellClick += _gridOpenStepsStatisticBase_CellClick;

            _gridOpenStepsFutures = CreateDataGrid(stepsColumnCount);
            OpenStepsFuturesHost.Child = _gridOpenStepsFutures;

            _gridOpenStepsStatisticFutures = CreateDataGrid(statisticColumnCount);
            OpenStepsFuturesStatisticHost.Child = _gridOpenStepsStatisticFutures;
            _gridOpenStepsStatisticFutures.CellClick += _gridOpenStepsStatisticFutures_CellClick;

            // Close steps
            _gridCloseStepsBase = CreateDataGrid(stepsColumnCount);
            CloseStepsBaseHost.Child = _gridCloseStepsBase;

            _gridCloseStepsStatisticBase = CreateDataGrid(statisticColumnCount);
            CloseStepsBaseStatisticHost.Child = _gridCloseStepsStatisticBase;
            _gridCloseStepsStatisticBase.CellClick += _gridCloseStepsStatisticBase_CellClick;

            _gridCloseStepsFutures = CreateDataGrid(stepsColumnCount);
            CloseStepsFuturesHost.Child = _gridCloseStepsFutures;

            _gridCloseStepsStatisticFutures = CreateDataGrid(statisticColumnCount);
            CloseStepsFuturesStatisticHost.Child = _gridCloseStepsStatisticFutures;
            _gridCloseStepsStatisticFutures.CellClick += _gridCloseStepsStatisticFutures_CellClick;
        }

        private void _gridCloseStepsStatisticFutures_CellClick(object sender, DataGridViewCellEventArgs e)
        {
            if (_gridCloseStepsStatisticFutures == null ||
                    (_gridCloseStepsStatisticFutures != null && _gridCloseStepsStatisticFutures.Rows == null) ||
                    (_gridCloseStepsStatisticFutures != null && _gridCloseStepsStatisticFutures.Rows != null && _gridCloseStepsStatisticFutures.Rows.Count == 0))
            {
                return;
            }

            GridCellClick(sender, e, _selectedScenario.ArbitrationIceberg.SecondaryLegs[0].ArbitrationLegStatistic.CurrentPosition, _selectedScenario.ArbitrationIceberg.SecondaryLegs[0].BotTab.StartProgram);
        }

        private void _gridCloseStepsStatisticBase_CellClick(object sender, DataGridViewCellEventArgs e)
        {
            if (_gridCloseStepsStatisticBase == null ||
                    (_gridCloseStepsStatisticBase != null && _gridCloseStepsStatisticBase.Rows == null) ||
                    (_gridCloseStepsStatisticBase != null && _gridCloseStepsStatisticBase.Rows != null && _gridCloseStepsStatisticBase.Rows.Count == 0))
            {
                return;
            }

            GridCellClick(sender, e, _selectedScenario.ArbitrationIceberg.SecondaryLegs[0].ArbitrationLegStatistic.CurrentPosition, _selectedScenario.ArbitrationIceberg.SecondaryLegs[0].BotTab.StartProgram);
        }

        private void _gridOpenStepsStatisticFutures_CellClick(object sender, DataGridViewCellEventArgs e)
        {
            if (_gridOpenStepsStatisticFutures == null ||
                    (_gridOpenStepsStatisticFutures != null && _gridOpenStepsStatisticFutures.Rows == null) ||
                    (_gridOpenStepsStatisticFutures != null && _gridOpenStepsStatisticFutures.Rows != null && _gridOpenStepsStatisticFutures.Rows.Count == 0))
            {
                return;
            }

            GridCellClick(sender, e, _selectedScenario.ArbitrationIceberg.SecondaryLegs[0].ArbitrationLegStatistic.CurrentPosition, _selectedScenario.ArbitrationIceberg.SecondaryLegs[0].BotTab.StartProgram);
        }

        private void _gridOpenStepsStatisticBase_CellClick(object sender, DataGridViewCellEventArgs e)
        {
            if (_gridOpenStepsStatisticBase == null ||
                    (_gridOpenStepsStatisticBase != null && _gridOpenStepsStatisticBase.Rows == null) ||
                    (_gridOpenStepsStatisticBase != null && _gridOpenStepsStatisticBase.Rows != null && _gridOpenStepsStatisticBase.Rows.Count == 0))
            {
                return;
            }

            GridCellClick(sender, e, _selectedScenario.ArbitrationIceberg.MainLegs[0].ArbitrationLegStatistic.CurrentPosition, _selectedScenario.ArbitrationIceberg.MainLegs[0].BotTab.StartProgram);
        }

        private void GridCellClick(object sender, DataGridViewCellEventArgs e, Position position, StartProgram startProgram)
        {
            int column = e.ColumnIndex;
            int row = e.RowIndex;

            if (column != 0)
                return;

            if (row == 0)
                return;

            if (position == null)
                return;

            PositionUi positionUi = new PositionUi(position, startProgram);
            positionUi.ShowDialog();
        }

        private void UpdatePositionStepsDataGrids()
        {
            try
            {
                ArbitrationLeg mainLeg = _selectedScenario.ArbitrationIceberg.MainLegs[0];
                ArbitrationLeg secondaryLeg = _selectedScenario.ArbitrationIceberg.SecondaryLegs[0];

                UpdateOpenStepsBase(mainLeg);
                UpdateOpenStepsStatisticBase(mainLeg);
                UpdateOpenStepsFutures(secondaryLeg);
                UpdateOpenStepsStatisticFutures(secondaryLeg);

                UpdateCloseStepsBase(mainLeg);
                UpdateCloseStepsStatisticBase(mainLeg);
                UpdateCloseStepsFutures(secondaryLeg);
                UpdateCloseStepsStatisticFutures(secondaryLeg);
            }
            catch (Exception ex)
            {
                ServerMaster.SendNewLogMessage(ex.ToString(), LogMessageType.Error);
            }
        }

        private void UpdateCloseStepsStatisticFutures(ArbitrationLeg secondaryLeg)
        {
            if (_gridCloseStepsStatisticFutures == null) return;
            if (_gridCloseStepsStatisticFutures.InvokeRequired)
            {
                _gridCloseStepsStatisticFutures.Invoke(new Action<ArbitrationLeg>(UpdateCloseStepsStatisticFutures), secondaryLeg);
                return;
            }

            UpdateDataGridView(_gridCloseStepsStatisticFutures, GetRowsToStatisticGrid(secondaryLeg, isEnter: false));

        }

        private void UpdateOpenStepsStatisticFutures(ArbitrationLeg secondaryLeg)
        {
            if (_gridOpenStepsStatisticFutures == null) return;
            if (_gridOpenStepsStatisticFutures.InvokeRequired)
            {
                _gridOpenStepsStatisticFutures.Invoke(new Action<ArbitrationLeg>(UpdateOpenStepsStatisticFutures), secondaryLeg);
                return;
            }

            UpdateDataGridView(_gridOpenStepsStatisticFutures, GetRowsToStatisticGrid(secondaryLeg, isEnter: true));
        }


        private void UpdateCloseStepsStatisticBase(ArbitrationLeg mainLeg)
        {
            if (_gridCloseStepsStatisticBase == null) return;
            if (_gridCloseStepsStatisticBase.InvokeRequired)
            {
                _gridCloseStepsStatisticBase.Invoke(new Action<ArbitrationLeg>(UpdateCloseStepsStatisticBase), mainLeg);
                return;
            }

            UpdateDataGridView(_gridCloseStepsStatisticBase, GetRowsToStatisticGrid(mainLeg, isEnter: false));
        }

        private void UpdateOpenStepsStatisticBase(ArbitrationLeg mainLeg)
        {
            if (_gridOpenStepsStatisticBase == null) return;
            if (_gridOpenStepsStatisticBase.InvokeRequired)
            {
                _gridOpenStepsStatisticBase.Invoke(new Action<ArbitrationLeg>(UpdateOpenStepsStatisticBase), mainLeg);
                return;
            }

            UpdateDataGridView(_gridOpenStepsStatisticBase, GetRowsToStatisticGrid(mainLeg, isEnter: true));
        }

        private List<DataGridViewRow> GetRowsToStatisticGrid(ArbitrationLeg mainLeg, bool isEnter)
        {
            List<DataGridViewRow> rows = new List<DataGridViewRow>();

            rows.Add(GetFirsStatisticGridRow());

            ArbitrationLegStatistic legStatistic = mainLeg.ArbitrationLegStatistic;

            if (legStatistic == null)
            {
                return rows;
            }

            DataGridViewRow nRow = new DataGridViewRow();
            DataGridViewButtonCell cell0 = new DataGridViewButtonCell(); // 0 Кнопка позиции

            if (legStatistic.CurrentPosition == null)
                cell0.Value = "Позиций нет";
            else
                cell0.Value = "Позиция " + legStatistic.CurrentPosition.Number;

            cell0.Style.Alignment = DataGridViewContentAlignment.MiddleCenter;
            nRow.Cells.Add(cell0);

            DataGridViewTextBoxCell cell1 = new DataGridViewTextBoxCell(); // 1 Направление
            cell1.Value = legStatistic.Side.ToString();
            cell1.Style.Alignment = DataGridViewContentAlignment.MiddleCenter;
            nRow.Cells.Add(cell1);

            DataGridViewTextBoxCell cell2 = new DataGridViewTextBoxCell(); // 2 Открытый объем
            cell2.Value = legStatistic.OpenVolume.ToString();
            cell2.Style.Alignment = DataGridViewContentAlignment.MiddleCenter;
            nRow.Cells.Add(cell2);

            DataGridViewTextBoxCell cell3 = new DataGridViewTextBoxCell(); // 3 Нужный объем
            cell3.Value = legStatistic.TotalVolumeLot.ToString();
            cell3.Style.Alignment = DataGridViewContentAlignment.MiddleCenter;
            nRow.Cells.Add(cell3);

            rows.Add(nRow);

            return rows;
        }

        private void UpdateCloseStepsFutures(ArbitrationLeg secondaryLeg)
        {
            if (_gridCloseStepsFutures == null) return;
            if (_gridCloseStepsFutures.InvokeRequired)
            {
                _gridCloseStepsFutures.Invoke(new Action<ArbitrationLeg>(UpdateCloseStepsFutures), secondaryLeg);
                return;
            }

            UpdateDataGridView(_gridCloseStepsFutures, GetRowsToStepsGrid(secondaryLeg, isEnter: false));
        }

        private void UpdateOpenStepsFutures(ArbitrationLeg secondaryLeg)
        {
            if (_gridOpenStepsFutures == null) return;
            if (_gridOpenStepsFutures.InvokeRequired)
            {
                _gridOpenStepsFutures.Invoke(new Action<ArbitrationLeg>(UpdateOpenStepsFutures), secondaryLeg);
                return;
            }

            UpdateDataGridView(_gridOpenStepsFutures, GetRowsToStepsGrid(secondaryLeg, isEnter: true));
        }

        private void UpdateCloseStepsBase(ArbitrationLeg mainLeg)
        {
            if (_gridCloseStepsBase == null) return;
            if (_gridCloseStepsBase.InvokeRequired)
            {
                _gridCloseStepsBase.Invoke(new Action<ArbitrationLeg>(UpdateCloseStepsBase), mainLeg);
                return;
            }

            UpdateDataGridView(_gridCloseStepsBase, GetRowsToStepsGrid(mainLeg, isEnter: false));
        }

        private void UpdateOpenStepsBase(ArbitrationLeg mainLeg)
        {
            if (_gridOpenStepsBase == null) return;
            if (_gridOpenStepsBase.InvokeRequired)
            {
                _gridOpenStepsBase.Invoke(new Action<ArbitrationLeg>(UpdateOpenStepsBase), mainLeg);
                return;
            }

            UpdateDataGridView(_gridOpenStepsBase, GetRowsToStepsGrid(mainLeg, isEnter: true));
        }

        private void UpdateDataGridView(DataGridView grid, List<DataGridViewRow> newRows)
        {
            if (newRows == null) return;

            int existingCount = grid.Rows.Count;
            int newCount = newRows.Count;

            if (existingCount != newCount)
            {
                int showRow = grid.FirstDisplayedScrollingRowIndex;
                grid.Rows.Clear();

                for (int i = 0; i < newCount; i++)
                    grid.Rows.Add(newRows[i]);

                if (showRow > 0 && showRow < grid.Rows.Count)
                    grid.FirstDisplayedScrollingRowIndex = showRow;
            }
            else
            {
                for (int i = 0; i < newCount; i++)
                {
                    for (int j = 0; j < grid.ColumnCount; j++)
                    {
                        DataGridViewCell existingCell = grid.Rows[i].Cells[j];
                        DataGridViewCell newCell = newRows[i].Cells[j];

                        if (!Equals(existingCell.Value, newCell.Value))
                            existingCell.Value = newCell.Value;

                        if (existingCell.Style.ForeColor != newCell.Style.ForeColor)
                            existingCell.Style.ForeColor = newCell.Style.ForeColor;

                        if (existingCell.Style.BackColor != newCell.Style.BackColor)
                            existingCell.Style.BackColor = newCell.Style.BackColor;
                    }
                }
            }
        }

        private List<DataGridViewRow> GetRowsToStepsGrid(ArbitrationLeg leg, bool isEnter)
        {
            List<DataGridViewRow> rows = new List<DataGridViewRow>();

            rows.Add(GetFirsStepsGridRow());

            List<ArbitrationStep> steps = null;

            if (isEnter)
            {
                steps = leg.EnterArbitrationSteps;
            }
            else
            {
                steps = leg.ExitArbitrationSteps;
            }

            for (int i = 0; steps != null && i < steps.Count; i++)
            {
                DataGridViewRow nRow = new DataGridViewRow();
                ArbitrationStep step = steps[i];

                if (step == null)
                {
                    continue;
                }

                DataGridViewTextBoxCell cell0 = new DataGridViewTextBoxCell(); // 0 Шаг
                cell0.Value = step.NumberStep.ToString();
                cell0.Style.Alignment = DataGridViewContentAlignment.MiddleCenter;
                nRow.Cells.Add(cell0);

                DataGridViewTextBoxCell cell2 = new DataGridViewTextBoxCell(); // 1 Время
                cell2.Value = step.TimeActivateStep.ToString();
                cell2.Style.Alignment = DataGridViewContentAlignment.MiddleCenter;
                nRow.Cells.Add(cell2);

                DataGridViewTextBoxCell cell3 = new DataGridViewTextBoxCell(); // 2 Статус
                cell3.Value = step.Status.ToString();
                cell3.Style.Alignment = DataGridViewContentAlignment.MiddleCenter;
                nRow.Cells.Add(cell3);

                DataGridViewTextBoxCell cell4 = new DataGridViewTextBoxCell(); // 3 Открытый объем
                cell4.Value = step.OpenVolume.ToString();
                cell4.Style.Alignment = DataGridViewContentAlignment.MiddleCenter;
                nRow.Cells.Add(cell4);

                DataGridViewTextBoxCell cell5 = new DataGridViewTextBoxCell(); // 4 Нужный объем
                cell5.Value = step.VolumeStep.ToString();
                cell5.Style.Alignment = DataGridViewContentAlignment.MiddleCenter;
                nRow.Cells.Add(cell5);

                rows.Add(nRow);
            }

            return rows;
        }

        private DataGridViewRow GetFirsStatisticGridRow()
        {
            DataGridViewRow nRow = new DataGridViewRow();

            DataGridViewTextBoxCell cell0 = new DataGridViewTextBoxCell(); // 0 Кнопка позиции
            cell0.Value = "Позиция";
            cell0.Style.Alignment = DataGridViewContentAlignment.MiddleCenter;
            nRow.Cells.Add(cell0);

            DataGridViewTextBoxCell cell1 = new DataGridViewTextBoxCell(); // 1 Направление
            cell1.Value = "Направление";
            cell1.Style.Alignment = DataGridViewContentAlignment.MiddleCenter;
            nRow.Cells.Add(cell1);

            DataGridViewTextBoxCell cell2 = new DataGridViewTextBoxCell(); // 2 Открытый объем
            cell2.Value = "Открытый объем";
            cell2.Style.Alignment = DataGridViewContentAlignment.MiddleCenter;
            nRow.Cells.Add(cell2);

            DataGridViewTextBoxCell cell3 = new DataGridViewTextBoxCell(); // 3 Нужный объем
            cell3.Value = "Нужный объем";
            cell3.Style.Alignment = DataGridViewContentAlignment.MiddleCenter;
            nRow.Cells.Add(cell3);

            return nRow;
        }

        private DataGridViewRow GetFirsStepsGridRow()
        {
            DataGridViewRow nRow = new DataGridViewRow();

            DataGridViewTextBoxCell cell0 = new DataGridViewTextBoxCell(); // 0 Номер шага
            cell0.Value = "Шаг";
            cell0.Style.Alignment = DataGridViewContentAlignment.MiddleCenter;
            nRow.Cells.Add(cell0);

            DataGridViewTextBoxCell cell2 = new DataGridViewTextBoxCell(); // 1 Время
            cell2.Value = "Время шага";
            cell2.Style.Alignment = DataGridViewContentAlignment.MiddleCenter;
            nRow.Cells.Add(cell2);

            DataGridViewTextBoxCell cell3 = new DataGridViewTextBoxCell(); // 2 Статус
            cell3.Value = "Статус";
            cell3.Style.Alignment = DataGridViewContentAlignment.MiddleCenter;
            nRow.Cells.Add(cell3);

            DataGridViewTextBoxCell cell4 = new DataGridViewTextBoxCell(); // 3 Открытый объем
            cell4.Value = "Открытый объем";
            cell4.Style.Alignment = DataGridViewContentAlignment.MiddleCenter;
            nRow.Cells.Add(cell4);

            DataGridViewTextBoxCell cell5 = new DataGridViewTextBoxCell(); // 4 Нужный объем
            cell5.Value = "Нужный объем";
            cell5.Style.Alignment = DataGridViewContentAlignment.MiddleCenter;
            nRow.Cells.Add(cell5);

            return nRow;
        }

        #endregion

        #region Log

        private void SubscribeToScenarioEvents()
        {
            for (int i = 0; i < _syntheticBond.ActiveScenarios.Count; i++)
            {
                SubscribeToScenario(_syntheticBond.ActiveScenarios[i]);
            }
        }

        private void SubscribeToScenario(BondScenario scenario)
        {
            scenario.ArbitrationIceberg.InfoLogEvent += OnIcebergInfoLog;
            scenario.ScenarioFilledEvent += OnScenarioFilled;
            scenario.ScenarioClosedEvent += OnScenarioClosed;
        }

        private void OnIcebergInfoLog(string message)
        {
            AddLogMessage(message, LogMessageType.System);
        }

        private void OnScenarioFilled(string scenarioName)
        {
            AddLogMessage("Сценарий \"" + scenarioName + "\": все ноги набрали целевой объём", LogMessageType.System);
        }

        private void OnScenarioClosed(string scenarioName)
        {
            AddLogMessage("Сценарий \"" + scenarioName + "\": все позиции закрыты", LogMessageType.System);
        }

        /// <summary>
        /// Добавляет запись в лог-таблицу
        /// </summary>
        public void AddLogMessage(string message, LogMessageType type)
        {
            try
            {
                SynteticBondLogEntry entry = new SynteticBondLogEntry
                {
                    Time = DateTime.Now,
                    Type = type.ToString(),
                    Message = message
                };

                if (LogDataGrid.Dispatcher.CheckAccess())
                {
                    InsertLogEntry(entry);
                }
                else
                {
                    LogDataGrid.Dispatcher.Invoke(() =>
                    {
                        InsertLogEntry(entry);
                    });
                }
            }
            catch (Exception ex)
            {
                ServerMaster.SendNewLogMessage(ex.ToString(), LogMessageType.Error);
            }
        }

        private void InsertLogEntry(SynteticBondLogEntry entry)
        {
            LogDataGrid.Items.Insert(0, entry);

            if (LogDataGrid.Items.SortDescriptions.Count > 0)
            {
                LogDataGrid.Items.Refresh();
            }

            LogDataGrid.ScrollIntoView(LogDataGrid.Items[0]);
        }

        #endregion
    }

    /// <summary>
    /// Запись лога для SynteticBondTradeUi
    /// </summary>
    public class SynteticBondLogEntry
    {
        public DateTime Time { get; set; }
        public string Type { get; set; }
        public string Message { get; set; }
    }
}
