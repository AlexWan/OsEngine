/*
 * Your rights to use code governed by this license http://o-s-a.net/doc/license_simple_engine.pdf
 * Ваши права на использование кода регулируются данной лицензией http://o-s-a.net/doc/license_simple_engine.pdf
*/

using System;
using System.Collections.Generic;
using System.Drawing;
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
using Rectangle = System.Drawing.Rectangle;
using System.Windows.Forms.DataVisualization.Charting;
using OsEngine.Language;
using Chart = System.Windows.Forms.DataVisualization.Charting.Chart;
using ChartArea = System.Windows.Forms.DataVisualization.Charting.ChartArea;
using MenuItem = System.Windows.Forms.ToolStripMenuItem;
using Series = System.Windows.Forms.DataVisualization.Charting.Series;
using OsEngine.Layout;
using OsEngine.Market;

namespace OsEngine.Journal
{
    /// <summary>
    /// Interaction logic for JournalNewUi.xaml
    /// Логика взаимодействия для JournalNewUi.xaml
    /// </summary>
    public partial class JournalUi
    {
        #region Constructor

        public bool IsErase;

        public JournalUi(List<BotPanelJournal> botsJournals, StartProgram startProgram)
        {
            _startProgram = startProgram;
            _botsJournals = botsJournals;
            InitializeComponent();
            _currentCulture = OsLocalization.CurCulture;

            OsEngine.Layout.StickyBorders.Listen(this);

            TabBots.SizeChanged += TabBotsSizeChanged;
            TabControlPrime.SelectionChanged += TabControlPrime_SelectionChanged;

            ComboBoxChartType.Items.Add("Absolute");
            ComboBoxChartType.Items.Add("Percent 1 contract");
            ComboBoxChartType.SelectedItem = "Absolute";
            ComboBoxChartType.SelectionChanged += ComboBoxChartType_SelectionChanged;

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

            LabelEqutyCharteType.Content = OsLocalization.Journal.Label8;

            CreatePositionsLists(botsJournals);
            TabControlCreateNameBots();

            Closing += JournalUi_Closing;

            this.Activate();
            this.Focus();

            string botNames = "";
            for (int i = 0; i < botsJournals.Count; i++)
            {
                botNames += botsJournals[i].BotName;
            }

            if (botsJournals.Count > 1)
            {
                _journalName = "Journal1Ui_" + "CommonJournal" + startProgram.ToString();
            }
            else
            {
                _journalName = "Journal1Ui_" + botNames + startProgram.ToString();
            }

            GlobalGUILayout.Listen(this, _journalName);
        }

        private string _journalName;

        private CultureInfo _currentCulture;

        private StartProgram _startProgram;

        private void JournalUi_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            try
            {
                IsErase = true;

                TabBots.SizeChanged -= TabBotsSizeChanged;
                TabBots.SelectionChanged -= TabBotsSelectionChanged;
                TabBots.Items.Clear();
                TabControlPrime.SelectionChanged -= TabControlPrime_SelectionChanged;
                TabControlPrime.Items.Clear();

                Closing -= JournalUi_Closing;
                _botsJournals.Clear();
                _botsJournals = null;

                if (_allPositions != null)
                {
                    _allPositions.Clear();
                    _allPositions = null;
                }

                if (_longPositions != null)
                {
                    _longPositions.Clear();
                    _longPositions = null;
                }

                if (_shortPositions != null)
                {
                    _shortPositions.Clear();
                    _shortPositions = null;
                }

                TabControlLeft.SelectionChanged -= TabControlLeftSelectionChanged;
                TabControlLeft.Items.Clear();

                if (_chartEquity != null)
                {
                    _chartEquity.Series.Clear();
                    _chartEquity.ChartAreas.Clear();
                    _chartEquity.Click -= _chartEquity_Click;
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

                if (_chartDd != null)
                {
                    _chartDd.Series.Clear();
                    _chartDd.ChartAreas.Clear();
                    _chartDd.Click -= _chartDd_Click;
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
                    HostOpenPosition.Child.Hide();
                    HostOpenPosition.Child = null;
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
                    HostClosePosition.Child.Hide();
                    HostClosePosition.Child = null;
                    HostClosePosition = null;
                }
            }
            catch (Exception ex)
            {
                SendNewLogMessage(ex.ToString(), LogMessageType.Error);
            }
        }

        #endregion

        #region Auto update thread

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

                    RePaint();
                }
                catch (Exception ex)
                {
                    SendNewLogMessage(ex.ToString(), LogMessageType.Error);
                }
            }
        }

        #endregion

        #region Main Paint Methods

        public void RePaint()
        {
            try
            {
                if (!TabControlLeft.CheckAccess())
                {
                    TabControlLeft.Dispatcher.Invoke(RePaint);
                    return;
                }

                CreatePositionsLists(_botsJournals);

                if (IsErase == true)
                {
                    return;
                }

                if (_allPositions == null)
                {
                    return;
                }

                lock (_paintLocker)
                {

                    if (TabControlPrime.SelectedIndex == 0)
                    {
                        PaintProfitOnChart(_allPositions);
                    }
                    else if (TabControlPrime.SelectedIndex == 1)
                    {
                        bool needShowTickState = !(_botsJournals.Count > 1);

                        PaintStatTable(_allPositions, _longPositions, _shortPositions, needShowTickState);
                    }
                    else if (TabControlPrime.SelectedIndex == 2)
                    {
                        PaintDrawDown(_allPositions);
                    }
                    else if (TabControlPrime.SelectedIndex == 3)
                    {
                        PaintVolumeOnChart(_allPositions);
                    }
                    else if (TabControlPrime.SelectedIndex == 4)
                    {
                        PaintOpenPositionGrid(_allPositions);
                    }
                    else if (TabControlPrime.SelectedIndex == 5)
                    {
                        PaintClosePositionGrid(_allPositions);
                    }
                }
            }
            catch (Exception error)
            {
                System.Windows.MessageBox.Show(error.ToString());
            }
        }

        private string _paintLocker = "_paintLocker";

        private void ComboBoxChartType_SelectionChanged(object sender, SelectionChangedEventArgs e)
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
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        #endregion

        #region Positions managment

        private void CreatePositionsLists(List<BotPanelJournal> _botsJournals)
        {
            try
            {
                if (TabControlLeft == null)
                {
                    return;
                }

                if (TabControlLeft.Dispatcher.CheckAccess() == false)
                {
                    TabControlLeft.Dispatcher.Invoke(new Action<List<BotPanelJournal>>(CreatePositionsLists), _botsJournals);
                    return;
                }

                if (TabControlLeft.SelectedItem == null)
                {
                    return;
                }

                // 1 collecting all journals.
                // 1 собираем все журналы
                List<Journal> myJournals = new List<Journal>();

                for (int i = 0; i < _botsJournals.Count; i++)
                {
                    string name = ((TabItem)TabBots.SelectedItem).Header.ToString();
                    // 1 only take our bots
                    // 1 берём только нашего бота
                    if (name == "V" || name == _botsJournals[i].BotName)
                    {
                        for (int i2 = 0; i2 < _botsJournals[i]._Tabs.Count; i2++)
                        {
                            string nameTab = ((TabItem)TabControlLeft.SelectedItem).Header.ToString().Replace(" ", "");
                            // 2 only take our tabs
                            // 2 берём только наши вкладки
                            if (name == "V" || nameTab == "V" || nameTab == _botsJournals[i]._Tabs[i2].TabNum.ToString())
                            {
                                myJournals.Add(_botsJournals[i]._Tabs[i2].Journal);
                            }
                        }
                    }
                }

                if (myJournals.Count == 0)
                {
                    return;
                }

                // 2 sorting deals on ALL / Long / Short
                // 2 сортируем сделки на ВСЕ / Лонг / Шорт

                List<Position> positionsAll = new List<Position>();

                for (int i = 0; i < myJournals.Count; i++)
                {
                    if (myJournals[i].AllPosition != null) positionsAll.AddRange(myJournals[i].AllPosition);
                }

                List<Position> newPositionsAll = new List<Position>();

                for (int i = 0; i < positionsAll.Count; i++)
                {
                    Position pose = positionsAll[i];

                    if (pose.State == PositionStateType.OpeningFail)
                    {
                        continue;
                    }

                    DateTime timeCreate = pose.TimeCreate;

                    if (newPositionsAll.Count == 0 ||
                        newPositionsAll[newPositionsAll.Count - 1].TimeCreate <= timeCreate)
                    {
                        newPositionsAll.Add(pose);
                    }
                    else if (newPositionsAll[0].TimeCreate >= timeCreate)
                    {
                        newPositionsAll.Insert(0, pose);
                    }
                    else
                    {
                        for (int i2 = 0; i2 < newPositionsAll.Count - 1; i2++)
                        {
                            if (newPositionsAll[i2].TimeCreate <= timeCreate &&
                                newPositionsAll[i2 + 1].TimeCreate >= timeCreate)
                            {
                                newPositionsAll.Insert(i2 + 1, pose);
                                break;
                            }
                        }
                    }
                }

                positionsAll = newPositionsAll;

                _allPositions = positionsAll.FindAll(p => p.State != PositionStateType.OpeningFail);
                _longPositions = _allPositions.FindAll(p => p.Direction == Side.Buy);
                _shortPositions = _allPositions.FindAll(p => p.Direction == Side.Sell);
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
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
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
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
                                RePaint();
                            }
                            return;
                        }
                    }
                }
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        private List<Position> _allPositions;

        private List<Position> _longPositions;

        private List<Position> _shortPositions;

        List<BotPanelJournal> _botsJournals;

        #endregion

        #region Bots tab management

        private void TabControlCreateNameBots()
        {
            try
            {
                TabBots.SelectionChanged -= TabBotsSelectionChanged;
                TabBots.Items.Clear();

                if (_botsJournals.Count >= 1)
                {
                    TabItem item = new TabItem() { Header = "V", FontSize = 12 };
                    TabBots.Items.Add(item);
                }

                for (int i = 0; i < _botsJournals.Count; i++)
                {
                    // addition of a new element
                    // добавление нового элемента
                    TabItem item = new TabItem() { Header = _botsJournals[i].BotName.ToString(), FontSize = 12 };
                    TabBots.Items.Add(item);
                }
                TabBots.SelectionChanged += TabBotsSelectionChanged;
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        private void TabBotsSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            try
            {
                ReloadTabs();
                RePaint();
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        private void ReloadTabs()
        {
            try
            {
                TabControlLeft.SelectionChanged -= TabControlLeftSelectionChanged;
                TabControlLeft.Items.Clear();
                if (_botsJournals.Count != 0) // TabBotsPrime.SelectedItem
                {
                    for (int i = 0; i < _botsJournals.Count; i++)
                    {
                        if (TabBots == null ||
                            TabBots.Items == null
                            || TabBots.Items.Count <= i)
                        {
                            continue;
                        }

                        if (((TabItem)TabBots.Items[TabBots.SelectedIndex]).Header.ToString() ==
                            _botsJournals[i].BotName)
                        {
                            if (_botsJournals[i]._Tabs.Count > 1)
                            {
                                TabItem item = new TabItem() { Header = " V", FontSize = 12 };
                                TabControlLeft.Items.Add(item);
                            }

                            for (int i2 = 0; i2 < _botsJournals[i]._Tabs.Count; i2++)
                            {
                                TabItem item = new TabItem() { Header = " " + i2.ToString(), FontSize = 12 };
                                TabControlLeft.Items.Add(item);
                            }

                            TabControlLeft.SelectedIndex = 0;
                        }
                    }

                    if (((TabItem)TabBots.Items[TabBots.SelectedIndex]).Header.ToString() == "V")
                    {
                        TabItem item = new TabItem() { Header = " V", FontSize = 12 };
                        TabControlLeft.Items.Add(item);
                        TabControlLeft.SelectedItem = item;
                    }

                    TabControlLeft.SelectionChanged += TabControlLeftSelectionChanged;

                }
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        private void TabControlLeftSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            RePaint();
        }

        private void TabBotsSizeChanged(object sender, SizeChangedEventArgs e)
        {
            try
            {
                double up = TabBots.ActualHeight - 14;

                if (up < 0)
                {
                    up = 0;
                }

                GridTabPrime.Margin = new Thickness(5, up, 0, 0);
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

        Chart _chartEquity;

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
                _chartEquity.Click += _chartEquity_Click;

                ChartArea areaLineProfit = new ChartArea("ChartAreaProfit");
                areaLineProfit.Position.Height = 70;
                areaLineProfit.Position.Width = 100;
                areaLineProfit.Position.Y = 0;
                areaLineProfit.CursorX.IsUserSelectionEnabled = false; //allow the user to change the view scope/ разрешаем пользователю изменять рамки представления
                areaLineProfit.CursorX.IsUserEnabled = true; //trait/чертa

                _chartEquity.ChartAreas.Add(areaLineProfit);

                ChartArea areaLineProfitBar = new ChartArea("ChartAreaProfitBar");
                areaLineProfitBar.AlignWithChartArea = "ChartAreaProfit";
                areaLineProfitBar.Position.Height = 30;
                areaLineProfitBar.Position.Width = 100;
                areaLineProfitBar.Position.Y = 70;
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
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        private void PaintProfitOnChart(List<Position> positionsAll)
        {
            try
            {
                if (!TabBots.Dispatcher.CheckAccess())
                {
                    TabBots.Dispatcher.Invoke(
                        new Action<List<Position>>(PaintProfitOnChart), positionsAll);
                    return;
                }

                if (_chartEquity == null)
                {
                    CreateChartProfit();
                }

                _chartEquity.Series.Clear();

                Series profit = new Series("SeriesProfit");
                profit.ChartType = SeriesChartType.Line;
                profit.Color = Color.White;   //DeepSkyBlue;
                profit.YAxisType = AxisType.Secondary;
                profit.ChartArea = "ChartAreaProfit";
                profit.BorderWidth = 4;
                profit.ShadowOffset = 2;

                Series profitLong = new Series("SeriesProfitLong");
                profitLong.ChartType = SeriesChartType.Line;
                profitLong.Color = Color.DeepSkyBlue;   //DeepSkyBlue;
                profitLong.YAxisType = AxisType.Secondary;
                profitLong.ChartArea = "ChartAreaProfit";
                profitLong.BorderWidth = 2;
                profitLong.ShadowOffset = 2;

                Series profitShort = new Series("SeriesProfitShort");
                profitShort.ChartType = SeriesChartType.Line;
                profitShort.Color = Color.DarkOrange;  //DeepSkyBlue;
                profitShort.YAxisType = AxisType.Secondary;
                profitShort.ChartArea = "ChartAreaProfit";
                profitShort.ShadowOffset = 2;
                profitShort.BorderWidth = 2;

                Series profitBar = new Series("SeriesProfitBar");
                profitBar.ChartType = SeriesChartType.Column;
                profitBar.YAxisType = AxisType.Secondary;
                profitBar.ChartArea = "ChartAreaProfitBar";
                profitBar.ShadowOffset = 2;

                Series nullLine = new Series("SeriesNullLine");
                nullLine.ChartType = SeriesChartType.Line;
                nullLine.YAxisType = AxisType.Secondary;
                nullLine.ChartArea = "ChartAreaProfit";
                nullLine.ShadowOffset = 0;


                decimal profitSum = 0;
                decimal profitSumLong = 0;
                decimal profitSumShort = 0;
                decimal maxYVal = decimal.MinValue;
                decimal minYval = decimal.MaxValue;
                decimal curProfit = 0;
                decimal maxYValBars = 0;
                decimal minYValBars = decimal.MaxValue;

                string chartType = ComboBoxChartType.SelectedItem.ToString();

                for (int i = 0; i < positionsAll.Count; i++)
                {

                    if (chartType == "Absolute")
                    {
                        curProfit = positionsAll[i].ProfitPortfolioAbs;
                    }
                    else if (chartType == "Percent 1 contract")
                    {
                        curProfit = positionsAll[i].ProfitOperationPercent;
                    }

                    profitSum += curProfit;
                    profit.Points.AddXY(i, Math.Round(profitSum, 3));
                    profit.Points[profit.Points.Count - 1].AxisLabel =
                        positionsAll[i].TimeCreate.ToString(_currentCulture);

                    profitBar.Points.AddXY(i, Math.Round(curProfit, 3));

                    if (curProfit > maxYValBars)
                    {
                        maxYValBars = curProfit;
                    }
                    if (curProfit < minYValBars)
                    {
                        minYValBars = curProfit;
                    }

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
                    profitShort.Points.AddXY(i, Math.Round(profitSumShort, 3));

                    if (curProfit > 0)
                    {
                        profitBar.Points[profitBar.Points.Count - 1].Color = Color.Gainsboro;
                    }
                    else
                    {
                        profitBar.Points[profitBar.Points.Count - 1].Color = Color.DarkRed;
                    }
                }

                _chartEquity.Series.Add(profit);
                _chartEquity.Series.Add(profitLong);
                _chartEquity.Series.Add(profitShort);
                _chartEquity.Series.Add(profitBar);

                nullLine.Points.AddXY(0, 0);
                nullLine.Points.AddXY(positionsAll.Count, 0);

                _chartEquity.Series.Add(nullLine);

                if (minYval != decimal.MaxValue &&
                     maxYVal != decimal.MinValue)
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

                if (maxYValBars != 0 &&
                    minYValBars != decimal.MaxValue &&
                    maxYValBars != minYValBars)
                {
                    decimal chartHeigh = maxYValBars - minYValBars;
                    maxYValBars = Math.Round(maxYValBars + chartHeigh * 0.05m, 5);
                    minYValBars = Math.Round(minYValBars - chartHeigh * 0.05m, 5);

                    if (maxYValBars != minYValBars)
                    {
                        _chartEquity.ChartAreas[1].AxisY2.Maximum = (double)maxYValBars;
                        _chartEquity.ChartAreas[1].AxisY2.Minimum = (double)minYValBars;
                    }
                }
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        private void _chartEquity_Click(object sender, EventArgs e)
        {
            try
            {
                if (double.IsNaN(_chartEquity.ChartAreas[0].CursorX.Position) ||
                 _chartEquity.ChartAreas[0].CursorX.Position == 0)
                {
                    return;
                }

                for (int i = 0; i < _chartEquity.Series.Count; i++)
                {
                    if (_chartEquity.Series[i].Points.Count == 1)
                    {
                        continue;
                    }
                    for (int i2 = 0; i2 < _chartEquity.Series[i].Points.Count; i2++)
                    {
                        _chartEquity.Series[i].Points[i2].Label = "";
                    }
                }

                for (int i = 0; i < _chartEquity.Series.Count; i++)
                {
                    if (_chartEquity.Series[i].Points.Count == 1)
                    {
                        continue;
                    }
                    string label = "";

                    int index = Convert.ToInt32(_chartEquity.ChartAreas[0].CursorX.Position) - 1;

                    if (index >= _chartEquity.Series[i].Points.Count)
                    {
                        return;
                    }

                    if (!string.IsNullOrWhiteSpace(_chartEquity.Series[i].Points[index].AxisLabel))
                    {
                        label += _chartEquity.Series[i].Points[index].AxisLabel + "\n";
                    }

                    label += _chartEquity.Series[i].Points[index].YValues[0];

                    _chartEquity.Series[i].Points[index].Label = label;
                    _chartEquity.Series[i].Points[index].LabelForeColor = _chartEquity.Series[i].Points[index].Color;
                    _chartEquity.Series[i].Points[index].LabelBackColor = Color.FromArgb(17, 18, 23);
                }
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        #endregion

        #region Volume Chart

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

        #region Max DD Chart

        Chart _chartDd;

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
                _chartDd.Click += _chartDd_Click;

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
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

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

        private void _chartDd_Click(object sender, EventArgs e)
        {
            try
            {
                if (double.IsNaN(_chartDd.ChartAreas[0].CursorX.Position) ||
                _chartDd.ChartAreas[0].CursorX.Position == 0)
                {
                    return;
                }

                for (int i = 0; i < _chartDd.Series.Count; i++)
                {
                    if (_chartDd.Series[i].Points.Count == 1)
                    {
                        continue;
                    }
                    for (int i2 = 0; i2 < _chartDd.Series[i].Points.Count; i2++)
                    {
                        _chartDd.Series[i].Points[i2].Label = "";
                    }
                }

                for (int i = 0; i < _chartDd.Series.Count; i++)
                {
                    if (_chartDd.Series[i].Points.Count == 1)
                    {
                        continue;
                    }
                    string label = "";

                    int index = Convert.ToInt32(_chartDd.ChartAreas[0].CursorX.Position) - 1;

                    if (index >= _chartDd.Series[i].Points.Count)
                    {
                        return;
                    }

                    if (!string.IsNullOrWhiteSpace(_chartDd.Series[i].Points[index].AxisLabel))
                    {
                        label += _chartDd.Series[i].Points[index].AxisLabel + "\n";
                    }

                    label += _chartDd.Series[i].Points[index].YValues[0];

                    _chartDd.Series[i].Points[index].Label = label;
                    _chartDd.Series[i].Points[index].LabelForeColor = _chartDd.Series[i].Points[index].Color;
                    _chartDd.Series[i].Points[index].LabelBackColor = Color.Black;
                }
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
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
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
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
            try
            {
                int number;
                try
                {
                    number = Convert.ToInt32(_openPositionGrid.Rows[_openPositionGrid.CurrentCell.RowIndex].Cells[0].Value);
                }
                catch (Exception)
                {
                    return;
                }

                ShowPositionDialog(number);
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
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

                    var item = new ToolStripMenuItem(OsLocalization.Journal.PositionMenuItem13);
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
            try
            {
                int number;
                try
                {
                    number = Convert.ToInt32(_openPositionGrid.Rows[_openPositionGrid.CurrentCell.RowIndex].Cells[0].Value);
                }
                catch (Exception)
                {
                    return;
                }

                ShowPositionDialog(number);
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        private void OpenDealDelete_Click(object sender, EventArgs e)
        {
            try
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

                RePaint();
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        private void OpenDealClearAll_Click(object sender, EventArgs e)
        {
            try
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
                RePaint();
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
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

                int number = ((MenuItem)sender).MergeIndex;

                string botName = _botsJournals[number].BotName;

                Position newPos = new Position();

                newPos.Number = NumberGen.GetNumberDeal(_startProgram);
                newPos.NameBot = botName;
                _botsJournals[number]._Tabs[0].Journal.SetNewDeal(newPos);

                RePaint();
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
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
                _openPositionGrid.Rows.Clear();

                for (int i = 0; i < positionsAll.Count; i++)
                {
                    if (positionsAll[i].State != PositionStateType.Done &&
                        positionsAll[i].State != PositionStateType.OpeningFail)
                    {
                        _openPositionGrid.Rows.Insert(0, GetRow(positionsAll[i]));
                    }
                }
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
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        private void PaintClosePositionGrid(List<Position> positionsAll)
        {
            try
            {
                if (_closePositionGrid == null)
                {
                    CreateClosePositionTable();
                }
                _closePositionGrid.Rows.Clear();

                if (positionsAll.Count == 0)
                {
                    return;
                }

                List<Position> closePositions = new List<Position>();

                for (int i = 0; i < positionsAll.Count; i++)
                {
                    if (positionsAll[i].State == PositionStateType.Done ||
                        positionsAll[i].State == PositionStateType.OpeningFail)
                    {
                        closePositions.Add(positionsAll[i]);
                    }
                }

                if (closePositions.Count == 0)
                {
                    return;
                }

                for (int i = 0; i < closePositions.Count; i++)
                {
                    for (int i2 = i; i2 < closePositions.Count; i2++)
                    {
                        if (closePositions[i].TimeClose > closePositions[i2].TimeClose)
                        {
                            Position pos = closePositions[i2];
                            closePositions[i2] = closePositions[i];
                            closePositions[i] = pos;
                        }
                    }
                }

                for (int i = 0; i < closePositions.Count; i++)
                {
                    _closePositionGrid.Rows.Insert(0, GetRow(closePositions[i]));
                }
            }
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
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
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
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

                MenuItem[] items = new ToolStripMenuItem[4];

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
            try
            {
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
            catch (Exception error)
            {
                SendNewLogMessage(error.ToString(), LogMessageType.Error);
            }
        }

        private void CloseDealDelete_Click(object sender, EventArgs e)
        {
            try
            {
                int number;
                try
                {
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
                RePaint();
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

    internal class CustomDataGridViewCell : DataGridViewTextBoxCell
    {
        private DataGridViewAdvancedBorderStyle _style;

        public CustomDataGridViewCell()
            : base()
        {
            _style = new DataGridViewAdvancedBorderStyle();
            _style.Bottom = DataGridViewAdvancedCellBorderStyle.None;
            _style.Top = DataGridViewAdvancedCellBorderStyle.None;
            _style.Left = DataGridViewAdvancedCellBorderStyle.None;
            _style.Right = DataGridViewAdvancedCellBorderStyle.None;
        }

        public DataGridViewAdvancedBorderStyle AdvancedBorderStyle
        {
            get { return _style; }
            set
            {
                _style.Bottom = value.Bottom;
                _style.Top = value.Top;
                _style.Left = value.Left;
                _style.Right = value.Right;
            }
        }

        protected override void PaintBorder(Graphics graphics, Rectangle clipBounds, Rectangle bounds,
            DataGridViewCellStyle cellStyle, DataGridViewAdvancedBorderStyle advancedBorderStyle)
        {
            base.PaintBorder(graphics, clipBounds, bounds, cellStyle, _style);
        }

        protected override void Paint(Graphics graphics, Rectangle clipBounds, Rectangle cellBounds, int rowIndex,
            DataGridViewElementStates cellState, object value, object formattedValue, string errorText,
            DataGridViewCellStyle cellStyle, DataGridViewAdvancedBorderStyle advancedBorderStyle,
            DataGridViewPaintParts paintParts)
        {
            base.Paint(graphics, clipBounds, cellBounds, rowIndex, cellState, value, formattedValue, errorText,
                cellStyle, _style, paintParts);
        }

    }

    public class VolumeSecurity
    {
        public List<decimal> Volume;

        public string Security;
    }
}