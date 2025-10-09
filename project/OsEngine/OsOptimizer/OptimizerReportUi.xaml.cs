/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using OsEngine.Entity;
using OsEngine.Language;
using OsEngine.OsTrader.Panels;
using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Forms;
using OsEngine.Logging;
using MessageBox = System.Windows.MessageBox;
using OsEngine.OsOptimizer.OptEntity;
using System.IO;
using System.Text;
using System.Windows.Forms.DataVisualization.Charting;
using System.Drawing;
using System.Linq;

namespace OsEngine.OsOptimizer
{
    /// <summary>
    /// Логика взаимодействия для OptimizerReportUi.xaml
    /// </summary>
    public partial class OptimizerReportUi : Window
    {
        public OptimizerReportUi(OptimizerMaster master)
        {
            InitializeComponent();
            OsEngine.Layout.StickyBorders.Listen(this);
            _master = master;

            _resultsCharting = new OptimizerReportCharting(
            HostStepsOfOptimizationTable,
            HostRobustness,
            ComboBoxSortResultsType,
            LabelRobustnessMetricValue,
            ComboBoxSortResultsBotNumPercent);

            _resultsCharting.ActivateTotalProfitChart(HostTotalProfit, ComboBoxTotalProfit);

            _resultsCharting.ActivateAverageProfitChart(HostAverageProfit);
            _resultsCharting.ActivateProfitFactorChart(HostProfitFactor);

            _resultsCharting.LogMessageEvent += _master.SendLogMessage;

            CreateTableFazes();
            CreateTableResults();
            CreateChartSeriesResults();

            LabelSortBy.Content = OsLocalization.Optimizer.Label39;
            LabelOptimSeries.Content = OsLocalization.Optimizer.Label30;
            LabelTableResults.Content = OsLocalization.Optimizer.Label31;
            TabControlResultsSeries.Header = OsLocalization.Optimizer.Label37;
            TabControlResultsOutOfSampleResults.Header = OsLocalization.Optimizer.Label38;
            ButtonSaveInFile.Content = OsLocalization.Optimizer.Label45;
            ButtonLoadFromFile.Content = OsLocalization.Optimizer.Label46;
            LabelRobustnessMetric.Content = OsLocalization.Optimizer.Label53;
            LabelTotalProfit.Content = OsLocalization.Optimizer.Label54;
            LabelAverageProfitFactor.Content = OsLocalization.Optimizer.Label55;
            LabelAverageProfitPersent.Content = OsLocalization.Optimizer.Label56;
            LabelSeriesResultChart.Content = OsLocalization.Optimizer.Label67;

            Title += "   " + master.StrategyName;

            this.Activate();
            this.Focus();
            this.Closed += OptimizerReportUi_Closed;
        }

        private void OptimizerReportUi_Closed(object sender, EventArgs e)
        {
            try
            {
                if (WindowsFormsHostFazeNumOnTubResult != null)
                {
                    WindowsFormsHostFazeNumOnTubResult.Child = null;
                }

                if (WindowsFormsHostResults != null)
                {
                    WindowsFormsHostResults.Child = null;
                }

                if (_gridFazesEnd != null)
                {
                    _gridFazesEnd.CellClick -= _gridFazesEnd_CellClick;
                    _gridFazesEnd.DataError -= _gridFazesEnd_DataError;
                    DataGridFactory.ClearLinks(_gridFazesEnd);
                    _gridFazesEnd = null;
                }

                if (_gridResults != null)
                {
                    _gridResults.DataError -= _gridFazesEnd_DataError;
                    DataGridFactory.ClearLinks(_gridResults);
                    _gridResults = null;
                }
            }
            catch
            {
                // ignore
            }
        }

        public void Paint(List<OptimizerFazeReport> reports)
        {
            if (reports == null)
            {
                return;
            }

            _reports = new List<OptimizerFazeReport>();

            for (int i = 0; i < reports.Count; i++)
            {
                _reports.Add(reports[i]);
            }

            RepaintResults();
        }

        OptimizerMaster _master;

        private List<OptimizerFazeReport> _reports;

        private OptimizerReportCharting _resultsCharting;

        private void RepaintResults()
        {
            try
            {
                for (int i = 0; i < _reports.Count; i++)
                {
                    OptimizerFazeReport.SortResults(_reports[i].Reports, _sortBotsType);
                }

                PaintTableFazes();
                PaintTableResults();
                PaintSeriesResultsChart();

                _resultsCharting.ReLoad(_reports);
            }
            catch (Exception error)
            {
                _master.SendLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        private void ShowBotChartDialog(DataGridViewCellMouseEventArgs e)
        {
            OptimizerFazeReport fazeReport;

            if (_gridFazesEnd.CurrentCell == null ||
              _gridFazesEnd.CurrentCell.RowIndex == 0)
            {
                fazeReport = _reports[0];
            }
            else
            {
                if (_gridFazesEnd.CurrentCell.RowIndex > _reports.Count)
                {
                    return;
                }

                fazeReport = _reports[_gridFazesEnd.CurrentCell.RowIndex];
            }

            if (e.RowIndex >= fazeReport.Reports.Count)
            {
                return;
            }

            BotPanel bot = _master.TestBot(fazeReport, fazeReport.Reports[e.RowIndex]);

            if (bot == null)
            {
                return;
            }

            BotPanelChartUi ui = bot.ShowChartDialog();

            ui.Closed += (sender, e) =>
            {
                try
                {
                    bot.Delete();
                }
                catch (Exception error)
                {
                    _master.SendLogMessage(error.ToString(), LogMessageType.Error);
                }
            };
        }

        private void ShowParametersDialog(DataGridViewCellMouseEventArgs e)
        {
            OptimizerFazeReport fazeReport;

            if (_gridFazesEnd.CurrentCell == null ||
              _gridFazesEnd.CurrentCell.RowIndex == 0)
            {
                fazeReport = _reports[0];
            }
            else
            {
                if (_gridFazesEnd.CurrentCell.RowIndex > _reports.Count)
                {
                    return;
                }

                fazeReport = _reports[_gridFazesEnd.CurrentCell.RowIndex];
            }

            if (e.RowIndex >= fazeReport.Reports.Count)
            {
                return;
            }

            OptimizerBotParametersSimpleUi ui = new OptimizerBotParametersSimpleUi(fazeReport.Reports[e.RowIndex], fazeReport, _master.StrategyName);
            ui.Show();
        }

        private void ButtonSaveInFile_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                SaveFileDialog myDialog = new SaveFileDialog();

                string saveFileName = _master.StrategyName;

                IIStrategyParameter regime = _master._optimizerExecutor._parameters.Find(p => p.Name == "Regime");

                if (regime != null)
                {

                    saveFileName += "_" + ((StrategyParameterString)regime).ValueString;
                }
                saveFileName = saveFileName.Replace(".txt", "");

                myDialog.FileName = saveFileName;

                myDialog.Filter = "*.txt|";
                myDialog.ShowDialog();

                if (string.IsNullOrEmpty(myDialog.FileName))
                {
                    System.Windows.Forms.MessageBox.Show(OsLocalization.Journal.Message1);
                    return;
                }

                string fileName = myDialog.FileName;
                if (fileName.Split('.').Length == 1)
                {
                    fileName = fileName + ".txt";
                }

                StringBuilder saveStr = new StringBuilder();

                for (int i = 0; i < _reports.Count; i++)
                {
                    saveStr.Append(_reports[i].GetSaveString() + "\r\n");
                }

                StreamWriter writer = new StreamWriter(fileName);
                writer.Write(saveStr);
                writer.Close();
            }
            catch (Exception error)
            {
                MessageBox.Show(error.ToString());
            }
        }

        private void ButtonLoadFromFile_Click(object sender, RoutedEventArgs e)
        {
            Title = "Optimizer Report";

            try
            {
                OpenFileDialog myDialog = new OpenFileDialog();
                myDialog.Filter = "*.txt|";
                myDialog.ShowDialog();

                if (string.IsNullOrEmpty(myDialog.FileName))
                {
                    System.Windows.Forms.MessageBox.Show(OsLocalization.Journal.Message2);
                    return;
                }

                if (_reports == null)
                {
                    _reports = new List<OptimizerFazeReport>();
                }
                else
                {
                    _reports.Clear();
                }

                using (StreamReader reader = new StreamReader(myDialog.FileName))
                {
                    while (reader.EndOfStream == false)
                    {
                        string str = reader.ReadLine();

                        if (string.IsNullOrEmpty(str))
                        {
                            continue;
                        }

                        OptimizerFazeReport newReport = new OptimizerFazeReport();
                        newReport.LoadFromString(str);
                        _reports.Add(newReport);
                    }
                }

                RepaintResults();
            }
            catch (Exception error)
            {
                MessageBox.Show(error.ToString());
            }
        }

        #region Phase table for switching after testing

        private DataGridView _gridFazesEnd;

        private void CreateTableFazes()
        {
            _gridFazesEnd = DataGridFactory.GetDataGridView(DataGridViewSelectionMode.FullRowSelect,
                DataGridViewAutoSizeRowsMode.AllCells, true);

            _gridFazesEnd.ScrollBars = ScrollBars.Vertical;
            DataGridViewTextBoxCell cell0 = new DataGridViewTextBoxCell();
            cell0.Style = _gridFazesEnd.DefaultCellStyle;

            DataGridViewColumn column0 = new DataGridViewColumn();
            column0.CellTemplate = cell0;
            column0.HeaderText = OsLocalization.Optimizer.Message23;
            column0.ReadOnly = true;
            column0.Width = 100;

            _gridFazesEnd.Columns.Add(column0);

            DataGridViewColumn column1 = new DataGridViewColumn();
            column1.CellTemplate = cell0;
            column1.HeaderText = OsLocalization.Optimizer.Message24;
            column1.ReadOnly = true;
            column1.Width = 150;

            _gridFazesEnd.Columns.Add(column1);

            DataGridViewColumn column = new DataGridViewColumn();
            column.CellTemplate = cell0;
            column.HeaderText = OsLocalization.Optimizer.Message25;
            column.ReadOnly = true;
            column.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            _gridFazesEnd.Columns.Add(column);

            DataGridViewColumn column2 = new DataGridViewColumn();
            column2.CellTemplate = cell0;
            column2.HeaderText = OsLocalization.Optimizer.Message26;
            column2.ReadOnly = true;
            column2.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            _gridFazesEnd.Columns.Add(column2);

            DataGridViewColumn column3 = new DataGridViewColumn();
            column3.CellTemplate = cell0;
            column3.HeaderText = OsLocalization.Optimizer.Message27;
            column3.ReadOnly = true;
            column3.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            _gridFazesEnd.Columns.Add(column3);

            _gridFazesEnd.Rows.Add(null, null);

            WindowsFormsHostFazeNumOnTubResult.Child = _gridFazesEnd;

            _gridFazesEnd.CellClick += _gridFazesEnd_CellClick;
            _gridFazesEnd.DataError += _gridFazesEnd_DataError;
        }

        private void _gridFazesEnd_DataError(object sender, DataGridViewDataErrorEventArgs e)
        {
            _master.SendLogMessage(e.ToString(), LogMessageType.Error);
        }

        private void PaintTableFazes()
        {
            if (_gridFazesEnd.InvokeRequired)
            {
                _gridFazesEnd.Invoke(new Action(PaintTableFazes));
                return;
            }

            _gridFazesEnd.Rows.Clear();

            List<OptimizerFaze> fazes = new List<OptimizerFaze>();

            for (int i = 0; i < _reports.Count; i++)
            {
                fazes.Add(_reports[i].Faze);
            }

            if (fazes == null ||
                fazes.Count == 0)
            {
                return;
            }

            for (int i = 0; i < fazes.Count; i++)
            {
                DataGridViewRow row = new DataGridViewRow();

                row.Cells.Add(new DataGridViewTextBoxCell());
                row.Cells[0].Value = i + 1;

                DataGridViewTextBoxCell cell = new DataGridViewTextBoxCell();
                cell.Value = fazes[i].TypeFaze;
                row.Cells.Add(cell);

                DataGridViewTextBoxCell cell2 = new DataGridViewTextBoxCell();
                cell2.Value = fazes[i].TimeStart.ToString(OsLocalization.ShortDateFormatString);
                row.Cells.Add(cell2);

                DataGridViewTextBoxCell cell3 = new DataGridViewTextBoxCell();
                cell3.Value = fazes[i].TimeEnd.ToString(OsLocalization.ShortDateFormatString);
                row.Cells.Add(cell3);

                DataGridViewTextBoxCell cell4 = new DataGridViewTextBoxCell();
                cell4.Value = fazes[i].Days;
                row.Cells.Add(cell4);

                _gridFazesEnd.Rows.Add(row);
            }
        }

        private void _gridFazesEnd_CellClick(object sender, DataGridViewCellEventArgs e)
        {
            PaintTableResults();
            PaintSeriesResultsChart();
        }

        #endregion

        #region Optimization results table

        private DataGridView _gridResults;

        private void CreateTableResults()
        {
            _gridResults = DataGridFactory.GetDataGridView(DataGridViewSelectionMode.ColumnHeaderSelect,
                DataGridViewAutoSizeRowsMode.None, true);

            _gridResults.ScrollBars = ScrollBars.Vertical;

            DataGridViewTextBoxCell cell0 = new DataGridViewTextBoxCell();
            cell0.Style = _gridResults.DefaultCellStyle;

            DataGridViewColumn column0 = new DataGridViewColumn();
            column0.CellTemplate = cell0;
            column0.HeaderText = "Bot Name";
            column0.ReadOnly = true;
            column0.Width = 150;

            _gridResults.Columns.Add(column0);

            DataGridViewColumn column1 = new DataGridViewColumn();
            column1.CellTemplate = cell0;
            column1.HeaderText = "Parameters";
            column1.ReadOnly = false;
            column1.Width = 150;
            _gridResults.Columns.Add(column1);

            DataGridViewColumn column21 = new DataGridViewColumn();
            column21.CellTemplate = cell0;
            column21.HeaderText = "Pos Count";
            column21.ReadOnly = false;
            column21.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            _gridResults.Columns.Add(column21);

            DataGridViewColumn column2 = new DataGridViewColumn();
            column2.CellTemplate = cell0;
            column2.HeaderText = "Total Profit";
            column2.ReadOnly = false;
            column2.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            _gridResults.Columns.Add(column2);

            DataGridViewColumn column3 = new DataGridViewColumn();
            column3.CellTemplate = cell0;
            column3.HeaderText = "Max Drow Dawn";
            column3.ReadOnly = false;
            column3.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            _gridResults.Columns.Add(column3);

            DataGridViewColumn column4 = new DataGridViewColumn();
            column4.CellTemplate = cell0;
            column4.HeaderText = "Average Profit";
            column4.ReadOnly = false;
            column4.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            _gridResults.Columns.Add(column4);

            DataGridViewColumn column5 = new DataGridViewColumn();
            column5.CellTemplate = cell0;
            column5.HeaderText = "Average Profit %";
            column5.ReadOnly = false;
            column5.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            _gridResults.Columns.Add(column5);

            DataGridViewColumn column6 = new DataGridViewColumn();
            column6.CellTemplate = cell0;
            column6.HeaderText = "Profit Factor";
            column6.ReadOnly = false;
            column6.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            _gridResults.Columns.Add(column6);

            DataGridViewColumn column7 = new DataGridViewColumn();
            column7.CellTemplate = cell0;
            column7.HeaderText = "Pay Off Ratio";
            column7.ReadOnly = false;
            column7.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            _gridResults.Columns.Add(column7);

            DataGridViewColumn column8 = new DataGridViewColumn();
            column8.CellTemplate = cell0;
            column8.HeaderText = "Recovery";
            column8.ReadOnly = false;
            column8.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            _gridResults.Columns.Add(column8);

            DataGridViewColumn column9 = new DataGridViewColumn();
            column9.CellTemplate = cell0;
            column9.HeaderText = "Sharpe Ratio";
            column9.ReadOnly = false;
            column9.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            _gridResults.Columns.Add(column9);

            DataGridViewButtonColumn column11 = new DataGridViewButtonColumn();
            column11.CellTemplate = new DataGridViewButtonCell();
            //column11.HeaderText = OsLocalization.Optimizer.Message40;
            column11.ReadOnly = true;
            column11.AutoSizeMode = DataGridViewAutoSizeColumnMode.AllCells;
            _gridResults.Columns.Add(column11);

            DataGridViewButtonColumn column12 = new DataGridViewButtonColumn();
            column12.CellTemplate = new DataGridViewButtonCell();
            // column12.HeaderText = OsLocalization.Optimizer.Message42;
            column12.ReadOnly = true;
            column12.AutoSizeMode = DataGridViewAutoSizeColumnMode.AllCells;
            _gridResults.Columns.Add(column12);

            _gridResults.Rows.Add(null, null);

            WindowsFormsHostResults.Child = _gridResults;
            _gridResults.DataError += _gridFazesEnd_DataError;
        }

        private void UpdateHeaders()
        {

            _gridResults.Columns[0].HeaderText = "Bot Name \n \n \n";

            if (_sortBotsType == SortBotsType.BotName)
            {
                _gridResults.Columns[0].HeaderText += " vvv";
            }

            _gridResults.Columns[2].HeaderText = "Pos Count";

            Color cellColor = Color.Black;

            for (int i = 0; i < _gridResults.Columns.Count; i++)
            {
                _gridResults.Columns[i].HeaderCell.Style.BackColor = _gridResults.Columns[i].HeaderCell.Style.SelectionBackColor;
            }

            if (_sortBotsType == SortBotsType.PositionCount)
            {
                _gridResults.Columns[2].HeaderText += " vvv";
                _gridResults.Columns[2].HeaderCell.Style.BackColor = cellColor;
            }

            _gridResults.Columns[3].HeaderText = "Total Profit";

            if (_sortBotsType == SortBotsType.TotalProfit)
            {
                _gridResults.Columns[3].HeaderText += " vvv";
                _gridResults.Columns[3].HeaderCell.Style.BackColor = cellColor;
            }

            _gridResults.Columns[4].HeaderText = "Max Drow Dawn";
            if (_sortBotsType == SortBotsType.MaxDrawDawn)
            {
                _gridResults.Columns[4].HeaderText += " vvv";
                _gridResults.Columns[4].HeaderCell.Style.BackColor = cellColor;
            }

            _gridResults.Columns[5].HeaderText = "Average Profit";
            if (_sortBotsType == SortBotsType.AverageProfit)
            {
                _gridResults.Columns[5].HeaderText += " vvv";
                _gridResults.Columns[5].HeaderCell.Style.BackColor = cellColor;
            }

            _gridResults.Columns[6].HeaderText = "Average Profit %";
            if (_sortBotsType == SortBotsType.AverageProfitPercent)
            {
                _gridResults.Columns[6].HeaderText += " vvv";
                _gridResults.Columns[6].HeaderCell.Style.BackColor = cellColor;
            }

            _gridResults.Columns[7].HeaderText = "Profit Factor";
            if (_sortBotsType == SortBotsType.ProfitFactor)
            {
                _gridResults.Columns[7].HeaderText += " vvv";
                _gridResults.Columns[7].HeaderCell.Style.BackColor = cellColor;
            }

            _gridResults.Columns[8].HeaderText = "Pay Off Ratio";
            if (_sortBotsType == SortBotsType.PayOffRatio)
            {
                _gridResults.Columns[8].HeaderText += " vvv";
                _gridResults.Columns[8].HeaderCell.Style.BackColor = cellColor;
            }

            _gridResults.Columns[9].HeaderText = "Recovery";
            if (_sortBotsType == SortBotsType.Recovery)
            {
                _gridResults.Columns[9].HeaderText += " vvv";
                _gridResults.Columns[9].HeaderCell.Style.BackColor = cellColor;
            }

            _gridResults.Columns[10].HeaderText = "Sharpe Ratio";
            if (_sortBotsType == SortBotsType.SharpRatio)
            {
                _gridResults.Columns[10].HeaderText += " vvv";
                _gridResults.Columns[10].HeaderCell.Style.BackColor = cellColor;
            }

            _gridResults.ColumnHeadersHeight = 50;
        }

        private void PaintTableResults()
        {
            try
            {
                if (_gridResults == null)
                {
                    return;
                }

                if (_gridResults.InvokeRequired)
                {
                    _gridResults.Invoke(new Action(PaintTableResults));
                    return;
                }
                _gridResults.SelectionChanged -= _gridResults_SelectionChanged;
                _gridResults.CellMouseClick -= _gridResults_CellMouseClick;

                UpdateHeaders();

                _gridResults.Rows.Clear();

                if (_reports == null)
                {
                    return;
                }

                if (_gridFazesEnd.CurrentCell == null)
                {
                    return;
                }

                int num = 0;
                num = _gridFazesEnd.CurrentCell.RowIndex;

                if (num >= _reports.Count)
                {
                    return;
                }

                OptimizerFazeReport fazeReport = _reports[num];

                if (fazeReport == null)
                {
                    return;
                }

                List<DataGridViewRow> rows = new List<DataGridViewRow>();

                for (int i = 0; i < fazeReport.Reports.Count; i++)
                {
                    OptimizerReport report = fazeReport.Reports[i];
                    if (report == null ||
                        report.TabsReports.Count == 0)
                    {
                        continue;
                    }

                    DataGridViewRow row = new DataGridViewRow();
                    row.Height = 30;
                    row.Cells.Add(new DataGridViewTextBoxCell());

                    //if (report.TabsReports.Count == 1)
                    //{
                    row.Cells[0].Value = report.BotName;
                    //}
                    //else
                    //{
                    //    row.Cells[0].Value = "Сводные";
                    //}

                    DataGridViewTextBoxCell cell2 = new DataGridViewTextBoxCell();
                    cell2.Value = report.GetParametersToDataTable();
                    row.Cells.Add(cell2);

                    DataGridViewTextBoxCell cell3 = new DataGridViewTextBoxCell();
                    cell3.Value = report.PositionsCount;
                    row.Cells.Add(cell3);

                    DataGridViewTextBoxCell cell4 = new DataGridViewTextBoxCell();
                    cell4.Value = Math.Round(report.TotalProfit, 5).ToStringWithNoEndZero() + " (" + report.TotalProfitPercent.ToStringWithNoEndZero() + "%)";
                    row.Cells.Add(cell4);

                    DataGridViewTextBoxCell cell5 = new DataGridViewTextBoxCell();
                    cell5.Value = Math.Round(report.MaxDrawDawn, 5).ToStringWithNoEndZero();
                    row.Cells.Add(cell5);

                    DataGridViewTextBoxCell cell6 = new DataGridViewTextBoxCell();
                    cell6.Value = Math.Round(report.AverageProfit, 5).ToStringWithNoEndZero();
                    row.Cells.Add(cell6);

                    DataGridViewTextBoxCell cell7 = new DataGridViewTextBoxCell();
                    cell7.Value = Math.Round(report.AverageProfitPercentOneContract, 5).ToStringWithNoEndZero();
                    row.Cells.Add(cell7);

                    DataGridViewTextBoxCell cell8 = new DataGridViewTextBoxCell();
                    cell8.Value = Math.Round(report.ProfitFactor, 5).ToStringWithNoEndZero();
                    row.Cells.Add(cell8);

                    DataGridViewTextBoxCell cell9 = new DataGridViewTextBoxCell();
                    cell9.Value = Math.Round(report.PayOffRatio, 5).ToStringWithNoEndZero();
                    row.Cells.Add(cell9);

                    DataGridViewTextBoxCell cell10 = new DataGridViewTextBoxCell();
                    cell10.Value = Math.Round(report.Recovery, 5).ToStringWithNoEndZero();
                    row.Cells.Add(cell10);

                    DataGridViewTextBoxCell cell11 = new DataGridViewTextBoxCell();
                    cell11.Value = Math.Round(report.SharpRatio, 5).ToStringWithNoEndZero();
                    row.Cells.Add(cell11);

                    DataGridViewButtonCell cell12 = new DataGridViewButtonCell();
                    cell12.Value = OsLocalization.Optimizer.Message40;
                    row.Cells.Add(cell12);

                    DataGridViewButtonCell cell13 = new DataGridViewButtonCell();
                    cell13.Value = OsLocalization.Optimizer.Message42;
                    row.Cells.Add(cell13);

                    rows.Add(row);
                }

                WindowsFormsHostResults.Child = null;

                if (rows.Count > 0)
                {
                    _gridResults.Rows.AddRange(rows.ToArray());
                }

                WindowsFormsHostResults.Child = _gridResults;

                _gridResults.SelectionChanged += _gridResults_SelectionChanged;
                _gridResults.CellMouseClick += _gridResults_CellMouseClick;
            }
            catch (Exception ex)
            {
                _master.SendLogMessage(ex.ToString(), LogMessageType.Error);
            }
        }

        private void _gridResults_CellMouseClick(object sender, DataGridViewCellMouseEventArgs e)
        {
            if (e.RowIndex < 0)
            {
                return;
            }

            if (e.ColumnIndex == 11)
            {
                ShowBotChartDialog(e);
            }

            if (e.ColumnIndex == 12)
            {
                ShowParametersDialog(e);
            }
        }

        private void _gridResults_SelectionChanged(object sender, EventArgs e)
        {
            if (_gridResults.SelectedCells.Count == 0)
            {
                return;
            }
            int columnSelect = _gridResults.SelectedCells[0].ColumnIndex;

            int rowSelect = _gridResults.SelectedCells[0].RowIndex;

            if (rowSelect != 0)
            {
                return;
            }

            SortBotsType currentSelection = SortBotsType.BotName;

            if (columnSelect == 0)
            {
                return;
            }
            else if (columnSelect == 2)
            {
                currentSelection = SortBotsType.PositionCount;
            }
            else if (columnSelect == 3)
            {
                currentSelection = SortBotsType.TotalProfit;
            }
            else if (columnSelect == 4)
            {
                currentSelection = SortBotsType.MaxDrawDawn;
            }
            else if (columnSelect == 5)
            {
                currentSelection = SortBotsType.AverageProfit;
            }
            else if (columnSelect == 6)
            {
                currentSelection = SortBotsType.AverageProfitPercent;
            }
            else if (columnSelect == 7)
            {
                currentSelection = SortBotsType.ProfitFactor;
            }
            else if (columnSelect == 8)
            {
                currentSelection = SortBotsType.PayOffRatio;
            }
            else if (columnSelect == 9)
            {
                currentSelection = SortBotsType.Recovery;
            }
            else if (columnSelect == 10)
            {
                currentSelection = SortBotsType.SharpRatio;
            }
            else
            {
                return;
            }

            if (currentSelection == _sortBotsType)
            {
                return;
            }

            _sortBotsType = currentSelection;

            try
            {
                for (int i = 0; i < _reports.Count; i++)
                {
                    OptimizerFazeReport.SortResults(_reports[i].Reports, _sortBotsType);
                }

                PaintTableResults();
                PaintSeriesResultsChart();
            }
            catch (Exception error)
            {
                _master.SendLogMessage(error.ToString(), LogMessageType.Error);
            }

        }

        private SortBotsType _sortBotsType;

        private void PaintBotInTable(string botName)
        {
            for (int i2 = 0; i2 < _gridResults.Rows.Count; i2++)
            {
                DataGridViewRow row = _gridResults.Rows[i2];

                if (row.Cells[0].Value.ToString() == botName)
                {
                    for (int i = 0; i < row.Cells.Count; i++)
                    {
                        row.Cells[i].Style.ForeColor = Color.FromArgb(255, 83, 0);
                    }
                }
                else
                {
                    for (int i = 0; i < row.Cells.Count; i++)
                    {
                        row.Cells[i].Style = _gridResults.DefaultCellStyle;
                    }
                }
            }
        }

        #endregion

        #region Series results chart

        private Chart _chartSeriesResult;

        public void CreateChartSeriesResults()
        {
            _chartSeriesResult = new Chart();

            ChartArea area = new ChartArea("Prime");

            _chartSeriesResult.ChartAreas.Clear();
            _chartSeriesResult.ChartAreas.Add(area);
            _chartSeriesResult.BackColor = Color.FromArgb(21, 26, 30);
            _chartSeriesResult.ChartAreas[0].AxisX.TitleForeColor = Color.FromArgb(149, 159, 176);

            for (int i = 0; _chartSeriesResult.ChartAreas != null && i < _chartSeriesResult.ChartAreas.Count; i++)
            {
                _chartSeriesResult.ChartAreas[i].BackColor = Color.FromArgb(21, 26, 30);
                _chartSeriesResult.ChartAreas[i].CursorX.IsUserSelectionEnabled = false;
                _chartSeriesResult.ChartAreas[i].CursorX.IsUserEnabled = true;
                _chartSeriesResult.ChartAreas[i].CursorX.LineColor = Color.FromArgb(255, 83, 0);
                _chartSeriesResult.ChartAreas[i].CursorX.LineWidth = 2;
                _chartSeriesResult.ChartAreas[i].BorderColor = Color.FromArgb(17, 18, 23);
                _chartSeriesResult.ChartAreas[i].CursorY.LineColor = Color.FromArgb(149, 159, 176);

                foreach (var axe in _chartSeriesResult.ChartAreas[i].Axes)
                {
                    axe.LabelStyle.ForeColor = Color.FromArgb(149, 159, 176);
                }
            }

            Series series = new Series();
            series.ChartType = SeriesChartType.Column;
            _chartSeriesResult.Series.Clear();
            _chartSeriesResult.Series.Add(series);

            WindowsFormsHostResultsChart.Child = _chartSeriesResult;

            _chartSeriesResult.SuppressExceptions = true;
            _chartSeriesResult.Click += _chartSeriesResult_Click;
        }

        private void _chartSeriesResult_Click(object sender, EventArgs e)
        {
            try
            {
                if (_chartSeriesResult.ChartAreas[0].CursorX.Position == Double.NaN)
                {
                    return;
                }

                int index = (int)_chartSeriesResult.ChartAreas[0].CursorX.Position;

                index--;

                for (int i = 0; i < _chartSeriesResult.Series[0].Points.Count; i++)
                {
                    if (index == i)
                    {
                        continue;
                    }
                    _chartSeriesResult.Series[0].Points[i].Label = null;
                    _chartSeriesResult.Series[0].Points[i].LabelForeColor = Color.White;
                }

                if (index >= _chartSeriesResult.Series[0].Points.Count)
                {
                    return;
                }

                if (_chartSeriesResult.Series[0].Points[index].Label
                    != _chartSeriesResult.Series[0].Points[index].ToolTip)
                {
                    _chartSeriesResult.Series[0].Points[index].Label
                     = _chartSeriesResult.Series[0].Points[index].ToolTip;
                    string botName = _chartSeriesResult.Series[0].Points[index].ToolTip.Split('\n')[0];
                    PaintBotInTable(botName);
                }
                else
                {
                    _chartSeriesResult.ChartAreas[0].CursorX.Position = Double.NaN;
                    _chartSeriesResult.Series[0].Points[index].Label = null;
                    PaintBotInTable(" ");
                }
            }
            catch (Exception ex)
            {
                _master.SendLogMessage(ex.ToString(), LogMessageType.Error);
            }
        }

        private void PaintSeriesResultsChart()
        {
            try
            {
                if (_chartSeriesResult.InvokeRequired)
                {
                    _chartSeriesResult.Invoke(new Action(PaintSeriesResultsChart));
                    return;
                }

                _chartSeriesResult.Series[0].Points.Clear();

                if (_reports == null)
                {
                    return;
                }

                if (_gridFazesEnd.CurrentCell == null)
                {
                    return;
                }

                int num = 0;
                num = _gridFazesEnd.CurrentCell.RowIndex;

                if (num >= _reports.Count)
                {
                    return;
                }

                OptimizerFazeReport fazeReport = _reports[num];

                if (fazeReport == null)
                {
                    return;
                }

                LabelSeriesResultChart.Content
                 = OsLocalization.Optimizer.Label67 + " "
                 + (num + 1) + " " + fazeReport.Faze.TypeFaze + ". "
                 + OsLocalization.Optimizer.Label69 + ": " + _sortBotsType;


                List<ChartOptimizationResultValue> values = new List<ChartOptimizationResultValue>();

                for (int i = 0; i < fazeReport.Reports.Count; i++)
                {
                    OptimizerReport report = fazeReport.Reports[i];

                    if (report == null ||
                        report.TabsReports.Count == 0)
                    {
                        continue;
                    }

                    ChartOptimizationResultValue curReport = new ChartOptimizationResultValue();
                    values.Add(curReport);
                    curReport.BotName = report.BotName;
                    curReport.BotNum = report.BotNum;
                    curReport.Parameters = report.GetParametersToDataTable();

                    if (_sortBotsType == SortBotsType.PositionCount)
                    {
                        curReport.Value = report.PositionsCount;
                    }
                    else if (_sortBotsType == SortBotsType.TotalProfit)
                    {
                        curReport.Value = Math.Round(report.TotalProfitPercent, 5);
                    }
                    else if (_sortBotsType == SortBotsType.MaxDrawDawn)
                    {
                        curReport.Value = Math.Round(report.MaxDrawDawn, 5);
                    }
                    else if (_sortBotsType == SortBotsType.AverageProfit)
                    {
                        curReport.Value = Math.Round(report.AverageProfit, 5);
                    }
                    else if (_sortBotsType == SortBotsType.AverageProfitPercent)
                    {
                        curReport.Value = Math.Round(report.AverageProfitPercentOneContract, 5);
                    }
                    else if (_sortBotsType == SortBotsType.ProfitFactor)
                    {
                        curReport.Value = Math.Round(report.ProfitFactor, 5);
                    }
                    else if (_sortBotsType == SortBotsType.PayOffRatio)
                    {
                        curReport.Value = Math.Round(report.PayOffRatio, 5);
                    }
                    else if (_sortBotsType == SortBotsType.Recovery)
                    {
                        curReport.Value = Math.Round(report.Recovery, 5);
                    }
                    else if (_sortBotsType == SortBotsType.SharpRatio)
                    {
                        curReport.Value = Math.Round(report.SharpRatio, 5);
                    }
                }

                SetColorInOptimizationResultValues(values);

                values = values.OrderBy(x => x.BotNum).ToList();

                _lastValues = values;

                decimal max = decimal.MinValue;
                decimal min = decimal.MaxValue;

                for (int i = 0; i < values.Count; i++)
                {
                    ChartOptimizationResultValue curValue = values[i];

                    if (curValue.Value > max)
                    {
                        max = curValue.Value;
                    }
                    if (curValue.Value < min)
                    {
                        min = curValue.Value;
                    }

                    double valueToChart = Convert.ToDouble(Math.Round(curValue.Value, 4));

                    DataPoint point1 = new DataPoint(i + 1, valueToChart);
                    point1.ToolTip = curValue.ToolTip;
                    point1.Color = curValue.Color;

                    _chartSeriesResult.Series[0].Points.Add(point1);
                }

                max = Math.Round(max, 4);
                min = Math.Round(min, 4);

                if (max != decimal.MinValue
                    && max != 0
                    && min > 0)
                {
                    _chartSeriesResult.ChartAreas[0].AxisY.Maximum = Convert.ToDouble(max);
                    _chartSeriesResult.ChartAreas[0].AxisY.Minimum = 0;
                }
                else if (max != decimal.MinValue &&
                    min != decimal.MaxValue
                    && max != min)
                {
                    _chartSeriesResult.ChartAreas[0].AxisY.Maximum = Convert.ToDouble(max);
                    _chartSeriesResult.ChartAreas[0].AxisY.Minimum = Convert.ToDouble(min);
                }

                // label

                if (_chartSeriesResult.ChartAreas[0].CursorX.Position != Double.NaN)
                {
                    for (int i = 0; i < _chartSeriesResult.Series[0].Points.Count; i++)
                    {
                        _chartSeriesResult.Series[0].Points[i].Label = null;
                        _chartSeriesResult.Series[0].Points[i].LabelForeColor = Color.White;
                    }

                    int index = (int)_chartSeriesResult.ChartAreas[0].CursorX.Position;

                    index--;

                    if (index >= 0
                        && index < _chartSeriesResult.Series[0].Points.Count)
                    {
                        _chartSeriesResult.Series[0].Points[index].Label
                      = _chartSeriesResult.Series[0].Points[index].ToolTip;

                        string botName =
                            _chartSeriesResult.Series[0].Points[index].ToolTip.Split('\n')[0];
                        PaintBotInTable(botName);
                    }
                }
            }
            catch (Exception ex)
            {
                _master.SendLogMessage(ex.ToString(), LogMessageType.Error);
            }
        }

        List<ChartOptimizationResultValue> _lastValues;

        private void SetColorInOptimizationResultValues(List<ChartOptimizationResultValue> resultValues)
        {
            // 15% green
            // 15% dark green

            for (int i = 0; i < resultValues.Count; i++)
            {
                resultValues[i].Color = Color.Gray;
            }

            if (resultValues.Count <= 1)
            {
                return;
            }

            List<ChartOptimizationResultValue> sortedValue = new List<ChartOptimizationResultValue>();

            sortedValue = resultValues.OrderBy(x => x.Value).ToList();
            sortedValue.Reverse();

            int first15PercentMaxNum = Convert.ToInt32(sortedValue.Count * 0.15m);
            int first30PercentMaxNum = Convert.ToInt32(sortedValue.Count * 0.30m);

            for (int i = 0; i < sortedValue.Count; i++)
            {
                if (i < first15PercentMaxNum)
                {
                    sortedValue[i].Color = Color.Green;
                }
                else if (i < first30PercentMaxNum)
                {
                    sortedValue[i].Color = Color.DarkGreen;
                }
                else
                {
                    resultValues[i].Color = Color.Gray;
                }
            }
        }

        #endregion
    }

    public class ChartOptimizationResultValue
    {
        public decimal Value;

        public string BotName;

        public int BotNum;

        public string Parameters;

        public Color Color;

        public string ToolTip
        {
            get
            {
                string result = BotName + "\n";
                result += OsLocalization.Optimizer.Label69 + ": " + Value;

                return result;
            }
        }
    }
}