using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using System.Windows.Forms.DataVisualization.Charting;
using System.Windows.Forms.Integration;
using OsEngine.Entity;
using OsEngine.Logging;
using OsEngine.OsOptimizer.OptEntity;

namespace OsEngine.OsOptimizer
{
    public class OptimizerReportCharting
    {
        public OptimizerReportCharting(WindowsFormsHost hostDataGrid, WindowsFormsHost hostColumnsResult,
            WindowsFormsHost hostPieChartResult, System.Windows.Controls.ComboBox boxTypeSort, 
            WindowsFormsHost hostOutOfSampleEquity, System.Windows.Controls.Label outOfSampleLabel)
        {
            _sortBotsType = SortBotsType.TotalProfit;

            _hostDataGrid = hostDataGrid;
            _hostColumnsResult = hostColumnsResult;
            _hostPieChartResult = hostPieChartResult;

            _windowsFormsHostOutOfSampleEquity = hostOutOfSampleEquity;
            _outOfSampleLabel = outOfSampleLabel;

            boxTypeSort.Items.Add(SortBotsType.PositionCount.ToString());
            boxTypeSort.Items.Add(SortBotsType.TotalProfit.ToString());
            boxTypeSort.Items.Add(SortBotsType.MaxDrowDawn.ToString());
            boxTypeSort.Items.Add(SortBotsType.AverageProfit.ToString());
            boxTypeSort.Items.Add(SortBotsType.AverageProfitPercent.ToString());
            boxTypeSort.Items.Add(SortBotsType.ProfitFactor.ToString());
            boxTypeSort.Items.Add(SortBotsType.PayOffRatio.ToString());
            boxTypeSort.Items.Add(SortBotsType.Recovery.ToString());

            boxTypeSort.SelectedItem = SortBotsType.TotalProfit.ToString();
            boxTypeSort.SelectionChanged += _gridResults_SelectionChanged;

            _boxTypeSort = boxTypeSort;

            CreateGridDep();
            CreateColumns();
            CreatePie();
            PaintOutOfSampleEquityChart();
        }

        private System.Windows.Controls.ComboBox _boxTypeSort;

        void _gridResults_SelectionChanged(object sender, EventArgs e)
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
                _sortBotsType = SortBotsType.MaxDrowDawn;
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
            else
            {
                return;
            }

            if (_reports != null)
            {
                ReLoad(_reports);
            }
        }

        public void ReLoad(List<OptimazerFazeReport> reports)
        {
            try
            {
                _reports = reports;

                if (_reports.Count <= 1)
                {
                    return;
                }

                for (int i = 0; i < reports.Count; i++)
                {
                    SortResults(reports[i].Reports);
                }

                UpdGridDep();
                UpdateColumns();
                UpdatePie();
                PaintOutOfSampleEquityChart();
            }
            catch (Exception e)
            {
                SendLogMessage(e.ToString(), LogMessageType.Error);
            }
        }

        private void SortResults(List<OptimizerReport> reports)
        {
            for (int i = 0; i < reports.Count; i++)
            {
                for (int i2 = 0; i2 < reports.Count - 1; i2++)
                {
                    if (FirstLessSecond(reports[i2], reports[i2 + 1], _sortBotsType))
                    {
                        // сортировка пузыриком // фаталити https://youtu.be/LOh_J0Dah7c?t=30
                        OptimizerReport glass = reports[i2];
                        reports[i2] = reports[i2 + 1];
                        reports[i2 + 1] = glass;
                    }
                }
            }
        }

        private bool FirstLessSecond(OptimizerReport rep1, OptimizerReport rep2, SortBotsType sortType)
        {
            if (sortType == SortBotsType.TotalProfit &&
                rep1.TotalProfit < rep2.TotalProfit)
            {
                return true;
            }
            else if (sortType == SortBotsType.PositionCount &&
                     rep1.PositionsCount < rep2.PositionsCount)
            {
                return true;
            }
            else if (sortType == SortBotsType.MaxDrowDawn &&
                     rep1.MaxDrowDawn < rep2.MaxDrowDawn)
            {
                return true;
            }
            else if (sortType == SortBotsType.AverageProfit &&
                     rep1.AverageProfit < rep2.AverageProfit)
            {
                return true;
            }
            else if (sortType == SortBotsType.AverageProfitPercent &&
                     rep1.AverageProfitPercent < rep2.AverageProfitPercent)
            {
                return true;
            }
            else if (sortType == SortBotsType.ProfitFactor &&
                     rep1.ProfitFactor < rep2.ProfitFactor)
            {
                return true;
            }
            else if (sortType == SortBotsType.PayOffRatio &&
                     rep1.PayOffRatio < rep2.PayOffRatio)
            {
                return true;
            }
            else if (sortType == SortBotsType.Recovery &&
                     rep1.Recovery < rep2.Recovery)
            {
                return true;
            }

            return false;
        }

        /// <summary>
        /// robot sorting type in the results table
        /// тип сортировки роботов в таблице результатов
        /// </summary>
        private SortBotsType _sortBotsType;

        private List<OptimazerFazeReport> _reports;
        private WindowsFormsHost _hostDataGrid;
        private WindowsFormsHost _hostColumnsResult;
        private WindowsFormsHost _hostPieChartResult;

        // таблица

        private DataGridView _gridDep;

        private void CreateGridDep()
        {
            _gridDep = DataGridFactory.GetDataGridView(DataGridViewSelectionMode.ColumnHeaderSelect, DataGridViewAutoSizeRowsMode.None,true);
            _gridDep.ScrollBars = ScrollBars.Vertical;

            DataGridViewTextBoxCell cell0 = new DataGridViewTextBoxCell();
            cell0.Style = _gridDep.DefaultCellStyle;

            DataGridViewColumn column0 = new DataGridViewColumn();
            column0.CellTemplate = cell0;
            column0.HeaderText = "Period";
            column0.ReadOnly = true;
            column0.Width = 80;

            _gridDep.Columns.Add(column0);

            DataGridViewColumn column1 = new DataGridViewColumn();
            column1.CellTemplate = cell0;
            column1.HeaderText = "Start";
            column1.ReadOnly = false;
            column1.Width = 80;
            _gridDep.Columns.Add(column1);

            DataGridViewColumn column21 = new DataGridViewColumn();
            column21.CellTemplate = cell0;
            column21.HeaderText = "End";
            column21.ReadOnly = false;
            column21.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            _gridDep.Columns.Add(column21);

            DataGridViewColumn column2 = new DataGridViewColumn();
            column2.CellTemplate = cell0;
            column2.HeaderText = "Best bot number InSample";
            column2.ReadOnly = false;
            column2.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            _gridDep.Columns.Add(column2);

            DataGridViewColumn column3 = new DataGridViewColumn();
            column3.CellTemplate = cell0;
            column3.HeaderText = "Parameters";
            column3.ReadOnly = false;
            column3.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            _gridDep.Columns.Add(column3);

            DataGridViewColumn column4 = new DataGridViewColumn();
            column4.CellTemplate = cell0;
            column4.HeaderText = "Bot results in OutOfSample";
            column4.ReadOnly = false;
            column4.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            _gridDep.Columns.Add(column4);

            DataGridViewColumn column5 = new DataGridViewColumn();
            column5.CellTemplate = cell0;
            column5.HeaderText = "Profit";
            column5.ReadOnly = false;
            column5.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            _gridDep.Columns.Add(column5);

            DataGridViewColumn column6 = new DataGridViewColumn();
            column6.CellTemplate = cell0;
            column6.HeaderText = "Average profit %";
            column6.ReadOnly = false;
            column6.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            _gridDep.Columns.Add(column6);

            DataGridViewColumn column7 = new DataGridViewColumn();
            column7.CellTemplate = cell0;
            column7.HeaderText = "Position count";
            column7.ReadOnly = false;
            column7.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            _gridDep.Columns.Add(column7);

            _gridDep.Rows.Add(null, null);

            _hostDataGrid.Child = _gridDep;
        }

        private void UpdGridDep()
        {
            if (_gridDep.InvokeRequired)
            {
                _gridDep.Invoke(new Action(UpdGridDep));
                return;
            }

            _gridDep.Rows.Clear();

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

            int num = 0;

            OptimizerReport inSampleReport = null;

            for (int i = 0; i < _reports.Count; i++)
            {
                OptimazerFazeReport curReport = _reports[i];

                if (curReport == null ||
                    curReport.Reports == null ||
                    curReport.Reports.Count == 0)
                {
                    continue;
                }

                if (curReport.Faze.TypeFaze == OptimizerFazeType.InSample)
                {
                    inSampleReport = curReport.Reports[0];
                }

                OptimizerReport reportToPaint;

                if (curReport.Faze.TypeFaze == OptimizerFazeType.InSample)
                {
                    reportToPaint = curReport.Reports[0];
                }
                else // if(curReport.Faze.TypeFaze == OptimizerFazeType.OutOfSample)
                {
                    string botName = inSampleReport.BotName.Replace(" InSample", "");
                    reportToPaint = curReport.Reports.Find(rep => rep.BotName.StartsWith(botName));
                }

                if (reportToPaint == null)
                {
                    continue;
                }

                DataGridViewRow row = new DataGridViewRow();
                row.Cells.Add(new DataGridViewTextBoxCell());
                row.Cells[0].Value = curReport.Faze.TypeFaze.ToString();


                DataGridViewTextBoxCell cell2 = new DataGridViewTextBoxCell();
                cell2.Value = curReport.Faze.TimeStart.ToShortDateString();
                row.Cells.Add(cell2);

                DataGridViewTextBoxCell cell3 = new DataGridViewTextBoxCell();
                cell3.Value = curReport.Faze.TimeEnd.ToShortDateString();
                row.Cells.Add(cell3);

                DataGridViewTextBoxCell cell4 = new DataGridViewTextBoxCell();

                if (curReport.Faze.TypeFaze == OptimizerFazeType.InSample)
                {
                    cell4.Value = inSampleReport.BotName.Replace(" InSample", "");
                }
                row.Cells.Add(cell4);

                DataGridViewTextBoxCell cell5 = new DataGridViewTextBoxCell();
                cell5.Value = reportToPaint.GetParamsToDataTable();
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
                cell8.Value = Math.Round(reportToPaint.AverageProfitPercent,4).ToStringWithNoEndZero();
                row.Cells.Add(cell8);

                DataGridViewTextBoxCell cell9 = new DataGridViewTextBoxCell();
                cell9.Value = reportToPaint.PositionsCount.ToString();
                row.Cells.Add(cell9);

                _gridDep.Rows.Add(row);
            }
        }

        // столбики

        private Chart _chart;

        private void CreateColumns()
        {
            _chart = new Chart();

            ChartArea area = new ChartArea("Prime");

            _chart.ChartAreas.Clear();
            _chart.ChartAreas.Add(area);
            _chart.BackColor = Color.FromArgb(21, 26, 30);
            _chart.ChartAreas[0].AxisX.TitleForeColor = Color.FromArgb(149, 159, 176);

            for (int i = 0; _chart.ChartAreas != null && i < _chart.ChartAreas.Count; i++)
            {
                _chart.ChartAreas[i].BackColor = Color.FromArgb(21, 26, 30);
                _chart.ChartAreas[i].BorderColor = Color.FromArgb(17, 18, 23);
                _chart.ChartAreas[i].CursorY.LineColor = Color.FromArgb(149, 159, 176);
                _chart.ChartAreas[i].CursorX.LineColor = Color.FromArgb(149, 159, 176);

                foreach (var axe in _chart.ChartAreas[i].Axes)
                {
                    axe.LabelStyle.ForeColor = Color.FromArgb(149, 159, 176);
                }
            }

            Series series = new Series();
            series.ChartType = SeriesChartType.Column;
            _chart.Series.Clear();
            _chart.Series.Add(series);

            _hostColumnsResult.Child = _chart;
        }

        private void UpdateColumns()
        {
            if (_gridDep.InvokeRequired)
            {
                _gridDep.Invoke(new Action(UpdateColumns));
                return;
            }

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

            int num = 0;

            OptimizerReport inSampleReport = null;

            for (int i = 0; i < _reports.Count; i++)
            {
                OptimazerFazeReport curReport = _reports[i];

                if (curReport == null ||
                    curReport.Reports == null ||
                    curReport.Reports.Count == 0)
                {
                    continue;
                }

                if (curReport.Faze.TypeFaze == OptimizerFazeType.InSample)
                {
                    inSampleReport = curReport.Reports[0];
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
                            }
                            else if (botNum > 20 && botNum <= 40)
                            {
                                count20_40 += 1;
                            }
                            else if (botNum > 40 && botNum <= 60)
                            {
                                count40_60 += 1;
                            }
                            else if (botNum > 60 && botNum <= 80)
                            {
                                count60_80 += 1;
                            }
                            else if (botNum > 80)
                            {
                                countWorst20 += 1;
                            }

                            break;
                        }
                    }
                }
            }

            /* int countBestTwenty = 0;
             int count20_40 = 0;
             int count40_60 = 0;
             int count60_80 = 0;
             int countWorst20 = 0;*/

            _chart.Series[0].Points.Clear();

            DataPoint point1 = new DataPoint(1, countBestTwenty);
            point1.AxisLabel = "Best 20%";
            point1.Color = Color.FromArgb(57, 157, 54);

            DataPoint point2 = new DataPoint(2, count20_40);
            point2.AxisLabel = "20 - 40 %";
            point2.Color = Color.FromArgb(57, 157, 54);

            DataPoint point3 = new DataPoint(3, count40_60);
            point3.AxisLabel = "40 - 60 %";
            point3.Color = Color.FromArgb(149, 159, 176);

            DataPoint point4 = new DataPoint(4, count60_80);
            point4.AxisLabel = "60 - 80 %";
            point4.Color = Color.FromArgb(255, 83, 0);

            DataPoint point5 = new DataPoint(5, countWorst20);
            point5.AxisLabel = "Worst 20 %";
            point5.Color = Color.FromArgb(255, 83, 0);

            _chart.Series[0].Points.Add(point1);
            _chart.Series[0].Points.Add(point2);
            _chart.Series[0].Points.Add(point3);
            _chart.Series[0].Points.Add(point4);
            _chart.Series[0].Points.Add(point5);
        }

        // пирог

        private Chart _chartPie;

        private void CreatePie()
        {
            _chartPie = new Chart();

            ChartArea area = new ChartArea("Prime");

            _chartPie.ChartAreas.Clear();
            _chartPie.ChartAreas.Add(area);
            _chartPie.BackColor = Color.FromArgb(21, 26, 30);
            _chartPie.ChartAreas[0].AxisX.TitleForeColor = Color.FromArgb(149, 159, 176);

            for (int i = 0; _chartPie.ChartAreas != null && i < _chartPie.ChartAreas.Count; i++)
            {
                _chartPie.ChartAreas[i].BackColor = Color.FromArgb(21, 26, 30);
                _chartPie.ChartAreas[i].BorderColor = Color.FromArgb(17, 18, 23);
                _chartPie.ChartAreas[i].CursorY.LineColor = Color.FromArgb(149, 159, 176);
                _chartPie.ChartAreas[i].CursorX.LineColor = Color.FromArgb(149, 159, 176);

                foreach (var axe in _chartPie.ChartAreas[i].Axes)
                {
                    axe.LabelStyle.ForeColor = Color.FromArgb(149, 159, 176);
                }
            }

            Series series = new Series();
            series.ChartType = SeriesChartType.Pie;
            _chartPie.Series.Clear();
            _chartPie.Series.Add(series);

            _hostPieChartResult.Child = _chartPie;
        }

        private void UpdatePie()
        {
            if (_gridDep.InvokeRequired)
            {
                _gridDep.Invoke(new Action(UpdatePie));
                return;
            }

            int countProfitBots = 0;
            int countLossBots = 0;

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

            int num = 0;

            OptimizerReport inSampleReport = null;

            for (int i = 0; i < _reports.Count; i++)
            {
                OptimazerFazeReport curReport = _reports[i];

                if (curReport == null ||
                    curReport.Reports == null ||
                    curReport.Reports.Count == 0)
                {
                    continue;
                }

                if (curReport.Faze.TypeFaze == OptimizerFazeType.InSample)
                {
                    inSampleReport = curReport.Reports[0];
                }

                if (curReport.Faze.TypeFaze == OptimizerFazeType.OutOfSample)
                {
                    string botName = inSampleReport.BotName.Replace(" InSample", "");
                    // reportToPaint = curReport.Reports.Find(rep => rep.BotName.StartsWith(botName));

                    for (int i2 = 0; i2 < curReport.Reports.Count; i2++)
                    {
                        if (curReport.Reports[i2].BotName.StartsWith(botName))
                        {

                            if (curReport.Reports[i2].TotalProfit > 0)
                            {
                                countProfitBots += 1;
                            }
                            else
                            {
                                countLossBots += 1;
                            }

                            break;
                        }
                    }
                }
            }

            if (countProfitBots + countLossBots == 0)
            {
                return;
            }

            decimal profitPercent = Math.Round((Convert.ToDecimal(countProfitBots) / (countProfitBots + countLossBots) * 100), 0);

            decimal lossPercent = Math.Round((Convert.ToDecimal(countLossBots) / (countProfitBots + countLossBots) * 100), 0);

            _chartPie.Series[0].Points.Clear();

            DataPoint point1 = new DataPoint(1, countProfitBots);
            point1.AxisLabel = "Profit " + profitPercent + " %";
            point1.Color = Color.FromArgb(57, 157, 54);
            _chartPie.Series[0].Points.Add(point1);

            if (countLossBots != 0)
            {
                DataPoint point2 = new DataPoint(2, countLossBots);
                point2.AxisLabel = "Loss " + lossPercent + " %";
                point2.Color = Color.FromArgb(255, 83, 0);
                _chartPie.Series[0].Points.Add(point2);
            }
        }

        //

        WindowsFormsHost _windowsFormsHostOutOfSampleEquity;
        System.Windows.Controls.Label _outOfSampleLabel;

        private void PaintOutOfSampleEquityChart()
        {
            if(_reports == null ||
                _reports.Count == 0)
            {
                return;
            }
            if (_windowsFormsHostOutOfSampleEquity == null)
            {
                return;
            }
            List<decimal> values = new List<decimal>();

            decimal averageProfitPercent = 0;
            

            for (int i = 0; i < _reports.Count; i += 2)
            {
                // берём из ИнСампле таблицу роботов
                SortResults(_reports[i].Reports);
                List<OptimizerReport> bots = _reports[i].Reports;

                OptimizerReport bestBot = _reports[i].Reports[0];

                // находим этого робота в аутОфСемпл

                if (i + 1 == _reports.Count)
                {
                    break;
                }

                OptimizerReport bestBotInOutOfSample
                    = _reports[i + 1].Reports.Find(b => b.BotName.Replace(" OutOfSample", "") == bestBot.BotName.Replace(" InSample", ""));

                decimal value = bestBotInOutOfSample.TotalProfitPersent;

                if (values.Count == 0)
                {
                    values.Add(value);
                }
                else
                {
                    values.Add(value + values[values.Count - 1]);
                }

                averageProfitPercent += bestBotInOutOfSample.AverageProfitPercent;
            }
            if(values.Count != 0)
            {
                averageProfitPercent = averageProfitPercent / values.Count;
            }
            

            ChartPainterLine.Paint(_windowsFormsHostOutOfSampleEquity, values);

            _outOfSampleLabel.Content = _outOfSampleLabel.Content.ToString().Split('(')[0]  + 
                "( Total: " + Math.Round(values[values.Count-1], 4) + ". Average: " + Math.Round(averageProfitPercent,4) + " )" ;
        }

        // логирование

        // logging/логирование

        /// <summary>
        /// send up a new message
        /// выслать наверх новое сообщение
        /// </summary>
        /// <param name="message">Message text/текст сообщения</param>
        /// <param name="type">message type/тип сообщения</param>
        private void SendLogMessage(string message, LogMessageType type)
        {
            if (LogMessageEvent != null)
            {
                LogMessageEvent(message, type);
            }
        }

        /// <summary>
        /// event: new message for log
        /// событие: новое сообщение для лога
        /// </summary>
        public event Action<string, LogMessageType> LogMessageEvent;

    }
}
