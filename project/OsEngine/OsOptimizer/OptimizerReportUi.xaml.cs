using OsEngine.Entity;
using OsEngine.Language;
using OsEngine.Market.Servers.Tester;
using OsEngine.OsTrader.Panels;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms;
using OsEngine.Logging;
using OsEngine.Robots;
using MessageBox = System.Windows.MessageBox;
using ProgressBar = System.Windows.Controls.ProgressBar;
using OsEngine.OsOptimizer.OptEntity;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

using System.Windows.Forms.DataVisualization.Charting;
using OsEngine.Market;

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
            _master = master;

            _resultsCharting = new OptimizerReportCharting(
            WindowsFormsHostDependences, WindowsFormsHostColumnsResults,
            WindowsFormsHostPieResults, ComboBoxSortDependencesResults, 
            WindowsFormsHostOutOfSampleEquity, LabelTotalProfitInOutOfSample);

            _resultsCharting.LogMessageEvent += _master.SendLogMessage;
            CreateTableFazes();
            CreateTableResults();


            LabelSortBy.Content = OsLocalization.Optimizer.Label39;
            LabelOptimSeries.Content = OsLocalization.Optimizer.Label30;
            LabelTableResults.Content = OsLocalization.Optimizer.Label31;
            TabControlResultsSeries.Header = OsLocalization.Optimizer.Label37;
            TabControlResultsOutOfSampleResults.Header = OsLocalization.Optimizer.Label38;
            LabelTotalProfitInOutOfSample.Content = OsLocalization.Optimizer.Label43;
            ButtonSaveInFile.Content = OsLocalization.Optimizer.Label45;
            ButtonLoadFromFile.Content = OsLocalization.Optimizer.Label46;
        }

        public void Paint(List<OptimazerFazeReport> reports)
        {
            if(reports == null)
            {
                return;
            }
            _reports = reports;
            RepaintResults();
        }

        OptimizerMaster _master;

        private List<OptimazerFazeReport> _reports;

        private OptimizerReportCharting _resultsCharting;

        private void RepaintResults()
        {
            try
            {
                for (int i = 0; i < _reports.Count; i++)
                {
                    SortResults(_reports[i].Reports);
                }

                PaintTableFazes();
                PaintTableResults();

                _resultsCharting.ReLoad(_reports);
            }
            catch (Exception error)
            {
                _master.SendLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        // phase table for switching after testing/таблица фаз для переключения после тестирования

        /// <summary>
        /// table with optimization steps on the totals tab
        /// таблица с этапами оптимизации на вкладке итогов
        /// </summary>
        private DataGridView _gridFazesEnd;

        /// <summary>
        /// create phase table on totals tabs
        /// создать таблицу фаз на вкладки итогов
        /// </summary>
        private void CreateTableFazes()
        {
            _gridFazesEnd = DataGridFactory.GetDataGridView(DataGridViewSelectionMode.FullRowSelect, DataGridViewAutoSizeRowsMode.None,true);
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
        }

        /// <summary>
        /// draw phase table on totals tab
        /// прорисовать таблицу фаз на вкладке итогов
        /// </summary>
        private void PaintTableFazes()
        {
            if (_gridFazesEnd.InvokeRequired)
            {
                _gridFazesEnd.Invoke(new Action(PaintTableFazes));
                return;
            }

            _gridFazesEnd.Rows.Clear();




            List<OptimizerFaze> fazes = new List<OptimizerFaze>();

            for(int i = 0;i< _reports.Count;i++)
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
                cell2.Value = fazes[i].TimeStart.ToShortDateString();
                row.Cells.Add(cell2);

                DataGridViewTextBoxCell cell3 = new DataGridViewTextBoxCell();
                cell3.Value = fazes[i].TimeEnd.ToShortDateString();
                row.Cells.Add(cell3);

                DataGridViewTextBoxCell cell4 = new DataGridViewTextBoxCell();
                cell4.Value = fazes[i].Days;
                row.Cells.Add(cell4);

                _gridFazesEnd.Rows.Add(row);
            }
        }

        /// <summary>
        /// the user clicked on the phase table in the totals tab
        /// пользователь кликнул по таблице фаз на вкладке итогов
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        void _gridFazesEnd_CellClick(object sender, DataGridViewCellEventArgs e)
        {
            PaintTableResults();
        }

        // optimization results table/таблица результатов оптимизации


        /// <summary>
        /// table with optimization steps
        /// таблица с этапами оптимизации
        /// </summary>
        private DataGridView _gridResults;

        /// <summary>
        /// create a table of results
        /// создать таблицу результатов
        /// </summary>
        private void CreateTableResults()
        {
            _gridResults = DataGridFactory.GetDataGridView(DataGridViewSelectionMode.ColumnHeaderSelect, DataGridViewAutoSizeRowsMode.None,true);
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

            DataGridViewButtonColumn column11 = new DataGridViewButtonColumn();
            column11.CellTemplate = new DataGridViewButtonCell();
            column11.HeaderText = OsLocalization.Optimizer.Message40;
            column11.ReadOnly = true;
            column11.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            _gridResults.Columns.Add(column11);

            _gridResults.Rows.Add(null, null);

            WindowsFormsHostResults.Child = _gridResults;
        }

        private void UpdateHeaders()
        {

            _gridResults.Columns[0].HeaderText = "Bot Name";

            if (_sortBotsType == SortBotsType.BotName)
            {
                _gridResults.Columns[0].HeaderText += " vvv";
            }

            _gridResults.Columns[2].HeaderText = "Pos Count";

            if (_sortBotsType == SortBotsType.PositionCount)
            {
                _gridResults.Columns[2].HeaderText += " vvv";
            }

            _gridResults.Columns[3].HeaderText = "Total Profit";

            if (_sortBotsType == SortBotsType.TotalProfit)
            {
                _gridResults.Columns[3].HeaderText += " vvv";
            }

            _gridResults.Columns[4].HeaderText = "Max Drow Dawn";
            if (_sortBotsType == SortBotsType.MaxDrowDawn)
            {
                _gridResults.Columns[4].HeaderText += " vvv";
            }

            _gridResults.Columns[5].HeaderText = "Average Profit";
            if (_sortBotsType == SortBotsType.AverageProfit)
            {
                _gridResults.Columns[5].HeaderText += " vvv";
            }

            _gridResults.Columns[6].HeaderText = "Average Profit %";
            if (_sortBotsType == SortBotsType.AverageProfitPercent)
            {
                _gridResults.Columns[6].HeaderText += " vvv";
            }

            _gridResults.Columns[7].HeaderText = "Profit Factor";
            if (_sortBotsType == SortBotsType.ProfitFactor)
            {
                _gridResults.Columns[7].HeaderText += " vvv";
            }

            _gridResults.Columns[8].HeaderText = "Pay Off Ratio";
            if (_sortBotsType == SortBotsType.PayOffRatio)
            {
                _gridResults.Columns[8].HeaderText += " vvv";
            }

            _gridResults.Columns[9].HeaderText = "Recovery";
            if (_sortBotsType == SortBotsType.Recovery)
            {
                _gridResults.Columns[9].HeaderText += " vvv";
            }
        }

        /// <summary>
        /// draw a table of results
        /// прорисовать таблицу результатов
        /// </summary>
        private void PaintTableResults()
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

            OptimazerFazeReport fazeReport = _reports[num];

            if (fazeReport == null)
            {
                return;
            }

            for (int i = 0; i < fazeReport.Reports.Count; i++)
            {
                OptimizerReport report = fazeReport.Reports[i];
                if (report == null ||
                    report.TabsReports.Count == 0 ||
                    !_master.IsAcceptedByFilter(report))
                {
                    continue;
                }

                DataGridViewRow row = new DataGridViewRow();
                row.Cells.Add(new DataGridViewTextBoxCell());

                if (report.TabsReports.Count == 1)
                {
                    row.Cells[0].Value = report.BotName;
                }
                else
                {
                    row.Cells[0].Value = "Сводные";
                }

                DataGridViewTextBoxCell cell2 = new DataGridViewTextBoxCell();
                cell2.Value = report.GetParamsToDataTable();
                row.Cells.Add(cell2);

                DataGridViewTextBoxCell cell3 = new DataGridViewTextBoxCell();
                cell3.Value = report.PositionsCount;
                row.Cells.Add(cell3);

                DataGridViewTextBoxCell cell4 = new DataGridViewTextBoxCell();
                cell4.Value = report.TotalProfit.ToStringWithNoEndZero() + " (" + report.TotalProfitPersent.ToStringWithNoEndZero() + "%)";
                row.Cells.Add(cell4);

                DataGridViewTextBoxCell cell5 = new DataGridViewTextBoxCell();
                cell5.Value = report.MaxDrowDawn.ToStringWithNoEndZero();
                row.Cells.Add(cell5);

                DataGridViewTextBoxCell cell6 = new DataGridViewTextBoxCell();
                cell6.Value = report.AverageProfit.ToStringWithNoEndZero();
                row.Cells.Add(cell6);

                DataGridViewTextBoxCell cell7 = new DataGridViewTextBoxCell();
                cell7.Value = report.AverageProfitPercent.ToStringWithNoEndZero();
                row.Cells.Add(cell7);

                DataGridViewTextBoxCell cell8 = new DataGridViewTextBoxCell();
                cell8.Value = report.ProfitFactor.ToStringWithNoEndZero();
                row.Cells.Add(cell8);

                DataGridViewTextBoxCell cell9 = new DataGridViewTextBoxCell();
                cell9.Value = report.PayOffRatio.ToStringWithNoEndZero();
                row.Cells.Add(cell9);

                DataGridViewTextBoxCell cell10 = new DataGridViewTextBoxCell();
                cell10.Value = report.Recovery.ToStringWithNoEndZero();
                row.Cells.Add(cell10);


                DataGridViewButtonCell cell11 = new DataGridViewButtonCell();
                cell11.Value = OsLocalization.Optimizer.Message40;
                row.Cells.Add(cell11);

                _gridResults.Rows.Add(row);

                if (report.TabsReports.Count > 1)
                {
                    for (int i2 = 0; i2 < report.TabsReports.Count; i2++)
                    {
                        _gridResults.Rows.Add(GetRowResult(report.TabsReports[i2]));
                    }
                }
            }

            _gridResults.SelectionChanged += _gridResults_SelectionChanged;
            _gridResults.CellMouseClick += _gridResults_CellMouseClick;
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
            if (sortType == SortBotsType.BotName &&
            Convert.ToInt32(rep1.BotName.Split(' ')[0]) > Convert.ToInt32(rep2.BotName.Split(' ')[0]))
            {
                return true;
            }
            else if (sortType == SortBotsType.TotalProfit &&
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

        private DataGridViewRow GetRowResult(OptimizerReportTab report)
        {
            DataGridViewRow row = new DataGridViewRow();

            row.Cells.Add(new DataGridViewTextBoxCell());
            row.Cells[0].Value = report.SecurityName;


            DataGridViewTextBoxCell cell2 = new DataGridViewTextBoxCell();
            row.Cells.Add(cell2);

            DataGridViewTextBoxCell cell3 = new DataGridViewTextBoxCell();
            cell3.Value = report.PositionsCount;
            row.Cells.Add(cell3);

            DataGridViewTextBoxCell cell4 = new DataGridViewTextBoxCell();
            cell4.Value = report.TotalProfit.ToStringWithNoEndZero();
            row.Cells.Add(cell4);

            DataGridViewTextBoxCell cell5 = new DataGridViewTextBoxCell();
            cell5.Value = report.MaxDrowDawn.ToStringWithNoEndZero();
            row.Cells.Add(cell5);

            DataGridViewTextBoxCell cell6 = new DataGridViewTextBoxCell();
            cell6.Value = report.AverageProfit.ToStringWithNoEndZero();
            row.Cells.Add(cell6);

            DataGridViewTextBoxCell cell7 = new DataGridViewTextBoxCell();
            cell7.Value = report.AverageProfitPercent.ToStringWithNoEndZero();
            row.Cells.Add(cell7);

            DataGridViewTextBoxCell cell8 = new DataGridViewTextBoxCell();
            cell8.Value = report.ProfitFactor.ToStringWithNoEndZero();
            row.Cells.Add(cell8);

            DataGridViewTextBoxCell cell9 = new DataGridViewTextBoxCell();
            cell9.Value = report.PayOffRatio.ToStringWithNoEndZero();
            row.Cells.Add(cell9);

            DataGridViewTextBoxCell cell10 = new DataGridViewTextBoxCell();
            cell10.Value = report.Recovery.ToStringWithNoEndZero();
            row.Cells.Add(cell10);

            row.Cells.Add(null);

            return row;

        }

        /// <summary>
        /// user clicked a button in the result table
        /// пользователь нажал на кнопку в таблице результатов
        /// </summary>
        void _gridResults_CellMouseClick(object sender, DataGridViewCellMouseEventArgs e)
        {
            if (e.RowIndex < 0)
            {
                return;
            }

            if (e.ColumnIndex != 10)
            {
                return;
            }

            OptimazerFazeReport fazeReport;

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

            bot.ShowChartDialog();
        }

        /// <summary>
        /// user clicked results table
        /// пользователь кликнул по таблице результатов
        /// </summary>
        void _gridResults_SelectionChanged(object sender, EventArgs e)
        {
            if (_gridResults.SelectedCells.Count == 0)
            {
                return;
            }
            int columnSelect = _gridResults.SelectedCells[0].ColumnIndex;


            if (columnSelect == 0)
            {
                _sortBotsType = SortBotsType.BotName;
            }
            else if (columnSelect == 2)
            {
                _sortBotsType = SortBotsType.PositionCount;
            }
            else if (columnSelect == 3)
            {
                _sortBotsType = SortBotsType.TotalProfit;
            }
            else if (columnSelect == 4)
            {
                _sortBotsType = SortBotsType.MaxDrowDawn;
            }
            else if (columnSelect == 5)
            {
                _sortBotsType = SortBotsType.AverageProfit;
            }
            else if (columnSelect == 6)
            {
                _sortBotsType = SortBotsType.AverageProfitPercent;
            }
            else if (columnSelect == 7)
            {
                _sortBotsType = SortBotsType.ProfitFactor;
            }
            else if (columnSelect == 8)
            {
                _sortBotsType = SortBotsType.PayOffRatio;
            }
            else if (columnSelect == 9)
            {
                _sortBotsType = SortBotsType.Recovery;
            }
            else
            {
                return;
            }

            try
            {
                for (int i = 0; i < _reports.Count; i++)
                {
                    SortResults(_reports[i].Reports);
                }

                PaintTableResults();
            }
            catch (Exception error)
            {
                _master.SendLogMessage(error.ToString(), LogMessageType.Error);
            }

        }

        /// <summary>
        /// robot sorting type in the results table
        /// тип сортировки роботов в таблице результатов
        /// </summary>
        private SortBotsType _sortBotsType;

        private void ButtonSaveInFile_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                SaveFileDialog myDialog = new SaveFileDialog();

                string saveFileName = _master.StrategyName;
                
                if(_master.TabsSimpleNamesAndTimeFrames != null && _master.TabsSimpleNamesAndTimeFrames.Count != 0)
                {
                    saveFileName += "_" + _master.TabsSimpleNamesAndTimeFrames[0].NameSecurity;
                    saveFileName += "_" + _master.TabsSimpleNamesAndTimeFrames[0].TimeFrame;
                }

                IIStrategyParameter regime = _master._optimizerExecutor._parameters.Find(p => p.Name == "Regime");

                if(regime != null)
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

                string saveStr = "";

                for (int i = 0; i < _reports.Count; i++)
                {
                    saveStr += _reports[i].GetSaveString() + "\r\n";
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
                    _reports = new List<OptimazerFazeReport>();
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

                        OptimazerFazeReport newReport = new OptimazerFazeReport();
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
        
    }
}
