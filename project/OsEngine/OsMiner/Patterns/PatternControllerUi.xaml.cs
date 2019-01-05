﻿using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms;
using System.Windows.Forms.Integration;
using System.Windows.Input;
using OsEngine.Entity;
using OsEngine.Logging;
using OsEngine.Market.Servers.Tester;
using ContextMenu = System.Windows.Forms.ContextMenu;
using DataGrid = System.Windows.Controls.DataGrid;
using MenuItem = System.Windows.Forms.MenuItem;
using MouseEventArgs = System.Windows.Forms.MouseEventArgs;

namespace OsEngine.OsMiner.Patterns
{
    /// <summary>
    /// Логика взаимодействия для PatternControllerUi.xaml
    /// </summary>
    public partial class PatternControllerUi
    {

        private PatternController _pattern;

        public PatternControllerUi(PatternController pattern)
        {
            InitializeComponent();
            _pattern = pattern;

            InitializeTabDataSeries();
            InitializeTabOpenPosition();

            InitializeTabClosePosition();

            _gridPatternsToOpen = DataGridFactory.GetDataGridView(DataGridViewSelectionMode.FullRowSelect, DataGridViewAutoSizeRowsMode.None);
            _gridPatternsToOpen.MouseClick += _gridPatternsToOpen_MouseClick;
            _gridPatternsToOpen.CellValueChanged += _gridPatternsToOpen_CellValueChanged;

            _gridPatternsToClose = DataGridFactory.GetDataGridView(DataGridViewSelectionMode.FullRowSelect, DataGridViewAutoSizeRowsMode.None);
            _gridPatternsToClose.MouseClick += _gridPatternsToClose_MouseClick;
            _gridPatternsToClose.CellValueChanged += _gridPatternsToClose_CellValueChanged;

            CreateGridPatternsGrid(_gridPatternsToOpen, HostGridPatternsToOpen);
            CreateGridPatternsGrid(_gridPatternsToClose, HostGridPatternToClose);

            PaintGridPatternsToOpen();
            PaintGridPatternsToClose();

            InitializeTabPatternsSearch();

            InitializeMiningTab();
        }

// Авто поиск паттернов

        private void InitializeMiningTab()
        {
            TextBoxMiningMo.Text = _pattern.MiningMo.ToString(CultureInfo.InvariantCulture);
            TextBoxMiningMo.TextChanged += TextBoxMiningMoOnTextChanged;

            TextBoxMiningDealsCount.Text = _pattern.MiningDealsCount.ToString(CultureInfo.InvariantCulture);
            TextBoxMiningDealsCount.TextChanged += TextBoxMiningDealsCountOnTextChanged;

            TextBoxMiningProfit.Text = _pattern.MiningProfit.ToString(CultureInfo.InvariantCulture);
            TextBoxMiningProfit.TextChanged += TextBoxMiningProfitOnTextChanged;
        }

        private void TextBoxMiningProfitOnTextChanged(object sender, TextChangedEventArgs textChangedEventArgs)
        {
            try
            {
                _pattern.MiningProfit = Convert.ToDecimal(TextBoxMiningProfit.Text.Replace(",",
                    CultureInfo.InvariantCulture.NumberFormat.NumberDecimalSeparator), CultureInfo.InvariantCulture);
            }
            catch (Exception)
            {
                TextBoxMiningProfit.Text = _pattern.MiningProfit.ToString(CultureInfo.InvariantCulture);
            }
            _pattern.Save();
        }

        private void TextBoxMiningDealsCountOnTextChanged(object sender, TextChangedEventArgs textChangedEventArgs)
        {
            try
            {
                _pattern.MiningDealsCount = Convert.ToInt32(TextBoxMiningDealsCount.Text);
            }
            catch (Exception)
            {
                TextBoxMiningDealsCount.Text = _pattern.MiningDealsCount.ToString();
            }
            _pattern.Save();
        }

        private void TextBoxMiningMoOnTextChanged(object sender, TextChangedEventArgs textChangedEventArgs)
        {
            try
            {
                _pattern.MiningMo = Convert.ToDecimal(TextBoxMiningMo.Text.Replace(",",
                    CultureInfo.InvariantCulture.NumberFormat.NumberDecimalSeparator), CultureInfo.InvariantCulture);
            }
            catch (Exception)
            {
                TextBoxMiningMo.Text = _pattern.MiningMo.ToString(CultureInfo.InvariantCulture);
            }
            _pattern.Save();
        }

        private void ButtonStartMining_Click(object sender, RoutedEventArgs e)
        {
            _pattern.StartMining();
        }

        private void ButtonStopMining_Click(object sender, RoutedEventArgs e)
        {
            _pattern.StopMining();
        }

        private void ButtonJournals_Click(object sender, RoutedEventArgs e)
        {
            _pattern.ShowTestResults();
        }

// вкладка поиска паттернов

        void InitializeTabPatternsSearch()
        {
            ComboBoxPlaceToUsePattern.Items.Add(UsePatternType.OpenPosition);
            ComboBoxPlaceToUsePattern.Items.Add(UsePatternType.ClosePosition);
            ComboBoxPlaceToUsePattern.SelectedItem = _pattern.PlaceToUsePattern;
            ComboBoxPlaceToUsePattern.SelectionChanged += ComboBoxPlaceToUsePattern_SelectionChanged;

            TextBoxWeigthToTempPattern.Text = _pattern.WeigthToTempPattern.ToString(CultureInfo.InvariantCulture);
            TextBoxWeigthToTempPattern.TextChanged += TextBoxWeigthToTempPattern_TextChanged;

            _pattern.PaintController(HostTempPattern,HostSinglePatternToOpen,HostSinglePatternToClose);
            _pattern.BackTestEndEvent += _pattern_BackTestEndEvent;

            TextBoxExpandToTempPattern.Text = _pattern.ExpandToTempPattern.ToString(CultureInfo.InvariantCulture);
            TextBoxExpandToTempPattern.TextChanged += TextBoxExpandToTempPattern_TextChanged;

            InitializeTimePatternTab();
            InitializePatternIndicatorsTab();
            InitializeVolumePatternTab();
            InitializeCandlePatternTab();

            TabControlTypePatternsToFind.MouseUp += TabControlTypePatternsToFind_MouseUp;
        }

        void TextBoxExpandToTempPattern_TextChanged(object sender, TextChangedEventArgs e)
        {
            try
            {
                _pattern.ExpandToTempPattern = Convert.ToDecimal(TextBoxExpandToTempPattern.Text.Replace(",",
                    CultureInfo.InvariantCulture.NumberFormat.NumberDecimalSeparator), CultureInfo.InvariantCulture);
            }
            catch (Exception)
            {
                TextBoxExpandToTempPattern.Text = _pattern.ExpandToTempPattern.ToString(CultureInfo.InvariantCulture);
            }
            _pattern.Save();
        }

        void _pattern_BackTestEndEvent(string shortReport)
        {
            if (TextBoxShortReportToTempPattern.Dispatcher.CheckAccess() == false)
            {
                TextBoxShortReportToTempPattern.Dispatcher.Invoke(new Action<string>(_pattern_BackTestEndEvent), shortReport);
                return;
            }

            TextBoxShortReportToTempPattern.Text = shortReport;
            TabControlPrime.IsEnabled = true;
        }

        void TextBoxWeigthToTempPattern_TextChanged(object sender, TextChangedEventArgs e)
        {
            try
            {
                _pattern.WeigthToTempPattern = Convert.ToDecimal(TextBoxWeigthToTempPattern.Text.Replace(",",
                    CultureInfo.InvariantCulture.NumberFormat.NumberDecimalSeparator), CultureInfo.InvariantCulture);
            }
            catch (Exception)
            {
                TextBoxWeigthToTempPattern.Text = _pattern.WeigthToTempPattern.ToString(CultureInfo.InvariantCulture);
            }
            _pattern.Save();
        }

        void ComboBoxPlaceToUsePattern_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            UsePatternType newType;

            Enum.TryParse(ComboBoxPlaceToUsePattern.SelectedItem.ToString(), out newType);

            if (newType == UsePatternType.ClosePosition &&
                _pattern.PatternsToOpen.Count == 0)
            {
                ComboBoxPlaceToUsePattern.SelectedItem = UsePatternType.OpenPosition;
                return;
            }

            _pattern.PlaceToUsePattern = newType;
            _pattern.Save();
        }

        private void ButtonReload_Click(object sender, RoutedEventArgs e)
        {
            _pattern.TestCurrentPatterns();
        }

        private void ButtonReloadTempPattern_Click(object sender, RoutedEventArgs e)
        {
            _pattern.BackTestTempPattern(true);
        }

        private void ButtonTempPatternJournal_Click(object sender, RoutedEventArgs e)
        {
            _pattern.ShowJournal();
        }

        private void ButtonSaveTempPattern_Click(object sender, RoutedEventArgs e)
        {
            _pattern.SaveTempPattern();
            PaintGridPatternsToOpen();
            PaintGridPatternsToClose();
        }

// индивидуальные вкладки паттернов

        void TabControlTypePatternsToFind_MouseUp(object sender, MouseButtonEventArgs e)
        {
            if (TabControlTypePatternsToFind.SelectedIndex == 0)
            {
               _pattern.GetTempPattern(PatternType.Candle);
            }
            else if (TabControlTypePatternsToFind.SelectedIndex == 1)
            {
                _pattern.GetTempPattern(PatternType.Volume);
            }
            else if (TabControlTypePatternsToFind.SelectedIndex == 2)
            {
                _pattern.GetTempPattern(PatternType.Time);
            }
            else if (TabControlTypePatternsToFind.SelectedIndex == 3)
            {
                _pattern.GetTempPattern(PatternType.Indicators);
            }
        }

        void InitializeCandlePatternTab()
        {
            PatternCandle pattern = (PatternCandle)_pattern.GetTempPattern(PatternType.Candle);

            TextBoxCandlePatternLength.TextChanged -= TextBoxCandlePatternLength_TextChanged;
            TextBoxCandlePatternLength.Text = pattern.Length.ToString();
            TextBoxCandlePatternLength.TextChanged += TextBoxCandlePatternLength_TextChanged;

            ComboBoxTypeWatchCandlePattern.Items.Clear();
            ComboBoxTypeWatchCandlePattern.Items.Add(TypeWatchCandlePattern.ShadowAndBody);
            ComboBoxTypeWatchCandlePattern.Items.Add(TypeWatchCandlePattern.Shadow);
            ComboBoxTypeWatchCandlePattern.Items.Add(TypeWatchCandlePattern.Body);
            ComboBoxTypeWatchCandlePattern.SelectedItem = pattern.TypeWatch;

            ComboBoxTypeWatchCandlePattern.SelectionChanged -= ComboBoxTypeWatchCandlePattern_SelectionChanged;
            ComboBoxTypeWatchCandlePattern.SelectionChanged += ComboBoxTypeWatchCandlePattern_SelectionChanged;
        }

        void TextBoxCandlePatternLength_TextChanged(object sender, TextChangedEventArgs e)
        {
            PatternCandle pattern = (PatternCandle)_pattern.GetTempPattern(PatternType.Candle);
            try
            {
                pattern.Length = Convert.ToInt32(TextBoxCandlePatternLength.Text);
            }
            catch (Exception)
            {
                TextBoxCandlePatternLength.Text = pattern.Length.ToString();
            }
            _pattern.GetPatternToIndex();
            _pattern.Save();
        }

        void ComboBoxTypeWatchCandlePattern_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if(ComboBoxTypeWatchCandlePattern.SelectedItem == null)
            {
                return;
            }
            PatternCandle pattern = (PatternCandle)_pattern.GetTempPattern(PatternType.Candle);
            Enum.TryParse(ComboBoxTypeWatchCandlePattern.SelectedItem.ToString(), out pattern.TypeWatch);
        }

        void InitializeVolumePatternTab()
        {
            PatternVolume pattern = (PatternVolume)_pattern.GetTempPattern(PatternType.Volume);
            TextBoxVolumePatternLength.Text = pattern.Length.ToString();
            TextBoxVolumePatternLength.TextChanged += TextBoxVolumePatternLength_TextChanged;
        }

        void TextBoxVolumePatternLength_TextChanged(object sender, TextChangedEventArgs e)
        {
            PatternVolume pattern = (PatternVolume)_pattern.GetTempPattern(PatternType.Volume);
            try
            {
                
                pattern.Length = Convert.ToInt32(TextBoxVolumePatternLength.Text);
            }
            catch (Exception)
            {
                TextBoxVolumePatternLength.Text = pattern.Length.ToString();
            }
            _pattern.GetPatternToIndex();
            _pattern.Save();
        }

        void InitializeTimePatternTab()
        {
           
            PatternTime pattern = (PatternTime)_pattern.GetTempPattern(PatternType.Time);

            TextBoxPatternTimeStartTime.Text = pattern.StartTime.ToShortTimeString();
            TextBoxPatternTimeStartTime.TextChanged += TextBoxPatternTimeStartTime_TextChanged;

            TextBoxPatternTimeEndTime.Text = pattern.EndTime.ToShortTimeString();
            TextBoxPatternTimeEndTime.TextChanged += TextBoxPatternTimeEndTime_TextChanged;
        }

        void TextBoxPatternTimeEndTime_TextChanged(object sender, TextChangedEventArgs e)
        {
            PatternTime pattern = (PatternTime)_pattern.GetTempPattern(PatternType.Time);
            try
            {
                pattern.EndTime = Convert.ToDateTime(TextBoxPatternTimeEndTime.Text);
            }
            catch (Exception)
            {
                TextBoxPatternTimeEndTime.Text = pattern.EndTime.ToShortTimeString();
            }
            _pattern.Save();
        }

        void TextBoxPatternTimeStartTime_TextChanged(object sender, TextChangedEventArgs e)
        {
            PatternTime pattern = (PatternTime)_pattern.GetTempPattern(PatternType.Time);
            try
            {
                pattern.StartTime = Convert.ToDateTime(TextBoxPatternTimeStartTime.Text);
            }
            catch (Exception)
            {
                TextBoxPatternTimeStartTime.Text = pattern.StartTime.ToShortTimeString();
            }
            _pattern.Save();
        }

        void InitializePatternIndicatorsTab()
        {
            PatternIndicators pattern = (PatternIndicators)_pattern.GetTempPattern(PatternType.Indicators);

            TextBoxPatternIndicatorLenght.Text = pattern.Length.ToString();
            TextBoxPatternIndicatorLenght.TextChanged += TextBoxPatternIndicatorLenght_TextChanged;

            ComboBoxPatternIndicatorSearchType.Items.Add(PatternIndicatorSearchType.CandlePosition);
            ComboBoxPatternIndicatorSearchType.Items.Add(PatternIndicatorSearchType.IndicatorsAngle);
            ComboBoxPatternIndicatorSearchType.SelectedItem = pattern.SearchType;
            ComboBoxPatternIndicatorSearchType.SelectionChanged += ComboBoxPatternIndicatorSearchType_SelectionChanged;

        }

        void ComboBoxPatternIndicatorSearchType_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            PatternIndicators pattern = (PatternIndicators)_pattern.GetTempPattern(PatternType.Indicators);

            Enum.TryParse(ComboBoxPatternIndicatorSearchType.SelectedValue.ToString(), out pattern.SearchType);
            _pattern.Save();
        }

        void TextBoxPatternIndicatorLenght_TextChanged(object sender, TextChangedEventArgs e)
        {
            PatternIndicators pattern = (PatternIndicators)_pattern.GetTempPattern(PatternType.Indicators);
            try
            {
                pattern.Length = Convert.ToInt32(TextBoxPatternIndicatorLenght.Text);
            }
            catch (Exception)
            {
                TextBoxPatternIndicatorLenght.Text = pattern.Length.ToString();
            }
            _pattern.GetPatternToIndex();
            _pattern.Save();
        }

// вкладка открытие позиции ПЕРЕМЕННЫЕ

        void InitializeTabOpenPosition()
        {
            ComboBoxSideInter.Items.Add(Side.Buy.ToString());
            ComboBoxSideInter.Items.Add(Side.Sell.ToString());
            ComboBoxSideInter.SelectedItem = _pattern.SideInter.ToString();
            ComboBoxSideInter.SelectionChanged += ComboBoxSideInter_SelectionChanged;

            InitializeComboBoxSecurityToInter();

            TextBoxWeigthToInter.Text = _pattern.WeigthToInter.ToString(CultureInfo.InvariantCulture);
            TextBoxWeigthToInter.TextChanged += TextBoxWeigthToInter_TextChanged;

            ComboBoxLotsCountType.Items.Add(LotsCountType.All);
            ComboBoxLotsCountType.Items.Add(LotsCountType.One);
            ComboBoxLotsCountType.SelectedItem = _pattern.LotsCount;
            ComboBoxLotsCountType.SelectionChanged += ComboBoxLotsCountType_SelectionChanged;
        }

        void ComboBoxLotsCountType_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            Enum.TryParse(ComboBoxLotsCountType.SelectedItem.ToString(), out _pattern.LotsCount);
        }

        void InitializeComboBoxSecurityToInter()
        {
            if (ComboBoxSecurityToInter.Dispatcher.CheckAccess() == false)
            {
                ComboBoxSecurityToInter.Dispatcher.Invoke(InitializeComboBoxSecurityToInter);
                return;
            }

            ComboBoxSecurityToInter.SelectionChanged -= ComboBoxSecurityToInter_SelectionChanged;

            ComboBoxSecurityToInter.Items.Clear();

            for (int i = 0; _pattern.CandleSeries != null && i < _pattern.CandleSeries.Count; i++)
            {
                ComboBoxSecurityToInter.Items.Add(_pattern.CandleSeries[i].Security.Name);
            }
            if (ComboBoxSecurityToInter.Items.Count != 0 &&
                !string.IsNullOrEmpty(_pattern.SecurityToInter))
            {
                ComboBoxSecurityToInter.SelectedItem = _pattern.SecurityToInter;
            }
            else if (ComboBoxSecurityToInter.Items.Count == 0 && !string.IsNullOrEmpty(_pattern.SecurityToInter))
            {
                ComboBoxSecurityToInter.Items.Add(_pattern.SecurityToInter);
                ComboBoxSecurityToInter.SelectedItem = _pattern.SecurityToInter;
            }

            ComboBoxSecurityToInter.SelectionChanged += ComboBoxSecurityToInter_SelectionChanged;
        }

        void TextBoxWeigthToInter_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (TextBoxWeigthToInter.Text == "" ||
                TextBoxWeigthToInter.Text == "0," ||
                TextBoxWeigthToInter.Text == "0.")
            {
                return;
            } 
            try
            {
                _pattern.WeigthToInter = Convert.ToDecimal(TextBoxWeigthToInter.Text.Replace(",",
                    CultureInfo.InvariantCulture.NumberFormat.NumberDecimalSeparator), CultureInfo.InvariantCulture);
            }
            catch (Exception)
            {
                TextBoxWeigthToInter.Text = _pattern.WeigthToInter.ToString(CultureInfo.InvariantCulture);
            }
            _pattern.Save();
        }

        void ComboBoxSecurityToInter_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ComboBoxSecurityToInter.SelectedItem == null)
            {
                return;
            }

            _pattern.SecurityToInter = ComboBoxSecurityToInter.SelectedItem.ToString();
            _pattern.Save();
        }

        void ComboBoxSideInter_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            Enum.TryParse(ComboBoxSideInter.SelectedItem.ToString(), out _pattern.SideInter);
            _pattern.Save();
        }

// вкладка открытие позиции РАБОТА С ГРИДАМИ

        private DataGridView _gridPatternsToOpen;

        void CreateGridPatternsGrid(DataGridView grid, WindowsFormsHost host)
        {

            DataGridViewTextBoxCell cell0 = new DataGridViewTextBoxCell();
            cell0.Style = grid.DefaultCellStyle;

            DataGridViewComboBoxCell cellComboBox = new DataGridViewComboBoxCell();
            cellComboBox.Style = grid.DefaultCellStyle;

            DataGridViewColumn column0 = new DataGridViewColumn();
            column0.CellTemplate = cell0;
            column0.HeaderText = @"№";
            column0.ReadOnly = true;
            column0.Width = 40;

            grid.Columns.Add(column0);

            DataGridViewColumn column1 = new DataGridViewColumn();
            column1.CellTemplate = cell0;
            column1.HeaderText = @"Тип";
            column1.ReadOnly = true;
            column1.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            grid.Columns.Add(column1);

            DataGridViewColumn column2 = new DataGridViewColumn();
            column2.CellTemplate = cell0;
            column2.HeaderText = @"Вес";
            column2.ReadOnly = false;
            column2.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            grid.Columns.Add(column2);

            DataGridViewColumn column4 = new DataGridViewColumn();
            column4.CellTemplate = cell0;
            column4.HeaderText = @"Узнаваемость";
            column4.ReadOnly = false;
            column4.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            grid.Columns.Add(column4);
            
            grid.Rows.Add(null, null);

            host.Child = grid;
        }

        DataGridViewRow GetRow(IPattern pattern,int num)
        {
            DataGridViewRow nRow = new DataGridViewRow();
            nRow.Cells.Add(new DataGridViewTextBoxCell());
            nRow.Cells[0].Value = num;
            nRow.Cells.Add(new DataGridViewTextBoxCell());
            nRow.Cells[1].Value = pattern.Type;
            nRow.Cells.Add(new DataGridViewTextBoxCell());
            nRow.Cells[2].Value = pattern.Weigth;
            nRow.Cells.Add(new DataGridViewTextBoxCell());
            nRow.Cells[3].Value = pattern.Expand;

            return nRow;
        }

        private void PaintGridPatternsToOpen()
        {
            if (_gridPatternsToOpen.InvokeRequired)
            {
                _gridPatternsToOpen.Invoke(new Action(PaintGridPatternsToOpen));
                return;
            }

            _gridPatternsToOpen.CellValueChanged -= _gridPatternsToOpen_CellValueChanged;

            _gridPatternsToOpen.Rows.Clear();

            for (int i = 0; i < _pattern.PatternsToOpen.Count; i++)
            {
                _gridPatternsToOpen.Rows.Add(GetRow(_pattern.PatternsToOpen[i],i+1));
            }

            _gridPatternsToOpen.CellValueChanged += _gridPatternsToOpen_CellValueChanged;
        }

        void _gridPatternsToOpen_CellValueChanged(object sender, DataGridViewCellEventArgs e)
        {
            try
            {
                for (int i = 0; i < _gridPatternsToOpen.Rows.Count; i++)
                {
                    _pattern.PatternsToOpen[i].Weigth = Convert.ToDecimal(_gridPatternsToOpen.Rows[i].Cells[2].Value.ToString().Replace(",",
                        CultureInfo.InvariantCulture.NumberFormat.NumberDecimalSeparator), CultureInfo.InvariantCulture);

                    _pattern.PatternsToOpen[i].Expand = Convert.ToDecimal(_gridPatternsToOpen.Rows[i].Cells[3].Value.ToString().Replace(",",
                        CultureInfo.InvariantCulture.NumberFormat.NumberDecimalSeparator), CultureInfo.InvariantCulture);
                }
                _pattern.Save();
            }
            catch (Exception)
            {
                PaintGridPatternsToOpen();
            }
        }

        void _gridPatternsToOpen_MouseClick(object sender, MouseEventArgs mouse)
        {
            if (mouse.Button == MouseButtons.Left &&
                _gridPatternsToOpen.SelectedCells.Count != 0)
            {
                _pattern.PaintOpenPattern(_gridPatternsToOpen.SelectedCells[0].RowIndex);
                return;
            }
            if (mouse.Button != MouseButtons.Right)
            {
                return;
            }
            try
            {
                MenuItem[] items = new MenuItem[2];

                items[0] = new MenuItem { Text = @"Добавить" };
                items[0].Click += GridPatternsToOpenAdd_Click;

                items[1] = new MenuItem { Text = @"Удалить" };
                items[1].Click += GridPatternsToOpenRemove_Click;

                ContextMenu menu = new ContextMenu(items);

                _gridPatternsToOpen.ContextMenu = menu;
                _gridPatternsToOpen.ContextMenu.Show(_gridPatternsToOpen, new System.Drawing.Point(mouse.X, mouse.Y));
            }
            catch (Exception error)
            {
                _pattern.SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        void GridPatternsToOpenAdd_Click(object sender, EventArgs e)
        {
            TabControlPrime.SelectedIndex = 3;
            _pattern.PlaceToUsePattern = UsePatternType.OpenPosition;
            ComboBoxPlaceToUsePattern.SelectedItem = _pattern.PlaceToUsePattern;
            _pattern.Save();
        }

        void GridPatternsToOpenRemove_Click(object sender, EventArgs e)
        {
            int patternNum = _gridPatternsToOpen.SelectedCells[0].RowIndex;

            _pattern.RemovePatternToInter(patternNum);
            PaintGridPatternsToOpen();
        }


//вкладка закрытие позиции ПЕРЕМЕННЫЕ

        void InitializeTabClosePosition()
        {
            CheckBoxStopOrderIsOn.IsChecked = _pattern.StopOrderIsOn;
            CheckBoxStopOrderIsOn.Click += CheckBoxStopOrderIsOn_Click;

            CheckBoxProfitOrderIsOn.IsChecked = _pattern.ProfitOrderIsOn;
            CheckBoxProfitOrderIsOn.Click += CheckBoxProfitOrderIsOn_Click;

            CheckBoxExitFromSomeCandlesIsOn.IsChecked = _pattern.ExitFromSomeCandlesIsOn;
            CheckBoxExitFromSomeCandlesIsOn.Click += CheckBoxExitFromSomeCandlesIsOn_Click;

            CheckBoxTrailingStopIsOn.IsChecked = _pattern.TrailingStopIsOn;
            CheckBoxTrailingStopIsOn.Click += CheckBoxTrailingStopIsOn_Click;

            TextBoxStopOrderValue.Text = _pattern.StopOrderValue.ToString();
            TextBoxStopOrderValue.TextChanged += TextBoxStopOrderValue_TextChanged;

            TextBoxProfitOrderValue.Text = _pattern.ProfitOrderValue.ToString();
            TextBoxProfitOrderValue.TextChanged += TextBoxProfitOrderValue_TextChanged;

            TextBoxExitFromSomeCandlesValue.Text = _pattern.ExitFromSomeCandlesValue.ToString();
            TextBoxExitFromSomeCandlesValue.TextChanged += TextBoxExitFromSomeCandlesValue_TextChanged;

            TextBoxTreilingStopValue.Text = _pattern.TreilingStopValue.ToString();
            TextBoxTreilingStopValue.TextChanged += TextBoxTreilingStopValue_TextChanged;

            TextBoxWeigthToExit.Text = _pattern.WeigthToExit.ToString(CultureInfo.InvariantCulture);
            TextBoxWeigthToExit.TextChanged += TextBoxWeigthToExit_TextChanged;
        }

        void TextBoxWeigthToExit_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (TextBoxWeigthToExit.Text == "" ||
                TextBoxWeigthToExit.Text == "0," ||
                TextBoxWeigthToExit.Text == "0.")
            {
                return;
            }
            try
            {
                _pattern.WeigthToExit = Convert.ToDecimal(TextBoxWeigthToExit.Text.Replace(",",
                    CultureInfo.InvariantCulture.NumberFormat.NumberDecimalSeparator), CultureInfo.InvariantCulture);
            }
            catch (Exception)
            {
                TextBoxWeigthToExit.Text = _pattern.WeigthToExit.ToString(CultureInfo.InvariantCulture);
            }
            _pattern.Save();
        }

        void TextBoxExitFromSomeCandlesValue_TextChanged(object sender, TextChangedEventArgs e)
        {
            try
            {
                _pattern.ExitFromSomeCandlesValue = Convert.ToInt32(TextBoxExitFromSomeCandlesValue.Text);
            }
            catch (Exception)
            {
                TextBoxExitFromSomeCandlesValue.Text = _pattern.ExitFromSomeCandlesValue.ToString();
            }
            _pattern.Save();
        }

        void TextBoxTreilingStopValue_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (TextBoxTreilingStopValue.Text == "" ||
                TextBoxTreilingStopValue.Text == "0," ||
                TextBoxTreilingStopValue.Text == "0.")
            {
                return;
            }
            try
            {
                _pattern.TreilingStopValue = Convert.ToDecimal(TextBoxTreilingStopValue.Text.Replace(",",
                    CultureInfo.InvariantCulture.NumberFormat.NumberDecimalSeparator), CultureInfo.InvariantCulture);
            }
            catch (Exception)
            {
                TextBoxTreilingStopValue.Text = _pattern.TreilingStopValue.ToString();
            }
            _pattern.Save();
        }

        void TextBoxProfitOrderValue_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (TextBoxProfitOrderValue.Text == "" ||
                TextBoxProfitOrderValue.Text == "0," ||
                TextBoxProfitOrderValue.Text == "0.")
            {
                return;
            }
            try
            {
                _pattern.ProfitOrderValue = Convert.ToDecimal(TextBoxProfitOrderValue.Text.Replace(",",
                    CultureInfo.InvariantCulture.NumberFormat.NumberDecimalSeparator), CultureInfo.InvariantCulture);
            }
            catch (Exception)
            {
                TextBoxProfitOrderValue.Text = _pattern.ProfitOrderValue.ToString();
            }
            _pattern.Save();
        }

        void TextBoxStopOrderValue_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (TextBoxStopOrderValue.Text == "" ||
                TextBoxStopOrderValue.Text == "0," ||
                TextBoxStopOrderValue.Text == "0.")
            {
                return;
            }
            try
            {
                _pattern.StopOrderValue = Convert.ToDecimal(TextBoxStopOrderValue.Text.Replace(",",
                    CultureInfo.InvariantCulture.NumberFormat.NumberDecimalSeparator), CultureInfo.InvariantCulture);
            }
            catch (Exception)
            {
                TextBoxStopOrderValue.Text = _pattern.StopOrderValue.ToString();
            }
            _pattern.Save();
        }

        void CheckBoxTrailingStopIsOn_Click(object sender, RoutedEventArgs e)
        {
            if (CheckBoxTrailingStopIsOn.IsChecked != null)
            {
                _pattern.TrailingStopIsOn = CheckBoxTrailingStopIsOn.IsChecked.Value;
            }
            _pattern.Save();
        }

        void CheckBoxExitFromSomeCandlesIsOn_Click(object sender, RoutedEventArgs e)
        {
            if (CheckBoxExitFromSomeCandlesIsOn.IsChecked != null)
            {
                _pattern.ExitFromSomeCandlesIsOn = CheckBoxExitFromSomeCandlesIsOn.IsChecked.Value;
            }
            _pattern.Save();
        }

        void CheckBoxProfitOrderIsOn_Click(object sender, RoutedEventArgs e)
        {
            if (CheckBoxProfitOrderIsOn.IsChecked != null)
            {
                _pattern.ProfitOrderIsOn = CheckBoxProfitOrderIsOn.IsChecked.Value;
            }
            _pattern.Save();
        }

        void CheckBoxStopOrderIsOn_Click(object sender, RoutedEventArgs e)
        {
            if (CheckBoxStopOrderIsOn.IsChecked != null)
            {
                _pattern.StopOrderIsOn = CheckBoxStopOrderIsOn.IsChecked.Value;
            }
            _pattern.Save();
        }


// вкладка закрытие позиции РАБОТА С ГРИДАМИ

        private DataGridView _gridPatternsToClose;

        private void PaintGridPatternsToClose()
        {
            if (_gridPatternsToOpen.InvokeRequired)
            {
                _gridPatternsToOpen.Invoke(new Action(PaintGridPatternsToClose));
                return;
            }

            _gridPatternsToClose.CellValueChanged -= _gridPatternsToClose_CellValueChanged;
            _gridPatternsToClose.Rows.Clear();

            for (int i = 0; i < _pattern.PatternsToClose.Count; i++)
            {
                _gridPatternsToClose.Rows.Add(GetRow(_pattern.PatternsToClose[i], i + 1));
            }
            _gridPatternsToClose.CellValueChanged += _gridPatternsToClose_CellValueChanged;
        }

        void _gridPatternsToClose_CellValueChanged(object sender, DataGridViewCellEventArgs e)
        {
            try
            {
                for (int i = 0; i < _gridPatternsToClose.Rows.Count; i++)
                {
                    _pattern.PatternsToClose[i].Weigth = Convert.ToDecimal(_gridPatternsToClose.Rows[i].Cells[2].Value.ToString().Replace(",",
                        CultureInfo.InvariantCulture.NumberFormat.NumberDecimalSeparator), CultureInfo.InvariantCulture);

                    _pattern.PatternsToClose[i].Expand = Convert.ToDecimal(_gridPatternsToClose.Rows[i].Cells[3].Value.ToString().Replace(",",
                        CultureInfo.InvariantCulture.NumberFormat.NumberDecimalSeparator), CultureInfo.InvariantCulture);
                }
                _pattern.Save();
            }
            catch (Exception)
            {
                PaintGridPatternsToClose();
            }
        }

        void _gridPatternsToClose_MouseClick(object sender, MouseEventArgs mouse)
        {
            if (mouse.Button == MouseButtons.Left)
            {
                if (_gridPatternsToClose.SelectedCells.Count == 0)
                {
                    return;
                }
                _pattern.PaintClosePattern(_gridPatternsToClose.SelectedCells[0].RowIndex);
                return;
            }

            try
            {
                MenuItem[] items = new MenuItem[2];

                items[0] = new MenuItem { Text = @"Добавить" };
                items[0].Click += GridPatternsToCloseAdd_Click;

                items[1] = new MenuItem { Text = @"Удалить" };
                items[1].Click += GridPatternsToCloseRemove_Click;

                ContextMenu menu = new ContextMenu(items);

                _gridPatternsToClose.ContextMenu = menu;
                _gridPatternsToClose.ContextMenu.Show(_gridPatternsToClose, new System.Drawing.Point(mouse.X, mouse.Y));
            }
            catch (Exception error)
            {
                _pattern.SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        void GridPatternsToCloseAdd_Click(object sender, EventArgs e)
        {
            TabControlPrime.SelectedIndex = 3;
            _pattern.PlaceToUsePattern = UsePatternType.ClosePosition;
            ComboBoxPlaceToUsePattern.SelectedItem = _pattern.PlaceToUsePattern;
            _pattern.Save();
        }

        void GridPatternsToCloseRemove_Click(object sender, EventArgs e)
        {
            int patternNum = _gridPatternsToClose.SelectedCells[0].RowIndex;

            _pattern.RemovePatternToExit(patternNum);
            PaintGridPatternsToClose();
        }

// вкладка ДАТА

        void InitializeTabDataSeries()
        {
            _dataServer = _pattern.DataServer;
            _pattern.DataServer.Log.StartPaint(HostLogDataServer);
            _dataServer.CandleSeriesChangeEvent += _server_CandleSeriesChangeEvent;

            CreateGridDataServer();
            PaintGridDataSeries();

            List<string> sets = _dataServer.Sets;

            // сеты

            for (int i = 0; sets != null && sets.Count != 0 && i < sets.Count; i++)
            {
                ComboBoxSets.Items.Add(sets[i]);
            }
            if (!string.IsNullOrEmpty(_dataServer.ActiveSet) &&
                _dataServer.ActiveSet.Split('_').Length == 2)
            {
                ComboBoxSets.SelectedItem = _dataServer.ActiveSet.Split('_')[1];
            }

            ComboBoxSets.SelectionChanged += ComboBoxSets_SelectionChanged;


            TextBoxDataPath.Text = _dataServer.PathToFolder;
            ComboBoxDataSourseType.Items.Add(TesterSourceDataType.Folder);
            ComboBoxDataSourseType.Items.Add(TesterSourceDataType.Set);
            ComboBoxDataSourseType.SelectedItem = _dataServer.SourceDataType;
            ComboBoxDataSourseType.SelectionChanged += ComboBoxDataSourseType_SelectionChanged;
        }

        void _server_CandleSeriesChangeEvent(List<MinerCandleSeries> series)
        {
            PaintGridDataSeries();
            InitializeComboBoxSecurityToInter();
        }

        /// <summary>
        /// источник данных изменился. Папка или Сет 
        /// </summary>
        void ComboBoxDataSourseType_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            TesterSourceDataType sourceDataType;
            Enum.TryParse(ComboBoxDataSourseType.SelectedItem.ToString(), out sourceDataType);
            _dataServer.SourceDataType = sourceDataType;
        }

        /// <summary>
        /// сет данных изменился
        /// </summary>
        void ComboBoxSets_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            _dataServer.SetNewSet(ComboBoxSets.SelectedItem.ToString());
            PaintGridDataSeries();
        }

        /// <summary>
        /// тестовый сервер
        /// </summary>
        private OsMinerServer _dataServer;

        /// <summary>
        /// таблица с инструментами
        /// </summary>
        private DataGridView _myGridView;

        /// <summary>
        /// создать таблицу с инструментами
        /// </summary>
        private void CreateGridDataServer()
        {
            _myGridView = DataGridFactory.GetDataGridView(DataGridViewSelectionMode.FullRowSelect, DataGridViewAutoSizeRowsMode.None);

            DataGridViewTextBoxCell cell0 = new DataGridViewTextBoxCell();
            cell0.Style = _myGridView.DefaultCellStyle;

            HostSecurities.Child = _myGridView;
            HostSecurities.Child.Show();

            DataGridViewColumn column2 = new DataGridViewColumn();
            column2.CellTemplate = cell0;
            column2.HeaderText = @"Файл";
            column2.ReadOnly = true;
            column2.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;

            _myGridView.Columns.Add(column2);

            DataGridViewColumn column0 = new DataGridViewColumn();
            column0.CellTemplate = cell0;
            column0.HeaderText = @"Бумага";
            column0.ReadOnly = true;
            column0.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;

            _myGridView.Columns.Add(column0);

            DataGridViewColumn column = new DataGridViewColumn();
            column.CellTemplate = cell0;
            column.HeaderText = @"Таймфрейм";
            column.ReadOnly = true;
            column.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;

            _myGridView.Columns.Add(column);

            DataGridViewColumn column1 = new DataGridViewColumn();
            column1.CellTemplate = cell0;
            column1.HeaderText = @"Шаг цены";
            column1.ReadOnly = true;
            column1.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;

            _myGridView.Columns.Add(column1);

            DataGridViewColumn column3 = new DataGridViewColumn();
            column3.CellTemplate = cell0;
            column3.HeaderText = @"Дата начала";
            column3.ReadOnly = true;
            column3.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;

            _myGridView.Columns.Add(column3);

            DataGridViewColumn column4 = new DataGridViewColumn();
            column4.CellTemplate = cell0;
            column4.HeaderText = @"Дата конца";
            column4.ReadOnly = true;
            column4.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;

            _myGridView.Columns.Add(column4);

            _myGridView.Rows.Add();
        }

        /// <summary>
        /// прорисовать таблицу с инструментами
        /// </summary>
        private void PaintGridDataSeries()
        {
            if (_myGridView.InvokeRequired)
            {
                _myGridView.Invoke(new Action(PaintGridDataSeries));
                return;
            }

            _myGridView.Rows.Clear();

            List<MinerCandleSeries> securities = _dataServer.SecuritiesTester;

            if (securities != null && securities.Count != 0)
            {
                for (int i = 0; i < securities.Count; i++)
                {
                    DataGridViewRow nRow = new DataGridViewRow();
                    nRow.Cells.Add(new DataGridViewTextBoxCell());
                    nRow.Cells[0].Value = securities[i].FileAdress;
                    nRow.Cells.Add(new DataGridViewTextBoxCell());
                    nRow.Cells[1].Value = securities[i].Security.Name;
                    nRow.Cells.Add(new DataGridViewTextBoxCell());
                    nRow.Cells[2].Value = SecurityTesterDataType.Candle;
                    nRow.Cells.Add(new DataGridViewTextBoxCell());
                    nRow.Cells[3].Value = securities[i].Security.PriceStep;
                    nRow.Cells.Add(new DataGridViewTextBoxCell());
                    nRow.Cells[4].Value = securities[i].TimeStart;
                    nRow.Cells.Add(new DataGridViewTextBoxCell());
                    nRow.Cells[5].Value = securities[i].TimeEnd;

                    _myGridView.Rows.Add(nRow);
                }
            }
        }

        private void ButtonSetDataFromPath_Click(object sender, RoutedEventArgs e)
        {
            _dataServer.ShowPathSenderDialog();
            TextBoxDataPath.Text = _dataServer.PathToFolder;
        }
    }
}
