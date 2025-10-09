/*
 * Your rights to use code governed by this license https://github.com/AlexWan/OsEngine/blob/master/LICENSE
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using System.Windows.Forms.DataVisualization.Charting;
using System.Windows.Forms.Integration;
using OsEngine.Charts;
using OsEngine.Entity;
using OsEngine.Language;
using OsEngine.Logging;

namespace OsEngine.OsOptimizer
{
    public class OptimizerReportCharting
    {
        public OptimizerReportCharting(
            WindowsFormsHost hostStepsOfOptimization,
            WindowsFormsHost hostRobustness,
            System.Windows.Controls.ComboBox boxTypeSort,
            System.Windows.Controls.Label labelRobustnessMetricValue,
            System.Windows.Controls.ComboBox boxTypeSortBotNum)
        {
            _sortBotsType = SortBotsType.TotalProfit;
            _hostStepsOfOptimization = hostStepsOfOptimization;
            _hostRobustness = hostRobustness;
            _labelRobustnessMetricValue = labelRobustnessMetricValue;

            boxTypeSort.Items.Add(SortBotsType.PositionCount.ToString());
            boxTypeSort.Items.Add(SortBotsType.TotalProfit.ToString());
            boxTypeSort.Items.Add(SortBotsType.MaxDrawDawn.ToString());
            boxTypeSort.Items.Add(SortBotsType.AverageProfit.ToString());
            boxTypeSort.Items.Add(SortBotsType.AverageProfitPercent.ToString());
            boxTypeSort.Items.Add(SortBotsType.ProfitFactor.ToString());
            boxTypeSort.Items.Add(SortBotsType.PayOffRatio.ToString());
            boxTypeSort.Items.Add(SortBotsType.Recovery.ToString());
            boxTypeSort.Items.Add(SortBotsType.SharpRatio.ToString());

            boxTypeSort.SelectedItem = SortBotsType.TotalProfit.ToString();
            boxTypeSort.SelectionChanged += _gridResults_SelectionChanged;

            _boxTypeSort = boxTypeSort;

            _boxTypeSortBotNum = boxTypeSortBotNum;

            for (int i = 0; i < 99; i++)
            {
                _boxTypeSortBotNum.Items.Add(i.ToString());
            }

            _boxTypeSortBotNum.SelectedItem = "0";
            _boxTypeSortBotNum.SelectionChanged += _boxTypeSortBotNum_SelectionChanged;

            CreateStepsOfOptimization();
            CreateRobustnessChart();
        }

        private System.Windows.Controls.ComboBox _boxTypeSort;

        private System.Windows.Controls.ComboBox _boxTypeSortBotNum;

        System.Windows.Controls.Label _labelRobustnessMetricValue;

        private void _boxTypeSortBotNum_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            try
            {
                _sortBotPercent = Convert.ToInt32(_boxTypeSortBotNum.SelectedItem.ToString());

                if (_reports != null)
                {
                    ReLoad(_reports);
                }
            }
            catch
            {

            }
        }

        private void _gridResults_SelectionChanged(object sender, EventArgs e)
        {

            if (_boxTypeSort.Items.Count == 0)
            {
                return;
            }

            int columnSelect = _boxTypeSort.SelectedIndex;

            if (columnSelect == 0)
            {
                _sortBotsType = SortBotsType.PositionCount;
            }
            else if (columnSelect == 1)
            {
                _sortBotsType = SortBotsType.TotalProfit;
            }
            else if (columnSelect == 2)
            {
                _sortBotsType = SortBotsType.MaxDrawDawn;
            }
            else if (columnSelect == 3)
            {
                _sortBotsType = SortBotsType.AverageProfit;
            }
            else if (columnSelect == 4)
            {
                _sortBotsType = SortBotsType.AverageProfitPercent;
            }
            else if (columnSelect == 5)
            {
                _sortBotsType = SortBotsType.ProfitFactor;
            }
            else if (columnSelect == 6)
            {
                _sortBotsType = SortBotsType.PayOffRatio;
            }
            else if (columnSelect == 7)
            {
                _sortBotsType = SortBotsType.Recovery;
            }
            else if (columnSelect == 8)
            {
                _sortBotsType = SortBotsType.SharpRatio;
            }
            else
            {
                return;
            }

            if (_reports != null)
            {
                ReLoad(_reports);
            }
        }

        public void ReLoad(List<OptimizerFazeReport> reports)
        {
            try
            {
                _reports = reports;

                if (_reports == null
                    || _reports.Count <= 1)
                {
                    return;
                }

                for (int i = 0; i < reports.Count; i++)
                {
                    OptimizerFazeReport.SortResults(reports[i].Reports, _sortBotsType);
                }

                GetBestBotNum(reports[0].Reports);

                UpdGridStepsOfOptimization();
                UpdateRobustnessChart();
                UpdateTotalProfitChart();
                UpdateAverageProfitChart();
                UpdateProfitFactorChart();
            }
            catch (Exception e)
            {
                SendLogMessage(e.ToString(), LogMessageType.Error);
            }
        }

        private void GetBestBotNum(List<OptimizerReport> reports)
        {
            if (_sortBotPercent == 0)
            {
                _sortBotNumber = 0;
                return;
            }

            decimal countBotsPercent = reports.Count / 100m;

            decimal result = countBotsPercent * _sortBotPercent;

            _sortBotNumber = Convert.ToInt32(result);

            if (_sortBotNumber > reports.Count)
            {
                _sortBotNumber = reports.Count - 1;
            }
        }

        private SortBotsType _sortBotsType;

        private int _sortBotPercent = 0;

        private int _sortBotNumber = 0;

        private List<OptimizerFazeReport> _reports;

        #region Fazes in table

        private WindowsFormsHost _hostStepsOfOptimization;

        private DataGridView _gridStepsOfOptimization;

        private void CreateStepsOfOptimization()
        {
            _gridStepsOfOptimization = DataGridFactory.GetDataGridView(DataGridViewSelectionMode.ColumnHeaderSelect,
                DataGridViewAutoSizeRowsMode.None, true);

            _gridStepsOfOptimization.ScrollBars = ScrollBars.Vertical;

            DataGridViewTextBoxCell cell0 = new DataGridViewTextBoxCell();
            cell0.Style = _gridStepsOfOptimization.DefaultCellStyle;

            DataGridViewColumn column0 = new DataGridViewColumn();
            column0.CellTemplate = cell0;
            column0.HeaderText = "Period";
            column0.ReadOnly = true;
            column0.Width = 80;

            _gridStepsOfOptimization.Columns.Add(column0);

            DataGridViewColumn column1 = new DataGridViewColumn();
            column1.CellTemplate = cell0;
            column1.HeaderText = "Start";
            column1.ReadOnly = false;
            column1.Width = 80;
            _gridStepsOfOptimization.Columns.Add(column1);

            DataGridViewColumn column21 = new DataGridViewColumn();
            column21.CellTemplate = cell0;
            column21.HeaderText = "End";
            column21.ReadOnly = false;
            column21.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            _gridStepsOfOptimization.Columns.Add(column21);

            DataGridViewColumn column2 = new DataGridViewColumn();
            column2.CellTemplate = cell0;
            column2.HeaderText = "Best bot number InSample";
            column2.ReadOnly = false;
            column2.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            _gridStepsOfOptimization.Columns.Add(column2);

            DataGridViewColumn column3 = new DataGridViewColumn();
            column3.CellTemplate = cell0;
            column3.HeaderText = "Parameters";
            column3.ReadOnly = false;
            column3.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            _gridStepsOfOptimization.Columns.Add(column3);

            DataGridViewColumn column4 = new DataGridViewColumn();
            column4.CellTemplate = cell0;
            column4.HeaderText = "Bot results in OutOfSample";
            column4.ReadOnly = false;
            column4.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            _gridStepsOfOptimization.Columns.Add(column4);

            DataGridViewColumn column5 = new DataGridViewColumn();
            column5.CellTemplate = cell0;
            column5.HeaderText = "Profit";
            column5.ReadOnly = false;
            column5.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            _gridStepsOfOptimization.Columns.Add(column5);

            DataGridViewColumn column6 = new DataGridViewColumn();
            column6.CellTemplate = cell0;
            column6.HeaderText = "Average profit %";
            column6.ReadOnly = false;
            column6.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            _gridStepsOfOptimization.Columns.Add(column6);

            DataGridViewColumn column7 = new DataGridViewColumn();
            column7.CellTemplate = cell0;
            column7.HeaderText = "Position count";
            column7.ReadOnly = false;
            column7.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            _gridStepsOfOptimization.Columns.Add(column7);

            DataGridViewColumn column8 = new DataGridViewColumn();
            column8.CellTemplate = cell0;
            column8.HeaderText = "Sharp ratio";
            column8.ReadOnly = false;
            column8.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            _gridStepsOfOptimization.Columns.Add(column8);

            _gridStepsOfOptimization.Rows.Add(null, null);

            _hostStepsOfOptimization.Child = _gridStepsOfOptimization;
            _gridStepsOfOptimization.DataError += _gridStepsOfOptimization_DataError;
        }

        private void _gridStepsOfOptimization_DataError(object sender, DataGridViewDataErrorEventArgs e)
        {
            SendLogMessage(e.ToString(), LogMessageType.Error);
        }

        private void UpdGridStepsOfOptimization()
        {
            if (_gridStepsOfOptimization.InvokeRequired)
            {
                _gridStepsOfOptimization.Invoke(new Action(UpdGridStepsOfOptimization));
                return;
            }

            _gridStepsOfOptimization.Rows.Clear();

            if (_reports == null)
            {
                return;
            }

            try
            {
                if (_reports.Count <= 1)
                {
                    return;
                }

                if (_reports.Count == 2 &&
                    _reports[1].Reports.Count == 0)
                {
                    return;
                }

                OptimizerReport inSampleReport = null;

                for (int i = 0; i < _reports.Count; i++)
                {
                    OptimizerFazeReport curReport = _reports[i];

                    if (curReport == null ||
                        curReport.Reports == null ||
                        curReport.Reports.Count == 0)
                    {
                        continue;
                    }

                    if (curReport.Faze.TypeFaze == OptimizerFazeType.InSample)
                    {
                        inSampleReport = curReport.Reports[_sortBotNumber];
                    }

                    OptimizerReport reportToPaint;

                    if (curReport.Faze.TypeFaze == OptimizerFazeType.InSample)
                    {
                        reportToPaint = curReport.Reports[_sortBotNumber];
                    }
                    else // if(curReport.Faze.TypeFaze == OptimizerFazeType.OutOfSample)
                    {
                        string botName = inSampleReport.BotName.Replace(" InSample", "").Replace("OpT", "");
                        reportToPaint = curReport.Reports.Find(rep => rep.BotName.StartsWith(botName));
                    }

                    if (reportToPaint == null)
                    {
                        continue;
                    }

                    DataGridViewRow row = new DataGridViewRow();
                    row.Height = 30;
                    row.Cells.Add(new DataGridViewTextBoxCell());
                    row.Cells[0].Value = curReport.Faze.TypeFaze.ToString();


                    DataGridViewTextBoxCell cell2 = new DataGridViewTextBoxCell();
                    cell2.Value = curReport.Faze.TimeStart.ToString(OsLocalization.ShortDateFormatString);
                    row.Cells.Add(cell2);

                    DataGridViewTextBoxCell cell3 = new DataGridViewTextBoxCell();
                    cell3.Value = curReport.Faze.TimeEnd.ToString(OsLocalization.ShortDateFormatString);
                    row.Cells.Add(cell3);

                    DataGridViewTextBoxCell cell4 = new DataGridViewTextBoxCell();

                    if (curReport.Faze.TypeFaze == OptimizerFazeType.InSample)
                    {
                        cell4.Value = inSampleReport.BotName.Replace(" InSample", "").Replace("OpT", "");
                    }
                    row.Cells.Add(cell4);

                    DataGridViewTextBoxCell cell5 = new DataGridViewTextBoxCell();
                    cell5.Value = reportToPaint.GetParametersToDataTable();
                    row.Cells.Add(cell5);

                    DataGridViewTextBoxCell cell6 = new DataGridViewTextBoxCell();

                    if (curReport.Faze.TypeFaze == OptimizerFazeType.OutOfSample)
                    {
                        string botName = inSampleReport.BotName.Replace(" InSample", "");
                        // reportToPaint = curReport.Reports.Find(rep => rep.BotName.StartsWith(botName));

                        for (int i2 = 0; i2 < curReport.Reports.Count; i2++)
                        {
                            string curName = curReport.Reports[i2].BotName.Replace(" InSample", "").Replace(" OutOfSample", "");

                            if (curName == botName)
                            {
                                cell6.Value = (i2 + 1).ToString();
                                break;
                            }
                        }
                    }
                    row.Cells.Add(cell6);

                    DataGridViewTextBoxCell cell7 = new DataGridViewTextBoxCell();
                    cell7.Value = Math.Round(reportToPaint.TotalProfit, 4).ToStringWithNoEndZero();
                    row.Cells.Add(cell7);

                    DataGridViewTextBoxCell cell8 = new DataGridViewTextBoxCell();
                    cell8.Value = Math.Round(reportToPaint.AverageProfitPercentOneContract, 4).ToStringWithNoEndZero();
                    row.Cells.Add(cell8);

                    DataGridViewTextBoxCell cell9 = new DataGridViewTextBoxCell();
                    cell9.Value = reportToPaint.PositionsCount.ToString();
                    row.Cells.Add(cell9);

                    DataGridViewTextBoxCell cell10 = new DataGridViewTextBoxCell();
                    cell10.Value = reportToPaint.SharpRatio.ToString();
                    row.Cells.Add(cell10);

                    _gridStepsOfOptimization.Rows.Add(row);
                }
            }
            catch (Exception ex)
            {
                SendLogMessage(ex.ToString(), LogMessageType.Error);
            }
        }

        #endregion

        #region Robustness

        private WindowsFormsHost _hostRobustness;

        private Chart _chartRobustness;

        private void CreateRobustnessChart()
        {
            _chartRobustness = new Chart();

            ChartArea area = new ChartArea("Prime");

            _chartRobustness.ChartAreas.Clear();
            _chartRobustness.ChartAreas.Add(area);
            _chartRobustness.BackColor = Color.FromArgb(21, 26, 30);
            _chartRobustness.ChartAreas[0].AxisX.TitleForeColor = Color.FromArgb(149, 159, 176);

            for (int i = 0; _chartRobustness.ChartAreas != null && i < _chartRobustness.ChartAreas.Count; i++)
            {
                _chartRobustness.ChartAreas[i].BackColor = Color.FromArgb(21, 26, 30);
                _chartRobustness.ChartAreas[i].BorderColor = Color.FromArgb(17, 18, 23);
                _chartRobustness.ChartAreas[i].CursorY.LineColor = Color.FromArgb(149, 159, 176);
                _chartRobustness.ChartAreas[i].CursorX.LineColor = Color.FromArgb(149, 159, 176);

                foreach (var axe in _chartRobustness.ChartAreas[i].Axes)
                {
                    axe.LabelStyle.ForeColor = Color.FromArgb(149, 159, 176);
                }
            }

            Series series = new Series();
            series.ChartType = SeriesChartType.Column;
            _chartRobustness.Series.Clear();
            _chartRobustness.Series.Add(series);

            _hostRobustness.Child = _chartRobustness;

            _chartRobustness.SuppressExceptions = true;
        }

        private void UpdateRobustnessChart()
        {
            if (_gridStepsOfOptimization.InvokeRequired)
            {
                _gridStepsOfOptimization.Invoke(new Action(UpdateRobustnessChart));
                return;
            }

            try
            {
                _labelRobustnessMetricValue.Content = "";

                int countBestTwenty = 0;
                int count20_40 = 0;
                int count40_60 = 0;
                int count60_80 = 0;
                int countWorst20 = 0;

                if (_reports == null)
                {
                    return;
                }

                if (_reports.Count <= 1)
                {
                    return;
                }

                if (_reports.Count == 2 &&
                    _reports[1].Reports.Count == 0)
                {
                    return;
                }

                OptimizerReport inSampleReport = null;

                decimal max = decimal.MinValue;

                for (int i = 0; i < _reports.Count; i++)
                {
                    OptimizerFazeReport curReport = _reports[i];

                    if (curReport == null ||
                        curReport.Reports == null ||
                        curReport.Reports.Count == 0)
                    {
                        continue;
                    }

                    if (curReport.Faze.TypeFaze == OptimizerFazeType.InSample)
                    {
                        inSampleReport = curReport.Reports[_sortBotNumber];
                    }

                    if (curReport.Faze.TypeFaze == OptimizerFazeType.OutOfSample)
                    {
                        string botName = inSampleReport.BotName.Replace(" InSample", "");
                        // reportToPaint = curReport.Reports.Find(rep => rep.BotName.StartsWith(botName));

                        for (int i2 = 0; i2 < curReport.Reports.Count; i2++)
                        {
                            if (curReport.Reports[i2].BotName.StartsWith(botName))
                            {
                                decimal botNum = Convert.ToDecimal(i2 + 1) / curReport.Reports.Count * 100m;

                                if (botNum <= 20)
                                {
                                    countBestTwenty += 1;

                                    if (countBestTwenty > max)
                                    {
                                        max = countBestTwenty;
                                    }
                                }
                                else if (botNum > 20 && botNum <= 40)
                                {
                                    count20_40 += 1;
                                    if (count20_40 > max)
                                    {
                                        max = count20_40;
                                    }
                                }
                                else if (botNum > 40 && botNum <= 60)
                                {
                                    count40_60 += 1;

                                    if (count40_60 > max)
                                    {
                                        max = count40_60;
                                    }
                                }
                                else if (botNum > 60 && botNum <= 80)
                                {
                                    count60_80 += 1;

                                    if (count60_80 > max)
                                    {
                                        max = count60_80;
                                    }
                                }
                                else if (botNum > 80)
                                {
                                    countWorst20 += 1;

                                    if (countWorst20 > max)
                                    {
                                        max = countWorst20;
                                    }
                                }

                                break;
                            }
                        }
                    }
                }


                decimal allCount = 0;

                allCount += countBestTwenty;
                allCount += countWorst20;
                allCount += count20_40;
                allCount += count40_60;
                allCount += count60_80;

                if (allCount != 0)
                {
                    decimal oneBestP = 100 / allCount;
                    decimal robustness = 0;

                    robustness += countBestTwenty * oneBestP;
                    robustness += count20_40 * oneBestP * 0.75m;
                    robustness += count40_60 * oneBestP * 0.5m;
                    robustness += count60_80 * oneBestP * 0.25m;

                    _labelRobustnessMetricValue.Content = Math.Round(robustness, 2).ToString() + " %";
                }

                _chartRobustness.Series[0].Points.ClearFast();

                DataPoint point1 = new DataPoint(1, countBestTwenty);
                point1.AxisLabel = "Best 20%";
                point1.Color = Color.DarkGreen;

                DataPoint point2 = new DataPoint(2, count20_40);
                point2.AxisLabel = "20 - 40 %";
                point2.Color = Color.DarkGreen;

                DataPoint point3 = new DataPoint(3, count40_60);
                point3.AxisLabel = "40 - 60 %";
                point3.Color = Color.FromArgb(149, 159, 176);

                DataPoint point4 = new DataPoint(4, count60_80);
                point4.AxisLabel = "60 - 80 %";
                point4.Color = Color.DarkRed;

                DataPoint point5 = new DataPoint(5, countWorst20);
                point5.AxisLabel = "Worst 20 %";
                point5.Color = Color.DarkRed;

                _chartRobustness.Series[0].Points.Add(point1);
                _chartRobustness.Series[0].Points.Add(point2);
                _chartRobustness.Series[0].Points.Add(point3);
                _chartRobustness.Series[0].Points.Add(point4);
                _chartRobustness.Series[0].Points.Add(point5);

                if (max != decimal.MinValue)
                {
                    _chartRobustness.ChartAreas[0].AxisY.Maximum = Convert.ToDouble(max);
                    _chartRobustness.ChartAreas[0].AxisY.Minimum = 0;
                }
            }
            catch (Exception ex)
            {
                SendLogMessage(ex.ToString(), LogMessageType.Error);
            }
        }

        #endregion

        #region Total profit

        System.Windows.Controls.ComboBox _comboBoxTotalProfitEquityType;

        private Chart _chartTotalProfit;

        private WindowsFormsHost _hostTotalProfit;

        public void ActivateTotalProfitChart(WindowsFormsHost hostTotalProfit, System.Windows.Controls.ComboBox comboBoxProfitType)
        {
            _hostTotalProfit = hostTotalProfit;
            _comboBoxTotalProfitEquityType = comboBoxProfitType;

            _comboBoxTotalProfitEquityType.Items.Add("Absolute");
            _comboBoxTotalProfitEquityType.Items.Add("Persent");
            _comboBoxTotalProfitEquityType.SelectedItem = "Absolute";

            CreateTotalProfitChart();

            UpdateTotalProfitChart();

            _comboBoxTotalProfitEquityType.SelectionChanged += _comboBoxProfitType_SelectionChanged;
        }

        private void _comboBoxProfitType_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            UpdateTotalProfitChart();
        }

        private void CreateTotalProfitChart()
        {
            _chartTotalProfit = new Chart();

            ChartArea area = new ChartArea("Prime");

            _chartTotalProfit.ChartAreas.Clear();
            _chartTotalProfit.ChartAreas.Add(area);
            _chartTotalProfit.BackColor = Color.FromArgb(21, 26, 30);
            _chartTotalProfit.ChartAreas[0].AxisX.TitleForeColor = Color.FromArgb(149, 159, 176);

            for (int i = 0; _chartTotalProfit.ChartAreas != null && i < _chartTotalProfit.ChartAreas.Count; i++)
            {
                _chartTotalProfit.ChartAreas[i].BackColor = Color.FromArgb(21, 26, 30);
                _chartTotalProfit.ChartAreas[i].BorderColor = Color.FromArgb(17, 18, 23);
                _chartTotalProfit.ChartAreas[i].CursorY.LineColor = Color.FromArgb(149, 159, 176);
                _chartTotalProfit.ChartAreas[i].CursorX.LineColor = Color.FromArgb(149, 159, 176);

                foreach (var axe in _chartTotalProfit.ChartAreas[i].Axes)
                {
                    axe.LabelStyle.ForeColor = Color.FromArgb(149, 159, 176);
                }
            }

            Series series = new Series();
            series.ChartType = SeriesChartType.Candlestick;
            _chartTotalProfit.Series.Clear();
            _chartTotalProfit.Series.Add(series);

            _hostTotalProfit.Child = _chartTotalProfit;

            _chartTotalProfit.SuppressExceptions = true;
        }

        private void UpdateTotalProfitChart()
        {
            if (_chartTotalProfit == null)
            {
                return;
            }

            if (_gridStepsOfOptimization.InvokeRequired)
            {
                _gridStepsOfOptimization.Invoke(new Action(UpdateTotalProfitChart));
                return;
            }

            if (_reports == null)
            {
                return;
            }

            if (_reports.Count <= 1)
            {
                return;
            }

            try
            {

                string profitType = _comboBoxTotalProfitEquityType.SelectedItem.ToString();

                List<decimal> profitsSumm = new List<decimal>();

                List<decimal> profit = new List<decimal>();

                OptimizerReport inSampleReport = null;

                List<OptimizerFazeReport> outOfSampleReports = new List<OptimizerFazeReport>();

                for (int i = 0; i < _reports.Count; i++)
                {
                    OptimizerFazeReport curReport = _reports[i];

                    if (curReport == null ||
                        curReport.Reports == null ||
                        curReport.Reports.Count == 0)
                    {
                        continue;
                    }

                    if (curReport.Faze.TypeFaze == OptimizerFazeType.InSample)
                    {
                        inSampleReport = curReport.Reports[_sortBotNumber];
                    }

                    if (curReport.Faze.TypeFaze == OptimizerFazeType.OutOfSample)
                    {


                        string botName = inSampleReport.BotName.Replace(" InSample", "");
                        // reportToPaint = curReport.Reports.Find(rep => rep.BotName.StartsWith(botName));

                        for (int i2 = 0; i2 < curReport.Reports.Count; i2++)
                        {
                            if (curReport.Reports[i2].BotName.StartsWith(botName))
                            {
                                outOfSampleReports.Add(curReport);
                                if (profitType == "Absolute")
                                {
                                    profit.Add(curReport.Reports[i2].TotalProfit);
                                    if (profitsSumm.Count == 0)
                                    {
                                        profitsSumm.Add(curReport.Reports[i2].TotalProfit);
                                    }
                                    else
                                    {
                                        profitsSumm.Add(profitsSumm[profitsSumm.Count - 1] + curReport.Reports[i2].TotalProfit);
                                    }
                                }
                                else if (profitType == "Persent")
                                {
                                    profit.Add(curReport.Reports[i2].TotalProfitPercent);
                                    if (profitsSumm.Count == 0)
                                    {
                                        profitsSumm.Add(curReport.Reports[i2].TotalProfitPercent);
                                    }
                                    else
                                    {
                                        profitsSumm.Add(profitsSumm[profitsSumm.Count - 1] + curReport.Reports[i2].TotalProfitPercent);
                                    }
                                }

                                break;
                            }
                        }
                    }
                }

                Series series = _chartTotalProfit.Series[0];

                series.Points.ClearFast();

                if (profitsSumm.Count == 0)
                {
                    return;
                }

                decimal max = decimal.MinValue;
                decimal min = decimal.MaxValue;

                for (int i = 0; i < profitsSumm.Count; i++)
                {
                    decimal open = 0;
                    decimal close = 0;
                    decimal low = 0;
                    decimal high = 0;

                    if (i > 0)
                    {
                        open = profitsSumm[i - 1];
                    }
                    close = profitsSumm[i];

                    if (close > max)
                    {
                        max = close;
                    }
                    if (close < min)
                    {
                        min = close;
                    }

                    if (close > open)
                    {
                        low = open;
                        high = close;
                    }
                    else
                    {
                        high = open;
                        low = close;
                    }

                    series.Points.AddXY(i + 1, low, high, open, close);

                    if (close > open)
                    {
                        series.Points[series.Points.Count - 1].Color = Color.DarkGreen;
                        series.Points[series.Points.Count - 1].BorderColor = Color.DarkGreen;
                        series.Points[series.Points.Count - 1].BackSecondaryColor = Color.DarkGreen;
                    }
                    else
                    {
                        series.Points[series.Points.Count - 1].Color = Color.DarkRed;
                        series.Points[series.Points.Count - 1].BorderColor = Color.DarkRed;
                        series.Points[series.Points.Count - 1].BackSecondaryColor = Color.DarkRed;
                    }

                    string toolTip = "";

                    toolTip = "OOS " + (i + 1) + "\n" +
                         "start: " + outOfSampleReports[i].Faze.TimeStart.ToString(OsLocalization.ShortDateFormatString) + "\n" +
                         "end: " + outOfSampleReports[i].Faze.TimeEnd.ToString(OsLocalization.ShortDateFormatString) + "\n" +
                         "profit: " + profit[i].ToStringWithNoEndZero();

                    series.Points[series.Points.Count - 1].ToolTip = toolTip;

                    if (i + 1 == profitsSumm.Count)
                    { // last point
                        series.Points[series.Points.Count - 1].Label = Math.Round(profitsSumm[i], 4).ToStringWithNoEndZero();
                        series.Points[series.Points.Count - 1].LabelForeColor = Color.AntiqueWhite;
                    }

                }

                if (max != decimal.MinValue &&
                    min != decimal.MaxValue)
                {
                    max = Math.Round(max + max * 0.2m, 4);
                    min = Math.Round(min, 4);

                    if (max > min)
                    {
                        _chartTotalProfit.ChartAreas[0].AxisY.Maximum = Convert.ToDouble(max);
                        _chartTotalProfit.ChartAreas[0].AxisY.Minimum = Convert.ToDouble(min);
                    }
                }
            }
            catch (Exception ex)
            {
                SendLogMessage(ex.ToString(), LogMessageType.Error);
            }
        }

        #endregion

        #region Average profit

        private WindowsFormsHost _hostAverageProfitChart;

        private Chart _chartAverageProfit;

        public void ActivateAverageProfitChart(WindowsFormsHost hostAverageProfit)
        {
            _hostAverageProfitChart = hostAverageProfit;

            CreateAverageProfitChart();

            ReLoad(_reports);
        }

        private void CreateAverageProfitChart()
        {
            _chartAverageProfit = new Chart();

            ChartArea area = new ChartArea("Prime");

            _chartAverageProfit.ChartAreas.Clear();
            _chartAverageProfit.ChartAreas.Add(area);
            _chartAverageProfit.BackColor = Color.FromArgb(21, 26, 30);
            _chartAverageProfit.ChartAreas[0].AxisX.TitleForeColor = Color.FromArgb(149, 159, 176);

            for (int i = 0; _chartAverageProfit.ChartAreas != null && i < _chartAverageProfit.ChartAreas.Count; i++)
            {
                _chartAverageProfit.ChartAreas[i].BackColor = Color.FromArgb(21, 26, 30);
                _chartAverageProfit.ChartAreas[i].BorderColor = Color.FromArgb(17, 18, 23);
                _chartAverageProfit.ChartAreas[i].CursorY.LineColor = Color.FromArgb(149, 159, 176);
                _chartAverageProfit.ChartAreas[i].CursorX.LineColor = Color.FromArgb(149, 159, 176);

                foreach (var axe in _chartAverageProfit.ChartAreas[i].Axes)
                {
                    axe.LabelStyle.ForeColor = Color.FromArgb(149, 159, 176);
                }
            }

            Series series = new Series();
            series.ChartType = SeriesChartType.Column;
            _chartAverageProfit.Series.Clear();
            _chartAverageProfit.Series.Add(series);

            Series series2 = new Series();
            series2.ChartType = SeriesChartType.Line;
            _chartAverageProfit.Series.Add(series2);

            Series series3 = new Series();
            series3.ChartType = SeriesChartType.Point;
            _chartAverageProfit.Series.Add(series3);


            _hostAverageProfitChart.Child = _chartAverageProfit;

            _chartAverageProfit.SuppressExceptions = true;
        }

        private void UpdateAverageProfitChart()
        {
            if (_reports == null ||
                _reports.Count == 0)
            {
                return;
            }
            if (_hostAverageProfitChart == null)
            {
                return;
            }

            try
            {

                List<decimal> values = new List<decimal>();
                decimal maxValue = decimal.MinValue;

                decimal averageProfitPercent = 0;

                List<OptimizerFazeReport> outOfSampleReports = new List<OptimizerFazeReport>();

                for (int i = 0; i < _reports.Count; i += 2)
                {
                    // берём из ИнСампле таблицу роботов
                    List<OptimizerReport> bots = _reports[i].Reports;

                    OptimizerReport bestBot = _reports[i].Reports[_sortBotNumber];

                    // находим этого робота в аутОфСемпл

                    if (i + 1 == _reports.Count)
                    {
                        break;
                    }

                    OptimizerReport bestBotInOutOfSample
                        = _reports[i + 1].Reports.Find(b => b.BotName.Replace(" OutOfSample", "") == bestBot.BotName.Replace(" InSample", ""));

                    if (bestBotInOutOfSample == null)
                    {
                        continue;
                    }

                    outOfSampleReports.Add(_reports[i + 1]);

                    decimal value = bestBotInOutOfSample.AverageProfitPercentOneContract;

                    if (maxValue < value)
                    {
                        maxValue = value;
                    }

                    if (values.Count == 0)
                    {
                        values.Add(value);
                    }
                    else
                    {
                        values.Add(value);
                    }

                    averageProfitPercent += value;
                }
                if (values.Count != 0)
                {
                    averageProfitPercent = averageProfitPercent / values.Count;
                }

                // прорисовка

                Series seriesOosValues = _chartAverageProfit.Series[0];
                Series seriesAverageLine = _chartAverageProfit.Series[1];
                Series seriesAveragePoint = _chartAverageProfit.Series[2];

                seriesOosValues.Points.ClearFast();
                seriesAverageLine.Points.ClearFast();
                seriesAveragePoint.Points.ClearFast();

                if (values.Count == 0)
                {
                    return;
                }

                decimal max = decimal.MinValue;
                decimal min = decimal.MaxValue;

                for (int i = 0; i < values.Count; i++)
                {
                    seriesOosValues.Points.AddXY(i + 1, values[i]);

                    if (values[i] > max)
                    {
                        max = values[i];
                    }

                    if (values[i] < min)
                    {
                        min = values[i];
                    }

                    if (values[i] > 0)
                    {
                        seriesOosValues.Points[seriesOosValues.Points.Count - 1].Color = Color.DarkGreen;
                        seriesOosValues.Points[seriesOosValues.Points.Count - 1].BorderColor = Color.DarkGreen;
                        seriesOosValues.Points[seriesOosValues.Points.Count - 1].BackSecondaryColor = Color.DarkGreen;
                    }
                    else
                    {
                        seriesOosValues.Points[seriesOosValues.Points.Count - 1].Color = Color.DarkRed;
                        seriesOosValues.Points[seriesOosValues.Points.Count - 1].BorderColor = Color.DarkRed;
                        seriesOosValues.Points[seriesOosValues.Points.Count - 1].BackSecondaryColor = Color.DarkRed;
                    }

                    string toolTip = "";

                    toolTip = "OOS " + (i + 1) + "\n" +
                        "start: " + outOfSampleReports[i].Faze.TimeStart.ToString(OsLocalization.ShortDateFormatString) + "\n" +
                         "end: " + outOfSampleReports[i].Faze.TimeEnd.ToString(OsLocalization.ShortDateFormatString) + "\n" +
                         "P/L % " + Math.Round(values[i], 4).ToStringWithNoEndZero();

                    seriesOosValues.Points[seriesOosValues.Points.Count - 1].ToolTip = toolTip;
                }

                if (averageProfitPercent != 0)
                {
                    seriesAverageLine.Points.AddXY(1, averageProfitPercent);

                    seriesAverageLine.Points.AddXY(values.Count, averageProfitPercent);

                    for (int i = 0; i < seriesAverageLine.Points.Count; i++)
                    {
                        seriesAverageLine.Points[i].Color = Color.AntiqueWhite;
                    }

                    string label = "Average: " + Math.Round(averageProfitPercent, 4).ToStringWithNoEndZero();
                    seriesAveragePoint.Points.AddXY(values.Count - 1, maxValue + maxValue * 0.05m);
                    seriesAveragePoint.Points[0].Color = Color.AntiqueWhite;

                    seriesAveragePoint.Points[0].Label = label;
                    seriesAveragePoint.Points[0].LabelForeColor = Color.AntiqueWhite;
                }

                if (max != decimal.MinValue &&
                    min != decimal.MaxValue)
                {
                    if(max > 0)
                    {
                        max = Math.Round(max + max * 0.2m, 4);
                    }
                    else
                    {
                        max = Math.Round(max - max * 0.2m, 4);
                    }
                        min = Math.Round(min, 4);

                    if (max > min)
                    {
                        _chartAverageProfit.ChartAreas[0].AxisY.Maximum = Convert.ToDouble(max);
                        _chartAverageProfit.ChartAreas[0].AxisY.Minimum = Convert.ToDouble(min);
                    }
                }

            }
            catch (Exception ex)
            {
                SendLogMessage(ex.ToString(), LogMessageType.Error);
            }


        }

        #endregion

        #region Profit factor

        private WindowsFormsHost _hostProfitFactor;

        private Chart _chartProfitFactor;

        public void ActivateProfitFactorChart(WindowsFormsHost hostProfitFactor)
        {
            _hostProfitFactor = hostProfitFactor;
            CreateProfitFactorChart();

            ReLoad(_reports);
        }

        private void CreateProfitFactorChart()
        {
            _chartProfitFactor = new Chart();

            ChartArea area = new ChartArea("Prime");

            _chartProfitFactor.ChartAreas.Clear();
            _chartProfitFactor.ChartAreas.Add(area);
            _chartProfitFactor.BackColor = Color.FromArgb(21, 26, 30);
            _chartProfitFactor.ChartAreas[0].AxisX.TitleForeColor = Color.FromArgb(149, 159, 176);

            for (int i = 0; _chartProfitFactor.ChartAreas != null && i < _chartProfitFactor.ChartAreas.Count; i++)
            {
                _chartProfitFactor.ChartAreas[i].BackColor = Color.FromArgb(21, 26, 30);
                _chartProfitFactor.ChartAreas[i].BorderColor = Color.FromArgb(17, 18, 23);
                _chartProfitFactor.ChartAreas[i].CursorY.LineColor = Color.FromArgb(149, 159, 176);
                _chartProfitFactor.ChartAreas[i].CursorX.LineColor = Color.FromArgb(149, 159, 176);

                foreach (var axe in _chartProfitFactor.ChartAreas[i].Axes)
                {
                    axe.LabelStyle.ForeColor = Color.FromArgb(149, 159, 176);
                }
            }

            Series series = new Series();
            series.ChartType = SeriesChartType.Column;
            _chartProfitFactor.Series.Clear();
            _chartProfitFactor.Series.Add(series);

            Series series2 = new Series();
            series2.ChartType = SeriesChartType.Line;
            _chartProfitFactor.Series.Add(series2);

            Series series3 = new Series();
            series3.ChartType = SeriesChartType.Point;
            _chartProfitFactor.Series.Add(series3);


            _hostProfitFactor.Child = _chartProfitFactor;

            _chartProfitFactor.SuppressExceptions = true;
        }

        private void UpdateProfitFactorChart()
        {
            if (_reports == null ||
                           _reports.Count == 0)
            {
                return;
            }
            if (_hostProfitFactor == null)
            {
                return;
            }

            try
            {

                List<decimal> values = new List<decimal>();

                decimal maxValue = 0;

                decimal averageProfitFactor = 0;

                List<OptimizerFazeReport> outOfSampleReports = new List<OptimizerFazeReport>();

                for (int i = 0; i < _reports.Count; i += 2)
                {
                    // берём из ИнСампле таблицу роботов
                    List<OptimizerReport> bots = _reports[i].Reports;

                    OptimizerReport bestBot = _reports[i].Reports[0];

                    // находим этого робота в аутОфСемпл

                    if (i + 1 == _reports.Count)
                    {
                        break;
                    }

                    OptimizerReport bestBotInOutOfSample
                        = _reports[i + 1].Reports.Find(b => b.BotName.Replace(" OutOfSample", "") == bestBot.BotName.Replace(" InSample", ""));

                    if (bestBotInOutOfSample == null)
                    {
                        continue;
                    }

                    outOfSampleReports.Add(_reports[i + 1]);

                    decimal value = bestBotInOutOfSample.ProfitFactor;

                    if (maxValue < value)
                    {
                        maxValue = value;
                    }

                    if (values.Count == 0)
                    {
                        values.Add(value);
                    }
                    else
                    {
                        values.Add(value);
                    }

                    averageProfitFactor += bestBotInOutOfSample.ProfitFactor;
                }
                if (values.Count != 0)
                {
                    averageProfitFactor = averageProfitFactor / values.Count;
                }

                // прорисовка

                Series seriesOosValues = _chartProfitFactor.Series[0];
                Series seriesAverageLine = _chartProfitFactor.Series[1];
                Series seriesAveragePoint = _chartProfitFactor.Series[2];

                seriesOosValues.Points.ClearFast();
                seriesAverageLine.Points.ClearFast();
                seriesAveragePoint.Points.ClearFast();

                if (values.Count == 0)
                {
                    return;
                }

                decimal max = decimal.MinValue;
                decimal min = decimal.MaxValue;

                for (int i = 0; i < values.Count; i++)
                {
                    seriesOosValues.Points.AddXY(i + 1, values[i]);

                    if (values[i] > max)
                    {
                        max = values[i];
                    }

                    if (values[i] < min)
                    {
                        min = values[i];
                    }

                    if (values[i] > 0)
                    {
                        seriesOosValues.Points[seriesOosValues.Points.Count - 1].Color = Color.DarkGreen;
                        seriesOosValues.Points[seriesOosValues.Points.Count - 1].BorderColor = Color.DarkGreen;
                        seriesOosValues.Points[seriesOosValues.Points.Count - 1].BackSecondaryColor = Color.DarkGreen;
                    }
                    else
                    {
                        seriesOosValues.Points[seriesOosValues.Points.Count - 1].Color = Color.DarkRed;
                        seriesOosValues.Points[seriesOosValues.Points.Count - 1].BorderColor = Color.DarkRed;
                        seriesOosValues.Points[seriesOosValues.Points.Count - 1].BackSecondaryColor = Color.DarkRed;
                    }

                    string toolTip = "";

                    toolTip = "OOS " + (i + 1) + "\n" +
                        "start: " + outOfSampleReports[i].Faze.TimeStart.ToString(OsLocalization.ShortDateFormatString) + "\n" +
                         "end: " + outOfSampleReports[i].Faze.TimeEnd.ToString(OsLocalization.ShortDateFormatString) + "\n" +
                         "Profit Factor: " + Math.Round(values[i], 4).ToStringWithNoEndZero();

                    seriesOosValues.Points[seriesOosValues.Points.Count - 1].ToolTip = toolTip;
                }

                if (averageProfitFactor != 0)
                {
                    seriesAverageLine.Points.AddXY(1, averageProfitFactor);

                    seriesAverageLine.Points.AddXY(values.Count, averageProfitFactor);

                    for (int i = 0; i < seriesAverageLine.Points.Count; i++)
                    {
                        seriesAverageLine.Points[i].Color = Color.AntiqueWhite;
                    }

                    string label = "Average: " + Math.Round(averageProfitFactor, 4).ToStringWithNoEndZero();
                    seriesAveragePoint.Points.AddXY(values.Count - 1, maxValue + maxValue * 0.05m);
                    seriesAveragePoint.Points[0].Color = Color.AntiqueWhite;

                    seriesAveragePoint.Points[0].Label = label;
                    seriesAveragePoint.Points[0].LabelForeColor = Color.AntiqueWhite;
                }

                if (max != decimal.MinValue &&
                    min != decimal.MaxValue)
                {
                    max = Math.Round(max + max * 0.2m, 4);
                    min = Math.Round(min, 4);

                    if (max > min)
                    {
                        _chartProfitFactor.ChartAreas[0].AxisY.Maximum = Convert.ToDouble(max);
                        _chartProfitFactor.ChartAreas[0].AxisY.Minimum = Convert.ToDouble(min);
                    }
                }
            }
            catch (Exception ex)
            {
                SendLogMessage(ex.ToString(), LogMessageType.Error);
            }
        }

        #endregion

        #region Log

        private void SendLogMessage(string message, LogMessageType type)
        {
            if (LogMessageEvent != null)
            {
                LogMessageEvent(message, type);
            }
        }

        public event Action<string, LogMessageType> LogMessageEvent;

        #endregion

    }
}