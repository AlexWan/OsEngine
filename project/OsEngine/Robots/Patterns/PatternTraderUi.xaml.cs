/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms;
using System.Windows.Forms.Integration;
using System.Windows.Shapes;
using OsEngine.Charts.CandleChart;
using OsEngine.Entity;
using OsEngine.Language;
using OsEngine.OsMiner.Patterns;
using OsEngine.OsTrader.Panels;

namespace OsEngine.Robots.Patterns
{
    public partial class PatternTraderUi
    {
        public PatternTraderUi(PatternTrader bot)
        {
            InitializeComponent();

            _bot = bot;

            _gridPatternsToOpen = new DataGridView();
            _gridPatternsToOpen.MouseClick += _gridPatternsToOpen_MouseClick;
            _gridPatternsToOpen.CellValueChanged += _gridPatternsToOpen_CellValueChanged;

            _gridPatternsToClose = new DataGridView();
            _gridPatternsToClose.MouseClick += _gridPatternsToClose_MouseClick;
            _gridPatternsToClose.CellValueChanged += _gridPatternsToClose_CellValueChanged;

            CreateGridPatternsGrid(_gridPatternsToOpen, HostGridPatternsToOpen);
            CreateGridPatternsGrid(_gridPatternsToClose, HostGridPatternToClose);

            _chartSingleOpenPattern = new WinFormsChartPainter("OpenSinglePattern", bot.StartProgram);
            _chartSingleOpenPattern.IsPatternChart = true;
            _chartSingleClosePattern = new WinFormsChartPainter("CloseSinglePattern", bot.StartProgram);
            _chartSingleClosePattern.IsPatternChart = true;

            _chartSingleOpenPattern.StartPaintPrimeChart(null,HostSinglePatternToOpen, new Rectangle());
            _chartSingleClosePattern.StartPaintPrimeChart(null,HostSinglePatternToClose, new Rectangle());

            InitializePrimeSettings();
            InitializePattarnsToOpenTab();
            InitializeTabClosePosition();

            PaintGridPatternsToOpen();
            PaintGridPatternsToClose();
            PaintClosePattern(0);
            PaintOpenPattern(0);

            Title = OsLocalization.Trader.Label114;
            LabelRegime.Content = OsLocalization.Trader.Label115;
            LabelSet.Content = OsLocalization.Trader.Label116;
            LabelPatternGroup.Content = OsLocalization.Trader.Label117;
            LabelMaximumPositions.Content = OsLocalization.Trader.Label118;
            LabelVolume.Content = OsLocalization.Trader.Label30;
            TabItemOpeningPosition.Header = OsLocalization.Trader.Label119;
            LabelPatterns.Content = OsLocalization.Trader.Label120;
            LabelSide.Content = OsLocalization.Trader.Label121;
            LabelEntryWeight.Content = OsLocalization.Trader.Label122;
            LabelSlippage.Content = OsLocalization.Trader.Label92;
            TabItemClosePosition.Header = OsLocalization.Trader.Label100;
            CheckBoxStopOrderIsOn.Content = OsLocalization.Trader.Label123;
            CheckBoxProfitOrderIsOn.Content = OsLocalization.Trader.Label124;
            CheckBoxExitFromSomeCandlesIsOn.Content = OsLocalization.Trader.Label125;
            CheckBoxTrailingStopIsOn.Content = OsLocalization.Trader.Label126;

            LabelSlippage1.Content = OsLocalization.Trader.Label92;
            LabelSlippage2.Content = OsLocalization.Trader.Label92;
            LabelSlippage3.Content = OsLocalization.Trader.Label92;
            LabelSlippage4.Content = OsLocalization.Trader.Label92;
            LabelSlippageExitByPatterns.Content = OsLocalization.Trader.Label127;
            LabelWeightForExit.Content = OsLocalization.Trader.Label128;
        }

        private PatternTrader _bot;

// pattern selection and basic settings выбор паттерна и базовые настройки

        private void InitializePrimeSettings()
        {
            ComboBoxRegime.Items.Add(BotTradeRegime.Off);
            ComboBoxRegime.Items.Add(BotTradeRegime.On);
            ComboBoxRegime.SelectedItem = _bot.Regime;
            ComboBoxRegime.SelectionChanged += ComboBoxRegime_SelectionChanged;

            List<string> setsNames = _bot.GetListSetsName();

            for (int i = 0;setsNames != null && i < setsNames.Count; i++)
            {
                ComboBoxSets.Items.Add(setsNames[i]);
            }
           
            ComboBoxSets.SelectionChanged += ComboBoxSets_SelectionChanged;
            ComboBoxSets.SelectedItem = _bot.NameSetToTrade;


            ComboBoxPatternsGroups.SelectionChanged += ComboBoxPatternsGroups_SelectionChanged;
            TextBoxMaxPosition.Text = _bot.MaxPosition.ToString();
            TextBoxMaxPosition.TextChanged += TextBoxMaxPosition_TextChanged;

            TextBoxOpenVolume.Text = _bot.OpenVolume.ToString();
            TextBoxOpenVolume.TextChanged += TextBoxOpenVolume_TextChanged;
        }

        void TextBoxOpenVolume_TextChanged(object sender, TextChangedEventArgs e)
        {
            try
            {
                if (_bot.OpenVolume.ToString() == "" ||
                    _bot.OpenVolume.ToString().EndsWith(",") ||
                    _bot.OpenVolume.ToString().EndsWith("."))
                {
                    return;
                }

                _bot.OpenVolume = TextBoxOpenVolume.Text.ToDecimal();
            }
            catch (Exception)
            {
                TextBoxOpenVolume.Text = _bot.OpenVolume.ToString();
            }
            _bot.Save();
        }

        void TextBoxMaxPosition_TextChanged(object sender, TextChangedEventArgs e)
        {
            try
            {
                _bot.MaxPosition = Convert.ToInt32(TextBoxMaxPosition.Text.Replace(",",
                    CultureInfo.InvariantCulture.NumberFormat.NumberDecimalSeparator), CultureInfo.InvariantCulture);
            }
            catch (Exception)
            {
                TextBoxMaxPosition.Text = _bot.MaxPosition.ToString();
            }
            _bot.Save();
        }

        void ComboBoxPatternsGroups_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ComboBoxPatternsGroups.SelectedItem == null)
            {
                return;
            }
            _bot.NameGroupPatternsToTrade = ComboBoxPatternsGroups.SelectedItem.ToString();
            _bot.GetPatterns();
            PaintGridPatternsToOpen();
            PaintGridPatternsToClose();
            PaintClosePattern(0);
            PaintOpenPattern(0);

            _bot.Save();
        }

        void ComboBoxSets_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            _bot.NameSetToTrade = ComboBoxSets.SelectedItem.ToString();
            InitializeComboBoxPatternsGroups();
        }

        void ComboBoxRegime_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            Enum.TryParse(ComboBoxRegime.SelectedItem.ToString(), out _bot.Regime);
            _bot.Save();
        }

        void InitializeComboBoxPatternsGroups()
        {
            ComboBoxPatternsGroups.SelectionChanged -= ComboBoxPatternsGroups_SelectionChanged;
            ComboBoxPatternsGroups.Items.Clear();

            List<string> names = _bot.GetListPatternsNames(ComboBoxSets.SelectedItem.ToString());

            for (int i = 0; i < names.Count; i++)
            {
                ComboBoxPatternsGroups.Items.Add(names[i]);
            }

            ComboBoxPatternsGroups.SelectionChanged += ComboBoxPatternsGroups_SelectionChanged;
            ComboBoxPatternsGroups.SelectedItem = _bot.NameGroupPatternsToTrade;
        }

// work with the first tab работа с первой вкладкой

        private void InitializePattarnsToOpenTab()
        {
            ComboBoxSideInter.Items.Add(Side.Buy);
            ComboBoxSideInter.Items.Add(Side.Sell);
            ComboBoxSideInter.SelectedItem = _bot.SideInter;
            ComboBoxSideInter.SelectionChanged += ComboBoxSideInter_SelectionChanged;

            TextBoxWeigthToInter.Text = _bot.WeigthToInter.ToString();
            TextBoxWeigthToInter.TextChanged += TextBoxWeigthToInter_TextChanged;

            TextBoxInterToPatternSleepage.Text = _bot.InterToPatternSleepage.ToString();
            TextBoxInterToPatternSleepage.TextChanged += TextBoxInterToPatternSleepage_TextChanged;
        }

        void TextBoxInterToPatternSleepage_TextChanged(object sender, TextChangedEventArgs e)
        {
            try
            {
                _bot.InterToPatternSleepage = Convert.ToInt32(TextBoxInterToPatternSleepage.Text);
            }
            catch (Exception)
            {
                TextBoxInterToPatternSleepage.Text = _bot.InterToPatternSleepage.ToString();
            }
            _bot.Save();
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
                _bot.WeigthToInter = TextBoxWeigthToInter.Text.ToDecimal();
            }
            catch (Exception)
            {
                TextBoxWeigthToInter.Text = _bot.WeigthToInter.ToString();
            }
            _bot.Save();
        }

        void ComboBoxSideInter_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            Enum.TryParse(ComboBoxSideInter.SelectedItem.ToString(), out _bot.SideInter);
            _bot.Save();
        }

//work with the second tab  работа со второй вкладкой


        void InitializeTabClosePosition()
        {
            CheckBoxStopOrderIsOn.IsChecked = _bot.StopOrderIsOn;
            CheckBoxStopOrderIsOn.Click += CheckBoxStopOrderIsOn_Click;

            CheckBoxProfitOrderIsOn.IsChecked = _bot.ProfitOrderIsOn;
            CheckBoxProfitOrderIsOn.Click += CheckBoxProfitOrderIsOn_Click;

            CheckBoxExitFromSomeCandlesIsOn.IsChecked = _bot.ExitFromSomeCandlesIsOn;
            CheckBoxExitFromSomeCandlesIsOn.Click += CheckBoxExitFromSomeCandlesIsOn_Click;

            CheckBoxTrailingStopIsOn.IsChecked = _bot.TrailingStopIsOn;
            CheckBoxTrailingStopIsOn.Click += CheckBoxTrailingStopIsOn_Click;

            TextBoxStopOrderValue.Text = _bot.StopOrderValue.ToString();
            TextBoxStopOrderValue.TextChanged += TextBoxStopOrderValue_TextChanged;

            TextBoxProfitOrderValue.Text = _bot.ProfitOrderValue.ToString();
            TextBoxProfitOrderValue.TextChanged += TextBoxProfitOrderValue_TextChanged;

            TextBoxExitFromSomeCandlesValue.Text = _bot.ExitFromSomeCandlesValue.ToString();
            TextBoxExitFromSomeCandlesValue.TextChanged += TextBoxExitFromSomeCandlesValue_TextChanged;

            TextBoxTreilingStopValue.Text = _bot.TreilingStopValue.ToString();
            TextBoxTreilingStopValue.TextChanged += TextBoxTreilingStopValue_TextChanged;

            TextBoxWeigthToExit.Text = _bot.WeigthToExit.ToString(CultureInfo.InvariantCulture);
            TextBoxWeigthToExit.TextChanged += TextBoxWeigthToExit_TextChanged;

            TextBoxStopOrderValueSleepage.Text = _bot.StopOrderSleepage.ToString();
            TextBoxStopOrderValueSleepage.TextChanged += TextBoxStopOrderValueSleepage_TextChanged;

            TextBoxProfitOrderValueSleepage.Text = _bot.ProfitOrderSleepage.ToString();
            TextBoxProfitOrderValueSleepage.TextChanged += TextBoxProfitOrderValueSleepage_TextChanged;

            TextBoxExitFromSomeCandlesValueSleepage.Text = _bot.ExitFromSomeCandlesSleepage.ToString();
            TextBoxExitFromSomeCandlesValueSleepage.TextChanged += TextBoxExitFromSomeCandlesValueSleepage_TextChanged;

            TextBoxTreilingStopValueSleepage.Text = _bot.TreilingStopSleepage.ToString();
            TextBoxTreilingStopValueSleepage.TextChanged += TextBoxTreilingStopValueSleepage_TextChanged;

            TextBoxStopExitToPatternsSleepage.Text = _bot.StopOrderSleepage.ToString();
            TextBoxStopExitToPatternsSleepage.TextChanged += TextBoxStopExitToPatternsSleepage_TextChanged;
        }

        void TextBoxStopExitToPatternsSleepage_TextChanged(object sender, TextChangedEventArgs e)
        {
            try
            {
                _bot.StopOrderSleepage = Convert.ToInt32(TextBoxStopExitToPatternsSleepage.Text);
            }
            catch (Exception)
            {
                TextBoxStopExitToPatternsSleepage.Text = _bot.StopOrderSleepage.ToString();
            }
            _bot.Save();
        }

        void TextBoxTreilingStopValueSleepage_TextChanged(object sender, TextChangedEventArgs e)
        {
            try
            {
                _bot.TreilingStopSleepage = Convert.ToInt32(TextBoxTreilingStopValueSleepage.Text);
            }
            catch (Exception)
            {
                TextBoxTreilingStopValueSleepage.Text = _bot.TreilingStopSleepage.ToString();
            }
            _bot.Save();
        }

        void TextBoxExitFromSomeCandlesValueSleepage_TextChanged(object sender, TextChangedEventArgs e)
        {
            try
            {
                _bot.ExitFromSomeCandlesSleepage = Convert.ToInt32(TextBoxExitFromSomeCandlesValueSleepage.Text);
            }
            catch (Exception)
            {
                TextBoxExitFromSomeCandlesValueSleepage.Text = _bot.ExitFromSomeCandlesSleepage.ToString();
            }
            _bot.Save();
        }

        void TextBoxProfitOrderValueSleepage_TextChanged(object sender, TextChangedEventArgs e)
        {
            try
            {
                _bot.ProfitOrderSleepage = Convert.ToInt32(TextBoxProfitOrderValueSleepage.Text);
            }
            catch (Exception)
            {
                TextBoxProfitOrderValueSleepage.Text = _bot.ProfitOrderSleepage.ToString();
            }
            _bot.Save();
        }

        void TextBoxStopOrderValueSleepage_TextChanged(object sender, TextChangedEventArgs e)
        {
            try
            {
                _bot.StopOrderSleepage = Convert.ToInt32(TextBoxStopOrderValueSleepage.Text);
            }
            catch (Exception)
            {
                TextBoxStopOrderValueSleepage.Text = _bot.StopOrderSleepage.ToString();
            }
            _bot.Save();
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
                _bot.WeigthToExit = TextBoxWeigthToExit.Text.ToDecimal();
            }
            catch (Exception)
            {
                TextBoxWeigthToExit.Text = _bot.WeigthToExit.ToString(CultureInfo.InvariantCulture);
            }
            _bot.Save();
        }

        void TextBoxExitFromSomeCandlesValue_TextChanged(object sender, TextChangedEventArgs e)
        {
            try
            {
                _bot.ExitFromSomeCandlesValue = Convert.ToInt32(TextBoxExitFromSomeCandlesValue.Text);
            }
            catch (Exception)
            {
                TextBoxExitFromSomeCandlesValue.Text = _bot.ExitFromSomeCandlesValue.ToString();
            }
            _bot.Save();
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
                _bot.TreilingStopValue = TextBoxTreilingStopValue.Text.ToDecimal();
            }
            catch (Exception)
            {
                TextBoxTreilingStopValue.Text = _bot.TreilingStopValue.ToString();
            }
            _bot.Save();
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
                _bot.ProfitOrderValue = TextBoxProfitOrderValue.Text.ToDecimal();
            }
            catch (Exception)
            {
                TextBoxProfitOrderValue.Text = _bot.ProfitOrderValue.ToString();
            }
            _bot.Save();
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
                _bot.StopOrderValue = TextBoxStopOrderValue.Text.ToDecimal();
            }
            catch (Exception)
            {
                TextBoxStopOrderValue.Text = _bot.StopOrderValue.ToString();
            }
            _bot.Save();
        }

        void CheckBoxTrailingStopIsOn_Click(object sender, RoutedEventArgs e)
        {
            if (CheckBoxTrailingStopIsOn.IsChecked != null)
            {
                _bot.TrailingStopIsOn = CheckBoxTrailingStopIsOn.IsChecked.Value;
            }
            _bot.Save();
        }

        void CheckBoxExitFromSomeCandlesIsOn_Click(object sender, RoutedEventArgs e)
        {
            if (CheckBoxExitFromSomeCandlesIsOn.IsChecked != null)
            {
                _bot.ExitFromSomeCandlesIsOn = CheckBoxExitFromSomeCandlesIsOn.IsChecked.Value;
            }
            _bot.Save();
        }

        void CheckBoxProfitOrderIsOn_Click(object sender, RoutedEventArgs e)
        {
            if (CheckBoxProfitOrderIsOn.IsChecked != null)
            {
                _bot.ProfitOrderIsOn = CheckBoxProfitOrderIsOn.IsChecked.Value;
            }
            _bot.Save();
        }

        void CheckBoxStopOrderIsOn_Click(object sender, RoutedEventArgs e)
        {
            if (CheckBoxStopOrderIsOn.IsChecked != null)
            {
                _bot.StopOrderIsOn = CheckBoxStopOrderIsOn.IsChecked.Value;
            }
            _bot.Save();
        }

//WORK WITH GRID РАБОТА С ГРИДАМИ

        private DataGridView _gridPatternsToOpen;

        /// <summary>
        /// chart for drawing a single entry pattern
        /// чарт для отрисовки одиночного паттерна на вход
        /// </summary>
        private WinFormsChartPainter _chartSingleOpenPattern;

        /// <summary>
        /// chart for drawing a single exit pattern
        /// чарт для отрисовки одиночного паттерна на выход
        /// </summary>
        private WinFormsChartPainter _chartSingleClosePattern;

        void CreateGridPatternsGrid(DataGridView grid, WindowsFormsHost host)
        {

            grid.AllowUserToOrderColumns = true;
            grid.AllowUserToResizeRows = true;
            grid.AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.AllCells;
            grid.AllowUserToDeleteRows = false;
            grid.AllowUserToAddRows = false;
            grid.RowHeadersVisible = false;
            grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            grid.MultiSelect = false;

            DataGridViewCellStyle style = new DataGridViewCellStyle();
            style.Alignment = DataGridViewContentAlignment.TopLeft;
            style.WrapMode = DataGridViewTriState.True;
            grid.DefaultCellStyle = style;

            DataGridViewTextBoxCell cell0 = new DataGridViewTextBoxCell();
            cell0.Style = style;

            DataGridViewComboBoxCell cellComboBox = new DataGridViewComboBoxCell();
            cellComboBox.Style = style;

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

        DataGridViewRow GetRow(IPattern pattern, int num)
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
            if (_bot.PatternsToOpen == null)
            {
                return;
            }
            if (_gridPatternsToOpen.InvokeRequired)
            {
                _gridPatternsToOpen.Invoke(new Action(PaintGridPatternsToOpen));
                return;
            }

            _gridPatternsToOpen.CellValueChanged -= _gridPatternsToOpen_CellValueChanged;

            _gridPatternsToOpen.Rows.Clear();

            for (int i = 0; i < _bot.PatternsToOpen.Count; i++)
            {
                _gridPatternsToOpen.Rows.Add(GetRow(_bot.PatternsToOpen[i], i + 1));
            }

            _gridPatternsToOpen.CellValueChanged += _gridPatternsToOpen_CellValueChanged;
        }

        void _gridPatternsToOpen_CellValueChanged(object sender, DataGridViewCellEventArgs e)
        {
            try
            {
                for (int i = 0; i < _gridPatternsToOpen.Rows.Count; i++)
                {
                    _bot.PatternsToOpen[i].Weigth = _gridPatternsToOpen.Rows[i].Cells[2].Value.ToString().ToDecimal();

                    _bot.PatternsToOpen[i].Expand = _gridPatternsToOpen.Rows[i].Cells[3].Value.ToString().ToDecimal();
                }
            }
            catch (Exception)
            {
                PaintGridPatternsToOpen();
            }
        }

        void _gridPatternsToOpen_MouseClick(object sender, System.Windows.Forms.MouseEventArgs mouse)
        {
            if (mouse.Button == MouseButtons.Left &&
                _gridPatternsToOpen.SelectedCells.Count != 0)
            {
                PaintOpenPattern(_gridPatternsToOpen.SelectedCells[0].RowIndex);
                return;
            }
        }

        private DataGridView _gridPatternsToClose;

        private void PaintGridPatternsToClose()
        {
            if (_bot.PatternsToClose == null)
            {
                return;
            }
            if (_gridPatternsToOpen.InvokeRequired)
            {
                _gridPatternsToOpen.Invoke(new Action(PaintGridPatternsToClose));
                return;
            }

            _gridPatternsToClose.CellValueChanged -= _gridPatternsToClose_CellValueChanged;
            _gridPatternsToClose.Rows.Clear();

            for (int i = 0; i < _bot.PatternsToClose.Count; i++)
            {
                _gridPatternsToClose.Rows.Add(GetRow(_bot.PatternsToClose[i], i + 1));
            }
            _gridPatternsToClose.CellValueChanged += _gridPatternsToClose_CellValueChanged;
        }

        void _gridPatternsToClose_CellValueChanged(object sender, DataGridViewCellEventArgs e)
        {
            try
            {
                for (int i = 0; i < _gridPatternsToClose.Rows.Count; i++)
                {
                    _bot.PatternsToClose[i].Weigth = _gridPatternsToClose.Rows[i].Cells[2].Value.ToString().ToDecimal();
                    _bot.PatternsToClose[i].Expand = _gridPatternsToClose.Rows[i].Cells[3].Value.ToString().ToDecimal();
                }
            }
            catch (Exception)
            {
                PaintGridPatternsToClose();
            }
        }

        void _gridPatternsToClose_MouseClick(object sender, System.Windows.Forms.MouseEventArgs mouse)
        {
            if (mouse.Button == MouseButtons.Left)
            {
                if (_gridPatternsToClose.SelectedCells.Count == 0)
                {
                    return;
                }
                PaintClosePattern(_gridPatternsToClose.SelectedCells[0].RowIndex);
            }

        }

        /// <summary>
        /// draw a pattern on the opening on his individual charts
        /// прорисовать паттерн на открытие на его индивидуальном чарте
        /// </summary>
        public void PaintOpenPattern(int index)
        {
            if (_bot.PatternsToOpen == null || _bot.PatternsToOpen.Count <= index)
            {
                _chartSingleOpenPattern.ClearSeries();
                return;
            }

            IPattern pattern = _bot.PatternsToOpen[index];

            PaintSinglePattern(pattern, _chartSingleOpenPattern);
        }

        /// <summary>
        /// draw a closing pattern on its individual chart
        /// прорисовать паттерн на закрытие на его индивидуальном чарте
        /// </summary>
        public void PaintClosePattern(int index)
        {
            if (_bot.PatternsToClose == null || _bot.PatternsToClose.Count <= index)
            {
                _chartSingleClosePattern.ClearSeries();
                return;
            }
            IPattern pattern = _bot.PatternsToClose[index];

            PaintSinglePattern(pattern, _chartSingleClosePattern);
        }

        /// <summary>
        /// draw a pattern on his individual chart
        /// прорисовать паттерн на его индивидуальном чарте
        /// </summary>
        private void PaintSinglePattern(IPattern pattern, WinFormsChartPainter chart)
        {
            chart.ClearDataPointsAndSizeValue();
            chart.ClearSeries();

            if (pattern.Type == PatternType.Candle)
            {
                chart.PaintSingleCandlePattern(((PatternCandle)pattern).GetInCandle());
            }
            if (pattern.Type == PatternType.Volume)
            {
                chart.PaintSingleVolumePattern(((PatternVolume)pattern).GetInCandle());
            }
            if (pattern.Type == PatternType.Indicators)
            {
                PatternIndicators pat = (PatternIndicators)pattern;

                for (int i = 0; pat.Indicators != null && i < pat.Indicators.Count; i++)
                {
                    if (chart.IndicatorIsCreate(pat.Indicators[i].Name + "0") == false)
                    {
                        chart.CreateSeries(pat.Indicators[i].NameArea, pat.Indicators[i].TypeIndicator, pat.Indicators[i].NameSeries + "0");
                    }

                    chart.ProcessIndicator(pat.Indicators[i]);
                }
            }
        }
    }
}
