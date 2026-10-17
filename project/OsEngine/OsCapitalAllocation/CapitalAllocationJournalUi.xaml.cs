using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using OsEngine.Entity;
using OsEngine.Journal.Internal;
using OsEngine.Language;
using OsEngine.Layout;
using OsEngine.Logging;
using OsEngine.Market;
using Chart = System.Windows.Forms.DataVisualization.Charting.Chart;
using ChartArea = System.Windows.Forms.DataVisualization.Charting.ChartArea;
using Series = System.Windows.Forms.DataVisualization.Charting.Series;
using SeriesChartType = System.Windows.Forms.DataVisualization.Charting.SeriesChartType;
using AxisEnabled = System.Windows.Forms.DataVisualization.Charting.AxisEnabled;
using AxisType = System.Windows.Forms.DataVisualization.Charting.AxisType;
using DataPoint = System.Windows.Forms.DataVisualization.Charting.DataPoint;
using Forms = System.Windows.Forms;

namespace OsEngine.OsCapitalAllocation
{
    public partial class CapitalAllocationJournalUi : Window
    {
        private List<Position> _positions;

        private Chart _chartEquity;

        private Forms.DataGridView _gridStatistics;

        private Forms.DataGridView _gridPositions;

        public CapitalAllocationJournalUi(List<Position> positions, int clusterNumber)
        {
            InitializeComponent();
            StickyBorders.Listen(this);
            StartupLocation.Start_MouseInCentre(this);

            Title = OsLocalization.CapitalAllocation.JournalTitle + " "
                + OsLocalization.CapitalAllocation.ClusterPrefix + " " + clusterNumber;

            _positions = positions;

            SetLocalization();

            CreateEquityChart();
            CreateStatisticsGrid();
            CreatePositionsGrid();

            PaintEquity();
            PaintStatistics();
            PaintPositions();

            ButtonHelpEquity.Click += ButtonHelp_Click;
            ButtonHelpStatistics.Click += ButtonHelp_Click;
            ButtonHelpClosedPositions.Click += ButtonHelp_Click;

            Closed += CapitalAllocationJournalUi_Closed;
        }

        private void SetLocalization()
        {
            TabItem tabEquity = (TabItem)TabControlPrime.Items[0];
            TabItem tabStatistics = (TabItem)TabControlPrime.Items[1];
            TabItem tabPositions = (TabItem)TabControlPrime.Items[2];

            tabEquity.Header = OsLocalization.CapitalAllocation.TabEquity;
            tabStatistics.Header = OsLocalization.CapitalAllocation.TabStatistics;
            tabPositions.Header = OsLocalization.CapitalAllocation.TabClosedPositions;

            ButtonHelpEquity.Tag = OsLocalization.CapitalAllocation.HelpJournalEquity;
            ButtonHelpStatistics.Tag = OsLocalization.CapitalAllocation.HelpJournalStatistics;
            ButtonHelpClosedPositions.Tag = OsLocalization.CapitalAllocation.HelpJournalClosedPositions;
        }

        private void CapitalAllocationJournalUi_Closed(object sender, EventArgs e)
        {
            try
            {
                if (_chartEquity != null)
                {
                    _chartEquity.Click -= _chartEquity_Click;
                    _chartEquity.Series.Clear();
                    _chartEquity.ChartAreas.Clear();
                    _chartEquity.Dispose();
                    _chartEquity = null;
                    HostEquity.Child = null;
                }

                if (_gridStatistics != null)
                {
                    DataGridFactory.ClearLinks(_gridStatistics);
                    _gridStatistics.DataError -= _gridStatistics_DataError;
                    _gridStatistics.Rows.Clear();
                    _gridStatistics.Columns.Clear();
                    _gridStatistics.Dispose();
                    _gridStatistics = null;
                    HostStatistics.Child = null;
                }

                if (_gridPositions != null)
                {
                    DataGridFactory.ClearLinks(_gridPositions);
                    _gridPositions.DataError -= _gridStatistics_DataError;
                    _gridPositions.Rows.Clear();
                    _gridPositions.Columns.Clear();
                    _gridPositions.Dispose();
                    _gridPositions = null;
                    HostPositions.Child = null;
                }

                ButtonHelpEquity.Click -= ButtonHelp_Click;
                ButtonHelpStatistics.Click -= ButtonHelp_Click;
                ButtonHelpClosedPositions.Click -= ButtonHelp_Click;

                Closed -= CapitalAllocationJournalUi_Closed;
            }
            catch (Exception error)
            {
                ServerMaster.SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        #region Equity chart

        private void CreateEquityChart()
        {
            _chartEquity = new Chart();
            _chartEquity.Click += _chartEquity_Click;
            HostEquity.Child = _chartEquity;
        }

        private void PaintEquity()
        {
            _chartEquity.Series.Clear();
            _chartEquity.ChartAreas.Clear();

            _chartEquity.BackColor = Themes.ThemeManager.GetColorWinForms("JournalChartBackColor");

            ChartArea area = new ChartArea("Area");
            area.BackColor = Themes.ThemeManager.GetColorWinForms("JournalChartBackColor");
            area.BorderColor = Themes.ThemeManager.GetColorWinForms("JournalChartBorderColor");
            area.AxisX.LabelStyle.ForeColor = Themes.ThemeManager.GetColorWinForms("JournalChartTextColor");
            area.AxisX.LabelStyle.Font = Themes.ThemeManager.GetChartAxisFont();
            area.AxisY.Enabled = AxisEnabled.False;
            area.AxisY2.Enabled = AxisEnabled.True;
            area.AxisY2.LabelStyle.ForeColor = Themes.ThemeManager.GetColorWinForms("JournalChartTextColor");
            area.AxisY2.LabelStyle.Font = Themes.ThemeManager.GetChartAxisFont();
            area.AxisY2.TitleForeColor = Themes.ThemeManager.GetColorWinForms("JournalChartTextColor");
            area.AxisY2.Title = OsLocalization.CapitalAllocation.LabelProfit;
            area.CursorX.IsUserEnabled = true;
            _chartEquity.ChartAreas.Add(area);

            Series equity = new Series("Equity");
            equity.ChartType = SeriesChartType.Line;
            equity.Color = Themes.ThemeManager.GetColorWinForms("JournalEquityTotalColor");
            equity.BorderWidth = 2;
            equity.ChartArea = "Area";
            equity.YAxisType = AxisType.Secondary;

            Series bars = new Series("Bars");
            bars.ChartType = SeriesChartType.Column;
            bars.ChartArea = "Area";
            bars.YAxisType = AxisType.Secondary;

            decimal sum = 0;

            for (int i = 0; i < _positions.Count; i++)
            {
                Position position = _positions[i];
                decimal profit = position.ProfitPortfolioAbs;
                sum += profit;

                equity.Points.AddXY(i, (double)Math.Round(sum, 3));
                equity.Points[i].AxisLabel = position.TimeCreate.ToString("dd.MM.yyyy HH:mm");

                bars.Points.AddXY(i, (double)Math.Round(profit, 3));
                bars.Points[i].Color = profit > 0
                    ? Themes.ThemeManager.GetColorWinForms("JournalBarPlusColor")
                    : Themes.ThemeManager.GetColorWinForms("ChartBarMinusColor");
            }

            _chartEquity.Series.Add(equity);
            _chartEquity.Series.Add(bars);
        }

        private void _chartEquity_Click(object sender, EventArgs e)
        {
            try
            {
                if (_chartEquity.ChartAreas.Count == 0)
                {
                    return;
                }

                ChartArea area = _chartEquity.ChartAreas[0];

                if (double.IsNaN(area.CursorX.Position))
                {
                    return;
                }

                int index = Convert.ToInt32(area.CursorX.Position);

                Series equity = _chartEquity.Series.FindByName("Equity");
                Series bars = _chartEquity.Series.FindByName("Bars");

                if (equity != null)
                {
                    for (int i = 0; i < equity.Points.Count; i++)
                    {
                        equity.Points[i].Label = "";
                    }

                    if (index >= 0 && index < equity.Points.Count)
                    {
                        DataPoint equityPoint = equity.Points[index];
                        equityPoint.Label = equityPoint.AxisLabel + "\n" + equityPoint.YValues[0];
                        equityPoint.LabelForeColor = equityPoint.Color;
                        equityPoint.LabelBackColor = _chartEquity.BackColor;
                    }
                }

                if (bars != null && equity != null && index >= 0
                    && index < bars.Points.Count && index < equity.Points.Count)
                {
                    for (int i = 0; i < bars.Points.Count; i++)
                    {
                        bars.Points[i].Label = "";
                    }

                    DataPoint barPoint = bars.Points[index];
                    barPoint.Label = equity.Points[index].AxisLabel + "\n" + barPoint.YValues[0];
                    barPoint.LabelForeColor = barPoint.Color;
                    barPoint.LabelBackColor = _chartEquity.BackColor;
                }
            }
            catch (Exception error)
            {
                ServerMaster.SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        #endregion

        #region Statistics

        private void CreateStatisticsGrid()
        {
            _gridStatistics = DataGridFactory.GetDataGridView(Forms.DataGridViewSelectionMode.FullRowSelect,
                Forms.DataGridViewAutoSizeRowsMode.None);
            _gridStatistics.AllowUserToResizeRows = false;
            _gridStatistics.DataError += _gridStatistics_DataError;
            HostStatistics.Child = _gridStatistics;

            Forms.DataGridViewTextBoxCell cell = new Forms.DataGridViewTextBoxCell();
            cell.Style = _gridStatistics.DefaultCellStyle;

            Forms.DataGridViewColumn columnLeft = new Forms.DataGridViewColumn();
            columnLeft.CellTemplate = cell;
            columnLeft.HeaderText = "";
            columnLeft.ReadOnly = true;
            columnLeft.AutoSizeMode = Forms.DataGridViewAutoSizeColumnMode.Fill;
            _gridStatistics.Columns.Add(columnLeft);

            Forms.DataGridViewColumn columnName = new Forms.DataGridViewColumn();
            columnName.CellTemplate = cell;
            columnName.HeaderText = "";
            columnName.ReadOnly = true;
            columnName.AutoSizeMode = Forms.DataGridViewAutoSizeColumnMode.AllCells;
            _gridStatistics.Columns.Add(columnName);

            Forms.DataGridViewColumn columnValue = new Forms.DataGridViewColumn();
            columnValue.CellTemplate = cell;
            columnValue.HeaderText = OsLocalization.CapitalAllocation.ColumnAll;
            columnValue.ReadOnly = true;
            columnValue.AutoSizeMode = Forms.DataGridViewAutoSizeColumnMode.AllCells;
            _gridStatistics.Columns.Add(columnValue);

            Forms.DataGridViewColumn columnRight = new Forms.DataGridViewColumn();
            columnRight.CellTemplate = cell;
            columnRight.HeaderText = "";
            columnRight.ReadOnly = true;
            columnRight.AutoSizeMode = Forms.DataGridViewAutoSizeColumnMode.Fill;
            _gridStatistics.Columns.Add(columnRight);

            string[] labels = OsLocalization.CapitalAllocation.StatisticsLabels;

            for (int i = 0; i < labels.Length; i++)
            {
                int rowIndex = _gridStatistics.Rows.Add();
                _gridStatistics.Rows[rowIndex].Cells[1].Value = labels[i];
            }
        }

        private void _gridStatistics_DataError(object sender, Forms.DataGridViewDataErrorEventArgs e)
        {
            ServerMaster.SendNewLogMessage(e.ToString(), LogMessageType.Error);
        }

        private void PaintStatistics()
        {
            List<string> statistic = PositionStatisticGenerator.GetStatisticNew(_positions, true);

            if (statistic == null)
            {
                return;
            }

            for (int i = 0; i < 31 && i < statistic.Count; i++)
            {
                _gridStatistics.Rows[i].Cells[2].Value = statistic[i];
            }
        }

        #endregion

        #region Positions

        private void CreatePositionsGrid()
        {
            _gridPositions = DataGridFactory.GetDataGridView(Forms.DataGridViewSelectionMode.FullRowSelect,
                Forms.DataGridViewAutoSizeRowsMode.AllCells);
            _gridPositions.ScrollBars = Forms.ScrollBars.Vertical;
            _gridPositions.DataError += _gridStatistics_DataError;
            HostPositions.Child = _gridPositions;

            AddEmptyFillColumn();
            AddTextColumn(OsLocalization.CapitalAllocation.ColumnTimeOpen);
            AddTextColumn(OsLocalization.CapitalAllocation.ColumnBot);
            AddTextColumn(OsLocalization.CapitalAllocation.ColumnSecurity);
            AddTextColumn(OsLocalization.CapitalAllocation.ColumnDirection);
            AddTextColumn(OsLocalization.CapitalAllocation.ColumnProfitAbs);
            AddTextColumn(OsLocalization.CapitalAllocation.ColumnProfitPercent);
            AddEmptyFillColumn();
        }

        private void AddTextColumn(string header)
        {
            Forms.DataGridViewTextBoxColumn column = new Forms.DataGridViewTextBoxColumn();
            column.HeaderText = header;
            column.ReadOnly = true;
            column.AutoSizeMode = Forms.DataGridViewAutoSizeColumnMode.AllCells;
            _gridPositions.Columns.Add(column);
        }

        private void AddEmptyFillColumn()
        {
            Forms.DataGridViewTextBoxColumn column = new Forms.DataGridViewTextBoxColumn();
            column.HeaderText = "";
            column.ReadOnly = true;
            column.AutoSizeMode = Forms.DataGridViewAutoSizeColumnMode.Fill;
            _gridPositions.Columns.Add(column);
        }

        private void PaintPositions()
        {
            _gridPositions.Rows.Clear();

            for (int i = 0; i < _positions.Count; i++)
            {
                Position position = _positions[i];

                int rowIndex = _gridPositions.Rows.Add();
                _gridPositions.Rows[rowIndex].Cells[1].Value = position.TimeCreate.ToString("dd.MM.yyyy HH:mm");
                _gridPositions.Rows[rowIndex].Cells[2].Value = position.NameBot;
                _gridPositions.Rows[rowIndex].Cells[3].Value = position.SecurityName;
                _gridPositions.Rows[rowIndex].Cells[4].Value = position.Direction.ToString();
                _gridPositions.Rows[rowIndex].Cells[5].Value = Math.Round(position.ProfitPortfolioAbs, 2);
                _gridPositions.Rows[rowIndex].Cells[6].Value = Math.Round(position.ProfitOperationPercent, 2);
            }
        }

        #endregion

        #region Help

        private void ButtonHelp_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (sender is Button button && button.Tag is string text)
                {
                    CustomMessageBoxUi ui = new CustomMessageBoxUi(text);
                    ui.ShowDialog();
                }
            }
            catch (Exception error)
            {
                ServerMaster.SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        #endregion
    }
}
