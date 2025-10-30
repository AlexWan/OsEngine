/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System.Windows;
using System.Windows.Forms;
using OsEngine.Language;
using OsEngine.Entity;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Threading;
using OsEngine.Market;

namespace OsEngine.OsTrader.Panels.Tab
{
    public partial class BotTabIndexUi
    {

        public BotTabIndexUi(BotTabIndex spread)
        {
            InitializeComponent();
            OsEngine.Layout.StickyBorders.Listen(this);
            OsEngine.Layout.StartupLocation.Start_MouseInCentre(this);
            CreateTable();
            _spread = spread;
            ReloadSecurityTable();
            TextboxUserFormula.Text = _spread.UserFormula;
            TextboxUserFormula.TextChanged += TextboxUserFormula_TextChanged;
            TextboxUserFormulaSecondTab.Text = _spread.UserFormula;

            IndexFormulaBuilder autoFormulaBuilder = spread.AutoFormulaBuilder;

            ComboBoxRegime.Items.Add(IndexAutoFormulaBuilderRegime.Off.ToString());
            ComboBoxRegime.Items.Add(IndexAutoFormulaBuilderRegime.OncePerWeek.ToString());
            ComboBoxRegime.Items.Add(IndexAutoFormulaBuilderRegime.OncePerDay.ToString());
            ComboBoxRegime.Items.Add(IndexAutoFormulaBuilderRegime.OncePerHour.ToString());
            ComboBoxRegime.SelectedItem = autoFormulaBuilder.Regime.ToString();
            ComboBoxRegime.SelectionChanged += ComboBoxRegime_SelectionChanged;

            ComboBoxDayOfWeekToRebuildIndex.Items.Add(DayOfWeek.Monday.ToString());
            ComboBoxDayOfWeekToRebuildIndex.Items.Add(DayOfWeek.Tuesday.ToString());
            ComboBoxDayOfWeekToRebuildIndex.Items.Add(DayOfWeek.Wednesday.ToString());
            ComboBoxDayOfWeekToRebuildIndex.Items.Add(DayOfWeek.Thursday.ToString());
            ComboBoxDayOfWeekToRebuildIndex.Items.Add(DayOfWeek.Friday.ToString());
            ComboBoxDayOfWeekToRebuildIndex.Items.Add(DayOfWeek.Saturday.ToString());
            ComboBoxDayOfWeekToRebuildIndex.Items.Add(DayOfWeek.Sunday.ToString());
            ComboBoxDayOfWeekToRebuildIndex.SelectedItem = autoFormulaBuilder.DayOfWeekToRebuildIndex.ToString();
            ComboBoxDayOfWeekToRebuildIndex.SelectionChanged += ComboBoxDayOfWeekToRebuildIndex_SelectionChanged;

            for (int i = 0; i < 24; i++)
            {
                ComboBoxHourInDayToRebuildIndex.Items.Add(i.ToString());
            }
            ComboBoxHourInDayToRebuildIndex.SelectedItem = autoFormulaBuilder.HourInDayToRebuildIndex.ToString();
            ComboBoxHourInDayToRebuildIndex.SelectionChanged += ComboBoxHourInDayToRebuildIndex_SelectionChanged;

            CheckBoxWriteLogMessageOnRebuild.IsChecked = autoFormulaBuilder.WriteLogMessageOnRebuild;
            CheckBoxWriteLogMessageOnRebuild.Click += CheckBoxWriteLogMessageOnRebuild_Click;

            ComboBoxIndexSortType.Items.Add(SecuritySortType.FirstInArray.ToString());
            ComboBoxIndexSortType.Items.Add(SecuritySortType.VolumeWeighted.ToString());
            ComboBoxIndexSortType.Items.Add(SecuritySortType.MaxVolatilityWeighted.ToString());
            ComboBoxIndexSortType.Items.Add(SecuritySortType.MinVolatilityWeighted.ToString());
            ComboBoxIndexSortType.SelectedItem = autoFormulaBuilder.IndexSortType.ToString();
            ComboBoxIndexSortType.SelectionChanged += ComboBoxIndexSortType_SelectionChanged;

            for (int i = 1; i < 301; i++)
            {
                ComboBoxIndexSecCount.Items.Add(i.ToString());
            }
            ComboBoxIndexSecCount.SelectedItem = autoFormulaBuilder.IndexSecCount.ToString();
            ComboBoxIndexSecCount.SelectionChanged += ComboBoxIndexSecCount_SelectionChanged;

            ComboBoxIndexMultType.Items.Add(IndexMultType.PriceWeighted.ToString());
            ComboBoxIndexMultType.Items.Add(IndexMultType.VolumeWeighted.ToString());
            ComboBoxIndexMultType.Items.Add(IndexMultType.EqualWeighted.ToString());
            ComboBoxIndexMultType.Items.Add(IndexMultType.Cointegration.ToString());
            ComboBoxIndexMultType.SelectedItem = autoFormulaBuilder.IndexMultType.ToString();
            ComboBoxIndexMultType.SelectionChanged += ComboBoxIndexMultType_SelectionChanged;

            if (autoFormulaBuilder.IndexMultType == IndexMultType.Cointegration
                 && ComboBoxIndexSecCount.IsEnabled != false)
            {
                ComboBoxIndexSecCount.SelectedItem = "2";
                ComboBoxIndexSecCount.IsEnabled = false;
            }

            for (int i = 1; i < 301; i++)
            {
                ComboBoxDaysLookBackInBuilding.Items.Add(i.ToString());
            }
            ComboBoxDaysLookBackInBuilding.SelectedItem = autoFormulaBuilder.DaysLookBackInBuilding.ToString();
            ComboBoxDaysLookBackInBuilding.SelectionChanged += ComboBoxDaysLookBackInBuilding_SelectionChanged;

            ButtonRebuildFormulaNow.Click += ButtonRebuildFormulaNow_Click;

            TextBoxDepth.Text = spread.CalculationDepth.ToString();
            TextBoxDepth.TextChanged += TextBoxDepth_TextChanged;

            CheckBoxPercentNormalization.IsChecked = spread.PercentNormalization;

            CheckDayComboBox();
            CheckHourComboBox();

            Title = OsLocalization.Trader.Label81;
            LabelDepth.Content = OsLocalization.Trader.Label411;
            ButtonAccept.Content = OsLocalization.Trader.Label17;
            ButtonClearAllSecurities.Content = OsLocalization.Trader.Label369;
            TabControlItem1.Header = OsLocalization.Trader.Label374;
            TabControlItem2.Header = OsLocalization.Trader.Label375;

            LabelTimeSettingsToRebuildFormula.Content = OsLocalization.Trader.Label376;
            LabelRegime.Content = OsLocalization.Trader.Label115;
            LabelDayOfWeekToRebuildIndex.Content = OsLocalization.Trader.Label378;
            LabelHourInDayToRebuildIndex.Content = OsLocalization.Trader.Label379;
            CheckBoxWriteLogMessageOnRebuild.Content = OsLocalization.Trader.Label380;

            LabelTypeSettingsToRebuildFormula.Content = OsLocalization.Trader.Label377;
            LabelIndexSortType.Content = OsLocalization.Trader.Label381;
            LabelIndexSecCount.Content = OsLocalization.Trader.Label382;
            LabelIndexMultType.Content = OsLocalization.Trader.Label383;
            LabelDaysLookBackInBuilding.Content = OsLocalization.Trader.Label384;

            CheckBoxPercentNormalization.Content = OsLocalization.Trader.Label431;
            CheckBoxPercentNormalization.Click += CheckBoxPercentNormalization_Click;
            ButtonRebuildFormulaNow.Content = OsLocalization.Trader.Label385;

            this.Closed += BotTabIndexUi_Closed;
            this.Activate();
            this.Focus();

            Thread worker = new Thread(PricePainterThreadWorker);
            worker.Name = "BotTabIndexPricePainter";
            worker.Start();
        }

        private void BotTabIndexUi_Closed(object sender, System.EventArgs e)
        {
            try
            {
                _windowIsClosed = true;

                if (_uiNewSecurities != null)
                {
                    _uiNewSecurities.Close();
                }

                ComboBoxRegime.SelectionChanged -= ComboBoxRegime_SelectionChanged;
                ComboBoxDayOfWeekToRebuildIndex.SelectionChanged -= ComboBoxDayOfWeekToRebuildIndex_SelectionChanged;
                ComboBoxHourInDayToRebuildIndex.SelectionChanged -= ComboBoxHourInDayToRebuildIndex_SelectionChanged;
                CheckBoxWriteLogMessageOnRebuild.Click -= CheckBoxWriteLogMessageOnRebuild_Click;
                ComboBoxIndexSortType.SelectionChanged -= ComboBoxIndexSortType_SelectionChanged;
                ComboBoxIndexSecCount.SelectionChanged -= ComboBoxIndexSecCount_SelectionChanged;
                ComboBoxIndexMultType.SelectionChanged -= ComboBoxIndexMultType_SelectionChanged;
                ComboBoxDaysLookBackInBuilding.SelectionChanged -= ComboBoxDaysLookBackInBuilding_SelectionChanged;
                ButtonRebuildFormulaNow.Click -= ButtonRebuildFormulaNow_Click;

                this.Closed -= BotTabIndexUi_Closed;

                if (_sourcesGrid != null)
                {
                    _sourcesGrid.CellClick -= _sourcesGrid_CellClick;
                    _sourcesGrid.CellDoubleClick -= Grid1CellValueChangeClick;
                    _sourcesGrid.DataError -= _sourcesGrid_DataError;
                    DataGridFactory.ClearLinks(_sourcesGrid);
                    _sourcesGrid.Rows.Clear();
                    _sourcesGrid = null;
                }
                
                _spread = null;
            }
            catch (Exception ex)
            {
                CustomMessageBoxUi ui = new CustomMessageBoxUi(ex.Message);
                ui.ShowDialog();
            }
        }

        private bool _windowIsClosed;

        private void TextBoxDepth_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            try
            {
                if (TextBoxDepth.Text == "")
                {
                    return;
                }

                int value = Convert.ToInt32(TextBoxDepth.Text);

                if (value < 5)
                {
                    return;
                }

                if (_spread.CalculationDepth != value)
                {
                    _spread.CalculationDepth = value;
                    _spread.Save();

                    if (_spread.Candles != null &&
                        _spread.Candles.Count > 0)
                    {
                        _spread.RebuildHard();
                    }
                }

            }
            catch
            {
                TextBoxDepth.Text = _spread.CalculationDepth.ToString();
            }
        }

        private void CheckBoxPercentNormalization_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_spread.PercentNormalization != CheckBoxPercentNormalization.IsChecked.Value)
                {
                    _spread.PercentNormalization = CheckBoxPercentNormalization.IsChecked.Value;
                    _spread.Save();

                    if (_spread.Candles != null &&
                        _spread.Candles.Count > 0)
                    {
                        _spread.RebuildHard();
                    }
                }
            }
            catch
            {
                // ignore
            }
        }

        private void CheckDayComboBox()
        {
            if (_spread.AutoFormulaBuilder.Regime == IndexAutoFormulaBuilderRegime.OncePerWeek)
            {
                ComboBoxDayOfWeekToRebuildIndex.IsEnabled = true;
            }
            else
            {
                ComboBoxDayOfWeekToRebuildIndex.IsEnabled = false;
            }
        }

        private void CheckHourComboBox()
        {
            if (_spread.AutoFormulaBuilder.Regime == IndexAutoFormulaBuilderRegime.OncePerDay
                || _spread.AutoFormulaBuilder.Regime == IndexAutoFormulaBuilderRegime.OncePerWeek)
            {
                ComboBoxHourInDayToRebuildIndex.IsEnabled = true;
            }
            else
            {
                ComboBoxHourInDayToRebuildIndex.IsEnabled = false;
            }
        }

        private void ComboBoxDaysLookBackInBuilding_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            _spread.AutoFormulaBuilder.DaysLookBackInBuilding
              = Convert.ToInt32(ComboBoxDaysLookBackInBuilding.SelectedItem.ToString());
        }

        private void ComboBoxIndexMultType_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            IndexMultType multType;

            if (Enum.TryParse(ComboBoxIndexMultType.SelectedItem.ToString(), out multType))
            {
                _spread.AutoFormulaBuilder.IndexMultType = multType;

                if (multType == IndexMultType.Cointegration
                    && ComboBoxIndexSecCount.IsEnabled != false)
                {
                    ComboBoxIndexSecCount.SelectedItem = "2";
                    ComboBoxIndexSecCount.IsEnabled = false;
                    _spread.AutoFormulaBuilder.IndexSecCount = 2;
                }

                if (multType != IndexMultType.Cointegration &&
                    ComboBoxIndexSecCount.IsEnabled != true)
                {
                    ComboBoxIndexSecCount.IsEnabled = true;
                }
            }
        }

        private void ComboBoxIndexSecCount_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            _spread.AutoFormulaBuilder.IndexSecCount
               = Convert.ToInt32(ComboBoxIndexSecCount.SelectedItem.ToString());
        }

        private void CheckBoxWriteLogMessageOnRebuild_Click(object sender, RoutedEventArgs e)
        {
            _spread.AutoFormulaBuilder.WriteLogMessageOnRebuild = CheckBoxWriteLogMessageOnRebuild.IsChecked.Value;
        }

        private void ComboBoxIndexSortType_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            SecuritySortType sortType;

            if (Enum.TryParse(ComboBoxIndexSortType.SelectedItem.ToString(), out sortType))
            {
                _spread.AutoFormulaBuilder.IndexSortType = sortType;
            }
        }

        private void ComboBoxHourInDayToRebuildIndex_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            _spread.AutoFormulaBuilder.HourInDayToRebuildIndex
                = Convert.ToInt32(ComboBoxHourInDayToRebuildIndex.SelectedItem.ToString());
        }

        private void ComboBoxDayOfWeekToRebuildIndex_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            DayOfWeek curDay;

            if (Enum.TryParse(ComboBoxDayOfWeekToRebuildIndex.SelectedItem.ToString(), out curDay))
            {
                _spread.AutoFormulaBuilder.DayOfWeekToRebuildIndex = curDay;
            }
        }

        private void ComboBoxRegime_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            IndexAutoFormulaBuilderRegime curRegime;

            if (Enum.TryParse(ComboBoxRegime.SelectedItem.ToString(), out curRegime))
            {
                _spread.AutoFormulaBuilder.Regime = curRegime;
            }

            CheckDayComboBox();
            CheckHourComboBox();
        }

        private void TextboxUserFormula_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            TextboxUserFormulaSecondTab.Text = TextboxUserFormula.Text;
        }

        public bool IndexOrSourcesChanged = false;

        private BotTabIndex _spread;

        private DataGridView _sourcesGrid;

        private void CreateTable()
        {
            _sourcesGrid = DataGridFactory.GetDataGridView(
                DataGridViewSelectionMode.FullRowSelect, DataGridViewAutoSizeRowsMode.AllCells);

            _sourcesGrid.ScrollBars = ScrollBars.Vertical;

            _sourcesGrid.CellDoubleClick += Grid1CellValueChangeClick;
            _sourcesGrid.CellClick += _sourcesGrid_CellClick;
            _sourcesGrid.DataError += _sourcesGrid_DataError;

            DataGridViewTextBoxCell fcell0 = new DataGridViewTextBoxCell();


            DataGridViewColumn fcolumn0 = new DataGridViewColumn();
            fcolumn0.CellTemplate = fcell0;
            fcolumn0.HeaderText = OsLocalization.Trader.Label82; // Index number
            fcolumn0.ReadOnly = true;
            fcolumn0.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            _sourcesGrid.Columns.Add(fcolumn0);

            DataGridViewColumn fcolumn1 = new DataGridViewColumn();
            fcolumn1.CellTemplate = fcell0;
            fcolumn1.HeaderText = OsLocalization.Trader.Label83; // Security code
            fcolumn1.ReadOnly = true;
            fcolumn1.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            _sourcesGrid.Columns.Add(fcolumn1);

            DataGridViewColumn fcolumn2 = new DataGridViewColumn();
            fcolumn2.CellTemplate = fcell0;
            fcolumn2.HeaderText = OsLocalization.Trader.Label178; // Server
            fcolumn2.ReadOnly = true;
            fcolumn2.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            _sourcesGrid.Columns.Add(fcolumn2);

            DataGridViewColumn fcolumn3 = new DataGridViewColumn();
            fcolumn3.CellTemplate = fcell0;
            fcolumn3.HeaderText = OsLocalization.Trader.Label179; // Time frame
            fcolumn3.ReadOnly = true;
            fcolumn3.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            _sourcesGrid.Columns.Add(fcolumn3);

            DataGridViewColumn fcolumn4 = new DataGridViewColumn();
            fcolumn4.CellTemplate = fcell0;
            fcolumn4.HeaderText = OsLocalization.Trader.Label398; // Last price
            fcolumn4.ReadOnly = true;
            fcolumn4.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            _sourcesGrid.Columns.Add(fcolumn4);

            DataGridViewColumn fcolumn5 = new DataGridViewColumn();
            fcolumn5.CellTemplate = fcell0;
            fcolumn5.HeaderText = "";                             // Set security
            fcolumn5.ReadOnly = true;
            fcolumn5.Width = 150;
            _sourcesGrid.Columns.Add(fcolumn5);

            DataGridViewColumn fcolumn6 = new DataGridViewColumn();
            fcolumn6.CellTemplate = fcell0;
            fcolumn6.HeaderText = "";                             // Delete
            fcolumn6.ReadOnly = true;
            fcolumn6.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            _sourcesGrid.Columns.Add(fcolumn6);

            HostSecurity1.Child = _sourcesGrid;
        }

        private void _sourcesGrid_DataError(object sender, DataGridViewDataErrorEventArgs e)
        {
            CustomMessageBoxUi ui = new CustomMessageBoxUi(e.ToString());
            ui.ShowDialog();
        }

        private void Grid1CellValueChangeClick(object sender, DataGridViewCellEventArgs e)
        {
            try
            {
                int index = _sourcesGrid.CurrentCell.RowIndex;
                _spread.ShowIndexConnectorIndexDialog(index);
                ReloadSecurityTable();
                IndexOrSourcesChanged = true;
            }
            catch (Exception ex)
            {
                CustomMessageBoxUi ui = new CustomMessageBoxUi(ex.Message);
                ui.ShowDialog();
            }
        }

        private void _sourcesGrid_CellClick(object sender, DataGridViewCellEventArgs e)
        {
            try
            {
                if (e.ColumnIndex == 5)
                { // connection dialog
                    int index = _sourcesGrid.CurrentCell.RowIndex;
                    _spread.ShowIndexConnectorIndexDialog(index);
                    ReloadSecurityTable();
                    IndexOrSourcesChanged = true;
                }
                else if (e.ColumnIndex == 6)
                { // delete
                    if (string.IsNullOrEmpty(_spread.UserFormula) == false)
                    {
                        AcceptDialogUi acceptDialog = new AcceptDialogUi(OsLocalization.Trader.Label399);

                        acceptDialog.ShowDialog();

                        if (acceptDialog.UserAcceptAction)
                        {
                            _spread.DeleteSecurityTab(e.RowIndex);
                            _spread.SecuritiesInIndex.Clear();
                            ReloadSecurityTable();
                        }
                    }
                    else
                    {
                        _spread.DeleteSecurityTab(e.RowIndex);
                        ReloadSecurityTable();
                    }
                }
            }
            catch (Exception ex)
            {
                CustomMessageBoxUi ui = new CustomMessageBoxUi(ex.Message);
                ui.ShowDialog();
            }
        }

        private void ReloadSecurityTable()
        {
            try
            {
                if (_spread.Tabs == null)
                {
                    return;
                }

                int showRow = _sourcesGrid.FirstDisplayedScrollingRowIndex;

                int selectedSec = -1;

                if (_sourcesGrid.SelectedCells != null &&
                    _sourcesGrid.SelectedCells.Count > 0)
                {
                    selectedSec = _sourcesGrid.SelectedCells[0].RowIndex;
                }

                _sourcesGrid.Rows.Clear();

                string formula = _spread.UserFormula;

                List<Security> secInIndex = _spread.SecuritiesInIndex;

                for (int i = 0; _spread.Tabs != null && i < _spread.Tabs.Count; i++)
                {
                    DataGridViewRow row = new DataGridViewRow();

                    row.Cells.Add((new DataGridViewTextBoxCell()));
                    row.Cells[0].Value = "A" + i;

                    row.Cells.Add(new DataGridViewTextBoxCell());
                    if (string.IsNullOrWhiteSpace(_spread.Tabs[i].SecurityName))
                    {
                        row.Cells[1].Value = OsLocalization.Trader.Label84;

                    }
                    else
                    {
                        row.Cells[1].Value = _spread.Tabs[i].SecurityName;
                    }

                    row.Cells.Add((new DataGridViewTextBoxCell()));
                    row.Cells[2].Value = _spread.Tabs[i].ServerFullName;

                    row.Cells.Add((new DataGridViewTextBoxCell()));
                    row.Cells[3].Value = _spread.Tabs[i].TimeFrame.ToString();

                    row.Cells.Add((new DataGridViewTextBoxCell())); // LastPrice
                                                                    // row.Cells[4].Value = _spread.Tabs[i].TimeFrame.ToString();

                    DataGridViewButtonCell button = new DataGridViewButtonCell();
                    button.Value = OsLocalization.Trader.Label235;
                    row.Cells.Add(button);

                    DataGridViewButtonCell buttonDelete = new DataGridViewButtonCell();
                    buttonDelete.Value = OsLocalization.Trader.Label39;
                    row.Cells.Add(buttonDelete);

                    if (secInIndex != null &&
                        secInIndex.Count > 0)
                    {
                        bool thisSecInIndex = false;

                        for (int i2 = 0; i2 < secInIndex.Count; i2++)
                        {
                            if (secInIndex[i2].Name == _spread.Tabs[i].SecurityName)
                            {
                                thisSecInIndex = true;
                                break;
                            }
                        }

                        if (thisSecInIndex)
                        {
                            for (int i2 = 0; i2 < row.Cells.Count; i2++)
                            {
                                row.Cells[i2].Style.BackColor = Color.DarkGreen;
                                row.Cells[i2].Style.SelectionBackColor = Color.Black;
                                row.Cells[i2].Style.ForeColor = Color.White;
                            }
                        }
                    }

                    for (int i2 = 0; i2 < row.Cells.Count; i2++)
                    {
                        row.Cells[i2].Style.Alignment = DataGridViewContentAlignment.MiddleCenter;
                    }

                    _sourcesGrid.Rows.Add(row);
                }


                if (showRow > 0 &&
                    showRow < _sourcesGrid.Rows.Count)
                {
                    _sourcesGrid.FirstDisplayedScrollingRowIndex = showRow;
                }

                if (selectedSec > 0
                    && selectedSec < _sourcesGrid.Rows.Count)
                {
                    _sourcesGrid.Rows[selectedSec].Selected = true;
                }
            }
            catch(Exception ex) 
            {
                ServerMaster.SendNewLogMessage(ex.ToString(),Logging.LogMessageType.Error);
            }
        }

        private void ButtonAddSecurity_Click(object sender, RoutedEventArgs e)
        {
            if (_spread.ShowNewSecurityDialog())
            {
                _uiNewSecurities = _spread.UiSecuritiesSelection;
                _uiNewSecurities.Closed += UiSecuritiesSelection_Closed;
            }
        }

        private Market.Connectors.MassSourcesCreateUi _uiNewSecurities;

        private void UiSecuritiesSelection_Closed(object sender, EventArgs e)
        {
            if (_windowIsClosed == false)
            {
                ReloadSecurityTable();
                IndexOrSourcesChanged = true;
            }

            _uiNewSecurities.Closed -= UiSecuritiesSelection_Closed;
            _uiNewSecurities = null;
        }

        private void RepeatButtonDeleteSecurity_Click(object sender, RoutedEventArgs e)
        {
            if (_sourcesGrid.CurrentCell == null)
            {
                return;
            }

            _spread.DeleteSecurityTab(_sourcesGrid.CurrentCell.RowIndex);
            ReloadSecurityTable();
            IndexOrSourcesChanged = true;
        }

        private void ButtonAccept_Click(object sender, RoutedEventArgs e)
        {
            if (_spread.UserFormula != TextboxUserFormula.Text
                || IndexOrSourcesChanged == true)
            {
                _spread.UserFormula = TextboxUserFormula.Text;
            }

            Close();
        }

        private void ButtonClearAllSecurities_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_sourcesGrid.Rows.Count == 0)
                {
                    return;
                }

                AcceptDialogUi ui = new AcceptDialogUi(OsLocalization.Trader.Label370);
                ui.ShowDialog();

                if (ui.UserAcceptAction == false)
                {
                    return;
                }

                while (_spread.Tabs.Count > 0)
                {
                    _spread.DeleteSecurityTab(0);
                }

                _spread.SecuritiesInIndex.Clear();

                ReloadSecurityTable();
                IndexOrSourcesChanged = true;
            }
            catch (Exception ex)
            {
                CustomMessageBoxUi ui = new CustomMessageBoxUi(ex.Message);
                ui.ShowDialog();
            }
        }

        private void ButtonRebuildFormulaNow_Click(object sender, RoutedEventArgs e)
        {
            _spread.RebuildHard();
            TextboxUserFormula.Text = _spread.UserFormula;

            ReloadSecurityTable();
        }

        private void PricePainterThreadWorker()
        {
            while (true)
            {
                Thread.Sleep(3000);

                if (_windowIsClosed)
                {
                    return;
                }

                if (MainWindow.ProccesIsWorked == false)
                {
                    return;
                }

                PaintPrices();
            }
        }

        private void PaintPrices()
        {
            try
            {
                if (_sourcesGrid == null)
                {
                    return;
                }

                if (_sourcesGrid.InvokeRequired)
                {
                    _sourcesGrid.Invoke(new Action(PaintPrices));
                    return;
                }

                for (int i = 0; i < _spread.Tabs.Count; i++)
                {
                    DataGridViewRow row = _sourcesGrid.Rows[i];

                    if (row == null)
                    {
                        continue;
                    }

                    DataGridViewCell priceCell = row.Cells[4];

                    if (priceCell == null)
                    {
                        continue;
                    }

                    List<Candle> candles = _spread.Tabs[i].Candles(false);

                    string priceInStr = "";

                    if (candles != null
                        && candles.Count > 0)
                    {
                        priceInStr =
                            candles[candles.Count - 1].Close.ToString() + " " + candles[candles.Count - 1].TimeStart.TimeOfDay;
                    }

                    if (priceCell.Value == null
                        || priceCell.Value.ToString() != priceInStr)
                    {
                        priceCell.Value = priceInStr;
                    }
                }
            }
            catch
            {
                // ignore
            }
        }

    }
}