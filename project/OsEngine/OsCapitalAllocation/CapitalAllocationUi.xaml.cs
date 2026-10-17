using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using Microsoft.Win32;
using OsEngine.Entity;
using OsEngine.Language;
using OsEngine.Layout;
using OsEngine.Logging;
using OsEngine.Market;
using OsEngine.OsCapitalAllocation.Algorithms.HierarchicalRiskParity;
using OsEngine.OsCapitalAllocation.Algorithms.CorrelationClustering;
using Forms = System.Windows.Forms;
using OpenFileDialog = Microsoft.Win32.OpenFileDialog;
using OpenFolderDialog = Microsoft.Win32.OpenFolderDialog;
using SaveFileDialog = Microsoft.Win32.SaveFileDialog;
using Chart = System.Windows.Forms.DataVisualization.Charting.Chart;
using ChartArea = System.Windows.Forms.DataVisualization.Charting.ChartArea;
using Series = System.Windows.Forms.DataVisualization.Charting.Series;
using SeriesChartType = System.Windows.Forms.DataVisualization.Charting.SeriesChartType;
using AxisEnabled = System.Windows.Forms.DataVisualization.Charting.AxisEnabled;
using AxisType = System.Windows.Forms.DataVisualization.Charting.AxisType;

namespace OsEngine.OsCapitalAllocation
{
    public partial class CapitalAllocationUi : Window
    {
        private const string _cashFileName = @"Engine\LastSessionFilesList.txt";

        public bool IsDialogMode;

        private CapitalAllocationMaster _master = new CapitalAllocationMaster();

        private Forms.DataGridView _gridBots;

        private Forms.DataGridView _gridResultCorrelationClustering;

        private Forms.DataGridView _gridResultHierarchicalRiskParity;

        private Chart _chartEquity;

        private Dictionary<int, Ellipse> _dendrogramDots = new Dictionary<int, Ellipse>();

        private Dictionary<int, Point> _dendrogramDotPositions = new Dictionary<int, Point>();

        private int[] _hrpClusterOfBot;

        private List<List<int>> _hrpClusters;

        private HierarchicalRiskParityAlgorithm _hrpAlgorithm;

        private int _highlightedBotIndex = -1;

        private Dictionary<int, Ellipse> _graphNodes = new Dictionary<int, Ellipse>();

        private Dictionary<int, Point> _graphPositions = new Dictionary<int, Point>();

        private List<CorrelationCluster> _clusteringClusters;

        private int _highlightedGraphBotIndex = -1;

        public CapitalAllocationMaster Master
        {
            get { return _master; }
        }

        public CapitalAllocationUi(bool isDialogMode = false)
        {
            IsDialogMode = isDialogMode;

            InitializeComponent();
            StickyBorders.Listen(this);
            StartupLocation.Start_MouseInCentre(this);

            ButtonAddFile.Click += ButtonAddFile_Click;
            ButtonAddFolder.Click += ButtonAddFolder_Click;
            ButtonDelete.Click += ButtonDelete_Click;
            ButtonClearAll.Click += ButtonClearAll_Click;
            ButtonRecalculate.Click += ButtonRecalculate_Click;
            ButtonCalculateCorrelationClustering.Click += ButtonCalculateCorrelationClustering_Click;
            ButtonCalculateHierarchicalRiskParity.Click += ButtonCalculateHierarchicalRiskParity_Click;
            ButtonSaveCorrelationClustering.Click += ButtonSaveCorrelationClustering_Click;
            ButtonSaveHierarchicalRiskParity.Click += ButtonSaveHierarchicalRiskParity_Click;
            ButtonHelpDataDownloading.Click += ButtonHelp_Click;
            ButtonHelpBaseEquity.Click += ButtonHelp_Click;
            ButtonHelpCorrelationClustering.Click += ButtonHelp_Click;
            ButtonHelpHierarchicalRiskParity.Click += ButtonHelp_Click;

            ComboBoxPeriod.Items.Add(OsLocalization.CapitalAllocation.PeriodWeek);
            ComboBoxPeriod.Items.Add(OsLocalization.CapitalAllocation.PeriodMonth);
            ComboBoxPeriod.SelectedIndex = 0;

            ComboBoxLinkage.Items.Add("single");
            ComboBoxLinkage.Items.Add("complete");
            ComboBoxLinkage.Items.Add("average");
            ComboBoxLinkage.Items.Add("ward");
            ComboBoxLinkage.SelectedIndex = 0;

            ComboBoxCovariance.Items.Add("sample");
            ComboBoxCovariance.Items.Add("shrinkage");
            ComboBoxCovariance.SelectedIndex = 0;

            ComboBoxCorrelation.Items.Add("Pearson");
            ComboBoxCorrelation.Items.Add("Spearman");
            ComboBoxCorrelation.SelectedIndex = 0;

            TextBoxMinCorrelation.Text = "0,4";

            SetLocalization();

            ComboBoxChartType.Items.Add(OsLocalization.CapitalAllocation.ChartTypeAbsolute);
            ComboBoxChartType.Items.Add(OsLocalization.CapitalAllocation.ChartTypePercent);
            ComboBoxChartType.SelectedIndex = 1;
            ComboBoxChartType.SelectionChanged += ComboBoxChartType_SelectionChanged;

            ComboBoxPeriod.SelectionChanged += ComboBoxPeriod_SelectionChanged;
            DatePickerStart.SelectedDateChanged += DatePickerStart_SelectedDateChanged;
            DatePickerEnd.SelectedDateChanged += DatePickerEnd_SelectedDateChanged;

            CreateBotsGrid();
            CreateResultGrids();
            CreateEquityChart();

            CanvasDendrogram.SizeChanged += CanvasDendrogram_SizeChanged;
            CanvasGraph.SizeChanged += CanvasGraph_SizeChanged;

            CanvasDendrogram.Background = Themes.ThemeManager.GetBrush("ChartBackColor");
            CanvasGraph.Background = Themes.ThemeManager.GetBrush("ChartBackColor");

            if (IsDialogMode == false)
            {
                LoadLastSessionFiles();
            }

            Closing += CapitalAllocationUi_Closing;
            Closed += CapitalAllocationUi_Closed;
        }

        private void SetLocalization()
        {
            Title = OsLocalization.CapitalAllocation.Title;

            TabItem tabIncomingData = (TabItem)TabControlPrime.Items[0];
            TabItem tabCalculations = (TabItem)TabControlPrime.Items[1];

            tabIncomingData.Header = OsLocalization.CapitalAllocation.TabIncomingData;
            tabCalculations.Header = OsLocalization.CapitalAllocation.TabCalculations;

            TabItem tabDataDownloading = (TabItem)TabControlData.Items[0];
            TabItem tabBaseEquity = (TabItem)TabControlData.Items[1];

            tabDataDownloading.Header = OsLocalization.CapitalAllocation.TabDataDownloading;
            tabBaseEquity.Header = OsLocalization.CapitalAllocation.TabBaseEquity;

            TabItem tabCorrelationClustering = (TabItem)TabControlCalculations.Items[0];
            TabItem tabHierarchicalRiskParity = (TabItem)TabControlCalculations.Items[1];

            tabCorrelationClustering.Header = OsLocalization.CapitalAllocation.TabCorrelationClustering;
            tabHierarchicalRiskParity.Header = OsLocalization.CapitalAllocation.TabHierarchicalRiskParity;

            ButtonAddFile.Content = OsLocalization.CapitalAllocation.ButtonAddFile;
            ButtonAddFolder.Content = OsLocalization.CapitalAllocation.ButtonAddFolder;
            ButtonDelete.Content = OsLocalization.CapitalAllocation.ButtonDelete;
            ButtonClearAll.Content = OsLocalization.CapitalAllocation.ButtonClearAll;
            ButtonCalculateCorrelationClustering.Content = OsLocalization.CapitalAllocation.ButtonCalculate;
            ButtonCalculateHierarchicalRiskParity.Content = OsLocalization.CapitalAllocation.ButtonCalculate;
            ButtonSaveCorrelationClustering.Content = OsLocalization.CapitalAllocation.ButtonSaveResult;
            ButtonSaveHierarchicalRiskParity.Content = OsLocalization.CapitalAllocation.ButtonSaveResult;
            ButtonHelpDataDownloading.Tag = OsLocalization.CapitalAllocation.HelpDataDownloading;
            ButtonHelpBaseEquity.Tag = OsLocalization.CapitalAllocation.HelpBaseEquity;
            ButtonHelpCorrelationClustering.Tag = OsLocalization.CapitalAllocation.HelpCorrelationClustering;
            ButtonHelpHierarchicalRiskParity.Tag = OsLocalization.CapitalAllocation.HelpHierarchicalRiskParity;
            ButtonRecalculate.Content = OsLocalization.CapitalAllocation.ButtonRecalculate;

            LabelPrecision.Content = OsLocalization.CapitalAllocation.LabelPrecision;
            LabelMinCorrelation.Content = OsLocalization.CapitalAllocation.LabelMinCorrelation;
            LabelLinkage.Content = OsLocalization.CapitalAllocation.LabelLinkage;
            LabelCovariance.Content = OsLocalization.CapitalAllocation.LabelCovariance;
            LabelCorrelation.Content = OsLocalization.CapitalAllocation.LabelCorrelation;
            LabelFrom.Content = OsLocalization.CapitalAllocation.LabelFrom;
            LabelTo.Content = OsLocalization.CapitalAllocation.LabelTo;
            LabelChartType.Content = OsLocalization.CapitalAllocation.LabelChartType;
        }

        public void AddBots(List<BotFile> bots)
        {
            try
            {
                if (bots == null)
                {
                    return;
                }

                for (int i = 0; i < bots.Count; i++)
                {
                    _master.Bots.Add(bots[i]);
                }

                PaintBotsGrid();
                SetDefaultDateRange();
                OverwriteCash();
            }
            catch (Exception error)
            {
                ServerMaster.SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        private void CapitalAllocationUi_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            try
            {
                if (IsDialogMode)
                {
                    return;
                }

                bool isProgrammaticClose = false;

                if (Application.Current.MainWindow is MainWindow mainWindow)
                {
                    isProgrammaticClose = mainWindow.IsProgrammaticClose;
                }

                if (isProgrammaticClose == false)
                {
                    AcceptDialogUi ui = new AcceptDialogUi(OsLocalization.CapitalAllocation.MessageClose);
                    ui.ShowDialog();

                    if (ui.UserAcceptAction == false)
                    {
                        e.Cancel = true;
                        return;
                    }
                }
            }
            catch (Exception error)
            {
                ServerMaster.SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        private void CapitalAllocationUi_Closed(object sender, EventArgs e)
        {
            try
            {
                if (_gridBots != null)
                {
                    _gridBots.DataError -= _gridBots_DataError;
                    DataGridFactory.ClearLinks(_gridBots);
                    _gridBots.Rows.Clear();
                    _gridBots.Columns.Clear();
                    _gridBots.Dispose();
                    _gridBots = null;
                    HostBots.Child = null;
                }

                if (_gridResultCorrelationClustering != null)
                {
                    _gridResultCorrelationClustering.DataError -= _gridResult_DataError;
                    _gridResultCorrelationClustering.CellContentClick -= _gridResultCorrelationClustering_CellContentClick;
                    _gridResultCorrelationClustering.SelectionChanged -= _gridResultCorrelationClustering_SelectionChanged;
                    DataGridFactory.ClearLinks(_gridResultCorrelationClustering);
                    _gridResultCorrelationClustering.Rows.Clear();
                    _gridResultCorrelationClustering.Columns.Clear();
                    _gridResultCorrelationClustering.Dispose();
                    _gridResultCorrelationClustering = null;
                    HostResultCorrelationClustering.Child = null;
                }

                if (_gridResultHierarchicalRiskParity != null)
                {
                    _gridResultHierarchicalRiskParity.DataError -= _gridResult_DataError;
                    _gridResultHierarchicalRiskParity.SelectionChanged -= _gridResultHierarchicalRiskParity_SelectionChanged;
                    _gridResultHierarchicalRiskParity.CellContentClick -= _gridResultHierarchicalRiskParity_CellContentClick;
                    DataGridFactory.ClearLinks(_gridResultHierarchicalRiskParity);
                    _gridResultHierarchicalRiskParity.Rows.Clear();
                    _gridResultHierarchicalRiskParity.Columns.Clear();
                    _gridResultHierarchicalRiskParity.Dispose();
                    _gridResultHierarchicalRiskParity = null;
                    HostResultHierarchicalRiskParity.Child = null;
                }

                if (_chartEquity != null)
                {
                    _chartEquity.Series.Clear();
                    _chartEquity.ChartAreas.Clear();
                    _chartEquity.Dispose();
                    _chartEquity = null;
                    HostEquity.Child = null;
                }

                CanvasDendrogram.SizeChanged -= CanvasDendrogram_SizeChanged;
                CanvasGraph.SizeChanged -= CanvasGraph_SizeChanged;

                ComboBoxChartType.SelectionChanged -= ComboBoxChartType_SelectionChanged;
                ComboBoxPeriod.SelectionChanged -= ComboBoxPeriod_SelectionChanged;
                DatePickerStart.SelectedDateChanged -= DatePickerStart_SelectedDateChanged;
                DatePickerEnd.SelectedDateChanged -= DatePickerEnd_SelectedDateChanged;

                ButtonAddFile.Click -= ButtonAddFile_Click;
                ButtonAddFolder.Click -= ButtonAddFolder_Click;
                ButtonDelete.Click -= ButtonDelete_Click;
                ButtonClearAll.Click -= ButtonClearAll_Click;
                ButtonRecalculate.Click -= ButtonRecalculate_Click;
                ButtonCalculateCorrelationClustering.Click -= ButtonCalculateCorrelationClustering_Click;
                ButtonCalculateHierarchicalRiskParity.Click -= ButtonCalculateHierarchicalRiskParity_Click;
                ButtonSaveCorrelationClustering.Click -= ButtonSaveCorrelationClustering_Click;
                ButtonSaveHierarchicalRiskParity.Click -= ButtonSaveHierarchicalRiskParity_Click;
                ButtonHelpDataDownloading.Click -= ButtonHelp_Click;
                ButtonHelpBaseEquity.Click -= ButtonHelp_Click;
                ButtonHelpCorrelationClustering.Click -= ButtonHelp_Click;
                ButtonHelpHierarchicalRiskParity.Click -= ButtonHelp_Click;

                Closing -= CapitalAllocationUi_Closing;
                Closed -= CapitalAllocationUi_Closed;
            }
            catch (Exception error)
            {
                ServerMaster.SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        #region Bots grid

        private void CreateBotsGrid()
        {
            _gridBots = DataGridFactory.GetDataGridView(Forms.DataGridViewSelectionMode.FullRowSelect,
                Forms.DataGridViewAutoSizeRowsMode.AllCells);
            _gridBots.ScrollBars = Forms.ScrollBars.Vertical;
            _gridBots.Dock = Forms.DockStyle.Fill;
            _gridBots.DataError += _gridBots_DataError;
            HostBots.Child = _gridBots;

            Forms.DataGridViewTextBoxColumn columnNumber = new Forms.DataGridViewTextBoxColumn();
            columnNumber.HeaderText = "№";
            columnNumber.Width = 40;
            columnNumber.ReadOnly = true;
            _gridBots.Columns.Add(columnNumber);

            Forms.DataGridViewTextBoxColumn columnName = new Forms.DataGridViewTextBoxColumn();
            columnName.HeaderText = OsLocalization.CapitalAllocation.ColumnBotName;
            columnName.AutoSizeMode = Forms.DataGridViewAutoSizeColumnMode.AllCells;
            columnName.ReadOnly = true;
            _gridBots.Columns.Add(columnName);

            Forms.DataGridViewTextBoxColumn columnPath = new Forms.DataGridViewTextBoxColumn();
            columnPath.HeaderText = OsLocalization.CapitalAllocation.ColumnPath;
            columnPath.AutoSizeMode = Forms.DataGridViewAutoSizeColumnMode.Fill;
            columnPath.ReadOnly = true;
            _gridBots.Columns.Add(columnPath);
        }

        private void _gridBots_DataError(object sender, Forms.DataGridViewDataErrorEventArgs e)
        {
            ServerMaster.SendNewLogMessage(e.ToString(), LogMessageType.Error);
        }

        private void PaintBotsGrid()
        {
            _gridBots.Rows.Clear();

            for (int i = 0; i < _master.Bots.Count; i++)
            {
                int rowIndex = _gridBots.Rows.Add();
                _gridBots.Rows[rowIndex].Cells[0].Value = i + 1;
                _gridBots.Rows[rowIndex].Cells[1].Value = _master.Bots[i].BotName;
                _gridBots.Rows[rowIndex].Cells[2].Value = _master.Bots[i].Path;
            }
        }

        #endregion

        #region Session cash

        private void LoadLastSessionFiles()
        {
            try
            {
                if (File.Exists(_cashFileName) == false)
                {
                    return;
                }

                string[] paths = File.ReadAllLines(_cashFileName);

                for (int i = 0; i < paths.Length; i++)
                {
                    BotFile botFile = BotFileLoader.Load(paths[i]);

                    if (botFile != null && botFile.Positions.Count > 0)
                    {
                        _master.Bots.Add(botFile);
                    }
                }

                PaintBotsGrid();
                SetDefaultDateRange();
            }
            catch (Exception error)
            {
                ServerMaster.SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        private void OverwriteCash()
        {
            try
            {
                StringBuilder sb = new StringBuilder();

                for (int i = 0; i < _master.Bots.Count; i++)
                {
                    if (string.IsNullOrEmpty(_master.Bots[i].Path) == false)
                    {
                        sb.AppendLine(_master.Bots[i].Path);
                    }
                }

                if (sb.Length == 0)
                {
                    if (File.Exists(_cashFileName))
                    {
                        File.Delete(_cashFileName);
                    }

                    return;
                }

                File.WriteAllText(_cashFileName, sb.ToString());
            }
            catch (Exception error)
            {
                ServerMaster.SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        #endregion

        #region Buttons

        private void ButtonAddFile_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                OpenFileDialog dialog = new OpenFileDialog();
                dialog.Filter = "Text files (*.txt)|*.txt|All files (*.*)|*.*";
                dialog.Title = OsLocalization.CapitalAllocation.DialogFileTitle;

                bool? result = dialog.ShowDialog();

                if (result == true)
                {
                    BotFile botFile = BotFileLoader.Load(dialog.FileName);

                    if (botFile != null && botFile.Positions.Count > 0)
                    {
                        _master.Bots.Add(botFile);
                        PaintBotsGrid();
                        SetDefaultDateRange();
                        OverwriteCash();
                    }
                }
            }
            catch (Exception error)
            {
                ServerMaster.SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        private void ButtonAddFolder_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                OpenFolderDialog dialog = new OpenFolderDialog();
                dialog.Title = OsLocalization.CapitalAllocation.DialogFolderTitle;

                bool? result = dialog.ShowDialog();

                if (result == false)
                {
                    return;
                }

                List<BotFile> loaded = BotFileLoader.LoadFolder(dialog.FolderName);

                for (int i = 0; i < loaded.Count; i++)
                {
                    _master.Bots.Add(loaded[i]);
                }

                PaintBotsGrid();
                SetDefaultDateRange();
                OverwriteCash();
            }
            catch (Exception error)
            {
                ServerMaster.SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        private void ButtonDelete_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_gridBots.SelectedRows.Count == 0)
                {
                    return;
                }

                int rowIndex = _gridBots.SelectedRows[0].Index;

                if (rowIndex < 0 || rowIndex >= _master.Bots.Count)
                {
                    return;
                }

                AcceptDialogUi dialog = new AcceptDialogUi(OsLocalization.CapitalAllocation.DialogDeletePrefix + " " + _master.Bots[rowIndex].BotName + " ?");
                dialog.ShowDialog();

                if (dialog.UserAcceptAction == false)
                {
                    return;
                }

                _master.Bots.RemoveAt(rowIndex);
                PaintBotsGrid();
                OverwriteCash();
            }
            catch (Exception error)
            {
                ServerMaster.SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        private void ButtonClearAll_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_master.Bots.Count == 0)
                {
                    return;
                }

                AcceptDialogUi dialog = new AcceptDialogUi(OsLocalization.CapitalAllocation.DialogClearAllPrefix + " " + _master.Bots.Count + " " + OsLocalization.CapitalAllocation.DialogClearAllSuffix);
                dialog.ShowDialog();

                if (dialog.UserAcceptAction == false)
                {
                    return;
                }

                _master.Bots.Clear();
                PaintBotsGrid();
                OverwriteCash();
            }
            catch (Exception error)
            {
                ServerMaster.SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        #endregion

        #region Calculate

        private void ButtonCalculateCorrelationClustering_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (SyncSettings() == false)
                {
                    return;
                }

                _master.CalculateCorrelationClustering();

                if (_master.Matrix == null)
                {
                    ShowNoDataMessage();
                    return;
                }

                ShowCorrelationClusteringResult();
            }
            catch (Exception error)
            {
                ServerMaster.SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        private void ButtonCalculateHierarchicalRiskParity_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (SyncSettings() == false)
                {
                    return;
                }

                _master.CalculateHierarchicalRiskParity();

                if (_master.Matrix == null)
                {
                    ShowNoDataMessage();
                    return;
                }

                ShowHierarchicalRiskParityResult();
            }
            catch (Exception error)
            {
                ServerMaster.SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        private bool SyncSettings()
        {
            if (_master.Bots.Count == 0)
            {
                CustomMessageBoxUi ui = new CustomMessageBoxUi(OsLocalization.CapitalAllocation.MessageEmptyBots);
                ui.ShowDialog();
                return false;
            }

            _master.PeriodType = ComboBoxPeriod.SelectedIndex == 0
                ? ReturnPeriodType.Week
                : ReturnPeriodType.Month;

            _master.StartTime = DatePickerStart.SelectedDate;
            _master.EndTime = DatePickerEnd.SelectedDate;

            _master.MinCorrelation = ParseMinCorrelation();

            _master.LinkageMethod = (LinkageMethod)ComboBoxLinkage.SelectedIndex;
            _master.CovarianceMethod = (CovarianceMethod)ComboBoxCovariance.SelectedIndex;
            _master.CorrelationType = (CorrelationType)ComboBoxCorrelation.SelectedIndex;

            return true;
        }

        private void ShowNoDataMessage()
        {
            CustomMessageBoxUi ui = new CustomMessageBoxUi(OsLocalization.CapitalAllocation.MessageNoData);
            ui.ShowDialog();
        }

        private double ParseMinCorrelation()
        {
            string text = TextBoxMinCorrelation.Text;

            if (string.IsNullOrEmpty(text))
            {
                return 0.4;
            }

            decimal value = text.ToDecimal();

            if (value < 0)
            {
                value = 0;
            }

            if (value > 1)
            {
                value = 1;
            }

            return (double)value;
        }

        #endregion

        #region Result

        private void CreateResultGrids()
        {
            _gridResultCorrelationClustering = DataGridFactory.GetDataGridView(Forms.DataGridViewSelectionMode.FullRowSelect,
                Forms.DataGridViewAutoSizeRowsMode.AllCells);
            _gridResultCorrelationClustering.ScrollBars = Forms.ScrollBars.Vertical;
            _gridResultCorrelationClustering.Dock = Forms.DockStyle.Fill;
            _gridResultCorrelationClustering.DataError += _gridResult_DataError;
            _gridResultCorrelationClustering.CellContentClick += _gridResultCorrelationClustering_CellContentClick;
            _gridResultCorrelationClustering.SelectionChanged += _gridResultCorrelationClustering_SelectionChanged;
            HostResultCorrelationClustering.Child = _gridResultCorrelationClustering;

            _gridResultHierarchicalRiskParity = DataGridFactory.GetDataGridView(Forms.DataGridViewSelectionMode.FullRowSelect,
                Forms.DataGridViewAutoSizeRowsMode.AllCells);
            _gridResultHierarchicalRiskParity.ScrollBars = Forms.ScrollBars.Vertical;
            _gridResultHierarchicalRiskParity.Dock = Forms.DockStyle.Fill;
            _gridResultHierarchicalRiskParity.DataError += _gridResult_DataError;
            _gridResultHierarchicalRiskParity.SelectionChanged += _gridResultHierarchicalRiskParity_SelectionChanged;
            _gridResultHierarchicalRiskParity.CellContentClick += _gridResultHierarchicalRiskParity_CellContentClick;
            HostResultHierarchicalRiskParity.Child = _gridResultHierarchicalRiskParity;
        }

        private void _gridResult_DataError(object sender, Forms.DataGridViewDataErrorEventArgs e)
        {
            ServerMaster.SendNewLogMessage(e.ToString(), LogMessageType.Error);
        }

        private void AddTextColumn(Forms.DataGridView grid, string header, int width = 0, Forms.DataGridViewAutoSizeColumnMode autoSizeMode = Forms.DataGridViewAutoSizeColumnMode.None)
        {
            Forms.DataGridViewTextBoxColumn column = new Forms.DataGridViewTextBoxColumn();
            column.HeaderText = header;
            column.ReadOnly = true;

            if (width > 0)
            {
                column.Width = width;
            }

            if (autoSizeMode != Forms.DataGridViewAutoSizeColumnMode.None)
            {
                column.AutoSizeMode = autoSizeMode;
            }

            grid.Columns.Add(column);
        }

        private void ShowCorrelationClusteringResult()
        {
            Forms.DataGridView grid = _gridResultCorrelationClustering;

            grid.Rows.Clear();
            grid.Columns.Clear();

            AddTextColumn(grid, OsLocalization.CapitalAllocation.ColumnNumCluster, 100);
            AddTextColumn(grid, OsLocalization.CapitalAllocation.ColumnNumInCluster, 100);
            AddTextColumn(grid, OsLocalization.CapitalAllocation.ColumnNameRobot, autoSizeMode: Forms.DataGridViewAutoSizeColumnMode.Fill);
            AddTextColumn(grid, "100%", 70);
            AddTextColumn(grid, "200%", 70);
            AddTextColumn(grid, "300%", 70);
            AddTextColumn(grid, "400%", 70);
            AddTextColumn(grid, "500%", 70);
            AddTextColumn(grid, "600%", 70);

            Forms.DataGridViewButtonColumn journalColumn = new Forms.DataGridViewButtonColumn();
            journalColumn.HeaderText = OsLocalization.CapitalAllocation.ColumnJournal;
            journalColumn.ReadOnly = true;
            journalColumn.AutoSizeMode = Forms.DataGridViewAutoSizeColumnMode.AllCells;
            grid.Columns.Add(journalColumn);

            _clusteringClusters = _master.Clusters;

            if (_clusteringClusters == null)
            {
                return;
            }

            for (int i = 0; i < _clusteringClusters.Count; i++)
            {
                CorrelationCluster cluster = _clusteringClusters[i];

                int headerRow = grid.Rows.Add();
                grid.Rows[headerRow].Cells[0].Value = i + 1;
                grid.Rows[headerRow].Cells[9].Value = OsLocalization.CapitalAllocation.ColumnJournal;
                grid.Rows[headerRow].Cells[9].Tag = i;

                for (int j = 0; j < cluster.BotIndices.Count; j++)
                {
                    int botIndex = cluster.BotIndices[j];
                    decimal weight = _master.Weights[botIndex];

                    int row = grid.Rows.Add();
                    grid.Rows[row].Cells[1].Value = j + 1;
                    grid.Rows[row].Cells[2].Value = _master.Matrix.BotNames[botIndex];
                    grid.Rows[row].Cells[3].Value = Math.Round(weight * 100, 2);
                    grid.Rows[row].Cells[4].Value = Math.Round(weight * 200, 2);
                    grid.Rows[row].Cells[5].Value = Math.Round(weight * 300, 2);
                    grid.Rows[row].Cells[6].Value = Math.Round(weight * 400, 2);
                    grid.Rows[row].Cells[7].Value = Math.Round(weight * 500, 2);
                    grid.Rows[row].Cells[8].Value = Math.Round(weight * 600, 2);
                    grid.Rows[row].Tag = botIndex;
                }
            }

            _highlightedGraphBotIndex = -1;
            DrawGraph();
        }

        private void DrawGraph()
        {
            CanvasGraph.Children.Clear();
            _graphNodes.Clear();
            _graphPositions.Clear();

            if (_master.Matrix == null || _clusteringClusters == null || _clusteringClusters.Count == 0)
            {
                return;
            }

            double[,] corr = Statistics.CorrelationMatrix(_master.Matrix.Returns);

            List<int> order = new List<int>();

            for (int c = 0; c < _clusteringClusters.Count; c++)
            {
                for (int j = 0; j < _clusteringClusters[c].BotIndices.Count; j++)
                {
                    order.Add(_clusteringClusters[c].BotIndices[j]);
                }
            }

            int n = order.Count;

            if (n == 0)
            {
                return;
            }

            double width = CanvasGraph.ActualWidth;
            double height = CanvasGraph.ActualHeight;

            if (width <= 0 || height <= 0)
            {
                return;
            }

            Dictionary<int, int> clusterOfBot = new Dictionary<int, int>();

            for (int c = 0; c < _clusteringClusters.Count; c++)
            {
                for (int j = 0; j < _clusteringClusters[c].BotIndices.Count; j++)
                {
                    clusterOfBot[_clusteringClusters[c].BotIndices[j]] = c;
                }
            }

            double centerX = width / 2;
            double centerY = height / 2;
            double radius = Math.Min(width, height) / 2 - 30;

            Point[] positions = new Point[n];

            for (int i = 0; i < n; i++)
            {
                double angle = 2 * Math.PI * i / n - Math.PI / 2;
                positions[i] = new Point(centerX + radius * Math.Cos(angle), centerY + radius * Math.Sin(angle));
            }

            double threshold = _master.MinCorrelation;

            for (int i = 0; i < n; i++)
            {
                for (int j = i + 1; j < n; j++)
                {
                    if (corr[order[i], order[j]] >= threshold)
                    {
                        AddGraphLine(positions[i], positions[j]);
                    }
                }
            }

            for (int i = 0; i < n; i++)
            {
                int botIndex = order[i];
                int clusterIndex = clusterOfBot[botIndex];

                double nodeSize = botIndex == _highlightedGraphBotIndex ? 14 : 11;

                Ellipse node = new Ellipse();
                node.Width = nodeSize;
                node.Height = nodeSize;
                node.Fill = new SolidColorBrush(GetClusterColor(clusterIndex));
                Canvas.SetLeft(node, positions[i].X - nodeSize / 2);
                Canvas.SetTop(node, positions[i].Y - nodeSize / 2);
                node.ToolTip = string.Format("{0} — {1} {2}",
                    _master.Matrix.BotNames[botIndex],
                    OsLocalization.CapitalAllocation.ClusterPrefix,
                    clusterIndex + 1);
                node.Tag = botIndex;
                node.MouseLeftButtonDown += Node_MouseLeftButtonDown;
                CanvasGraph.Children.Add(node);

                _graphNodes[botIndex] = node;
                _graphPositions[botIndex] = positions[i];
            }
        }

        private void AddGraphLine(Point a, Point b)
        {
            Line line = new Line();
            line.X1 = a.X;
            line.Y1 = a.Y;
            line.X2 = b.X;
            line.Y2 = b.Y;
            line.Stroke = new SolidColorBrush(Color.FromArgb(120, 154, 156, 158));
            line.StrokeThickness = 1;
            CanvasGraph.Children.Add(line);
        }

        private void _gridResultCorrelationClustering_SelectionChanged(object sender, EventArgs e)
        {
            try
            {
                if (_gridResultCorrelationClustering.SelectedRows.Count == 0)
                {
                    return;
                }

                int rowIndex = _gridResultCorrelationClustering.SelectedRows[0].Index;

                if (rowIndex < 0 || rowIndex >= _gridResultCorrelationClustering.Rows.Count)
                {
                    return;
                }

                if (_gridResultCorrelationClustering.Rows[rowIndex].Tag is int botIndex)
                {
                    SetHighlightedGraphBot(botIndex);
                }
            }
            catch (Exception error)
            {
                ServerMaster.SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        private void SetHighlightedGraphBot(int botIndex)
        {
            _highlightedGraphBotIndex = botIndex;

            foreach (KeyValuePair<int, Ellipse> pair in _graphNodes)
            {
                double size = pair.Key == botIndex ? 14 : 11;
                Ellipse node = pair.Value;
                Point position = _graphPositions[pair.Key];

                node.Width = size;
                node.Height = size;
                Canvas.SetLeft(node, position.X - size / 2);
                Canvas.SetTop(node, position.Y - size / 2);
            }
        }

        private void Node_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            try
            {
                if (sender is Ellipse node && node.Tag is int botIndex)
                {
                    int rowIndex = FindBotRow(botIndex);

                    if (rowIndex >= 0)
                    {
                        _gridResultCorrelationClustering.ClearSelection();
                        _gridResultCorrelationClustering.Rows[rowIndex].Selected = true;
                        _gridResultCorrelationClustering.CurrentCell = _gridResultCorrelationClustering.Rows[rowIndex].Cells[0];
                        _gridResultCorrelationClustering.FirstDisplayedScrollingRowIndex = rowIndex;
                    }
                }
            }
            catch (Exception error)
            {
                ServerMaster.SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        private int FindBotRow(int botIndex)
        {
            for (int i = 0; i < _gridResultCorrelationClustering.Rows.Count; i++)
            {
                if (_gridResultCorrelationClustering.Rows[i].Tag is int tag && tag == botIndex)
                {
                    return i;
                }
            }

            return -1;
        }

        private static Color GetClusterColor(int clusterIndex)
        {
            double hue = (clusterIndex * 137.508) % 360;

            double c = 0.85;
            double x = c * (1 - Math.Abs((hue / 60) % 2 - 1));
            double m = 0.1;
            double r, g, b;

            if (hue < 60) { r = c; g = x; b = 0; }
            else if (hue < 120) { r = x; g = c; b = 0; }
            else if (hue < 180) { r = 0; g = c; b = x; }
            else if (hue < 240) { r = 0; g = x; b = c; }
            else if (hue < 300) { r = x; g = 0; b = c; }
            else { r = c; g = 0; b = x; }

            return Color.FromArgb(255, (byte)((r + m) * 255), (byte)((g + m) * 255), (byte)((b + m) * 255));
        }

        private void CanvasGraph_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            try
            {
                if (CanvasGraph.Visibility == Visibility.Visible)
                {
                    DrawGraph();
                }
            }
            catch (Exception error)
            {
                ServerMaster.SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        private void _gridResultCorrelationClustering_CellContentClick(object sender, Forms.DataGridViewCellEventArgs e)
        {
            try
            {
                if (e.RowIndex < 0 || e.ColumnIndex != 9)
                {
                    return;
                }

                if (_gridResultCorrelationClustering.Columns.Count <= 9 || _clusteringClusters == null)
                {
                    return;
                }

                if (_gridResultCorrelationClustering.Rows[e.RowIndex].Cells[9].Tag is int clusterIndex == false)
                {
                    return;
                }

                if (clusterIndex >= _clusteringClusters.Count)
                {
                    return;
                }

                List<Position> positions = new List<Position>();

                CorrelationCluster cluster = _clusteringClusters[clusterIndex];

                for (int i = 0; i < cluster.BotIndices.Count; i++)
                {
                    int botIndex = cluster.BotIndices[i];

                    if (botIndex >= 0 && botIndex < _master.Bots.Count)
                    {
                        positions.AddRange(_master.Bots[botIndex].Positions);
                    }
                }

                positions.Sort(delegate (Position a, Position b) { return a.TimeCreate.CompareTo(b.TimeCreate); });

                CapitalAllocationJournalUi journalUi = new CapitalAllocationJournalUi(positions, clusterIndex + 1);
                journalUi.Show();
            }
            catch (Exception error)
            {
                ServerMaster.SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        private void ShowHierarchicalRiskParityResult()
        {
            Forms.DataGridView grid = _gridResultHierarchicalRiskParity;

            grid.Rows.Clear();
            grid.Columns.Clear();

            AddTextColumn(grid, OsLocalization.CapitalAllocation.ColumnNumCluster, 100);
            AddTextColumn(grid, OsLocalization.CapitalAllocation.ColumnNumInCluster, 100);
            AddTextColumn(grid, OsLocalization.CapitalAllocation.ColumnNameRobot, autoSizeMode: Forms.DataGridViewAutoSizeColumnMode.Fill);
            AddTextColumn(grid, "100%", 70);
            AddTextColumn(grid, "200%", 70);
            AddTextColumn(grid, "300%", 70);
            AddTextColumn(grid, "400%", 70);
            AddTextColumn(grid, "500%", 70);
            AddTextColumn(grid, "600%", 70);

            Forms.DataGridViewButtonColumn journalColumn = new Forms.DataGridViewButtonColumn();
            journalColumn.HeaderText = OsLocalization.CapitalAllocation.ColumnJournal;
            journalColumn.ReadOnly = true;
            journalColumn.AutoSizeMode = Forms.DataGridViewAutoSizeColumnMode.AllCells;
            grid.Columns.Add(journalColumn);

            _hrpAlgorithm = _master.Hrp;

            ComputeHrpClusters();

            if (_hrpClusters == null)
            {
                return;
            }

            for (int i = 0; i < _hrpClusters.Count; i++)
            {
                List<int> cluster = _hrpClusters[i];

                int headerRow = grid.Rows.Add();
                grid.Rows[headerRow].Cells[0].Value = i + 1;
                grid.Rows[headerRow].Cells[9].Value = OsLocalization.CapitalAllocation.ColumnJournal;
                grid.Rows[headerRow].Cells[9].Tag = i;

                for (int j = 0; j < cluster.Count; j++)
                {
                    int botIndex = cluster[j];
                    decimal weight = _master.Weights[botIndex];

                    int row = grid.Rows.Add();
                    grid.Rows[row].Cells[1].Value = j + 1;
                    grid.Rows[row].Cells[2].Value = _master.Matrix.BotNames[botIndex];
                    grid.Rows[row].Cells[3].Value = Math.Round(weight * 100, 2);
                    grid.Rows[row].Cells[4].Value = Math.Round(weight * 200, 2);
                    grid.Rows[row].Cells[5].Value = Math.Round(weight * 300, 2);
                    grid.Rows[row].Cells[6].Value = Math.Round(weight * 400, 2);
                    grid.Rows[row].Cells[7].Value = Math.Round(weight * 500, 2);
                    grid.Rows[row].Cells[8].Value = Math.Round(weight * 600, 2);
                    grid.Rows[row].Tag = botIndex;
                }
            }

            _highlightedBotIndex = -1;
            CanvasDendrogram.UpdateLayout();
            DrawDendrogram();
        }

        private void _gridResultHierarchicalRiskParity_CellContentClick(object sender, Forms.DataGridViewCellEventArgs e)
        {
            try
            {
                if (e.RowIndex < 0 || e.ColumnIndex != 9)
                {
                    return;
                }

                if (_gridResultHierarchicalRiskParity.Columns.Count <= 9 || _hrpClusters == null)
                {
                    return;
                }

                if (_gridResultHierarchicalRiskParity.Rows[e.RowIndex].Cells[9].Tag is int clusterIndex == false)
                {
                    return;
                }

                if (clusterIndex >= _hrpClusters.Count)
                {
                    return;
                }

                List<Position> positions = new List<Position>();

                List<int> cluster = _hrpClusters[clusterIndex];

                for (int i = 0; i < cluster.Count; i++)
                {
                    int botIndex = cluster[i];

                    if (botIndex >= 0 && botIndex < _master.Bots.Count)
                    {
                        positions.AddRange(_master.Bots[botIndex].Positions);
                    }
                }

                positions.Sort(delegate (Position a, Position b) { return a.TimeCreate.CompareTo(b.TimeCreate); });

                CapitalAllocationJournalUi journalUi = new CapitalAllocationJournalUi(positions, clusterIndex + 1);
                journalUi.Show();
            }
            catch (Exception error)
            {
                ServerMaster.SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        #endregion

        #region Save result

        private void ButtonSaveCorrelationClustering_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_clusteringClusters == null)
                {
                    return;
                }

                List<List<int>> clusters = new List<List<int>>();

                for (int i = 0; i < _clusteringClusters.Count; i++)
                {
                    clusters.Add(_clusteringClusters[i].BotIndices);
                }

                SaveClustersToFile(clusters);
            }
            catch (Exception error)
            {
                ServerMaster.SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        private void ButtonSaveHierarchicalRiskParity_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                SaveClustersToFile(_hrpClusters);
            }
            catch (Exception error)
            {
                ServerMaster.SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        private void SaveClustersToFile(List<List<int>> clusters)
        {
            if (clusters == null || clusters.Count == 0 || _master.Matrix == null || _master.Weights == null)
            {
                return;
            }

            SaveFileDialog dialog = new SaveFileDialog();
            dialog.Filter = "Text files (*.txt)|*.txt|All files (*.*)|*.*";

            if (dialog.ShowDialog() != true)
            {
                return;
            }

            string fileName = dialog.FileName;

            if (System.IO.Path.GetExtension(fileName).Length == 0)
            {
                fileName += ".txt";
            }

            AllocationFileService.Save(fileName, clusters, _master.Matrix.BotNames, _master.Weights);
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

        #region Dendrogram

        private void CanvasDendrogram_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            try
            {
                if (CanvasDendrogram.Visibility == Visibility.Visible)
                {
                    DrawDendrogram();
                }
            }
            catch (Exception error)
            {
                ServerMaster.SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        private void DrawDendrogram()
        {
            CanvasDendrogram.Children.Clear();
            _dendrogramDots.Clear();

            if (_hrpAlgorithm == null || _master.Matrix == null)
            {
                return;
            }

            List<DendrogramNode> merges = _hrpAlgorithm.Merges;
            List<int> sorted = _hrpAlgorithm.SortedIndices;

            if (merges.Count == 0 || sorted.Count == 0)
            {
                return;
            }

            double width = CanvasDendrogram.ActualWidth;
            double height = CanvasDendrogram.ActualHeight;

            if (width <= 0 || height <= 0)
            {
                return;
            }

            int botsCount = _master.Matrix.BotsCount;

            double[] centerX = new double[botsCount + merges.Count];
            double[] clusterHeight = new double[botsCount + merges.Count];

            for (int i = 0; i < sorted.Count; i++)
            {
                centerX[sorted[i]] = width * (i + 0.5) / sorted.Count;
                clusterHeight[sorted[i]] = 0;
            }

            double maxDistance = 0;

            for (int i = 0; i < merges.Count; i++)
            {
                if (merges[i].Distance > maxDistance)
                {
                    maxDistance = merges[i].Distance;
                }
            }

            double yScale = (height - 30) / (maxDistance > 0 ? maxDistance : 1);
            double bottom = height - 10;

            ComputeHrpClusters();

            for (int k = 0; k < merges.Count; k++)
            {
                DendrogramNode node = merges[k];
                int id = botsCount + k;

                centerX[id] = (centerX[node.Left] + centerX[node.Right]) / 2;
                clusterHeight[id] = node.Distance;

                double y = bottom - node.Distance * yScale;
                double yLeft = bottom - clusterHeight[node.Left] * yScale;
                double yRight = bottom - clusterHeight[node.Right] * yScale;

                AddDendrogramLine(centerX[node.Left], yLeft, centerX[node.Left], y);
                AddDendrogramLine(centerX[node.Right], yRight, centerX[node.Right], y);
                AddDendrogramLine(centerX[node.Left], y, centerX[node.Right], y);
            }

            for (int i = 0; i < sorted.Count; i++)
            {
                int botIndex = sorted[i];

                double size = botIndex == _highlightedBotIndex ? 14 : 8;

                Ellipse dot = new Ellipse();
                dot.Width = size;
                dot.Height = size;
                dot.Fill = new SolidColorBrush(GetClusterColor(_hrpClusterOfBot[botIndex]));
                Canvas.SetLeft(dot, centerX[botIndex] - size / 2);
                Canvas.SetTop(dot, bottom - size / 2);
                dot.ToolTip = _master.Matrix.BotNames[botIndex];
                dot.Tag = botIndex;
                dot.MouseLeftButtonDown += DendrogramDot_MouseLeftButtonDown;
                _dendrogramDots[botIndex] = dot;
                _dendrogramDotPositions[botIndex] = new Point(centerX[botIndex], bottom);
                CanvasDendrogram.Children.Add(dot);
            }
        }

        private void AddDendrogramLine(double x1, double y1, double x2, double y2)
        {
            Line line = new Line();
            line.X1 = x1;
            line.Y1 = y1;
            line.X2 = x2;
            line.Y2 = y2;
            line.Stroke = new SolidColorBrush(Color.FromArgb(255, 154, 156, 158));
            line.StrokeThickness = 1;
            CanvasDendrogram.Children.Add(line);
        }

        private void _gridResultHierarchicalRiskParity_SelectionChanged(object sender, EventArgs e)
        {
            try
            {
                if (_gridResultHierarchicalRiskParity.SelectedRows.Count == 0)
                {
                    return;
                }

                int rowIndex = _gridResultHierarchicalRiskParity.SelectedRows[0].Index;

                if (rowIndex < 0 || rowIndex >= _gridResultHierarchicalRiskParity.Rows.Count)
                {
                    return;
                }

                if (_gridResultHierarchicalRiskParity.Rows[rowIndex].Tag is int botIndex)
                {
                    SetHighlightedBot(botIndex);
                }
            }
            catch (Exception error)
            {
                ServerMaster.SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        private void SetHighlightedBot(int botIndex)
        {
            _highlightedBotIndex = botIndex;

            foreach (KeyValuePair<int, Ellipse> pair in _dendrogramDots)
            {
                double size = pair.Key == botIndex ? 14 : 8;
                Ellipse dot = pair.Value;
                Point position = _dendrogramDotPositions[pair.Key];

                dot.Width = size;
                dot.Height = size;
                Canvas.SetLeft(dot, position.X - size / 2);
                Canvas.SetTop(dot, position.Y - size / 2);
            }
        }

        private void ComputeHrpClusters()
        {
            _hrpClusters = null;
            _hrpClusterOfBot = null;

            if (_hrpAlgorithm == null || _master.Matrix == null)
            {
                return;
            }

            int botsCount = _master.Matrix.BotsCount;

            _hrpClusters = AllocationFileService.ComputeHrpClusters(_hrpAlgorithm, botsCount, _master.MinCorrelation);

            if (_hrpClusters == null)
            {
                return;
            }

            int[] clusterOfBot = new int[botsCount];

            for (int c = 0; c < _hrpClusters.Count; c++)
            {
                for (int j = 0; j < _hrpClusters[c].Count; j++)
                {
                    clusterOfBot[_hrpClusters[c][j]] = c;
                }
            }

            _hrpClusterOfBot = clusterOfBot;
        }

        private void DendrogramDot_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            try
            {
                if (sender is Ellipse dot && dot.Tag is int botIndex)
                {
                    int rowIndex = FindHrpBotRow(botIndex);

                    if (rowIndex >= 0)
                    {
                        _gridResultHierarchicalRiskParity.ClearSelection();
                        _gridResultHierarchicalRiskParity.Rows[rowIndex].Selected = true;
                        _gridResultHierarchicalRiskParity.CurrentCell = _gridResultHierarchicalRiskParity.Rows[rowIndex].Cells[0];
                        _gridResultHierarchicalRiskParity.FirstDisplayedScrollingRowIndex = rowIndex;
                    }
                }
            }
            catch (Exception error)
            {
                ServerMaster.SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        private int FindHrpBotRow(int botIndex)
        {
            for (int i = 0; i < _gridResultHierarchicalRiskParity.Rows.Count; i++)
            {
                if (_gridResultHierarchicalRiskParity.Rows[i].Tag is int tag && tag == botIndex)
                {
                    return i;
                }
            }

            return -1;
        }

        #endregion

        #region Equity chart

        private void CreateEquityChart()
        {
            _chartEquity = new Chart();
            _chartEquity.BackColor = Themes.ThemeManager.GetColorWinForms("JournalChartBackColor");
            _chartEquity.Dock = Forms.DockStyle.Fill;
            HostEquity.Child = _chartEquity;
        }

        private void SetDefaultDateRange()
        {
            if (_master.Bots.Count == 0)
            {
                return;
            }

            DateTime? minDate = null;
            DateTime? maxDate = null;

            for (int i = 0; i < _master.Bots.Count; i++)
            {
                List<Position> positions = _master.Bots[i].Positions;

                for (int j = 0; j < positions.Count; j++)
                {
                    DateTime timeCreate = positions[j].TimeCreate;

                    if (minDate == null || timeCreate < minDate.Value)
                    {
                        minDate = timeCreate;
                    }

                    if (maxDate == null || timeCreate > maxDate.Value)
                    {
                        maxDate = timeCreate;
                    }
                }
            }

            if (minDate.HasValue)
            {
                DatePickerStart.SelectedDate = minDate.Value;
            }

            if (maxDate.HasValue)
            {
                DatePickerEnd.SelectedDate = maxDate.Value;
            }
        }

        private void ComboBoxChartType_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            try
            {
                PaintEquityChart();
            }
            catch (Exception error)
            {
                ServerMaster.SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        private void ButtonRecalculate_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                PaintEquityChart();
            }
            catch (Exception error)
            {
                ServerMaster.SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        private void ComboBoxPeriod_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            try
            {
                PaintEquityChart();
            }
            catch (Exception error)
            {
                ServerMaster.SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        private void DatePickerStart_SelectedDateChanged(object sender, SelectionChangedEventArgs e)
        {
            try
            {
                PaintEquityChart();
            }
            catch (Exception error)
            {
                ServerMaster.SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        private void DatePickerEnd_SelectedDateChanged(object sender, SelectionChangedEventArgs e)
        {
            try
            {
                PaintEquityChart();
            }
            catch (Exception error)
            {
                ServerMaster.SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        private void PaintEquityChart()
        {
            if (_chartEquity == null || _master.Bots.Count == 0)
            {
                return;
            }

            ReturnPeriodType periodType = ComboBoxPeriod.SelectedIndex == 0
                ? ReturnPeriodType.Week
                : ReturnPeriodType.Month;

            DateTime? startTime = DatePickerStart.SelectedDate;
            DateTime? endTime = DatePickerEnd.SelectedDate;

            ReturnsMatrix matrix = ReturnsMatrixBuilder.Build(_master.Bots, periodType, startTime, endTime);

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
            area.AxisY2.Title = ComboBoxChartType.SelectedIndex == 0
                ? OsLocalization.CapitalAllocation.LabelProfit
                : OsLocalization.CapitalAllocation.LabelProfitPercent;
            area.AxisX.MajorGrid.Enabled = false;
            area.AxisY2.MajorGrid.LineColor = Themes.ThemeManager.GetColorWinForms("GridLinesColor");
            _chartEquity.ChartAreas.Add(area);

            bool usePercent = ComboBoxChartType.SelectedIndex != 0;

            for (int i = 0; i < matrix.BotsCount; i++)
            {
                Series series = new Series("s" + i);
                series.ChartType = SeriesChartType.Line;
                series.ChartArea = "Area";
                series.BorderWidth = 1;
                series.YAxisType = AxisType.Secondary;
                series.IsVisibleInLegend = false;

                for (int j = 0; j < matrix.PeriodsCount; j++)
                {
                    double value = usePercent ? matrix.Returns[i, j] : matrix.ReturnsAbs[i, j];
                    series.Points.AddXY(j, value);

                    if (i == 0)
                    {
                        series.Points[j].AxisLabel = matrix.PeriodStarts[j].ToString("dd.MM.yyyy");
                    }
                }

                _chartEquity.Series.Add(series);
            }
        }

        #endregion
    }
}
