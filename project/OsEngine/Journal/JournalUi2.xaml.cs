/*
 * Your rights to use code governed by this license http://o-s-a.net/doc/license_simple_engine.pdf
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms;
using OsEngine.Entity;
using OsEngine.Journal.Internal;
using OsEngine.Logging;
using Color = System.Drawing.Color;
using System.Windows.Forms.DataVisualization.Charting;
using OsEngine.Language;
using Chart = System.Windows.Forms.DataVisualization.Charting.Chart;
using ChartArea = System.Windows.Forms.DataVisualization.Charting.ChartArea;
using Series = System.Windows.Forms.DataVisualization.Charting.Series;
using System.Threading;
using OsEngine.Layout;
using OsEngine.Market;
using System.Windows.Media;
using OsEngine.OsData;

namespace OsEngine.Journal
{
    /// <summary>
    /// Interaction logic for JournalNewUi.xaml
    /// </summary>
    public partial class JournalUi2
    {
        #region Constructor

        public bool IsErase;

        public JournalUi2(List<BotPanelJournal> botsJournals, StartProgram startProgram)
        {
            InitializeComponent();
            OsEngine.Layout.StickyBorders.Listen(this);

            _startProgram = startProgram;
            _botsJournals = botsJournals;

            if (botsJournals.Count > 1)
            {
                LoadGroups();
            }

            ComboBoxChartType.Items.Add("Absolute");
            ComboBoxChartType.Items.Add("Percent 1 contract");
            ComboBoxChartType.SelectedItem = "Absolute";

            ComboBoxBenchmark.Items.Add(BenchmarkSecurity.Off.ToString());
            ComboBoxBenchmark.Items.Add(BenchmarkSecurity.BTC.ToString());
            ComboBoxBenchmark.Items.Add(BenchmarkSecurity.MCFTR.ToString());
            ComboBoxBenchmark.Items.Add(BenchmarkSecurity.SnP500.ToString());
            ComboBoxBenchmark.Items.Add(BenchmarkSecurity.IMOEX.ToString());
            ComboBoxBenchmark.SelectedItem = BenchmarkSecurity.Off.ToString();

            _currentCulture = OsLocalization.CurCulture;

            Task task = new Task(ThreadWorkerPlace);
            task.Start();

            Title = OsLocalization.Journal.TitleJournalUi;
            Label1.Content = OsLocalization.Journal.Label1;
            Label2.Content = OsLocalization.Journal.Label2;
            Label3.Content = OsLocalization.Journal.Label3;

            TabItem1.Header = OsLocalization.Journal.TabItem1;
            TabItem2.Header = OsLocalization.Journal.TabItem2;
            TabItem3.Header = OsLocalization.Journal.TabItem3;
            TabItem4.Header = OsLocalization.Journal.TabItem4;
            TabItem5.Header = OsLocalization.Journal.TabItem5;
            TabItem6.Header = OsLocalization.Journal.TabItem6;
            LabelFrom.Content = OsLocalization.Journal.Label5;
            LabelTo.Content = OsLocalization.Journal.Label6;
            ButtonReload.Content = OsLocalization.Journal.Label7;
            ButtonAutoReload.Content = OsLocalization.Journal.Label15;

            TabItemBotFilters.Header = OsLocalization.Journal.Label21;
            TabItemSecurityFilters.Header = OsLocalization.Journal.Label22;

            LabelVolumeShowNumbers.Content = OsLocalization.Journal.Label16;

            ComboBoxClosePosesOnPage.Items.Add(OsLocalization.Journal.Label18);
            ComboBoxClosePosesOnPage.Items.Add(OsLocalization.Journal.Label19);
            ComboBoxClosePosesOnPage.Items.Add(OsLocalization.Journal.Label20);
            ComboBoxClosePosesOnPage.SelectedItem = OsLocalization.Journal.Label18;
            ComboBoxClosePosesOnPage.SelectionChanged += ComboBoxClosePosesOnPage_SelectionChanged;
            ComboBoxClosePosesShowNumbers.SelectionChanged += ComboBoxClosePosesShowNumbers_SelectionChanged;

            ComboBoxOpenPosesOnPage.Items.Add(OsLocalization.Journal.Label18);
            ComboBoxOpenPosesOnPage.Items.Add(OsLocalization.Journal.Label19);
            ComboBoxOpenPosesOnPage.Items.Add(OsLocalization.Journal.Label20);
            ComboBoxOpenPosesOnPage.SelectedItem = OsLocalization.Journal.Label18;
            ComboBoxOpenPosesOnPage.SelectionChanged += ComboBoxOpenPosesOnPage_SelectionChanged;
            ComboBoxOpenPosesShowNumbers.SelectionChanged += ComboBoxOpenPosesShowNumbers_SelectionChanged;

            ButtonAutoReload.Click += ButtonAutoReload_Click;
            ButtonAutoReload.IsChecked = false;

            LabelEqutyCharteType.Content = OsLocalization.Journal.Label8;
            LabelBenchmark.Content = OsLocalization.Journal.Label23;

            TabItemSecurities.Header = OsLocalization.Journal.TabItemSecurities;
            TabItemPortfolio.Header = OsLocalization.Journal.TabItemPortfolio;

            CreatePositionsLists();

            SelectOpenPosesPages();
            SelectCLosePosesPages();

            CreateSecuritiesFilterGrid();
            PaintSecuritiesFilterGrid();
            UpDateSelectedSecurities();

            Closing += JournalUi_Closing;

            ButtonShowLeftPanel.Visibility = Visibility.Hidden;

            CreateBotsGrid();
            PaintBotsGrid();

            Thread task2 = new Thread(LeftBotsPanelPainterThread);
            task2.Start();

            CreateSlidersShowPositions();

            this.Activate();
            this.Focus();

            string botNames = "";

            for (int i = 0; i < botsJournals.Count; i++)
            {
                botNames += botsJournals[i].BotName;
            }

            if (botsJournals.Count > 1)
            {
                JournalName = "Journal2Ui_" + "CommonJournal" + startProgram.ToString();
            }
            else
            {
                JournalName = "Journal2Ui_" + botNames + startProgram.ToString();
            }

            LoadSettings();

            ComboBoxChartType.SelectionChanged += ComboBoxChartType_SelectionChanged;
            TabControlPrime.SelectionChanged += TabControlPrime_SelectionChanged;
            TabControlVolume.SelectionChanged += TabControlVolume_SelectionChanged;
            ComboBoxBenchmark.SelectionChanged += ComboBoxBenchmark_SelectionChanged;
            VolumeShowNumbers.SelectionChanged += VolumeShowNumbers_SelectionChanged;

            CheckBoxShowDontOpenPoses.Click += CheckBoxShowDontOpenPoses_Click;
            CheckBoxShowDontOpenPoses.Content = OsLocalization.Journal.Label17;

            GlobalGUILayout.Listen(this, JournalName);

            RePaint();
        }

        private void JournalUi_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            try
            {
                IsErase = true;

                TabControlPrime.SelectionChanged -= TabControlPrime_SelectionChanged;
                TabControlVolume.SelectionChanged -= TabControlVolume_SelectionChanged;
                ComboBoxChartType.SelectionChanged -= ComboBoxChartType_SelectionChanged;
                VolumeShowNumbers.SelectionChanged -= VolumeShowNumbers_SelectionChanged;
                ComboBoxBenchmark.SelectionChanged -= ComboBoxBenchmark_SelectionChanged;
                TabControlPrime.Items.Clear();
                TabControlVolume.Items.Clear();

                Closing -= JournalUi_Closing;
                _botsJournals.Clear();
                _botsJournals = null;

                if (_allPositions != null)
                {
                    _allPositions = null;
                }

                if (_longPositions != null)
                {
                    _longPositions = null;
                }

                if (_shortPositions != null)
                {
                    _shortPositions = null;
                }

                if (_chartEquity != null)
                {
                    _chartEquity.MouseMove -= _chartEquity_MouseMove;
                    _chartEquity.MouseWheel -= _chartEquity_MouseWheel;
                    RectangleEquity.MouseDown -= RectangleEquity_MouseDown;
                    RectangleLong.MouseDown -= RectangleLong_MouseDown;
                    RectangleShort.MouseDown -= RectangleShort_MouseDown;

                    _chartEquity.Series.Clear();
                    _chartEquity.ChartAreas.Clear();
                    _chartEquity = null;
                    HostEquity.Child.Hide();
                    HostEquity.Child = null;
                    HostEquity = null;
                }

                if (_chartVolume != null)
                {
                    _chartVolume.Series.Clear();
                    _chartVolume.ChartAreas.Clear();
                    _chartVolume.Click -= _chartVolume_Click;
                    _chartVolume = null;
                    HostVolume.Child.Hide();
                    HostVolume.Child = null;
                    HostVolume = null;
                }

                if (_chartPortfolio != null)
                {
                    _chartPortfolio.MouseMove -= _chartEquity_MouseMove;
                    _chartPortfolio.MouseWheel -= _chartEquity_MouseWheel;
                    _chartPortfolio.Series.Clear();
                    _chartPortfolio.ChartAreas.Clear();
                    _chartPortfolio = null;
                }

                if (_gridLeveragePortfolio != null)
                {
                    DataGridFactory.ClearLinks(_gridLeveragePortfolio);
                    _gridLeveragePortfolio.Rows.Clear();
                    _gridLeveragePortfolio.DataError -= _gridLeveragePortfolio_DataError;
                    _gridLeveragePortfolio.Dispose();
                    _gridLeveragePortfolio = null;                   
                }

                if (_layoutPanelPortfolio != null)
                {
                    _layoutPanelPortfolio.Controls.Clear();
                    _layoutPanelPortfolio = null;
                    HostVolumePortfolio.Child.Hide();
                    HostVolumePortfolio.Child = null;
                    HostVolumePortfolio = null;
                }

                if (_chartDd != null)
                {
                    _chartDd.Series.Clear();
                    _chartDd.ChartAreas.Clear();
                    _chartDd = null;
                    HostDrawdown.Child.Hide();
                    HostDrawdown.Child = null;
                    HostDrawdown = null;
                }

                if (_gridStatistics != null)
                {
                    DataGridFactory.ClearLinks(_gridStatistics);
                    _gridStatistics.Rows.Clear();
                    _gridStatistics.DataError -= _gridStatistics_DataError;
                    _gridStatistics.Dispose();
                    _gridStatistics = null;
                    HostStatistics.Child.Hide();
                    HostStatistics.Child = null;
                    HostStatistics = null;
                }

                if (_openPositionGrid != null)
                {
                    DataGridFactory.ClearLinks(_openPositionGrid);
                    _openPositionGrid.Rows.Clear();
                    _openPositionGrid.Click -= _openPositionGrid_Click;
                    _openPositionGrid.DoubleClick -= _openPositionGrid_DoubleClick;
                    _openPositionGrid.DataError -= _gridStatistics_DataError;
                    _openPositionGrid.Dispose();
                    _openPositionGrid = null;

                    if (HostOpenPosition.Child != null)
                    {
                        HostOpenPosition.Child.Hide();
                        HostOpenPosition.Child = null;
                    }

                    HostOpenPosition = null;
                }

                if (_closePositionGrid != null)
                {
                    DataGridFactory.ClearLinks(_closePositionGrid);
                    _closePositionGrid.Rows.Clear();
                    _closePositionGrid.Click -= _closePositionGrid_Click;
                    _closePositionGrid.DoubleClick -= _closePositionGrid_DoubleClick;
                    _closePositionGrid.DataError -= _gridStatistics_DataError;
                    _closePositionGrid.Dispose();
                    _closePositionGrid = null;

                    if (HostClosePosition.Child != null)
                    {
                        HostClosePosition.Child.Hide();
                        HostClosePosition.Child = null;
                    }

                    HostClosePosition = null;
                }

                if (_gridLeftBotsPanel != null)
                {
                    HostBotsSelected.Child = null;
                    _gridLeftBotsPanel.CellEndEdit -= _gridLeftBotsPanel_CellEndEdit;
                    _gridLeftBotsPanel.CellBeginEdit -= _gridLeftBotsPanel_CellBeginEdit;
                    _gridLeftBotsPanel.DataError -= _gridStatistics_DataError;
                    DataGridFactory.ClearLinks(_gridLeftBotsPanel);
                    _gridLeftBotsPanel.Dispose();
                    _gridLeftBotsPanel = null;
                }

                if (_gridLeftSecuritiesPanel != null)
                {
                    HostSecuritiesSelected.Child = null;
                    _gridLeftSecuritiesPanel.CellClick -= _gridLeftSecuritiesPanel_CellClick;
                    _gridLeftSecuritiesPanel.DataError -= _gridStatistics_DataError;
                    DataGridFactory.ClearLinks(_gridLeftSecuritiesPanel);
                    _gridLeftSecuritiesPanel.Dispose();
                    _gridLeftSecuritiesPanel = null;
                }

            }
            catch (Exception ex)
            {
                SendNewLogMessage(ex.ToString(), LogMessageType.Error);
            }
        }

        private StartProgram _startProgram;

        private CultureInfo _currentCulture;

        public string JournalName;

        #endregion

        #region Auto update thread

        private bool _autoReloadIsOn;

        private void ButtonAutoReload_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                _autoReloadIsOn = ButtonAutoReload.IsChecked.Value;
            }
            catch (Exception ex)
            {
                SendNewLogMessage(ex.ToString(), LogMessageType.Error);
            }
        }

        private async void ThreadWorkerPlace()
        {
            if (_startProgram != StartProgram.IsOsTrader)
            {
                return;
            }

            while (true)
            {
                try
                {
                    await Task.Delay(30000);

                    if (IsErase == true)
                    {
                        return;
                    }

                    if (_autoReloadIsOn == false)
                    {
                        continue;
                    }

                    RePaint();
                }
                catch (Exception error)
                {
                    SendNewLogMessage(error.ToString(), LogMessageType.Error);
                }
            }
        }

        #endregion

        #region Main Paint Methods

        public void RePaint()
        {
            try
            {
                CreatePositionsLists();

                if (!TabControlPrime.CheckAccess())
                {
                    TabControlPrime.Dispatcher.Invoke(RePaint);
                    return;
                }

                if (IsErase == true)
                {
                    return;
                }

                if (_allPositions == null)
                {
                    return;
                }

                List<Position> allSortPoses = new List<Position>();
                List<Position> longPositions = new List<Position>();
                List<Position> shortPositions = new List<Position>();

                for (int i = 0; i < _allPositions.Count; i++)
                {
                    if (_allPositions[i] == null)
                    {
                        continue;
                    }
                    if (_allPositions[i].TimeCreate < _startTime
                        || _allPositions[i].TimeCreate > _endTime)
                    {
                        continue;
                    }
                    allSortPoses.Add(_allPositions[i]);
                }

                for (int i = 0; i < _longPositions.Count; i++)
                {
                    if (_longPositions[i] == null)
                    {
                        continue;
                    }
                    if (_longPositions[i].TimeCreate < _startTime
                        || _longPositions[i].TimeCreate > _endTime)
                    {
                        continue;
                    }
                    longPositions.Add(_longPositions[i]);
                }

                for (int i = 0; i < _shortPositions.Count; i++)
                {
                    if (_shortPositions[i] == null)
                    {
                        continue;
                    }
                    if (_shortPositions[i].TimeCreate < _startTime
                        || _shortPositions[i].TimeCreate > _endTime)
                    {
                        continue;
                    }
                    shortPositions.Add(_shortPositions[i]);
                }

                lock (_paintLocker)
                {
                    if (TabControlPrime.SelectedIndex == -1 ||
                        TabControlPrime.SelectedIndex == 0)
                    {
                        PaintProfitOnChart(allSortPoses);
                    }
                    else if (TabControlPrime.SelectedIndex == 1)
                    {
                        bool needShowTickState = !(_botsJournals.Count > 1);

                        PaintStatTable(allSortPoses, longPositions, shortPositions, needShowTickState);
                    }
                    else if (TabControlPrime.SelectedIndex == 2)
                    {
                        PaintDrawDown(allSortPoses);
                    }
                    else if (TabControlPrime.SelectedIndex == 3)
                    {
                        PaintVolume(allSortPoses);
                    }
                    else if (TabControlPrime.SelectedIndex == 4)
                    {
                        PaintOpenPositionGrid(allSortPoses);
                    }
                    else if (TabControlPrime.SelectedIndex == 5)
                    {
                        PaintClosePositionGrid();
                    }

                    PaintTitleAbsProfit(allSortPoses);
                }
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        private void PaintTitleAbsProfit(List<Position> positionsAll)
        {
            decimal absProfit = PositionStatisticGenerator.GetAllProfitInAbsolute(positionsAll.ToArray());

            if (absProfit != 0)
            {
                absProfit = Math.Round(absProfit, 3);

                Title = OsLocalization.Journal.TitleJournalUi
                    + ".  " + OsLocalization.Journal.Label1 + ": " + absProfit;
            }
            else
            {
                if (Title != OsLocalization.Journal.TitleJournalUi)
                {
                    Title = OsLocalization.Journal.TitleJournalUi;
                }
            }

        }

        private string _paintLocker = "journalPainterLocker";

        private void ComboBoxChartType_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            try
            {
                RePaint();
                SaveSettings();
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        private void TabControlPrime_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            try
            {
                if (_volumeControlUpdated == true)
                {
                    _volumeControlUpdated = false;
                    return;
                }
                RePaint();
                _volumeControlUpdated = false;
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        private void TabControlLeftSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            try
            {
                RePaint();
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        private int _countLoadBenchmark = 0;

        private void ComboBoxBenchmark_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            try
            {
                _countLoadBenchmark = 0;
                _checkBenchmarkData = false;

                RePaint();
                SaveSettings();
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        #endregion

        #region Statistics table

        private DataGridView _gridStatistics;

        public void CreateTableToStatistic()
        {
            try
            {
                _gridStatistics = DataGridFactory.GetDataGridView(DataGridViewSelectionMode.FullRowSelect, DataGridViewAutoSizeRowsMode.None);

                _gridStatistics.AllowUserToResizeRows = false;

                CustomDataGridViewCell cell0 = new CustomDataGridViewCell();
                cell0.Style = _gridStatistics.DefaultCellStyle;
                cell0.AdvancedBorderStyle = new DataGridViewAdvancedBorderStyle
                {
                    Bottom = DataGridViewAdvancedCellBorderStyle.None,
                    Top = DataGridViewAdvancedCellBorderStyle.None,
                    Left = DataGridViewAdvancedCellBorderStyle.Inset,
                    Right = DataGridViewAdvancedCellBorderStyle.Inset
                };

                HostStatistics.Child = _gridStatistics;
                HostStatistics.Child.Show();
                _gridStatistics.DataError += _gridStatistics_DataError;

                DataGridViewColumn column0 = new DataGridViewColumn();
                column0.CellTemplate = cell0;
                column0.HeaderText = @"";
                column0.ReadOnly = true;
                column0.Width = 200;

                _gridStatistics.Columns.Add(column0);

                DataGridViewColumn column1 = new DataGridViewColumn();
                column1.CellTemplate = cell0;
                column1.HeaderText = OsLocalization.Journal.GridColumn1;
                column1.ReadOnly = true;
                column1.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
                _gridStatistics.Columns.Add(column1);


                DataGridViewColumn column2 = new DataGridViewColumn();
                column2.CellTemplate = cell0;
                column2.HeaderText = OsLocalization.Journal.GridColumn2;
                column2.ReadOnly = true;
                column2.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
                _gridStatistics.Columns.Add(column2);

                DataGridViewColumn column3 = new DataGridViewColumn();
                column3.CellTemplate = cell0;
                column3.HeaderText = OsLocalization.Journal.GridColumn3;
                column3.ReadOnly = true;
                column3.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
                _gridStatistics.Columns.Add(column3);

                for (int i = 0; i < 31; i++)
                {
                    _gridStatistics.Rows.Add(); //string addition/ добавление строки
                }

                _gridStatistics.Rows[0].Cells[0].Value = OsLocalization.Journal.GridRow1;
                _gridStatistics.Rows[1].Cells[0].Value = OsLocalization.Journal.GridRow2;
                _gridStatistics.Rows[2].Cells[0].Value = OsLocalization.Journal.GridRow3;
                _gridStatistics.Rows[3].Cells[0].Value = OsLocalization.Journal.GridRow17;
                _gridStatistics.Rows[4].Cells[0].Value = OsLocalization.Journal.GridRow18;

                _gridStatistics.Rows[5].Cells[0].Value = OsLocalization.Journal.GridRow4;
                _gridStatistics.Rows[6].Cells[0].Value = OsLocalization.Journal.GridRow5;

                _gridStatistics.Rows[8].Cells[0].Value = OsLocalization.Journal.GridRow6;
                _gridStatistics.Rows[9].Cells[0].Value = OsLocalization.Journal.GridRow7;
                _gridStatistics.Rows[10].Cells[0].Value = OsLocalization.Journal.GridRow8;
                _gridStatistics.Rows[11].Cells[0].Value = OsLocalization.Journal.GridRow9;


                _gridStatistics.Rows[13].Cells[0].Value = OsLocalization.Journal.GridRow10;
                _gridStatistics.Rows[14].Cells[0].Value = OsLocalization.Journal.GridRow11;
                _gridStatistics.Rows[15].Cells[0].Value = OsLocalization.Journal.GridRow6;
                _gridStatistics.Rows[16].Cells[0].Value = OsLocalization.Journal.GridRow7;
                _gridStatistics.Rows[17].Cells[0].Value = OsLocalization.Journal.GridRow8;
                _gridStatistics.Rows[18].Cells[0].Value = OsLocalization.Journal.GridRow9;
                _gridStatistics.Rows[19].Cells[0].Value = OsLocalization.Journal.GridRow12;

                _gridStatistics.Rows[21].Cells[0].Value = OsLocalization.Journal.GridRow13;
                _gridStatistics.Rows[22].Cells[0].Value = OsLocalization.Journal.GridRow14;
                _gridStatistics.Rows[23].Cells[0].Value = OsLocalization.Journal.GridRow6;
                _gridStatistics.Rows[24].Cells[0].Value = OsLocalization.Journal.GridRow7;
                _gridStatistics.Rows[25].Cells[0].Value = OsLocalization.Journal.GridRow8;
                _gridStatistics.Rows[26].Cells[0].Value = OsLocalization.Journal.GridRow9;
                _gridStatistics.Rows[27].Cells[0].Value = OsLocalization.Journal.GridRow12;
                _gridStatistics.Rows[28].Cells[0].Value = "";
                _gridStatistics.Rows[29].Cells[0].Value = OsLocalization.Journal.GridRow15;
                _gridStatistics.Rows[30].Cells[0].Value = OsLocalization.Journal.GridRow16;
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        private void _gridStatistics_DataError(object sender, DataGridViewDataErrorEventArgs e)
        {
            ServerMaster.SendNewLogMessage(e.ToString(), Logging.LogMessageType.Error);
        }

        private void PaintStatTable(List<Position> positionsAll, List<Position> positionsLong, List<Position> positionsShort, bool needShowTickState)
        {
            try
            {
                if (_gridStatistics == null)
                {
                    CreateTableToStatistic();
                }

                List<string> positionsAllState = PositionStatisticGenerator.GetStatisticNew(positionsAll);
                List<string> positionsLongState = PositionStatisticGenerator.GetStatisticNew(positionsLong);
                List<string> positionsShortState = PositionStatisticGenerator.GetStatisticNew(positionsShort);

                if (positionsAllState == null)
                {
                    for (int i = 0; i < 31; i++)
                    {
                        _gridStatistics.Rows[i].Cells[1].Value = "";
                    }
                }
                if (positionsLongState == null)
                {
                    for (int i = 0; i < 31; i++)
                    {
                        _gridStatistics.Rows[i].Cells[2].Value = "";
                    }
                }
                if (positionsShortState == null)
                {
                    for (int i = 0; i < 31; i++)
                    {
                        _gridStatistics.Rows[i].Cells[3].Value = "";
                    }
                }
                if (positionsLongState != null)
                {
                    for (int i = 0; i < 31; i++)
                    {
                        _gridStatistics.Rows[i].Cells[2].Value = positionsLongState[i].ToString();
                    }
                }
                if (positionsShortState != null)
                {
                    for (int i = 0; i < 31; i++)
                    {
                        _gridStatistics.Rows[i].Cells[3].Value = positionsShortState[i].ToString();
                    }
                }
                if (positionsAllState != null)
                {
                    for (int i = 0; i < 31; i++)
                    {
                        _gridStatistics.Rows[i].Cells[1].Value = positionsAllState[i].ToString();
                    }
                }
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        #endregion

        #region Equity chart

        private Chart _chartEquity;

        private void CreateChartProfit()
        {
            try
            {
                _chartEquity = new Chart();
                HostEquity.Child = _chartEquity;
                HostEquity.Child.Show();

                _chartEquity.Series.Clear();
                _chartEquity.ChartAreas.Clear();
                _chartEquity.BackColor = Color.FromArgb(17, 18, 23);

                ChartArea areaLineProfit = new ChartArea("ChartAreaProfit");
                areaLineProfit.Position.Height = 80;
                areaLineProfit.Position.Width = 100;
                areaLineProfit.Position.Y = 0;
                areaLineProfit.CursorX.IsUserSelectionEnabled = true; //allow the user to change the view scope/ разрешаем пользователю изменять рамки представления
                areaLineProfit.CursorX.IsUserEnabled = true; //trait/чертa

                _chartEquity.ChartAreas.Add(areaLineProfit);

                ChartArea areaLineProfitBar = new ChartArea("ChartAreaProfitBar");
                areaLineProfitBar.AlignWithChartArea = "ChartAreaProfit";
                areaLineProfitBar.Position.Height = 20;
                areaLineProfitBar.Position.Width = 100;
                areaLineProfitBar.Position.Y = 80;
                areaLineProfitBar.AxisX.Enabled = AxisEnabled.False;
                areaLineProfitBar.CursorX.IsUserEnabled = true; //trait/чертa

                _chartEquity.ChartAreas.Add(areaLineProfitBar);

                for (int i = 0; i < _chartEquity.ChartAreas.Count; i++)
                {
                    _chartEquity.ChartAreas[i].BorderColor = Color.Black;
                    _chartEquity.ChartAreas[i].BackColor = Color.FromArgb(17, 18, 23);
                    _chartEquity.ChartAreas[i].CursorY.LineColor = Color.Gainsboro;
                    _chartEquity.ChartAreas[i].CursorX.LineColor = Color.Black;
                    _chartEquity.ChartAreas[i].AxisX.TitleForeColor = Color.Gainsboro;
                    _chartEquity.ChartAreas[i].AxisY.TitleForeColor = Color.Gainsboro;

                    foreach (var axe in _chartEquity.ChartAreas[i].Axes)
                    {
                        axe.LabelStyle.ForeColor = Color.Gainsboro;
                    }
                }

                _chartEquity.MouseMove += _chartEquity_MouseMove;
                _chartEquity.MouseWheel += _chartEquity_MouseWheel;
                RectangleEquity.MouseDown += RectangleEquity_MouseDown;
                RectangleLong.MouseDown += RectangleLong_MouseDown;
                RectangleShort.MouseDown += RectangleShort_MouseDown;

            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        private bool _visibleEquityLine = true;
        private bool _visibleLongLine = true;
        private bool _visibleShortLine = true;

        private void RectangleEquity_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            try
            {
                if (_visibleEquityLine)
                {
                    _visibleEquityLine = false;
                }
                else
                {
                    _visibleEquityLine = true;
                }

                RePaint();
                SaveSettings();
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        private void RectangleLong_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            try
            {
                if (_visibleLongLine)
                {
                    _visibleLongLine = false;
                }
                else
                {
                    _visibleLongLine = true;
                }

                RePaint();
                SaveSettings();
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        private void RectangleShort_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            try
            {
                if (_visibleShortLine)
                {
                    _visibleShortLine = false;
                }
                else
                {
                    _visibleShortLine = true;
                }

                RePaint();
                SaveSettings();
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        private void _chartEquity_MouseWheel(object sender, MouseEventArgs e)
        {
            try
            {
                if (_chartEquity.ChartAreas[0].AxisX.ScaleView.IsZoomed)
                {
                    _chartEquity.ChartAreas[0].AxisX.ScaleView.ZoomReset();
                }
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        private int _lastMouseXValue = -1;

        private void _chartEquity_MouseMove(object sender, MouseEventArgs e)
        {
            try
            {
                if (_chartEquity.Series == null
                    || _chartEquity.Series.Count == 0)
                {
                    return;
                }
                if (_chartEquity.ChartAreas[0].AxisX.ScaleView.Size == double.NaN)
                {
                    return;
                }

                if (e.X == _lastMouseXValue)
                {
                    return;
                }

                _lastMouseXValue = e.X;

                int curCountOfPoints = 0;

                if (_chartEquity.ChartAreas[0].AxisX.ScaleView.IsZoomed)
                {
                    curCountOfPoints = Convert.ToInt32(_chartEquity.ChartAreas[0].AxisX.ScaleView.Size);
                }
                else
                {
                    curCountOfPoints = _chartEquity.Series[0].Points.Count;
                }

                double sizeArea = _chartEquity.ChartAreas[0].InnerPlotPosition.Size.Width;
                double allSizeAbs = _chartEquity.Size.Width * (sizeArea / 100);

                double onePointLen = allSizeAbs / curCountOfPoints;

                double curMousePosAbs = e.X;

                double curPointNum = curMousePosAbs / onePointLen - 1;
                try
                {
                    if (Double.IsInfinity(curPointNum))
                    {
                        return;
                    }

                    curPointNum = Convert.ToDouble(Convert.ToInt32(curPointNum));
                }
                catch
                {
                    return;
                }

                int firstPoint = 0;

                if (_chartEquity.ChartAreas[0].AxisX.ScaleView.IsZoomed)
                {
                    firstPoint = Convert.ToInt32(_chartEquity.ChartAreas[0].AxisX.ScaleView.Position);
                    curPointNum = firstPoint + curPointNum;
                }

                //_chartEquity.ChartAreas[0].CursorX.Position = curPointNum;
                if (_chartEquity.ChartAreas[0].CursorX.Position != curPointNum)
                {
                    _chartEquity.ChartAreas[0].CursorX.SetCursorPosition(curPointNum);
                }
                else
                {
                    return;
                }

                int numPointInt = Convert.ToInt32(curPointNum);

                if (numPointInt <= 0)
                {
                    return;
                }

                for (int i = 0; i < _chartEquity.Series.Count; i++)
                {
                    if (_chartEquity.Series[i].Points.Count > _lastSeriesEquityChartPointWithLabel)
                    {
                        _chartEquity.Series[i].Points[_lastSeriesEquityChartPointWithLabel].Label = "";
                    }
                    if (_chartEquity.Series[i].Points.Count > numPointInt)
                    {
                        _chartEquity.Series[i].Points[numPointInt].Label
                        = _chartEquity.Series[i].Points[numPointInt].AxisLabel;
                    }
                }

                _lastSeriesEquityChartPointWithLabel = numPointInt;
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        private int _lastSeriesEquityChartPointWithLabel = 0;

        private decimal _startValuePortfolio;

        private void PaintProfitOnChart(List<Position> positionsAll)
        {
            try
            {
                if (!GridTabPrime.Dispatcher.CheckAccess())
                {
                    GridTabPrime.Dispatcher.Invoke(
                        new Action<List<Position>>(PaintProfitOnChart), positionsAll);
                    return;
                }

                if (_chartEquity == null)
                {
                    CreateChartProfit();
                }

                _chartEquity.Series.Clear();

                if (positionsAll == null)
                {
                    return;
                }

                Series profit = new Series("SeriesProfit");
                profit.ChartType = SeriesChartType.Line;
                profit.Color = Color.White;   //DeepSkyBlue;
                profit.LabelForeColor = Color.White;
                profit.YAxisType = AxisType.Secondary;
                profit.ChartArea = "ChartAreaProfit";
                profit.BorderWidth = 4;
                profit.ShadowOffset = 2;

                Series profitLong = new Series("SeriesProfitLong");
                profitLong.ChartType = SeriesChartType.Line;
                profitLong.Color = Color.DeepSkyBlue;   //DeepSkyBlue;
                profitLong.LabelForeColor = Color.DeepSkyBlue;
                profitLong.YAxisType = AxisType.Secondary;
                profitLong.ChartArea = "ChartAreaProfit";
                profitLong.BorderWidth = 2;
                profitLong.ShadowOffset = 2;

                Series profitShort = new Series("SeriesProfitShort");
                profitShort.ChartType = SeriesChartType.Line;
                profitShort.Color = Color.DarkOrange;  //DeepSkyBlue;
                profitShort.LabelForeColor = Color.DarkOrange;
                profitShort.YAxisType = AxisType.Secondary;
                profitShort.ChartArea = "ChartAreaProfit";
                profitShort.ShadowOffset = 2;
                profitShort.BorderWidth = 2;

                Series profitBar = new Series("SeriesProfitBar");
                profitBar.ChartType = SeriesChartType.Column;
                profitBar.YAxisType = AxisType.Secondary;
                profitBar.LabelForeColor = Color.White;
                profitBar.ChartArea = "ChartAreaProfitBar";
                profitBar.ShadowOffset = 2;

                Series nullLine = new Series("SeriesNullLine");
                nullLine.ChartType = SeriesChartType.Line;
                nullLine.YAxisType = AxisType.Secondary;
                nullLine.LabelForeColor = Color.White;
                nullLine.ChartArea = "ChartAreaProfit";
                nullLine.ShadowOffset = 0;

                decimal profitSum = 0;
                decimal profitSumLong = 0;
                decimal profitSumShort = 0;
                decimal maxYVal = decimal.MinValue;
                decimal minYval = decimal.MaxValue;

                decimal maxYValBars = 0;
                decimal minYvalBars = decimal.MaxValue;

                decimal curProfit = 0;
                string chartType = ComboBoxChartType.SelectedItem.ToString();

                for (int i = 0; i < positionsAll.Count; i++)
                {
                    decimal curMult = positionsAll[i].MultToJournal;

                    if (chartType == "Absolute")
                    {
                        curProfit = positionsAll[i].ProfitPortfolioAbs * (curMult / 100);
                    }
                    else if (chartType == "Percent 1 contract")
                    {
                        curProfit = positionsAll[i].ProfitOperationPercent * (curMult / 100);
                    }

                    curProfit = Math.Round(curProfit, 8);

                    if (curProfit > maxYValBars)
                    {
                        maxYValBars = curProfit;
                    }

                    if (curProfit < minYvalBars)
                    {
                        minYvalBars = curProfit;
                    }

                    profitSum += curProfit;
                    profit.Points.AddXY(i, Math.Round(profitSum, 3));

                    nullLine.Points.AddXY(i, 0);
                    nullLine.Points[nullLine.Points.Count - 1].AxisLabel =
                        positionsAll[i].TimeCreate.ToString(_currentCulture);

                    profit.Points[profit.Points.Count - 1].AxisLabel = Math.Round(profitSum, 3).ToString();

                    profitBar.Points.AddXY(i, Math.Round(curProfit, 3));

                    profitBar.Points[profitBar.Points.Count - 1].LabelForeColor = Color.DarkOrange;
                    profitBar.Points[profitBar.Points.Count - 1].AxisLabel
                        = positionsAll[i].SecurityName + "\n" +
                          Math.Round(curProfit, 3).ToString() + "\n" +
                          positionsAll[i].NameBot;

                    if (positionsAll[i].Direction == Side.Buy)
                    {
                        profitSumLong += curProfit;
                    }

                    if (positionsAll[i].Direction == Side.Sell)
                    {
                        profitSumShort += curProfit;
                    }

                    if (profitSum > maxYVal)
                    {
                        maxYVal = profitSum;
                    }
                    if (profitSumLong > maxYVal)
                    {
                        maxYVal = profitSumLong;
                    }
                    if (profitSumShort > maxYVal)
                    {
                        maxYVal = profitSumShort;
                    }

                    if (profitSum < minYval)
                    {
                        minYval = profitSum;
                    }
                    if (profitSumLong < minYval)
                    {
                        minYval = profitSumLong;
                    }
                    if (profitSumShort < minYval)
                    {
                        minYval = profitSumShort;
                    }

                    profitLong.Points.AddXY(i, Math.Round(profitSumLong, 3));
                    profitLong.Points[profitLong.Points.Count - 1].AxisLabel = Math.Round(profitSumLong, 3).ToString();

                    profitShort.Points.AddXY(i, Math.Round(profitSumShort, 3));
                    profitShort.Points[profitShort.Points.Count - 1].AxisLabel = Math.Round(profitSumShort, 3).ToString();

                    if (positionsAll[i].State != PositionStateType.Done)
                    {
                        profitBar.Points[profitBar.Points.Count - 1].Color = Color.BlueViolet;
                    }
                    else if (curProfit > 0)
                    {
                        profitBar.Points[profitBar.Points.Count - 1].Color = Color.Gainsboro;
                    }
                    else
                    {
                        profitBar.Points[profitBar.Points.Count - 1].Color = Color.DarkRed;
                    }
                }

                if (_visibleEquityLine)
                {
                    _chartEquity.Series.Add(profit);
                }

                if (_visibleLongLine)
                {
                    _chartEquity.Series.Add(profitLong);
                }

                if (_visibleShortLine)
                {
                    _chartEquity.Series.Add(profitShort);
                }

                _chartEquity.Series.Add(profitBar);
                _chartEquity.Series.Add(nullLine);

                if (chartType == "Absolute")
                {
                    ComboBoxBenchmark.IsEnabled = true;
                }
                else
                {
                    ComboBoxBenchmark.IsEnabled = false;
                }

                if (ComboBoxBenchmark.SelectedItem.ToString() != BenchmarkSecurity.Off.ToString() &&
                    chartType == "Absolute")
                {
                    _startValuePortfolio = positionsAll[0].PortfolioValueOnOpenPosition;

                    Series benchmarkLine = GetBenchmarkPoints(nullLine, maxYVal, minYval);

                    if (benchmarkLine != null)
                    {
                        _chartEquity.Series.Add(benchmarkLine);

                        if (_benchmark != null)
                        {
                            _benchmark.NewLogMessageEvent -= SendNewLogMessage;
                            _benchmark.DownloadBenchmarkEvent -= Benchmark_DownloadBenchmarkEvent;
                            _benchmark = null;
                        }

                        for (int i = 0; i < benchmarkLine.Points.Count; i++)
                        {
                            decimal benchmarkValue = (decimal)benchmarkLine.Points[i].YValues[0];

                            if (benchmarkValue > maxYVal)
                            {
                                maxYVal = benchmarkValue;
                            }

                            if (benchmarkValue < minYval)
                            {
                                minYval = benchmarkValue;
                            }
                        }
                    }                    
                }

                if (minYval != decimal.MaxValue &&
                    maxYVal != decimal.MinValue &&
                    minYval != maxYVal)
                {
                    decimal chartHeigh = maxYVal - minYval;

                    maxYVal = Math.Round(maxYVal + chartHeigh * 0.05m, 5);
                    minYval = Math.Round(minYval - chartHeigh * 0.05m, 5);

                    if (maxYVal != minYval)
                    {
                        _chartEquity.ChartAreas[0].AxisY2.Maximum = (double)maxYVal;
                        _chartEquity.ChartAreas[0].AxisY2.Minimum = (double)minYval;
                    }
                }

                if (minYvalBars != decimal.MaxValue &&
                    maxYValBars != 0 &&
                    minYvalBars != maxYValBars)
                {
                    decimal chartHeigh = maxYValBars - minYvalBars;
                    maxYValBars = Math.Round(maxYValBars + chartHeigh * 0.05m, 5);
                    minYvalBars = Math.Round(minYvalBars - chartHeigh * 0.05m, 5);

                    if (maxYValBars != minYvalBars)
                    {
                        _chartEquity.ChartAreas[1].AxisY2.Maximum = (double)maxYValBars;
                        _chartEquity.ChartAreas[1].AxisY2.Minimum = (double)minYvalBars;
                    }
                }

                PaintXLabelsOnEquityChart(positionsAll);

                PaintRectangleEqutyLines();                
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        private Benchmark _benchmark;
        private bool _checkBenchmarkData = false;

        private Series GetBenchmarkPoints(Series series, decimal maxYVal, decimal minYVal)
        {
            try
            {
                _benchmark = new Benchmark(ComboBoxBenchmark.SelectedItem.ToString());
                _benchmark.NewLogMessageEvent += SendNewLogMessage;
                _benchmark.DownloadBenchmarkEvent += Benchmark_DownloadBenchmarkEvent;                

                List <decimal> data = LoadBenchmarkData(series);

                if (data == null && !_checkBenchmarkData)
                {
                    _checkBenchmarkData = true;
                    _countLoadBenchmark++;

                    _benchmark.GetData(series);
                }
                else
                {
                    return ScaleDataToChart(data, minYVal, maxYVal);
                }

                return null;
            }
            catch (Exception ex)
            {
                SendNewLogMessage(ex.ToString(), LogMessageType.Error);
                return null;
            }            
        }

        private void Benchmark_DownloadBenchmarkEvent()
        {
            try
            {
                if (_benchmark != null)
                {
                    _benchmark.NewLogMessageEvent -= SendNewLogMessage;
                    _benchmark.DownloadBenchmarkEvent -= Benchmark_DownloadBenchmarkEvent;
                    _benchmark = null;
                }

                _checkBenchmarkData = false;

                RePaint();
            }
            catch (Exception ex)
            {
                SendNewLogMessage(ex.ToString(), LogMessageType.Error);
            }
        }

        private List<decimal> LoadBenchmarkData(Series series)
        {
            try
            {
                if (!File.Exists(_benchmark.FileSetBenchmark))
                {
                    return null;
                }

                List<DateTime> listData = new();
                Dictionary<DateTime, decimal> candleData = new();

                string line;
                using (StreamReader reader = new StreamReader(_benchmark.FileSetBenchmark))
                {
                    while ((line = reader.ReadLine()) != null)
                    {
                        string[] parts = line.Split(',');

                        if (parts.Length >= 8)
                        {
                            string dateStr = parts[0];

                            DateTime date = DateTime.ParseExact(dateStr, "yyyyMMdd", null);
                            DateTime dateTime = date.Date;

                            listData.Add(dateTime);

                            decimal lastValue = decimal.Parse(parts[5].Replace(".", ","));
                            candleData[dateTime] = lastValue;
                        }
                    }
                }

                if (_countLoadBenchmark < 3)
                {
                    if (candleData == null || candleData.Count == 0)
                    {
                        return null;
                    }

                    if (DateTime.Parse(series.Points[0].AxisLabel) < listData[0])
                    {
                        return null;
                    }

                    if (DateTime.Parse(series.Points[^1].AxisLabel).AddDays(-1).Date > listData[^1])
                    {
                        return null;
                    }
                }

                List<decimal> data = new();

                for (int i = 0; i < series.Points.Count; i++)
                {
                    DateTime dateTime = DateTime.Parse(series.Points[i].AxisLabel).Date;

                    DateTime roundedDateTime = candleData.Keys
                            .Where(date => date < dateTime)
                            .OrderByDescending(date => date)
                            .FirstOrDefault(candleData.Keys.Min());
                                        
                    if (candleData.ContainsKey(roundedDateTime))
                    {
                        if (ComboBoxChartType.SelectedItem.ToString() == "Absolute")
                        {
                            data.Add(candleData[roundedDateTime]);
                        }                        
                    }
                }

                return data;
            }            
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
                return null;
            }
        }

        private Series ScaleDataToChart(List<decimal> originalData, decimal chartMin, decimal chartMax)
        {
            try
            {                
                if (originalData == null || originalData.Count == 0)
                    return new Series();
               
                Series benchmark = new Series("SeriesBenchmark");
                benchmark.ChartType = SeriesChartType.Line;
                benchmark.YAxisType = AxisType.Secondary;
                benchmark.Color = Color.Green;
                benchmark.LabelForeColor = Color.Green;
                benchmark.ChartArea = "ChartAreaProfit";
                benchmark.ShadowOffset = 2;
                benchmark.BorderWidth = 2;

                decimal startValue = originalData[0];

                for (int i = 0; i < originalData.Count; i++)
                {
                    decimal scaledValue = 0;

                    if (startValue != originalData[i])
                    {
                        decimal relativeGrowth = (originalData[i] - startValue) / startValue;
                        scaledValue = _startValuePortfolio * relativeGrowth;
                    }

                    benchmark.Points.AddXY(i, scaledValue);
                    benchmark.Points[^1].AxisLabel = originalData[i].ToString();
                }

                return benchmark;
            }
            catch (Exception ex)
            {
                SendNewLogMessage(ex.ToString(), LogMessageType.Error);
                return null;
            }
        }

        private void PaintRectangleEqutyLines()
        {
            if (_visibleEquityLine)
            {
                RectangleEquity.Fill = Brushes.White;
            }
            else
            {
                RectangleEquity.Fill = Brushes.Gray;                
            }

            if (_visibleLongLine)
            {
                RectangleLong.Fill = Brushes.DeepSkyBlue;
            }
            else
            {
                RectangleLong.Fill = new SolidColorBrush(System.Windows.Media.Color.FromArgb(255, 0, 112, 149));                
            }

            if (_visibleShortLine)
            {
                RectangleShort.Fill = Brushes.DarkOrange;
            }
            else
            {                
                RectangleShort.Fill = new SolidColorBrush(System.Windows.Media.Color.FromArgb(255, 145, 80, 0));
            }
        }

        private void PaintXLabelsOnEquityChart(List<Position> poses)
        {
            try
            {
                int values = poses.Count;

                int labelCount = 5;

                if (values < 5)
                {
                    labelCount = values;
                }

                if (labelCount == 0)
                {
                    return;
                }

                ChartArea area = _chartEquity.ChartAreas[0];

                area.AxisX.Interval = values / labelCount;

                while (area.AxisX.CustomLabels.Count < labelCount)
                {
                    area.AxisX.CustomLabels.Add(new CustomLabel());
                }
                while (area.AxisX.CustomLabels.Count > labelCount)
                {
                    area.AxisX.CustomLabels.RemoveAt(0);
                }

                double value = 0 + area.AxisX.Interval;

                if (labelCount < 4)
                {
                    value = 0;
                }

                for (int i = 0; i < labelCount; i++)
                {
                    area.AxisX.CustomLabels[i].FromPosition = value - area.AxisX.Interval * 0.7;
                    area.AxisX.CustomLabels[i].ToPosition = value + area.AxisX.Interval * 0.7;
                    area.AxisX.CustomLabels[i].Text = poses[(int)value].TimeCreate.ToString();

                    value += area.AxisX.Interval;

                    if (value >= poses.Count)
                    {
                        value = poses.Count - 1;
                    }
                }
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        #endregion

        #region Volume Tabs

        private void PaintVolume(List<Position> positionsAll)
        {
            if (TabControlVolume.SelectedIndex == -1 ||
                        TabControlVolume.SelectedIndex == 0)
            {
                PaintVolumeOnChart(positionsAll);
            }
            else if (TabControlVolume.SelectedIndex == 1)
            {
                PaintPortfolioOnChart(positionsAll);
            }
        }

        private void TabControlVolume_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            try
            {
                RePaint();               
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        #endregion

        #region Volume Securities Chart

        private Chart _chartVolume;

        private void CreateChartVolumeControls()
        {
            try
            {
                _chartVolume = new Chart();
                HostVolume.Child = _chartVolume;
                HostVolume.Child.Show();

                _chartVolume.BackColor = Color.FromArgb(17, 18, 23);
                _chartVolume.Click += _chartVolume_Click;
                _chartVolume.MouseWheel += _chartVolume_MouseWheel;
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        private bool _volumeControlUpdated;

        private void UpdateVolumeShowNumbers(List<Position> positionsAll)
        {
            try
            {
                if (!GridTabPrime.Dispatcher.CheckAccess())
                {
                    GridTabPrime.Dispatcher.Invoke(
                        new Action<List<Position>>(UpdateVolumeShowNumbers), positionsAll);
                    return;
                }

                _volumeControlUpdated = true;

                VolumeShowNumbers.SelectionChanged -= VolumeShowNumbers_SelectionChanged;
                TabControlPrime.SelectionChanged -= TabControlPrime_SelectionChanged;
                TabControlVolume.SelectionChanged -= TabControlVolume_SelectionChanged;

                string lastSelectedValue = null;

                if (VolumeShowNumbers.SelectedItem != null)
                {
                    lastSelectedValue = VolumeShowNumbers.SelectedItem.ToString();
                }

                VolumeShowNumbers.Items.Clear();

                List<VolumeSecurity> volumes = new List<VolumeSecurity>();

                for (int i = 0; i < positionsAll.Count; i++)
                {
                    if (volumes.Find(vol => vol.Security == positionsAll[i].SecurityName) == null)
                    {
                        volumes.Add(new VolumeSecurity() { Security = positionsAll[i].SecurityName });
                    }
                }

                if (volumes.Count == 0)
                {
                    TabControlPrime.SelectionChanged += TabControlPrime_SelectionChanged;
                    return;
                }

                for (int i = 0; i < volumes.Count; i += 10)
                {
                    string value = (i + 1) + " >> " + (i + 10);
                    VolumeShowNumbers.Items.Add(value);
                }

                if (lastSelectedValue != null)
                {
                    for (int i = 0; i < VolumeShowNumbers.Items.Count; i++)
                    {
                        if (VolumeShowNumbers.Items[i].ToString() == lastSelectedValue)
                        {
                            VolumeShowNumbers.SelectedIndex = i;
                            break;
                        }
                    }
                }

                if (VolumeShowNumbers.SelectedItem == null)
                {
                    VolumeShowNumbers.SelectedItem = VolumeShowNumbers.Items[0];
                    _numberOfTensToBeDrawn = 0;
                }

                VolumeShowNumbers.SelectionChanged += VolumeShowNumbers_SelectionChanged;
                TabControlPrime.SelectionChanged += TabControlPrime_SelectionChanged;
                TabControlVolume.SelectionChanged += TabControlVolume_SelectionChanged;
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        private void PaintVolumeOnChart(List<Position> positionsAll)
        {
            try
            {
                if (IsErase == true)
                {
                    return;
                }

                if (!GridTabPrime.Dispatcher.CheckAccess())
                {
                    GridTabPrime.Dispatcher.Invoke(
                        new Action<List<Position>>(PaintVolumeOnChart), positionsAll);
                    return;
                }

                if (_chartVolume == null)
                {
                    CreateChartVolumeControls();
                }

                UpdateVolumeShowNumbers(positionsAll);

                _chartVolume.Series.Clear();
                _chartVolume.ChartAreas.Clear();

                if (positionsAll == null
                    || positionsAll.Count == 0)
                {
                    return;
                }

                //  take the number of tools
                // берём кол-во инструментов
                List<VolumeSecurity> volumes = new List<VolumeSecurity>();

                for (int i = 0; i < positionsAll.Count; i++)
                {
                    if (volumes.Find(vol => vol.Security == positionsAll[i].SecurityName) == null)
                    {
                        volumes.Add(new VolumeSecurity() { Security = positionsAll[i].SecurityName });
                    }
                }

                if (volumes.Count == 0)
                {
                    return;
                }

                // 1 создаём общую линию времени со всеми изменениями

                List<DateTime> allChange = new List<DateTime>();

                for (int i = 0; i < positionsAll.Count; i++)
                {
                    Position pos = positionsAll[i];
                    DateTime timeCreate = pos.TimeCreate;
                    DateTime timeClose = pos.TimeClose;

                    if (allChange.FindIndex(chnge => chnge == timeCreate) == -1)
                    {
                        allChange.Add(timeCreate);
                    }

                    if (pos.State == PositionStateType.Done)
                    {
                        if (allChange.FindIndex(chnge => chnge == timeClose) == -1)
                        {
                            allChange.Add(timeClose);
                        }
                    }
                }

                // 2 сортировка
                allChange = allChange.OrderBy(x => x).ToList();

                // 3 активируем массив Volume по бумагам

                for (int i = 0; i < volumes.Count; i++)
                {
                    volumes[i].Volume = new decimal[allChange.Count].ToList();
                }

                // 4 считаем по каждой временной точке объёмы

                for (int i = 0; i < positionsAll.Count; i++)
                {
                    Position pos = positionsAll[i];
                    decimal maxVolume = pos.MaxVolume;

                    if (maxVolume == 0)
                    {
                        continue;
                    }

                    DateTime timeCreate = pos.TimeCreate;
                    DateTime timeClose = pos.TimeClose;

                    VolumeSecurity volume = volumes.Find(vol => vol.Security == pos.SecurityName);
                    int indexOpen = allChange.FindIndex(change => change == timeCreate);
                    int indexClose = allChange.FindIndex(change => change == timeClose);

                    if (indexOpen != -1)
                    {
                        for (int i2 = indexOpen; i2 < volume.Volume.Count; i2++)
                        {
                            if (pos.Direction == Side.Buy)
                            {
                                volume.Volume[i2] += maxVolume;
                            }
                            else
                            {
                                volume.Volume[i2] -= maxVolume;
                            }
                        }
                    }

                    if (pos.State == PositionStateType.Done
                        && indexClose != -1)
                    {
                        for (int i2 = indexClose; i2 < volume.Volume.Count; i2++)
                        {
                            if (pos.Direction == Side.Buy)
                            {
                                volume.Volume[i2] -= maxVolume;
                            }
                            else
                            {
                                volume.Volume[i2] += maxVolume;
                            }
                        }
                    }
                }

                // 5 прорисовываем значения на чарте

                int volumesStartNum = 0;

                if (_numberOfTensToBeDrawn > 0)
                {
                    volumesStartNum = _numberOfTensToBeDrawn * 10;
                }

                for (int i = volumesStartNum; i < volumes.Count && i < volumesStartNum + 10; i++)
                {
                    if (i % 2 == 0)
                    {
                        PaintValuesVolume(volumes[i].Volume, volumes[i].Security, Color.DeepSkyBlue, allChange);
                    }
                    else
                    {
                        PaintValuesVolume(volumes[i].Volume, volumes[i].Security, Color.DarkOrange, allChange);
                    }
                }

                float step = (float)(100m / _chartVolume.ChartAreas.Count);

                float y = 0;

                for (int i = 0; i < _chartVolume.ChartAreas.Count; i++)
                {
                    _chartVolume.ChartAreas[i].Position.Width = 100;
                    _chartVolume.ChartAreas[i].Position.Height = step;
                    _chartVolume.ChartAreas[i].Position.Y = y;
                    _chartVolume.ChartAreas[i].AlignWithChartArea = _chartVolume.ChartAreas[0].Name;
                    y += step;
                }
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        private void PaintValuesVolume(List<decimal> volume, string name, Color color, List<DateTime> times)
        {
            try
            {
                if (volume == null ||
                    volume.Count == 0)
                {
                    return;
                }

                ChartArea areaLineSecurity = new ChartArea("ChartArea" + name);

                areaLineSecurity.CursorX.IsUserSelectionEnabled = true; //allow the user to change the view scope/ разрешаем пользователю изменять рамки представления
                areaLineSecurity.CursorX.IsUserEnabled = true; //trait/чертa

                areaLineSecurity.BorderColor = Color.Black;
                areaLineSecurity.BackColor = Color.FromArgb(17, 18, 23);
                areaLineSecurity.CursorY.LineColor = Color.Gainsboro;
                areaLineSecurity.CursorX.LineColor = Color.Black;
                areaLineSecurity.AxisX.TitleForeColor = Color.Gainsboro;
                areaLineSecurity.AxisY.TitleForeColor = Color.Gainsboro;
                areaLineSecurity.AxisY2.IntervalAutoMode = IntervalAutoMode.FixedCount;
                areaLineSecurity.AxisY2.Enabled = AxisEnabled.False;

                foreach (var axe in areaLineSecurity.Axes)
                {
                    axe.LabelStyle.ForeColor = Color.Gainsboro;
                }

                _chartVolume.ChartAreas.Add(areaLineSecurity);

                Series volumeSeries = new Series("Series" + name);
                volumeSeries.ChartType = SeriesChartType.Line;
                volumeSeries.Color = color;
                volumeSeries.YAxisType = AxisType.Secondary;
                volumeSeries.ChartArea = areaLineSecurity.Name;
                volumeSeries.BorderWidth = 3;
                volumeSeries.ShadowOffset = 2;

                decimal maxVolume = 0;
                decimal minVolume = decimal.MaxValue;

                for (int i = 0; i < volume.Count; i++)
                {
                    if (volume[i] > maxVolume)
                    {
                        maxVolume = volume[i];
                    }
                    if (volume[i] < minVolume)
                    {
                        minVolume = volume[i];
                    }
                    volumeSeries.Points.AddXY(i, Convert.ToDouble(volume[i]));
                    volumeSeries.Points[volumeSeries.Points.Count - 1].AxisLabel = times[i].ToString(_currentCulture);
                }

                _chartVolume.Series.Add(volumeSeries);

                Series nameSeries = new Series("Name" + name);
                nameSeries.ChartType = SeriesChartType.Point;
                nameSeries.Color = Color.White;
                nameSeries.YAxisType = AxisType.Secondary;
                nameSeries.ChartArea = areaLineSecurity.Name;
                nameSeries.BorderWidth = 3;
                nameSeries.ShadowOffset = 2;
                nameSeries.MarkerStyle = MarkerStyle.Square;
                nameSeries.MarkerSize = 4;

                nameSeries.Points.Add(Convert.ToDouble(maxVolume));
                nameSeries.Points[0].Label = name;
                nameSeries.Points[0].LabelForeColor = Color.White;

                _chartVolume.Series.Add(nameSeries);

                if (minVolume != decimal.MaxValue &&
                    maxVolume != 0 &&
                    minVolume != maxVolume)
                {
                    areaLineSecurity.AxisY2.Maximum = Convert.ToDouble(maxVolume + (maxVolume * 0.05m));
                    areaLineSecurity.AxisY2.Minimum = Convert.ToDouble(minVolume - (maxVolume * 0.05m));
                    double interval = Convert.ToDouble(Math.Abs(maxVolume - minVolume) / 8);
                    areaLineSecurity.AxisY2.Interval = interval;
                }
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        private void _chartVolume_Click(object sender, EventArgs e)
        {
            try
            {
                if (_chartVolume.ChartAreas.Count == 0)
                {
                    return;
                }
                if (double.IsNaN(_chartVolume.ChartAreas[0].CursorX.Position) ||
                    _chartVolume.ChartAreas[0].CursorX.Position == 0)
                {
                    return;
                }

                for (int i = 0; i < _chartVolume.Series.Count; i++)
                {
                    if (_chartVolume.Series[i].Points.Count == 1)
                    {
                        continue;
                    }
                    for (int i2 = 0; i2 < _chartVolume.Series[i].Points.Count; i2++)
                    {
                        _chartVolume.Series[i].Points[i2].Label = "";
                    }
                }

                for (int i = 0; i < _chartVolume.Series.Count; i++)
                {
                    if (_chartVolume.Series[i].Points.Count == 1)
                    {
                        continue;
                    }
                    string label = "";

                    int index = Convert.ToInt32(_chartVolume.ChartAreas[0].CursorX.Position) - 1;

                    if (index >= _chartVolume.Series[i].Points.Count)
                    {
                        return;
                    }

                    if (!string.IsNullOrWhiteSpace(_chartVolume.Series[i].Points[index].AxisLabel))
                    {
                        label += _chartVolume.Series[i].Points[index].AxisLabel + "\n";
                    }

                    label += _chartVolume.Series[i].Points[index].YValues[0];

                    _chartVolume.Series[i].Points[index].Label = label;
                    _chartVolume.Series[i].Points[index].LabelForeColor = _chartVolume.Series[i].Points[index].Color;
                    _chartVolume.Series[i].Points[index].LabelBackColor = Color.FromArgb(17, 18, 23);
                }
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        private void _chartVolume_MouseWheel(object sender, MouseEventArgs e)
        {
            try
            {
                if (_chartVolume.ChartAreas[0].AxisX.ScaleView.IsZoomed)
                {
                    _chartVolume.ChartAreas[0].AxisX.ScaleView.ZoomReset();
                }
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        int _numberOfTensToBeDrawn;

        private void VolumeShowNumbers_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            try
            {
                if (VolumeShowNumbers.SelectedValue == null
                    || VolumeShowNumbers.Items == null
                    || VolumeShowNumbers.Items.Count == 0)
                {
                    _numberOfTensToBeDrawn = 0;
                    return;
                }

                string selectedValue = VolumeShowNumbers.SelectedValue.ToString();

                for (int i = 0; i < VolumeShowNumbers.Items.Count; i++)
                {
                    if (VolumeShowNumbers.Items[i].ToString() == selectedValue)
                    {
                        _numberOfTensToBeDrawn = i;
                        break;
                    }
                }
                RePaint();
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        #endregion

        #region Volume to Portfolio

        private Chart _chartPortfolio;

        private DataGridView _gridLeveragePortfolio;

        private TableLayoutPanel _layoutPanelPortfolio;

        private void CreateChartPortfolio()
        {
            try
            {
                _chartPortfolio = new Chart();
                _chartPortfolio.Series.Clear();
                _chartPortfolio.ChartAreas.Clear();
                _chartPortfolio.BackColor = Color.FromArgb(17, 18, 23);
                _chartPortfolio.Dock = DockStyle.Fill;

                ChartArea areaLinePortfolio = new ChartArea("ChartAreaPortfolio");
                areaLinePortfolio.Position.Height = 70;
                areaLinePortfolio.Position.Width = 100;
                areaLinePortfolio.Position.Y = 0;
                areaLinePortfolio.CursorX.IsUserSelectionEnabled = true;
                areaLinePortfolio.CursorX.IsUserEnabled = true;
                areaLinePortfolio.AxisX.LabelStyle.Angle = 0;

                _chartPortfolio.ChartAreas.Add(areaLinePortfolio);

                ChartArea areaLineLeverageBar = new ChartArea("ChartAreaPortfolioBar");
                areaLineLeverageBar.AlignWithChartArea = "ChartAreaPortfolio";
                areaLineLeverageBar.Position.Height = 30;
                areaLineLeverageBar.Position.Width = 100;
                areaLineLeverageBar.Position.Y = 70;
                areaLineLeverageBar.AxisX.Enabled = AxisEnabled.False;
                areaLineLeverageBar.CursorX.IsUserEnabled = true;

                _chartPortfolio.ChartAreas.Add(areaLineLeverageBar);

                for (int i = 0; i < _chartPortfolio.ChartAreas.Count; i++)
                {
                    _chartPortfolio.ChartAreas[i].BorderColor = Color.Black;
                    _chartPortfolio.ChartAreas[i].BackColor = Color.FromArgb(17, 18, 23);
                    _chartPortfolio.ChartAreas[i].CursorY.LineColor = Color.Gainsboro;
                    _chartPortfolio.ChartAreas[i].CursorX.LineColor = Color.Black;
                    _chartPortfolio.ChartAreas[i].AxisX.TitleForeColor = Color.Gainsboro;
                    _chartPortfolio.ChartAreas[i].AxisY.TitleForeColor = Color.Gainsboro;

                    foreach (var axe in _chartPortfolio.ChartAreas[i].Axes)
                    {
                        axe.LabelStyle.ForeColor = Color.Gainsboro;
                    }
                }

                _chartPortfolio.MouseMove += _chartPortfolio_MouseMove;
                _chartPortfolio.MouseWheel += _chartPortfolio_MouseWheel;

                _gridLeveragePortfolio = DataGridFactory.GetDataGridView(DataGridViewSelectionMode.FullRowSelect, DataGridViewAutoSizeRowsMode.None);

                _gridLeveragePortfolio.AllowUserToResizeRows = false;
                _gridLeveragePortfolio.AllowUserToResizeColumns = true;
                _gridLeveragePortfolio.ColumnCount = 2;
                _gridLeveragePortfolio.RowCount = 0;
                _gridLeveragePortfolio.Dock = DockStyle.Fill;
                _gridLeveragePortfolio.ScrollBars = ScrollBars.Vertical;

                foreach (DataGridViewColumn column in _gridLeveragePortfolio.Columns)
                {
                    column.SortMode = DataGridViewColumnSortMode.NotSortable;
                    column.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleLeft;
                    column.ReadOnly = true;
                }

                _gridLeveragePortfolio.Columns[0].HeaderText = OsLocalization.Journal.LeverageGridColumn0;
                _gridLeveragePortfolio.Columns[1].HeaderText = OsLocalization.Journal.LeverageGridColumn1;

                CustomDataGridViewCell cell0 = new CustomDataGridViewCell();
                cell0.Style = _gridLeveragePortfolio.DefaultCellStyle;
                cell0.AdvancedBorderStyle = new DataGridViewAdvancedBorderStyle
                {
                    Bottom = DataGridViewAdvancedCellBorderStyle.None,
                    Top = DataGridViewAdvancedCellBorderStyle.None,
                    Left = DataGridViewAdvancedCellBorderStyle.Inset,
                    Right = DataGridViewAdvancedCellBorderStyle.Inset
                };

                _gridLeveragePortfolio.DataError += _gridLeveragePortfolio_DataError;

                _layoutPanelPortfolio = new TableLayoutPanel();
                _layoutPanelPortfolio.Dock = DockStyle.Fill;
                _layoutPanelPortfolio.ColumnCount = 2;
                _layoutPanelPortfolio.RowCount = 1;
                _layoutPanelPortfolio.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 80));
                _layoutPanelPortfolio.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 20));               
                _layoutPanelPortfolio.Controls.Add(_chartPortfolio, 0, 0);
                _layoutPanelPortfolio.Controls.Add(_gridLeveragePortfolio, 1, 0);

                HostVolumePortfolio.Child = _layoutPanelPortfolio;
                HostVolumePortfolio.Child.Show();
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        private void _gridLeveragePortfolio_DataError(object sender, DataGridViewDataErrorEventArgs e)
        {
            ServerMaster.SendNewLogMessage(e.ToString(), Logging.LogMessageType.Error);
        }

        private void _chartPortfolio_MouseWheel(object sender, MouseEventArgs e)
        {
            try
            {
                if (_chartPortfolio.ChartAreas[0].AxisX.ScaleView.IsZoomed)
                {
                    _chartPortfolio.ChartAreas[0].AxisX.ScaleView.ZoomReset();
                }
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        private void _chartPortfolio_MouseMove(object sender, MouseEventArgs e)
        {
            try
            {
                if (_chartPortfolio.Series == null
                    || _chartPortfolio.Series.Count == 0)
                {
                    return;
                }
                if (_chartPortfolio.ChartAreas[0].AxisX.ScaleView.Size == double.NaN)
                {
                    return;
                }

                if (e.X == _lastMouseXValue)
                {
                    return;
                }

                _lastMouseXValue = e.X;

                int curCountOfPoints = 0;

                if (_chartPortfolio.ChartAreas[0].AxisX.ScaleView.IsZoomed)
                {
                    curCountOfPoints = Convert.ToInt32(_chartPortfolio.ChartAreas[0].AxisX.ScaleView.Size);
                }
                else
                {
                    curCountOfPoints = _chartPortfolio.Series[0].Points.Count;
                }

                double sizeArea = _chartPortfolio.ChartAreas[0].InnerPlotPosition.Size.Width;
                double allSizeAbs = _chartPortfolio.Size.Width * (sizeArea / 100);

                double onePointLen = allSizeAbs / curCountOfPoints;

                double curMousePosAbs = e.X;

                double curPointNum = curMousePosAbs / onePointLen - 1;

                try
                {
                    if (Double.IsInfinity(curPointNum))
                    {
                        return;
                    }

                    curPointNum = Convert.ToDouble(Convert.ToInt32(curPointNum));
                }
                catch
                {
                    return;
                }

                int firstPoint = 0;

                if (_chartPortfolio.ChartAreas[0].AxisX.ScaleView.IsZoomed)
                {
                    firstPoint = Convert.ToInt32(_chartPortfolio.ChartAreas[0].AxisX.ScaleView.Position);
                    curPointNum = firstPoint + curPointNum;
                }

                if (_chartPortfolio.ChartAreas[0].CursorX.Position != curPointNum)
                {
                    _chartPortfolio.ChartAreas[0].CursorX.SetCursorPosition(curPointNum);
                }
                else
                {
                    return;
                }

                int numPointInt = Convert.ToInt32(curPointNum);

                if (numPointInt <= 0)
                {
                    return;
                }

                for (int i = 0; i < _chartPortfolio.Series.Count; i++)
                {
                    if (_chartPortfolio.Series[i].Points.Count > _lastSeriesEquityChartPointWithLabel)
                    {
                        _chartPortfolio.Series[i].Points[_lastSeriesEquityChartPointWithLabel].Label = "";
                    }
                    if (_chartPortfolio.Series[i].Points.Count > numPointInt)
                    {
                        _chartPortfolio.Series[i].Points[numPointInt].Label
                        = _chartPortfolio.Series[i].Points[numPointInt].AxisLabel + "\n" + Math.Round(_chartPortfolio.Series[i].Points[numPointInt].YValues[0], 2);
                    }
                }

                _lastSeriesEquityChartPointWithLabel = numPointInt;
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        private void PaintPortfolioOnChart(List<Position> positionsAll)
        {
            try
            {
                if (!GridTabPrime.Dispatcher.CheckAccess())
                {
                    GridTabPrime.Dispatcher.Invoke(
                        new Action<List<Position>>(PaintPortfolioOnChart), positionsAll);
                    return;
                }

                if (_chartPortfolio == null || _gridLeveragePortfolio == null)
                {
                    CreateChartPortfolio();
                }

                _chartPortfolio.Series.Clear();

                if (positionsAll == null || positionsAll.Count == 0)
                {
                    return;
                }

                Series totalPortfolio = new Series("SeriesPortfolio");
                totalPortfolio.ChartType = SeriesChartType.Line;
                totalPortfolio.Color = Color.White;  
                totalPortfolio.LabelForeColor = Color.White;
                totalPortfolio.YAxisType = AxisType.Secondary;
                totalPortfolio.ChartArea = "ChartAreaPortfolio";
                totalPortfolio.BorderWidth = 4;
                totalPortfolio.ShadowOffset = 2;                

                Series volumePortfolio = new Series("SeriesVolumeToPortfolio");
                volumePortfolio.ChartType = SeriesChartType.Line;
                volumePortfolio.Color = Color.DeepSkyBlue;  
                volumePortfolio.LabelForeColor = Color.DeepSkyBlue;
                volumePortfolio.YAxisType = AxisType.Secondary;
                volumePortfolio.ChartArea = "ChartAreaPortfolio";
                volumePortfolio.BorderWidth = 2;
                volumePortfolio.ShadowOffset = 2;

                Series leverageBars = new Series("SeriesLeverageBar");
                leverageBars.ChartType = SeriesChartType.Column;
                leverageBars.YAxisType = AxisType.Secondary;
                leverageBars.LabelForeColor = Color.White;
                leverageBars.ChartArea = "ChartAreaPortfolioBar";
                leverageBars.ShadowOffset = 2;

                List<DateTime> allChange = new List<DateTime>();

                for (int i = 0; i < positionsAll.Count; i++)
                {
                    Position pos = positionsAll[i];
                    DateTime timeCreate = pos.TimeCreate;
                    DateTime timeClose = pos.TimeClose;

                    if (allChange.FindIndex(chnge => chnge == timeCreate) == -1)
                    {
                        allChange.Add(timeCreate);
                    }

                    if (pos.State == PositionStateType.Done)
                    {
                        if (allChange.FindIndex(chnge => chnge == timeClose) == -1)
                        {
                            allChange.Add(timeClose);
                        }
                    }
                }

                allChange = allChange.OrderBy(x => x).ToList();

                decimal[] values = new decimal[allChange.Count];
                List<decimal> volume = new(values);
                List<decimal> deposit = new(values);

                SortedDictionary<decimal, TimeSpan> leverageList = new();

                for (int i = 0; i < positionsAll.Count; i++)
                {
                    Position pos = positionsAll[i];
                    
                    if (pos.MaxVolume == 0)
                    {
                        continue;
                    }

                    DateTime timeCreate = pos.TimeCreate;
                    DateTime timeClose = pos.TimeClose;
                                        
                    int indexOpen = allChange.FindIndex(change => change == timeCreate);
                    int indexClose = allChange.FindIndex(change => change == timeClose);

                    if (indexOpen != -1)
                    {                        
                        volume[indexOpen] += pos.MaxVolume * pos.EntryPrice;
                        deposit[indexOpen] = pos.PortfolioValueOnOpenPosition;
                    }

                    if (pos.State == PositionStateType.Done
                        && indexClose != -1)
                    {
                        volume[indexClose] -= pos.MaxVolume * pos.EntryPrice;
                        deposit[indexClose] = pos.PortfolioValueOnOpenPosition;
                    }
                }

                List<decimal> volumeData = new();

                for (int i = 0; i < volume.Count; i++)
                {   
                    if (i > 0)
                    {
                        volumeData.Add(volumeData[^1] + volume[i]);
                    }
                    else
                    {
                        volumeData.Add(volume[i]);
                    }                    
                }

                decimal maxVolume = 0;
                decimal minVolume = decimal.MaxValue;

                for (int i = 0; i < allChange.Count; i++)
                {
                    decimal totalDataPoint = Math.Round(deposit[i],4);
                    totalPortfolio.Points.AddXY(i, totalDataPoint);
                    totalPortfolio.Points[^1].AxisLabel = allChange[i].ToString();

                    decimal volumeDataPoint = Math.Round(volumeData[i],4);             
                    volumePortfolio.Points.AddXY(i, volumeDataPoint);
                    volumePortfolio.Points[^1].AxisLabel = allChange[i].ToString();

                    decimal leverage = Math.Round(volumeDataPoint / totalDataPoint, 2);
                    leverageBars.Points.AddXY(i, leverage);
                    leverageBars.Points[^1].AxisLabel = allChange[i].ToString();

                    leverageBars.Points[^1].Color = GetColorForLeverageLevel(leverageBars.Points[^1].YValues[0]);

                    decimal leverageLevel = Math.Round(leverage, MidpointRounding.ToPositiveInfinity);

                    if (leverage > 1 && leverage <= 1.5m)
                    {
                        leverageLevel = 1.5m;
                    }
                    else if (leverage > 1.5m && leverage <= 2)
                    {
                        leverageLevel = 2;
                    }
                    else if (leverage > 2 && leverage <= 2.5m)
                    {
                        leverageLevel = 2.5m;
                    }
                    else if (leverage > 2.5m && leverage <= 3)
                    {
                        leverageLevel = 3;
                    }

                    if (leverageLevel == 0)
                    {
                        leverageLevel = 1;
                    }

                    if (i < allChange.Count - 1)
                    {
                        if (leverageList.ContainsKey(leverageLevel))
                        {
                            leverageList[leverageLevel] += allChange[i + 1] - allChange[i];
                        }
                        else
                        {
                            leverageList[leverageLevel] = allChange[i + 1] - allChange[i];
                        }
                    }

                    if (volumeData[i] > maxVolume)
                    {
                        maxVolume = volumeData[i];
                    }
                    if (volumeData[i] < minVolume)
                    {
                        minVolume = volumeData[i];
                    }

                    if (deposit[i] > maxVolume)
                    {
                        maxVolume = deposit[i];
                    }
                    if (deposit[i] < minVolume)
                    {
                        minVolume = deposit[i];
                    }
                }

                if (minVolume != decimal.MaxValue &&
                    maxVolume != 0 &&
                    minVolume != maxVolume)
                {
                    double valueMax = Convert.ToDouble(maxVolume + (maxVolume * 0.05m));
                    double valueMin = Convert.ToDouble(minVolume - (minVolume * 0.05m));

                    valueMax = Math.Round(valueMax, 4);
                    valueMin = Math.Round(valueMin, 4);

                    if(valueMax > valueMin)
                    {
                        _chartPortfolio.ChartAreas["ChartAreaPortfolio"].AxisY2.Maximum = valueMax;
                        _chartPortfolio.ChartAreas["ChartAreaPortfolio"].AxisY2.Minimum = valueMin;
                        double interval = Convert.ToDouble(Math.Abs(maxVolume - minVolume) / 8);

                        interval = Math.Round(interval, 4);

                        _chartPortfolio.ChartAreas["ChartAreaPortfolio"].AxisY2.Interval = interval;

                    }
                }

                _chartPortfolio.Series.Add(volumePortfolio);
                _chartPortfolio.Series.Add(totalPortfolio);
                _chartPortfolio.Series.Add(leverageBars);

                AddDataToGridLeverage(leverageList);
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        private Color GetColorForLeverageLevel(double value)
        {
            if (value < 1)
            {
                return Color.Green;
            }
            else if (value > 3)
            {
                return Color.Red;
            }
            else
            {
                return Color.Orange;
            }
        }

        private void AddDataToGridLeverage(SortedDictionary<decimal, TimeSpan> leverageList)
        {
            try
            {
                if (!GridTabPrime.Dispatcher.CheckAccess())
                {
                    GridTabPrime.Dispatcher.Invoke(
                        new Action<SortedDictionary<decimal, TimeSpan>>(AddDataToGridLeverage), leverageList);
                    return;
                }

                if (leverageList == null || leverageList.Count == 0) return;

                for (int i = 0; i < _gridLeveragePortfolio.RowCount; i++)
                {
                    _gridLeveragePortfolio.Rows.RemoveAt(i);
                    i--;
                }

                int count = (int)leverageList.Keys.Max();

                TimeSpan timeSpan = new TimeSpan(0);

                foreach (var keys in leverageList)
                {
                    timeSpan += keys.Value;
                }
                
                for (int i = 0; i < leverageList.Count; i++)
                {
                    DataGridViewRow newRow = new DataGridViewRow();

                    string value = $"{i - 1} - {i}";

                    decimal key = leverageList.Keys.ElementAt(i);

                    switch (leverageList.Keys.ElementAt(i))
                    {
                        case 1:
                            value = "0 - 1";
                            break;
                        case 1.5m:
                            value = "1 - 1.5";
                            break;
                        case 2:
                            value = "1.5 - 2";
                            break;
                        case 2.5m:
                            value = "2 - 2.5";
                            break;
                        case 3:
                            value = "2.5 - 3";
                            break;
                    }

                    newRow.Cells.Add(new DataGridViewTextBoxCell() { Value = value });

                    if (leverageList.ContainsKey(key))
                    {
                        newRow.Cells.Add(new DataGridViewTextBoxCell() { Value = Math.Round(leverageList[key].TotalSeconds / timeSpan.TotalSeconds * 100, 2) + "%" });
                    }
                    else
                    {
                        newRow.Cells.Add(new DataGridViewTextBoxCell() { Value = "0%" });
                    }

                    newRow.DefaultCellStyle.ForeColor = GetColorForLeverageLevel((double)key - 0.1);
                    newRow.DefaultCellStyle.SelectionForeColor = newRow.DefaultCellStyle.ForeColor;

                    _gridLeveragePortfolio.Rows.Add(newRow);
                }
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        #endregion

        #region Max DD Chart

        private Chart _chartDd;

        private void CreateChartDrawDown()
        {
            try
            {
                _chartDd = new Chart();
                HostDrawdown.Child = _chartDd;
                HostDrawdown.Child.Show();

                _chartDd.Series.Clear();
                _chartDd.ChartAreas.Clear();
                _chartDd.BackColor = Color.FromArgb(17, 18, 23);

                ChartArea areaDdPunct = new ChartArea("ChartAreaDdPunct");
                areaDdPunct.Position.Height = 50;
                areaDdPunct.Position.Width = 100;
                areaDdPunct.Position.Y = 0;
                areaDdPunct.CursorX.IsUserSelectionEnabled = false; //allow the user to change the view scope/ разрешаем пользователю изменять рамки представления
                areaDdPunct.CursorX.IsUserEnabled = true; //trait/чертa

                _chartDd.ChartAreas.Add(areaDdPunct);

                ChartArea areaDdPercent = new ChartArea("ChartAreaDdPercent");
                areaDdPercent.AlignWithChartArea = "ChartAreaDdPunct";
                areaDdPercent.Position.Height = 50;
                areaDdPercent.Position.Width = 100;
                areaDdPercent.Position.Y = 50;
                areaDdPercent.AxisX.Enabled = AxisEnabled.False;
                areaDdPercent.CursorX.IsUserEnabled = true; //trait/чертa

                _chartDd.ChartAreas.Add(areaDdPercent);

                for (int i = 0; i < _chartDd.ChartAreas.Count; i++)
                {
                    _chartDd.ChartAreas[i].BorderColor = Color.Black;
                    _chartDd.ChartAreas[i].BackColor = Color.FromArgb(17, 18, 23);
                    _chartDd.ChartAreas[i].CursorY.LineColor = Color.Gainsboro;
                    _chartDd.ChartAreas[i].CursorX.LineColor = Color.Black;
                    _chartDd.ChartAreas[i].AxisX.TitleForeColor = Color.Gainsboro;
                    _chartDd.ChartAreas[i].AxisY.TitleForeColor = Color.Gainsboro;

                    foreach (var axe in _chartDd.ChartAreas[i].Axes)
                    {
                        axe.LabelStyle.ForeColor = Color.Gainsboro;
                    }
                }

                _chartDd.MouseMove += _chartDd_MouseMove;
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        private void _chartDd_MouseMove(object sender, MouseEventArgs e)
        {
            try
            {
                if (_chartDd.ChartAreas[0].AxisX.ScaleView.Size == double.NaN)
                {
                    return;
                }

                if (_chartDd.Series == null
                    || _chartDd.Series.Count == 0)
                {
                    return;
                }

                int curCountOfPoints = _chartDd.Series[0].Points.Count;

                double sizeArea = _chartDd.ChartAreas[0].InnerPlotPosition.Size.Width;
                double allSizeAbs = _chartDd.Size.Width * (sizeArea / 100);

                double onePointLen = allSizeAbs / curCountOfPoints;

                double curMousePosAbs = e.X;

                double curPointNum = curMousePosAbs / onePointLen - 1;
                try
                {
                    curPointNum = Convert.ToDouble(Convert.ToInt32(curPointNum));
                }
                catch
                {
                    return;
                }

                if (_chartDd.ChartAreas[0].CursorX.Position != curPointNum)
                {
                    _chartDd.ChartAreas[0].CursorX.SetCursorPosition(curPointNum);
                }



                int numPointInt = Convert.ToInt32(curPointNum);

                if (numPointInt <= 0)
                {
                    return;
                }

                if (_chartDd.Series[0].Points.Count > _lastSeriesDrawDownPointWithLabel)
                {
                    _chartDd.Series[0].Points[_lastSeriesDrawDownPointWithLabel].Label = "";
                }
                if (_chartDd.Series[0].Points.Count > numPointInt)
                {
                    _chartDd.Series[0].Points[numPointInt].Label
                    = _chartDd.Series[0].Points[numPointInt].LegendText;
                }

                if (_chartDd.Series[1].Points.Count > _lastSeriesDrawDownPointWithLabel)
                {
                    _chartDd.Series[1].Points[_lastSeriesDrawDownPointWithLabel].Label = "";
                }
                if (_chartDd.Series[1].Points.Count > numPointInt)
                {
                    _chartDd.Series[1].Points[numPointInt].Label
                    = _chartDd.Series[1].Points[numPointInt].AxisLabel;
                }

                if (_chartDd.Series[2].Points.Count > _lastSeriesDrawDownPointWithLabel)
                {
                    _chartDd.Series[2].Points[_lastSeriesDrawDownPointWithLabel].Label = "";
                }
                if (_chartDd.Series[2].Points.Count > numPointInt)
                {
                    _chartDd.Series[2].Points[numPointInt].Label
                    = _chartDd.Series[2].Points[numPointInt].LegendText;
                }

                _lastSeriesDrawDownPointWithLabel = numPointInt;
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        private int _lastSeriesDrawDownPointWithLabel;

        private void PaintDrawDown(List<Position> positionsAll)
        {
            try
            {
                if (_chartDd == null)
                {
                    CreateChartDrawDown();
                }

                _chartDd.Series.Clear();

                if (positionsAll == null
                    || positionsAll.Count == 0)
                {
                    return;
                }

                List<decimal> ddPunct = new decimal[positionsAll.Count].ToList();
                decimal lastMax = 0;
                decimal currentProfit = 0;

                for (int i = 0; i < positionsAll.Count; i++)
                {
                    currentProfit += positionsAll[i].ProfitPortfolioAbs * (positionsAll[i].MultToJournal / 100);

                    if (lastMax < currentProfit)
                    {
                        lastMax = currentProfit;
                    }

                    if (currentProfit - lastMax < 0)
                    {
                        ddPunct[i] = currentProfit - lastMax;
                    }
                }

                Series drowDownPunct = new Series("SeriesDdPunct");
                drowDownPunct.ChartType = SeriesChartType.Line;
                drowDownPunct.Color = Color.DeepSkyBlue;
                drowDownPunct.LabelForeColor = Color.White;
                drowDownPunct.YAxisType = AxisType.Secondary;
                drowDownPunct.ChartArea = "ChartAreaDdPunct";
                drowDownPunct.BorderWidth = 2;
                drowDownPunct.ShadowOffset = 2;

                Series nullLine = new Series("SeriesNullLine");
                nullLine.ChartType = SeriesChartType.Line;
                nullLine.YAxisType = AxisType.Secondary;
                nullLine.LabelForeColor = Color.White;
                nullLine.ChartArea = "ChartAreaDdPunct";
                nullLine.ShadowOffset = 0;

                for (int i = 0; i < ddPunct.Count; i++)
                {
                    decimal val = Math.Round(ddPunct[i], 6);
                    drowDownPunct.Points.AddXY(i, Convert.ToDouble(val));

                    drowDownPunct.Points[drowDownPunct.Points.Count - 1].AxisLabel =
                   positionsAll[i].TimeCreate.ToString(_currentCulture);

                    drowDownPunct.Points[drowDownPunct.Points.Count - 1].LegendText = val.ToString();

                    nullLine.Points.AddXY(i, 0);
                    nullLine.Points[nullLine.Points.Count - 1].AxisLabel =
                        positionsAll[i].TimeCreate.ToString(_currentCulture);
                }

                _chartDd.Series.Add(drowDownPunct);
                _chartDd.Series.Add(nullLine);

                decimal minOnY = decimal.MaxValue;

                for (int i = 0; i < ddPunct.Count; i++)
                {
                    if (ddPunct[i] < minOnY)
                    {
                        minOnY = ddPunct[i];
                    }
                }

                if (minOnY != decimal.MaxValue &&
                    minOnY != 0)
                {
                    _chartDd.ChartAreas[0].AxisY2.IntervalType = DateTimeIntervalType.Number;
                    _chartDd.ChartAreas[0].AxisY2.IntervalOffsetType = DateTimeIntervalType.Number;
                    _chartDd.ChartAreas[0].AxisY2.Maximum = Math.Round(-Convert.ToDouble(minOnY) * 0.05, 6);
                    _chartDd.ChartAreas[0].AxisY2.Minimum = Math.Round(Convert.ToDouble(minOnY) + Convert.ToDouble(minOnY) * 0.05, 6);
                }

                // dd in %
                // дд в %

                List<decimal> ddPepcent = new decimal[positionsAll.Count].ToList();

                decimal firsValue = positionsAll[0].PortfolioValueOnOpenPosition;

                for (int i = 0; i < positionsAll.Count; i++)
                {
                    if (firsValue != 0)
                    {
                        break;
                    }
                    firsValue = positionsAll[i].PortfolioValueOnOpenPosition;
                }

                if (firsValue == 0)
                {
                    firsValue = 1;
                }

                decimal thisSumm = firsValue;
                decimal thisPik = firsValue;

                for (int i = 0; i < positionsAll.Count; i++)
                {
                    thisSumm += positionsAll[i].ProfitPortfolioAbs * (positionsAll[i].MultToJournal / 100);

                    if (thisSumm > thisPik)
                    {
                        thisPik = thisSumm;
                    }

                    decimal thisDown = 0;

                    if (thisSumm < 0)
                    {
                        // уже ушли ниже нулевой отметки по счёту

                        thisDown = -thisPik + thisSumm;
                    }
                    else if (thisSumm > 0)
                    {
                        // выше нулевой отметки по счёту
                        thisDown = -(thisPik - thisSumm);
                    }

                    ddPepcent[i] = (thisDown / (thisPik / 100));
                }

                Series drowDownPercent = new Series("SeriesDdPercent");
                drowDownPercent.ChartType = SeriesChartType.Line;
                drowDownPercent.Color = Color.DarkOrange;
                drowDownPercent.LabelForeColor = Color.White;
                drowDownPercent.YAxisType = AxisType.Secondary;
                drowDownPercent.ChartArea = "ChartAreaDdPercent";
                drowDownPercent.BorderWidth = 2;
                drowDownPercent.ShadowOffset = 2;
                drowDownPercent.XAxisType = AxisType.Primary;

                for (int i = 0; i < ddPepcent.Count; i++)
                {
                    decimal val = Math.Round(ddPepcent[i], 6);
                    drowDownPercent.Points.AddXY(i, Convert.ToDouble(val));
                    drowDownPercent.Points[drowDownPercent.Points.Count - 1].AxisLabel = positionsAll[i].TimeCreate.ToString(_currentCulture);
                    drowDownPercent.Points[drowDownPercent.Points.Count - 1].LegendText = val.ToString();
                }

                _chartDd.Series.Add(drowDownPercent);

                decimal minOnY2 = decimal.MaxValue;

                for (int i = 0; i < ddPepcent.Count; i++)
                {
                    if (ddPepcent[i] < minOnY2)
                    {
                        minOnY2 = ddPepcent[i];
                    }
                }

                if (minOnY2 != decimal.MaxValue &&
                    minOnY2 != 0)
                {
                    _chartDd.ChartAreas[1].AxisY2.IntervalType = DateTimeIntervalType.Number;
                    _chartDd.ChartAreas[1].AxisY2.IntervalOffsetType = DateTimeIntervalType.Number;
                    _chartDd.ChartAreas[1].AxisY2.Maximum = Math.Round(-Convert.ToDouble(minOnY2) * 0.05, 6);
                    _chartDd.ChartAreas[1].AxisY2.Minimum = Math.Round(Convert.ToDouble(minOnY2) + Convert.ToDouble(minOnY2) * 0.05, 6);
                }
            }
            catch (Exception ex)
            {
                SendNewLogMessage(ex.ToString(), LogMessageType.Error);
            }
        }

        #endregion

        #region Open positions grid

        private DataGridView _openPositionGrid;

        private void CreateOpenPositionTable()
        {
            try
            {
                _openPositionGrid = CreateNewTable();
                HostOpenPosition.Child = _openPositionGrid;
                _openPositionGrid.Click += _openPositionGrid_Click;
                _openPositionGrid.DoubleClick += _openPositionGrid_DoubleClick;
                _openPositionGrid.DataError += _gridStatistics_DataError;
            }
            catch (Exception ex)
            {
                SendNewLogMessage(ex.ToString(), LogMessageType.Error);
            }
        }

        private void PaintOpenPositionGrid(List<Position> positionsAll)
        {
            try
            {
                if (_openPositionGrid == null)
                {
                    CreateOpenPositionTable();
                }

                List<Position> openPositions = new List<Position>();

                for (int i = 0; i < positionsAll.Count; i++)
                {
                    if (positionsAll[i].State != PositionStateType.Done &&
                        positionsAll[i].State != PositionStateType.OpeningFail)
                    {
                        openPositions.Add(positionsAll[i]);
                    }
                }

                HostOpenPosition.Child = null;

                _openPositionGrid.Rows.Clear();

                if (openPositions == null
                    || openPositions.Count == 0)
                {
                    HostOpenPosition.Child = _openPositionGrid;
                    return;
                }

                int startNum = 0;
                int endNum = openPositions.Count;

                if (ComboBoxOpenPosesShowNumbers.SelectedItem != null)
                {
                    string selectNum = ComboBoxOpenPosesShowNumbers.SelectedItem.ToString().Replace(" ", "");

                    startNum = Convert.ToInt32(selectNum.Split('>')[0]);
                    endNum = Convert.ToInt32(selectNum.Split('>')[1]);
                }

                for (int i = startNum; i < endNum && i < openPositions.Count; i++)
                {
                    _openPositionGrid.Rows.Insert(0, GetRow(openPositions[i]));
                }
            }
            catch (Exception ex)
            {
                SendNewLogMessage(ex.ToString(), LogMessageType.Error);
            }

            HostOpenPosition.Child = _openPositionGrid;
        }

        private DataGridView CreateNewTable()
        {
            try
            {
                DataGridView newGrid = DataGridFactory.GetDataGridPosition();
                newGrid.ScrollBars = ScrollBars.Vertical;

                return newGrid;
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
            return null;
        }

        private DataGridViewRow GetRow(Position position)
        {
            if (position == null)
            {
                return null;
            }

            try
            {
                DataGridViewRow nRow = new DataGridViewRow();

                if (position.ProfitPortfolioAbs > 0)
                {
                    nRow.DefaultCellStyle.ForeColor = Color.FromArgb(57, 157, 54);
                }
                else if (position.ProfitPortfolioAbs <= 0)
                {
                    nRow.DefaultCellStyle.ForeColor = Color.FromArgb(254, 84, 0);
                }

                nRow.Cells.Add(new DataGridViewTextBoxCell());
                nRow.Cells[0].Value = position.Number;

                nRow.Cells.Add(new DataGridViewTextBoxCell());
                nRow.Cells[1].Value = position.TimeCreate.ToString(_currentCulture);

                nRow.Cells.Add(new DataGridViewTextBoxCell());
                if (position.TimeClose != position.TimeOpen)
                {
                    nRow.Cells[2].Value = position.TimeClose.ToString(_currentCulture);
                }
                else
                {
                    nRow.Cells[2].Value = "";
                }

                nRow.Cells.Add(new DataGridViewTextBoxCell());
                nRow.Cells[3].Value = position.NameBot;

                nRow.Cells.Add(new DataGridViewTextBoxCell());
                nRow.Cells[4].Value = position.SecurityName;

                nRow.Cells.Add(new DataGridViewTextBoxCell());
                nRow.Cells[5].Value = position.Direction;

                if (position.Direction == Side.Buy)
                {
                    nRow.Cells[5].Style.ForeColor = Color.DodgerBlue;
                    //nRow.Cells[5].Style.SelectionBackColor = Color.DodgerBlue;
                }
                else
                {
                    nRow.Cells[5].Style.ForeColor = Color.DarkRed;
                    //nRow.Cells[5].Style.SelectionBackColor = Color.DarkOrange;
                }

                int decimalsPrice = position.PriceStep.ToStringWithNoEndZero().DecimalsCount();

                decimalsPrice++;

                nRow.Cells.Add(new DataGridViewTextBoxCell());
                nRow.Cells[6].Value = position.State;

                nRow.Cells.Add(new DataGridViewTextBoxCell());
                nRow.Cells[7].Value = position.MaxVolume;

                nRow.Cells.Add(new DataGridViewTextBoxCell());
                nRow.Cells[8].Value = position.OpenVolume;

                nRow.Cells.Add(new DataGridViewTextBoxCell());
                nRow.Cells[9].Value = position.WaitVolume;

                nRow.Cells.Add(new DataGridViewTextBoxCell());
                nRow.Cells[10].Value = Math.Round(position.EntryPrice, decimalsPrice).ToStringWithNoEndZero();

                nRow.Cells.Add(new DataGridViewTextBoxCell());
                nRow.Cells[11].Value = Math.Round(position.ClosePrice, decimalsPrice).ToStringWithNoEndZero();

                nRow.Cells.Add(new DataGridViewTextBoxCell());
                nRow.Cells[12].Value = Math.Round(position.ProfitPortfolioAbs, decimalsPrice).ToStringWithNoEndZero();

                nRow.Cells.Add(new DataGridViewTextBoxCell());
                nRow.Cells[13].Value = Math.Round(position.StopOrderRedLine, decimalsPrice).ToStringWithNoEndZero();

                nRow.Cells.Add(new DataGridViewTextBoxCell());
                nRow.Cells[14].Value = Math.Round(position.StopOrderPrice, decimalsPrice).ToStringWithNoEndZero();

                nRow.Cells.Add(new DataGridViewTextBoxCell());
                nRow.Cells[15].Value = Math.Round(position.ProfitOrderRedLine, decimalsPrice).ToStringWithNoEndZero();

                nRow.Cells.Add(new DataGridViewTextBoxCell());
                nRow.Cells[16].Value = Math.Round(position.ProfitOrderPrice, decimalsPrice).ToStringWithNoEndZero();

                nRow.Cells.Add(new DataGridViewTextBoxCell());
                nRow.Cells[17].Value = position.SignalTypeOpen;

                nRow.Cells.Add(new DataGridViewTextBoxCell());
                nRow.Cells[18].Value = position.SignalTypeClose;

                return nRow;
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
            return null;
        }

        private void _openPositionGrid_DoubleClick(object sender, EventArgs e)
        {
            int number;
            try
            {
                if (_openPositionGrid.CurrentCell == null)
                {
                    return;
                }

                number = Convert.ToInt32(_openPositionGrid.Rows[_openPositionGrid.CurrentCell.RowIndex].Cells[0].Value);
            }
            catch (Exception)
            {
                return;
            }

            ShowPositionDialog(number);
        }

        private void _openPositionGrid_Click(object sender, EventArgs e)
        {
            try
            {
                MouseEventArgs mouse = (MouseEventArgs)e;
                if (mouse.Button != MouseButtons.Right)
                {
                    if (_openPositionGrid.ContextMenuStrip != null)
                    {
                        _openPositionGrid.ContextMenuStrip = null;
                    }
                    return;
                }

                List<ToolStripMenuItem> items = new List<ToolStripMenuItem>();

                items.Add(new ToolStripMenuItem { Text = OsLocalization.Journal.PositionMenuItem8 });
                items[0].Click += OpenDealMoreInfo_Click;

                items.Add(new ToolStripMenuItem { Text = OsLocalization.Journal.PositionMenuItem9 });
                items[1].Click += OpenDealDelete_Click;

                items.Add(new ToolStripMenuItem { Text = OsLocalization.Journal.PositionMenuItem10 });
                items[2].Click += OpenDealClearAll_Click;

                if (_botsJournals.Count != 0)
                {
                    List<ToolStripMenuItem> itemsBots = new List<ToolStripMenuItem>();

                    for (int i = 0; i < _botsJournals.Count; i++)
                    {
                        itemsBots.Add(new ToolStripMenuItem { Text = _botsJournals[i].BotName });
                        itemsBots[i].Click += OpenDealCreatePosition_Click;
                    }

                    var item = new ToolStripMenuItem { Text = OsLocalization.Journal.PositionMenuItem13 };
                    item.DropDownItems.AddRange(itemsBots.ToArray());

                    items.Add(item);
                }

                ContextMenuStrip menu = new ContextMenuStrip();
                menu.Items.AddRange(items.ToArray());

                _openPositionGrid.ContextMenuStrip = menu;
                _openPositionGrid.ContextMenuStrip.Show(_openPositionGrid, new System.Drawing.Point(mouse.X, mouse.Y));
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        private void OpenDealMoreInfo_Click(object sender, EventArgs e)
        {
            int number;

            try
            {
                if (_openPositionGrid.CurrentCell == null)
                {
                    return;
                }

                number = Convert.ToInt32(_openPositionGrid.Rows[_openPositionGrid.CurrentCell.RowIndex].Cells[0].Value);
            }
            catch (Exception)
            {
                return;
            }

            ShowPositionDialog(number);
        }

        private void OpenDealDelete_Click(object sender, EventArgs e)
        {
            if (_openPositionGrid.Rows.Count == 0)
            {
                return;
            }

            int number;

            try
            {
                number = Convert.ToInt32(_openPositionGrid.Rows[_openPositionGrid.CurrentCell.RowIndex].Cells[0].Value);
            }
            catch (Exception)
            {
                return;
            }

            AcceptDialogUi ui = new AcceptDialogUi(OsLocalization.Journal.Message3);
            ui.ShowDialog();

            if (ui.UserAcceptAction == false)
            {
                return;
            }

            DeletePosition(number);
            SelectOpenPosesPages();
            RePaint();
        }

        private void OpenDealClearAll_Click(object sender, EventArgs e)
        {
            List<int> numbers = new List<int>();
            try
            {
                for (int i = 0; i < _openPositionGrid.Rows.Count; i++)
                {
                    numbers.Add(Convert.ToInt32(_openPositionGrid.Rows[i].Cells[0].Value));
                }

            }
            catch (Exception)
            {
                return;
            }

            AcceptDialogUi ui = new AcceptDialogUi(OsLocalization.Journal.Message4);
            ui.ShowDialog();

            if (ui.UserAcceptAction == false)
            {
                return;
            }

            for (int i = 0; i < numbers.Count; i++)
            {
                DeletePosition(numbers[i]);
            }
            SelectOpenPosesPages();
            RePaint();
        }

        private void OpenDealCreatePosition_Click(object sender, EventArgs e)
        {
            try
            {
                if (_botsJournals == null ||
                    _botsJournals.Count == 0)
                {
                    return;
                }

                ToolStripMenuItem tab = (ToolStripMenuItem)sender;

                string botName = tab.Text;

                Position newPos = new Position();

                newPos.Number = NumberGen.GetNumberDeal(_startProgram);
                newPos.NameBot = botName;

                BotPanelJournal myJournal = null;

                for (int i = 0; i < _botsJournals.Count; i++)
                {
                    if (_botsJournals[i].BotName == botName)
                    {
                        myJournal = _botsJournals[i];
                        break;

                    }
                }

                if (myJournal == null)
                {
                    return;
                }

                myJournal._Tabs[0].Journal.SetNewDeal(newPos);

                RePaint();
            }
            catch (Exception ex)
            {
                SendNewLogMessage(ex.ToString(), LogMessageType.Error);
            }
        }

        private void ComboBoxOpenPosesOnPage_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            SelectOpenPosesPages();
            RePaint();
        }

        private void SelectOpenPosesPages()
        {
            _volumeControlUpdated = true;
            ComboBoxOpenPosesShowNumbers.SelectionChanged -= ComboBoxOpenPosesShowNumbers_SelectionChanged;
            ComboBoxOpenPosesShowNumbers.Items.Clear();

            string selectedPosesOnPage = ComboBoxOpenPosesOnPage.SelectedItem.ToString();

            int countPosOnPage = 0;

            if (selectedPosesOnPage == OsLocalization.Journal.Label18)
            {
                countPosOnPage = 100;
            }
            else if (selectedPosesOnPage == OsLocalization.Journal.Label19)
            {
                countPosOnPage = 500;
            }
            else if (selectedPosesOnPage == OsLocalization.Journal.Label20)
            {
                countPosOnPage = 1000;
            }

            List<Position> allSortPoses = new List<Position>();

            for (int i = 0; _allPositions != null && i < _allPositions.Count; i++)
            {
                if (_allPositions[i] == null)
                {
                    continue;
                }
                if (_allPositions[i].TimeCreate < _startTime
                    || _allPositions[i].TimeCreate > _endTime)
                {
                    continue;
                }
                if (_allPositions[i].State != PositionStateType.Done &&
                    _allPositions[i].State != PositionStateType.OpeningFail)
                {
                    allSortPoses.Add(_allPositions[i]);
                }
            }

            if (allSortPoses.Count == 0)
            {
                return;
            }

            int curPositionNum = 0;

            while (curPositionNum < allSortPoses.Count)
            {
                ComboBoxOpenPosesShowNumbers.Items.Add(curPositionNum + " > " + (curPositionNum + countPosOnPage));
                curPositionNum += countPosOnPage;
            }

            _volumeControlUpdated = true;
            ComboBoxOpenPosesShowNumbers.SelectedIndex = ComboBoxOpenPosesShowNumbers.Items.Count - 1;
            ComboBoxOpenPosesShowNumbers.SelectionChanged += ComboBoxOpenPosesShowNumbers_SelectionChanged;

            _volumeControlUpdated = true;
        }

        private void ComboBoxOpenPosesShowNumbers_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            try
            {
                _volumeControlUpdated = true;
                RePaint();
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        #endregion

        #region Close positions grid

        private DataGridView _closePositionGrid;

        private void CreateClosePositionTable()
        {
            try
            {
                _closePositionGrid = CreateNewTable();
                HostClosePosition.Child = _closePositionGrid;
                _closePositionGrid.Click += _closePositionGrid_Click;
                _closePositionGrid.DoubleClick += _closePositionGrid_DoubleClick;
                _closePositionGrid.DataError += _gridStatistics_DataError;
            }
            catch (Exception ex)
            {
                SendNewLogMessage(ex.ToString(), LogMessageType.Error);
            }
        }

        private void PaintClosePositionGrid()
        {
            try
            {

                if (_closePositionGrid == null)
                {
                    CreateClosePositionTable();
                }

                HostClosePosition.Child = null;

                _closePositionGrid.Rows.Clear();
                _closePositionGrid.ClearSelection();

                if (ComboBoxClosePosesShowNumbers.Items == null ||
                    ComboBoxClosePosesShowNumbers.Items.Count == 0)
                {
                    return;
                }

                string selectNums = ComboBoxClosePosesShowNumbers.SelectedItem.ToString().Replace(" ", "");

                int startNum = Convert.ToInt32(selectNums.Split('>')[0]);
                int endNum = Convert.ToInt32(selectNums.Split('>')[1]);

                List<Position> closePositions = GetClosePositions();

                if (closePositions == null ||
                    closePositions.Count == 0)
                {
                    HostClosePosition.Child = _openPositionGrid;
                    return;
                }

                List<DataGridViewRow> rows = new List<DataGridViewRow>();

                for (int i = startNum; i < endNum + 1 && i < closePositions.Count; i++)
                {
                    rows.Insert(0, GetRow(closePositions[i]));
                }

                if (rows.Count > 0)
                {
                    HostClosePosition.Child = null;
                    _closePositionGrid.Rows.AddRange(rows.ToArray());
                    HostClosePosition.Child = _closePositionGrid;
                    return;
                }
            }
            catch (Exception ex)
            {
                SendNewLogMessage(ex.ToString(), LogMessageType.Error);
            }
            HostClosePosition.Child = _openPositionGrid;
        }

        private List<Position> GetClosePositions()
        {
            List<Journal> myJournals = GetActiveJournals();

            if (myJournals == null
                || myJournals.Count == 0)
            {
                return null;
            }

            List<Position> positionsAll = new List<Position>();

            for (int i = 0; i < myJournals.Count; i++)
            {
                if (myJournals[i].AllPosition != null) positionsAll.AddRange(myJournals[i].AllPosition);
            }

            if (positionsAll == null
                || positionsAll.Count == 0)
            {
                return null;
            }

            for (int i = 0; i < positionsAll.Count; i++)
            {
                if (positionsAll[i] == null)
                {
                    positionsAll.RemoveAt(i);
                    i--;
                }
            }

            if (_selectedSecurities.Count > 0)
            {
                for (int i = 0; i < positionsAll.Count; i++)
                {
                    Position curPos = positionsAll[i];

                    string secName = curPos.SecurityName;

                    bool isAccepted = false;

                    for (int i2 = 0; i2 < _selectedSecurities.Count; i2++)
                    {
                        if (_selectedSecurities[i2].Name == secName)
                        {
                            isAccepted = _selectedSecurities[i2].IsOn;
                            break;
                        }
                    }

                    if (isAccepted == false)
                    {
                        positionsAll.RemoveAt(i);
                        i--;
                    }
                }
            }

            bool showDontOpenPositions = false;

            if (CheckBoxShowDontOpenPoses.IsChecked.HasValue)
            {
                showDontOpenPositions = CheckBoxShowDontOpenPoses.IsChecked.Value;
            }

            List<Position> closePositions = new List<Position>();

            for (int i = 0; i < positionsAll.Count; i++)
            {
                Position pos = positionsAll[i];

                if (pos == null)
                {
                    continue;
                }

                if ((_startTime != DateTime.MinValue && pos.TimeCreate < _startTime)
                   ||
                   (_endTime != DateTime.MinValue && pos.TimeCreate > _endTime))
                {
                    continue;
                }

                if (pos.State == PositionStateType.Done)
                {
                    closePositions.Add(pos);
                }
                else if (pos.State == PositionStateType.OpeningFail
                    && showDontOpenPositions == true)
                {
                    closePositions.Add(pos);
                }
            }

            if (closePositions.Count == 0)
            {
                return null;
            }

            if (closePositions.Count > 1)
            {
                closePositions = closePositions.OrderBy(x => x.TimeClose).ToList();
            }

            return closePositions;
        }

        private void CheckBoxShowDontOpenPoses_Click(object sender, RoutedEventArgs e)
        {
            PaintClosePositionGrid();
        }

        private void _closePositionGrid_DoubleClick(object sender, EventArgs e)
        {
            try
            {
                if (_closePositionGrid.Rows.Count == 0)
                {
                    return;
                }

                int number;

                try
                {
                    number = Convert.ToInt32(_closePositionGrid.Rows[_closePositionGrid.CurrentCell.RowIndex].Cells[0].Value);
                }
                catch (Exception)
                {
                    return;
                }

                ShowPositionDialog(number);
            }
            catch (Exception ex)
            {
                SendNewLogMessage(ex.ToString(), LogMessageType.Error);
            }
        }

        private void _closePositionGrid_Click(object sender, EventArgs e)
        {
            try
            {
                MouseEventArgs mouse = (MouseEventArgs)e;
                if (mouse.Button != MouseButtons.Right)
                {
                    return;
                }

                ToolStripMenuItem[] items = new ToolStripMenuItem[4];

                items[0] = new ToolStripMenuItem { Text = OsLocalization.Journal.PositionMenuItem8 };
                items[0].Click += CloseDealMoreInfo_Click;

                items[1] = new ToolStripMenuItem { Text = OsLocalization.Journal.PositionMenuItem9 };
                items[1].Click += CloseDealDelete_Click;

                items[2] = new ToolStripMenuItem { Text = OsLocalization.Journal.PositionMenuItem10 };
                items[2].Click += CloseDealClearAll_Click;

                items[3] = new ToolStripMenuItem { Text = OsLocalization.Journal.PositionMenuItem11 };
                items[3].Click += CloseDealSaveInFile_Click;

                ContextMenuStrip menu = new ContextMenuStrip();
                menu.Items.AddRange(items);

                _closePositionGrid.ContextMenuStrip = menu;
                _closePositionGrid.ContextMenuStrip.Show(_closePositionGrid, new System.Drawing.Point(mouse.X, mouse.Y));
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        private void CloseDealSaveInFile_Click(object sender, EventArgs e)
        {
            try
            {
                SaveFileDialog myDialog = new SaveFileDialog();
                myDialog.Filter = "*.txt|";
                myDialog.ShowDialog();

                if (string.IsNullOrEmpty(myDialog.FileName))
                {
                    System.Windows.Forms.MessageBox.Show(OsLocalization.Journal.Message1);
                    return;
                }

                StringBuilder workSheet = new StringBuilder();
                workSheet.Append(OsLocalization.Entity.PositionColumn1 + ";");
                workSheet.Append(OsLocalization.Entity.PositionColumn2 + ";");
                workSheet.Append(OsLocalization.Entity.PositionColumn3 + ";");
                workSheet.Append(OsLocalization.Entity.PositionColumn4 + ";");
                workSheet.Append(OsLocalization.Entity.PositionColumn5 + ";");
                workSheet.Append(OsLocalization.Entity.PositionColumn6 + ";");
                workSheet.Append(OsLocalization.Entity.PositionColumn7 + ";");
                workSheet.Append(OsLocalization.Entity.PositionColumn8 + ";");
                workSheet.Append(OsLocalization.Entity.PositionColumn9 + ";");
                workSheet.Append(OsLocalization.Entity.PositionColumn10 + ";");
                workSheet.Append(OsLocalization.Entity.PositionColumn11 + ";");
                workSheet.Append(OsLocalization.Entity.PositionColumn12 + ";");
                workSheet.Append(OsLocalization.Entity.PositionColumn13 + ";");
                workSheet.Append(OsLocalization.Entity.PositionColumn14 + ";");
                workSheet.Append(OsLocalization.Entity.PositionColumn15 + ";");
                workSheet.Append(OsLocalization.Entity.PositionColumn16 + ";");
                workSheet.Append(OsLocalization.Entity.PositionColumn17 + ";");
                workSheet.Append(OsLocalization.Entity.PositionColumn18 + ";");
                workSheet.Append(OsLocalization.Entity.PositionColumn19 + "\r\n");

                for (int i = 0; i < _closePositionGrid.Rows.Count; i++)
                {
                    workSheet.Append(_closePositionGrid.Rows[i].Cells[0].Value + ";");
                    workSheet.Append(_closePositionGrid.Rows[i].Cells[1].Value + ";");
                    workSheet.Append(_closePositionGrid.Rows[i].Cells[2].Value + ";");

                    workSheet.Append(_closePositionGrid.Rows[i].Cells[3].Value + ";");
                    workSheet.Append(_closePositionGrid.Rows[i].Cells[4].Value + ";");
                    workSheet.Append(_closePositionGrid.Rows[i].Cells[5].Value + ";");
                    workSheet.Append(_closePositionGrid.Rows[i].Cells[6].Value + ";");
                    workSheet.Append(_closePositionGrid.Rows[i].Cells[7].Value + ";");
                    workSheet.Append(_closePositionGrid.Rows[i].Cells[8].Value + ";");
                    workSheet.Append(_closePositionGrid.Rows[i].Cells[9].Value + ";");
                    workSheet.Append(_closePositionGrid.Rows[i].Cells[10].Value + ";");
                    workSheet.Append(_closePositionGrid.Rows[i].Cells[11].Value + ";");
                    workSheet.Append(_closePositionGrid.Rows[i].Cells[12].Value + ";");
                    workSheet.Append(_closePositionGrid.Rows[i].Cells[13].Value + ";");
                    workSheet.Append(_closePositionGrid.Rows[i].Cells[14].Value + ";");
                    workSheet.Append(_closePositionGrid.Rows[i].Cells[15].Value + ";");
                    workSheet.Append(_closePositionGrid.Rows[i].Cells[16].Value + ";");
                    workSheet.Append(_closePositionGrid.Rows[i].Cells[17].Value + ";");
                    workSheet.Append(_closePositionGrid.Rows[i].Cells[18].Value + "\r\n");
                }

                string fileName = myDialog.FileName;
                if (fileName.Split('.').Length == 1)
                {
                    fileName = fileName + ".txt";
                }

                StreamWriter writer = new StreamWriter(fileName);
                writer.Write(workSheet);
                writer.Close();
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        private void CloseDealMoreInfo_Click(object sender, EventArgs e)
        {
            int number;
            try
            {
                if (_closePositionGrid.CurrentCell == null)
                {
                    return;
                }
                number = Convert.ToInt32(_closePositionGrid.Rows[_closePositionGrid.CurrentCell.RowIndex].Cells[0].Value);
            }
            catch (Exception)
            {
                return;
            }

            ShowPositionDialog(number);
        }

        private void CloseDealDelete_Click(object sender, EventArgs e)
        {
            try
            {
                int number;
                try
                {
                    if (_closePositionGrid.CurrentCell == null)
                    {
                        return;
                    }
                    number = Convert.ToInt32(_closePositionGrid.Rows[_closePositionGrid.CurrentCell.RowIndex].Cells[0].Value);
                }
                catch (Exception)
                {
                    return;
                }

                AcceptDialogUi ui = new AcceptDialogUi(OsLocalization.Journal.Message3);
                ui.ShowDialog();

                if (ui.UserAcceptAction == false)
                {
                    return;
                }

                DeletePosition(number);
                SelectCLosePosesPages();
                RePaint();
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        private void CloseDealClearAll_Click(object sender, EventArgs e)
        {
            try
            {
                List<int> numbers = new List<int>();
                try
                {
                    for (int i = 0; i < _closePositionGrid.Rows.Count; i++)
                    {
                        numbers.Add(Convert.ToInt32(_closePositionGrid.Rows[i].Cells[0].Value));
                    }
                    List<Position> poses = new List<Position>();

                    for (int i = 0; i < _botsJournals.Count; i++)
                    {
                        for (int j = 0; j < _botsJournals[i]._Tabs.Count; j++)
                        {
                            poses.AddRange(_botsJournals[i]._Tabs[j].Journal.AllPosition.FindAll(p => p.State == PositionStateType.OpeningFail));
                        }
                    }

                    for (int i = 0; i < poses.Count; i++)
                    {
                        numbers.Add(poses[i].Number);
                    }

                }
                catch (Exception)
                {
                    return;
                }

                AcceptDialogUi ui = new AcceptDialogUi(OsLocalization.Journal.Message4);
                ui.ShowDialog();

                if (ui.UserAcceptAction == false)
                {
                    return;
                }

                for (int i = 0; i < numbers.Count; i++)
                {
                    DeletePosition(numbers[i]);
                }
                SelectCLosePosesPages();
                RePaint();
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        private void ComboBoxClosePosesOnPage_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            try
            {
                SelectCLosePosesPages();
                RePaint();
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        private void SelectCLosePosesPages()
        {
            _volumeControlUpdated = true;
            ComboBoxClosePosesShowNumbers.SelectionChanged -= ComboBoxClosePosesShowNumbers_SelectionChanged;
            ComboBoxClosePosesShowNumbers.Items.Clear();

            string selectedPosesOnPage = ComboBoxClosePosesOnPage.SelectedItem.ToString();

            int countPosOnPage = 0;

            if (selectedPosesOnPage == OsLocalization.Journal.Label18)
            {
                countPosOnPage = 100;
            }
            else if (selectedPosesOnPage == OsLocalization.Journal.Label19)
            {
                countPosOnPage = 500;
            }
            else if (selectedPosesOnPage == OsLocalization.Journal.Label20)
            {
                countPosOnPage = 1000;
            }

            List<Position> closePositions = GetClosePositions();

            if (closePositions == null
                || closePositions.Count == 0)
            {
                return;
            }

            int curPositionNum = 0;

            while (curPositionNum < closePositions.Count)
            {
                ComboBoxClosePosesShowNumbers.Items.Add(curPositionNum + " > " + (curPositionNum + countPosOnPage));
                curPositionNum += countPosOnPage;
            }

            _volumeControlUpdated = true;

            ComboBoxClosePosesShowNumbers.SelectedIndex = ComboBoxClosePosesShowNumbers.Items.Count - 1;
            ComboBoxClosePosesShowNumbers.SelectionChanged += ComboBoxClosePosesShowNumbers_SelectionChanged;

            _volumeControlUpdated = true;
        }

        private void ComboBoxClosePosesShowNumbers_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            try
            {
                _volumeControlUpdated = true;
                RePaint();
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        #endregion

        #region Left panel managment. Bots

        private DataGridView _gridLeftBotsPanel;

        private void CreateBotsGrid()
        {
            try
            {
                _gridLeftBotsPanel
                    = DataGridFactory.GetDataGridView(DataGridViewSelectionMode.CellSelect, DataGridViewAutoSizeRowsMode.AllCells, false);

                _gridLeftBotsPanel.AllowUserToResizeRows = true;
                _gridLeftBotsPanel.ScrollBars = ScrollBars.Vertical;

                CustomDataGridViewCell cell0 = new CustomDataGridViewCell();
                cell0.Style = _gridLeftBotsPanel.DefaultCellStyle;

                DataGridViewComboBoxColumn column0 = new DataGridViewComboBoxColumn();
                //column0.CellTemplate = cell0;
                column0.HeaderText = OsLocalization.Journal.Label9;
                column0.ReadOnly = false;
                column0.AutoSizeMode = DataGridViewAutoSizeColumnMode.AllCells;

                _gridLeftBotsPanel.Columns.Add(column0);

                DataGridViewColumn column1 = new DataGridViewColumn();
                column1.CellTemplate = cell0;
                column1.HeaderText = @"#";
                column1.ReadOnly = true;
                column1.Width = 75;
                column1.AutoSizeMode = DataGridViewAutoSizeColumnMode.AllCells;
                _gridLeftBotsPanel.Columns.Add(column1);

                DataGridViewColumn column2 = new DataGridViewColumn();
                column2.CellTemplate = cell0;
                column2.HeaderText = OsLocalization.Journal.Label10;
                column2.ReadOnly = true;
                column2.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
                _gridLeftBotsPanel.Columns.Add(column2);

                DataGridViewColumn column22 = new DataGridViewColumn();
                column22.CellTemplate = cell0;
                column22.HeaderText = OsLocalization.Journal.Label11;
                column22.ReadOnly = true;
                column22.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
                _gridLeftBotsPanel.Columns.Add(column22);

                DataGridViewCheckBoxColumn column4 = new DataGridViewCheckBoxColumn();
                column4.HeaderText = OsLocalization.Journal.Label12;
                column4.ReadOnly = false;
                column4.AutoSizeMode = DataGridViewAutoSizeColumnMode.AllCells;
                _gridLeftBotsPanel.Columns.Add(column4);

                DataGridViewColumn column5 = new DataGridViewColumn();
                column5.CellTemplate = cell0;
                column5.HeaderText = @"Mult %";
                column5.ReadOnly = false;
                column5.AutoSizeMode = DataGridViewAutoSizeColumnMode.AllCells;
                _gridLeftBotsPanel.Columns.Add(column5);

                HostBotsSelected.Child = _gridLeftBotsPanel;
                HostBotsSelected.Child.Show();

                _gridLeftBotsPanel.CellEndEdit += _gridLeftBotsPanel_CellEndEdit;
                _gridLeftBotsPanel.CellBeginEdit += _gridLeftBotsPanel_CellBeginEdit;
                _gridLeftBotsPanel.DataError += _gridStatistics_DataError;
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        private void PaintBotsGrid()
        {
            try
            {
                if (!TabControlPrime.CheckAccess())
                {
                    TabControlPrime.Dispatcher.Invoke(PaintBotsGrid);
                    return;
                }

                _gridLeftBotsPanel.CellEndEdit -= _gridLeftBotsPanel_CellEndEdit;

                List<PanelGroups> groups = GetGroups(_botsJournals);
                List<DataGridViewRow> rows = new List<DataGridViewRow>();
                List<string> allGroups = GetAllGroups(groups);


                for (int i = 0; i < groups.Count; i++)
                {
                    rows.AddRange(GetGroupRowList(groups[i], allGroups));
                }

                int showRowNum = _gridLeftBotsPanel.FirstDisplayedScrollingRowIndex;

                _gridLeftBotsPanel.Rows.Clear();

                for (int i = 0; i < rows.Count; i++)
                {
                    _gridLeftBotsPanel.Rows.Add(rows[i]);
                }

                if (showRowNum > 0)
                {
                    _gridLeftBotsPanel.FirstDisplayedScrollingRowIndex = showRowNum;
                }

                _gridLeftBotsPanel.CellEndEdit += _gridLeftBotsPanel_CellEndEdit;
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        private bool _needToRaPaintBotsGrid;

        private void LeftBotsPanelPainterThread()
        {
            while (true)
            {
                try
                {
                    if (IsErase == true)
                    {
                        return;
                    }

                    if (_needToRaPaintBotsGrid)
                    {
                        _needToRaPaintBotsGrid = false;
                        PaintBotsGrid();
                        RePaint();
                    }
                    else
                    {
                        Thread.Sleep(200);
                    }
                }
                catch (Exception error)
                {
                    SendNewLogMessage(error.ToString(), LogMessageType.Error);
                    Thread.Sleep(5000);
                }
            }
        }

        private void LoadSettings()
        {
            if (!File.Exists(@"Engine\LayoutJournal" + JournalName + ".txt"))
            {
                return;
            }

            try
            {
                using (StreamReader reader = new StreamReader(@"Engine\LayoutJournal" + JournalName + ".txt"))
                {
                    _leftPanelIsHide = Convert.ToBoolean(reader.ReadLine());
                    string profitType = reader.ReadLine();

                    if (string.IsNullOrEmpty(profitType) == false)
                    {
                        ComboBoxChartType.SelectedItem = profitType;
                    }

                    if(reader.EndOfStream == true)
                    {
                        return;
                    }

                    _visibleEquityLine = Convert.ToBoolean(reader.ReadLine());
                    _visibleLongLine = Convert.ToBoolean(reader.ReadLine());
                    _visibleShortLine = Convert.ToBoolean(reader.ReadLine());

                    string benchmark = reader.ReadLine();

                    if (string.IsNullOrEmpty(benchmark) == false)
                    {
                        ComboBoxBenchmark.SelectedItem = benchmark;
                    }

                    reader.Close();
                }
            }
            catch (Exception)
            {
                // ignore
            }

            if (_leftPanelIsHide)
            {
                HideLeftPanel();
            }
            else
            {
                ShowLeftPanel();
            }
        }

        private void SaveSettings()
        {
            try
            {
                using (StreamWriter writer = new StreamWriter(@"Engine\LayoutJournal" + JournalName + ".txt", false))
                {
                    writer.WriteLine(_leftPanelIsHide);
                    writer.WriteLine(ComboBoxChartType.SelectedItem.ToString());
                    writer.WriteLine(_visibleEquityLine.ToString());
                    writer.WriteLine(_visibleLongLine.ToString());
                    writer.WriteLine(_visibleShortLine.ToString());
                    writer.WriteLine(ComboBoxBenchmark.SelectedItem.ToString());

                    writer.Close();
                }
            }
            catch (Exception)
            {
                // ignore
            }
        }

        private List<string> GetAllGroups(List<PanelGroups> groups)
        {
            try
            {
                List<string> groupsInStrArray = new List<string>();

                for (int i = 0; i < groups.Count; i++)
                {
                    groupsInStrArray.Add(groups[i].BotGroup);
                }
                return groupsInStrArray;
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }

            return null;
        }

        private List<DataGridViewRow> GetGroupRowList(PanelGroups group, List<string> groupsAll)
        {
            try
            {
                List<DataGridViewRow> rows = new List<DataGridViewRow>();

                DataGridViewRow row = new DataGridViewRow();

                DataGridViewComboBoxCell groupBox = new DataGridViewComboBoxCell();
                groupBox.Items.Add(group.BotGroup);
                groupBox.Value = group.BotGroup;
                row.Cells.Add(groupBox); // группа
                groupBox.ReadOnly = true;

                row.Cells.Add(new DataGridViewTextBoxCell()); // номер
                row.Cells.Add(new DataGridViewTextBoxCell()); // имя
                row.Cells.Add(new DataGridViewTextBoxCell()); // класс 

                DataGridViewCheckBoxCell cell = new DataGridViewCheckBoxCell();
                cell.Value = group.Panels[0].IsOn;

                row.Cells.Add(cell); // вкл / выкл

                row.Cells.Add(new DataGridViewTextBoxCell()); // мультипликатор
                rows.Add(row);

                for (int i = 0; i < group.Panels.Count; i++)
                {
                    rows.AddRange(GetPanelRowList(group.Panels[i], groupsAll, i + 1));
                }

                return rows;
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
            return null;
        }

        private List<DataGridViewRow> GetPanelRowList(BotPanelJournal panel, List<string> groupNames, int panelNum)
        {
            try
            {
                List<DataGridViewRow> rows = new List<DataGridViewRow>();

                DataGridViewRow row = new DataGridViewRow();

                DataGridViewComboBoxCell groupBox = new DataGridViewComboBoxCell();

                for (int i = 0; i < groupNames.Count; i++)
                {
                    groupBox.Items.Add(groupNames[i]);
                }
                groupBox.Items.Add("new");
                groupBox.Value = panel.BotGroup;

                row.Cells.Add(groupBox); // группа

                row.Cells.Add(new DataGridViewTextBoxCell()); // номер
                row.Cells[row.Cells.Count - 1].Value = panelNum;

                row.Cells.Add(new DataGridViewTextBoxCell()); // имя
                row.Cells[row.Cells.Count - 1].Value = panel.BotName;

                row.Cells.Add(new DataGridViewTextBoxCell()); // класс 
                row.Cells[row.Cells.Count - 1].Value = panel.BotClass;

                DataGridViewCheckBoxCell cell = new DataGridViewCheckBoxCell();
                cell.Value = panel.IsOn.ToString();

                row.Cells.Add(cell); // вкл / выкл

                row.Cells.Add(new DataGridViewTextBoxCell()); // мультипликатор
                row.Cells[row.Cells.Count - 1].Value = panel.Mult.ToString();

                rows.Add(row);

                return rows;
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
            return null;
        }

        private List<PanelGroups> GetGroups(List<BotPanelJournal> oldPanels)
        {
            try
            {
                List<PanelGroups> groups = new List<PanelGroups>();

                for (int i = 0; i < oldPanels.Count; i++)
                {
                    PanelGroups myGroup = groups.Find(g => g.BotGroup == oldPanels[i].BotGroup);

                    if (myGroup == null)
                    {
                        myGroup = new PanelGroups();
                        myGroup.BotGroup = oldPanels[i].BotGroup;
                        groups.Add(myGroup);
                    }

                    myGroup.Panels.Add(oldPanels[i]);
                }

                return groups;
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
            return null;
        }

        private List<Journal> GetActiveJournals()
        {
            try
            {
                List<Journal> journals = new List<Journal>();

                for (int i = 0; i < _botsJournals.Count; i++)
                {
                    BotPanelJournal panel = _botsJournals[i];

                    for (int i2 = 0; i2 < panel._Tabs.Count; i2++)
                    {
                        if (panel.IsOn)
                        {
                            journals.Add(panel._Tabs[i2].Journal);

                            List<Position> poses = panel._Tabs[i2].Journal.AllPosition;

                            for (int i3 = 0; poses != null && i3 < poses.Count; i3++)
                            {
                                Position pos = poses[i3];

                                if (pos == null)
                                {
                                    continue;
                                }

                                pos.MultToJournal = panel.Mult;
                            }

                        }
                    }
                }

                return journals;
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }

            return null;
        }

        private void LoadGroups()
        {
            string path = @"Engine\" + _startProgram + @"JournalSettings.txt";

            //_botsJournals;

            if (!File.Exists(path))
            {
                return;
            }
            try
            {
                using (StreamReader reader = new StreamReader(path))
                {
                    while (reader.EndOfStream == false)
                    {
                        string botString = reader.ReadLine();

                        if (string.IsNullOrEmpty(botString))
                        {
                            continue;
                        }

                        string[] saveArray = botString.Split('&');

                        string botName = saveArray[0];
                        string botGroup = saveArray[1];
                        decimal mult = saveArray[2].ToDecimal();
                        bool isOn = Convert.ToBoolean(saveArray[3]);

                        BotPanelJournal journal = _botsJournals.Find(b => b.BotName == botName);

                        if (journal == null)
                        {
                            continue;
                        }

                        journal.BotGroup = botGroup;
                        journal.Mult = mult;
                        journal.IsOn = isOn;
                    }

                    reader.Close();
                }
            }
            catch (Exception)
            {
                // ignore
            }
        }

        private void SaveGroups()
        {
            if (_botsJournals.Count == 1)
            {
                return;
            }
            try
            {
                string path = @"Engine\" + _startProgram + @"JournalSettings.txt";

                using (StreamWriter writer = new StreamWriter(path, false)
                    )
                {
                    for (int i = 0; i < _botsJournals.Count; i++)
                    {
                        string res = _botsJournals[i].BotName +
                            "&" + _botsJournals[i].BotGroup +
                            "&" + _botsJournals[i].Mult +
                            "&" + _botsJournals[i].IsOn;


                        writer.WriteLine(res);
                    }

                    writer.Close();
                }
            }
            catch (Exception)
            {
                // ignore
            }
        }

        private void _gridLeftBotsPanel_CellBeginEdit(object sender, DataGridViewCellCancelEventArgs e)
        {
            try
            {
                if (e.ColumnIndex == 4)
                {
                    _lastChangeRow = e.RowIndex;
                    Task.Run(ChangeOnOffAwait);
                }
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        private void _gridLeftBotsPanel_CellEndEdit(object sender, DataGridViewCellEventArgs e)
        {
            try
            {
                if (e.ColumnIndex == 0)
                {
                    ChangeGroup(e);
                }
                else if (e.ColumnIndex == 5)
                {
                    ChangeMult(e);
                }
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        private int _lastChangeRow;

        private void ChangeOnOffAwait()
        {
            Thread.Sleep(200);
            ChangeOnOff(_lastChangeRow);
        }

        private void ChangeOnOff(int rowIndx)
        {
            try
            {
                if (TextBoxFrom.Dispatcher.CheckAccess() == false)
                {
                    TextBoxFrom.Dispatcher.Invoke(new Action<int>(ChangeOnOff), rowIndx);
                    return;
                }

                string textInCell = _gridLeftBotsPanel.Rows[rowIndx].Cells[4].Value.ToString();

                BotPanelJournal bot = GetBotByNum(rowIndx);

                if (bot == null)
                {
                    ChangeOnOffByGroup(rowIndx);
                    return;
                }

                if (Convert.ToBoolean(textInCell) == false)
                {
                    bot.IsOn = true;
                }
                else
                {
                    bot.IsOn = false;
                }

                IsSlide = false;
                SaveGroups();
                CreatePositionsLists();
                CreateSlidersShowPositions();
                _needToRaPaintBotsGrid = true;
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        private void ChangeOnOffByGroup(int rowIndx)
        {
            string textInCell = _gridLeftBotsPanel.Rows[rowIndx].Cells[4].Value.ToString();

            PanelGroups group = GetGroupByNum(rowIndx);

            if (group == null)
            {
                return;
            }

            for (int i = 0; i < group.Panels.Count; i++)
            {
                if (textInCell == "True")
                {
                    group.Panels[i].IsOn = false;
                }
                else if (textInCell == "False")
                {
                    group.Panels[i].IsOn = true;
                }
            }

            SaveGroups();
            _needToRaPaintBotsGrid = true;
            CreatePositionsLists();
        }

        private void ChangeMult(DataGridViewCellEventArgs e)
        {
            string textInCell = _gridLeftBotsPanel.Rows[e.RowIndex].Cells[5].Value.ToString();

            BotPanelJournal bot = GetBotByNum(e.RowIndex);

            if (bot == null)
            {
                return;
            }

            try
            {
                bot.Mult = textInCell.ToDecimal();
            }
            catch
            {
                return;
            }

            SaveGroups();
            RePaint();
        }

        private void ChangeGroup(DataGridViewCellEventArgs e)
        {
            string textInCell = _gridLeftBotsPanel.Rows[e.RowIndex].Cells[0].Value.ToString();

            BotPanelJournal bot = GetBotByNum(e.RowIndex);

            if (bot == null)
            {
                return;
            }

            if (textInCell == "new")
            { // Создаём новую группу
                List<PanelGroups> groups = GetGroups(_botsJournals);
                List<string> allGroups = GetAllGroups(groups);

                NewGroupAddInJournalUi ui = new NewGroupAddInJournalUi(allGroups);
                ui.WindowStartupLocation = WindowStartupLocation.CenterScreen;
                ui.ShowDialog();

                if (ui.IsAccepted == false)
                {
                    return;
                }
                bot.BotGroup = ui.NewGroupName;
            }
            else
            { // обновляем группу у бота
                bot.BotGroup = textInCell;
            }

            SaveGroups();
            _needToRaPaintBotsGrid = true;
        }

        private BotPanelJournal GetBotByNum(int num)
        {
            List<PanelGroups> groups = GetGroups(_botsJournals);

            int curBotNum = 1;

            for (int i = 0; i < groups.Count; i++)
            {
                for (int i2 = 0; i2 < groups[i].Panels.Count; i2++)
                {
                    if (num == curBotNum)
                    {
                        return groups[i].Panels[i2];
                    }

                    curBotNum++;
                }
                curBotNum++;
            }

            return null;
        }

        private PanelGroups GetGroupByNum(int num)
        {
            List<PanelGroups> groups = GetGroups(_botsJournals);

            int curBotNum = 1;

            for (int i = 0; i < groups.Count; i++)
            {
                for (int i2 = 0; i2 < groups[i].Panels.Count; i2++)
                {
                    if (curBotNum >= num)
                    {
                        return groups[i];
                    }

                    curBotNum++;
                }
                curBotNum++;
            }

            return null;
        }

        private void ButtonHideLeftPanel_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                HideLeftPanel();
                SaveSettings();
            }
            catch (Exception ex)
            {
                SendNewLogMessage(ex.ToString(), LogMessageType.Error);
            }
        }

        private void ButtonShowLeftPanel_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                ShowLeftPanel();
                SaveSettings();
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        private void HideLeftPanel()
        {
            try
            {
                // GridTabPrime
                GridActivBots.Visibility = Visibility.Hidden;
                ButtonShowLeftPanel.Visibility = Visibility.Visible;
                GridTabPrime.Margin = new Thickness(0, 0, -0.333, -0.333);

                //this.MinWidth = 950;
                //this.MinHeight = 300;
                _leftPanelIsHide = true;
            }
            catch (Exception ex)
            {
                SendNewLogMessage(ex.ToString(), LogMessageType.Error);
            }
        }

        private void ShowLeftPanel()
        {
            try
            {
                GridActivBots.Visibility = Visibility.Visible;
                ButtonShowLeftPanel.Visibility = Visibility.Hidden;
                GridTabPrime.Margin = new Thickness(510, 0, -0.333, -0.333);

                //this.MinWidth = 1450;
                //this.MinHeight = 500;
                _leftPanelIsHide = false;
            }
            catch (Exception ex)
            {
                SendNewLogMessage(ex.ToString(), LogMessageType.Error);
            }
        }

        private bool _leftPanelIsHide;

        #endregion

        #region Left panel managment. Securities

        private DataGridView _gridLeftSecuritiesPanel;

        private void CreateSecuritiesFilterGrid()
        {
            try
            {
                _gridLeftSecuritiesPanel
                    = DataGridFactory.GetDataGridView(DataGridViewSelectionMode.CellSelect, DataGridViewAutoSizeRowsMode.AllCells, false);

                _gridLeftSecuritiesPanel.AllowUserToResizeRows = true;
                _gridLeftSecuritiesPanel.ScrollBars = ScrollBars.Vertical;

                CustomDataGridViewCell cell0 = new CustomDataGridViewCell();
                cell0.Style = _gridLeftSecuritiesPanel.DefaultCellStyle;

                DataGridViewColumn column1 = new DataGridViewColumn();
                column1.CellTemplate = cell0;
                column1.HeaderText = @"#";
                column1.ReadOnly = true;
                column1.Width = 75;
                _gridLeftSecuritiesPanel.Columns.Add(column1);

                DataGridViewColumn column2 = new DataGridViewColumn();
                column2.CellTemplate = cell0;
                column2.HeaderText = OsLocalization.Journal.Label10;
                column2.ReadOnly = true;
                column2.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
                _gridLeftSecuritiesPanel.Columns.Add(column2);

                DataGridViewCheckBoxColumn column4 = new DataGridViewCheckBoxColumn();
                column4.HeaderText = OsLocalization.Journal.Label12;
                column4.ReadOnly = false;
                column4.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
                column4.HeaderCell.Style.Alignment = DataGridViewContentAlignment.MiddleCenter;
                _gridLeftSecuritiesPanel.Columns.Add(column4);

                HostSecuritiesSelected.Child = _gridLeftSecuritiesPanel;
                HostSecuritiesSelected.Child.Show();

                _gridLeftSecuritiesPanel.CellClick += _gridLeftSecuritiesPanel_CellClick;
                _gridLeftSecuritiesPanel.DataError += _gridStatistics_DataError;
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        private void PaintSecuritiesFilterGrid()
        {
            try
            {
                if (!TabControlPrime.CheckAccess())
                {
                    TabControlPrime.Dispatcher.Invoke(PaintSecuritiesFilterGrid);
                    return;
                }

                List<SecurityToPaint> securities = GetAllSecuritiesToPaint();

                int showRowNum = _gridLeftSecuritiesPanel.FirstDisplayedScrollingRowIndex;

                _gridLeftSecuritiesPanel.Rows.Clear();

                // first row

                DataGridViewRow firstRow = new DataGridViewRow();

                firstRow.Cells.Add(new DataGridViewTextBoxCell());
                firstRow.Cells.Add(new DataGridViewTextBoxCell());

                DataGridViewCheckBoxCell cell = new DataGridViewCheckBoxCell();
                cell.Value = true;
                firstRow.Cells.Add(cell); // вкл / выкл

                _gridLeftSecuritiesPanel.Rows.Add(firstRow);

                // securities row

                for (int i = 0; i < securities.Count; i++)
                {
                    DataGridViewRow newRow = new DataGridViewRow();

                    newRow.Cells.Add(new DataGridViewTextBoxCell()); // номер
                    newRow.Cells[0].Value = i + 1;

                    newRow.Cells.Add(new DataGridViewTextBoxCell()); // имя
                    newRow.Cells[1].Value = securities[i].Name;

                    DataGridViewCheckBoxCell cellCheckBox = new DataGridViewCheckBoxCell();
                    cellCheckBox.Value = securities[i].IsOn;
                    newRow.Cells.Add(cellCheckBox); // вкл / выкл

                    _gridLeftSecuritiesPanel.Rows.Add(newRow);
                }

                if (showRowNum > 0 &&
                    _gridLeftSecuritiesPanel.Rows.Count != 0 &&
                    showRowNum <= _gridLeftSecuritiesPanel.Rows.Count)
                {
                    _gridLeftSecuritiesPanel.FirstDisplayedScrollingRowIndex = showRowNum;
                }
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        private void _gridLeftSecuritiesPanel_CellClick(object sender, DataGridViewCellEventArgs e)
        {
            try
            {
                if (e.ColumnIndex == 2)
                {
                    _lastSecuritiesEvent = e;
                    _gridLeftSecuritiesPanel.Rows[e.RowIndex].Cells[2].Selected = false;
                    TextBoxFrom.Focus();
                    Task.Run(ChangeSecuritiesOnOff);
                }
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }

        }

        private DataGridViewCellEventArgs _lastSecuritiesEvent;

        private void ChangeSecuritiesOnOff()
        {
            try
            {
                if (TextBoxFrom.Dispatcher.CheckAccess() == false)
                {
                    Thread.Sleep(500);
                    TextBoxFrom.Dispatcher.Invoke(new Action(ChangeSecuritiesOnOff));
                    return;
                }

                int rowIndex = _lastSecuritiesEvent.RowIndex;

                int columnIndex = _lastSecuritiesEvent.ColumnIndex;

                if (columnIndex != 2)
                {
                    return;
                }

                if (rowIndex == 0)
                { // меняем у всех вкл/выкл

                    _gridLeftSecuritiesPanel.CellClick -= _gridLeftSecuritiesPanel_CellClick;

                    DataGridViewCheckBoxCell cell = (DataGridViewCheckBoxCell)_gridLeftSecuritiesPanel.Rows[0].Cells[2];

                    bool curValue = Convert.ToBoolean(cell.EditedFormattedValue.ToString());

                    for (int i = 1; i < _gridLeftSecuritiesPanel.Rows.Count; i++)
                    {
                        _gridLeftSecuritiesPanel.Rows[i].Cells[2].Value = curValue;
                    }

                    _gridLeftSecuritiesPanel.CellClick += _gridLeftSecuritiesPanel_CellClick;
                }

                UpDateSelectedSecurities();
                RePaint();
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }


        }

        private List<SecurityToPaint> GetAllSecuritiesToPaint()
        {
            List<SecurityToPaint> securities = new List<SecurityToPaint>();

            if (_allPositions == null)
            {
                return securities;
            }

            for (int i = 0; i < _allPositions.Count; i++)
            {
                Position position = _allPositions[i];

                string secName = position.SecurityName;

                SecurityToPaint security = new SecurityToPaint();
                security.Name = secName;
                security.IsOn = true;

                bool isInArray = false;

                for (int i2 = 0; i2 < securities.Count; i2++)
                {
                    if (securities[i2].Name == security.Name)
                    {
                        isInArray = true;
                        break;
                    }
                }

                if (isInArray == false)
                {
                    securities.Add(security);
                }
            }

            return securities;
        }

        private List<SecurityToPaint> _selectedSecurities = new List<SecurityToPaint>();

        private void UpDateSelectedSecurities()
        {
            _selectedSecurities.Clear();

            for (int i = 1; i < _gridLeftSecuritiesPanel.Rows.Count; i++)
            {
                DataGridViewRow row = _gridLeftSecuritiesPanel.Rows[i];

                SecurityToPaint newSecurity = new SecurityToPaint();

                if (row.Cells[1].Value == null)
                {
                    continue;
                }

                newSecurity.Name = row.Cells[1].Value.ToString();
                newSecurity.IsOn = Convert.ToBoolean(row.Cells[2].EditedFormattedValue.ToString());

                _selectedSecurities.Add(newSecurity);
            }
        }

        #endregion

        #region Positions managment

        private void CreatePositionsLists()
        {
            try
            {
                // 1 collecting all journals.
                // 1 собираем все журналы
                List<Journal> myJournals = GetActiveJournals();

                if (myJournals == null
                    || myJournals.Count == 0)
                {
                    _allPositions?.Clear();
                    _longPositions?.Clear();
                    _shortPositions?.Clear();
                    _startTime = DateTime.MinValue;
                    _endTime = DateTime.MinValue;

                    return;
                }
                // 2 sorting deals on ALL / Long / Short
                // 2 сортируем сделки на ВСЕ / Лонг / Шорт

                List<Position> positionsAll = new List<Position>();

                for (int i = 0; i < myJournals.Count; i++)
                {
                    if (myJournals[i].AllPosition != null)
                    {
                        positionsAll.AddRange(myJournals[i].AllPosition);
                    }
                }

                for (int i = 0; i < positionsAll.Count; i++)
                {
                    if (positionsAll[i] == null)
                    {
                        positionsAll.RemoveAt(i);
                        i--;
                    }
                }

                if (_selectedSecurities.Count > 0)
                {
                    for (int i = 0; i < positionsAll.Count; i++)
                    {
                        Position curPos = positionsAll[i];

                        string secName = curPos.SecurityName;

                        bool isAccepted = false;

                        for (int i2 = 0; i2 < _selectedSecurities.Count; i2++)
                        {
                            if (string.IsNullOrEmpty(secName)
                                || _selectedSecurities[i2].Name == secName)
                            {
                                isAccepted = _selectedSecurities[i2].IsOn;
                                break;
                            }
                        }

                        if (isAccepted == false)
                        {
                            positionsAll.RemoveAt(i);
                            i--;
                        }
                    }
                }

                int lastPositionsCount = 0;

                if (_allPositions != null
                    && _allPositions.Count != 0)
                {
                    lastPositionsCount = _allPositions.Count;
                }

                if (positionsAll.Count > 1)
                {
                    for (int i = 0; i < positionsAll.Count; i++)
                    {
                        if (positionsAll[i] == null)
                        {
                            positionsAll.RemoveAt(i);
                            i--;
                        }
                    }

                    positionsAll = positionsAll.OrderBy(x => x.TimeOpen).ToList();
                }

                _allPositions = positionsAll.FindAll(p => p.State != PositionStateType.OpeningFail);

                _longPositions = _allPositions.FindAll(p => p.Direction == Side.Buy);
                _shortPositions = _allPositions.FindAll(p => p.Direction == Side.Sell);

                if (_allPositions.Count == 0)
                {
                    return;
                }

                DateTime endTime = DateTime.MinValue;

                DateTime startTime = DateTime.MaxValue;

                for (int i = 0; i < _allPositions.Count; i++)
                {
                    Position curPos = _allPositions[i];

                    DateTime openTime = curPos.TimeOpen;
                    DateTime closeTime = curPos.TimeClose;
                    DateTime createTime = curPos.TimeCreate;

                    if (openTime > endTime)
                    {
                        endTime = openTime;
                    }
                    if (closeTime > endTime)
                    {
                        endTime = closeTime;
                    }
                    if (createTime > endTime)
                    {
                        endTime = createTime;
                    }

                    if (openTime < startTime)
                    {
                        startTime = openTime;
                    }
                    if (endTime < startTime)
                    {
                        startTime = openTime;
                    }
                    if (createTime < startTime)
                    {
                        startTime = createTime;
                    }
                }

                _minTime = _startTime;
                _maxTime = _endTime;

                if (startTime != DateTime.MaxValue
                    && startTime != DateTime.MinValue)
                {
                    startTime = startTime.AddDays(-1);
                }
                else
                {
                    startTime = DateTime.MinValue;
                }

                if (endTime != DateTime.MinValue
                     && endTime != DateTime.MaxValue)
                {
                    endTime = endTime.AddDays(1);
                }
                else
                {
                    endTime = DateTime.MaxValue;
                }

                if (IsSlide == false)
                { // слайдер времени выключен. Просто обновляем
                    _startTime = startTime;
                    _endTime = endTime;
                    _minTime = _startTime;
                    _maxTime = _endTime;
                    CreateSlidersShowPositions();
                }
                else if (IsSlide == true
                  && (_startTime == DateTime.MinValue
                      || _endTime == DateTime.MinValue))
                {
                    _startTime = startTime;
                    _endTime = endTime;
                    _minTime = _startTime;
                    _maxTime = _endTime;
                    CreateSlidersShowPositions();
                }
                else if (lastPositionsCount != _allPositions.Count)
                {
                    _startTime = startTime;
                    _endTime = endTime;
                    _minTime = _startTime;
                    _maxTime = _endTime;
                    CreateSlidersShowPositions();
                }
            }
            catch (Exception ex)
            {
                SendNewLogMessage(ex.ToString(), LogMessageType.Error);
            }
        }

        private void DeletePosition(int number)
        {
            try
            {
                for (int i = 0; i < _botsJournals.Count; i++)
                {
                    for (int i2 = 0; i2 < _botsJournals[i]._Tabs.Count; i2++)
                    {
                        if (_botsJournals[i]._Tabs[i2].Journal.AllPosition == null)
                        {
                            continue;
                        }
                        Position pos = _botsJournals[i]._Tabs[i2].Journal.AllPosition.Find(p => p.Number == number);

                        if (pos != null)
                        {
                            _botsJournals[i]._Tabs[i2].Journal.DeletePosition(pos);
                            return;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                SendNewLogMessage(ex.ToString(), LogMessageType.Error);
            }
        }

        private void ShowPositionDialog(int number)
        {
            try
            {
                for (int i = 0; i < _botsJournals.Count; i++)
                {
                    for (int i2 = 0; i2 < _botsJournals[i]._Tabs.Count; i2++)
                    {
                        if (_botsJournals[i]._Tabs[i2].Journal.AllPosition == null)
                        {
                            continue;
                        }
                        Position pos = _botsJournals[i]._Tabs[i2].Journal.AllPosition.Find(p => p.Number == number);

                        if (pos != null)
                        {
                            PositionUi ui = new PositionUi(pos, _startProgram);
                            ui.ShowDialog();

                            if (ui.PositionChanged)
                            {
                                _botsJournals[i]._Tabs[i2].Journal.Save();
                                _botsJournals[i]._Tabs[i2].Journal.NeedToUpdateStatePositions();
                                PaintSecuritiesFilterGrid();
                                UpDateSelectedSecurities();
                                RePaint();
                            }
                            return;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                SendNewLogMessage(ex.ToString(), LogMessageType.Error);
            }
        }

        private List<Position> _allPositions;

        private List<Position> _longPositions;

        private List<Position> _shortPositions;

        private List<BotPanelJournal> _botsJournals;

        #endregion

        #region Displaying positions by time

        private DateTime _startTime;

        private DateTime _minTime;

        private DateTime _endTime;

        private DateTime _maxTime;

        private bool IsSlide;      // двигались ли слайдеры установки периода

        private void CreateSlidersShowPositions()
        {
            try
            {
                if (TextBoxFrom.Dispatcher.CheckAccess() == false)
                {
                    TextBoxFrom.Dispatcher.Invoke(new Action(CreateSlidersShowPositions));
                    return;
                }

                SliderFrom.ValueChanged -= SliderFrom_ValueChanged;
                SliderTo.ValueChanged -= SliderTo_ValueChanged;

                if (_startTime == DateTime.MinValue
                    || _endTime == DateTime.MinValue)
                {
                    TextBoxFrom.Text = "";
                    TextBoxTo.Text = "";
                    return;
                }

                TextBoxFrom.Text = _startTime.ToString(_currentCulture);
                TextBoxTo.Text = _endTime.ToString(_currentCulture);

                SliderFrom.Minimum = (_startTime - DateTime.MinValue).TotalMinutes;
                SliderFrom.Maximum = (_endTime - DateTime.MinValue).TotalMinutes;
                SliderFrom.Value = (_startTime - DateTime.MinValue).TotalMinutes;

                SliderTo.Minimum = (_startTime - DateTime.MinValue).TotalMinutes;
                SliderTo.Maximum = (_endTime - DateTime.MinValue).TotalMinutes;
                SliderTo.Value = (_startTime - DateTime.MinValue).TotalMinutes;

                if (_endTime != DateTime.MinValue &&
                    SliderFrom.Minimum + SliderTo.Maximum - (_endTime - DateTime.MinValue).TotalMinutes > 0)
                {
                    SliderTo.Value =
                    SliderFrom.Minimum + SliderTo.Maximum - (_endTime - DateTime.MinValue).TotalMinutes;
                }

                SliderFrom.ValueChanged += SliderFrom_ValueChanged;
                SliderTo.ValueChanged += SliderTo_ValueChanged;
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        private void SliderTo_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            try
            {
                TextBoxTo.TextChanged -= TextBoxTo_TextChanged;

                DateTime to = DateTime.MinValue.AddMinutes(SliderFrom.Minimum + SliderFrom.Maximum - SliderTo.Value);
                _endTime = to;
                TextBoxTo.Text = to.ToString(_currentCulture);

                if (SliderFrom.Minimum + SliderFrom.Maximum - SliderTo.Value < SliderFrom.Value)
                {
                    SliderFrom.Value = SliderFrom.Minimum + SliderFrom.Maximum - SliderTo.Value;
                }
                TextBoxTo.TextChanged += TextBoxTo_TextChanged;
                IsSlide = true;
                SaveSettings();
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        private void SliderFrom_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            try
            {
                TextBoxFrom.TextChanged -= TextBoxFrom_TextChanged;

                DateTime from = DateTime.MinValue.AddMinutes(SliderFrom.Value);
                _startTime = from;
                TextBoxFrom.Text = from.ToString(_currentCulture);

                if (SliderFrom.Minimum + SliderFrom.Maximum - SliderTo.Value < SliderFrom.Value)
                {
                    SliderTo.Value = SliderFrom.Minimum + SliderFrom.Maximum - SliderFrom.Value;
                }

                TextBoxFrom.TextChanged += TextBoxFrom_TextChanged;
                IsSlide = true;
                SaveSettings();
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        private void TextBoxTo_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            try
            {
                DateTime to;
                try
                {
                    to = Convert.ToDateTime(TextBoxTo.Text, _currentCulture);

                    if (to < _minTime ||
                        to > _maxTime)
                    {
                        throw new Exception();
                    }
                }
                catch (Exception)
                {
                    TextBoxTo.Text = _endTime.ToString(_currentCulture);
                    return;
                }

                _endTime = to;
                // SliderTo.Value = SliderFrom.Minimum + SliderFrom.Maximum - to.Minute;
                // SliderFrom.Minimum + SliderFrom.Maximum - SliderTo.Value
                SliderTo.Value = SliderFrom.Minimum + SliderTo.Maximum - (to - DateTime.MinValue).TotalMinutes;
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        private void TextBoxFrom_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            try
            {
                DateTime from;
                try
                {
                    from = Convert.ToDateTime(TextBoxFrom.Text, _currentCulture);

                    if (from < _minTime ||
                        from > _maxTime)
                    {
                        throw new Exception();
                    }
                }
                catch (Exception)
                {
                    TextBoxFrom.Text = _startTime.ToString(_currentCulture);
                    return;
                }

                _startTime = from;
                SliderFrom.Value = (_startTime - DateTime.MinValue).TotalMinutes;
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        private void ButtonReload_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                SelectOpenPosesPages();
                SelectCLosePosesPages();
                RePaint();
                _volumeControlUpdated = false;
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        #endregion

        #region Logging

        private void SendNewLogMessage(string message, LogMessageType type)
        {
            if (LogMessageEvent != null)
            {
                LogMessageEvent(message, type);
            }
        }

        public event Action<string, LogMessageType> LogMessageEvent;

        #endregion
    }

    public class BotTabJournal
    {
        public int TabNum;

        public Journal Journal;
    }

    public class BotPanelJournal
    {
        public string BotName;

        public string BotGroup = "none";

        public string BotClass;

        public bool IsOn = true;

        public decimal Mult = 100;

        public List<BotTabJournal> _Tabs;

        public List<Position> AllPositions
        {
            get
            {
                List<Position> poses = new List<Position>();

                for (int i = 0; i < _Tabs.Count; i++)
                {
                    poses.AddRange(_Tabs[i].Journal.AllPosition);
                }

                return poses;
            }
        }
    }

    public class PanelGroups
    {
        public string BotGroup = "none";

        public List<BotPanelJournal> Panels = new List<BotPanelJournal>();
    }

    public class SecurityToPaint
    {
        public string Name;

        public bool IsOn;
    }
}