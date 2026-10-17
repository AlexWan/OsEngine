using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using OsEngine.Entity;
using OsEngine.Language;
using OsEngine.Layout;
using OsEngine.Logging;
using OsEngine.Market;
using OsEngine.OsTrader;
using OsEngine.OsTrader.Panels;
using Forms = System.Windows.Forms;

namespace OsEngine.OsCapitalAllocation
{
    public partial class CapitalAllocationLoadUi : Window
    {
        private const string _settingsFile = @"Engine\AllocationLoad.txt";

        private Forms.DataGridView _gridLoaded;

        private Forms.DataGridView _gridApplied;

        private Forms.DataGridView _gridSkipped;

        private List<ClusterInFile> _clusters = new List<ClusterInFile>();

        private AllocationApplyResult _applyResult = new AllocationApplyResult();

        #region Constructors

        public CapitalAllocationLoadUi()
        {
            InitializeComponent();
            StickyBorders.Listen(this);
            StartupLocation.Start_MouseInCentre(this);

            ComboBoxShoulder.Items.Add("1");
            ComboBoxShoulder.Items.Add("2");
            ComboBoxShoulder.Items.Add("3");
            ComboBoxShoulder.Items.Add("4");
            ComboBoxShoulder.Items.Add("5");
            ComboBoxShoulder.Items.Add("6");
            ComboBoxShoulder.SelectedIndex = 1;

            ButtonBrowse.Click += ButtonBrowse_Click;
            ButtonApply.Click += ButtonApply_Click;
            ButtonCancel.Click += ButtonCancel_Click;
            ButtonHelpLoadData.Click += ButtonHelp_Click;
            ButtonHelpSetData.Click += ButtonHelp_Click;

            SetLocalization();

            LoadSettings();

            ComboBoxShoulder.SelectionChanged += ComboBoxShoulder_SelectionChanged;

            CreateLoadedGrid();
            CreateAppliedGrid();
            CreateSkippedGrid();

            Closed += CapitalAllocationLoadUi_Closed;

            if (string.IsNullOrEmpty(TextBoxFile.Text) == false && File.Exists(TextBoxFile.Text))
            {
                LoadAndCompute();
            }
        }

        #endregion

        #region Localization

        private void SetLocalization()
        {
            Title = OsLocalization.CapitalAllocation.LoadAllocationTitle;
            TabLoadData.Header = OsLocalization.CapitalAllocation.TabLoadData;
            TabSetData.Header = OsLocalization.CapitalAllocation.TabSetData;
            LabelFile.Content = OsLocalization.CapitalAllocation.LabelLoadFile;
            ButtonBrowse.Content = OsLocalization.CapitalAllocation.ButtonBrowse;
            LabelShoulder.Content = OsLocalization.CapitalAllocation.LabelLoadShoulder;
            LabelWillBeSet.Content = OsLocalization.CapitalAllocation.LabelWillBeSet;
            LabelSkipped.Content = OsLocalization.CapitalAllocation.LabelSkipped;
            ButtonApply.Content = OsLocalization.CapitalAllocation.ButtonApply;
            ButtonCancel.Content = OsLocalization.CapitalAllocation.ButtonCancel;
            ButtonHelpLoadData.Tag = OsLocalization.CapitalAllocation.HelpLoadDataTab;
            ButtonHelpSetData.Tag = OsLocalization.CapitalAllocation.HelpSetDataTab;
        }

        private void LoadSettings()
        {
            try
            {
                if (File.Exists(_settingsFile) == false)
                {
                    return;
                }

                string[] lines = File.ReadAllLines(_settingsFile);

                if (lines.Length >= 1)
                {
                    TextBoxFile.Text = lines[0];
                }

                if (lines.Length >= 2 && ComboBoxShoulder.Items.Contains(lines[1]))
                {
                    ComboBoxShoulder.SelectedItem = lines[1];
                }
            }
            catch (Exception error)
            {
                ServerMaster.SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        private void SaveSettings()
        {
            try
            {
                string shoulder = ComboBoxShoulder.SelectedItem != null
                    ? ComboBoxShoulder.SelectedItem.ToString()
                    : "2";

                File.WriteAllText(_settingsFile, TextBoxFile.Text + "\r\n" + shoulder);
            }
            catch (Exception error)
            {
                ServerMaster.SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        #endregion

        #region Window events

        private void CapitalAllocationLoadUi_Closed(object sender, EventArgs e)
        {
            try
            {
                ButtonBrowse.Click -= ButtonBrowse_Click;
                ButtonApply.Click -= ButtonApply_Click;
                ButtonCancel.Click -= ButtonCancel_Click;
                ButtonHelpLoadData.Click -= ButtonHelp_Click;
                ButtonHelpSetData.Click -= ButtonHelp_Click;

                ComboBoxShoulder.SelectionChanged -= ComboBoxShoulder_SelectionChanged;

                if (_gridLoaded != null)
                {
                    DataGridFactory.ClearLinks(_gridLoaded);
                    _gridLoaded.DataError -= _grid_DataError;
                    _gridLoaded.Rows.Clear();
                    _gridLoaded.Columns.Clear();
                    _gridLoaded.Dispose();
                    _gridLoaded = null;
                    HostLoaded.Child = null;
                }

                if (_gridApplied != null)
                {
                    DataGridFactory.ClearLinks(_gridApplied);
                    _gridApplied.DataError -= _grid_DataError;
                    _gridApplied.Rows.Clear();
                    _gridApplied.Columns.Clear();
                    _gridApplied.Dispose();
                    _gridApplied = null;
                    HostApplied.Child = null;
                }

                if (_gridSkipped != null)
                {
                    DataGridFactory.ClearLinks(_gridSkipped);
                    _gridSkipped.DataError -= _grid_DataError;
                    _gridSkipped.Rows.Clear();
                    _gridSkipped.Columns.Clear();
                    _gridSkipped.Dispose();
                    _gridSkipped = null;
                    HostSkipped.Child = null;
                }

                Closed -= CapitalAllocationLoadUi_Closed;
            }
            catch (Exception error)
            {
                ServerMaster.SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        #endregion

        #region UI event handlers

        private void ButtonBrowse_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                OpenFileDialog dialog = new OpenFileDialog();
                dialog.Filter = OsLocalization.CapitalAllocation.DialogFilterTxt;

                if (dialog.ShowDialog() == true)
                {
                    TextBoxFile.Text = dialog.FileName;
                    LoadAndCompute();
                    SaveSettings();
                }
            }
            catch (Exception error)
            {
                ServerMaster.SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        private void LoadAndCompute()
        {
            try
            {
                string filePath = TextBoxFile.Text;

                if (string.IsNullOrEmpty(filePath) || File.Exists(filePath) == false)
                {
                    CustomMessageBoxUi ui = new CustomMessageBoxUi(OsLocalization.CapitalAllocation.MessageSelectFile);
                    ui.ShowDialog();
                    return;
                }

                _clusters = AllocationFileService.Load(filePath);

                ComputeAllocation(_clusters, ComboBoxShoulder.SelectedItem.ToString());

                PaintLoaded();
                PaintApplied();
                PaintSkipped();
            }
            catch (Exception error)
            {
                ServerMaster.SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        private void ComboBoxShoulder_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            try
            {
                SaveSettings();

                if (_clusters.Count == 0)
                {
                    return;
                }

                ComputeAllocation(_clusters, ComboBoxShoulder.SelectedItem.ToString());
                PaintApplied();
                PaintSkipped();
            }
            catch (Exception error)
            {
                ServerMaster.SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        private void ButtonApply_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                AllocationFileService.Apply(_applyResult);

                Close();
            }
            catch (Exception error)
            {
                ServerMaster.SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        private void ButtonCancel_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Close();
            }
            catch (Exception error)
            {
                ServerMaster.SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

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

        #region Allocation

        private void ComputeAllocation(List<ClusterInFile> botInFiles, string shoulder)
        {
            _applyResult = AllocationFileService.Compute(botInFiles, shoulder, OsTraderMaster.Master.PanelsArray);
        }

        #endregion

        #region Grids

        private void CreateLoadedGrid()
        {
            _gridLoaded = DataGridFactory.GetDataGridView(Forms.DataGridViewSelectionMode.FullRowSelect,
                Forms.DataGridViewAutoSizeRowsMode.AllCells);
            _gridLoaded.ScrollBars = Forms.ScrollBars.Vertical;
            _gridLoaded.Dock = Forms.DockStyle.Fill;
            _gridLoaded.DataError += _grid_DataError;
            HostLoaded.Child = _gridLoaded;

            AddLoadedColumn(OsLocalization.CapitalAllocation.ColumnNumCluster, 100);
            AddLoadedColumn(OsLocalization.CapitalAllocation.ColumnNumInCluster, 100);
            AddLoadedColumn(OsLocalization.CapitalAllocation.ColumnNameRobot, 0, Forms.DataGridViewAutoSizeColumnMode.Fill);
            AddLoadedColumn("100%", 70);
            AddLoadedColumn("200%", 70);
            AddLoadedColumn("300%", 70);
            AddLoadedColumn("400%", 70);
            AddLoadedColumn("500%", 70);
            AddLoadedColumn("600%", 70);
        }

        private void AddLoadedColumn(string header, int width = 0, Forms.DataGridViewAutoSizeColumnMode autoSizeMode = Forms.DataGridViewAutoSizeColumnMode.None)
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

            _gridLoaded.Columns.Add(column);
        }

        private void PaintLoaded()
        {
            _gridLoaded.Rows.Clear();

            for (int c = 0; c < _clusters.Count; c++)
            {
                ClusterInFile cluster = _clusters[c];

                int headerRow = _gridLoaded.Rows.Add();
                _gridLoaded.Rows[headerRow].Cells[0].Value = cluster.ClusterNum;

                for (int j = 0; j < cluster.Bots.Count; j++)
                {
                    BotInFile bot = cluster.Bots[j];

                    int row = _gridLoaded.Rows.Add();
                    _gridLoaded.Rows[row].Cells[1].Value = j + 1;
                    _gridLoaded.Rows[row].Cells[2].Value = bot.Name;
                    _gridLoaded.Rows[row].Cells[3].Value = bot.Shoulder1Value;
                    _gridLoaded.Rows[row].Cells[4].Value = bot.Shoulder2Value;
                    _gridLoaded.Rows[row].Cells[5].Value = bot.Shoulder3Value;
                    _gridLoaded.Rows[row].Cells[6].Value = bot.Shoulder4Value;
                    _gridLoaded.Rows[row].Cells[7].Value = bot.Shoulder5Value;
                    _gridLoaded.Rows[row].Cells[8].Value = bot.Shoulder6Value;
                }
            }
        }

        private void CreateAppliedGrid()
        {
            _gridApplied = DataGridFactory.GetDataGridView(Forms.DataGridViewSelectionMode.FullRowSelect,
                Forms.DataGridViewAutoSizeRowsMode.AllCells);
            _gridApplied.ScrollBars = Forms.ScrollBars.Vertical;
            _gridApplied.Dock = Forms.DockStyle.Fill;
            _gridApplied.DataError += _grid_DataError;
            HostApplied.Child = _gridApplied;

            AddEmptyFillColumn(_gridApplied);
            AddTextColumn(_gridApplied, OsLocalization.CapitalAllocation.ColumnBot);
            AddTextColumn(_gridApplied, OsLocalization.CapitalAllocation.ColumnVolume);
            AddEmptyFillColumn(_gridApplied);
        }

        private void CreateSkippedGrid()
        {
            _gridSkipped = DataGridFactory.GetDataGridView(Forms.DataGridViewSelectionMode.FullRowSelect,
                Forms.DataGridViewAutoSizeRowsMode.AllCells);
            _gridSkipped.ScrollBars = Forms.ScrollBars.Vertical;
            _gridSkipped.Dock = Forms.DockStyle.Fill;
            _gridSkipped.DataError += _grid_DataError;
            HostSkipped.Child = _gridSkipped;

            AddEmptyFillColumn(_gridSkipped);
            AddTextColumn(_gridSkipped, OsLocalization.CapitalAllocation.ColumnBot);
            AddTextColumn(_gridSkipped, OsLocalization.CapitalAllocation.ColumnReason);
            AddEmptyFillColumn(_gridSkipped);
        }

        private void AddTextColumn(Forms.DataGridView grid, string header)
        {
            Forms.DataGridViewTextBoxColumn column = new Forms.DataGridViewTextBoxColumn();
            column.HeaderText = header;
            column.ReadOnly = true;
            column.AutoSizeMode = Forms.DataGridViewAutoSizeColumnMode.AllCells;
            grid.Columns.Add(column);
        }

        private void AddEmptyFillColumn(Forms.DataGridView grid)
        {
            Forms.DataGridViewTextBoxColumn column = new Forms.DataGridViewTextBoxColumn();
            column.HeaderText = "";
            column.ReadOnly = true;
            column.AutoSizeMode = Forms.DataGridViewAutoSizeColumnMode.Fill;
            grid.Columns.Add(column);
        }

        private void _grid_DataError(object sender, Forms.DataGridViewDataErrorEventArgs e)
        {
            ServerMaster.SendNewLogMessage(e.ToString(), LogMessageType.Error);
        }

        private void PaintApplied()
        {
            _gridApplied.Rows.Clear();

            for (int i = 0; i < _applyResult.Applied.Count; i++)
            {
                int rowIndex = _gridApplied.Rows.Add();
                _gridApplied.Rows[rowIndex].Cells[1].Value = _applyResult.Applied[i].Bot.NameStrategyUniq;
                _gridApplied.Rows[rowIndex].Cells[2].Value = _applyResult.Applied[i].Volume;
            }
        }

        private void PaintSkipped()
        {
            _gridSkipped.Rows.Clear();

            for (int i = 0; i < _applyResult.Skipped.Count; i++)
            {
                int rowIndex = _gridSkipped.Rows.Add();
                _gridSkipped.Rows[rowIndex].Cells[1].Value = _applyResult.Skipped[i].BotName;
                _gridSkipped.Rows[rowIndex].Cells[2].Value = _applyResult.Skipped[i].Reason;
            }
        }

        #endregion
    }
}
